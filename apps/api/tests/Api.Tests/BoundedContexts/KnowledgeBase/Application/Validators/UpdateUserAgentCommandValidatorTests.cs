using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Unit tests for UpdateUserAgentCommandValidator.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateUserAgentCommandValidatorTests
{
    private readonly UpdateUserAgentCommandValidator _validator = new();

    private static UpdateUserAgentCommand CreateCommand(
        string userTier = "free",
        string userRole = "User",
        string? name = null,
        string? strategyName = null,
        IDictionary<string, object>? strategyParameters = null)
    {
        return new UpdateUserAgentCommand(
            AgentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserTier: userTier,
            UserRole: userRole,
            Name: name,
            StrategyName: strategyName,
            StrategyParameters: strategyParameters
        );
    }

    [Fact]
    public void ValidCommand_NameOnly_Passes()
    {
        var command = CreateCommand(name: "New Name");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyAgentId_Fails()
    {
        var command = new UpdateUserAgentCommand(
            AgentId: Guid.Empty, UserId: Guid.NewGuid(),
            UserTier: "free", UserRole: "User",
            Name: "X", StrategyName: null, StrategyParameters: null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AgentId);
    }

    [Fact]
    public void EmptyUserId_Fails()
    {
        var command = new UpdateUserAgentCommand(
            AgentId: Guid.NewGuid(), UserId: Guid.Empty,
            UserTier: "free", UserRole: "User",
            Name: "X", StrategyName: null, StrategyParameters: null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
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
    public void EmptyOrWhitespaceName_Fails(string name)
    {
        var command = CreateCommand(name: name);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
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
    public void NormalTier_WithStrategy_Passes()
    {
        var command = CreateCommand(userTier: "normal", strategyName: "HybridSearch");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
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
    public void NormalTier_InvalidStrategy_Fails()
    {
        var command = CreateCommand(userTier: "normal", strategyName: "InvalidStrategy");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.StrategyName);
    }

    [Fact]
    public void PremiumTier_FullConfig_Passes()
    {
        var parameters = new Dictionary<string, object> { ["TopK"] = 10, ["MinScore"] = 0.8 };
        var command = CreateCommand(
            userTier: "premium",
            strategyName: "IterativeRAG",
            strategyParameters: parameters);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AdminRole_BypassesTierRestrictions()
    {
        var parameters = new Dictionary<string, object> { ["TopK"] = 10 };
        var command = CreateCommand(
            userTier: "free",
            userRole: "Admin",
            strategyName: "HybridSearch",
            strategyParameters: parameters);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EditorRole_BypassesTierRestrictions()
    {
        var parameters = new Dictionary<string, object> { ["TopK"] = 10 };
        var command = CreateCommand(
            userTier: "free",
            userRole: "Editor",
            strategyName: "HybridSearch",
            strategyParameters: parameters);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
