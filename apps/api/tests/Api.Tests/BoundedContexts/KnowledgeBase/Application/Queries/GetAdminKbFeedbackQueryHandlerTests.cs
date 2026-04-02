using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetAdminKbFeedbackQueryHandler.
/// KB-08: Admin Feedback Review backend.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAdminKbFeedbackQueryHandlerTests
{
    private readonly IKbUserFeedbackRepository _repo = Substitute.For<IKbUserFeedbackRepository>();
    private readonly GetAdminKbFeedbackQueryHandler _sut;

    public GetAdminKbFeedbackQueryHandlerTests()
        => _sut = new GetAdminKbFeedbackQueryHandler(_repo);

    [Fact]
    public async Task Handle_EmptyRepo_ReturnsEmptyPage()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repo.GetByGameIdAsync(gameId, null, null, 1, 20, Arg.Any<CancellationToken>())
             .Returns(new List<KbUserFeedback>());
        _repo.CountByGameIdAsync(gameId, null, null, Arg.Any<CancellationToken>())
             .Returns(0);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, 1, 20),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithFeedback_ReturnsMappedItems()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var feedback = KbUserFeedback.Create(
            userId, gameId, sessionId, messageId, "helpful", "Great answer!");

        _repo.GetByGameIdAsync(gameId, null, null, 1, 20, Arg.Any<CancellationToken>())
             .Returns(new List<KbUserFeedback> { feedback });
        _repo.CountByGameIdAsync(gameId, null, null, Arg.Any<CancellationToken>())
             .Returns(1);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, 1, 20),
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);

        var item = result.Items.First();
        item.UserId.Should().Be(userId);
        item.GameId.Should().Be(gameId);
        item.ChatSessionId.Should().Be(sessionId);
        item.MessageId.Should().Be(messageId);
        item.Outcome.Should().Be("helpful");
        item.Comment.Should().Be("Great answer!");
    }

    [Fact]
    public async Task Handle_WithOutcomeFilter_PassesFilterToRepo()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repo.GetByGameIdAsync(gameId, "not_helpful", null, 1, 20, Arg.Any<CancellationToken>())
             .Returns(new List<KbUserFeedback>());
        _repo.CountByGameIdAsync(gameId, "not_helpful", null, Arg.Any<CancellationToken>())
             .Returns(0);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, "not_helpful", null, 1, 20),
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        await _repo.Received(1).GetByGameIdAsync(
            gameId, "not_helpful", null, 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PageSizeAboveMax_ClampsTo100()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repo.GetByGameIdAsync(gameId, null, null, 1, 100, Arg.Any<CancellationToken>())
             .Returns(new List<KbUserFeedback>());
        _repo.CountByGameIdAsync(gameId, null, null, Arg.Any<CancellationToken>())
             .Returns(0);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, 1, 9999),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _repo.Received(1).GetByGameIdAsync(
            gameId, null, null, 1, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PageBelowMin_ClampsTo1()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repo.GetByGameIdAsync(gameId, null, null, 1, 20, Arg.Any<CancellationToken>())
             .Returns(new List<KbUserFeedback>());
        _repo.CountByGameIdAsync(gameId, null, null, Arg.Any<CancellationToken>())
             .Returns(0);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, -5, 20),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _repo.Received(1).GetByGameIdAsync(
            gameId, null, null, 1, 20, Arg.Any<CancellationToken>());
    }
}
