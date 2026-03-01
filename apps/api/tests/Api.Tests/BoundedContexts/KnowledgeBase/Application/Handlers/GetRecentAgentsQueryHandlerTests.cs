using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using LibraryAgentConfig = Api.BoundedContexts.UserLibrary.Domain.ValueObjects.AgentConfiguration;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetRecentAgentsQueryHandler.
/// Issue #4126: Dashboard widget showing recently used agents from both
/// the agents table and UserLibraryEntry.CustomAgentConfigJson.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetRecentAgentsQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepo;
    private readonly Mock<IUserLibraryRepository> _libraryRepo;
    private readonly GetRecentAgentsQueryHandler _handler;

    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid GameId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public GetRecentAgentsQueryHandlerTests()
    {
        _agentRepo = new Mock<IAgentRepository>();
        _libraryRepo = new Mock<IUserLibraryRepository>();
        _handler = new GetRecentAgentsQueryHandler(_agentRepo.Object, _libraryRepo.Object);
    }

    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullAgentRepository_ThrowsArgumentNullException()
    {
        var act = () => new GetRecentAgentsQueryHandler(null!, _libraryRepo.Object);
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_WithNullLibraryRepository_ThrowsArgumentNullException()
    {
        var act = () => new GetRecentAgentsQueryHandler(_agentRepo.Object, null!);
        Assert.Throws<ArgumentNullException>(act);
    }

    // ── No UserId ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoUserId_ReturnsEmptyList()
    {
        var query = new GetRecentAgentsQuery(Limit: 10, UserId: null);

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Empty(result);
        _agentRepo.Verify(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _libraryRepo.Verify(r => r.GetUserGamesAsync(It.IsAny<Guid>(), It.IsAny<GameStateType?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Agents from agents table ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithUserId_ReturnsAgentsFromAgentRepository()
    {
        _agentRepo
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateAgent("Catan Agent", GameId, UserId)]);
        _libraryRepo
            .Setup(r => r.GetUserGamesAsync(UserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new GetRecentAgentsQuery(Limit: 10, UserId: UserId), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Catan Agent", result[0].Name);
    }

    [Fact]
    public async Task Handle_WithUserId_EmptyAgentTable_ReturnsEmptyFromAgentSource()
    {
        _agentRepo
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _libraryRepo
            .Setup(r => r.GetUserGamesAsync(UserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new GetRecentAgentsQuery(Limit: 10, UserId: UserId), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // ── Agents from library CustomAgentConfig ─────────────────────────────────

    [Fact]
    public async Task Handle_WithUserId_ReturnsAgentsFromLibraryWithCustomAgent()
    {
        var entry = CreateLibraryEntryWithAgent(UserId, GameId, "Esperto: Coach personalizzato");
        _agentRepo
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _libraryRepo
            .Setup(r => r.GetUserGamesAsync(UserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);

        var result = await _handler.Handle(new GetRecentAgentsQuery(Limit: 10, UserId: UserId), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Esperto", result[0].Name);
        Assert.Equal("Custom", result[0].Type);
        Assert.Equal(GameId, result[0].GameId);
    }

    [Fact]
    public async Task Handle_SkipsLibraryEntriesWithoutCustomAgent()
    {
        var entryNoAgent = new UserLibraryEntry(Guid.NewGuid(), UserId, GameId);
        // No ConfigureAgent call → HasCustomAgent() == false
        _agentRepo
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _libraryRepo
            .Setup(r => r.GetUserGamesAsync(UserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([entryNoAgent]);

        var result = await _handler.Handle(new GetRecentAgentsQuery(Limit: 10, UserId: UserId), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // ── Merge & dedup ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MergesBothSourcesAndDeduplicatesById()
    {
        var sharedId = Guid.NewGuid();
        var agentFromTable = CreateAgent("Agent A", GameId, UserId, id: sharedId);
        var entryWithSameId = CreateLibraryEntryWithAgent(UserId, GameId, "Esperto: B", id: sharedId);
        var distinctEntry = CreateLibraryEntryWithAgent(UserId, Guid.NewGuid(), "Coach: C");

        _agentRepo
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([agentFromTable]);
        _libraryRepo
            .Setup(r => r.GetUserGamesAsync(UserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([entryWithSameId, distinctEntry]);

        var result = await _handler.Handle(new GetRecentAgentsQuery(Limit: 10, UserId: UserId), TestContext.Current.CancellationToken);

        // sharedId deduped → agent table wins; distinctEntry kept
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Id == sharedId && a.Name == "Agent A");
        Assert.Contains(result, a => a.Name == "Coach");
    }

    [Fact]
    public async Task Handle_RespectsLimit()
    {
        var agents = Enumerable.Range(0, 5)
            .Select(_ => CreateAgent("A", GameId, UserId))
            .ToList();
        _agentRepo
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);
        _libraryRepo
            .Setup(r => r.GetUserGamesAsync(UserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new GetRecentAgentsQuery(Limit: 3, UserId: UserId), TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
    }

    // ── Personality parsing ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Esperto: coach dettagliato", "Esperto")]
    [InlineData("Amichevole", "Amichevole")]
    [InlineData("Coach: descrizione con: due punti", "Coach")]
    public async Task Handle_ParsesPersonalityNameCorrectly(string personality, string expectedName)
    {
        var entry = CreateLibraryEntryWithAgent(UserId, GameId, personality);
        _agentRepo
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _libraryRepo
            .Setup(r => r.GetUserGamesAsync(UserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);

        var result = await _handler.Handle(new GetRecentAgentsQuery(Limit: 10, UserId: UserId), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(expectedName, result[0].Name);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Agent CreateAgent(string name, Guid gameId, Guid createdByUserId, Guid? id = null)
    {
        var agentType = AgentType.Parse("RAG");
        var strategy = AgentStrategy.Custom("Default", new Dictionary<string, object>());
        return new Agent(id ?? Guid.NewGuid(), name, agentType, strategy, isActive: true);
    }

    private static UserLibraryEntry CreateLibraryEntryWithAgent(
        Guid userId, Guid gameId, string personality, Guid? id = null)
    {
        var entry = new UserLibraryEntry(id ?? Guid.NewGuid(), userId, gameId);
        var config = LibraryAgentConfig.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: personality,
            detailLevel: "Normale");
        entry.ConfigureAgent(config);
        return entry;
    }
}
