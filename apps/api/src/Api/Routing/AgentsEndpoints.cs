using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPopularAgents;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing Agents endpoints (read-only listing + by-id lookup + recent widget + readiness status).
/// Issue #641 (Wave B.2 hotfix): expose existing GetAllAgentsQuery handler over HTTP
/// so the frontend <c>useAgents</c> hook can resolve agents at <c>GET /api/v1/agents</c>.
/// Issue #647 (Phase γ.1): expose <c>GET /api/v1/agents/{id}</c> for the single-agent
/// detail surface consumed by the frontend <c>agentsClient.getById</c> helper.
/// Issue #650 (Phase γ.3): expose <c>GET /api/v1/agents/recent</c> for the dashboard
/// recent-agents widget consumed by the frontend <c>useRecentAgents</c> hook.
/// Issue #648 (Phase γ.2): expose <c>GET /api/v1/agents/{id}/status</c> for the
/// readiness widget consumed by the frontend <c>useAgentStatus</c> hook.
/// Issue #654 (Phase β.2): expose <c>POST /api/v1/agents/user</c> for the
/// frontend <c>agentsClient.createUserAgent</c> helper. MVP defers documentIds
/// linking and tier/quota validation.
/// Issue #655 (Phase β.3): expose <c>POST /api/v1/agents/create-with-setup</c>
/// orchestration route consumed by the AgentCreationSheet wizard. Optional
/// addToCollection step + agent creation (β.2 reuse). Placeholder threadId
/// (chat-thread BC integration deferred) + slotUsed=0 (Issue #4771 deferred).
/// Issue #659 (Phase δ.1): expose <c>POST /api/v1/agents/quick-create</c>
/// for the frontend <c>agentsClient.quickCreateTutor</c> helper. Reuses
/// <c>CreateUserAgentCommand</c> (β.2) with hardcoded <c>AgentType = "Tutor"</c>;
/// auto-derived name <c>"Tutor for {GameName}"</c>; placeholder chat-thread + KB count.
/// </summary>
internal static class AgentsEndpoints
{
    public static RouteGroupBuilder MapAgentsEndpoints(this RouteGroupBuilder group)
    {
        MapGetAgentsEndpoint(group);
        // Recent must be registered before the {id:guid} route so that the literal "recent"
        // segment never reaches the GUID-constrained handler. The :guid constraint already
        // disambiguates at the matcher level, but explicit ordering keeps intent obvious.
        MapGetRecentAgentsEndpoint(group);
        // Popular: same literal-before-{id:guid} ordering rationale as recent.
        // Wave 3 Phase 1 (#805) — /discover "Popular agents" rail.
        MapGetPopularAgentsEndpoint(group);
        MapGetAgentByIdEndpoint(group);
        MapGetAgentStatusEndpoint(group);
        MapCreateUserAgentEndpoint(group);
        MapCreateAgentWithSetupEndpoint(group);
        MapQuickCreateAgentEndpoint(group);
        MapUpdateUserAgentEndpoint(group);
        MapGetAgentConfigurationEndpoint(group);
        MapUpdateAgentConfigurationEndpoint(group);
        // Issue #904: SG3 — Agent CRUD lifecycle + soft-delete cascade
        MapSoftDeleteUserAgentEndpoint(group);
        MapRestoreUserAgentEndpoint(group);
        MapStartTestingUserAgentEndpoint(group);
        MapPublishUserAgentEndpoint(group);
        MapUnpublishUserAgentEndpoint(group);
        return group;
    }

    /// <summary>
    /// Frontend <c>agentsClient.createUserAgent</c> request body shape.
    /// Mirrors <c>CreateUserAgentRequest</c> in <c>apps/web/src/lib/api/clients/agentsClient.ts</c>.
    /// </summary>
    private sealed record CreateUserAgentRequest(
        Guid GameId,
        string AgentType,
        string? Name = null,
        string? StrategyName = null,
        Dictionary<string, object>? StrategyParameters = null,
        List<Guid>? DocumentIds = null
    );

    /// <summary>
    /// Frontend <c>agentsClient.createWithSetup</c> request body shape.
    /// Mirrors <c>CreateAgentWithSetupRequest</c> in
    /// <c>apps/web/src/lib/api/clients/agentsClient.ts</c>. Issue #655 (Phase β.3).
    /// </summary>
    private sealed record CreateAgentWithSetupRequest(
        Guid GameId,
        bool AddToCollection,
        string AgentType,
        string? AgentName = null,
        string? StrategyName = null,
        Dictionary<string, object>? StrategyParameters = null,
        List<Guid>? DocumentIds = null
    );

