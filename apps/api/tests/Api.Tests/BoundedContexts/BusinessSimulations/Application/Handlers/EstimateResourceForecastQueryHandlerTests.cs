using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Unit tests for EstimateResourceForecastQueryHandler (Issue #3726)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class EstimateResourceForecastQueryHandlerTests
{
    private readonly Mock<ILogger<EstimateResourceForecastQueryHandler>> _loggerMock;
    private readonly EstimateResourceForecastQueryHandler _handler;

    public EstimateResourceForecastQueryHandlerTests()
    {
        _loggerMock = new Mock<ILogger<EstimateResourceForecastQueryHandler>>();
        _handler = new EstimateResourceForecastQueryHandler(_loggerMock.Object);
    }

    // ========== Projection Tests ==========

    [Fact]
    public async Task Handle_WithLinearGrowth_ShouldReturn12MonthProjections()
    {
        var query = CreateQuery(growthPattern: "Linear", monthlyGrowthRate: 10m, currentUsers: 1000);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Projections.Should().HaveCount(12);
        result.GrowthPattern.Should().Be("Linear");
        result.MonthlyGrowthRate.Should().Be(10m);
        result.CurrentUsers.Should().Be(1000);
        result.ProjectedUsersMonth12.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task Handle_WithExponentialGrowth_ShouldGrowFasterThanLinear()
    {
        var linearQuery = CreateQuery(growthPattern: "Linear", monthlyGrowthRate: 10m, currentUsers: 1000);
        var exponentialQuery = CreateQuery(growthPattern: "Exponential", monthlyGrowthRate: 10m, currentUsers: 1000);

        var linearResult = await _handler.Handle(linearQuery, CancellationToken.None);
        var exponentialResult = await _handler.Handle(exponentialQuery, CancellationToken.None);

        exponentialResult.ProjectedUsersMonth12.Should()
            .BeGreaterThan(linearResult.ProjectedUsersMonth12);
    }

    [Fact]
    public async Task Handle_WithLogarithmicGrowth_ShouldGrowSlowerThanLinear()
    {
        var linearQuery = CreateQuery(growthPattern: "Linear", monthlyGrowthRate: 10m, currentUsers: 1000);
        var logQuery = CreateQuery(growthPattern: "Logarithmic", monthlyGrowthRate: 10m, currentUsers: 1000);

        var linearResult = await _handler.Handle(linearQuery, CancellationToken.None);
        var logResult = await _handler.Handle(logQuery, CancellationToken.None);

        logResult.ProjectedUsersMonth12.Should()
            .BeLessThan(linearResult.ProjectedUsersMonth12);
    }

    [Fact]
    public async Task Handle_WithSCurveGrowth_ShouldReturnValidProjections()
    {
        var query = CreateQuery(growthPattern: "SCurve", monthlyGrowthRate: 10m, currentUsers: 1000);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Projections.Should().HaveCount(12);
        result.ProjectedUsersMonth12.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task Handle_WithZeroGrowthRate_ShouldKeepUsersConstant()
    {
        var query = CreateQuery(growthPattern: "Linear", monthlyGrowthRate: 0m, currentUsers: 1000);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.ProjectedUsersMonth12.Should().Be(1000);
    }

    [Fact]
    public async Task Handle_WithZeroUsers_ShouldReturnZeroProjections()
    {
        var query = CreateQuery(growthPattern: "Linear", monthlyGrowthRate: 10m, currentUsers: 0);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.ProjectedUsersMonth12.Should().Be(0);
    }

    // ========== Cost Calculation Tests ==========

    [Fact]
    public async Task Handle_ShouldCalculateMonthlyCosts()
    {
        var query = CreateQuery(
            growthPattern: "Linear",
            monthlyGrowthRate: 5m,
            currentUsers: 1000,
            currentDbSizeGb: 10m,
            currentDailyTokens: 1_000_000,
            currentCacheMb: 512m,
            currentVectorEntries: 500_000);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.ProjectedMonthlyCostMonth12.Should().BeGreaterThan(0);
        result.Projections.Should().AllSatisfy(p =>
            p.EstimatedMonthlyCostUsd.Should().BeGreaterThanOrEqualTo(0));
    }

    [Fact]
    public async Task Handle_CostsShouldIncreaseOverTime()
    {
        var query = CreateQuery(
            growthPattern: "Linear",
            monthlyGrowthRate: 10m,
            currentUsers: 1000,
            currentDbSizeGb: 5m,
            currentDailyTokens: 500_000,
            currentCacheMb: 256m,
            currentVectorEntries: 100_000);

        var result = await _handler.Handle(query, CancellationToken.None);

        var firstMonthCost = result.Projections[0].EstimatedMonthlyCostUsd;
        var lastMonthCost = result.Projections[^1].EstimatedMonthlyCostUsd;

        lastMonthCost.Should().BeGreaterThan(firstMonthCost);
    }

    // ========== Recommendation Tests ==========

    [Fact]
    public async Task Handle_WithHighGrowth_ShouldGenerateRecommendations()
    {
        var query = CreateQuery(
            growthPattern: "Linear",
            monthlyGrowthRate: 50m,
            currentUsers: 10_000,
            currentDbSizeGb: 20m,
            dbPerUserMb: 5m,
            currentDailyTokens: 5_000_000,
            tokensPerUserPerDay: 1000,
            currentCacheMb: 1024m,
            cachePerUserMb: 0.5m,
            currentVectorEntries: 2_000_000,
            vectorsPerUser: 500);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Recommendations.Should().NotBeEmpty();
        result.Recommendations.Should().AllSatisfy(r =>
        {
            r.ResourceType.Should().NotBeNullOrEmpty();
            r.Severity.Should().BeOneOf("warning", "critical");
            r.Message.Should().NotBeNullOrEmpty();
            r.Action.Should().NotBeNullOrEmpty();
            r.TriggerMonth.Should().BeInRange(1, 12);
        });
    }

    [Fact]
    public async Task Handle_WithLowGrowth_ShouldGenerateNoRecommendations()
    {
        var query = CreateQuery(
            growthPattern: "Linear",
            monthlyGrowthRate: 1m,
            currentUsers: 100,
            currentDbSizeGb: 1m,
            dbPerUserMb: 0.1m,
            currentDailyTokens: 10_000,
            tokensPerUserPerDay: 100,
            currentCacheMb: 10m,
            cachePerUserMb: 0.01m,
            currentVectorEntries: 1000,
            vectorsPerUser: 10);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Recommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RecommendationsShouldBeDeduplicated()
    {
        var query = CreateQuery(
            growthPattern: "Linear",
            monthlyGrowthRate: 50m,
            currentUsers: 10_000,
            currentDbSizeGb: 40m,
            dbPerUserMb: 10m);

        var result = await _handler.Handle(query, CancellationToken.None);

        // Each resource+severity combination should appear at most once
        var resourceSeverityPairs = result.Recommendations
            .Select(r => $"{r.ResourceType}:{r.Severity}")
            .ToList();

        resourceSeverityPairs.Should().OnlyHaveUniqueItems();
    }

    // ========== Projection Data Tests ==========

    [Fact]
    public async Task Handle_ProjectionMonthsShouldBeSequential()
    {
        var query = CreateQuery(growthPattern: "Linear", monthlyGrowthRate: 5m, currentUsers: 100);

        var result = await _handler.Handle(query, CancellationToken.None);

        var months = result.Projections.Select(p => p.Month).ToList();
        months.Should().BeEquivalentTo(Enumerable.Range(1, 12));
    }

    [Fact]
    public async Task Handle_ProjectedUsersShouldNeverDecrease()
    {
        var query = CreateQuery(growthPattern: "Linear", monthlyGrowthRate: 5m, currentUsers: 1000);

        var result = await _handler.Handle(query, CancellationToken.None);

        for (var i = 1; i < result.Projections.Count; i++)
        {
            result.Projections[i].ProjectedUsers.Should()
                .BeGreaterThanOrEqualTo(result.Projections[i - 1].ProjectedUsers);
        }
    }

    // ========== Null/Edge Case Tests ==========

    [Fact]
    public async Task Handle_WithNullQuery_ShouldThrowArgumentNullException()
    {
        var act = async () => await _handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var act = () => new EstimateResourceForecastQueryHandler(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ========== Helper ==========

    private static EstimateResourceForecastQuery CreateQuery(
        string growthPattern = "Linear",
        decimal monthlyGrowthRate = 10m,
        int currentUsers = 1000,
        decimal currentDbSizeGb = 5m,
        long currentDailyTokens = 500_000,
        decimal currentCacheMb = 256m,
        long currentVectorEntries = 100_000,
        decimal dbPerUserMb = 2m,
        int tokensPerUserPerDay = 500,
        decimal cachePerUserMb = 0.5m,
        int vectorsPerUser = 100)
    {
        return new EstimateResourceForecastQuery(
            GrowthPattern: growthPattern,
            MonthlyGrowthRate: monthlyGrowthRate,
            CurrentUsers: currentUsers,
            CurrentDbSizeGb: currentDbSizeGb,
            CurrentDailyTokens: currentDailyTokens,
            CurrentCacheMb: currentCacheMb,
            CurrentVectorEntries: currentVectorEntries,
            DbPerUserMb: dbPerUserMb,
            TokensPerUserPerDay: tokensPerUserPerDay,
            CachePerUserMb: cachePerUserMb,
            VectorsPerUser: vectorsPerUser);
    }
}
