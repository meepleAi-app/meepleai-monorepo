using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Domain.Entities;

/// <summary>
/// Unit tests for ResourceForecast domain entity (Issue #3726)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class ResourceForecastTests
{
    private const string ValidName = "Q1 Growth Forecast";
    private const string ValidGrowthPattern = "Linear";
    private const decimal ValidMonthlyGrowthRate = 10m;
    private const int ValidCurrentUsers = 1000;
    private const decimal ValidCurrentDbSizeGb = 5m;
    private const long ValidCurrentDailyTokens = 500_000;
    private const decimal ValidCurrentCacheMb = 256m;
    private const long ValidCurrentVectorEntries = 100_000;
    private const decimal ValidDbPerUserMb = 2m;
    private const int ValidTokensPerUserPerDay = 500;
    private const decimal ValidCachePerUserMb = 0.5m;
    private const int ValidVectorsPerUser = 100;
    private const string ValidProjectionsJson = "[{\"month\":1}]";
    private const string ValidRecommendationsJson = "[{\"type\":\"warning\"}]";
    private const decimal ValidProjectedMonthlyCost = 150.50m;
    private static readonly Guid ValidUserId = Guid.NewGuid();

    // ========== Happy Path Tests ==========

    [Fact]
    public void Create_WithValidParameters_ShouldReturnForecast()
    {
        var forecast = CreateValidForecast();

        forecast.Should().NotBeNull();
        forecast.Id.Should().NotBe(Guid.Empty);
        forecast.Name.Should().Be(ValidName);
        forecast.GrowthPattern.Should().Be(ValidGrowthPattern);
        forecast.MonthlyGrowthRate.Should().Be(ValidMonthlyGrowthRate);
        forecast.CurrentUsers.Should().Be(ValidCurrentUsers);
        forecast.CurrentDbSizeGb.Should().Be(ValidCurrentDbSizeGb);
        forecast.CurrentDailyTokens.Should().Be(ValidCurrentDailyTokens);
        forecast.CurrentCacheMb.Should().Be(ValidCurrentCacheMb);
        forecast.CurrentVectorEntries.Should().Be(ValidCurrentVectorEntries);
        forecast.DbPerUserMb.Should().Be(ValidDbPerUserMb);
        forecast.TokensPerUserPerDay.Should().Be(ValidTokensPerUserPerDay);
        forecast.CachePerUserMb.Should().Be(ValidCachePerUserMb);
        forecast.VectorsPerUser.Should().Be(ValidVectorsPerUser);
        forecast.ProjectionsJson.Should().Be(ValidProjectionsJson);
        forecast.RecommendationsJson.Should().Be(ValidRecommendationsJson);
        forecast.ProjectedMonthlyCost.Should().Be(ValidProjectedMonthlyCost);
        forecast.CreatedByUserId.Should().Be(ValidUserId);
        forecast.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNullRecommendations_ShouldSucceed()
    {
        var forecast = ResourceForecast.Create(
            name: ValidName, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        forecast.RecommendationsJson.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var forecast = ResourceForecast.Create(
            name: "  Forecast Name  ", growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        forecast.Name.Should().Be("Forecast Name");
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var forecast1 = CreateValidForecast();
        var forecast2 = CreateValidForecast();

        forecast1.Id.Should().NotBe(forecast2.Id);
    }

    // ========== Validation Tests ==========

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ShouldThrowArgumentException(string? name)
    {
        var act = () => ResourceForecast.Create(
            name: name!, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithNameExceeding200Chars_ShouldThrow()
    {
        var act = () => ResourceForecast.Create(
            name: new string('A', 201), growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidGrowthPattern_ShouldThrow(string? pattern)
    {
        var act = () => ResourceForecast.Create(
            name: ValidName, growthPattern: pattern!,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("growthPattern");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_WithInvalidMonthlyGrowthRate_ShouldThrow(decimal rate)
    {
        var act = () => ResourceForecast.Create(
            name: ValidName, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: rate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("monthlyGrowthRate");
    }

    [Fact]
    public void Create_WithNegativeCurrentUsers_ShouldThrow()
    {
        var act = () => ResourceForecast.Create(
            name: ValidName, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: -1,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("currentUsers");
    }

    [Fact]
    public void Create_WithNegativeCurrentDbSizeGb_ShouldThrow()
    {
        var act = () => ResourceForecast.Create(
            name: ValidName, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: -1m, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("currentDbSizeGb");
    }

    [Fact]
    public void Create_WithEmptyProjectionsJson_ShouldThrow()
    {
        var act = () => ResourceForecast.Create(
            name: ValidName, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: "", recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("projectionsJson");
    }

    [Fact]
    public void Create_WithEmptyCreatedByUserId_ShouldThrow()
    {
        var act = () => ResourceForecast.Create(
            name: ValidName, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("createdByUserId");
    }

    // ========== Boundary Tests ==========

    [Fact]
    public void Create_WithZeroValues_ShouldSucceed()
    {
        var forecast = ResourceForecast.Create(
            name: ValidName, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: 0m, currentUsers: 0,
            currentDbSizeGb: 0m, currentDailyTokens: 0,
            currentCacheMb: 0m, currentVectorEntries: 0,
            dbPerUserMb: 0m, tokensPerUserPerDay: 0,
            cachePerUserMb: 0m, vectorsPerUser: 0,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: 0m, createdByUserId: ValidUserId);

        forecast.CurrentUsers.Should().Be(0);
        forecast.MonthlyGrowthRate.Should().Be(0m);
    }

    [Fact]
    public void Create_WithNameExactly200Chars_ShouldSucceed()
    {
        var exactName = new string('A', 200);

        var forecast = ResourceForecast.Create(
            name: exactName, growthPattern: ValidGrowthPattern,
            monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
            currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
            currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
            dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
            cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
            projectionsJson: ValidProjectionsJson, recommendationsJson: null,
            projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);

        forecast.Name.Should().HaveLength(200);
    }

    // ========== Helper ==========

    private static ResourceForecast CreateValidForecast() => ResourceForecast.Create(
        name: ValidName, growthPattern: ValidGrowthPattern,
        monthlyGrowthRate: ValidMonthlyGrowthRate, currentUsers: ValidCurrentUsers,
        currentDbSizeGb: ValidCurrentDbSizeGb, currentDailyTokens: ValidCurrentDailyTokens,
        currentCacheMb: ValidCurrentCacheMb, currentVectorEntries: ValidCurrentVectorEntries,
        dbPerUserMb: ValidDbPerUserMb, tokensPerUserPerDay: ValidTokensPerUserPerDay,
        cachePerUserMb: ValidCachePerUserMb, vectorsPerUser: ValidVectorsPerUser,
        projectionsJson: ValidProjectionsJson, recommendationsJson: ValidRecommendationsJson,
        projectedMonthlyCost: ValidProjectedMonthlyCost, createdByUserId: ValidUserId);
}
