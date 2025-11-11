using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to rollback configuration to a previous version.
/// </summary>
public record RollbackConfigCommand(
    Guid ConfigurationId,
    int TargetVersion,
    Guid UserId
) : ICommand<ConfigurationDto?>;
