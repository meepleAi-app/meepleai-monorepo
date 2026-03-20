using Api.BoundedContexts.Administration.Application.Queries.AlertConfiguration;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertConfiguration;

/// <summary>
/// Handler for GetAllAlertConfigurationsQuery (Issue #915)
/// </summary>
internal class GetAllAlertConfigurationsQueryHandler : IRequestHandler<GetAllAlertConfigurationsQuery, List<AlertConfigurationDto>>
{
    private readonly IAlertConfigurationRepository _repository;

    public GetAllAlertConfigurationsQueryHandler(IAlertConfigurationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<AlertConfigurationDto>> Handle(GetAllAlertConfigurationsQuery request, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return configs.Select(c => new AlertConfigurationDto(
            c.Id,
            c.ConfigKey,
            c.ConfigValue,
            c.Category.ToDisplayString(),
            c.IsEncrypted,
            c.Description,
            c.UpdatedAt,
            c.UpdatedBy)).ToList();
    }
}

