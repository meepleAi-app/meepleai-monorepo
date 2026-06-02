namespace Api.BoundedContexts.KbQuality.Application.Services;

/// <summary>
/// Single goldset query execution result feeding the metrics calculator.
/// </summary>
/// <param name="QueryId">Identifier of the goldset Q&amp;A pair</param>
/// <param name="RelevantHits">Boolean array marking top-N retrieved chunks; index 0 = rank 1.</param>
public sealed record QueryResult(string QueryId, IReadOnlyList<bool> RelevantHits);
