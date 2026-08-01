using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Filters;
using MediatR;

namespace Api.Routing.KnowledgeBase;

/// <summary>
/// Admin endpoints for the RAG evaluation suite scaffolding (Issue #3433):
/// running retrieval evaluation against a file-based dataset and generating
/// AI-proposed labeling candidates for human review. CQRS: dispatches only via
/// <see cref="IMediator"/>.Send(...), no DbContext involved (this subsystem is
/// entirely file-based JSON).
/// </summary>
internal static class AdminEvalEndpoints
{
    public static void MapAdminEvalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/eval")
            .WithTags("Admin - RAG Evaluation")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/retrieval", HandleRunRetrievalEvaluation)
            .WithName("RunRetrievalEvaluation")
            .Produces<string>(200, contentType: "application/json")
            .WithSummary("Run RAG retrieval evaluation against a dataset file and return a JSON report (coverage + per-language breakdown)");

        group.MapPost("/labeling-candidates", HandleGenerateLabelingCandidates)
            .WithName("GenerateLabelingCandidates")
            .Produces<LabelingReview>(200)
            .WithSummary("Generate AI-proposed retrieval candidates from a dataset file, awaiting human relevance review");
    }

    private static async Task<IResult> HandleRunRetrievalEvaluation(
        RunRetrievalEvaluationRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        // NOTE: this only rejects a missing/non-existent datasetPath with a 4xx before
        // dispatching any command. Full path sandboxing (restricting datasetPath to an
        // allowlisted root directory) is tracked in issue #3438.
        var pathError = ValidateDatasetPath(request.DatasetPath);
        if (pathError is not null)
        {
            return pathError;
        }

        var dataset = await mediator.Send(
            new LoadDatasetCommand { FilePath = request.DatasetPath }, ct).ConfigureAwait(false);

        var result = await mediator.Send(
            new RunEvaluationCommand { DatasetPath = request.DatasetPath, MaxSamples = request.MaxSamples },
            ct).ConfigureAwait(false);

        var byLanguage = EvaluationReportFormatter.MetricsByLanguage(result, dataset);
        var json = EvaluationReportFormatter.ToJson(result, byLanguage);

        return Results.Text(json, "application/json");
    }

    private static async Task<IResult> HandleGenerateLabelingCandidates(
        GenerateLabelingCandidatesRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        // NOTE: this only rejects a missing/non-existent datasetPath with a 4xx before
        // dispatching any command. Full path sandboxing (restricting datasetPath to an
        // allowlisted root directory) is tracked in issue #3438.
        var pathError = ValidateDatasetPath(request.DatasetPath);
        if (pathError is not null)
        {
            return pathError;
        }

        var review = await mediator.Send(
            new GenerateLabelingCandidatesCommand(request.DatasetPath, request.TopN ?? 10),
            ct).ConfigureAwait(false);

        return Results.Ok(review);
    }

    private static IResult? ValidateDatasetPath(string? datasetPath)
    {
        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            return Results.BadRequest(new { error = "datasetPath is required" });
        }

        if (!File.Exists(datasetPath))
        {
            return Results.NotFound(new { error = $"dataset not found: {datasetPath}" });
        }

        return null;
    }
}

internal sealed record RunRetrievalEvaluationRequest(string DatasetPath, int? MaxSamples = null);
internal sealed record GenerateLabelingCandidatesRequest(string DatasetPath, int? TopN = null);
