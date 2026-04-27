using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Waitlist;

/// <summary>
/// Command to enroll an email into the public Alpha waitlist.
/// Spec §3.5 (2026-04-27-v2-migration-wave-a-2-join.md).
/// </summary>
internal record JoinWaitlistCommand(
    string Email,
    string? Name,
    string GamePreferenceId,
    string? GamePreferenceOther,
    bool NewsletterOptIn) : ICommand<JoinWaitlistResult>;
