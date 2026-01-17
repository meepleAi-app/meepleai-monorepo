using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

public sealed record DeleteAiModelConfigCommand(Guid Id) : ICommand;
