using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertConfiguration;

/// <summary>
/// Query to get all alert configurations (Issue #915)
/// </summary>
public record GetAllAlertConfigurationsQuery : IRequest<List<AlertConfigurationDto>>;
