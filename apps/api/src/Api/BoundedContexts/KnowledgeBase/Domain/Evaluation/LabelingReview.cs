namespace Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// A single candidate chunk proposed for a labeling review item.
/// </summary>
/// <param name="ChunkId">Retrieved chunk identifier (equals <c>Snippet.source</c>).</param>
/// <param name="Page">Source page of the retrieved chunk.</param>
/// <param name="Score">Retrieval score for the chunk.</param>
/// <param name="Snippet">Retrieved chunk text snippet.</param>
/// <param name="Relevant">Human relevance verdict: null = not yet reviewed.</param>
internal sealed record LabelingCandidate(
    string ChunkId,
    int Page,
    float Score,
    string Snippet,
    bool? Relevant);

/// <summary>
/// A single dataset sample under review, with its proposed labeling candidates.
/// </summary>
/// <param name="SampleId">The <see cref="EvaluationSample.Id"/> this item corresponds to.</param>
/// <param name="Question">The sample's question, for reviewer context.</param>
/// <param name="Candidates">Retrieved chunk candidates proposed for this sample.</param>
internal sealed record LabelingReviewItem(
    string SampleId,
    string Question,
    IReadOnlyList<LabelingCandidate> Candidates);

/// <summary>
/// AI-proposes/human-verifies labeling review: a set of per-sample retrieval candidates
/// awaiting a human relevance verdict before being merged back into a dataset.
/// </summary>
/// <param name="Items">Review items, one per evaluated sample.</param>
internal sealed record LabelingReview(IReadOnlyList<LabelingReviewItem> Items);
