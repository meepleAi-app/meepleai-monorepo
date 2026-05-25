using Api.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.Persistence;

/// <summary>
/// Unit tests for AuditingSaveChangesInterceptor — verifies that before/after JSON snapshots
/// are captured for [Auditable] entities and that non-auditable entities are skipped (SP5 S1 T2).
/// Uses EF Core InMemory provider; intentionally does NOT test Postgres-specific behaviour.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class AuditingSaveChangesInterceptorTests
{
    #region Insert

    [Fact]
    public async Task CapturesAfterJson_OnInsert_OfAuditableEntity()
    {
        var capture = new TestAuditCapture();
        await using var ctx = TestContextFactory.Create(capture);

        var user = new TestAuditableUser { Id = Guid.NewGuid(), Email = "alice@x" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        capture.Snapshots.Should().HaveCount(1);
        var snap = capture.Snapshots[0];
        snap.EntityType.Should().Be(nameof(TestAuditableUser));
        snap.Operation.Should().Be(AuditOperation.Insert);
        snap.BeforeJson.Should().BeNull();
        snap.AfterJson.Should().Contain("alice@x");
    }

    #endregion

    #region Update

    [Fact]
    public async Task CapturesBeforeAndAfterJson_OnUpdate_OfAuditableEntity()
    {
        var capture = new TestAuditCapture();
        await using var ctx = TestContextFactory.Create(capture);

        var user = new TestAuditableUser { Id = Guid.NewGuid(), Email = "old@x" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        capture.Snapshots.Clear();

        user.Email = "new@x";
        await ctx.SaveChangesAsync();

        capture.Snapshots.Should().HaveCount(1);
        var snap = capture.Snapshots[0];
        snap.Operation.Should().Be(AuditOperation.Update);
        snap.BeforeJson.Should().Contain("old@x");
        snap.AfterJson.Should().Contain("new@x");
    }

    #endregion

    #region Delete

    [Fact]
    public async Task CapturesBeforeJson_OnDelete_OfAuditableEntity()
    {
        var capture = new TestAuditCapture();
        await using var ctx = TestContextFactory.Create(capture);

        var user = new TestAuditableUser { Id = Guid.NewGuid(), Email = "doomed@x" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        capture.Snapshots.Clear();

        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();

        capture.Snapshots.Should().HaveCount(1);
        var snap = capture.Snapshots[0];
        snap.Operation.Should().Be(AuditOperation.Delete);
        snap.BeforeJson.Should().Contain("doomed@x");
        snap.AfterJson.Should().BeNull();
    }

    #endregion

    #region Non-auditable skip

    [Fact]
    public async Task SkipsCapture_ForNonAuditableEntities()
    {
        var capture = new TestAuditCapture();
        await using var ctx = TestContextFactory.Create(capture);

        ctx.PlainItems.Add(new TestPlainItem { Id = Guid.NewGuid(), Value = "x" });
        await ctx.SaveChangesAsync();

        capture.Snapshots.Should().BeEmpty();
    }

    #endregion

    #region Unchanged skip

    [Fact]
    public async Task SkipsCapture_ForUnchangedAuditableEntities()
    {
        var capture = new TestAuditCapture();
        await using var ctx = TestContextFactory.Create(capture);

        var user = new TestAuditableUser { Id = Guid.NewGuid(), Email = "stable@x" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        capture.Snapshots.Clear();

        // No mutation — save changes should be a no-op for auditing
        await ctx.SaveChangesAsync();

        capture.Snapshots.Should().BeEmpty();
    }

    #endregion

    #region Multiple entities

    [Fact]
    public async Task CapturesMultipleEntities_InOneSaveChanges()
    {
        var capture = new TestAuditCapture();
        await using var ctx = TestContextFactory.Create(capture);

        ctx.Users.Add(new TestAuditableUser { Id = Guid.NewGuid(), Email = "a@x" });
        ctx.Users.Add(new TestAuditableUser { Id = Guid.NewGuid(), Email = "b@x" });
        await ctx.SaveChangesAsync();

        capture.Snapshots.Should().HaveCount(2);
        capture.Snapshots.Should().AllSatisfy(s =>
            s.Operation.Should().Be(AuditOperation.Insert));
    }

    #endregion

    #region SensitiveData redaction

    [Fact]
    public async Task RedactsSensitiveDataProperty_InSnapshot()
    {
        var capture = new TestAuditCapture();
        await using var ctx = TestContextFactory.Create(capture);

        ctx.Secrets.Add(new TestAuditableSecretHolder
        {
            Id = Guid.NewGuid(),
            Email = "a@x",
            Secret = "p@ssw0rd-hash"
        });
        await ctx.SaveChangesAsync();

        capture.Snapshots.Should().HaveCount(1);
        var afterJson = capture.Snapshots[0].AfterJson!;
        afterJson.Should().Contain("a@x");
        afterJson.Should().NotContain("p@ssw0rd-hash");
        afterJson.Should().Contain("***REDACTED***");
    }

    #endregion
}

// ─── Test doubles ────────────────────────────────────────────────────────────

internal sealed class TestAuditCapture : IAuditSnapshotSink
{
    public List<AuditSnapshot> Snapshots { get; } = new();
    public void Record(AuditSnapshot snapshot) => Snapshots.Add(snapshot);
}

[Auditable]
internal sealed class TestAuditableUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
}

[Auditable]
internal sealed class TestAuditableSecretHolder
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    [SensitiveData]
    public string Secret { get; set; } = "";
}

internal sealed class TestPlainItem
{
    public Guid Id { get; set; }
    public string Value { get; set; } = "";
}

internal sealed class TestAuditContext : DbContext
{
    public DbSet<TestAuditableUser> Users => Set<TestAuditableUser>();
    public DbSet<TestAuditableSecretHolder> Secrets => Set<TestAuditableSecretHolder>();
    public DbSet<TestPlainItem> PlainItems => Set<TestPlainItem>();

    public TestAuditContext(DbContextOptions<TestAuditContext> opts) : base(opts) { }
}

internal static class TestContextFactory
{
    public static TestAuditContext Create(IAuditSnapshotSink sink)
    {
        var opts = new DbContextOptionsBuilder<TestAuditContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditingSaveChangesInterceptor(sink))
            .Options;
        return new TestAuditContext(opts);
    }
}
