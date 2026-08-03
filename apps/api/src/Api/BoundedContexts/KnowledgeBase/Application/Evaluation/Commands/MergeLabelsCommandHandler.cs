using MediatR;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Handler for <see cref="MergeLabelsCommand"/>.
/// Loads a dataset and applies a human-reviewed <see cref="LabelingReview"/>: for each
/// reviewed sample, <see cref="EvaluationSample.RelevantChunkIds"/> is replaced with the
/// candidate chunk ids the reviewer marked <c>Relevant == true</c>. Samples without a
/// matching review item are carried over unchanged.
/// </summary>
internal sealed class MergeLabelsCommandHandler : IRequestHandler<MergeLabelsCommand, EvaluationDataset>
{
    private readonly ILogger<MergeLabelsCommandHandler> _logger;

    public MergeLabelsCommandHandler(ILogger<MergeLabelsCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EvaluationDataset> Handle(MergeLabelsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var json = await File.ReadAllTextAsync(request.DatasetPath, cancellationToken).ConfigureAwait(false);
        var dataset = EvaluationDataset.FromJson(json);

        // GroupBy + First (rather than ToDictionary directly) tolerates a review payload with
        // duplicate sample ids instead of throwing ArgumentException on the second occurrence.
        var relevantChunkIdsBySampleId = request.Review.Items
            .GroupBy(item => item.SampleId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.First().Candidates
                    .Where(c => c.Relevant == true)
                    .Select(c => c.ChunkId)
                    .ToList(),
                StringComparer.Ordinal);

        // Preserve source metadata (Version/CreatedAt) — persisting an in-place merge must not silently
        // downgrade the version-tracked golden dataset.
        var merged = EvaluationDataset.Create(
            dataset.Name, dataset.Description, dataset.SourceType, dataset.Version, dataset.CreatedAt);

        foreach (var sample in dataset.Samples)
        {
            var updatedSample = relevantChunkIdsBySampleId.TryGetValue(sample.Id, out var relevantChunkIds)
                ? sample with { RelevantChunkIds = relevantChunkIds }
                : sample;

            merged.AddSample(updatedSample);
        }

        _logger.LogInformation(
            "Merged labels for {ReviewedCount} of {SampleCount} samples in dataset '{DatasetPath}'",
            relevantChunkIdsBySampleId.Count,
            dataset.Count,
            request.DatasetPath);

        // Persistence is opt-in: when an OutputPath is supplied, write the labeled dataset back to disk
        // (closing the labeling round-trip); otherwise the merge stays in-memory (backward compatible).
        // Write to a temp file then move-with-overwrite so a crash/concurrent write can never truncate the
        // (possibly in-place / version-tracked) destination file.
        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            var tempPath = request.OutputPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, merged.ToJson(), cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, request.OutputPath, overwrite: true);
            _logger.LogInformation("Persisted merged dataset to '{OutputPath}'", request.OutputPath);
        }

        return merged;
    }
}
