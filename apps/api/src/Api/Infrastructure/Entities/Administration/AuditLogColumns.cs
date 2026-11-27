using Api.Infrastructure.Persistence;

namespace Api.Infrastructure.Entities;

/// <summary>
/// The lengths of the bounded text columns of <c>audit_logs</c>, read by <c>AuditLogEntityConfiguration</c>, and the
/// cut that makes a row fit them (#5156).
/// </summary>
/// <remarks>
/// Some of these values are chosen by the client (<c>user_agent</c> is the <c>User-Agent</c> header) or carried by the
/// command (<c>resource_id</c>, <c>details</c>). Postgres refuses a longer value instead of cutting it, and the row is
/// lost; in a batch of the outbox processor, so was every row after it. Every writer of <c>audit_logs</c> fits the row
/// just before adding it.
/// </remarks>
internal static class AuditLogColumns
{
    public const int ActionLength = 64;
    public const int ResourceLength = 128;
    public const int ResourceIdLength = 64;
    public const int ResultLength = 32;
    public const int DetailsLength = 1024;
    public const int IpAddressLength = 64;
    public const int UserAgentLength = 256;

    /// <summary>Cuts each bounded text of <paramref name="row"/> to its column, and returns the row.</summary>
    public static AuditLogEntity FitToColumns(this AuditLogEntity row)
    {
        ArgumentNullException.ThrowIfNull(row);

        row.Action = ColumnText.Fit(row.Action, ActionLength);
        row.Resource = ColumnText.Fit(row.Resource, ResourceLength);
        row.ResourceId = ColumnText.Fit(row.ResourceId, ResourceIdLength);
        row.Result = ColumnText.Fit(row.Result, ResultLength);
        row.Details = ColumnText.Fit(row.Details, DetailsLength);
        row.IpAddress = ColumnText.Fit(row.IpAddress, IpAddressLength);
        row.UserAgent = ColumnText.Fit(row.UserAgent, UserAgentLength);
        return row;
    }
}
