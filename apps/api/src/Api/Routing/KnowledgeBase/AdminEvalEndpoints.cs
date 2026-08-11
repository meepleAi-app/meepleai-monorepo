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

        group.MapPost("/merge-labels", HandleMergeLabels)
            .WithName("MergeEvaluationLabels")
            .Produces<string>(200, contentType: "application/json")
            .WithSummary("Merge human-reviewed labeling verdicts into a dataset and persist the labeled dataset to file");
    }

    private static async Task<IResult> HandleRunRetrievalEvaluation(
        RunRetrievalEvaluationRequest request,
        IMediator mediator,
        IEvalDatasetPathResolver pathResolver,
        CancellationToken ct)
    {
        // #3438: datasetPath is resolved against the configured root BEFORE any command sees it.
        // Commands downstream call File.ReadAllTextAsync on whatever they are given, so this
        // endpoint is the trust boundary: everything past it works on an already-sandboxed path.
        var (datasetPath, pathError) = ResolveDatasetForRead(pathResolver, request.DatasetPath);
        if (pathError is not null)
        {
            return pathError;
        }

        var dataset = await mediator.Send(
            new LoadDatasetCommand { FilePath = datasetPath! }, ct).ConfigureAwait(false);

        var result = await mediator.Send(
            new RunEvaluationCommand
            {
                DatasetPath = datasetPath!,
                MaxSamples = request.MaxSamples,
                Enhancements = request.Enhancements
            },
            ct).ConfigureAwait(false);

        var byLanguage = EvaluationReportFormatter.MetricsByLanguage(result, dataset);
        var json = EvaluationReportFormatter.ToJson(result, byLanguage);

        return Results.Text(json, "application/json");
    }

    private static async Task<IResult> HandleGenerateLabelingCandidates(
        GenerateLabelingCandidatesRequest request,
        IMediator mediator,
        IEvalDatasetPathResolver pathResolver,
        CancellationToken ct)
    {
        var (datasetPath, pathError) = ResolveDatasetForRead(pathResolver, request.DatasetPath);
        if (pathError is not null)
        {
            return pathError;
        }

        var review = await mediator.Send(
            new GenerateLabelingCandidatesCommand(datasetPath!, request.TopN ?? 10),
            ct).ConfigureAwait(false);

        return Results.Ok(review);
    }

    private static async Task<IResult> HandleMergeLabels(
        MergeLabelsRequest request,
        IMediator mediator,
        IEvalDatasetPathResolver pathResolver,
        CancellationToken ct)
    {
        var (datasetPath, pathError) = ResolveDatasetForRead(pathResolver, request.DatasetPath);
        if (pathError is not null)
        {
            return pathError;
        }

        // Guard Items too: a body like {"review":{}} deserializes to a non-null Review with a null Items,
        // which would NRE (500) when the handler enumerates it — reject it as a 400 instead.
        if (request.Review?.Items is null)
        {
            return Results.BadRequest(new { error = "review with items is required" });
        }

        // #3438: outputPath is the WRITE side and the more dangerous of the two — an unfiltered
        // value here means writing an attacker-shaped JSON document anywhere the process can write.
        // Sandboxed against the same root; unlike the read side the file need not already exist,
        // since targeting a new labelled dataset is the normal use.
        string outputPath;
        if (request.OutputPath is null)
        {
            // Defaults to applying labels in place, which is already inside the root.
            outputPath = datasetPath!;
        }
        else
        {
            var resolvedOutput = pathResolver.ResolveForWrite(request.OutputPath);
            if (!resolvedOutput.IsSuccess)
            {
                return PathErrorResult(resolvedOutput.Error!.Value, "outputPath");
            }

            outputPath = resolvedOutput.FullPath!;
        }

        var merged = await mediator.Send(
            new MergeLabelsCommand(datasetPath!, request.Review, outputPath),
            ct).ConfigureAwait(false);

        return Results.Text(merged.ToJson(), "application/json");
    }

    /// <summary>
    /// Resolves a read path against the sandbox root and maps the failure to an HTTP result.
    /// Existence is checked here rather than in the resolver so that a 404 can only ever be
    /// returned for a path already proven to be inside the root — otherwise the endpoint would
    /// answer "does this file exist?" for arbitrary paths, which is half the leak it is closing.
    /// </summary>
    private static (string? Path, IResult? Error) ResolveDatasetForRead(
        IEvalDatasetPathResolver resolver,
        string? requestedPath)
    {
        var resolved = resolver.ResolveForRead(requestedPath);
        if (!resolved.IsSuccess)
        {
            return (null, PathErrorResult(resolved.Error!.Value, "datasetPath"));
        }

        if (!File.Exists(resolved.FullPath))
        {
            return (null, Results.NotFound(new { error = $"dataset not found: {requestedPath}" }));
        }

        return (resolved.FullPath, null);
    }

    /// <summary>
    /// Maps a refusal to a response. The message never echoes the resolved absolute path: it would
    /// disclose the server's layout to a caller who just tried to escape it.
    /// </summary>
    private static IResult PathErrorResult(EvalDatasetPathError error, string field) => error switch
    {
        // Server misconfiguration, not a bad request: the subsystem is unavailable until an
        // operator declares where datasets live.
        EvalDatasetPathError.RootNotConfigured => Results.Problem(
            detail: $"Evaluation dataset root is not configured. Set '{EvalDatasetPathResolver.RootConfigKey}' " +
                    $"(or {EvalDatasetPathResolver.RootEnvAlias}) to enable the evaluation endpoints.",
            statusCode: StatusCodes.Status503ServiceUnavailable),

        EvalDatasetPathError.Empty => Results.BadRequest(new { error = $"{field} is required" }),

        EvalDatasetPathError.OutsideRoot => Results.BadRequest(new
        {
            error = $"{field} must be a relative path inside the configured evaluation dataset root"
        }),

        EvalDatasetPathError.NotJson => Results.BadRequest(new { error = $"{field} must be a .json file" }),

        _ => Results.BadRequest(new { error = $"{field} is invalid" }),
    };
}

// #3390 Slice 4 Step 1: Enhancements null → legacy hybrid eval; non-null (incl. empty) → grounded seam
// with the parsed enhancement set (empty = grounded baseline). Identifiers: "crag-evaluation" etc.
internal sealed record RunRetrievalEvaluationRequest(
    string DatasetPath, int? MaxSamples = null, IReadOnlyList<string>? Enhancements = null);
internal sealed record GenerateLabelingCandidatesRequest(string DatasetPath, int? TopN = null);
internal sealed record MergeLabelsRequest(string DatasetPath, LabelingReview Review, string? OutputPath = null);
