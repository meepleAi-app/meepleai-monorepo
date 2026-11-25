namespace Api.BoundedContexts.Administration.Application.Attributes;

/// <summary>
/// Implemented by the response of an <see cref="AuditableActionAttribute"/> command whose audit row
/// needs facts that only the handler learns, such as whose row it deleted. On success the behaviour
/// adds <see cref="AuditDetails"/> to the details of the row: next to the command's properties, never
/// replacing one of them or the caller's, and leaving out the same sensitive names. On failure there is
/// no response: a best-effort command gets the usual Error row (command type and error), an
/// <c>[AtomicAudit]</c> one no row at all.
/// </summary>
/// <remarks>
/// <para>
/// Implement it explicitly (<c>IReadOnlyDictionary&lt;string, object?&gt; IAuditDetails.AuditDetails</c>):
/// a response that an endpoint returns as JSON would otherwise publish its audit details too.
/// </para>
/// <para>
/// Keep the values small and serialisable: <c>audit_logs.details</c> is <c>varchar(1024)</c> (#5156). A
/// value that cannot be serialised does not lose the row: the row keeps the command and names the error
/// in <c>auditDetailsError</c>.
/// </para>
/// <para>
/// #5154: the deletion of a rule comment cascades to other users' replies, and the command carries
/// only the comment id and the caller.
/// </para>
/// </remarks>
internal interface IAuditDetails
{
    IReadOnlyDictionary<string, object?> AuditDetails { get; }
}
