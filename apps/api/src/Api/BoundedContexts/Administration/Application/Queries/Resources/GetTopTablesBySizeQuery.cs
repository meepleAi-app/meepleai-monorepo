using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Query to retrieve top tables by size for database optimization analysis.
/// Issue #3695: Resources Monitoring - Top tables by size
/// </summary>
internal record GetTopTablesBySizeQuery(
    int Limit = 10
) : IQuery<IReadOnlyList<TableSizeDto>>;
