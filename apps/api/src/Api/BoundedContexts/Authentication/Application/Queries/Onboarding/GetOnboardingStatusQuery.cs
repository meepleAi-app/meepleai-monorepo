using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Queries.Onboarding;

/// <summary>
/// Query to retrieve the onboarding status for a user.
/// Cross-BC: aggregates data from Authentication, UserLibrary, and SessionTracking.
/// </summary>
public sealed record GetOnboardingStatusQuery(Guid UserId) : IRequest<OnboardingStatusResponse>;
