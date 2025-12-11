using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for GetApiKeyUsageLogsQuery.
/// Retrieves paginated usage logs for a specific API key.
/// </summary>
public class GetApiKeyUsageLogsQueryHandler : IQueryHandler<GetApiKeyUsageLogsQuery, List<ApiKeyUsageLogDto>>
{
    private readonly MeepleAiDbContext _db;
    private readonly IApiKeyUsageLogRepository _usageLogRepository;

    public GetApiKeyUsageLogsQueryHandler(
        MeepleAiDbContext db,
        IApiKeyUsageLogRepository usageLogRepository)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _usageLogRepository = usageLogRepository ?? throw new ArgumentNullException(nameof(usageLogRepository));
    }

    public async Task<List<ApiKeyUsageLogDto>> Handle(
        GetApiKeyUsageLogsQuery request,
        CancellationToken cancellationToken)
    {
        // Verify the user owns this API key
        var apiKeyExists = await _db.ApiKeys
            .AsNoTracking()
            .AnyAsync(k => k.Id == request.KeyId && k.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!apiKeyExists)
        {
            return new List<ApiKeyUsageLogDto>();
        }

        // Get usage logs
        var logs = await _usageLogRepository.GetByKeyIdAsync(
            request.KeyId,
            request.Skip,
            request.Take,
            cancellationToken)
            .ConfigureAwait(false);

        // Map to DTOs
        return logs.Select(log => new ApiKeyUsageLogDto
        {
            Id = log.Id,
            KeyId = log.KeyId,
            UsedAt = log.UsedAt,
            Endpoint = log.Endpoint,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            HttpMethod = log.HttpMethod,
            StatusCode = log.StatusCode,
            ResponseTimeMs = log.ResponseTimeMs
        }).ToList();
    }
}
