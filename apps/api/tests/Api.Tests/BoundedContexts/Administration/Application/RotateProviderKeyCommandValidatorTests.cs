using Api.BoundedContexts.Administration.Application.Commands.Providers;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application;

/// <summary>
/// Unit tests for <see cref="RotateProviderKeyCommandValidator"/>. Issue #1859.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class RotateProviderKeyCommandValidatorTests
{
    private readonly RotateProviderKeyCommandValidator _sut = new();

    private static RotateProviderKeyCommand ValidCommand(
        string provider = "deepseek",
        string newKey = "sk-deepseek-newkey-12345",
        string? confirmed = null,
        Guid? actor = null) =>
        new(provider, newKey, confirmed ?? provider, actor ?? Guid.NewGuid());

    [Fact]
    public void Validate_AllValid_NoErrors()
    {
        _sut.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_OpenRouterIsAllowed()
    {
        _sut.TestValidate(ValidCommand(provider: "openrouter", confirmed: "openrouter"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyProviderName_Errors(string provider)
    {
        _sut.TestValidate(ValidCommand(provider: provider, confirmed: provider))
            .ShouldHaveValidationErrorFor(x => x.ProviderName);
    }

    [Theory]
    [InlineData("ollama")]      // not in whitelist (no API key required)
    [InlineData("gpt4")]
    [InlineData("anthropic")]
    public void Validate_DisallowedProviderName_Errors(string provider)
    {
        var cmd = ValidCommand(provider: provider, confirmed: provider);
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ProviderName);
    }

    [Fact]
    public void Validate_ConfirmedMismatch_Errors()
    {
        _sut.TestValidate(ValidCommand(provider: "deepseek", confirmed: "openrouter"))
            .ShouldHaveValidationErrorFor(x => x.ConfirmedProviderName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]                              // <10
    [InlineData("nine_char")]                          // 9 chars
    [InlineData("has whitespace inside")]
    [InlineData("has\ttab")]
    [InlineData("has\nnewline")]
    public void Validate_InvalidNewApiKey_Errors(string key)
    {
        _sut.TestValidate(ValidCommand(newKey: key))
            .ShouldHaveValidationErrorFor(x => x.NewApiKey);
    }

    [Fact]
    public void Validate_ApiKeyAtMinimumLength_NoErrors()
    {
        // Exactly 10 chars — boundary
        _sut.TestValidate(ValidCommand(newKey: new string('a', 10)))
            .ShouldNotHaveValidationErrorFor(x => x.NewApiKey);
    }

    [Fact]
    public void Validate_ApiKeyAtMaximumLength_NoErrors()
    {
        // Exactly 512 chars — upper boundary
        _sut.TestValidate(ValidCommand(newKey: new string('a', 512)))
            .ShouldNotHaveValidationErrorFor(x => x.NewApiKey);
    }

    [Fact]
    public void Validate_ApiKeyTooLong_Errors()
    {
        _sut.TestValidate(ValidCommand(newKey: new string('a', 513)))
            .ShouldHaveValidationErrorFor(x => x.NewApiKey);
    }

    [Fact]
    public void Validate_EmptyRequestingUserId_Errors()
    {
        _sut.TestValidate(ValidCommand(actor: Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.RequestingUserId);
    }
}
