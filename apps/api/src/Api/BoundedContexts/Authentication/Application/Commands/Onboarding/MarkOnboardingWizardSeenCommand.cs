using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Command to mark the onboarding wizard as seen for a user.
/// Idempotent — if already marked, the handler no-ops.
/// </summary>
internal sealed record MarkOnboardingWizardSeenCommand(Guid UserId) : ICommand;
