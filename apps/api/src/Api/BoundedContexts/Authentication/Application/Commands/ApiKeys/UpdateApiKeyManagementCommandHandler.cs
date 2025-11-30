using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Commands;

public class UpdateApiKeyManagementCommandHandler : ICommandHandler<UpdateApiKeyManagementCommand, ApiKeyDto?>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<UpdateApiKeyManagementCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public UpdateApiKeyManagementCommandHandler(
        MeepleAiDbContext db,
        ILogger<UpdateApiKeyManagementCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiKeyDto?> Handle(UpdateApiKeyManagementCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.KeyId, out var keyGuid) || !Guid.TryParse(command.UserId, out var userGuid))
        {
            return null;
        }

        var apiKey = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyGuid && k.UserId == userGuid, cancellationToken);

        if (apiKey == null)
        {
            _logger.LogWarning("API key update failed: key not found or unauthorized. KeyId: {KeyId}", command.KeyId);
            return null;
        }

        // Update fields if provided
        if (command.Request.KeyName != null)
            apiKey.KeyName = command.Request.KeyName;

        if (command.Request.Scopes != null)
            apiKey.Scopes = string.Join(",", command.Request.Scopes);

        if (command.Request.ExpiresAt.HasValue)
            apiKey.ExpiresAt = command.Request.ExpiresAt.Value;

        if (command.Request.IsActive.HasValue)
            apiKey.IsActive = command.Request.IsActive.Value;

        // Update quota metadata if specified
        if (command.Request.MaxRequestsPerDay.HasValue || command.Request.MaxRequestsPerHour.HasValue)
        {
            var quota = new
            {
                maxRequestsPerDay = command.Request.MaxRequestsPerDay,
                maxRequestsPerHour = command.Request.MaxRequestsPerHour
            };
            apiKey.Metadata = System.Text.Json.JsonSerializer.Serialize(quota);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("API key updated. KeyId: {KeyId}, UserId: {UserId}", command.KeyId, command.UserId);

        return MapToDto(apiKey);
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
