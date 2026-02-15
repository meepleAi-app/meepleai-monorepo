using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.UserNotifications.Application.Validators;

[Trait("Category", TestCategories.Unit)]
public sealed class EnqueueEmailCommandValidatorTests
{
    private readonly EnqueueEmailCommandValidator _validator = new();

    private static EnqueueEmailCommand CreateValidCommand() => new(
        UserId: Guid.NewGuid(),
        To: "user@test.com",
        Subject: "Test Subject",
        TemplateName: "document_ready",
        UserName: "Test User",
        FileName: "rules.pdf");

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var command = CreateValidCommand();
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        var command = CreateValidCommand() with { UserId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyTo_Fails(string to)
    {
        var command = CreateValidCommand() with { To = to };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Validate_InvalidEmail_Fails()
    {
        var command = CreateValidCommand() with { To = "not-an-email" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.To);
    }

    [Fact]
    public void Validate_EmptySubject_Fails()
    {
        var command = CreateValidCommand() with { Subject = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Validate_SubjectTooLong_Fails()
    {
        var command = CreateValidCommand() with { Subject = new string('a', 501) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Validate_InvalidTemplateName_Fails()
    {
        var command = CreateValidCommand() with { TemplateName = "unknown_template" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TemplateName);
    }

    [Theory]
    [InlineData("document_ready")]
    [InlineData("document_failed")]
    [InlineData("retry_available")]
    public void Validate_ValidTemplateNames_Pass(string templateName)
    {
        var command = CreateValidCommand() with { TemplateName = templateName };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.TemplateName);
    }

    [Fact]
    public void Validate_EmptyUserName_Fails()
    {
        var command = CreateValidCommand() with { UserName = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserName);
    }

    [Fact]
    public void Validate_EmptyFileName_Fails()
    {
        var command = CreateValidCommand() with { FileName = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FileName);
    }
}
