using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Unit tests for <see cref="GetLiveSessionDiaryQueryHandler"/>.
/// TDD: Tests written first (RED), then implementation (GREEN).
/// Issue #2570 SP3 T4 / T5 authz fix.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetLiveSessionDiaryQueryHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly GetLiveSessionDiaryQueryHandler _handler;

    private static readonly Guid DefaultSessionId = Guid.NewGuid();
    private static readonly Guid DefaultCreatorId = Guid.NewGuid();
    private static readonly Guid DefaultAuthorId = Guid.NewGuid();

    public GetLiveSessionDiaryQueryHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _handler = new GetLiveSessionDiaryQueryHandler(_repositoryMock.Object);
    }

    // === Helpers ===

    private static LiveGameSession CreateSession(Guid? sessionId = null, Guid? creatorId = null)
    {
        return LiveGameSession.Create(
            sessionId ?? DefaultSessionId,
            creatorId ?? DefaultCreatorId,
            "Test Game",
            TimeProvider.System);
    }

    private static LiveGameSession CreateActiveSessionWithDiary(
        Guid sessionId,
        params string[] entryTexts)
    {
        var session = CreateSession(sessionId, DefaultCreatorId);
        // Start the session so diary entries can be added
        session.AddPlayer(DefaultAuthorId, "Host", PlayerColor.Red, TimeProvider.System);
        session.Start(TimeProvider.System);

        foreach (var text in entryTexts)
        {
            session.AddDiaryEntry(DefaultAuthorId, text, TimeProvider.System);
        }

        return session;
    }

    private void SetupRepoGetById(Guid sessionId, LiveGameSession? session)
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    // === Constructor guard ===

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        var act = () => new GetLiveSessionDiaryQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // === Handle: null guard ===

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        var act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // === Handle: not found (404 takes precedence over 403) ===

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupRepoGetById(DefaultSessionId, null);
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, DefaultCreatorId);

        // Act & Assert
        var act = () => _handler.Handle(query, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.ResourceType.Should().Be("LiveGameSession");
        exception.ResourceId.Should().Be(DefaultSessionId.ToString());
    }

    // === Handle: authz — non-participant → ForbiddenException (HTTP 403) ===

    [Fact]
    public async Task Handle_CallerIsNotParticipant_ThrowsForbiddenException()
    {
        // Arrange
        var session = CreateSession(DefaultSessionId, DefaultCreatorId);
        SetupRepoGetById(DefaultSessionId, session);
        var nonParticipantId = Guid.NewGuid();
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, nonParticipantId);

        // Act & Assert
        var act = () => _handler.Handle(query, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>(
            "a caller who is neither the creator nor an active player must receive 403");
    }

    // === Handle: authz — creator can read ===

    [Fact]
    public async Task Handle_CallerIsCreator_ReturnsDiaryEntries()
    {
        // Arrange — creator is DefaultCreatorId (no players needed — creator check fires first)
        var session = CreateSession(DefaultSessionId, DefaultCreatorId);
        SetupRepoGetById(DefaultSessionId, session);
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, DefaultCreatorId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("no entries were added to this session");
    }

    // === Handle: empty diary ===

    [Fact]
    public async Task Handle_SessionWithNoDiaryEntries_ReturnsEmptyList()
    {
        // Arrange — use creator as caller so authz passes
        var session = CreateSession(DefaultSessionId, DefaultCreatorId);
        SetupRepoGetById(DefaultSessionId, session);
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, DefaultCreatorId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // === Handle: chronological order ===

    [Fact]
    public async Task Handle_SessionWithDiaryEntries_ReturnsEntriesChronologically()
    {
        // Arrange
        var session = CreateActiveSessionWithDiary(
            DefaultSessionId,
            "First entry",
            "Second entry",
            "Third entry");

        SetupRepoGetById(DefaultSessionId, session);
        // DefaultAuthorId is an active player (added in CreateActiveSessionWithDiary)
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, DefaultCreatorId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(e => e.CreatedAt);
    }

    [Fact]
    public async Task Handle_SessionWithDiaryEntries_MapsFieldsCorrectly()
    {
        // Arrange
        var session = CreateActiveSessionWithDiary(DefaultSessionId, "Great round!");
        SetupRepoGetById(DefaultSessionId, session);
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, DefaultCreatorId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        var dto = result[0];
        dto.Id.Should().NotBe(Guid.Empty);
        dto.AuthorId.Should().Be(DefaultAuthorId);
        dto.Text.Should().Be("Great round!");
        dto.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task Handle_SingleEntry_ReturnsSingleDto()
    {
        // Arrange
        var session = CreateActiveSessionWithDiary(DefaultSessionId, "Only entry");
        SetupRepoGetById(DefaultSessionId, session);
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, DefaultCreatorId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result[0].Text.Should().Be("Only entry");
    }

    [Fact]
    public async Task Handle_MultipleEntries_PreservesDistinctIds()
    {
        // Arrange
        var session = CreateActiveSessionWithDiary(DefaultSessionId, "Entry A", "Entry B");
        SetupRepoGetById(DefaultSessionId, session);
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, DefaultCreatorId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Handle_CallsRepositoryOnce()
    {
        // Arrange
        var session = CreateSession(DefaultSessionId, DefaultCreatorId);
        SetupRepoGetById(DefaultSessionId, session);
        var query = new GetLiveSessionDiaryQuery(DefaultSessionId, DefaultCreatorId);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(
            r => r.GetByIdAsync(DefaultSessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
