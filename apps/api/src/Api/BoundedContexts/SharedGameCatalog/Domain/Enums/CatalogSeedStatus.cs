namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Lifecycle states for CatalogSeedDraft entries (admin import workflow).
/// Persisted as string for forward compatibility (varchar(32) column).
/// </summary>
public enum CatalogSeedStatus
{
    /// <summary>Just enqueued by admin, awaiting provider fetch.</summary>
    Pending = 0,

    /// <summary>Provider fetch completed, awaiting admin review.</summary>
    Fetched = 1,

    /// <summary>Provider fetch failed after N retries. Admin can manual-fill.</summary>
    FetchFailed = 2,

    /// <summary>Admin approved; copied into SharedGameCatalogEntry.</summary>
    Approved = 3,

    /// <summary>Admin rejected; soft-deleted (audit retained).</summary>
    Rejected = 4,
}
