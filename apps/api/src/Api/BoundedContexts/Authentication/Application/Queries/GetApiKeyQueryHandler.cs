using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

public class GetApiKeyQueryHandler : IQueryHandler<GetApiKeyQuery, ApiKeyDto?>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetApiKeyQueryHandler(MeepleAiDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiKeyDto?> Handle(GetApiKeyQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.KeyId, out var keyGuid) || !Guid.TryParse(request.UserId, out var userGuid))
        {
            return null;
        }

        var key = await _db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == keyGuid && k.UserId == userGuid, cancellationToken).ConfigureAwait(false);

        return key == null ? null : MapToDto(key);
    }

    private ApiKeyDto MapToDto(ApiKeyEntity entity)
    {
        var quota = ParseQuotaFromMetadata(entity.Metadata);

        return new ApiKeyDto
        {
            Id = entity.Id.ToString(),
            KeyName = entity.KeyName,
            KeyPrefix = entity.KeyPrefix,
            Scopes = entity.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries),
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            LastUsedAt = entity.LastUsedAt,
            ExpiresAt = entity.ExpiresAt,
            RevokedAt = entity.RevokedAt,
            RevokedBy = entity.RevokedBy?.ToString(),
            Quota = quota.MaxRequestsPerDay.HasValue || quota.MaxRequestsPerHour.HasValue
                ? new ApiKeyQuotaDto
                {
                    MaxRequestsPerDay = quota.MaxRequestsPerDay,
                    MaxRequestsPerHour = quota.MaxRequestsPerHour,
                    RequestsToday = 0,
                    RequestsThisHour = 0,
                    ResetsAt = _timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1)
                }
                : null
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
