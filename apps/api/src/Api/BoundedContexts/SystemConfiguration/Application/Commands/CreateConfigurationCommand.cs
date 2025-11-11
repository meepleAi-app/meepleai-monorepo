using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

public record CreateConfigurationCommand(
    string Key,
    string Value,
    string ValueType,
    string? Description,
    string Category,
    string Environment,
    bool RequiresRestart,
    Guid CreatedByUserId
) : ICommand<ConfigurationDto>;
