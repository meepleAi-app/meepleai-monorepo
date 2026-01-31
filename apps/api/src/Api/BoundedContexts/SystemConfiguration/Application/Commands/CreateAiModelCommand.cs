using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to create a new AI model configuration
/// </summary>
/// <remarks>
/// Issue #2567: Admin endpoint for creating AI models
/// </remarks>
internal sealed record CreateAiModelCommand(
    string ModelId,
    string DisplayName,
    string Provider,
    int Priority,
    bool IsActive,
    bool IsPrimary,
    ModelSettings Settings
) : ICommand<AiModelDto>;
