using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for automated agent testing.
/// Supports running auto-test suites against game agents after PDF processing.
/// </summary>
internal static class AdminAgentTestEndpoints
{
    internal static RouteGroupBuilder MapAdminAgentTestEndpoints(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/games")
            .WithTags("Admin - Agent Testing")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // POST /api/v1/admin/games/{gameId}/agent/auto-test
        group.MapPost("/{gameId:guid}/agent/auto-test", HandleRunAutoTest)
            .WithName("RunAgentAutoTest")
            .WithSummary("Run automated test suite against game agent (Admin)")
            .WithDescription("Sends 8 standard board game questions to the RAG agent and evaluates response quality, confidence, and latency. Returns a graded quality report.")
            .Produces<AgentAutoTestResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> HandleRunAutoTest(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        var command = new RunAgentAutoTestCommand(
            GameId: gameId,
            RequestedByUserId: userId);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "AutoTest: Completed for game {GameId}. Grade={Grade}, Pass={Passed}/{Total}",
            gameId, result.Report.OverallGrade, result.Report.Passed, result.Report.TotalTests);

        return Results.Ok(result);
    }
}
