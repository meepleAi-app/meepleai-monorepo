using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

public sealed record ToggleAiModelActiveCommand(Guid Id, bool IsActive) : ICommand;
