using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to update user preferences (language, theme, notifications, data retention).
/// </summary>
public record UpdatePreferencesCommand : ICommand
{
    public Guid UserId { get; init; }
    public string Language { get; init; } = null!;
    public string Theme { get; init; } = null!;
    public bool EmailNotifications { get; init; }
    public int DataRetentionDays { get; init; }
}
