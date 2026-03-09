using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to compare two rulebook analyses side by side.
/// Issue #5461: Analysis comparison tool.
/// </summary>
internal sealed record CompareAnalysesQuery(
    Guid LeftAnalysisId,
    Guid RightAnalysisId
) : IQuery<AnalysisComparisonDto>;
