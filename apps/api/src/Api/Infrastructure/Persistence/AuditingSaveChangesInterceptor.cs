using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Infrastructure.Persistence;

/// <summary>Immutable before/after snapshot of a single audited entity mutation.</summary>
/// <remarks>
/// <paramref name="PrimaryKey"/>: for composite primary keys, components are joined with the
/// separator <c>"|"</c> (see <see cref="AuditingSaveChangesInterceptor.PkSeparator"/>).
/// Example for a composite (Guid, int) PK: <c>"ab12...|42"</c>. Downstream consumers
/// (the T4 outbox processor) rely on this convention to parse individual PK components.
/// </remarks>
public record AuditSnapshot(
    string EntityType,
    string PrimaryKey,
    string? BeforeJson,
    string? AfterJson,
    AuditOperation Operation);

public enum AuditOperation { Insert, Update, Delete }

public interface IAuditSnapshotSink
{
    void Record(AuditSnapshot snapshot);
}

/// <summary>
/// Captures before/after JSON snapshots of <see cref="AuditableAttribute"/>-decorated entities
/// immediately before SaveChanges commits. Snapshots are forwarded to
/// <see cref="IAuditSnapshotSink"/> — the AuditLoggingBehavior reads them when an
/// [AuditableAction] command is in flight and writes the results to audit_outbox.
/// </summary>
/// <remarks>
/// Works with any DbContext — does not reference MeepleAiDbContext directly.
/// Only scalar properties are serialised (<see cref="EntityEntry.Properties"/>);
/// navigation properties (references and collections) are intentionally excluded to
/// prevent circular-reference serialisation failures and avoid leaking child data.
/// </remarks>
public sealed class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IAuditSnapshotSink _sink;

    /// <summary>
    /// Separator used to encode composite primary keys in <see cref="AuditSnapshot.PrimaryKey"/>.
    /// Example for a composite (Guid, int) PK: <c>"ab123...|42"</c>. Downstream consumers
    /// (the T4 outbox processor) rely on this convention to parse individual PK components.
    /// </summary>
    internal const string PkSeparator = "|";

    /// <summary>
    /// Placeholder value written into the audit snapshot instead of a property's actual value
    /// when that property is decorated with <see cref="SensitiveDataAttribute"/>.
    /// </summary>
    internal const string RedactedPlaceholder = "***REDACTED***";

    /// <summary>Maximum JSON byte size for a single audit snapshot payload (three-amigos Q2).</summary>
    private const int MaxPayloadBytes = 256_000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public AuditingSaveChangesInterceptor(IAuditSnapshotSink sink)
        => _sink = sink ?? throw new ArgumentNullException(nameof(sink));

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx is not null)
            CaptureSnapshots(ctx);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // Synchronous override so tests using InMemory database (which calls SaveChanges
    // internally on EF core in-memory provider) also get coverage.
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        var ctx = eventData.Context;
        if (ctx is not null)
            CaptureSnapshots(ctx);

        return base.SavingChanges(eventData, result);
    }

    private void CaptureSnapshots(DbContext ctx)
    {
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
                continue;

            var clrType = entry.Entity.GetType();
            if (clrType.GetCustomAttribute<AuditableAttribute>() is null)
                continue;

            var pk = ResolvePrimaryKey(entry);
            var (before, after, op) = ResolveSnapshotData(entry);

            _sink.Record(new AuditSnapshot(clrType.Name, pk, before, after, op));
        }
    }

    private static string ResolvePrimaryKey(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null or { Count: 0 })
            return "";

        // LIMITATION: For Insert (EntityState.Added) of entities with DB-generated integer PKs
        // (e.g. `ValueGeneratedOnAdd()` identity columns), CurrentValue here is the CLR default (0).
        // PK resolution is accurate only when the caller assigns the PK before EF tracking
        // (e.g. Guid PKs assigned via `Guid.NewGuid()` in factory methods). All current MeepleAI
        // entities use pre-assigned Guid PKs, so this is not an issue today.
        return keyProperties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "")
            .Aggregate((a, b) => $"{a}{PkSeparator}{b}");
    }

    private static (string? before, string? after, AuditOperation op) ResolveSnapshotData(
        EntityEntry entry)
        => entry.State switch
        {
            EntityState.Added => (
                null,
                SerializeProperties(entry, useOriginal: false),
                AuditOperation.Insert),

            EntityState.Modified => (
                SerializeProperties(entry, useOriginal: true),
                SerializeProperties(entry, useOriginal: false),
                AuditOperation.Update),

            EntityState.Deleted => (
                SerializeProperties(entry, useOriginal: true),
                null,
                AuditOperation.Delete),

            _ => throw new UnreachableException(
                $"Unexpected EntityState {entry.State} after state guard."),
        };

    private static string SerializeProperties(EntityEntry entry, bool useOriginal)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var p in entry.Properties)
        {
            // Redact properties marked [SensitiveData] (PropertyInfo is null for shadow properties —
            // they have no CLR member to carry the attribute, so they are never sensitive by this
            // mechanism).
            if (p.Metadata.PropertyInfo?.GetCustomAttribute<SensitiveDataAttribute>() is not null)
            {
                dict[p.Metadata.Name] = RedactedPlaceholder;
                continue;
            }
            dict[p.Metadata.Name] = useOriginal ? p.OriginalValue : p.CurrentValue;
        }

        // Capture navigation collections (e.g. a User's Games) so the audit reflects related-entity
        // membership. Only the CURRENT value is available for navigations — EF does not track an
        // "original" collection snapshot — so we capture current regardless of useOriginal.
        foreach (var nav in entry.Collections)
        {
            if (nav.CurrentValue is IEnumerable enumerable && nav.CurrentValue is not string)
            {
                dict[nav.Metadata.Name] = enumerable.Cast<object?>().ToList();
            }
        }

        var truncated = PayloadTruncator.Truncate(dict, MaxPayloadBytes);
        return JsonSerializer.Serialize(truncated, JsonOpts);
    }
}
