using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries.Onboarding;

/// <summary>
/// Handles GetOnboardingStatusQuery by reading User via domain repository
/// and cross-BC data (UserLibrary, SessionTracking) via DbContext.
/// </summary>
internal sealed class GetOnboardingStatusQueryHandler
    : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusResponse>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IUserRepository _userRepository;

    public GetOnboardingStatusQueryHandler(
        MeepleAiDbContext dbContext,
        IUserRepository userRepository)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<OnboardingStatusResponse> Handle(
        GetOnboardingStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Use IUserRepository for User reads (runs restore methods for proper hydration)
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("User not found");

        // Use DbContext for cross-BC read-only queries (established pattern)
        var hasGames = await _dbContext.UserLibraryEntries
            .AnyAsync(e => e.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Use full DbSet property name to disambiguate from auth sessions
        var hasSessions = await _dbContext.SessionTrackingSessions
            .AnyAsync(s => s.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        return new OnboardingStatusResponse
        {
            WizardSeenAt = user.OnboardingWizardSeenAt,
            DismissedAt = user.OnboardingDismissedAt,
            Steps = new OnboardingStepsDto
            {
                HasGames = hasGames,
                HasSessions = hasSessions,
                HasCompletedProfile = user.AvatarUrl != null || user.Bio != null,
            },
        };
    }
}
