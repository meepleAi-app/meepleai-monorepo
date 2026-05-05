using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for HeuristicHouseRuleMatcher (Phase 2 Task 2.5).
/// Validates heuristic word-overlap house rule matching for libro game assistant context.
/// Cross-BC read from AgentMemory.IGameMemoryRepository with graceful degradation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class HeuristicHouseRuleMatcherTests
{
    private readonly Mock<IGameMemoryRepository> _repo = new();

    private HeuristicHouseRuleMatcher CreateSut() =>
        new(_repo.Object, NullLogger<HeuristicHouseRuleMatcher>.Instance);

    // ─── Null/Empty guards ───────────────────────────────────────────────────

    [Fact]
    public async Task FindMatchingHouseRuleAsync_NullUserId_ReturnsNullWithoutRepoCall()
    {
        var sut = CreateSut();

        var result = await sut.FindMatchingHouseRuleAsync(
            Guid.NewGuid(), null, "test question", CancellationToken.None);

        result.Should().BeNull();
        _repo.Verify(
            r => r.GetByGameAndOwnerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FindMatchingHouseRuleAsync_NoMemory_ReturnsNull()
    {
        _repo.Setup(r => r.GetByGameAndOwnerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMemory?)null);

        var sut = CreateSut();
        var result = await sut.FindMatchingHouseRuleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "test", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindMatchingHouseRuleAsync_EmptyHouseRules_ReturnsNull()
    {
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        _repo.Setup(r => r.GetByGameAndOwnerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        var sut = CreateSut();
        var result = await sut.FindMatchingHouseRuleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "fireball costs mana", CancellationToken.None);

        result.Should().BeNull();
    }

    // ─── Matching ────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindMatchingHouseRuleAsync_HighOverlap_ReturnsRuleDescription()
    {
        var memory = BuildMemoryWithRule("Fireball costs 2 mana instead of standard 3");
        _repo.Setup(r => r.GetByGameAndOwnerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        var sut = CreateSut();
        var result = await sut.FindMatchingHouseRuleAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            "How much mana does fireball cost?",
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("mana");
    }

    [Fact]
    public async Task FindMatchingHouseRuleAsync_NoOverlap_ReturnsNull()
    {
        var memory = BuildMemoryWithRule("Fireball costs 2 mana instead of standard 3");
        _repo.Setup(r => r.GetByGameAndOwnerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        var sut = CreateSut();
        var result = await sut.FindMatchingHouseRuleAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            "How many players can play?",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindMatchingHouseRuleAsync_MultipleRules_ReturnsFirstMatch()
    {
        var memory = BuildMemoryWithRules(new[]
        {
            "Players draw 5 cards at start instead of 4",
            "Fireball costs 2 mana instead of standard 3",
            "Critical hits deal triple damage"
        });
        _repo.Setup(r => r.GetByGameAndOwnerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        var sut = CreateSut();
        var result = await sut.FindMatchingHouseRuleAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            "How much mana does fireball cost?",
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("Fireball");
    }

    // ─── Graceful degradation ────────────────────────────────────────────────

    [Fact]
    public async Task FindMatchingHouseRuleAsync_RepositoryThrows_ReturnsNullGracefully()
    {
        _repo.Setup(r => r.GetByGameAndOwnerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var sut = CreateSut();
        var result = await sut.FindMatchingHouseRuleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "test question", CancellationToken.None);

        result.Should().BeNull();
    }

    // ─── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FindMatchingHouseRuleAsync_ShortWordsOnlyQuestion_ReturnsNull()
    {
        // Words with length <= 3 are excluded from the word set (stop-word heuristic).
        // "can you win" → all words <= 3 chars → empty set → no match possible.
        var memory = BuildMemoryWithRule("Players draw 5 cards at start instead of 4");
        _repo.Setup(r => r.GetByGameAndOwnerAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        var sut = CreateSut();
        var result = await sut.FindMatchingHouseRuleAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            "can you win",
            CancellationToken.None);

        result.Should().BeNull();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static GameMemory BuildMemoryWithRule(string description) =>
        BuildMemoryWithRules(new[] { description });

    private static GameMemory BuildMemoryWithRules(IEnumerable<string> descriptions)
    {
        var memory = GameMemory.Create(Guid.NewGuid(), Guid.NewGuid());
        foreach (var desc in descriptions)
            memory.AddHouseRule(desc, HouseRuleSource.UserAdded);
        return memory;
    }
}
