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

        var relevantChunkIdsBySampleId = request.Review.Items.ToDictionary(
            item => item.SampleId,
            item => (IReadOnlyList<string>)item.Candidates
                .Where(c => c.Relevant == true)
                .Select(c => c.ChunkId)
                .ToList(),
            StringComparer.Ordinal);

        var merged = EvaluationDataset.Create(dataset.Name, dataset.Description, dataset.SourceType);

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

        return merged;
    }
}
