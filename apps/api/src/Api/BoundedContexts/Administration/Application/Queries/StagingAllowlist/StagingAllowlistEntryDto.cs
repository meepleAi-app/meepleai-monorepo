namespace Api.BoundedContexts.Administration.Application.Queries.StagingAllowlist;

/// <summary>
/// Read-model DTO for staging allowlist entries exposed by the admin endpoints.
/// Mirrors <see cref="Domain.Entities.StagingAllowlistEntry"/> minus soft-delete metadata.
/// </summary>
public sealed record StagingAllowlistEntryDto(
    Guid Id,
    string Email,
    Guid? AddedByUserId,
    DateTimeOffset AddedAt,
    string? Note);
