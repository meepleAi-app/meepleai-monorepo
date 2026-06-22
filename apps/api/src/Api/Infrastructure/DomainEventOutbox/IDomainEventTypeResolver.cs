namespace Api.Infrastructure.DomainEventOutbox;

/// <summary>
/// Resolves the persisted <c>event_type</c> string back to a CLR <see cref="Type"/>
/// at processor read time (issue #1535).
///
/// <para>Lookup order:</para>
/// <list type="number">
///   <item>Registry alias from <c>EventTypeRegistry.AliasByType</c> (preferred — stable
///         across CLR-type renames). Issue #661 contract: aliases are durable identifiers.</item>
///   <item>CLR <see cref="Type.FullName"/> fallback for events not yet enrolled in the
///         registry. Less stable across refactors but covers the long tail.</item>
/// </list>
///
/// <para>Returns <c>null</c> when neither resolution path matches. Caller (the
/// processor) treats this as a poison-message: the row is moved to Failed with an
/// appropriate <c>last_error</c> so ops can investigate without blocking the batch.</para>
/// </summary>
public interface IDomainEventTypeResolver
{
    /// <summary>Resolves the CLR type for the persisted alias / FullName, or null when unknown.</summary>
    Type? Resolve(string eventType);
}
