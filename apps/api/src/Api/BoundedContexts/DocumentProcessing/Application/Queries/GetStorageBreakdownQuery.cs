using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query for storage breakdown analytics.
/// Issue #3715.4: Storage analytics by tier/user/status.
/// </summary>
public record GetStorageBreakdownQuery(
    string GroupBy,  // "tier" | "user" | "status" | "type"
    string UserRole
) : IRequest<StorageBreakdownDto>;

public record StorageBreakdownDto
{
    public required long TotalBytes { get; init; }
    public required int TotalCount { get; init; }
    public required List<StorageBreakdownItem> Breakdown { get; init; }
}

public record StorageBreakdownItem
{
    public required string Key { get; init; }
    public required long Bytes { get; init; }
    public required int PdfCount { get; init; }
    public required decimal Percentage { get; init; }
}
