namespace Api.BoundedContexts.Administration.Application.Attributes;

/// <summary>
/// Implemented by the response of an <see cref="AuditableActionAttribute"/> command whose audit row
/// needs facts that only the handler learns, such as whose row it deleted. On success the behaviour
/// adds <see cref="AuditDetails"/> to the details of the row; on failure there is no response, and
/// the row has only the command.
/// </summary>
/// <remarks>
/// #5154: the deletion of a rule comment cascades to other users' replies, and the command carries
/// only the comment id and the caller.
/// </remarks>
internal interface IAuditDetails
{
    IReadOnlyDictionary<string, object?> AuditDetails { get; }
}
