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

        return group;
    }
}
