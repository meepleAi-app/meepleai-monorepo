using Api.BoundedContexts.Administration.Application.Queries.AlertConfiguration;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertConfiguration;

/// <summary>
/// Handler for GetAlertConfigurationQuery (Issue #915)
/// </summary>
internal class GetAlertConfigurationQueryHandler : IRequestHandler<GetAlertConfigurationQuery, AlertConfigurationDto>
{
    private readonly IAlertConfigurationRepository _repository;

    public GetAlertConfigurationQueryHandler(IAlertConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<AlertConfigurationDto> Handle(GetAlertConfigurationQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var category = ConfigCategoryExtensions.FromString(request.Category);
        var configs = await _repository.GetByCategoryAsync(category, ct).ConfigureAwait(false);

        if (configs.Count == 0)
        {
            throw new InvalidOperationException($"No configuration found for category: {request.Category}");
        }

        var config = configs.First();
        return MapToDto(config);
    }

    private static AlertConfigurationDto MapToDto(Domain.Aggregates.AlertConfigurations.AlertConfiguration config)
    {
        return new AlertConfigurationDto(
            config.Id,
            config.ConfigKey,
            config.ConfigValue,
            config.Category.ToDisplayString(),
            config.IsEncrypted,
            config.Description,
            config.UpdatedAt,
            config.UpdatedBy);
    }
}
