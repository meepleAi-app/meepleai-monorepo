using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="GenerateAgentSummaryHandler"/>.
/// Game Night Improvvisata — E4: async session recap generation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GenerateAgentSummaryHandlerTests
{
    // ─── Fixtures ─────────────────────────────────────────────────────────────

    private static readonly Guid TestSnapshotId = Guid.NewGuid();
    private static readonly Guid TestSessionId = Guid.NewGuid();
    private static readonly Guid TestAgentId = Guid.NewGuid();

    private const string DefaultSummary =
        "Quando avete messo in pausa la partita, il turno 5 era appena iniziato.";

    private readonly Mock<IAgentDefinitionRepository> _agentRepoMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly GenerateAgentSummaryHandler _sut;

    public GenerateAgentSummaryHandlerTests()
    {
        _agentRepoMock = new Mock<IAgentDefinitionRepository>();
        _llmServiceMock = new Mock<ILlmService>();
        _publisherMock = new Mock<IPublisher>();

        // Default: agent definition returns null (not found is gracefully handled)
        _agentRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition?)null);

        // Default: LLM returns a summary
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(DefaultSummary));

        _publisherMock
            .Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new GenerateAgentSummaryHandler(
            _agentRepoMock.Object,
            _llmServiceMock.Object,
            _publisherMock.Object,
            NullLogger<GenerateAgentSummaryHandler>.Instance);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static SessionSaveRequestedEvent BuildEvent(
        List<string>? messages = null,
        Guid? snapshotId = null,
        Guid? agentId = null)
        => new SessionSaveRequestedEvent(
            pauseSnapshotId: snapshotId ?? TestSnapshotId,
            liveGameSessionId: TestSessionId,
            agentDefinitionId: agentId ?? TestAgentId,
            lastMessages: messages ?? new List<string>
            {
                "[user] Chi vince se abbiamo lo stesso punteggio?",
                "[assistant] Secondo le regole, in caso di parità vince il giocatore che ha giocato meno turni in totale.",
                "[user] Grazie! Facciamo pausa."
            });

    // ─── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_PublishesAgentSummaryGeneratedEvent()
    {
        // Arrange
        var notification = BuildEvent();

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<AgentSummaryGeneratedEvent>(e =>
                    e.PauseSnapshotId == TestSnapshotId &&
                    e.Summary == DefaultSummary),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_HappyPath_CallsLlmWithSummaryPrompt()
    {
        // Arrange
        var notification = BuildEvent();

        string? capturedSystemPrompt = null;
        string? capturedUserPrompt = null;

        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>(
                (sys, usr, _, _) =>
                {
                    capturedSystemPrompt = sys;
                    capturedUserPrompt = usr;
                })
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(DefaultSummary));

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedSystemPrompt);
        Assert.Contains("riassunto", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(capturedUserPrompt);
        // User prompt should include actual message content
        Assert.Contains("[user]", capturedUserPrompt, StringComparison.Ordinal);
        Assert.Contains("[assistant]", capturedUserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_HappyPath_CapsAtLast50Messages()
    {
        // Arrange — provide 75 messages
        var messages = Enumerable
            .Range(1, 75)
            .Select(i => $"[user] Messaggio {i}")
            .ToList();

        var notification = BuildEvent(messages: messages);

        string? capturedUserPrompt = null;
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>(
                (_, usr, _, _) => capturedUserPrompt = usr)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(DefaultSummary));

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert — only last 50 messages (26..75) should be in the prompt
        Assert.NotNull(capturedUserPrompt);
        Assert.Contains("Messaggio 75", capturedUserPrompt, StringComparison.Ordinal);
        Assert.Contains("Messaggio 26", capturedUserPrompt, StringComparison.Ordinal);
        capturedUserPrompt.Should().NotContain("Messaggio 25");
        capturedUserPrompt.Should().NotContain("Messaggio 1");
    }

    // ─── Failure cases ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoMessages_DoesNotCallLlmOrPublish()
    {
        // Arrange
        var notification = BuildEvent(messages: new List<string>());

        // Act — should not throw
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        _llmServiceMock.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _publisherMock.Verify(
            p => p.Publish(It.IsAny<AgentSummaryGeneratedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLlmFails_DoesNotPublishAndDoesNotThrow()
    {
        // Arrange
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("OpenRouter unavailable"));

        var notification = BuildEvent();

        // Act — should NOT throw; summary is optional
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        _publisherMock.Verify(
            p => p.Publish(It.IsAny<AgentSummaryGeneratedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLlmThrows_DoesNotPropagateException()
    {
        // Arrange
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var notification = BuildEvent();

        // Act — must not throw; fire-and-forget handler
        var exception = await Record.ExceptionAsync(
            () => _sut.Handle(notification, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_WhenLlmReturnsEmptySummary_DoesNotPublish()
    {
        // Arrange
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(string.Empty));

        var notification = BuildEvent();

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        _publisherMock.Verify(
            p => p.Publish(It.IsAny<AgentSummaryGeneratedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAgentDefinitionNotFound_StillGeneratesSummary()
    {
        // Arrange — agent definition returns null (uses generic name)
        _agentRepoMock
            .Setup(r => r.GetByIdAsync(TestAgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition?)null);

        var notification = BuildEvent();

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert — should still call LLM and publish
        _publisherMock.Verify(
            p => p.Publish(
                It.IsAny<AgentSummaryGeneratedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
