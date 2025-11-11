using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get all active alerts.
/// </summary>
public record GetActiveAlertsQuery : IQuery<List<AlertDto>>;
