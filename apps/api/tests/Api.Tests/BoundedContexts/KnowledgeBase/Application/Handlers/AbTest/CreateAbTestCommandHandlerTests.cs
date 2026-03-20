using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AbTest;

/// <summary>
/// Unit tests for CreateAbTestCommandHandler.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5494")]
public sealed class CreateAbTestCommandHandlerTests
{
    private readonly Mock<IAbTestSessionRepository> _repoMock = new();
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly Mock<IAbTestBudgetService> _budgetServiceMock = new();
    private readonly ILogger<CreateAbTestCommandHandler> _logger = new LoggerFactory().CreateLogger<CreateAbTestCommandHandler>();

    private CreateAbTestCommandHandler CreateSut() =>
        new(_repoMock.Object, _llmServiceMock.Object, _budgetServiceMock.Object, _logger);

    private static readonly Guid UserId = Guid.NewGuid();

    public CreateAbTestCommandHandlerTests()
    {
        // Default: budget and rate limit available
        _budgetServiceMock.Setup(b => b.HasBudgetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _budgetServiceMock.Setup(b => b.HasRateLimitRemainingAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Default: LLM returns success
        _llmServiceMock
            .Setup(l => l.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Test response",
                new LlmUsage(50, 30, 80),
                new LlmCost { InputCost = 0.001m, OutputCost = 0.002m, ModelId = "test", Provider = "Test" }));
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesSessionAndGeneratesResponses()
    {
        var sut = CreateSut();
        var command = new CreateAbTestCommand(UserId, "What are the rules?", ["openai/gpt-4o-mini", "anthropic/claude-3-haiku"]);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Query.Should().Be("What are the rules?");
        result.Status.Should().Be("InProgress");
        result.Variants.Count.Should().Be(2);
        result.Variants[0].Label.Should().Be("A");
        result.Variants[1].Label.Should().Be("B");

        _repoMock.Verify(r => r.AddAsync(It.IsAny<AbTestSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UsesAbTestingRequestSource()
    {
        var sut = CreateSut();
        var command = new CreateAbTestCommand(UserId, "Test", ["m1", "m2"]);

        await sut.Handle(command, CancellationToken.None);

        _llmServiceMock.Verify(l => l.GenerateCompletionWithModelAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            RequestSource.ABTesting, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_RecordsBudgetUsage()
    {
        var sut = CreateSut();
        var command = new CreateAbTestCommand(UserId, "Test", ["m1", "m2"]);

        await sut.Handle(command, CancellationToken.None);

        _budgetServiceMock.Verify(b => b.RecordTestCostAsync(
            It.Is<decimal>(c => c > 0), It.IsAny<CancellationToken>()), Times.Once);

        _budgetServiceMock.Verify(b => b.RecordTestExecutionAsync(
            UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBudgetExhausted_ThrowsInvalidOperation()
    {
        _budgetServiceMock.Setup(b => b.HasBudgetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = CreateSut();
        var command = new CreateAbTestCommand(UserId, "Test", ["m1", "m2"]);

        Func<Task> act = () =>
            sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WhenRateLimitReached_ThrowsInvalidOperation()
    {
        _budgetServiceMock.Setup(b => b.HasRateLimitRemainingAsync(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = CreateSut();
        var command = new CreateAbTestCommand(UserId, "Test", ["m1", "m2"]);

        Func<Task> act = () =>
            sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WhenModelFails_VariantMarkedAsFailed()
    {
        _llmServiceMock
            .Setup(l => l.GenerateCompletionWithModelAsync(
                "model-fail", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Provider timeout"));

        _llmServiceMock
            .Setup(l => l.GenerateCompletionWithModelAsync(
                "model-ok", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("OK"));

        var sut = CreateSut();
        var command = new CreateAbTestCommand(UserId, "Test", ["model-ok", "model-fail"]);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Variants[0].Failed.Should().BeFalse();
        result.Variants[1].Failed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_BlindMode_DoesNotExposeModelInfo()
    {
        var sut = CreateSut();
        var command = new CreateAbTestCommand(UserId, "Test", ["openai/gpt-4o", "anthropic/claude-3"]);

        var result = await sut.Handle(command, CancellationToken.None);

        // AbTestSessionDto (blind) should NOT have Provider/ModelId properties
        // This is enforced by the DTO type itself — AbTestVariantDto has no Provider/ModelId
        result.Variants.Should().AllSatisfy(v =>
        {
            v.Label.Should().NotBeNull();
            v.Response.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task Handle_CacheHit_UsesCache()
    {
        _budgetServiceMock
            .Setup(b => b.GetCachedResponseAsync("Test", "m1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Cached response");

        var sut = CreateSut();
        var command = new CreateAbTestCommand(UserId, "Test", ["m1", "m2"]);

        var result = await sut.Handle(command, CancellationToken.None);

        result.Variants[0].Response.Should().Be("Cached response");

        // m1 should NOT call LLM (used cache), m2 should call LLM
        _llmServiceMock.Verify(l => l.GenerateCompletionWithModelAsync(
            "m1", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Never);

        _llmServiceMock.Verify(l => l.GenerateCompletionWithModelAsync(
            "m2", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
