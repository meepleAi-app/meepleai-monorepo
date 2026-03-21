namespace Api.BoundedContexts.SystemConfiguration.Application.Services;

internal interface IUserProfileReadService
{
    Task<UserProfileDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserProfileDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}

internal sealed record UserProfileDto(Guid Id, string? DisplayName, string Email, string Role, string Tier);
