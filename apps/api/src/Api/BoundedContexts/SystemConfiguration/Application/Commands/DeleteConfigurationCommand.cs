using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

internal record DeleteConfigurationCommand(Guid ConfigId) : ICommand<bool>;
