using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to seed AI model configurations for runtime selection.
/// Populates the default models (see <c>SeedAiModelsCommandHandler.Models</c>) and, since #4164,
/// brings their verified prices up to date on an already populated catalog.
/// </summary>
public sealed record SeedAiModelsCommand : ICommand;
