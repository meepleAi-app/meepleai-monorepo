using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

public class ListApiKeysQueryHandler : IQueryHandler<ListApiKeysQuery, ApiKeyListResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ListApiKeysQueryHandler(MeepleAiDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiKeyListResponse> Handle(ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.UserId, out var userGuid))
        {
            return new ApiKeyListResponse
            {
                Keys = new List<ApiKeyDto>(),
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        var query = _db.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userGuid);

        if (!request.IncludeRevoked)
        {
            query = query.Where(k => k.RevokedAt == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var keyEntities = await query
            .OrderByDescending(k => k.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var keys = keyEntities
            .Select(k => MapToDto(k))
            .ToList();

        return new ApiKeyListResponse
        {
            Keys = keys,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
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
