using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetUserUploadQuotaQuery.
/// Retrieves user's PDF upload quota information including usage and limits.
/// DDD: Uses repository pattern instead of direct DbContext access.
/// </summary>
internal class GetUserUploadQuotaQueryHandler : IQueryHandler<GetUserUploadQuotaQuery, PdfUploadQuotaInfo>
{
    private readonly IUserRepository _userRepository;
    private readonly IPdfUploadQuotaService _quotaService;
    private readonly ILogger<GetUserUploadQuotaQueryHandler> _logger;

    public GetUserUploadQuotaQueryHandler(
        IUserRepository userRepository,
        IPdfUploadQuotaService quotaService,
        ILogger<GetUserUploadQuotaQueryHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfUploadQuotaInfo> Handle(GetUserUploadQuotaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // Get user from repository (DDD pattern)
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for quota query", query.UserId);
            throw new DomainException($"User {query.UserId} not found");
        }

        // Get quota info from service
        var quotaInfo = await _quotaService.GetQuotaInfoAsync(
            query.UserId,
            user.Tier,
            user.Role,
            cancellationToken).ConfigureAwait(false);

        return quotaInfo;
    }
}
