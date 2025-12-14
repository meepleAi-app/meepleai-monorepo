using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using Api.Models;
using MediatR;

namespace Api.Routing;

/// <summary>
/// API key management endpoints.
/// Handles API key CRUD, rotation, usage tracking, and admin operations.
/// </summary>
public static class ApiKeyEndpoints
{
    public static RouteGroupBuilder MapApiKeyEndpoints(this RouteGroupBuilder group)
    {
        // API-04: API Key Management endpoints
        group.MapPost("/api-keys", async (Api.Models.CreateApiKeyRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            if (string.IsNullOrWhiteSpace(request.KeyName))
            {
                return Results.BadRequest(new { error = "Key name is required" });
            }

            logger.LogInformation("User {UserId} creating API key '{KeyName}'", session.User!.Id, request.KeyName);

            var command = new Api.BoundedContexts.Authentication.Application.Commands.CreateApiKeyManagementCommand(
                session.User!.Id.ToString(),
                request);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("API key '{KeyId}' created for user {UserId}", result.ApiKey.Id, session.User!.Id);

            return Results.Created($"/api/v1/api-keys/{result.ApiKey.Id}", result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapGet("/api-keys", async (HttpContext context, IMediator mediator, bool includeRevoked = false, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new Api.BoundedContexts.Authentication.Application.Queries.ListApiKeysQuery(
                session.User!.Id.ToString(),
                includeRevoked,
                page,
                pageSize);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapGet("/api-keys/{keyId}", async (string keyId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new Api.BoundedContexts.Authentication.Application.Queries.GetApiKeyQuery(keyId, session.User!.Id.ToString());
            var apiKey = await mediator.Send(query, ct).ConfigureAwait(false);

            if (apiKey == null)
            {
                logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User!.Id);
                return Results.NotFound(new { error = "API key not found" });
            }

            return Results.Json(apiKey);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapPut("/api-keys/{keyId}", async (string keyId, UpdateApiKeyRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("User {UserId} updating API key {KeyId}", session.User!.Id, keyId);

            var command = new Api.BoundedContexts.Authentication.Application.Commands.UpdateApiKeyManagementCommand(
                keyId,
                session.User!.Id.ToString(),
                request);
            var updated = await mediator.Send(command, ct).ConfigureAwait(false);

            if (updated == null)
            {
                logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User!.Id);
                return Results.NotFound(new { error = "API key not found" });
            }

            logger.LogInformation("API key {KeyId} updated by user {UserId}", keyId, session.User!.Id);
            return Results.Json(updated);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapDelete("/api-keys/{keyId}", async (string keyId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("User {UserId} revoking API key {KeyId}", session.User!.Id, keyId);

            var command = new Api.BoundedContexts.Authentication.Application.Commands.RevokeApiKeyManagementCommand(keyId, session.User!.Id.ToString());
            var success = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!success)
            {
                logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User!.Id);
                return Results.NotFound(new { error = "API key not found" });
            }

            logger.LogInformation("API key {KeyId} revoked by user {UserId}", keyId, session.User!.Id);
            return Results.NoContent();
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapPost("/api-keys/{keyId}/rotate", async (string keyId, RotateApiKeyRequest? request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("User {UserId} rotating API key {KeyId}", session.User!.Id, keyId);

            var command = new Api.BoundedContexts.Authentication.Application.Commands.RotateApiKeyCommand(
                keyId,
                session.User!.Id.ToString(),
                request ?? new RotateApiKeyRequest());
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (result == null)
            {
                logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User!.Id);
                return Results.NotFound(new { error = "API key not found" });
            }

            logger.LogInformation("API key {OldKeyId} rotated to {NewKeyId} by user {UserId}", keyId, result.NewApiKey.Id, session.User!.Id);
            return Results.Json(result);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        group.MapGet("/api-keys/{keyId}/usage", async (string keyId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new Api.BoundedContexts.Authentication.Application.Queries.GetApiKeyUsageQuery(keyId, session.User!.Id.ToString());
            var usage = await mediator.Send(query, ct).ConfigureAwait(false);

            if (usage == null)
            {
                logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User!.Id);
                return Results.NotFound(new { error = "API key not found" });
            }

            return Results.Json(usage);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // ISSUE-904: Get detailed usage statistics for an API key
        group.MapGet("/api-keys/{keyId}/stats", async (string keyId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            if (!Guid.TryParse(keyId, out var keyGuid))
            {
                return Results.BadRequest(new { error = "Invalid API key ID format" });
            }

            var query = new Api.BoundedContexts.Authentication.Application.Queries.GetApiKeyUsageStatsQuery(
                keyGuid,
                session.User!.Id);
            var stats = await mediator.Send(query, ct).ConfigureAwait(false);

            if (stats == null)
            {
                logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User!.Id);
                return Results.NotFound(new { error = "API key not found" });
            }

            return Results.Json(stats);
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // ISSUE-904: Get usage logs for an API key (paginated)
        group.MapGet("/api-keys/{keyId}/logs", async (string keyId, HttpContext context, IMediator mediator, ILogger<Program> logger, int skip = 0, int take = 50, CancellationToken ct = default) =>
        {
            // Session validated by RequireSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            if (!Guid.TryParse(keyId, out var keyGuid))
            {
                return Results.BadRequest(new { error = "Invalid API key ID format" });
            }

            // Validate pagination parameters
            if (skip < 0) skip = 0;
            if (take < 1) take = 50;
            if (take > 100) take = 100; // Max 100 logs per request

            var query = new Api.BoundedContexts.Authentication.Application.Queries.GetApiKeyUsageLogsQuery(
                keyGuid,
                session.User!.Id,
                skip,
                take);
            var logs = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Json(new
            {
                logs,
                pagination = new
                {
                    skip,
                    take,
                    count = logs.Count
                }
            });
        })
        .RequireSession(); // Issue #1446: Automatic session validation

        // API-04: Admin API Key Management endpoint
        group.MapDelete("/admin/api-keys/{keyId}", async (string keyId, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated AND Admin role checked by RequireAdminSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("Admin {AdminId} permanently deleting API key {KeyId}", session.User!.Id, keyId);

            var command = new Api.BoundedContexts.Authentication.Application.Commands.DeleteApiKeyCommand(keyId, session.User!.Id.ToString());
            var success = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!success)
            {
                logger.LogWarning("API key {KeyId} not found for admin deletion", keyId);
                return Results.NotFound(new { error = "API key not found" });
            }

            logger.LogInformation("API key {KeyId} permanently deleted by admin {AdminId}", keyId, session.User!.Id);
            return Results.NoContent();
        })
        .RequireAdminSession(); // Issue #1446: Automatic admin session validation

        // ISSUE-904: Admin endpoint - Get all API keys with usage statistics
        group.MapGet("/admin/api-keys/stats", async (HttpContext context, IMediator mediator, ILogger<Program> logger, Guid? userId = null, bool includeRevoked = false, CancellationToken ct = default) =>
        {
            // Session validated AND Admin role checked by RequireAdminSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("Admin {AdminId} fetching all API keys with stats (userId filter: {UserId}, includeRevoked: {IncludeRevoked})",
                session.User!.Id, userId?.ToString() ?? "none", includeRevoked);

            var query = new Api.BoundedContexts.Authentication.Application.Queries.GetAllApiKeysWithStatsQuery(
                userId,
                includeRevoked);
            var keysWithStats = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Json(new
            {
                keys = keysWithStats,
                count = keysWithStats.Count,
                filters = new
                {
                    userId,
                    includeRevoked
                }
            });
        })
        .RequireAdminSession(); // Issue #1446: Automatic admin session validation

        // ISSUE-906: Bulk CSV export for API keys
        group.MapGet("/admin/api-keys/bulk/export", async (HttpContext context, IMediator mediator, ILogger<Program> logger, Guid? userId = null, bool? isActive = null, string? searchTerm = null, CancellationToken ct = default) =>
        {
            // Session validated AND Admin role checked by RequireAdminSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            logger.LogInformation("Admin {AdminId} exporting API keys to CSV (userId filter: {UserId}, isActive: {IsActive}, search: {SearchTerm})",
                session.User!.Id, userId?.ToString() ?? "none", isActive?.ToString() ?? "none", searchTerm ?? "none");

            var query = new Api.BoundedContexts.Authentication.Application.Queries.BulkExportApiKeysQuery(
                userId,
                isActive,
                searchTerm);
            var csv = await mediator.Send(query, ct).ConfigureAwait(false);

            var fileName = $"apikeys-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv",
                fileName);
        })
        .RequireAdminSession(); // Issue #1446: Automatic admin session validation

        // ISSUE-906: Bulk CSV import for API keys
        group.MapPost("/admin/api-keys/bulk/import", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Session validated AND Admin role checked by RequireAdminSessionFilter
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            // Read CSV content from request body
            using var reader = new StreamReader(context.Request.Body);
            var csvContent = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(csvContent))
            {
                return Results.BadRequest(new { error = "CSV content cannot be empty" });
            }

            logger.LogInformation("Admin {AdminId} importing API keys from CSV ({Size} bytes)",
                session.User!.Id, System.Text.Encoding.UTF8.GetByteCount(csvContent));

            var command = new Api.BoundedContexts.Authentication.Application.Commands.ApiKeys.BulkImportApiKeysCommand(
                csvContent,
                session.User!.Id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("Bulk API key import completed: {SuccessCount} succeeded, {FailedCount} failed",
                result.SuccessCount, result.FailedCount);

            return Results.Json(result);
        })
        .RequireAdminSession() // Issue #1446: Automatic admin session validation
        .DisableAntiforgery(); // CSV upload requires form data

        return group;
    }
}
