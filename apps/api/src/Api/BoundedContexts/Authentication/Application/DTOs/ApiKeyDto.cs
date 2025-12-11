

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Data transfer object for API key information.
/// </summary>
public record ApiKeyDto(
    Guid Id,
    string KeyName,
    string KeyPrefix,
    string Scopes,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsActive
);

/// <summary>
/// DTO for creating an API key.
/// </summary>
public record CreateApiKeyRequest(
    string KeyName,
    string Scopes,
    DateTime? ExpiresAt = null,
    string? Metadata = null
);

/// <summary>
/// DTO for API key creation response.
/// Includes the plaintext key (only shown once).
/// </summary>
public record CreateApiKeyResponse(
    Guid Id,
    string KeyName,
    string KeyPrefix,
    string PlaintextKey,
    string Scopes,
    DateTime CreatedAt,
    DateTime? ExpiresAt
);
