using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get account lockout status for a user.
/// Issue #3339: Account lockout after failed login attempts.
/// </summary>
internal record GetAccountLockoutStatusQuery(Guid UserId) : IQuery<AccountLockoutStatusDto>;

/// <summary>
/// DTO representing account lockout status.
/// </summary>
internal record AccountLockoutStatusDto(
    Guid UserId,
    bool IsLocked,
    int FailedLoginAttempts,
    DateTime? LockedUntil,
    int? RemainingMinutes
);

/// <summary>
/// Handler for GetAccountLockoutStatusQuery.
/// </summary>
internal class GetAccountLockoutStatusQueryHandler : IQueryHandler<GetAccountLockoutStatusQuery, AccountLockoutStatusDto>
{
    private readonly IUserRepository _userRepository;

    public GetAccountLockoutStatusQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<AccountLockoutStatusDto> Handle(GetAccountLockoutStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new NotFoundException("User", query.UserId.ToString());

        var isLocked = user.IsLockedOut();
        var remainingTime = user.GetRemainingLockoutDuration();
        var remainingMinutes = isLocked ? (int?)Math.Ceiling(remainingTime.TotalMinutes) : null;

        return new AccountLockoutStatusDto(
            UserId: user.Id,
            IsLocked: isLocked,
            FailedLoginAttempts: user.FailedLoginAttempts,
            LockedUntil: user.LockedUntil,
            RemainingMinutes: remainingMinutes
        );
    }
}
