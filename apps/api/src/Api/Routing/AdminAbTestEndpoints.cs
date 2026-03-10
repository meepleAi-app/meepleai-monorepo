using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AbTest;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for A/B test session management.
/// Issue #5497: A/B Test backend API endpoints.
/// </summary>
internal static class AdminAbTestEndpoints
{
    public static RouteGroupBuilder MapAdminAbTestEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/v1/admin/ab-tests
        // Create a new A/B test session and generate model responses
        group.MapPost("/", async (
            CreateAbTestRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {UserId} creating A/B test with {ModelCount} models",
                session!.User!.Id,
                request.ModelIds.Count);

            var command = new CreateAbTestCommand(
                CreatedBy: session.User.Id,
                Query: request.Query,
                ModelIds: request.ModelIds,
                KnowledgeBaseId: request.KnowledgeBaseId);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "A/B test session {SessionId} created by admin {UserId}",
                result.Id,
                session.User.Id);

            return Results.Created($"/api/v1/admin/ab-tests/{result.Id}", result);
        })
        .WithName("AdminCreateAbTest")
        .WithTags("AbTest", "Admin")
        .WithSummary("Create a new A/B test session (Admin)")
        .WithDescription("Creates an A/B test session with parallel model response generation. Requires budget and rate limit availability.")
        .Produces<AbTestSessionDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/ab-tests
        // List A/B test sessions (paginated)
        group.MapGet("/", async (
            HttpContext context,
            IMediator mediator,
            int page = 1,
            int pageSize = 20,
            string? status = null,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAbTestsQuery(
                UserId: session!.User!.Id,
                Page: page,
                PageSize: pageSize,
                Status: status);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("AdminGetAbTests")
        .WithTags("AbTest", "Admin")
        .WithSummary("List A/B test sessions (Admin)")
        .WithDescription("Returns a paginated list of A/B test sessions for the current admin user. Optionally filter by status.")
        .Produces<AbTestSessionListDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/ab-tests/analytics
        // Get aggregated A/B test analytics (must be before {id} to avoid route conflict)
        group.MapGet("/analytics", async (
            HttpContext context,
            IMediator mediator,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAbTestAnalyticsQuery(
                DateFrom: dateFrom,
                DateTo: dateTo);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("AdminGetAbTestAnalytics")
        .WithTags("AbTest", "Admin")
        .WithSummary("Get A/B test analytics (Admin)")
        .WithDescription("Returns aggregated analytics: win rates, average scores per model, and cost totals.")
        .Produces<AbTestAnalyticsDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/ab-tests/{id}
        // Get a single A/B test session (blind mode)
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAbTestQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result is null
                ? Results.NotFound(new { error = "A/B test session not found" })
                : Results.Ok(result);
        })
        .WithName("AdminGetAbTest")
        .WithTags("AbTest", "Admin")
        .WithSummary("Get A/B test session in blind mode (Admin)")
        .WithDescription("Returns an A/B test session without revealing which model generated which response.")
        .Produces<AbTestSessionDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/ab-tests/{id}/evaluate
        // Submit evaluation scores for variants
        group.MapPost("/{id:guid}/evaluate", async (
            Guid id,
            EvaluateAbTestRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {UserId} evaluating A/B test {SessionId} with {EvalCount} evaluations",
                session!.User!.Id,
                id,
                request.Evaluations.Count);

            var command = new EvaluateAbTestCommand(
                SessionId: id,
                EvaluatorId: session.User.Id,
                Evaluations: request.Evaluations.Select(e => new VariantEvaluationInput(
                    Label: e.Label,
                    Accuracy: e.Accuracy,
                    Completeness: e.Completeness,
                    Clarity: e.Clarity,
                    Tone: e.Tone,
                    Notes: e.Notes)).ToList());

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "A/B test {SessionId} evaluated by admin {UserId}",
                id,
                session.User.Id);

            return Results.Ok(result);
        })
        .WithName("AdminEvaluateAbTest")
        .WithTags("AbTest", "Admin")
        .WithSummary("Evaluate A/B test variants (Admin)")
        .WithDescription("Submits evaluation scores (accuracy, completeness, clarity, tone) for each variant. Auto-completes the session when all variants are evaluated.")
        .Produces<AbTestSessionRevealedDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/ab-tests/{id}/reveal
        // Reveal model info (only after evaluation)
        group.MapGet("/{id:guid}/reveal", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new RevealAbTestQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result is null
                ? Results.NotFound(new { error = "A/B test session not found" })
                : Results.Ok(result);
        })
        .WithName("AdminRevealAbTest")
        .WithTags("AbTest", "Admin")
        .WithSummary("Reveal A/B test models (Admin)")
        .WithDescription("Returns the A/B test session with model identities revealed. Only available after evaluation is complete.")
        .Produces<AbTestSessionRevealedDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }
}

// Request DTOs

/// <summary>
/// Request to create a new A/B test session.
/// </summary>
internal sealed record CreateAbTestRequest(
    string Query,
    List<string> ModelIds,
    Guid? KnowledgeBaseId = null);

/// <summary>
/// Request to evaluate A/B test variants.
/// </summary>
internal sealed record EvaluateAbTestRequest(
    List<VariantEvaluationRequest> Evaluations);

/// <summary>
/// Evaluation input for a single variant.
/// </summary>
internal sealed record VariantEvaluationRequest(
    string Label,
    int Accuracy,
    int Completeness,
    int Clarity,
    int Tone,
    string? Notes = null);
