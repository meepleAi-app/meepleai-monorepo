using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update an existing AI model configuration
/// </summary>
/// <remarks>
/// Issue #2567: Admin endpoint for updating AI models
/// Note: ModelId and DisplayName are immutable and cannot be updated after creation
/// </remarks>
internal sealed record UpdateAiModelCommand(
    Guid Id,
    int Priority,
    bool IsActive,
    bool IsPrimary,
    ModelSettings Settings
) : ICommand<AiModelDto>;
