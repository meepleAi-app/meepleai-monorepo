using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Domain.Entities;

/// <summary>
/// Unit tests for CostScenario domain entity (Issue #3727)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class CostScenarioTests
{
    private const string ValidName = "Production Estimate";
    private const string ValidStrategy = "Balanced";
    private const string ValidModelId = "deepseek/deepseek-chat";
    private const int ValidMessagesPerDay = 1000;
    private const int ValidActiveUsers = 50;
    private const int ValidAvgTokensPerRequest = 2000;
    private const decimal ValidCostPerRequest = 0.0054m;
    private const decimal ValidDailyProjection = 5.40m;
    private const decimal ValidMonthlyProjection = 162.00m;
    private const string ValidWarnings = "[\"High token usage\"]";
    private static readonly Guid ValidUserId = Guid.NewGuid();

    // ========== Create Factory Method ==========

    [Fact]
    public void Create_WithValidParameters_ShouldCreateScenario()
    {
        var scenario = CreateValidScenario();

        scenario.Should().NotBeNull();
        scenario.Id.Should().NotBe(Guid.Empty);
        scenario.Name.Should().Be(ValidName);
        scenario.Strategy.Should().Be(ValidStrategy);
        scenario.ModelId.Should().Be(ValidModelId);
        scenario.MessagesPerDay.Should().Be(ValidMessagesPerDay);
        scenario.ActiveUsers.Should().Be(ValidActiveUsers);
        scenario.AvgTokensPerRequest.Should().Be(ValidAvgTokensPerRequest);
        scenario.CostPerRequest.Should().Be(ValidCostPerRequest);
        scenario.DailyProjection.Should().Be(ValidDailyProjection);
        scenario.MonthlyProjection.Should().Be(ValidMonthlyProjection);
        scenario.Warnings.Should().Be(ValidWarnings);
        scenario.CreatedByUserId.Should().Be(ValidUserId);
        scenario.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var scenario1 = CreateValidScenario();
        var scenario2 = CreateValidScenario();

        scenario1.Id.Should().NotBe(scenario2.Id);
    }

    [Fact]
    public void Create_WithNullWarnings_ShouldSucceed()
    {
        var scenario = CostScenario.Create(
            ValidName, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            warnings: null, ValidUserId);

        scenario.Warnings.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var scenario = CostScenario.Create(
            "  My Scenario  ", ValidStrategy, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        scenario.Name.Should().Be("My Scenario");
    }

    [Fact]
    public void Create_WithZeroMessagesPerDay_ShouldSucceed()
    {
        var scenario = CostScenario.Create(
            ValidName, ValidStrategy, ValidModelId,
            messagesPerDay: 0, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        scenario.MessagesPerDay.Should().Be(0);
    }

    [Fact]
    public void Create_WithZeroActiveUsers_ShouldSucceed()
    {
        var scenario = CostScenario.Create(
            ValidName, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, activeUsers: 0, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        scenario.ActiveUsers.Should().Be(0);
    }

    [Fact]
    public void Create_WithZeroCosts_ShouldSucceed()
    {
        var scenario = CostScenario.Create(
            ValidName, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            costPerRequest: 0m, dailyProjection: 0m, monthlyProjection: 0m,
            ValidWarnings, ValidUserId);

        scenario.CostPerRequest.Should().Be(0m);
        scenario.DailyProjection.Should().Be(0m);
        scenario.MonthlyProjection.Should().Be(0m);
    }

    [Fact]
    public void Create_WithMaxLengthName_ShouldSucceed()
    {
        var name = new string('A', 200);
        var scenario = CostScenario.Create(
            name, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        scenario.Name.Should().HaveLength(200);
    }

    // ========== Validation Tests ==========

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ShouldThrow(string? name)
    {
        var act = () => CostScenario.Create(
            name!, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithNameExceeding200Chars_ShouldThrow()
    {
        var name = new string('A', 201);
        var act = () => CostScenario.Create(
            name, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidStrategy_ShouldThrow(string? strategy)
    {
        var act = () => CostScenario.Create(
            ValidName, strategy!, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("strategy");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidModelId_ShouldThrow(string? modelId)
    {
        var act = () => CostScenario.Create(
            ValidName, ValidStrategy, modelId!,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("modelId");
    }

    [Fact]
    public void Create_WithNegativeMessagesPerDay_ShouldThrow()
    {
        var act = () => CostScenario.Create(
            ValidName, ValidStrategy, ValidModelId,
            messagesPerDay: -1, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("messagesPerDay");
    }

    [Fact]
    public void Create_WithNegativeActiveUsers_ShouldThrow()
    {
        var act = () => CostScenario.Create(
            ValidName, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, activeUsers: -1, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("activeUsers");
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        var act = () => CostScenario.Create(
            ValidName, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, createdByUserId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("createdByUserId");
    }

    // ========== Helper ==========

    private static CostScenario CreateValidScenario() =>
        CostScenario.Create(
            ValidName, ValidStrategy, ValidModelId,
            ValidMessagesPerDay, ValidActiveUsers, ValidAvgTokensPerRequest,
            ValidCostPerRequest, ValidDailyProjection, ValidMonthlyProjection,
            ValidWarnings, ValidUserId);
}
