namespace Api.BoundedContexts.BusinessSimulations.Domain.Enums;

/// <summary>
/// Type of financial ledger entry.
/// Issue #3720: Financial Ledger Data Model
/// </summary>
public enum LedgerEntryType
{
    /// <summary>Revenue or incoming funds</summary>
    Income = 0,

    /// <summary>Cost or outgoing funds</summary>
    Expense = 1
}
