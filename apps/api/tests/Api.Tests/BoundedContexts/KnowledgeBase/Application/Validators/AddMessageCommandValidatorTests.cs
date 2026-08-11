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
}
