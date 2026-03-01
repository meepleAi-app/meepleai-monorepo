using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Unit tests for CreateUserAgentCommandValidator.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateUserAgentCommandValidatorTests
{
    private readonly CreateUserAgentCommandValidator _validator = new();

    private static CreateUserAgentCommand CreateCommand(
        string userTier = "free",
        string userRole = "User",
        string agentType = "RAG",
        string? name = null,
        string? strategyName = null,
        IDictionary<string, object>? strategyParameters = null)
    {
        return new CreateUserAgentCommand(
            UserId: Guid.NewGuid(),
            UserTier: userTier,
            UserRole: userRole,
            GameId: Guid.NewGuid(),
            AgentType: agentType,
            Name: name,
            StrategyName: strategyName,
            StrategyParameters: strategyParameters
        );
    }

    [Fact]
    public void ValidFreeTierCommand_Passes()
    {
        var command = CreateCommand(userTier: "free", agentType: "RAG");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidNormalTierCommand_WithStrategy_Passes()
    {
        var command = CreateCommand(userTier: "normal", agentType: "RAG", strategyName: "HybridSearch");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidPremiumTierCommand_WithFullConfig_Passes()
    {
        var parameters = new Dictionary<string, object> { ["TopK"] = 10, ["MinScore"] = 0.7 };
        var command = CreateCommand(
            userTier: "premium",
            agentType: "RAG",
            strategyName: "HybridSearch",
            strategyParameters: parameters);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AdminUser_BypassesTierRestrictions()
    {
        var parameters = new Dictionary<string, object> { ["TopK"] = 10 };
        var command = CreateCommand(
            userTier: "free",
            userRole: "Admin",
            agentType: "RAG",
            strategyName: "HybridSearch",
            strategyParameters: parameters);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyUserId_Fails()
    {
        var command = new CreateUserAgentCommand(
            UserId: Guid.Empty,
            UserTier: "free",
            UserRole: "User",
            GameId: Guid.NewGuid(),
            AgentType: "RAG",
            Name: null,
            StrategyName: null,
            StrategyParameters: null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void EmptyGameId_Fails()
    {
        var command = new CreateUserAgentCommand(
            UserId: Guid.NewGuid(),
            UserTier: "free",
            UserRole: "User",
            GameId: Guid.Empty,
            AgentType: "RAG",
            Name: null,
            StrategyName: null,
            StrategyParameters: null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void InvalidAgentType_Fails()
    {
        var command = CreateCommand(agentType: "InvalidType");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AgentType);
    }

    [Fact]
    public void FreeTier_WithStrategy_Fails()
    {
        var command = CreateCommand(userTier: "free", strategyName: "HybridSearch");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.StrategyName);
    }

    [Fact]
    public void FreeTier_WithParameters_Fails()
    {
        var parameters = new Dictionary<string, object> { ["TopK"] = 10 };
        var command = CreateCommand(userTier: "free", strategyParameters: parameters);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.StrategyParameters);
    }

    [Fact]
    public void NormalTier_WithParameters_Fails()
    {
        var parameters = new Dictionary<string, object> { ["TopK"] = 10 };
        var command = CreateCommand(userTier: "normal", strategyParameters: parameters);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.StrategyParameters);
    }

    [Fact]
    public void NormalTier_WithInvalidStrategy_Fails()
    {
        var command = CreateCommand(userTier: "normal", strategyName: "NonExistentStrategy");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.StrategyName);
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        var command = CreateCommand(name: new string('A', 101));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void EmptyOrWhitespaceName_Fails(string name)
    {
        var command = CreateCommand(name: name);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("RAG")]
    [InlineData("Citation")]
    [InlineData("Confidence")]
    [InlineData("RulesInterpreter")]
    [InlineData("Conversation")]
    public void AllValidAgentTypes_Pass(string type)
    {
        var command = CreateCommand(agentType: type);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.AgentType);
    }

    [Theory]
    [InlineData("premium")]
    [InlineData("pro")]
    [InlineData("enterprise")]
    public void PremiumTiers_AllowFullConfig(string tier)
    {
        var parameters = new Dictionary<string, object> { ["TopK"] = 10, ["MinScore"] = 0.8 };
        var command = CreateCommand(
            userTier: tier,
            agentType: "RAG",
            strategyName: "IterativeRAG",
            strategyParameters: parameters);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
