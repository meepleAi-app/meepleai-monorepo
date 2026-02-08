using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Query to retrieve database metrics including size, growth trends, connection statistics, and top tables.
/// Issue #3695: Resources Monitoring - Database metrics
/// </summary>
internal record GetDatabaseMetricsQuery : IQuery<DatabaseMetricsDto>;
