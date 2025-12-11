using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Middleware;

/// <summary>
/// Middleware that enforces API key quota limits based on daily and hourly request counts.
/// This middleware tracks request usage and blocks requests that exceed configured quota limits.
/// </summary>
public class ApiKeyQuotaEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyQuotaEnforcementMiddleware> _logger;

    public ApiKeyQuotaEnforcementMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyQuotaEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, MeepleAiDbContext db, TimeProvider timeProvider)
    {
        // Only enforce quota on API endpoints (skip health checks, swagger, etc.)
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Skip quota enforcement if request is not authenticated via API key
        var authType = context.User.FindFirst("AuthType")?.Value;
        if (!string.Equals(authType, "ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Get the API key ID from context (set by ApiKeyAuthenticationMiddleware)
        var apiKeyIdStr = context.User.FindFirst("ApiKeyId")?.Value;
        if (string.IsNullOrWhiteSpace(apiKeyIdStr) || !Guid.TryParse(apiKeyIdStr, out var apiKeyId))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Load the API key with its metadata
        var apiKey = await db.ApiKeys
            .Where(k => k.Id == apiKeyId)
            .Select(k => new { k.Id, k.Metadata })
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (apiKey == null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Parse quota from metadata
        ApiKeyQuota? quota = null;
        if (!string.IsNullOrWhiteSpace(apiKey.Metadata))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<ApiKeyMetadata>(apiKey.Metadata);
                quota = metadata?.Quota;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse metadata for API key {ApiKeyId}", apiKeyIdStr);
            }
        }

        // If no quota is defined, allow the request
        if (quota == null || (quota.DailyLimit == null && quota.HourlyLimit == null))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var now = timeProvider.GetUtcNow();

        // Check hourly limit
        if (quota.HourlyLimit.HasValue)
        {
            var hourStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
            var hourStartUtc = hourStart.UtcDateTime;

            var hourlyCount = await db.AiRequestLogs
                .Where(l => l.ApiKeyId == apiKeyId && l.CreatedAt >= hourStartUtc)
                .CountAsync().ConfigureAwait(false);

            if (hourlyCount >= quota.HourlyLimit.Value)
            {
                _logger.LogWarning(
                    "API key {ApiKeyId} exceeded hourly quota: {Count}/{Limit}",
                    apiKeyIdStr, hourlyCount, quota.HourlyLimit.Value);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Append("X-RateLimit-Limit", quota.HourlyLimit.Value.ToString(CultureInfo.InvariantCulture));
                context.Response.Headers.Append("X-RateLimit-Remaining", "0");
                context.Response.Headers.Append("X-RateLimit-Reset", hourStart.AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

                var error = new
                {
                    error = "Quota exceeded",
                    message = $"Hourly request limit of {quota.HourlyLimit.Value} exceeded",
                    retryAfter = (int)(hourStart.AddHours(1) - now).TotalSeconds
                };

                await context.Response.WriteAsJsonAsync(error).ConfigureAwait(false);
                return;
            }

            // Add rate limit headers for hourly quota
            context.Response.Headers.Append("X-RateLimit-Limit", quota.HourlyLimit.Value.ToString(CultureInfo.InvariantCulture));
            context.Response.Headers.Append("X-RateLimit-Remaining", (quota.HourlyLimit.Value - hourlyCount - 1).ToString(CultureInfo.InvariantCulture));
            context.Response.Headers.Append("X-RateLimit-Reset", hourStart.AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }

        // Check daily limit
        if (quota.DailyLimit.HasValue)
        {
            var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
            var dayStartUtc = dayStart.UtcDateTime;

            var dailyCount = await db.AiRequestLogs
                .Where(l => l.ApiKeyId == apiKeyId && l.CreatedAt >= dayStartUtc)
                .CountAsync().ConfigureAwait(false);

            if (dailyCount >= quota.DailyLimit.Value)
            {
                _logger.LogWarning(
                    "API key {ApiKeyId} exceeded daily quota: {Count}/{Limit}",
                    apiKeyIdStr, dailyCount, quota.DailyLimit.Value);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Append("X-RateLimit-Limit-Daily", quota.DailyLimit.Value.ToString(CultureInfo.InvariantCulture));
                context.Response.Headers.Append("X-RateLimit-Remaining-Daily", "0");
                context.Response.Headers.Append("X-RateLimit-Reset-Daily", dayStart.AddDays(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

                var error = new
                {
                    error = "Quota exceeded",
                    message = $"Daily request limit of {quota.DailyLimit.Value} exceeded",
                    retryAfter = (int)(dayStart.AddDays(1) - now).TotalSeconds
                };

                await context.Response.WriteAsJsonAsync(error).ConfigureAwait(false);
                return;
            }

            // Add rate limit headers for daily quota
            context.Response.Headers.Append("X-RateLimit-Limit-Daily", quota.DailyLimit.Value.ToString(CultureInfo.InvariantCulture));
            context.Response.Headers.Append("X-RateLimit-Remaining-Daily", (quota.DailyLimit.Value - dailyCount - 1).ToString(CultureInfo.InvariantCulture));
            context.Response.Headers.Append("X-RateLimit-Reset-Daily", dayStart.AddDays(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }

        // Quota not exceeded, proceed with request
        await _next(context).ConfigureAwait(false);
    }

    // Suppress S3459/S1144: Properties assigned by JsonSerializer.Deserialize (line 74)
#pragma warning disable S3459, S1144
    private sealed class ApiKeyMetadata
    {
        public ApiKeyQuota? Quota { get; set; }
    }

    private sealed class ApiKeyQuota
    {
        public int? DailyLimit { get; set; }
        public int? HourlyLimit { get; set; }
    }
#pragma warning restore S3459, S1144
}
