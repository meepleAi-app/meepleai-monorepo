using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to export API keys to CSV format based on filters.
/// Returns CSV with metadata only (no actual keys for security).
/// Format: userId,keyName,scopes,expiresAt,metadata
/// </summary>
/// <param name="UserId">Optional user ID filter.</param>
/// <param name="IsActive">Optional active status filter.</param>
/// <param name="SearchTerm">Optional search term for key name.</param>
public record BulkExportApiKeysQuery(
    Guid? UserId = null,
    bool? IsActive = null,
    string? SearchTerm = null
) : IQuery<string>;
