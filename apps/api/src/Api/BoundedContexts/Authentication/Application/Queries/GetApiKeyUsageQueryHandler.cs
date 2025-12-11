using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

public class GetApiKeyUsageQueryHandler : IQueryHandler<GetApiKeyUsageQuery, ApiKeyQuotaDto?>
{
    private readonly MeepleAiDbContext _db;
    private readonly IApiKeyUsageLogRepository _usageLogRepository;
    private readonly TimeProvider _timeProvider;

    public GetApiKeyUsageQueryHandler(
        MeepleAiDbContext db,
        IApiKeyUsageLogRepository usageLogRepository,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _usageLogRepository = usageLogRepository ?? throw new ArgumentNullException(nameof(usageLogRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiKeyQuotaDto?> Handle(GetApiKeyUsageQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.KeyId, out var keyGuid) || !Guid.TryParse(request.UserId, out var userGuid))
        {
            return null;
        }

        var apiKey = await _db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == keyGuid && k.UserId == userGuid, cancellationToken).ConfigureAwait(false);

        if (apiKey == null)
            return null;

        // Parse quota from metadata
        var quota = ParseQuotaFromMetadata(apiKey.Metadata);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var startOfDay = now.Date;
        var startOfHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        // Get actual usage from logs
        var requestsToday = await _usageLogRepository.GetUsageCountInRangeAsync(
            keyGuid, startOfDay, now, cancellationToken).ConfigureAwait(false);

        var requestsThisHour = await _usageLogRepository.GetUsageCountInRangeAsync(
            keyGuid, startOfHour, now, cancellationToken).ConfigureAwait(false);

        return new ApiKeyQuotaDto
        {
            MaxRequestsPerDay = quota.MaxRequestsPerDay,
            MaxRequestsPerHour = quota.MaxRequestsPerHour,
            RequestsToday = requestsToday,
            RequestsThisHour = requestsThisHour,
            ResetsAt = startOfDay.AddDays(1)
        };
    }

    private (int? MaxRequestsPerDay, int? MaxRequestsPerHour) ParseQuotaFromMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return (null, null);

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadata);
            var root = doc.RootElement;

            var maxPerDay = root.TryGetProperty("maxRequestsPerDay", out var dayProp) && dayProp.ValueKind == System.Text.Json.JsonValueKind.Number
                ? (int?)dayProp.GetInt32()
                : null;

            var maxPerHour = root.TryGetProperty("maxRequestsPerHour", out var hourProp) && hourProp.ValueKind == System.Text.Json.JsonValueKind.Number
                ? (int?)hourProp.GetInt32()
                : null;

            return (maxPerDay, maxPerHour);
        }
        catch
        {
            return (null, null);
        }
    }
}
