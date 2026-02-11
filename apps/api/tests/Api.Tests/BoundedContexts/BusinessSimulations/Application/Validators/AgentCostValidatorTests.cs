using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Validators;

/// <summary>
/// Unit tests for agent cost validators (Issue #3727)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class AgentCostValidatorTests
{
    private readonly EstimateAgentCostQueryValidator _estimateValidator = new();
    private readonly SaveCostScenarioCommandValidator _saveValidator = new();
    private readonly DeleteCostScenarioCommandValidator _deleteValidator = new();
    private readonly GetCostScenariosQueryValidator _getValidator = new();

    // ========== EstimateAgentCostQuery Validator ==========

    [Fact]
    public void Estimate_ValidQuery_ShouldPassValidation()
    {
        var query = new EstimateAgentCostQuery(
            Strategy: "Balanced", ModelId: "deepseek/deepseek-chat",
            MessagesPerDay: 1000, ActiveUsers: 50, AvgTokensPerRequest: 2000);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Fast")]
    [InlineData("Balanced")]
    [InlineData("Precise")]
    [InlineData("Expert")]
    [InlineData("Consensus")]
    [InlineData("MultiAgent")]
    [InlineData("SentenceWindow")]
    [InlineData("Iterative")]
    [InlineData("Custom")]
    [InlineData("StepBack")]
    [InlineData("QueryExpansion")]
    [InlineData("RagFusion")]
    public void Estimate_WithValidStrategy_ShouldPass(string strategy)
    {
        var query = CreateEstimateQuery(strategy: strategy);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Strategy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("InvalidStrategy")]
    [InlineData("fast")] // case sensitive check via Must()
    public void Estimate_WithInvalidStrategy_ShouldFail(string strategy)
    {
        var query = CreateEstimateQuery(strategy: strategy);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Strategy);
    }

    [Fact]
    public void Estimate_WithEmptyModelId_ShouldFail()
    {
        var query = CreateEstimateQuery(modelId: "");

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.ModelId);
    }

    [Fact]
    public void Estimate_WithModelIdExceeding100Chars_ShouldFail()
    {
        var query = CreateEstimateQuery(modelId: new string('A', 101));

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.ModelId);
    }

    [Fact]
    public void Estimate_WithNegativeMessagesPerDay_ShouldFail()
    {
        var query = CreateEstimateQuery(messagesPerDay: -1);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.MessagesPerDay);
    }

    [Fact]
    public void Estimate_WithMessagesExceedingMax_ShouldFail()
    {
        var query = CreateEstimateQuery(messagesPerDay: 1_000_001);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.MessagesPerDay);
    }

    [Fact]
    public void Estimate_WithNegativeActiveUsers_ShouldFail()
    {
        var query = CreateEstimateQuery(activeUsers: -1);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.ActiveUsers);
    }

    [Fact]
    public void Estimate_WithActiveUsersExceedingMax_ShouldFail()
    {
        var query = CreateEstimateQuery(activeUsers: 10_000_001);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.ActiveUsers);
    }

    [Fact]
    public void Estimate_WithZeroAvgTokens_ShouldFail()
    {
        var query = CreateEstimateQuery(avgTokensPerRequest: 0);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.AvgTokensPerRequest);
    }

    [Fact]
    public void Estimate_WithAvgTokensExceedingMax_ShouldFail()
    {
        var query = CreateEstimateQuery(avgTokensPerRequest: 100_001);

        var result = _estimateValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.AvgTokensPerRequest);
    }

    // ========== SaveCostScenarioCommand Validator ==========

    [Fact]
    public void Save_ValidCommand_ShouldPassValidation()
    {
        var command = CreateSaveCommand();

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
    public void Save_WithEmptyStrategy_ShouldFail()
    {
        var command = CreateSaveCommand(strategy: "");

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Strategy);
    }

    [Fact]
    public void Save_WithEmptyModelId_ShouldFail()
    {
        var command = CreateSaveCommand(modelId: "");

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ModelId);
    }

    [Fact]
    public void Save_WithModelIdExceeding100Chars_ShouldFail()
    {
        var command = CreateSaveCommand(modelId: new string('X', 101));

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ModelId);
    }

    [Fact]
    public void Save_WithNegativeMessagesPerDay_ShouldFail()
    {
        var command = CreateSaveCommand(messagesPerDay: -1);

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MessagesPerDay);
    }

    [Fact]
    public void Save_WithNegativeActiveUsers_ShouldFail()
    {
        var command = CreateSaveCommand(activeUsers: -1);

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ActiveUsers);
    }

    [Fact]
    public void Save_WithZeroAvgTokens_ShouldFail()
    {
        var command = CreateSaveCommand(avgTokensPerRequest: 0);

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AvgTokensPerRequest);
    }

    [Fact]
    public void Save_WithNegativeCostPerRequest_ShouldFail()
    {
        var command = CreateSaveCommand(costPerRequest: -0.01m);

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CostPerRequest);
    }

    [Fact]
    public void Save_WithEmptyCreatedByUserId_ShouldFail()
    {
        var command = CreateSaveCommand(createdByUserId: Guid.Empty);

        var result = _saveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CreatedByUserId);
    }

    // ========== DeleteCostScenarioCommand Validator ==========

    [Fact]
    public void Delete_WithValidId_ShouldPass()
    {
        var command = new DeleteCostScenarioCommand(Guid.NewGuid());

        var result = _deleteValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Delete_WithEmptyId_ShouldFail()
    {
        var command = new DeleteCostScenarioCommand(Guid.Empty);

        var result = _deleteValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    // ========== GetCostScenariosQuery Validator ==========

    [Fact]
    public void Get_WithValidParams_ShouldPass()
    {
        var query = new GetCostScenariosQuery(Guid.NewGuid(), Page: 1, PageSize: 10);

        var result = _getValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Get_WithEmptyUserId_ShouldFail()
    {
        var query = new GetCostScenariosQuery(Guid.Empty, Page: 1, PageSize: 10);

        var result = _getValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Get_WithPageLessThan1_ShouldFail()
    {
        var query = new GetCostScenariosQuery(Guid.NewGuid(), Page: 0, PageSize: 10);

        var result = _getValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Get_WithPageSizeExceeding100_ShouldFail()
    {
        var query = new GetCostScenariosQuery(Guid.NewGuid(), Page: 1, PageSize: 101);

        var result = _getValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Get_WithPageSizeLessThan1_ShouldFail()
    {
        var query = new GetCostScenariosQuery(Guid.NewGuid(), Page: 1, PageSize: 0);

        var result = _getValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    // ========== Helpers ==========

    private static EstimateAgentCostQuery CreateEstimateQuery(
        string strategy = "Balanced", string modelId = "deepseek/deepseek-chat",
        int messagesPerDay = 1000, int activeUsers = 50,
        int avgTokensPerRequest = 2000) =>
        new(strategy, modelId, messagesPerDay, activeUsers, avgTokensPerRequest);

    private static SaveCostScenarioCommand CreateSaveCommand(
        string name = "Test Scenario", string strategy = "Balanced",
        string modelId = "deepseek/deepseek-chat", int messagesPerDay = 1000,
        int activeUsers = 50, int avgTokensPerRequest = 2000,
        decimal costPerRequest = 0.0054m, Guid? createdByUserId = null) =>
        new(Name: name, Strategy: strategy, ModelId: modelId,
            MessagesPerDay: messagesPerDay, ActiveUsers: activeUsers,
            AvgTokensPerRequest: avgTokensPerRequest,
            CostPerRequest: costPerRequest, DailyProjection: 5.40m,
            MonthlyProjection: 162.00m, Warnings: null,
            CreatedByUserId: createdByUserId ?? Guid.NewGuid());
}
