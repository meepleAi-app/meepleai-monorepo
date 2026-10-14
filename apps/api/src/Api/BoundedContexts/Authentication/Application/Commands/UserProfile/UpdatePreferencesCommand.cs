using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to update user preferences (language, theme, notifications, data retention, privacy).
/// Returns updated UserProfileDto with all profile information.
/// </summary>
/// <remarks>
/// #4157: aggiornamento parziale. Un campo <c>null</c> resta com'è. Prima il comando sostituiva tutto,
/// e un campo assente valeva il suo default: <c>DataRetentionDays = 0</c> faceva rispondere 422 alla
/// pagina delle preferenze, che non lo manda, e i tre flag di privacy sarebbero tornati <c>true</c> a
/// ogni salvataggio che non li nominava.
/// </remarks>
internal record UpdatePreferencesCommand : ICommand<UserProfileDto>
{
    public Guid UserId { get; init; }
    public string? Language { get; init; }
    public string? Theme { get; init; }
    public bool? EmailNotifications { get; init; }
    public int? DataRetentionDays { get; init; }
    public bool? ShowProfile { get; init; }
    public bool? ShowActivity { get; init; }
    public bool? ShowLibrary { get; init; }
}
