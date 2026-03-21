using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;
using Api.Extensions;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for AgentDefinition management (AI Lab).
/// Issue #3809 (Epic #3687): Agent Builder UI backend.
/// </summary>
internal static class AdminAgentDefinitionEndpoints
{
    public static void MapAdminAgentDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/agent-definitions")
            .WithTags("Admin - Agent Definitions")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // POST /api/v1/admin/agent-definitions
        // Create new agent definition
        group.MapPost("/", async (
            CreateAgentDefinitionRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {

            logger.LogInformation(
                "Admin creating AgentDefinition: {Name}",
                request.Name);

            var command = new CreateAgentDefinitionCommand(
                Name: request.Name,
                Description: request.Description,
                Type: request.Type,
                Model: request.Model,
                MaxTokens: request.MaxTokens,
                Temperature: request.Temperature,
                StrategyName: request.StrategyName,
                StrategyParameters: request.StrategyParameters,
                Prompts: request.Prompts,
                Tools: request.Tools);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "AgentDefinition created: {Id}, Model: {Model}",
                result.Id,
                result.Config.Model);

            return Results.Created($"/api/v1/admin/agent-definitions/{result.Id}", result);
        })
        .WithName("AdminCreateAgentDefinition")
        .WithTags("AgentDefinition", "Admin")
        .WithSummary("Create agent definition (Admin - AI Lab)")
        .WithDescription("Create a new AI agent definition for the AI Lab");

        // GET /api/v1/admin/agent-definitions
        // List all agent definitions with optional filters
        group.MapGet("/", async (
            [AsParameters] AgentDefinitionListRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {

            var query = new GetAllAgentDefinitionsQuery(ActiveOnly: request.ActiveOnly);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var searchQuery = new SearchAgentDefinitionsQuery(request.Search);
                result = await mediator.Send(searchQuery, ct).ConfigureAwait(false);
            }

            return Results.Ok(result);
        })
        .WithName("AdminGetAgentDefinitions")
        .WithTags("AgentDefinition", "Admin")
        .WithSummary("List agent definitions (Admin - AI Lab)")
        .WithDescription("Get all agent definitions with optional filters");

        // GET /api/v1/admin/agent-definitions/{id}
        // Get single agent definition by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {

            var query = new GetAgentDefinitionByIdQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result != null
                ? Results.Ok(result)
                : Results.NotFound(new { message = $"AgentDefinition {id} not found" });
        })
        .WithName("AdminGetAgentDefinitionById")
        .WithTags("AgentDefinition", "Admin")
        .WithSummary("Get agent definition by ID (Admin - AI Lab)")
        .WithDescription("Get a specific agent definition by its ID");

        // PUT /api/v1/admin/agent-definitions/{id}
        // Update existing agent definition
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateAgentDefinitionRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Admin updating AgentDefinition: {Id}",
                id);

            var command = new UpdateAgentDefinitionCommand(
                Id: id,
                Name: request.Name,
                Description: request.Description,
                Type: request.Type,
                Model: request.Model,
                MaxTokens: request.MaxTokens,
                Temperature: request.Temperature,
                StrategyName: request.StrategyName,
                StrategyParameters: request.StrategyParameters,
                Prompts: request.Prompts,
                Tools: request.Tools);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "AgentDefinition updated: {Id}",
                result.Id);

            return Results.Ok(result);
        })
        .WithName("AdminUpdateAgentDefinition")
        .WithTags("AgentDefinition", "Admin")
        .WithSummary("Update agent definition (Admin - AI Lab)")
        .WithDescription("Update an existing agent definition");

        // DELETE /api/v1/admin/agent-definitions/{id}
        // Delete agent definition
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Admin deleting AgentDefinition: {Id}",
                id);

            var command = new DeleteAgentDefinitionCommand(id);
            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "AgentDefinition deleted: {Id}",
                id);

            return Results.NoContent();
        })
        .WithName("AdminDeleteAgentDefinition")
        .WithTags("AgentDefinition", "Admin")
        .WithSummary("Delete agent definition (Admin - AI Lab)")
        .WithDescription("Delete an agent definition");

        // POST /api/v1/admin/agent-definitions/{id}/start-testing
        // Transition agent definition to Testing status
        group.MapPost("/{id:guid}/start-testing", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new StartTestingAgentDefinitionCommand(id), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("AdminStartTestingAgentDefinition")
        .WithTags("AgentDefinition", "Admin")
        .WithSummary("Start testing agent definition (Admin - AI Lab)")
        .WithDescription("Transition an agent definition from Draft to Testing status");

        // POST /api/v1/admin/agent-definitions/{id}/publish
        // Publish agent definition
        group.MapPost("/{id:guid}/publish", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new PublishAgentDefinitionCommand(id), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("AdminPublishAgentDefinition")
        .WithTags("AgentDefinition", "Admin")
        .WithSummary("Publish agent definition (Admin - AI Lab)")
        .WithDescription("Publish an agent definition, making it visible to regular users");

        // POST /api/v1/admin/agent-definitions/{id}/unpublish
        // Unpublish agent definition
        group.MapPost("/{id:guid}/unpublish", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new UnpublishAgentDefinitionCommand(id), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("AdminUnpublishAgentDefinition")
        .WithTags("AgentDefinition", "Admin")
        .WithSummary("Unpublish agent definition (Admin - AI Lab)")
        .WithDescription("Unpublish an agent definition, returning it to Draft status");

        // GET /api/v1/admin/agent-definitions/catalog-stats
        // Get agent catalog usage statistics aggregated from execution data
        // Issue #3713: Agent Catalog & Usage Stats
        group.MapGet("/catalog-stats", async (
            [AsParameters] AgentCatalogStatsRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetAgentCatalogStatsQuery
            {
                Range = request.Range ?? "30d",
                AgentDefinitionId = request.AgentDefinitionId
            };

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("AdminGetAgentCatalogStats")
        .WithTags("AgentDefinition", "Admin", "Stats", "Catalog")
        .WithSummary("Get agent catalog usage statistics (Admin - AI Lab)")
        .WithDescription("Get usage statistics for all agents aggregated from RAG execution data, including per-agent time series for charts");

        // GET /api/v1/admin/agent-definitions/stats
        // Get aggregated statistics for all agent definitions
        group.MapGet("/stats", async (
            [AsParameters] AgentDefinitionStatsRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetAgentDefinitionStatsQuery
            {
                ActiveOnly = request.ActiveOnly
            };

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("AdminGetAgentDefinitionStats")
        .WithTags("AgentDefinition", "Admin", "Stats")
        .WithSummary("Get agent definitions statistics (Admin - AI Lab)")
        .WithDescription("Get aggregated statistics for all agent definitions including type distribution and recent templates");
    }
}

// Request DTOs

internal sealed record CreateAgentDefinitionRequest(
    string Name,
    string Description,
    string Type,
    string Model,
    int MaxTokens,
    float Temperature,
    string? StrategyName,
    Dictionary<string, object>? StrategyParameters,
    List<PromptTemplateDto>? Prompts,
    List<ToolConfigDto>? Tools);

internal sealed record UpdateAgentDefinitionRequest(
    string Name,
    string Description,
    string Type,
    string Model,
    int MaxTokens,
    float Temperature,
    string? StrategyName,
    Dictionary<string, object>? StrategyParameters,
    List<PromptTemplateDto>? Prompts,
    List<ToolConfigDto>? Tools);

internal sealed record AgentDefinitionListRequest(
    bool ActiveOnly = false,
    string? Search = null);

internal sealed record AgentDefinitionStatsRequest(
    bool ActiveOnly = false);

internal sealed record AgentCatalogStatsRequest(
    string? Range = "30d",
    Guid? AgentDefinitionId = null);
