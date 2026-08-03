using MediatR;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Handler for <see cref="GenerateLabelingCandidatesCommand"/>.
/// Loads a dataset and, for every sample, queries the live RAG pipeline via
/// <see cref="IRagService.AskAsync(string, string, string?, bool, CancellationToken)"/>
/// to propose the top-N retrieved chunks as unreviewed labeling candidates
/// (<c>Relevant == null</c>). The chunk id used is <c>snippet.source</c>, matching
/// the id space consumed by <see cref="EvaluationSample.RelevantChunkIds"/>.
/// </summary>
internal sealed class GenerateLabelingCandidatesCommandHandler
    : IRequestHandler<GenerateLabelingCandidatesCommand, LabelingReview>
{
    private readonly IRagService _ragService;
    private readonly ILogger<GenerateLabelingCandidatesCommandHandler> _logger;

    public GenerateLabelingCandidatesCommandHandler(
        IRagService ragService,
        ILogger<GenerateLabelingCandidatesCommandHandler> logger)
    {
        _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LabelingReview> Handle(GenerateLabelingCandidatesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Generating labeling candidates from dataset '{DatasetPath}' (top {TopN})",
            request.DatasetPath,
            request.TopN);

        var json = await File.ReadAllTextAsync(request.DatasetPath, cancellationToken).ConfigureAwait(false);
        var dataset = EvaluationDataset.FromJson(json);

        var items = new List<LabelingReviewItem>();

        foreach (var sample in dataset.Samples)
        {
            // Canonical hybrid retrieval path — AskAsync's legacy vector path is deprecated and returns
            // empty results, so labeling candidates must come from AskWithHybridSearchAsync.
            var response = await _ragService.AskWithHybridSearchAsync(
                sample.GameId ?? "",
                sample.Question,
                SearchMode.Hybrid,
                language: null,
                bypassCache: false,
                cancellationToken).ConfigureAwait(false);

            var candidates = (response.snippets ?? [])
                .Take(request.TopN)
                .Select(s => new LabelingCandidate(
                    ChunkId: s.source,
                    Page: s.page,
                    Score: s.score,
                    Snippet: s.text,
                    Relevant: null))
                .ToList();

            items.Add(new LabelingReviewItem(sample.Id, sample.Question, candidates));
        }

        _logger.LogInformation(
            "Generated {ItemCount} labeling review items from dataset '{DatasetPath}'",
            items.Count,
            request.DatasetPath);

        return new LabelingReview(items);
    }
}
