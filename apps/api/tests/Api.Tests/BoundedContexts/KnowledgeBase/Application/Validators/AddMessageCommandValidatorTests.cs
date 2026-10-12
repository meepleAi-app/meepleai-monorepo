using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Tests for <see cref="AddMessageCommandValidator"/>.
/// The handler only accepts <c>user</c>/<c>assistant</c> roles and previously threw
/// <see cref="InvalidOperationException"/> (→ HTTP 500) on anything else. The validator
/// must reject an unknown role up front so the request maps to 422.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AddMessageCommandValidatorTests
{
    private readonly AddMessageCommandValidator _validator = new();

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    public void Validate_WithKnownRole_HasNoRoleError(string role)
    {
        var command = new AddMessageCommand(Guid.NewGuid(), "hello", role);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("admin")]
    [InlineData("unknown_role")]
    [InlineData("USER")]
    public void Validate_WithUnknownRole_HasRoleError(string role)
    {
        var command = new AddMessageCommand(Guid.NewGuid(), "hello", role);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    // #4168: versione del profilo e lingua descrivono una risposta, non una domanda.

    [Fact]
    public void Validate_AssistantWithProvenance_IsValid()
    {
        var command = new AddMessageCommand(Guid.NewGuid(), "hello", "assistant", Guid.NewGuid(), "it");

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_UserWithProvenance_HasErrors()
    {
        var command = new AddMessageCommand(Guid.NewGuid(), "hello", "user", Guid.NewGuid(), "it");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ProfileVersionId);
        result.ShouldHaveValidationErrorFor(x => x.Language);
    }

    [Theory]
    [InlineData("IT")]
    [InlineData("it-IT")]
    [InlineData(" it")]
    [InlineData("italiano")]
    [InlineData("")]
    public void Validate_LanguageNotAnIsoCode_HasLanguageError(string language)
    {
        var command = new AddMessageCommand(Guid.NewGuid(), "hello", "assistant", Guid.NewGuid(), language);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Language);
    }

    [Fact]
    public void Validate_EmptyProfileVersionId_HasError()
    {
        var command = new AddMessageCommand(Guid.NewGuid(), "hello", "assistant", Guid.Empty, "it");

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ProfileVersionId);
    }
}
