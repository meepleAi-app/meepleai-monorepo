using Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitVersions;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameToolkit;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Queries;

/// <summary>
/// Tests for the refreshed <see cref="GetToolkitVersionsQueryHandler"/>
/// (issue #822 — Phase 5 schema foundation). Exercises the switch from
/// the legacy stub to real <c>ToolkitVersion</c> repository reads.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class GetToolkitVersionsQueryHandlerTests
{
    private static readonly Guid AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid VisitorId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly DateTime Now = new(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);

    // ── Fixtures ─────────────────────────────────────────────────────────────

    private static GetToolkitVersionsQueryHandler CreateHandler(
        Api.Infrastructure.MeepleAiDbContext context,
        IToolkitVersionRepository versions)
    {
        var cache = new Mock<IHybridCacheService>();
        cache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ToolkitVersionsResponse>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<ToolkitVersionsResponse>>, string[]?, TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        return new GetToolkitVersionsQueryHandler(
            context,
            versions,
            cache.Object,
            NullLogger<GetToolkitVersionsQueryHandler>.Instance);
    }

    private static GameToolkitEntity SeedToolkit(
        Api.Infrastructure.MeepleAiDbContext context,
        bool isPublished = true,
        Guid? authorId = null)
    {
#pragma warning disable CS0618 // legacy Version field tested for paired-write coverage
        var toolkit = new GameToolkitEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Toolkit",
            CreatedByUserId = authorId ?? AuthorId,
            Version = 1,
            VersionSemver = "0.1.0",
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

    private static ToolkitVersionEntity SeedVersion(
        Api.Infrastructure.MeepleAiDbContext context,
        Guid toolkitId,
        string versionNumber,
        DateTime publishedAt,
        string? changelog = null,
        bool yanked = false)
    {
        var row = new ToolkitVersionEntity
        {
            Id = Guid.NewGuid(),
            ToolkitId = toolkitId,
            VersionNumber = versionNumber,
            Changelog = changelog,
            PublishedAt = publishedAt,
            PublishedBy = AuthorId,
            YankedAt = yanked ? publishedAt.AddDays(1) : null,
            YankReason = yanked ? "Test yank" : null,
            YankedBy = yanked ? AuthorId : null,
            RowVersion = [0],
        };
        context.Set<ToolkitVersionEntity>().Add(row);
        return row;
    }

    // ── Visibility (mirror legacy handler tests) ─────────────────────────────

    [Fact]
    public async Task Handle_ToolkitNotFound_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        await context.SaveChangesAsync();

        var versionsRepo = new Mock<IToolkitVersionRepository>().Object;
        var handler = CreateHandler(context, versionsRepo);

        var response = await handler.Handle(
            new GetToolkitVersionsQuery(Guid.NewGuid(), AuthorId), default);

        response.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DraftToolkitForNonOwner_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, isPublished: false);
        await context.SaveChangesAsync();

        var versionsRepo = new Mock<IToolkitVersionRepository>().Object;
        var handler = CreateHandler(context, versionsRepo);

        var response = await handler.Handle(
            new GetToolkitVersionsQuery(toolkit.Id, VisitorId), default);

        response.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DraftToolkitForOwner_ReturnsStub()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, isPublished: false);
        await context.SaveChangesAsync();

        // Empty repo — no version rows for draft.
        var versionsRepo = new Mock<IToolkitVersionRepository>();
        versionsRepo
            .Setup(r => r.GetByToolkitIdAsync(toolkit.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ToolkitVersion>());

        var handler = CreateHandler(context, versionsRepo.Object);

        var response = await handler.Handle(
            new GetToolkitVersionsQuery(toolkit.Id, AuthorId), default);

        response.Should().NotBeNull();
        response!.Items.Should().ContainSingle();
        response.Items[0].Changelog.Should().Contain("Draft");
        response.Items[0].IsCurrent.Should().BeTrue();
    }

    // ── Real entity reads (Phase 5 path) ─────────────────────────────────────

    [Fact]
    public async Task Handle_PublishedWithOneVersion_ReturnsThatVersionAsCurrent()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var version = ToolkitVersion.Publish(
            toolkit.Id, "1.0.0", "Initial release", AuthorId, Now);
        var versionsRepo = new Mock<IToolkitVersionRepository>();
        versionsRepo
            .Setup(r => r.GetByToolkitIdAsync(toolkit.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { version });

        var handler = CreateHandler(context, versionsRepo.Object);

        var response = await handler.Handle(
            new GetToolkitVersionsQuery(toolkit.Id, AuthorId), default);

        response.Should().NotBeNull();
        response!.Items.Should().ContainSingle();
        response.Items[0].Version.Should().Be("1.0.0");
        response.Items[0].Changelog.Should().Be("Initial release");
        response.Items[0].IsCurrent.Should().BeTrue();
        response.Items[0].YankedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MultipleVersions_IsCurrentOnLatestNonYanked()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        // Repository returns sorted-desc-by-publishedAt by contract (see IToolkitVersionRepository).
        var v200 = ToolkitVersion.Publish(toolkit.Id, "2.0.0", "Breaking change", AuthorId, Now);
        var v110 = ToolkitVersion.Publish(toolkit.Id, "1.1.0", "Feature", AuthorId, Now.AddDays(-7));
        var v100 = ToolkitVersion.Publish(toolkit.Id, "1.0.0", "Initial", AuthorId, Now.AddDays(-14));

        var versionsRepo = new Mock<IToolkitVersionRepository>();
        versionsRepo
            .Setup(r => r.GetByToolkitIdAsync(toolkit.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { v200, v110, v100 });

        var handler = CreateHandler(context, versionsRepo.Object);

        var response = await handler.Handle(
            new GetToolkitVersionsQuery(toolkit.Id, AuthorId), default);

        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(3);
        response.Items.Select(i => i.Version).Should().ContainInOrder("2.0.0", "1.1.0", "1.0.0");
        response.Items[0].IsCurrent.Should().BeTrue();   // 2.0.0
        response.Items[1].IsCurrent.Should().BeFalse();
        response.Items[2].IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LatestVersionYanked_IsCurrentFallsToNextNonYanked()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var v200 = ToolkitVersion.Publish(toolkit.Id, "2.0.0", "Critical bug", AuthorId, Now);
        v200.Yank(AuthorId, "Security regression", Now.AddHours(2));
        var v110 = ToolkitVersion.Publish(toolkit.Id, "1.1.0", "Feature", AuthorId, Now.AddDays(-7));

        var versionsRepo = new Mock<IToolkitVersionRepository>();
        versionsRepo
            .Setup(r => r.GetByToolkitIdAsync(toolkit.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { v200, v110 });

        var handler = CreateHandler(context, versionsRepo.Object);

        var response = await handler.Handle(
            new GetToolkitVersionsQuery(toolkit.Id, AuthorId), default);

        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(2);
        response.Items[0].Version.Should().Be("2.0.0");
        response.Items[0].YankedAt.Should().NotBeNull();
        response.Items[0].IsCurrent.Should().BeFalse();   // yanked — not current
        response.Items[1].Version.Should().Be("1.1.0");
        response.Items[1].IsCurrent.Should().BeTrue();    // fallback to next non-yanked
    }

    [Fact]
    public async Task Handle_AllVersionsYanked_NoIsCurrent()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var v100 = ToolkitVersion.Publish(toolkit.Id, "1.0.0", null, AuthorId, Now.AddDays(-7));
        v100.Yank(AuthorId, "Yank-1", Now.AddDays(-6));
        var v110 = ToolkitVersion.Publish(toolkit.Id, "1.1.0", null, AuthorId, Now);
        v110.Yank(AuthorId, "Yank-2", Now.AddHours(2));

        var versionsRepo = new Mock<IToolkitVersionRepository>();
        versionsRepo
            .Setup(r => r.GetByToolkitIdAsync(toolkit.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { v110, v100 });

        var handler = CreateHandler(context, versionsRepo.Object);

        var response = await handler.Handle(
            new GetToolkitVersionsQuery(toolkit.Id, AuthorId), default);

        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(2);
        response.Items.Should().AllSatisfy(item => item.IsCurrent.Should().BeFalse());
        response.Items.Should().AllSatisfy(item => item.YankedAt.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_PublishedToolkitForAnyViewer_ReturnsVersions()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var version = ToolkitVersion.Publish(toolkit.Id, "1.0.0", null, AuthorId, Now);
        var versionsRepo = new Mock<IToolkitVersionRepository>();
        versionsRepo
            .Setup(r => r.GetByToolkitIdAsync(toolkit.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { version });

        var handler = CreateHandler(context, versionsRepo.Object);

        // Visitor (not the author) can read published versions per visibility rule.
        var response = await handler.Handle(
            new GetToolkitVersionsQuery(toolkit.Id, VisitorId), default);

        response.Should().NotBeNull();
        response!.Items.Should().ContainSingle();
        response.Items[0].Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Handle_NullChangelog_ProjectsAsEmptyString()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context);
        await context.SaveChangesAsync();

        var version = ToolkitVersion.Publish(toolkit.Id, "1.0.0", null, AuthorId, Now);
        var versionsRepo = new Mock<IToolkitVersionRepository>();
        versionsRepo
            .Setup(r => r.GetByToolkitIdAsync(toolkit.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { version });

        var handler = CreateHandler(context, versionsRepo.Object);

        var response = await handler.Handle(
            new GetToolkitVersionsQuery(toolkit.Id, AuthorId), default);

        response!.Items[0].Changelog.Should().BeEmpty();
    }
}
