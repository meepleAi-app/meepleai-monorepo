using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// #535 ME-M3.3: sets the calling admin's opt-in for mechanic-card-suppression emails. Dedicated command
/// (not folded into <see cref="UpdateNotificationPreferencesCommand"/>) so an unrelated preferences save
/// from the FE — which only sends the Document-preference fields — never resets this flag.
/// </summary>
internal record UpdateCardSuppressionEmailPreferenceCommand(Guid UserId, bool EmailOnCardSuppressed) : ICommand;
