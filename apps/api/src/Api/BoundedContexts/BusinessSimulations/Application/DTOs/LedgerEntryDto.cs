using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;

namespace Api.BoundedContexts.BusinessSimulations.Application.DTOs;

/// <summary>
/// DTO representing a single ledger entry (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed record LedgerEntryDto(
    Guid Id,
    DateTime Date,
    LedgerEntryType Type,
    LedgerCategory Category,
    decimal Amount,
    string Currency,
    LedgerEntrySource Source,
    string? Description,
    string? Metadata,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static LedgerEntryDto FromEntity(LedgerEntry entry) => new(
        entry.Id,
        entry.Date,
        entry.Type,
        entry.Category,
        entry.Amount.Amount,
        entry.Amount.Currency,
        entry.Source,
        entry.Description,
        entry.Metadata,
        entry.CreatedByUserId,
        entry.CreatedAt,
        entry.UpdatedAt);
}

/// <summary>
/// Paginated response DTO for ledger entries
/// </summary>
internal sealed record LedgerEntriesResponseDto(
    IReadOnlyList<LedgerEntryDto> Entries,
    int Total,
    int Page,
    int PageSize);

/// <summary>
/// Summary DTO for ledger income vs expense totals
/// </summary>
internal sealed record LedgerSummaryDto(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetBalance,
    DateTime From,
    DateTime To);
