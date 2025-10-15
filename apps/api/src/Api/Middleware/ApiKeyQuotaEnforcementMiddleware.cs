using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Skip quota enforcement if no API key is present
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader) ||
            string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            await _next(context);
            return;
        }

        // Get the API key ID from context (set by ApiKeyAuthenticationMiddleware)
        var apiKeyId = context.User.FindFirst("ApiKeyId")?.Value;
        if (string.IsNullOrWhiteSpace(apiKeyId))
        {
            await _next(context);
            return;
        }

        // Load the API key with its metadata
        var apiKey = await db.ApiKeys
            .Where(k => k.Id == apiKeyId)
            .Select(k => new { k.Id, k.Metadata })
            .FirstOrDefaultAsync();

        if (apiKey == null)
        {
            await _next(context);
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
                _logger.LogWarning(ex, "Failed to parse metadata for API key {ApiKeyId}", apiKeyId);
            }
        }

        // If no quota is defined, allow the request
        if (quota == null || (quota.DailyLimit == null && quota.HourlyLimit == null))
        {
            await _next(context);
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
                .CountAsync();

            if (hourlyCount >= quota.HourlyLimit.Value)
            {
                _logger.LogWarning(
                    "API key {ApiKeyId} exceeded hourly quota: {Count}/{Limit}",
                    apiKeyId, hourlyCount, quota.HourlyLimit.Value);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Append("X-RateLimit-Limit", quota.HourlyLimit.Value.ToString());
                context.Response.Headers.Append("X-RateLimit-Remaining", "0");
                context.Response.Headers.Append("X-RateLimit-Reset", hourStart.AddHours(1).ToUnixTimeSeconds().ToString());

                var error = new
                {
                    error = "Quota exceeded",
                    message = $"Hourly request limit of {quota.HourlyLimit.Value} exceeded",
                    retryAfter = (int)(hourStart.AddHours(1) - now).TotalSeconds
                };

                await context.Response.WriteAsJsonAsync(error);
                return;
            }

            // Add rate limit headers for hourly quota
            context.Response.Headers.Append("X-RateLimit-Limit", quota.HourlyLimit.Value.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", (quota.HourlyLimit.Value - hourlyCount - 1).ToString());
            context.Response.Headers.Append("X-RateLimit-Reset", hourStart.AddHours(1).ToUnixTimeSeconds().ToString());
        }

        // Check daily limit
        if (quota.DailyLimit.HasValue)
        {
            var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
            var dayStartUtc = dayStart.UtcDateTime;

            var dailyCount = await db.AiRequestLogs
                .Where(l => l.ApiKeyId == apiKeyId && l.CreatedAt >= dayStartUtc)
                .CountAsync();

            if (dailyCount >= quota.DailyLimit.Value)
            {
                _logger.LogWarning(
                    "API key {ApiKeyId} exceeded daily quota: {Count}/{Limit}",
                    apiKeyId, dailyCount, quota.DailyLimit.Value);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Append("X-RateLimit-Limit-Daily", quota.DailyLimit.Value.ToString());
                context.Response.Headers.Append("X-RateLimit-Remaining-Daily", "0");
                context.Response.Headers.Append("X-RateLimit-Reset-Daily", dayStart.AddDays(1).ToUnixTimeSeconds().ToString());

                var error = new
                {
                    error = "Quota exceeded",
                    message = $"Daily request limit of {quota.DailyLimit.Value} exceeded",
                    retryAfter = (int)(dayStart.AddDays(1) - now).TotalSeconds
                };

                await context.Response.WriteAsJsonAsync(error);
                return;
            }

            // Add rate limit headers for daily quota
            context.Response.Headers.Append("X-RateLimit-Limit-Daily", quota.DailyLimit.Value.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining-Daily", (quota.DailyLimit.Value - dailyCount - 1).ToString());
            context.Response.Headers.Append("X-RateLimit-Reset-Daily", dayStart.AddDays(1).ToUnixTimeSeconds().ToString());
        }

        // Quota not exceeded, proceed with request
        await _next(context);
    }

    private class ApiKeyMetadata
    {
        public ApiKeyQuota? Quota { get; set; }
    }

    private class ApiKeyQuota
    {
        public int? DailyLimit { get; set; }
        public int? HourlyLimit { get; set; }
    }
}
