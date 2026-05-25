using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Infrastructure.Persistence;

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

        return keyProperties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "")
            .Aggregate((a, b) => $"{a}|{b}");
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

            _ => throw new InvalidOperationException(
                $"Unexpected EntityState {entry.State} after state guard."),
        };

    private static string SerializeProperties(EntityEntry entry, bool useOriginal)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in entry.Properties)
        {
            dict[p.Metadata.Name] = useOriginal ? p.OriginalValue : p.CurrentValue;
        }
        return JsonSerializer.Serialize(dict, JsonOpts);
    }
}
