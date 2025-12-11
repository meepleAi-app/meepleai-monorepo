using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Commands;

public class RotateApiKeyCommandHandler : ICommandHandler<RotateApiKeyCommand, RotateApiKeyResponse?>
{
    private readonly MeepleAiDbContext _db;
    private readonly ApiKeyAuthenticationService _authService;
    private readonly ILogger<RotateApiKeyCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RotateApiKeyCommandHandler(
        MeepleAiDbContext db,
        ApiKeyAuthenticationService authService,
        ILogger<RotateApiKeyCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RotateApiKeyResponse?> Handle(RotateApiKeyCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.KeyId, out var keyGuid) || !Guid.TryParse(command.UserId, out var userGuid))
        {
            return null;
        }

        var oldKey = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyGuid && k.UserId == userGuid, cancellationToken).ConfigureAwait(false);

        if (oldKey == null)
        {
            _logger.LogWarning("API key rotation failed: key not found or unauthorized. KeyId: {KeyId}", command.KeyId);
            return null;
        }

        // Extract environment from old key prefix
        var environment = oldKey.KeyPrefix.Contains("live") ? "live" : "test";

        // Generate new key with same scopes and settings
        var scopes = oldKey.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var (plaintextKey, newEntity) = await _authService.GenerateApiKeyAsync(
            command.UserId,
            $"{oldKey.KeyName} (Rotated)",
            scopes,
            command.Request.ExpiresAt ?? oldKey.ExpiresAt,
            environment,
            cancellationToken).ConfigureAwait(false);

        // Copy quota metadata from old key
        newEntity.Metadata = oldKey.Metadata;

        _db.ApiKeys.Add(newEntity);

        // Revoke old key
        await _authService.RevokeApiKeyAsync(command.KeyId, command.UserId, cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "API key rotated. OldKeyId: {OldKeyId}, NewKeyId: {NewKeyId}, UserId: {UserId}",
            command.KeyId,
            newEntity.Id,
            command.UserId);

        return new RotateApiKeyResponse
        {
            NewApiKey = MapToDto(newEntity),
            PlaintextKey = plaintextKey,
            RevokedKeyId = command.KeyId
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