    /// <summary>
    /// Frontend <c>agentsClient.quickCreateTutor</c> request body shape.
    /// Mirrors the <c>{ gameId, sharedGameId? }</c> body posted by
    /// <c>apps/web/src/lib/api/clients/agentsClient.ts</c>. Issue #659 (Phase δ.1).
    /// <c>SharedGameId</c> is informational only (alias of <c>GameId</c> in current schema).
    /// </summary>
    private sealed record QuickCreateAgentRequest(
        Guid GameId,
        Guid? SharedGameId = null
    );

    /// <summary>
    /// Frontend <c>agentsClient.updateUserAgent</c> request body shape.
    /// Mirrors <c>UpdateUserAgentRequest</c> in
    /// <c>apps/web/src/lib/api/clients/agentsClient.ts</c>. Issue #656.
    /// All fields optional — handler treats missing/blank values as no-op for that field.
    /// </summary>
    private sealed record UpdateUserAgentRequest(
        string? Name = null,
        string? StrategyName = null,
        Dictionary<string, object>? StrategyParameters = null
    );

    /// <summary>
    /// Frontend <c>agentsClient.updateAgentConfiguration</c> request body shape.
    /// Mirrors <c>UpdateAgentConfigurationRequest</c> in
    /// <c>apps/web/src/lib/api/clients/agentsClient.ts</c>. Issue #658.
    /// </summary>
    private sealed record UpdateAgentConfigurationRequest(
        string? ModelId = null,
        decimal? Temperature = null,
        int? MaxTokens = null,
        List<Guid>? SelectedDocumentIds = null
    );

