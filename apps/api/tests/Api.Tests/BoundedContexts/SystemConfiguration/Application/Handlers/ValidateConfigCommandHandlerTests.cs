using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class ValidateConfigCommandHandlerTests
{
    private readonly ConfigurationValidator _validator = new();
    private readonly ValidateConfigCommandHandler _handler;

    public ValidateConfigCommandHandlerTests()
    {
        _handler = new ValidateConfigCommandHandler(_validator);
    }

    [Fact]
    public async Task Handle_ValidConfig_ReturnsIsValidTrue()
    {
        // Arrange
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
        var command = new ValidateConfigCommand("test.key", "abc", "integer");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }
}
