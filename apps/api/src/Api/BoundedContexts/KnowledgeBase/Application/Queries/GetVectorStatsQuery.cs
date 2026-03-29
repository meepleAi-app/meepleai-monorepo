using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve pgvector statistics grouped by game.
/// Returns total chunk counts, health percentages, and per-game breakdown.
/// Task 3: Qdrant → pgvector migration.
/// </summary>
internal sealed record GetVectorStatsQuery() : IQuery<VectorStatsDto>;

/// <summary>
/// Top-level pgvector statistics DTO.
/// </summary>
internal sealed record VectorStatsDto(
    long TotalVectors,
    int Dimensions,
    int GamesIndexed,
    int AvgHealthPercent,
    long SizeEstimateBytes,
    List<VectorGameBreakdownDto> GameBreakdown);

/// <summary>
/// Per-game breakdown within pgvector statistics.
/// </summary>
internal sealed record VectorGameBreakdownDto(
    Guid GameId,
    string GameName,
    long VectorCount,
    long CompletedCount,
    long FailedCount,
    int HealthPercent);
