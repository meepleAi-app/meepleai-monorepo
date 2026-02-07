using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Query to get detailed database metrics (size, growth, top tables).
/// Issue #3695: Resources Monitoring - Database
/// </summary>
internal record GetDatabaseMetricsQuery : IQuery<DatabaseMetricsDto>;
