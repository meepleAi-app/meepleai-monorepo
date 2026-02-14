namespace Api.BoundedContexts.BusinessSimulations.Domain.Enums;

/// <summary>
/// Category for financial ledger entries.
/// Issue #3720: Financial Ledger Data Model
/// </summary>
public enum LedgerCategory
{
    /// <summary>User subscription revenue</summary>
    Subscription = 0,

    /// <summary>Token top-up purchases</summary>
    TokenPurchase = 1,

    /// <summary>AI token consumption costs</summary>
    TokenUsage = 2,

    /// <summary>Platform service fees</summary>
    PlatformFee = 3,

    /// <summary>Refunds issued to users</summary>
    Refund = 4,

    /// <summary>General operational costs</summary>
    Operational = 5,

    /// <summary>Marketing and advertising spend</summary>
    Marketing = 6,

    /// <summary>Server, hosting, and infrastructure costs</summary>
    Infrastructure = 7,

    /// <summary>Uncategorized or miscellaneous</summary>
    Other = 8
}
