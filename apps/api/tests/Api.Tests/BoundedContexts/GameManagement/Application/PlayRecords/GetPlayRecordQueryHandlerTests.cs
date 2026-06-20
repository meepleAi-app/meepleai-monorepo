using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="GetPlayRecordQueryHandler"/>.
/// Verifies Phase 1 outcome fields: TotalScore on SessionPlayerDto,
/// WinnerPlayerIds and OutcomeType on PlayRecordDto.
/// Issue #1663: Phase 1 – reskin-required fields computed on read.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "1663")]
public class GetPlayRecordQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly Mock<IBlobStorageService> _blob;
    private readonly GetPlayRecordQueryHandler _handler;

    public GetPlayRecordQueryHandlerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _blob = new Mock<IBlobStorageService>();
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((string?)null);
        _handler = new GetPlayRecordQueryHandler(_context, _blob.Object);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Handle_RecordWithWinner_ReturnsCorrectWinnerPlayerIds()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(winnerId, recordId, ("wins", 1)),
            MakePlayer(loserId, recordId, ("wins", 0))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId, entity.CreatedByUserId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.WinnerPlayerIds.Should().ContainSingle().Which.Should().Be(winnerId);
    }

    [Fact]
    public async Task Handle_TieGame_ReturnsBothPlayersAsWinners()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(player1Id, recordId, ("wins", 1)),
            MakePlayer(player2Id, recordId, ("wins", 1))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId, entity.CreatedByUserId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.WinnerPlayerIds.Should().HaveCount(2)
            .And.Contain(player1Id)
            .And.Contain(player2Id);
    }

    [Fact]
    public async Task Handle_NoWinsDimension_ReturnsEmptyWinnersAndNoneOutcome()
    {
        // Arrange — cooperative / in-progress: no "wins" dimension
        var recordId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(player1Id, recordId, ("points", 30)),
            MakePlayer(player2Id, recordId, ("points", 45))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId, entity.CreatedByUserId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.WinnerPlayerIds.Should().BeEmpty();
        result.OutcomeType.Should().Be("none");
    }

    [Fact]
    public async Task Handle_PlayerWithPoints_ReturnsCorrectTotalScore()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(playerId, recordId, ("points", 42))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId, entity.CreatedByUserId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var player = result.Players.Should().ContainSingle().Subject;
        player.TotalScore.Should().Be(42);
    }

    [Fact]
    public async Task Handle_PlayerWithNoPoints_ReturnsTotalScoreNull()
    {
        // Arrange — player has "wins" but not "points"
        var recordId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(playerId, recordId, ("wins", 1))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId, entity.CreatedByUserId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Players.Should().ContainSingle().Subject.TotalScore.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CompetitiveGame_OutcomeTypeIsCompetitive()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(playerId, recordId, ("wins", 1))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId, entity.CreatedByUserId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.OutcomeType.Should().Be("competitive");
    }

    [Fact]
    public async Task Handle_RecordNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var query = new GetPlayRecordQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UserIsNeitherCreatorNorPlayer_ThrowsForbiddenException()
    {
        // Arrange — record owned by someone else, requester is not a player
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var record = MakePlayRecord(Guid.NewGuid());
        record.CreatedByUserId = ownerId;
        _context.PlayRecords.Add(record);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(record.Id, requesterId);

        // Act & Assert
        var act = () => _handler.Handle(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_UserIsAPlayer_ReturnsRecord()
    {
        // Arrange — requester is linked as a player (not the creator)
        var requesterId = Guid.NewGuid();
        var record = MakePlayRecord(Guid.NewGuid());
        record.CreatedByUserId = Guid.NewGuid();
        var player = MakePlayer(Guid.NewGuid(), record.Id, ("points", 10));
        player.UserId = requesterId;
        record.Players.Add(player);
        _context.PlayRecords.Add(record);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(record.Id, requesterId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(record.Id);
    }

    [Fact]
    [Trait("Issue", "2436")]
    public async Task Handle_RecordWithPhotos_ReturnsPhotosOrderedByUploadedAt()
    {
        var recordId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity> { MakePlayer(Guid.NewGuid(), recordId, ("wins", 1)) };
        entity.Photos = new List<PlayRecordPhotoEntity>
        {
            new()
            {
                Id = Guid.NewGuid(), PlayRecordId = recordId,
                BlobUrl = "play-record-photos/abc/photo1.webp", ThumbnailUrl = "play-record-photos/abc/thumb1.webp",
                FileSizeBytes = 1234, Sha256Hash = "h1", OcrText = "42", Caption = "scoreboard",
                UploadedByUserId = uploaderId, UploadedAt = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = Guid.NewGuid(), PlayRecordId = recordId,
                BlobUrl = "play-record-photos/abc/photo2.webp", ThumbnailUrl = null,
                FileSizeBytes = 5678, Sha256Hash = "h2", OcrText = null, Caption = null,
                UploadedByUserId = uploaderId, UploadedAt = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            },
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId, entity.CreatedByUserId);
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Photos.Should().HaveCount(2);
        result.Photos[0].Caption.Should().BeNull();
        result.Photos[0].Url.Should().Be("play-record-photos/abc/photo2.webp");
        result.Photos[1].Caption.Should().Be("scoreboard");
        result.Photos[1].OcrText.Should().Be("42");
        result.Photos[1].ThumbnailUrl.Should().Be("play-record-photos/abc/thumb1.webp");
    }

    #region Test Helpers

    private static PlayRecordEntity MakePlayRecord(Guid id) => new()
    {
        Id = id,
        GameId = Guid.NewGuid(),
        GameName = "Test Game",
        CreatedByUserId = Guid.NewGuid(),
        Visibility = 0,
        SessionDate = DateTime.UtcNow.AddDays(-1),
        Status = 2, // Completed
        ScoringConfigJson = """{"Dimensions":["points","wins"],"Units":{"points":"pts","wins":"W"}}""",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static RecordPlayerEntity MakePlayer(
        Guid id,
        Guid playRecordId,
        params (string Dimension, int Value)[] scores) =>
        new()
        {
            Id = id,
            PlayRecordId = playRecordId,
            DisplayName = $"Player-{id:N}",
            Scores = scores.Select(s => new RecordScoreEntity
            {
                Id = Guid.NewGuid(),
                RecordPlayerId = id,
                Dimension = s.Dimension,
                Value = s.Value
            }).ToList()
        };

    #endregion
}
