using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to update user preferences (language, theme, notifications, data retention).
/// Returns updated UserProfileDto with all profile information.
/// </summary>
public record UpdatePreferencesCommand : ICommand<UserProfileDto>
{
    public Guid UserId { get; init; }
    public string Language { get; init; } = null!;
    public string Theme { get; init; } = null!;
    public bool EmailNotifications { get; init; }
    public int DataRetentionDays { get; init; }
}
