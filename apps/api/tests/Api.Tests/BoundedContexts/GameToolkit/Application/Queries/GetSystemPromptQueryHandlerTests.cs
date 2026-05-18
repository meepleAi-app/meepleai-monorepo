using Api.BoundedContexts.GameToolkit.Application.Queries.GetSystemPrompt;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
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
/// Unit tests for <see cref="GetSystemPromptQueryHandler"/>
/// (issue #822 — Phase 5 PR-2 / spec-panel 2026-05-18 §2).
///
/// Covers the Gherkin scenario "Given public viewer on unpublished toolkit
/// → When GET /system-prompt → Then 404" plus the owner/public DTO partition
/// and the JSON parsing edge cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class GetSystemPromptQueryHandlerTests
{
    private static readonly Guid AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid VisitorId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly DateTime Now = new(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);

    private static GetSystemPromptQueryHandler CreateHandler(Api.Infrastructure.MeepleAiDbContext context)
    {
        var cache = new Mock<IHybridCacheService>();
        cache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<SystemPromptResponse>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<SystemPromptResponse>>, string[]?, TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        var timeProvider = new FakeTimeProvider(Now);

        return new GetSystemPromptQueryHandler(
            context,
            cache.Object,
            timeProvider,
            NullLogger<GetSystemPromptQueryHandler>.Instance);
    }

    private static GameToolkitEntity SeedToolkit(
        Api.Infrastructure.MeepleAiDbContext context,
        bool isPublished = true,
        Guid? authorId = null,
        string? agentConfig = null)
    {
#pragma warning disable CS0618
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
            AgentConfig = agentConfig,
            RowVersion = [0],
        };
#pragma warning restore CS0618
        context.GameToolkits.Add(toolkit);
        return toolkit;
    }

    // ── Gherkin scenario #1: public viewer on unpublished → 404 ───────────────

    [Fact]
    public async Task Handle_PublicViewerOnUnpublishedToolkit_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, isPublished: false);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetSystemPromptQuery(toolkit.Id, VisitorId), default);

        response.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ToolkitNotFound_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetSystemPromptQuery(Guid.NewGuid(), AuthorId), default);

        response.Should().BeNull();
    }

    // ── Owner DTO ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerWithFullConfig_ReturnsOwnerDtoWithFullPrompt()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var agentJson = """{"name":"Marco","mode":"strategist","systemPrompt":"You are Marco, a board-game strategist."}""";
        var toolkit = SeedToolkit(context, agentConfig: agentJson);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetSystemPromptQuery(toolkit.Id, AuthorId), default);

        response.Should().NotBeNull();
        response!.Owner.Should().NotBeNull();
        response.Public.Should().BeNull();
        response.Owner!.FullPrompt.Should().Be("You are Marco, a board-game strategist.");
        response.Owner.AgentMode.Should().Be("strategist");
        response.Owner.GeneratedAt.Should().Be(new DateTimeOffset(Now, TimeSpan.Zero));
    }

    [Fact]
    public async Task Handle_OwnerWithNoAgentConfig_ReturnsEmptyPromptDefaultMode()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, agentConfig: null);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetSystemPromptQuery(toolkit.Id, AuthorId), default);

        response!.Owner!.FullPrompt.Should().BeEmpty();
        response.Owner.AgentMode.Should().Be("default");
    }

    [Fact]
    public async Task Handle_OwnerWithMalformedJson_ReturnsEmptyPromptDefaultMode()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, agentConfig: "{not valid json");
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetSystemPromptQuery(toolkit.Id, AuthorId), default);

        response!.Owner!.FullPrompt.Should().BeEmpty();
        response.Owner.AgentMode.Should().Be("default");
    }

    [Fact]
    public async Task Handle_OwnerWithAgentModeFallback_UsesAgentModeKey()
    {
        // agentMode key fallback path (some legacy AgentConfig uses this key).
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var agentJson = """{"agentMode":"explorer","systemPrompt":"prompt"}""";
        var toolkit = SeedToolkit(context, agentConfig: agentJson);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetSystemPromptQuery(toolkit.Id, AuthorId), default);

        response!.Owner!.AgentMode.Should().Be("explorer");
    }

    // ── Public DTO ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PublicViewerOnPublishedToolkit_ReturnsPublicDtoWithCharCount()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var prompt = "You are Marco, a board-game strategist.";
        var agentJson = $$"""{"mode":"strategist","systemPrompt":"{{prompt}}"}""";
        var toolkit = SeedToolkit(context, agentConfig: agentJson);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetSystemPromptQuery(toolkit.Id, VisitorId), default);

        response.Should().NotBeNull();
        response!.Owner.Should().BeNull();
        response.Public.Should().NotBeNull();
        response.Public!.AgentMode.Should().Be("strategist");
        response.Public.CharacterCount.Should().Be(prompt.Length);
    }

    [Fact]
    public async Task Handle_PublicViewerOnDraftBelongingToSomeoneElse_ReturnsNull()
    {
        using var context = TestDbContextFactory.CreateInMemoryDbContext();
        var toolkit = SeedToolkit(context, isPublished: false, authorId: AuthorId);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetSystemPromptQuery(toolkit.Id, VisitorId), default);

        response.Should().BeNull();
    }
}

/// <summary>
/// Test double for <see cref="TimeProvider"/> — returns a fixed UTC instant
/// so cache cache-key tests + GeneratedAt assertions are deterministic.
/// </summary>
file sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FakeTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
}
