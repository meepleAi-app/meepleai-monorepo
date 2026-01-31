using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to seed AI model configurations for runtime selection.
/// Populates 6 default models (OpenRouter + Ollama) with priority-based fallback.
/// </summary>
public sealed record SeedAiModelsCommand : ICommand;
