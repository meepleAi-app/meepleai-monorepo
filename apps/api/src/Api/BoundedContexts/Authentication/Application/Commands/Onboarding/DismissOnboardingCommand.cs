using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Command to dismiss the onboarding checklist for a user.
/// Idempotent — if already dismissed, the handler no-ops.
/// </summary>
internal sealed record DismissOnboardingCommand(Guid UserId) : ICommand;
