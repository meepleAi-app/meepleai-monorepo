using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handles rollback of configuration to previous version.
/// Currently supports single-level rollback via PreviousValue.
/// </summary>
internal class RollbackConfigCommandHandler : ICommandHandler<RollbackConfigCommand, ConfigurationDto?>
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RollbackConfigCommandHandler(
        IConfigurationRepository configurationRepository,
        IUnitOfWork unitOfWork)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ConfigurationDto?> Handle(RollbackConfigCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var config = await _configurationRepository.GetByIdAsync(command.ConfigurationId, cancellationToken).ConfigureAwait(false);
        if (config == null)
            return null;

        // Simple rollback implementation using PreviousValue
        // Note: TargetVersion is ignored in this simplified implementation
        // For full version history, a separate audit table would be needed
        config.Rollback(command.UserId);

        await _configurationRepository.UpdateAsync(config, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(config);
    }

    private static ConfigurationDto MapToDto(Domain.Entities.SystemConfiguration config)
    {
        return new ConfigurationDto(
            Id: config.Id,
            Key: config.Key.Value,
            Value: config.Value,
            ValueType: config.ValueType,
            Description: config.Description,
            Category: config.Category,
            IsActive: config.IsActive,
            RequiresRestart: config.RequiresRestart,
            Environment: config.Environment,
            Version: config.Version,
            CreatedAt: config.CreatedAt,
            UpdatedAt: config.UpdatedAt
        );
    }
}
