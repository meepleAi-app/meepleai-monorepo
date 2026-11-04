using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Public command: the guest behind an invitation asks to stop receiving game night emails (#4846). The
/// invitation <paramref name="Token"/> is the credential; the address it names goes on the list, whatever
/// became of the invitation.
/// </summary>
/// <param name="Token">The invitation token from the link in the email.</param>
internal sealed record OptOutGameNightGuestEmailCommand(string Token) : ICommand;
