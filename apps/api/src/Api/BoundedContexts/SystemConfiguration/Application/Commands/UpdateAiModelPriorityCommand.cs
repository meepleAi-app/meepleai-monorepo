using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

public sealed record UpdateAiModelPriorityCommand(Guid Id, int NewPriority) : ICommand;
