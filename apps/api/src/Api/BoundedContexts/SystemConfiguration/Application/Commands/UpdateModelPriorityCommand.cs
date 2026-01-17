using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update AI model priority for fallback chain ordering
/// </summary>
/// <remarks>
/// Issue #2567: Admin endpoint for updating model priority
/// Lower priority = higher preference (1 = primary, 2 = first fallback, etc.)
/// </remarks>
internal sealed record UpdateModelPriorityCommand(
    Guid Id,
    int NewPriority
) : ICommand<AiModelDto>;
