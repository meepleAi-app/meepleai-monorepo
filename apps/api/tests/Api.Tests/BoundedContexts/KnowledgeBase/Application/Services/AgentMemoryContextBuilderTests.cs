using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #2750 gap E5 — proving tests for <see cref="AgentMemoryContextBuilder"/>, the cross-context
/// read-only service that formats AgentMemory (house rules, notes, group preferences) for injection
/// into the session-agent system prompt. This class previously had ZERO test coverage even though the
/// behavior ships in production (Path 2, session-agent streaming chat).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class AgentMemoryContextBuilderTests
{
    private readonly Mock<IGameMemoryRepository> _mockGameMemoryRepository = new();
    private readonly Mock<IGroupMemoryRepository> _mockGroupMemoryRepository = new();
    private readonly Mock<IFeatureFlagService> _mockFeatureFlagService = new();

    private AgentMemoryContextBuilder BuildSut() =>
        new(
            _mockGameMemoryRepository.Object,
            _mockGroupMemoryRepository.Object,
            _mockFeatureFlagService.Object,
            NullLogger<AgentMemoryContextBuilder>.Instance);

    private void EnableFeatureFlag() =>
        _mockFeatureFlagService
            .Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<UserRole?>()))
            .ReturnsAsync(true);

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: house rule present + flag enabled → formatted context is returned
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildContextAsync_WithUserAddedHouseRule_ReturnsFormattedHouseRuleContext()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        const string ruleDescription = "No trading on turn 1";

        var gameMemory = GameMemory.Create(gameId, ownerId);
        gameMemory.AddHouseRule(ruleDescription, HouseRuleSource.UserAdded);

        EnableFeatureFlag();
        _mockGameMemoryRepository
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameMemory);

        var sut = BuildSut();

        // Act
        var context = await sut.BuildContextAsync(gameId, ownerId, groupId: null, TestContext.Current.CancellationToken);

        // Assert
        context.Should().NotBeNull();
        context.Should().Contain("House Rules for this game:");
        context.Should().Contain(ruleDescription);
        context.Should().Contain("(from player)", "UserAdded house rules render the 'from player' source label");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: feature flag disabled → returns null and repository is never queried
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildContextAsync_WhenFeatureFlagDisabled_ReturnsNullWithoutQueryingRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _mockFeatureFlagService
            .Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<UserRole?>()))
            .ReturnsAsync(false);

        var sut = BuildSut();

        // Act
        var context = await sut.BuildContextAsync(gameId, ownerId, groupId: null, TestContext.Current.CancellationToken);

        // Assert
        context.Should().BeNull();
        _mockGameMemoryRepository.Verify(
            r => r.GetByGameAndOwnerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the feature flag gate must short-circuit before any repository access");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: game memory exists but has no house rules and no notes → returns null
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildContextAsync_WithEmptyGameMemory_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var emptyGameMemory = GameMemory.Create(gameId, ownerId); // no rules, no notes

        EnableFeatureFlag();
        _mockGameMemoryRepository
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyGameMemory);

        var sut = BuildSut();

        // Act
        var context = await sut.BuildContextAsync(gameId, ownerId, groupId: null, TestContext.Current.CancellationToken);

        // Assert
        context.Should().BeNull("no house rules and no notes means there is nothing to inject");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: notes present + flag enabled → notes section is rendered
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildContextAsync_WithNotes_ReturnsFormattedNotesContext()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        const string noteContent = "We always play with the expansion";

        var gameMemory = GameMemory.Create(gameId, ownerId);
        gameMemory.AddNote(noteContent, addedByUserId: ownerId);

        EnableFeatureFlag();
        _mockGameMemoryRepository
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameMemory);

        var sut = BuildSut();

        // Act
        var context = await sut.BuildContextAsync(gameId, ownerId, groupId: null, TestContext.Current.CancellationToken);

        // Assert
        context.Should().NotBeNull();
        context.Should().Contain("Notes:");
        context.Should().Contain(noteContent);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5: no game memory at all → returns null (graceful "nothing to inject")
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildContextAsync_WhenNoGameMemory_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        EnableFeatureFlag();
        _mockGameMemoryRepository
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMemory?)null);

        var sut = BuildSut();

        // Act
        var context = await sut.BuildContextAsync(gameId, ownerId, groupId: null, TestContext.Current.CancellationToken);

        // Assert
        context.Should().BeNull();
    }
}
