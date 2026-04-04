using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Marks a user's onboarding as completed or skipped.
/// Issue #323: Onboarding completion tracking.
/// </summary>
internal record CompleteOnboardingCommand(
    Guid UserId,
    bool Skipped
) : ICommand;
