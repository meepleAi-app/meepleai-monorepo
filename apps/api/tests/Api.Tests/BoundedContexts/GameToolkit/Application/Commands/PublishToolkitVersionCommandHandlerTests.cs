using Api.BoundedContexts.GameToolkit.Application.Commands.PublishToolkitVersion;
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
/// Unit tests for <see cref="PublishToolkitVersionCommandHandler"/>
/// (issue #822 — Phase 5 PR-2 / spec-panel 2026-05-18 §3).
///
/// Covers two Gherkin scenarios:
///   - "Given owner with '1.0.0' published → When POST /versions {'1.1.0'}
///     → Then 201 + cache invalidation + ToolkitVersionPublishedEvent"
///   - "Given owner with '1.0.0' published → When POST /versions {'1.0.0'}
///     → Then 409 Conflict"
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class PublishToolkitVersionCommandHandlerTests
{
    private static readonly Guid AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid VisitorId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly DateTime Now = new(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);

    // ── Fixture builders ──────────────────────────────────────────────────────

    private sealed record HandlerHarness(
        PublishToolkitVersionCommandHandler Handler,
        Mock<IToolkitVersionRepository> VersionRepo,
        Mock<IHybridCacheService> Cache,
        Api.Infrastructure.MeepleAiDbContext Context);

    private static HandlerHarness CreateHandler(Api.Infrastructure.MeepleAiDbContext context)
    {
        var versionRepo = new Mock<IToolkitVersionRepository>();
        var cache = new Mock<IHybridCacheService>();
        var unitOfWork = new EfCoreUnitOfWork(context);
        var timeProvider = new FakeTimeProvider(Now);

        var handler = new PublishToolkitVersionCommandHandler(
            context,
            versionRepo.Object,
            unitOfWork,
            cache.Object,
            timeProvider,
            NullLogger<PublishToolkitVersionCommandHandler>.Instance);

        return new HandlerHarness(handler, versionRepo, cache, context);
    }

    private static GameToolkitEntity SeedToolkit(
        Api.Infrastructure.MeepleAiDbContext context,
        bool isPublished = true,
        Guid? authorId = null,
        string versionSemver = "1.0.0")
    {
#pragma warning disable CS0618
        var toolkit = new GameToolkitEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Toolkit",
            CreatedByUserId = authorId ?? AuthorId,
            Version = 1,
            VersionSemver = versionSemver,
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

    // ── Gherkin scenario: 201 happy path ──────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerPublishingStrictlyGreaterVersion_PersistsAndInvalidatesCache()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, versionSemver: "1.0.0");
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        var latestVersion = ToolkitVersion.Publish(toolkit.Id, "1.0.0", null, AuthorId, Now.AddDays(-7));
        harness.VersionRepo
            .Setup(r => r.ExistsAsync(toolkit.Id, "1.1.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        harness.VersionRepo
            .Setup(r => r.GetLatestNonYankedAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);

        var response = await harness.Handler.Handle(
            new PublishToolkitVersionCommand(toolkit.Id, AuthorId, "1.1.0", "Feature"),
            default);

        response.Should().NotBeNull();
        response!.VersionNumber.Should().Be("1.1.0");
        response.PublishedBy.Should().Be(AuthorId);
        response.Changelog.Should().Be("Feature");

        harness.VersionRepo.Verify(
            r => r.AddAsync(It.Is<ToolkitVersion>(v => v.VersionNumber == "1.1.0"), It.IsAny<CancellationToken>()),
            Times.Once);

        // Cache invalidation matrix (spec-panel §5).
        harness.Cache.Verify(c => c.RemoveByTagAsync($"toolkit:{toolkit.Id:N}", It.IsAny<CancellationToken>()), Times.Once);
        harness.Cache.Verify(c => c.RemoveByTagAsync("toolkitVersions", It.IsAny<CancellationToken>()), Times.Once);
        harness.Cache.Verify(c => c.RemoveAsync("toolkits:popular", It.IsAny<CancellationToken>()), Times.Once);

        // Toolkit row mutated: VersionSemver bumped, IsPublished true.
        var reloaded = await context.GameToolkits.FindAsync(toolkit.Id);
        reloaded!.VersionSemver.Should().Be("1.1.0");
        reloaded.IsPublished.Should().BeTrue();
        reloaded.UpdatedAt.Should().Be(Now);
    }

    // ── Gherkin scenario: 409 duplicate ───────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerRepublishingExistingVersion_ThrowsConflict()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, versionSemver: "1.0.0");
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        harness.VersionRepo
            .Setup(r => r.ExistsAsync(toolkit.Id, "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Func<Task> act = async () => await harness.Handler.Handle(
            new PublishToolkitVersionCommand(toolkit.Id, AuthorId, "1.0.0", null),
            default);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*permanently retired*");
    }

    // ── Monotonicity 409 ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerPublishingNonMonotonicVersion_ThrowsConflict()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        var latest = ToolkitVersion.Publish(toolkit.Id, "2.0.0", null, AuthorId, Now.AddDays(-7));
        harness.VersionRepo
            .Setup(r => r.ExistsAsync(toolkit.Id, "1.5.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        harness.VersionRepo
            .Setup(r => r.GetLatestNonYankedAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latest);

        Func<Task> act = async () => await harness.Handler.Handle(
            new PublishToolkitVersionCommand(toolkit.Id, AuthorId, "1.5.0", null),
            default);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*strictly greater*");
    }

    // ── First publish (no previous version) ───────────────────────────────────

    [Fact]
    public async Task Handle_FirstPublishWithNoPreviousVersion_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, isPublished: false, versionSemver: "0.1.0");
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);
        harness.VersionRepo
            .Setup(r => r.ExistsAsync(toolkit.Id, "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        harness.VersionRepo
            .Setup(r => r.GetLatestNonYankedAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolkitVersion?)null);

        var response = await harness.Handler.Handle(
            new PublishToolkitVersionCommand(toolkit.Id, AuthorId, "1.0.0", "Initial release"),
            default);

        response.Should().NotBeNull();
        response!.VersionNumber.Should().Be("1.0.0");

        // First publish promotes draft → published.
        var reloaded = await context.GameToolkits.FindAsync(toolkit.Id);
        reloaded!.IsPublished.Should().BeTrue();
    }

    // ── 403 non-owner ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonOwnerAttemptsPublish_ThrowsForbidden()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);

        Func<Task> act = async () => await harness.Handler.Handle(
            new PublishToolkitVersionCommand(toolkit.Id, VisitorId, "2.0.0", null),
            default);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*owner*");

        // No mutation, no cache invalidation on the forbidden path.
        harness.VersionRepo.Verify(
            r => r.AddAsync(It.IsAny<ToolkitVersion>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.Cache.Verify(
            c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── 404 toolkit not found ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ToolkitNotFound_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        await context.SaveChangesAsync();

        var harness = CreateHandler(context);

        var response = await harness.Handler.Handle(
            new PublishToolkitVersionCommand(Guid.NewGuid(), AuthorId, "1.0.0", null),
            default);

        response.Should().BeNull();
    }
}

/// <summary>Fixed-time test double for <see cref="TimeProvider"/>.</summary>
file sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FakeTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
}
