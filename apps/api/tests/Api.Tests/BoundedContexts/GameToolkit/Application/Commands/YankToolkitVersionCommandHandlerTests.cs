using Api.BoundedContexts.GameToolkit.Application.Commands.YankToolkitVersion;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure.Entities.GameToolkit;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Unit tests for <see cref="YankToolkitVersionCommandHandler"/>
/// (issue #822 — Phase 5 PR-2 / spec-panel 2026-05-18 §1 + §4).
///
/// Covers the Gherkin scenario "Given owner yanks the only published version
/// → Then 200 + toolkit.IsPublished=false + ToolkitVersionYankedEvent" plus
/// non-cascade (remaining versions), 403/404, and already-yanked 409 paths.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class YankToolkitVersionCommandHandlerTests
{
    private static readonly Guid AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid VisitorId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly DateTime Now = new(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);

    private sealed record HandlerHarness(
        YankToolkitVersionCommandHandler Handler,
        Mock<IToolkitVersionRepository> VersionRepo,
        Mock<IHybridCacheService> Cache,
        Api.Infrastructure.MeepleAiDbContext Context);

    private static HandlerHarness CreateHandler(Api.Infrastructure.MeepleAiDbContext context)
    {
        var versionRepo = new Mock<IToolkitVersionRepository>();
        var cache = new Mock<IHybridCacheService>();
        var unitOfWork = new EfCoreUnitOfWork(context);
        var timeProvider = new FakeTimeProvider(Now);

        var handler = new YankToolkitVersionCommandHandler(
            context,
            versionRepo.Object,
            unitOfWork,
            cache.Object,
            timeProvider,
            NullLogger<YankToolkitVersionCommandHandler>.Instance);

        return new HandlerHarness(handler, versionRepo, cache, context);
    }

    private static GameToolkitEntity SeedToolkit(
        Api.Infrastructure.MeepleAiDbContext context,
        bool isPublished = true,
        Guid? authorId = null)
    {
#pragma warning disable CS0618
        var toolkit = new GameToolkitEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Toolkit",
            CreatedByUserId = authorId ?? AuthorId,
            Version = 1,
            VersionSemver = "1.0.0",
            IsPublished = isPublished,
            TemplateStatus = (int)TemplateStatus.Approved,
            CreatedAt = Now,
            UpdatedAt = Now,
            RowVersion = [0],
        };
#pragma warning restore CS0618
        context.GameToolkits.Add(toolkit);
        return toolkit;
    }

    private static ToolkitVersionEntity SeedVersionRow(
        Api.Infrastructure.MeepleAiDbContext context,
        Guid toolkitId,
        string versionNumber,
        bool yanked = false)
    {
        var row = new ToolkitVersionEntity
        {
            Id = Guid.NewGuid(),
            ToolkitId = toolkitId,
            VersionNumber = versionNumber,
            PublishedAt = Now.AddDays(-1),
            PublishedBy = AuthorId,
            YankedAt = yanked ? Now.AddHours(-1) : null,
            YankReason = yanked ? "Earlier yank" : null,
            YankedBy = yanked ? AuthorId : null,
            RowVersion = [0],
        };
        context.Set<ToolkitVersionEntity>().Add(row);
        return row;
    }

    // ── Gherkin scenario: yank only version → cascade auto-unpublish ──────────

    [Fact]
    public async Task Handle_YankOnlyVersion_CascadesToolkitUnpublished()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, isPublished: true);
        var versionRow = SeedVersionRow(context, toolkit.Id, "1.0.0");
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        var versionAggregate = ToolkitVersion.Publish(toolkit.Id, "1.0.0", null, AuthorId, Now.AddDays(-1));
        // Force aggregate Id to match the seeded row so UpdateAsync targets the right entity.
        ForceProperty(versionAggregate, "Id", versionRow.Id);
        harness.VersionRepo
            .Setup(r => r.GetByIdAsync(versionRow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versionAggregate);

        var response = await harness.Handler.Handle(
            new YankToolkitVersionCommand(toolkit.Id, versionRow.Id, AuthorId, "Critical bug"),
            default);

        response.Should().NotBeNull();
        response!.ToolkitAutoUnpublished.Should().BeTrue();
        response.Reason.Should().Be("Critical bug");
        response.YankedAt.Should().Be(Now);

        // Toolkit was flipped to unpublished.
        var reloaded = await context.GameToolkits.FindAsync(toolkit.Id);
        reloaded!.IsPublished.Should().BeFalse();

        harness.VersionRepo.Verify(
            r => r.UpdateAsync(versionAggregate, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Cache.Verify(
            c => c.RemoveByTagAsync($"toolkit:{toolkit.Id:N}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Non-cascade: another non-yanked version remains ──────────────────────

    [Fact]
    public async Task Handle_YankOneOfTwoVersions_KeepsToolkitPublished()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, isPublished: true);
        var v100 = SeedVersionRow(context, toolkit.Id, "1.0.0");
        var v110 = SeedVersionRow(context, toolkit.Id, "1.1.0");
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        var v100Agg = ToolkitVersion.Publish(toolkit.Id, "1.0.0", null, AuthorId, Now.AddDays(-2));
        ForceProperty(v100Agg, "Id", v100.Id);
        harness.VersionRepo
            .Setup(r => r.GetByIdAsync(v100.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(v100Agg);

        var response = await harness.Handler.Handle(
            new YankToolkitVersionCommand(toolkit.Id, v100.Id, AuthorId, "Yanking older version"),
            default);

        response!.ToolkitAutoUnpublished.Should().BeFalse();

        var reloaded = await context.GameToolkits.FindAsync(toolkit.Id);
        reloaded!.IsPublished.Should().BeTrue();
    }

    // ── 404 toolkit ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ToolkitNotFound_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);

        var response = await harness.Handler.Handle(
            new YankToolkitVersionCommand(Guid.NewGuid(), Guid.NewGuid(), AuthorId, "Reason"),
            default);

        response.Should().BeNull();
    }

    // ── 404 version-not-found / route mismatch ───────────────────────────────

    [Fact]
    public async Task Handle_VersionNotFound_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        harness.VersionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolkitVersion?)null);

        var response = await harness.Handler.Handle(
            new YankToolkitVersionCommand(toolkit.Id, Guid.NewGuid(), AuthorId, "Reason"),
            default);

        response.Should().BeNull();
    }

    [Fact]
    public async Task Handle_VersionBelongsToDifferentToolkit_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkitA = SeedToolkit(context);
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        var foreignToolkitId = Guid.NewGuid();
        var foreignVersion = ToolkitVersion.Publish(foreignToolkitId, "1.0.0", null, AuthorId, Now);
        harness.VersionRepo
            .Setup(r => r.GetByIdAsync(foreignVersion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignVersion);

        var response = await harness.Handler.Handle(
            new YankToolkitVersionCommand(toolkitA.Id, foreignVersion.Id, AuthorId, "Reason"),
            default);

        response.Should().BeNull();
    }

    // ── 403 non-owner ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonOwnerAttemptsYank_ThrowsForbidden()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);

        Func<Task> act = async () => await harness.Handler.Handle(
            new YankToolkitVersionCommand(toolkit.Id, Guid.NewGuid(), VisitorId, "Reason"),
            default);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*owner*");
    }

    // ── 409 already-yanked (domain method idempotency boundary) ──────────────

    [Fact]
    public async Task Handle_AlreadyYankedVersion_ThrowsConflict()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        var yankedAggregate = ToolkitVersion.Publish(toolkit.Id, "1.0.0", null, AuthorId, Now.AddDays(-7));
        yankedAggregate.Yank(AuthorId, "First yank", Now.AddDays(-6));
        harness.VersionRepo
            .Setup(r => r.GetByIdAsync(yankedAggregate.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yankedAggregate);

        Func<Task> act = async () => await harness.Handler.Handle(
            new YankToolkitVersionCommand(toolkit.Id, yankedAggregate.Id, AuthorId, "Second yank"),
            default);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already yanked*");
    }

    // ── Reflection helper to align aggregate Id with seeded EF row ───────────

#pragma warning disable S3011 // intentional bypass — test seeding only.
    private static void ForceProperty(object instance, string propertyName, object value)
    {
        var prop = instance.GetType().GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic)
            ?? instance.GetType().BaseType!.GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property {propertyName} not found.");
        prop.SetValue(instance, value);
    }
#pragma warning restore S3011
}

/// <summary>Fixed-time test double for <see cref="TimeProvider"/>.</summary>
file sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FakeTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
}
