using Api.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get alert history within a date range.
/// </summary>
public record GetAlertHistoryQuery(
    DateTime FromDate,
    DateTime ToDate
) : IRequest<List<AlertDto>>;
