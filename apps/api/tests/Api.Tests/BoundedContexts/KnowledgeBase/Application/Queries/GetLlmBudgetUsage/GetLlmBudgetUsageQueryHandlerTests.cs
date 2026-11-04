using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetLlmBudgetUsage;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetLlmBudgetUsage;

/// <summary>#4169: il consumo della pagina «Budget e quote» viene dal gate del gateway.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4169")]
public sealed class GetLlmBudgetUsageQueryHandlerTests
{
    private readonly Mock<ILlmBudgetStatus> _status = new();

    private Task<LlmBudgetUsageDto> HandleAsync() =>
        new GetLlmBudgetUsageQueryHandler(_status.Object).Handle(new GetLlmBudgetUsageQuery(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task TheUsageOfTheGate_IsReturnedAsItIs()
    {
        var day = new DateOnly(2026, 10, 11);
        var today = new LlmBudgetSpend(0.45m, 0.30m, 0.10m, 0.05m);
        var month = new LlmBudgetSpend(2.45m, 2.30m, 0.10m, 0.05m);
        _status.Setup(s => s.GetUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmBudgetUsage(day, today, month, IsDegraded: true));

        var dto = await HandleAsync();

        dto.Should().Be(new LlmBudgetUsageDto(Available: true, day, today, month, IsDegraded: true));
    }

    [Fact]
    public async Task WhenTheSpendCannotBeRead_TheUsageIsUnavailable()
    {
        _status.Setup(s => s.GetUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync((LlmBudgetUsage?)null);

        var dto = await HandleAsync();

        dto.Available.Should().BeFalse("il gate in quel caso rifiuta le chiamate a pagamento, e la pagina lo deve dire");
        dto.Today.Should().BeNull();
    }
}
