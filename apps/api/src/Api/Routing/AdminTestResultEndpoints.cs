using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for agent test result management.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal static class AdminTestResultEndpoints
{
    public static RouteGroupBuilder MapAdminTestResultEndpoints(this RouteGroupBuilder group)
    {
        // ========================================
        // ADMIN: TEST RESULT MANAGEMENT
        // ========================================

        // POST /api/v1/admin/test-results
        // Save a test result
        group.MapPost("/", async (
            SaveTestResultRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {UserId} saving test result for typology {TypologyId}",
                session.User!.Id,
                request.TypologyId);

            var command = new SaveTestResultCommand(
                TypologyId: request.TypologyId,
                Query: request.Query,
                Response: request.Response,
                ModelUsed: request.ModelUsed,
                ConfidenceScore: request.ConfidenceScore,
                TokensUsed: request.TokensUsed,
                CostEstimate: request.CostEstimate,
                LatencyMs: request.LatencyMs,
                ExecutedBy: session.User.Id,
                StrategyOverride: request.StrategyOverride,
                CitationsJson: request.CitationsJson);

            var resultId = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Test result {TestResultId} saved by admin {UserId}",
                resultId,
                session.User.Id);

            return Results.Created($"/api/v1/admin/test-results/{resultId}", new { id = resultId });
        })
        .WithName("AdminSaveTestResult")
        .WithTags("TestResults", "Admin")
        .WithSummary("Save an agent test result (Admin)")
        .WithDescription("Persists an agent test result for history tracking and analysis.")
        .Produces<object>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/test-results
        // Get test results with filters
        group.MapGet("/", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            Guid? typologyId,
            DateTime? from,
            DateTime? to,
            bool? savedOnly,
            int skip = 0,
            int take = 50,
            CancellationToken ct = default) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogDebug(
                "Admin {UserId} getting test results - TypologyId: {TypologyId}, SavedOnly: {SavedOnly}",
                session.User!.Id,
                typologyId,
                savedOnly);

            var query = new GetTestResultsQuery(
                TypologyId: typologyId,
                ExecutedBy: null, // Admins can see all results
                From: from,
                To: to,
                SavedOnly: savedOnly,
                Skip: skip,
                Take: take);

            var results = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(results);
        })
        .WithName("AdminGetTestResults")
        .WithTags("TestResults", "Admin")
        .WithSummary("Get agent test results (Admin)")
        .WithDescription("Retrieves test results with optional filters for typology, date range, and saved status.")
        .Produces<AgentTestResultListDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/test-results/{id}
        // Get a single test result
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogDebug(
                "Admin {UserId} getting test result {TestResultId}",
                session.User!.Id,
                id);

            var query = new GetTestResultByIdQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = "Test result not found" });
            }

            return Results.Ok(result);
        })
        .WithName("AdminGetTestResultById")
        .WithTags("TestResults", "Admin")
        .WithSummary("Get a single agent test result (Admin)")
        .WithDescription("Retrieves a specific test result by ID.")
        .Produces<AgentTestResultDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/test-results/{id}
        // Delete a test result
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "Admin {UserId} deleting test result {TestResultId}",
                session.User!.Id,
                id);

            var command = new DeleteTestResultCommand(id, session.User.Id);
            var deleted = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!deleted)
            {
                return Results.NotFound(new { error = "Test result not found" });
            }

            logger.LogInformation(
                "Test result {TestResultId} deleted by admin {UserId}",
                id,
                session.User.Id);

            return Results.NoContent();
        })
        .WithName("AdminDeleteTestResult")
        .WithTags("TestResults", "Admin")
        .WithSummary("Delete an agent test result (Admin)")
        .WithDescription("Permanently deletes a test result from history.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }
}

// Request DTOs

/// <summary>
/// Request to save a test result.
/// </summary>
internal record SaveTestResultRequest(
    Guid TypologyId,
    string Query,
    string Response,
    string ModelUsed,
    double ConfidenceScore,
    int TokensUsed,
    decimal CostEstimate,
    int LatencyMs,
    string? StrategyOverride = null,
    string? CitationsJson = null
);
