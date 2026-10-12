using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;
using MediatR;

namespace Api.Routing;

/// <summary>Spec 2026-10-11 §7.1: l'esecutore di valutazione dell'assistente in partita (solo admin).</summary>
internal static class AdminSessionAssistantEndpoints
{
    public static void MapAdminSessionAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/session-assistant")
            .WithTags("Admin - Session Assistant")
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin", "Admin"));

        // POST /api/v1/admin/session-assistant/evaluations
        group.MapPost("/evaluations", async (RunSessionAssistantEvaluationRequest body, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new RunSessionAssistantEvaluationCommand(
                body.Scenarios,
                body.PrimaryModelId,
                body.FallbackModelId,
                body.Temperature ?? 0.2,
                body.MaxTokens ?? 600,
                body.MaxToolIterations ?? 4,
                body.Prompts), ct).ConfigureAwait(false)))
            .WithName("RunSessionAssistantEvaluation")
            .WithSummary("Run the in-game assistant scenario set against a model (Admin)")
            .Produces<AssistantEvaluationReport>(StatusCodes.Status200OK);
    }

    internal sealed record RunSessionAssistantEvaluationRequest(
        AssistantScenarioSet Scenarios,
        string PrimaryModelId,
        string? FallbackModelId,
        double? Temperature,
        int? MaxTokens,
        int? MaxToolIterations,
        AssistantPromptOverride? Prompts = null);
}
