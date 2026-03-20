using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class ValidateConfigCommandHandlerTests
{
    private readonly Mock<ConfigurationValidator> _validatorMock = new();
    private readonly ValidateConfigCommandHandler _handler;

    public ValidateConfigCommandHandlerTests()
    {
        _handler = new ValidateConfigCommandHandler(_validatorMock.Object);
    }

    [Fact]
    public async Task Handle_ValidConfig_ReturnsIsValidTrue()
    {
        // Arrange
        _validatorMock.Setup(v => v.Validate("test.key", "100", "integer"))
            .Returns(new Api.BoundedContexts.SystemConfiguration.Domain.Services.ValidationResult(true, Array.Empty<string>()));

        var command = new ValidateConfigCommand("test.key", "100", "integer");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Handle_InvalidConfig_ReturnsIsValidFalseWithErrors()
    {
        // Arrange
        var errors = new[] { "Value must be a valid integer" };
        _validatorMock.Setup(v => v.Validate("test.key", "abc", "integer"))
            .Returns(new Api.BoundedContexts.SystemConfiguration.Domain.Services.ValidationResult(false, errors));

        var command = new ValidateConfigCommand("test.key", "abc", "integer");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }
}
