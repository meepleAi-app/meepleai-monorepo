using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

public record DeleteConfigurationCommand(Guid ConfigId) : ICommand<bool>;
