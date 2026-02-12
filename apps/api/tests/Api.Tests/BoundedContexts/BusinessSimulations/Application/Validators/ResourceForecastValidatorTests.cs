using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Validators;

/// <summary>
/// Unit tests for resource forecast validators (Issue #3726)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class ResourceForecastValidatorTests
{
    private readonly EstimateResourceForecastQueryValidator _estimateValidator = new();
    private readonly SaveResourceForecastCommandValidator _saveValidator = new();
    private readonly DeleteResourceForecastCommandValidator _deleteValidator = new();
    private readonly GetResourceForecastsQueryValidator _getValidator = new();

    // ========== EstimateResourceForecastQuery Validator ==========

    [Fact]
    public void Estimate_ValidQuery_ShouldPassValidation()
    {
        var query = new EstimateResourceForecastQuery(
            GrowthPattern: "Linear", MonthlyGrowthRate: 10m, CurrentUsers: 1000,
            CurrentDbSizeGb: 5m, CurrentDailyTokens: 500_000, CurrentCacheMb: 256m,
            CurrentVectorEntries: 100_000, DbPerUserMb: 2m, TokensPerUserPerDay: 500,
            CachePerUserMb: 0.5m, VectorsPerUser: 100);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Linear")]
    [InlineData("Exponential")]
    [InlineData("Logarithmic")]
    [InlineData("SCurve")]
    public void Estimate_WithValidGrowthPattern_ShouldPass(string pattern)
    {
        var query = CreateEstimateQuery(growthPattern: pattern);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.GrowthPattern);
    }

    [Theory]
    [InlineData("")]
    [InlineData("InvalidPattern")]
    // Note: "linear" is valid (validator uses OrdinalIgnoreCase, matches "Linear")
    public void Estimate_WithInvalidGrowthPattern_ShouldFail(string pattern)
    {
        var query = CreateEstimateQuery(growthPattern: pattern);

        var result = _estimateValidator.TestValidate(query);

        // Empty string fails NotEmpty; invalid pattern fails Must
        result.ShouldHaveValidationErrorFor(x => x.GrowthPattern);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Estimate_WithInvalidGrowthRate_ShouldFail(decimal rate)
    {
        var query = CreateEstimateQuery(monthlyGrowthRate: rate);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.MonthlyGrowthRate);
    }

    [Fact]
    public void Estimate_WithNegativeCurrentUsers_ShouldFail()
    {
        var query = CreateEstimateQuery(currentUsers: -1);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.CurrentUsers);
    }

    [Fact]
    public void Estimate_WithUsersExceedingMax_ShouldFail()
    {
        var query = CreateEstimateQuery(currentUsers: 100_000_001);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.CurrentUsers);
    }

    [Fact]
    public void Estimate_WithNegativeDbSize_ShouldFail()
    {
        var query = CreateEstimateQuery(currentDbSizeGb: -1m);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.CurrentDbSizeGb);
    }

    [Fact]
    public void Estimate_WithNegativeDailyTokens_ShouldFail()
    {
        var query = CreateEstimateQuery(currentDailyTokens: -1);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.CurrentDailyTokens);
    }

    // ========== SaveResourceForecastCommand Validator ==========

    [Fact]
    public void Save_ValidCommand_ShouldPassValidation()
    {
        var command = new SaveResourceForecastCommand(
            Name: "Test Forecast", GrowthPattern: "Linear",
            MonthlyGrowthRate: 10m, CurrentUsers: 1000,
            CurrentDbSizeGb: 5m, CurrentDailyTokens: 500_000,
            CurrentCacheMb: 256m, CurrentVectorEntries: 100_000,
            DbPerUserMb: 2m, TokensPerUserPerDay: 500,
            CachePerUserMb: 0.5m, VectorsPerUser: 100,
            ProjectionsJson: "[{\"month\":1}]", RecommendationsJson: null,
            ProjectedMonthlyCost: 150m, CreatedByUserId: Guid.NewGuid());

        var result = _saveValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Save_WithEmptyName_ShouldFail()
    {
        var command = CreateSaveCommand(name: "");

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Save_WithNameExceeding200Chars_ShouldFail()
    {
        var command = CreateSaveCommand(name: new string('A', 201));

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Save_WithEmptyProjectionsJson_ShouldFail()
    {
        var command = CreateSaveCommand(projectionsJson: "");

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ProjectionsJson);
    }

    [Fact]
    public void Save_WithEmptyCreatedByUserId_ShouldFail()
    {
        var command = CreateSaveCommand(createdByUserId: Guid.Empty);

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CreatedByUserId);
    }

    [Fact]
    public void Save_WithNegativeCost_ShouldFail()
    {
        var command = CreateSaveCommand(projectedMonthlyCost: -1m);

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ProjectedMonthlyCost);
    }

    // ========== DeleteResourceForecastCommand Validator ==========

    [Fact]
    public void Delete_WithValidId_ShouldPass()
    {
        var command = new DeleteResourceForecastCommand(Guid.NewGuid());

        var result = _deleteValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Delete_WithEmptyId_ShouldFail()
    {
        var command = new DeleteResourceForecastCommand(Guid.Empty);

        var result = _deleteValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    // ========== GetResourceForecastsQuery Validator ==========

    [Fact]
    public void Get_WithValidParams_ShouldPass()
    {
        var query = new GetResourceForecastsQuery(Guid.NewGuid(), Page: 1, PageSize: 10);

        var result = _getValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Get_WithEmptyUserId_ShouldFail()
    {
        var query = new GetResourceForecastsQuery(Guid.Empty, Page: 1, PageSize: 10);

        var result = _getValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Get_WithPageLessThan1_ShouldFail()
    {
        var query = new GetResourceForecastsQuery(Guid.NewGuid(), Page: 0, PageSize: 10);

        var result = _getValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Get_WithPageSizeExceeding100_ShouldFail()
    {
        var query = new GetResourceForecastsQuery(Guid.NewGuid(), Page: 1, PageSize: 101);

        var result = _getValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    // ========== Helpers ==========

    private static EstimateResourceForecastQuery CreateEstimateQuery(
        string growthPattern = "Linear", decimal monthlyGrowthRate = 10m,
        int currentUsers = 1000, decimal currentDbSizeGb = 5m,
        long currentDailyTokens = 500_000, decimal currentCacheMb = 256m,
        long currentVectorEntries = 100_000) =>
        new(growthPattern, monthlyGrowthRate, currentUsers,
            currentDbSizeGb, currentDailyTokens, currentCacheMb, currentVectorEntries,
            DbPerUserMb: 2m, TokensPerUserPerDay: 500, CachePerUserMb: 0.5m, VectorsPerUser: 100);

    private static SaveResourceForecastCommand CreateSaveCommand(
        string name = "Test Forecast", string projectionsJson = "[{\"month\":1}]",
        Guid? createdByUserId = null, decimal projectedMonthlyCost = 150m) =>
        new(Name: name, GrowthPattern: "Linear", MonthlyGrowthRate: 10m,
            CurrentUsers: 1000, CurrentDbSizeGb: 5m, CurrentDailyTokens: 500_000,
            CurrentCacheMb: 256m, CurrentVectorEntries: 100_000,
            DbPerUserMb: 2m, TokensPerUserPerDay: 500, CachePerUserMb: 0.5m, VectorsPerUser: 100,
            ProjectionsJson: projectionsJson, RecommendationsJson: null,
            ProjectedMonthlyCost: projectedMonthlyCost,
            CreatedByUserId: createdByUserId ?? Guid.NewGuid());
}
