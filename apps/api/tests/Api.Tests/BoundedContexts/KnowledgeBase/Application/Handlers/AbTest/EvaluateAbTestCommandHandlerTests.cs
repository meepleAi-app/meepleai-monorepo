using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers.AbTest;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AbTest;

/// <summary>
/// Unit tests for EvaluateAbTestCommandHandler.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5494")]
public sealed class EvaluateAbTestCommandHandlerTests
{
    private readonly Mock<IAbTestSessionRepository> _repoMock = new();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid EvaluatorId = Guid.NewGuid();

    private EvaluateAbTestCommandHandler CreateSut() => new(_repoMock.Object);

    private static AbTestSession CreateSessionWithResponses()
    {
        var session = AbTestSession.Create(UserId, "Test query");
        session.AddVariant("A", "OpenRouter", "gpt-4o-mini");
        session.AddVariant("B", "OpenRouter", "claude-3-haiku");
        session.StartTest();
        session.Variants[0].RecordResponse("Response A", 100, 500, 0.001m);
        session.Variants[1].RecordResponse("Response B", 120, 600, 0.002m);
        return session;
    }

    [Fact]
    public async Task Handle_EvaluatesAllVariants_ReturnsRevealedDto()
    {
        var session = CreateSessionWithResponses();
        _repoMock.Setup(r => r.GetByIdWithVariantsAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var sut = CreateSut();
        var command = new EvaluateAbTestCommand(session.Id, EvaluatorId, [
            new VariantEvaluationInput("A", 5, 4, 3, 4, "Good"),
            new VariantEvaluationInput("B", 3, 3, 4, 5, "OK")
        ]);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Evaluated", result.Status);
        Assert.NotNull(result.WinnerLabel);
        Assert.NotNull(result.WinnerModelId);

        // Revealed mode — model info visible
        Assert.All(result.Variants, v =>
        {
            Assert.NotEmpty(v.Provider);
            Assert.NotEmpty(v.ModelId);
            Assert.NotNull(v.Evaluation);
        });

        _repoMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsInvalidOperation()
    {
        _repoMock.Setup(r => r.GetByIdWithVariantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AbTestSession?)null);

        var sut = CreateSut();
        var command = new EvaluateAbTestCommand(Guid.NewGuid(), EvaluatorId, [
            new VariantEvaluationInput("A", 5, 5, 5, 5)
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PartialEvaluation_SessionStaysInProgress()
    {
        var session = CreateSessionWithResponses();
        _repoMock.Setup(r => r.GetByIdWithVariantsAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var sut = CreateSut();
        var command = new EvaluateAbTestCommand(session.Id, EvaluatorId, [
            new VariantEvaluationInput("A", 5, 4, 3, 4)
            // B not evaluated
        ]);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.Equal("InProgress", result.Status);
        Assert.Null(result.WinnerLabel);
    }

    [Fact]
    public async Task Handle_DeterminesCorrectWinner()
    {
        var session = CreateSessionWithResponses();
        _repoMock.Setup(r => r.GetByIdWithVariantsAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var sut = CreateSut();
        var command = new EvaluateAbTestCommand(session.Id, EvaluatorId, [
            new VariantEvaluationInput("A", 2, 2, 2, 2), // avg 2.0
            new VariantEvaluationInput("B", 5, 5, 5, 5)  // avg 5.0
        ]);

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.Equal("B", result.WinnerLabel);
        Assert.Equal("claude-3-haiku", result.WinnerModelId);
    }
}
