namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

internal interface IUserQuotaInfoService
{
    Task<UserQuotaInfoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

internal sealed record UserQuotaInfoDto(Guid Id, string Tier, string Role);
