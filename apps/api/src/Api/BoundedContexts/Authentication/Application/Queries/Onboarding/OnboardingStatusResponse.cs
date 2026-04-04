namespace Api.BoundedContexts.Authentication.Application.Queries.Onboarding;

/// <summary>
/// Completion state for each onboarding checklist step.
/// </summary>
public sealed record OnboardingStepsDto
{
    public bool HasGames { get; init; }
    public bool HasSessions { get; init; }
    public bool HasCompletedProfile { get; init; }
}

/// <summary>
/// Aggregated onboarding status for the current user.
/// Combines wizard/dismiss timestamps with real data from cross-BC queries.
/// </summary>
public sealed record OnboardingStatusResponse
{
    public DateTime? WizardSeenAt { get; init; }
    public DateTime? DismissedAt { get; init; }
    public OnboardingStepsDto Steps { get; init; } = new();
}
