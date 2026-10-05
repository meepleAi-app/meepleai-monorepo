using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetAllAgentsQueryHandler — used by GET /api/v1/games/{id}/agents.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAllAgentsQueryHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly Mock<ISharedGameRepository> _mockSharedGameRepository;
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepository;
    private readonly GetAllAgentsQueryHandler _handler;

    public GetAllAgentsQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _mockSharedGameRepository = new Mock<ISharedGameRepository>();
        _mockLibraryRepository = new Mock<IUserLibraryRepository>();

        // Default: no agents have a GameId, so handler should not call GetNamesByIdsAsync at all.
        // Tests that exercise the GameName population path override this setup explicitly.

        // Default: library returns empty — scope branch is a no-op unless overridden.
        _mockLibraryRepository
            .Setup(r => r.GetUserGamesAsync(
                It.IsAny<Guid>(),
                It.IsAny<GameStateType?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserLibraryEntry>());

        _handler = new GetAllAgentsQueryHandler(
            _mockRepository.Object,
            _mockSharedGameRepository.Object,
            _mockLibraryRepository.Object);
    }

    [Fact]
    public async Task Handle_ActiveOnly_ShouldCallGetAllActiveAsync()
    {
        // Arrange
        var agents = new List<AgentDefinitionEntity>
        {
            CreateAgent("QA Agent"),
            CreateAgent("Rules Agent")
        };

        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetAllAgentsQuery(ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.Should().BeOfType<AgentDto>());
        _mockRepository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoFilter_ShouldCallGetAllAsync()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity>());

        var query = new GetAllAgentsQuery();

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MapsFieldsCorrectly()
    {
        // Arrange
        var agent = CreateAgent("Test Agent");
        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity> { agent });

        var query = new GetAllAgentsQuery(ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        var dto = result[0];
        dto.Name.Should().Be("Test Agent");
        dto.Type.Should().NotBeNullOrEmpty();
        dto.StrategyName.Should().NotBeNullOrEmpty();
        dto.IsActive.Should().BeFalse(); // AgentDefinition.Create sets IsActive=false
    }

    [Fact]
    public async Task Handle_EmptyResult_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity>());

        var query = new GetAllAgentsQuery(ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AgentLinkedToGame_PopulatesGameNameFromBulkLookup()
    {
        // Arrange (Issue #660): one agent linked to a game, repository returns matching name.
        var gameId = Guid.NewGuid();
        var agent = CreateAgent("Catan Helper");
        agent.SetGameId(gameId);

        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity> { agent });

        _mockSharedGameRepository
            .Setup(r => r.GetNamesByIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(gameId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [gameId] = "Catan" });

        var query = new GetAllAgentsQuery(ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result[0].GameId.Should().Be(gameId);
        result[0].GameName.Should().Be("Catan");
        _mockSharedGameRepository.Verify(
            r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoAgentsLinkedToGame_SkipsBulkNameLookup()
    {
        // Arrange (Issue #660): zero agents have GameId, so handler must NOT call GetNamesByIdsAsync.
        var agent = CreateAgent("Standalone Agent"); // no SetGameId call
        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity> { agent });

        var query = new GetAllAgentsQuery(ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result[0].GameId.Should().BeNull();
        result[0].GameName.Should().BeNull();
        _mockSharedGameRepository.Verify(
            r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ========== BE-2 #1589: scope=my-library tests ==========

    [Fact]
    public async Task Handle_NoScope_DoesNotCallLibraryRepository()
    {
        // Arrange: query has no Scope set — library repo must never be consulted.
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity>());

        var query = new GetAllAgentsQuery();

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLibraryRepository.Verify(
            r => r.GetUserGamesAsync(
                It.IsAny<Guid>(),
                It.IsAny<GameStateType?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ScopeMyLibrary_ReturnsLibraryGamesPlusSystemAgents()
    {
        // Arrange (BE-2 #1589): 3 agents — one in library, one NOT in library, one system (no GameId).
        // Only the library agent + system agent should be returned.
        var catanId = Guid.NewGuid();
        var azulId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var a1Catan = CreateAgent("Catan Helper");
        a1Catan.SetGameId(catanId);

        var a2Azul = CreateAgent("Azul Helper");
        a2Azul.SetGameId(azulId);

        // 🔴 #4081: questa riga diceva `// GameId = null (system agent)`, cioe' trattava
        // «senza gioco» come sinonimo di «di sistema» — la stessa confusione che il filtro
        // dell'handler aveva nel codice. Ora l'agente di sistema e' creato con la factory che
        // imposta davvero `IsSystemDefined`, e la distinzione fra i due concetti e' coperta dal
        // test `Handle_ScopeMyLibrary_ExcludesGamelessAgentThatIsNotSystemDefined` sotto.
        var a3System = CreateSystemAgent("Global Rules Agent");

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity> { a1Catan, a2Azul, a3System });

        // Library contains ONLY Catan, NOT Azul
        var catanEntry = new UserLibraryEntry(Guid.NewGuid(), userId, catanId);
        _mockLibraryRepository
            .Setup(r => r.GetUserGamesAsync(
                userId,
                It.IsAny<GameStateType?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserLibraryEntry> { catanEntry });

        _mockSharedGameRepository
            .Setup(r => r.GetNamesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [catanId] = "Catan" });

        var query = new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert: a1 (Catan, in library) + a3 (system, no GameId) — NOT a2 (Azul, not in library)
        result.Should().HaveCount(2);
        result.Select(a => a.GameId).Should()
            .BeEquivalentTo(new Guid?[] { catanId, null });
        result.First(a => a.GameId == catanId).GameName.Should().Be("Catan");
        result.First(a => a.GameId == null).GameName.Should().BeNull();

        // #4081: il flag arriva fino al DTO, altrimenti il client non puo' distinguerlo.
        result.First(a => a.GameId == null).IsSystemDefined.Should().BeTrue();
        result.First(a => a.GameId == catanId).IsSystemDefined.Should().BeFalse();

        _mockLibraryRepository.Verify(
            r => r.GetUserGamesAsync(
                userId,
                It.IsAny<GameStateType?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #4081 — il caso che il commento dell'handler dichiarava di escludere e che il
    // codice invece ammetteva.
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_ScopeMyLibrary_ExcludesGamelessAgentThatIsNotSystemDefined()
    {
        // Il filtro era `!a.GameId.HasValue || libraryGameIds.Contains(...)`, cioe' «qualunque
        // agente senza gioco», mentre il commento sopra promette «agenti di sistema». Un agente
        // senza gioco e NON di sistema comparirebbe quindi nella libreria personale di OGNI
        // utente. Oggi non e' creabile da un utente (`CreateUserAgentCommand` prende un
        // `Guid GameId` non nullable), ma per via amministrativa si', e il codice deve dire
        // cio' che il commento promette.
        var userId = Guid.NewGuid();

        var gameless = CreateAgent("Orphan Agent");           // GameId null, NON di sistema
        var system = CreateSystemAgent("Global Rules Agent");  // GameId null, di sistema

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity> { gameless, system });

        _mockLibraryRepository
            .Setup(r => r.GetUserGamesAsync(userId, It.IsAny<GameStateType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserLibraryEntry>());

        _mockSharedGameRepository
            .Setup(r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var result = await _handler.Handle(
            new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: userId),
            CancellationToken.None);

        // L'agente di sistema DEVE comparire: senza questa asserzione un filtro che escludesse
        // tutto passerebbe quella sotto, e un account nuovo resterebbe senza nulla da usare.
        result.Select(a => a.Name).Should().Contain("Global Rules Agent");

        result.Select(a => a.Name).Should().NotContain("Orphan Agent",
            because: "#4081 — un agente senza gioco e non di sistema non appartiene alla " +
                     "libreria personale di nessuno");

        result.Should().HaveCount(1);
    }

    private static AgentDefinitionEntity CreateSystemAgent(string name)
    {
        // #4081: `CreateSystem` usa il costruttore interno che imposta davvero IsSystemDefined,
        // a differenza di `Create` + GameId null, che e' solo «senza gioco».
        return AgentDefinitionEntity.CreateSystem(
            name,
            $"Description for {name}",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default(),
            typologySlug: "rules");
    }

    private static AgentDefinitionEntity CreateAgent(string name)
    {
        return AgentDefinitionEntity.Create(
            name,
            $"Description for {name}",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());
    }
}
