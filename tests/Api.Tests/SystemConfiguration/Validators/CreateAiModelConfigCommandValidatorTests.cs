using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SystemConfiguration.Validators;

public sealed class CreateAiModelConfigCommandValidatorTests
{
    private readonly CreateAiModelConfigCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CreateAiModelConfigCommand(
            "gpt-4o-mini",
            "GPT-4o Mini",
            "OpenRouter",
            Priority: 1,
            Settings: null,
            Pricing: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyModelId_ShouldFail()
    {
        // Arrange
        var command = new CreateAiModelConfigCommand(
            "",
            "GPT-4o Mini",
            "OpenRouter",
            Priority: 1,
            Settings: null,
            Pricing: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAiModelConfigCommand.ModelId));
    }

    [Fact]
    public void Validate_InvalidProvider_ShouldFail()
    {
        // Arrange
        var command = new CreateAiModelConfigCommand(
            "gpt-4o-mini",
            "GPT-4o Mini",
            "InvalidProvider",
            Priority: 1,
            Settings: null,
            Pricing: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAiModelConfigCommand.Provider));
    }

    [Fact]
    public void Validate_InvalidPriority_ShouldFail()
    {
        // Arrange
        var command = new CreateAiModelConfigCommand(
            "gpt-4o-mini",
            "GPT-4o Mini",
            "OpenRouter",
            Priority: 0,
            Settings: null,
            Pricing: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAiModelConfigCommand.Priority));
    }
}
