using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of configuration change history.
/// Note: This is a simplified implementation using PreviousValue field.
/// For full audit trail, a separate configuration_history table would be needed.
/// </summary>
public class GetConfigHistoryQueryHandler : IQueryHandler<GetConfigHistoryQuery, IReadOnlyList<ConfigurationHistoryDto>>
{
    private readonly IConfigurationRepository _configurationRepository;

    public GetConfigHistoryQueryHandler(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
    }

    public async Task<IReadOnlyList<ConfigurationHistoryDto>> Handle(GetConfigHistoryQuery query, CancellationToken cancellationToken)
    {
        var config = await _configurationRepository.GetByIdAsync(query.ConfigurationId, cancellationToken).ConfigureAwait(false);
        if (config == null)
        {
            return Array.Empty<ConfigurationHistoryDto>();
        }

        var history = new List<ConfigurationHistoryDto>();

        // Current version (if there was a previous value, this represents a change)
        if (!string.IsNullOrEmpty(config.PreviousValue))
        {
            history.Add(new ConfigurationHistoryDto(
                Id: Guid.NewGuid().ToString(),
                ConfigurationId: config.Id.ToString(),
                Key: config.Key.Value,
                OldValue: config.PreviousValue,
                NewValue: config.Value,
                Version: config.Version,
                ChangedAt: config.UpdatedAt,
                ChangedByUserId: (config.UpdatedByUserId ?? config.CreatedByUserId).ToString(),
                ChangeReason: "Configuration updated"
            ));
        }

        // Initial creation (if no previous value, show creation event)
        if (string.IsNullOrEmpty(config.PreviousValue) || history.Count < query.Limit)
        {
            history.Add(new ConfigurationHistoryDto(
                Id: Guid.NewGuid().ToString(),
                ConfigurationId: config.Id.ToString(),
                Key: config.Key.Value,
                OldValue: "",
                NewValue: config.Value,
                Version: 1,
                ChangedAt: config.CreatedAt,
                ChangedByUserId: config.CreatedByUserId.ToString(),
                ChangeReason: "Configuration created"
            ));
        }

        return history.Take(query.Limit).ToList();
    }
}
