using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

internal class CreateApiKeyManagementCommandHandler : ICommandHandler<CreateApiKeyManagementCommand, CreateApiKeyResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ApiKeyAuthenticationService _authService;
    private readonly ILogger<CreateApiKeyManagementCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public CreateApiKeyManagementCommandHandler(
        MeepleAiDbContext db,
        ApiKeyAuthenticationService authService,
        ILogger<CreateApiKeyManagementCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CreateApiKeyResponse> Handle(CreateApiKeyManagementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Validate request
        if (string.IsNullOrWhiteSpace(command.Request.KeyName))
            throw new ArgumentException("Key name is required", nameof(command));

        // Generate the API key using the authentication service
        var (plaintextKey, entity) = await _authService.GenerateApiKeyAsync(
            command.UserId,
            command.Request.KeyName,
            command.Request.Scopes,
            command.Request.ExpiresAt,
            command.Request.Environment,
            cancellationToken).ConfigureAwait(false);

        // Add quota information as metadata if specified
        if (command.Request.MaxRequestsPerDay.HasValue || command.Request.MaxRequestsPerHour.HasValue)
        {
            var quota = new
            {
                maxRequestsPerDay = command.Request.MaxRequestsPerDay,
                maxRequestsPerHour = command.Request.MaxRequestsPerHour
            };
            entity.Metadata = System.Text.Json.JsonSerializer.Serialize(quota);
        }

        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "API key created. KeyId: {KeyId}, UserId: {UserId}, Name: {Name}",
            entity.Id,
            command.UserId,
            command.Request.KeyName);

        return new CreateApiKeyResponse
        {
            ApiKey = MapToDto(entity),
            PlaintextKey = plaintextKey
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

    private static (int? MaxRequestsPerDay, int? MaxRequestsPerHour) ParseQuotaFromMetadata(string? metadata)
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
