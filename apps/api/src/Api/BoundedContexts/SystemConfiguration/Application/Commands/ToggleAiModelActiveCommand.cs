using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to toggle AI model active status (enable/disable)
/// </summary>
/// <remarks>
/// Issue #2567: Admin endpoint for toggling model active status
/// Toggling to inactive removes model from fallback chain
/// </remarks>
internal sealed record ToggleAiModelActiveCommand(Guid Id) : ICommand<AiModelDto>;
