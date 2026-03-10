using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Issue #5503: Returns model health/compatibility data for admin UI.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
public sealed record GetModelHealthQuery : IQuery<GetModelHealthResult>;

public sealed record ModelHealthDto(
    string ModelId,
    string DisplayName,
    string Provider,
    string[] Alternatives,
    int ContextWindow,
    string[] Strengths,
    bool IsCurrentlyAvailable,
    bool IsDeprecated,
    DateTime? LastVerifiedAt);

public sealed record GetModelHealthResult(IReadOnlyList<ModelHealthDto> Models);
