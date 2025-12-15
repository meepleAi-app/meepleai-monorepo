using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get alert history within a date range.
/// </summary>
internal record GetAlertHistoryQuery(
    DateTime FromDate,
    DateTime ToDate
) : IQuery<List<AlertDto>>;
