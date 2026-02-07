using Api.BoundedContexts.Administration.Application.Commands.Operations;
using Api.BoundedContexts.Administration.Application.Validators;
using Xunit;

namespace Api.Tests.Unit.Administration.Operations;

/// <summary>
/// Unit tests for RestartServiceCommandValidator.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public sealed class RestartServiceCommandValidatorTests
{
    private readonly RestartServiceCommandValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public void Validate_ValidCommand_PassesValidation()
    {
        // Arrange
        var command = new RestartServiceCommand("API", Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public void Validate_EmptyServiceName_FailsValidation()
    {
        // Arrange
        var command = new RestartServiceCommand("", Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ServiceName");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public void Validate_InvalidServiceName_FailsValidation()
    {
        // Arrange
        var command = new RestartServiceCommand("PostgreSQL", Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "ServiceName" &&
            e.ErrorMessage.Contains("Invalid service name", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public void Validate_EmptyAdminUserId_FailsValidation()
    {
        // Arrange
        var command = new RestartServiceCommand("API", Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "AdminUserId");
    }

    [Theory]
    [InlineData("api")]
    [InlineData("Api")]
    [InlineData("API")]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public void Validate_ServiceNameCaseInsensitive_PassesValidation(string serviceName)
    {
        // Arrange
        var command = new RestartServiceCommand(serviceName, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }
}
