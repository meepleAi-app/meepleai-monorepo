using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetUserUploadQuotaQuery.
/// Retrieves user's PDF upload quota information including usage and limits.
/// DDD: Uses local IUserQuotaInfoService (reads from UserProfile projection) instead of
/// cross-BC IUserRepository, keeping DocumentProcessing BC isolated from Authentication BC.
/// </summary>
internal class GetUserUploadQuotaQueryHandler : IQueryHandler<GetUserUploadQuotaQuery, PdfUploadQuotaInfo>
{
    private readonly IUserQuotaInfoService _userQuotaInfoService;
    private readonly IPdfUploadQuotaService _quotaService;
    private readonly ILogger<GetUserUploadQuotaQueryHandler> _logger;

    public GetUserUploadQuotaQueryHandler(
        IUserQuotaInfoService userQuotaInfoService,
        IPdfUploadQuotaService quotaService,
        ILogger<GetUserUploadQuotaQueryHandler> logger)
    {
        _userQuotaInfoService = userQuotaInfoService ?? throw new ArgumentNullException(nameof(userQuotaInfoService));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfUploadQuotaInfo> Handle(GetUserUploadQuotaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var userInfo = await _userQuotaInfoService.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        if (userInfo == null)
        {
            _logger.LogWarning("User {UserId} not found for quota query", query.UserId);
            throw new DomainException($"User {query.UserId} not found");
        }

        var userTier = UserTier.Parse(userInfo.Tier);
        var userRole = Role.Parse(userInfo.Role);

        var quotaInfo = await _quotaService.GetQuotaInfoAsync(
            query.UserId,
            userTier,
            userRole,
            cancellationToken).ConfigureAwait(false);

        return quotaInfo;
    }
}
