using Api.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get all active alerts.
/// </summary>
public record GetActiveAlertsQuery : IRequest<List<AlertDto>>;