    private static void MapGetAgentsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents", async (
            [FromQuery] bool? activeOnly,
            [FromQuery] string? type,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetAllAgentsQuery(
                ActiveOnly: activeOnly,
                Type: type
            );
            var agents = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                success = true,
                agents,
                count = agents.Count
            });
        })
        .RequireAuthenticatedUser()
        .Produces(200)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("List all agents")
        .WithDescription("Returns all agents with optional activeOnly and type filters. Issue #641 Wave B.2 hotfix.")
        .WithOpenApi();
    }

    private static void MapGetAgentByIdEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/{id:guid}", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetAgentByIdQuery(id);
            var dto = await mediator.Send(query, ct).ConfigureAwait(false);

            return dto is null ? Results.NotFound() : Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<AgentDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Get agent by ID")
        .WithDescription("Returns a single agent definition by id, mapped to AgentDto with GameName resolved via SharedGame catalog (PR #662 drift-fix). Issue #647 (Phase γ.1).")
        .WithOpenApi();
    }

    private static void MapGetRecentAgentsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/recent", async (
            [FromQuery] int? limit,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetRecentAgentsQuery(Limit: limit ?? 10);
            var agents = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(agents);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<AgentDto>>(200)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("Get recently used agents")
        .WithDescription("Returns active agents ordered by LastInvokedAt desc (limit clamped 1..50, default 10). Powers the dashboard recent-agents widget (frontend useRecentAgents hook). Issue #650 (Phase γ.3).")
        .WithOpenApi();
    }

    /// <summary>
    /// Wave 3 Phase 1 (PR #732 §4.3.3 / Issue #805): expose
    /// <c>GET /api/v1/agents/popular</c> for the SP4 /discover route's
    /// "Popular agents" rail (frontend <c>useDiscoverPopularAgents</c>).
    /// Schema reality v1 carryover: <c>InstallCount</c> always 0 — see
    /// <see cref="PopularAgentDto"/> for the full Gate B note.
    /// </summary>
    private static void MapGetPopularAgentsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/popular", async (
            [FromQuery] int? limit,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var safeLimit = limit.GetValueOrDefault(10);
            if (safeLimit < 1) safeLimit = 10;
            if (safeLimit > 50) safeLimit = 50;

            var query = new GetPopularAgentsQuery(safeLimit);
            var agents = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(new DiscoverItemsEnvelope<PopularAgentDto>(agents));
        })
        .RequireAuthenticatedUser()
        .Produces<DiscoverItemsEnvelope<PopularAgentDto>>(200)
        .Produces(401)
        .WithTags("Discover", "Agents")
        .WithSummary("Get popular agents")
        .WithDescription(
            "Returns active agents sorted by installCount DESC + invocationCount DESC tiebreak "
            + "(limit clamped 1..50, default 10). Powers the SP4 /discover \"Popular agents\" "
            + "rail. Cache: 15min via HybridCache. Wave 3 Phase 1 (Issue #805 / PR #732 §4.3.3).")
        .WithOpenApi();
    }

    private static void MapGetAgentStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/{id:guid}/status", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetAgentStatusQuery(id);
            var dto = await mediator.Send(query, ct).ConfigureAwait(false);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<AgentStatusDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Get agent readiness status")
        .WithDescription("Returns readiness derived from AgentDefinition flags (IsActive AND Strategy.Name presence). HasDocuments precise count is deferred to a follow-up if needed. Issue #648 — useAgentStatus widget.")
        .WithOpenApi();
    }

    private static void MapCreateUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/user", async (
            [FromBody] CreateUserAgentRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var command = new CreateUserAgentCommand(
                    UserId: userId,
                    GameId: request.GameId,
                    AgentType: request.AgentType,
                    Name: request.Name,
                    StrategyName: request.StrategyName,
                    StrategyParameters: request.StrategyParameters,
                    DocumentIds: request.DocumentIds
                );

                var dto = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/agents/{dto.Id}", dto);
            }
            catch (TierQuotaExceededException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status402PaymentRequired,
                    title: "Agent Slot Quota Exceeded",
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["errorCode"] = ex.ErrorCode });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<AgentDto>(201)
        .Produces(400)
        .Produces(401)
        .Produces(402)
        .WithTags("Agents")
        .WithSummary("Create a user-owned agent")
        .WithDescription(
            "Creates a custom agent linked to a SharedGame, returning AgentDto with GameName resolved. " +
            "MVP: documentIds parameter accepted but not linked (deferred). " +
            "Returns 402 AGENT_SLOT_QUOTA_EXCEEDED when the user has reached their tier's MaxAgents limit. " +
            "Issue #654 (Phase β.2). Quota enforcement: Issue #904 SG3.")
        .WithOpenApi();
    }

    private static void MapCreateAgentWithSetupEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/create-with-setup", async (
            [FromBody] CreateAgentWithSetupRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var command = new CreateAgentWithSetupCommand(
                    UserId: userId,
                    GameId: request.GameId,
                    AddToCollection: request.AddToCollection,
                    AgentType: request.AgentType,
                    AgentName: request.AgentName,
                    StrategyName: request.StrategyName,
                    StrategyParameters: request.StrategyParameters,
                    DocumentIds: request.DocumentIds);

                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/agents/{result.AgentId}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<CreateAgentWithSetupResult>(201)
        .Produces(400)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("Create agent with optional library add + thread")
        .WithDescription("Orchestrates: optional addToCollection → AddGameToLibraryCommand (idempotent: 'already in' counts as success) → CreateUserAgentCommand → result with placeholder threadId (chat-thread BC integration deferred) and slotUsed=0 (tier/quota Issue #4771 deferred). Issue #655 (Phase β.3).")
        .WithOpenApi();
    }

    private static void MapQuickCreateAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/quick-create", async (
            [FromBody] QuickCreateAgentRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var command = new QuickCreateAgentCommand(
                    UserId: userId,
                    GameId: request.GameId);

                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/agents/{result.AgentId}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<QuickCreateAgentResult>(201)
        .Produces(400)
        .Produces(401)
        .WithTags("Agents")
        .WithSummary("Quick-create a Tutor agent")
        .WithDescription("Fast 1-click Tutor onboarding: creates AgentDefinition with type=Tutor and HybridSearch strategy. Auto-derived name 'Tutor for {GameName}'. ChatThreadId placeholder (chat-thread BC integration deferred — same as PR #693) and KbCardCount=0 (KB query deferred). Issue #659 (Phase δ.1).")
        .WithOpenApi();
    }

    private static void MapUpdateUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/agents/{id:guid}/user", async (
            Guid id,
            [FromBody] UpdateUserAgentRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var command = new UpdateUserAgentCommand(
                    UserId: userId,
                    AgentId: id,
                    Name: request.Name,
                    StrategyName: request.StrategyName,
                    StrategyParameters: request.StrategyParameters);

                var dto = await mediator.Send(command, ct).ConfigureAwait(false);
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<AgentDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Update user-owned agent")
        .WithDescription("Updates name and/or strategy of a user-owned agent. Returns 404 if agent not found, AgentDto with GameName resolved on success. All request fields optional; missing/blank values are no-ops for that field. Issue #656.")
        .WithOpenApi();
    }

    private static void MapGetAgentConfigurationEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/agents/{id:guid}/configuration", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var query = new GetAgentConfigurationQuery(id);
            var dto = await mediator.Send(query, ct).ConfigureAwait(false);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<AgentConfigurationDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Get agent LLM configuration")
        .WithDescription("Returns the current LLM configuration view (model, provider, temperature, maxTokens, selectedDocumentIds). LlmProvider is heuristically inferred from the model name; SelectedDocumentIds is empty in MVP (KB linking deferred). Issue #657.")
        .WithOpenApi();
    }

    private static void MapUpdateAgentConfigurationEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/agents/{id:guid}/configuration", async (
            Guid id,
            [FromBody] UpdateAgentConfigurationRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            try
            {
                var command = new UpdateAgentConfigurationCommand(
                    AgentId: id,
                    ModelId: request.ModelId,
                    Temperature: request.Temperature,
                    MaxTokens: request.MaxTokens,
                    SelectedDocumentIds: request.SelectedDocumentIds);

                var dto = await mediator.Send(command, ct).ConfigureAwait(false);
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<AgentConfigurationDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Update agent LLM configuration")
        .WithDescription("Partial update (PATCH) of agent LLM configuration: modelId, temperature, maxTokens. Range validation delegated to AgentDefinitionConfig.Create (temperature 0.0-2.0, maxTokens 100-32000). SelectedDocumentIds accepted but not persisted (KB linking deferred). Issue #658.")
        .WithOpenApi();
    }

    // ────────────────────────────────────────────────────────────────────────────────
    // Issue #904: SG3 — Agent CRUD lifecycle + soft-delete cascade
    // ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// DELETE /api/v1/agents/{agentId:guid}
    /// Soft-deletes a user-owned agent and cascades CloseThread() on all active ChatThreads.
    /// System agents (IsSystemDefined=true) are rejected with 403.
    /// Issue #904: SG3.
    /// </summary>
    private static void MapSoftDeleteUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/agents/{agentId:guid}", async (
            Guid agentId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var command = new SoftDeleteUserAgentCommand(UserId: userId, AgentId: agentId);
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (SystemAgentProtectedException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "System Agent Protected",
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["errorCode"] = ex.ErrorCode });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Soft-delete a user-owned agent")
        .WithDescription(
            "Soft-deletes an agent (IsDeleted=true). Cascades CloseThread() on all active ChatThreads linked to the agent. " +
            "System-defined agents (IsSystemDefined=true) return 403 SYSTEM_AGENT_PROTECTED. " +
            "Issue #904: SG3.")
        .WithOpenApi();
    }

    /// <summary>
    /// POST /api/v1/agents/{agentId:guid}/restore
    /// Restores a soft-deleted user-owned agent. ChatThreads that were closed on delete remain closed.
    /// Issue #904: SG3.
    /// </summary>
    private static void MapRestoreUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/{agentId:guid}/restore", async (
            Guid agentId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var command = new RestoreUserAgentCommand(UserId: userId, AgentId: agentId);
                var dto = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(dto);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (BadRequestException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<AgentDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Restore a soft-deleted agent")
        .WithDescription(
            "Restores a previously soft-deleted agent (IsDeleted=false). " +
            "ChatThreads closed during delete remain closed by design. " +
            "Returns 404 if the agent ID is not found at all, 400 if the agent is not soft-deleted. " +
            "Issue #904: SG3.")
        .WithOpenApi();
    }

    /// <summary>
    /// POST /api/v1/agents/{agentId:guid}/start-testing
    /// Transitions the agent from Draft to Testing status.
    /// Reuses the existing admin command <see cref="StartTestingAgentDefinitionCommand"/>.
    /// Issue #904: SG3.
    /// </summary>
    private static void MapStartTestingUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/{agentId:guid}/start-testing", async (
            Guid agentId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            try
            {
                // Reuse the existing admin command — no separate user version needed;
                // the command is a pure state transition without admin-specific logic.
                await mediator.Send(new StartTestingAgentDefinitionCommand(agentId), ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Transition agent Draft → Testing")
        .WithDescription(
            "Transitions a Draft agent to Testing status. Returns 400 if the agent is Published " +
            "(must unpublish first). Returns 404 if not found. Issue #904: SG3.")
        .WithOpenApi();
    }

    /// <summary>
    /// POST /api/v1/agents/{agentId:guid}/publish
    /// Transitions the agent from Testing to Published status.
    /// Reuses the existing admin command <see cref="PublishAgentDefinitionCommand"/>.
    /// Issue #904: SG3.
    /// </summary>
    private static void MapPublishUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/{agentId:guid}/publish", async (
            Guid agentId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            try
            {
                await mediator.Send(new PublishAgentDefinitionCommand(agentId), ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Transition agent Testing → Published")
        .WithDescription(
            "Publishes an agent (makes it visible to all users). Agent must be in Testing status; " +
            "returns 400 if attempted from Draft directly. Issue #904: SG3.")
        .WithOpenApi();
    }

    /// <summary>
    /// POST /api/v1/agents/{agentId:guid}/unpublish
    /// Transitions the agent from Published back to Draft status.
    /// Reuses the existing admin command <see cref="UnpublishAgentDefinitionCommand"/>.
    /// Issue #904: SG3.
    /// </summary>
    private static void MapUnpublishUserAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/agents/{agentId:guid}/unpublish", async (
            Guid agentId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, _, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            try
            {
                await mediator.Send(new UnpublishAgentDefinitionCommand(agentId), ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .WithTags("Agents")
        .WithSummary("Transition agent Published → Draft")
        .WithDescription(
            "Unpublishes an agent, returning it to Draft status and deactivating it. " +
            "Returns 404 if not found. Issue #904: SG3.")
        .WithOpenApi();
    }
}
