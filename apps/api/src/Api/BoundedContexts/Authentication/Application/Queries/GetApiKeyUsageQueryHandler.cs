using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

public class GetApiKeyUsageQueryHandler : IQueryHandler<GetApiKeyUsageQuery, ApiKeyQuotaDto?>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetApiKeyUsageQueryHandler(MeepleAiDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiKeyQuotaDto?> Handle(GetApiKeyUsageQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.KeyId, out var keyGuid) || !Guid.TryParse(request.UserId, out var userGuid))
        {
            return null;
        }

        var apiKey = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyGuid && k.UserId == userGuid, cancellationToken);

        if (apiKey == null)
            return null;

        // Parse quota from metadata
        var quota = ParseQuotaFromMetadata(apiKey.Metadata);

        // FUTURE ENHANCEMENT: Implement actual usage tracking from request logs
        // For now, return placeholder data with zero usage
        return new ApiKeyQuotaDto
        {
            MaxRequestsPerDay = quota.MaxRequestsPerDay,
            MaxRequestsPerHour = quota.MaxRequestsPerHour,
            RequestsToday = 0,
            RequestsThisHour = 0,
            ResetsAt = _timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1)
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
