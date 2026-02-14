using Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;
using Api.BoundedContexts.KnowledgeBase.Application.Validators.PlaygroundTestScenario;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators.PlaygroundTestScenario;

/// <summary>
/// Unit tests for CreatePlaygroundTestScenarioCommandValidator.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreatePlaygroundTestScenarioCommandValidatorTests
{
    private readonly CreatePlaygroundTestScenarioCommandValidator _validator = new();

    private static CreatePlaygroundTestScenarioCommand CreateValidCommand() => new(
        Name: "Test Scenario",
        Description: "A valid test scenario description",
        Category: ScenarioCategory.Greeting,
        Messages: [new ScenarioMessage { Role = "user", Content = "Hello!" }],
        CreatedBy: Guid.NewGuid()
    );

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        var result = _validator.TestValidate(CreateValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmptyName_FailsValidation(string? name)
    {
        var command = CreateValidCommand() with { Name = name! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void ShortName_FailsValidation()
    {
        var command = CreateValidCommand() with { Name = "ab" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void LongName_FailsValidation()
    {
        var command = CreateValidCommand() with { Name = new string('x', 201) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void EmptyDescription_FailsValidation()
    {
        var command = CreateValidCommand() with { Description = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void LongDescription_FailsValidation()
    {
        var command = CreateValidCommand() with { Description = new string('x', 2001) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void NullMessages_FailsValidation()
    {
        var command = CreateValidCommand() with { Messages = null! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Messages);
    }

    [Fact]
    public void EmptyMessages_FailsValidation()
    {
        var command = CreateValidCommand() with { Messages = new List<ScenarioMessage>() };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Messages);
    }

    [Fact]
    public void TooManyMessages_FailsValidation()
    {
        var messages = Enumerable.Range(0, 51)
            .Select(_ => new ScenarioMessage { Role = "user", Content = "msg" })
            .ToList();
        var command = CreateValidCommand() with { Messages = messages };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Messages);
    }

    [Fact]
    public void InvalidMessageRole_FailsValidation()
    {
        var command = CreateValidCommand() with
        {
            Messages = [new ScenarioMessage { Role = "invalid", Content = "text" }]
        };
        var result = _validator.TestValidate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void EmptyMessageContent_FailsValidation()
    {
        var command = CreateValidCommand() with
        {
            Messages = [new ScenarioMessage { Role = "user", Content = "" }]
        };
        var result = _validator.TestValidate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void EmptyCreatedBy_FailsValidation()
    {
        var command = CreateValidCommand() with { CreatedBy = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CreatedBy);
    }

    [Fact]
    public void LongExpectedOutcome_FailsValidation()
    {
        var command = CreateValidCommand() with { ExpectedOutcome = new string('x', 2001) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ExpectedOutcome);
    }

    [Fact]
    public void TooManyTags_FailsValidation()
    {
        var tags = Enumerable.Range(0, 21).Select(i => $"tag{i}").ToList();
        var command = CreateValidCommand() with { Tags = tags };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Tags);
    }

    [Fact]
    public void LongTag_FailsValidation()
    {
        var command = CreateValidCommand() with { Tags = [new string('x', 51)] };
        var result = _validator.TestValidate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidCommand_WithAllOptionalFields_PassesValidation()
    {
        var command = new CreatePlaygroundTestScenarioCommand(
            Name: "Full Scenario",
            Description: "Full description",
            Category: ScenarioCategory.RagValidation,
            Messages: [
                new ScenarioMessage { Role = "user", Content = "Question", DelayMs = 100 },
                new ScenarioMessage { Role = "assistant", Content = "Answer" }
            ],
            CreatedBy: Guid.NewGuid(),
            ExpectedOutcome: "Should validate RAG context",
            AgentDefinitionId: Guid.NewGuid(),
            Tags: ["rag", "validation"]);

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MessageDelay_NegativeValue_FailsValidation()
    {
        var command = CreateValidCommand() with
        {
            Messages = [new ScenarioMessage { Role = "user", Content = "text", DelayMs = -1 }]
        };
        var result = _validator.TestValidate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void MessageDelay_TooLarge_FailsValidation()
    {
        var command = CreateValidCommand() with
        {
            Messages = [new ScenarioMessage { Role = "user", Content = "text", DelayMs = 31000 }]
        };
        var result = _validator.TestValidate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void BoundaryName_200Chars_PassesValidation()
    {
        var command = CreateValidCommand() with { Name = new string('x', 200) };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void AllCategories_PassValidation()
    {
        foreach (ScenarioCategory category in Enum.GetValues<ScenarioCategory>())
        {
            var command = CreateValidCommand() with { Category = category };
            var result = _validator.TestValidate(command);
            result.ShouldNotHaveValidationErrorFor(x => x.Category);
        }
    }
}
