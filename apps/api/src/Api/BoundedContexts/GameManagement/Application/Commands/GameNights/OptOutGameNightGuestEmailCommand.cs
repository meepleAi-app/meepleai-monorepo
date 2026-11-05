using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Public command: the guest behind an invitation asks to stop receiving game night emails (#4846). The invitation's
/// <paramref name="UnsubscribeKey"/> is the credential; the address it names goes on the list, whatever became of
/// the invitation. It is not the RSVP token: the organizer holds that one, and an opt-out is global.
/// </summary>
/// <param name="UnsubscribeKey">The key from the link in the email.</param>
internal sealed record OptOutGameNightGuestEmailCommand(string UnsubscribeKey) : ICommand;
