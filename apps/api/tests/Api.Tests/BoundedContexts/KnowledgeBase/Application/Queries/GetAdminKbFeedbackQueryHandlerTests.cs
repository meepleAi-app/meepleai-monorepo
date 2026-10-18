using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetAdminKbFeedbackQueryHandler.
/// KB-08: Admin Feedback Review backend. #4469: senza gioco obbligatorio, per versione del profilo.
/// Su Postgres lo prova <c>AdminKbFeedbackEndpointsTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAdminKbFeedbackQueryHandlerTests
{
    private readonly IKbUserFeedbackRepository _repo = Substitute.For<IKbUserFeedbackRepository>();
    private readonly IAgentProfileRepository _profiles = Substitute.For<IAgentProfileRepository>();
    private readonly IChatThreadRepository _threads = Substitute.For<IChatThreadRepository>();
    private readonly GetAdminKbFeedbackQueryHandler _sut;

    public GetAdminKbFeedbackQueryHandlerTests()
    {
        _threads.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ChatThread>());
        _sut = new GetAdminKbFeedbackQueryHandler(
            _repo, _profiles, _threads, TestDbContextFactory.CreateInMemoryDbContext());
    }

    [Fact]
    public async Task Handle_EmptyRepo_ReturnsEmptyPage()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var filter = new KbFeedbackFilter(gameId);
        _repo.ListAsync(filter, 1, 20, Arg.Any<CancellationToken>()).Returns(new List<KbUserFeedback>());
        _repo.CountAsync(filter, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, null, 1, 20),
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

        _repo.ListAsync(new KbFeedbackFilter(gameId), 1, 20, Arg.Any<CancellationToken>())
             .Returns(new List<KbUserFeedback> { feedback });
        _repo.CountAsync(new KbFeedbackFilter(gameId), Arg.Any<CancellationToken>())
             .Returns(1);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, null, 1, 20),
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
        item.ProfileVersionId.Should().BeNull();
        item.VersionNumber.Should().BeNull();
        item.AnswerExcerpt.Should().BeNull("il thread non esiste");
    }

    [Fact]
    public async Task Handle_WithOutcomeFilter_PassesFilterToRepo()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var filter = new KbFeedbackFilter(gameId, Outcome: "not_helpful");
        _repo.ListAsync(filter, 1, 20, Arg.Any<CancellationToken>()).Returns(new List<KbUserFeedback>());
        _repo.CountAsync(filter, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, "not_helpful", null, 1, 20),
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        await _repo.Received(1).ListAsync(filter, 1, 20, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("none", true)]
    public async Task Handle_VersionWithoutAnId_PassesTheMatchingFilter(string? version, bool withoutVersion)
    {
        _repo.ListAsync(Arg.Any<KbFeedbackFilter>(), 1, 20, Arg.Any<CancellationToken>()).Returns(new List<KbUserFeedback>());

        await _sut.Handle(new GetAdminKbFeedbackQuery(null, version, null, null, 1, 20), CancellationToken.None);

        await _repo.Received(1).ListAsync(
            new KbFeedbackFilter(WithoutVersion: withoutVersion), 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VersionId_FiltersThatVersion()
    {
        var versionId = Guid.NewGuid();
        _repo.ListAsync(Arg.Any<KbFeedbackFilter>(), 1, 20, Arg.Any<CancellationToken>()).Returns(new List<KbUserFeedback>());

        await _sut.Handle(new GetAdminKbFeedbackQuery(null, versionId.ToString(), null, null, 1, 20), CancellationToken.None);

        await _repo.Received(1).ListAsync(
            new KbFeedbackFilter(ProfileVersionId: versionId), 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VersionThatIsNeitherAnIdNorNone_IsABadRequest()
    {
        var act = () => _sut.Handle(new GetAdminKbFeedbackQuery(null, "latest", null, null, 1, 20), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
        await _repo.DidNotReceiveWithAnyArgs().ListAsync(default!, default, default, default);
    }

    [Fact]
    public async Task Handle_ALongAnswer_IsCutToTheExcerptLength()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(Guid.NewGuid(), userId, gameId);
        thread.AddUserMessage("Domanda");
        thread.AddAssistantMessage(new string('a', GetAdminKbFeedbackQueryHandler.AnswerExcerptLength + 50));
        var answer = thread.Messages.Single(m => m.IsAssistantMessage);
        var feedback = KbUserFeedback.Create(userId, gameId, thread.Id, answer.Id, "helpful", null);
        _repo.ListAsync(Arg.Any<KbFeedbackFilter>(), 1, 20, Arg.Any<CancellationToken>()).Returns(new List<KbUserFeedback> { feedback });
        _threads.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new[] { thread });

        var result = await _sut.Handle(new GetAdminKbFeedbackQuery(null, null, null, null, 1, 20), CancellationToken.None);

        result.Items.Single().AnswerExcerpt.Should()
            .Be(new string('a', GetAdminKbFeedbackQueryHandler.AnswerExcerptLength) + "…");
    }

    [Fact]
    public async Task Handle_PageSizeAboveMax_ClampsTo100()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var filter = new KbFeedbackFilter(gameId);
        _repo.ListAsync(filter, 1, 100, Arg.Any<CancellationToken>()).Returns(new List<KbUserFeedback>());
        _repo.CountAsync(filter, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, null, 1, 9999),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _repo.Received(1).ListAsync(filter, 1, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PageBelowMin_ClampsTo1()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var filter = new KbFeedbackFilter(gameId);
        _repo.ListAsync(filter, 1, 20, Arg.Any<CancellationToken>()).Returns(new List<KbUserFeedback>());
        _repo.CountAsync(filter, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, null, -5, 20),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _repo.Received(1).ListAsync(filter, 1, 20, Arg.Any<CancellationToken>());
    }
}
