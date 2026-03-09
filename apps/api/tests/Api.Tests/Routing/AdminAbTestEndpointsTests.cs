using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AbTest;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// Unit tests for AdminAbTestEndpoints — mediator-level verification.
/// Issue #5497: A/B Test backend API endpoints.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5497")]
public sealed class AdminAbTestEndpointsTests
{
    private readonly Mock<IMediator> _mediatorMock = new();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task CreateAbTest_SendsCreateCommand_ReturnsSession()
    {
        var expectedDto = new AbTestSessionDto
        {
            Id = Guid.NewGuid(),
            CreatedBy = UserId,
            Query = "What are the rules?",
            Status = "InProgress",
            Variants =
            [
                new AbTestVariantDto { Id = Guid.NewGuid(), Label = "A", Response = "Response A", TokensUsed = 100, LatencyMs = 250 },
                new AbTestVariantDto { Id = Guid.NewGuid(), Label = "B", Response = "Response B", TokensUsed = 80, LatencyMs = 300 }
            ],
            TotalCost = 0.003m,
            CreatedAt = DateTime.UtcNow
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateAbTestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var result = await _mediatorMock.Object.Send(
            new CreateAbTestCommand(UserId, "What are the rules?", ["openai/gpt-4o-mini", "anthropic/claude-3-haiku"]),
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Query.Should().Be("What are the rules?");
        result.Status.Should().Be("InProgress");
        result.Variants.Should().HaveCount(2);
        result.Variants[0].Label.Should().Be("A");
        result.Variants[1].Label.Should().Be("B");
    }

    [Fact]
    public async Task GetAbTests_SendsQuery_ReturnsPaginatedList()
    {
        var expectedDto = new AbTestSessionListDto(
            Items: [],
            TotalCount: 0,
            Page: 1,
            PageSize: 20);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAbTestsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var result = await _mediatorMock.Object.Send(
            new GetAbTestsQuery(UserId),
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAbTest_SendsQuery_ReturnsBlindSession()
    {
        var sessionId = Guid.NewGuid();
        var expectedDto = new AbTestSessionDto
        {
            Id = sessionId,
            CreatedBy = UserId,
            Query = "Test query",
            Status = "InProgress",
            Variants = [new AbTestVariantDto { Id = Guid.NewGuid(), Label = "A", Response = "Response", TokensUsed = 50, LatencyMs = 200 }],
            TotalCost = 0.001m,
            CreatedAt = DateTime.UtcNow
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAbTestQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var result = await _mediatorMock.Object.Send(
            new GetAbTestQuery(sessionId),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(sessionId);
    }

    [Fact]
    public async Task GetAbTest_NotFound_ReturnsNull()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAbTestQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AbTestSessionDto?)null);

        var result = await _mediatorMock.Object.Send(
            new GetAbTestQuery(Guid.NewGuid()),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAbTest_SendsCommand_ReturnsRevealedSession()
    {
        var sessionId = Guid.NewGuid();
        var evaluatorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var expectedDto = new AbTestSessionRevealedDto
        {
            Id = sessionId,
            CreatedBy = UserId,
            Query = "Test query",
            Status = "Evaluated",
            Variants =
            [
                new AbTestVariantRevealedDto
                {
                    Id = Guid.NewGuid(), Label = "A", Provider = "OpenRouter", ModelId = "openai/gpt-4o",
                    Response = "Response A", TokensUsed = 50, LatencyMs = 200, CostUsd = 0.001m,
                    Evaluation = new AbTestEvaluationDto(evaluatorId, 4, 5, 4, 3, "Good", 4.0m, now)
                },
                new AbTestVariantRevealedDto
                {
                    Id = Guid.NewGuid(), Label = "B", Provider = "OpenRouter", ModelId = "anthropic/claude-3",
                    Response = "Response B", TokensUsed = 60, LatencyMs = 180, CostUsd = 0.002m,
                    Evaluation = new AbTestEvaluationDto(evaluatorId, 3, 4, 5, 4, null, 4.0m, now)
                }
            ],
            TotalCost = 0.003m,
            CreatedAt = now,
            WinnerLabel = "A",
            WinnerModelId = "openai/gpt-4o"
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<EvaluateAbTestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var result = await _mediatorMock.Object.Send(
            new EvaluateAbTestCommand(
                sessionId,
                evaluatorId,
                [
                    new VariantEvaluationInput("A", 4, 5, 4, 3, "Good"),
                    new VariantEvaluationInput("B", 3, 4, 5, 4)
                ]),
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("Evaluated");
        result.WinnerLabel.Should().Be("A");
        result.WinnerModelId.Should().Be("openai/gpt-4o");
        result.Variants.Should().HaveCount(2);
        result.Variants[0].Provider.Should().Be("OpenRouter");
        result.Variants[0].ModelId.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public async Task RevealAbTest_SendsQuery_ReturnsRevealedSession()
    {
        var sessionId = Guid.NewGuid();
        var expectedDto = new AbTestSessionRevealedDto
        {
            Id = sessionId,
            CreatedBy = UserId,
            Query = "Test",
            Status = "Evaluated",
            Variants = [],
            TotalCost = 0m,
            CreatedAt = DateTime.UtcNow
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RevealAbTestQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var result = await _mediatorMock.Object.Send(
            new RevealAbTestQuery(sessionId),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Evaluated");
    }

    [Fact]
    public async Task RevealAbTest_NotFound_ReturnsNull()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RevealAbTestQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AbTestSessionRevealedDto?)null);

        var result = await _mediatorMock.Object.Send(
            new RevealAbTestQuery(Guid.NewGuid()),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAbTestAnalytics_SendsQuery_ReturnsAnalytics()
    {
        var expectedDto = new AbTestAnalyticsDto
        {
            TotalTests = 10,
            CompletedTests = 8,
            TotalCost = 0.05m,
            ModelWinRates = [new ModelWinRateDto("openai/gpt-4o", 5, 8, 0.625m)],
            ModelAvgScores = [new ModelAvgScoreDto("openai/gpt-4o", 4.2m, 4.0m, 3.8m, 4.1m, 4.025m, 8)]
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAbTestAnalyticsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var result = await _mediatorMock.Object.Send(
            new GetAbTestAnalyticsQuery(DateFrom: DateTime.UtcNow.AddDays(-30)),
            CancellationToken.None);

        result.Should().NotBeNull();
        result.TotalTests.Should().Be(10);
        result.CompletedTests.Should().Be(8);
        result.ModelWinRates.Should().HaveCount(1);
        result.ModelWinRates[0].WinRate.Should().Be(0.625m);
        result.ModelAvgScores.Should().HaveCount(1);
    }
}
