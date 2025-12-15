using Api.BoundedContexts.Administration.Application.Commands.AlertConfiguration;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertConfiguration;

/// <summary>
/// Handler for UpdateAlertConfigurationCommand (Issue #915)
/// </summary>
public class UpdateAlertConfigurationCommandHandler : IRequestHandler<UpdateAlertConfigurationCommand, bool>
{
    private readonly IAlertConfigurationRepository _repository;
    private readonly ILogger<UpdateAlertConfigurationCommandHandler> _logger;

    public UpdateAlertConfigurationCommandHandler(
        IAlertConfigurationRepository repository,
        ILogger<UpdateAlertConfigurationCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(UpdateAlertConfigurationCommand request, CancellationToken ct)
    {
        var category = ConfigCategoryExtensions.FromString(request.Category);
        var existing = await _repository.GetByKeyAsync(request.ConfigKey, ct).ConfigureAwait(false);

        if (existing != null)
        {
            existing.UpdateValue(request.ConfigValue, request.UpdatedBy);
            await _repository.UpdateAsync(existing, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Alert configuration updated: {ConfigKey} in category {Category} by {UpdatedBy}",
                request.ConfigKey, request.Category, request.UpdatedBy);

            return true;
        }

        // Create new configuration if not exists
        var newConfig = Domain.Aggregates.AlertConfigurations.AlertConfiguration.Create(
            request.ConfigKey,
            request.ConfigValue,
            category,
            request.UpdatedBy,
            request.Description);

        await _repository.AddAsync(newConfig, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Alert configuration created: {ConfigKey} in category {Category} by {UpdatedBy}",
            request.ConfigKey, request.Category, request.UpdatedBy);

        return true;
    }
}
