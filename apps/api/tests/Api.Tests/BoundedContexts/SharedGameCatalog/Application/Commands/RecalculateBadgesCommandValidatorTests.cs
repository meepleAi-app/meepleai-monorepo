using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class RecalculateBadgesCommandValidatorTests
{
    private readonly RecalculateBadgesCommandValidator _validator;

    public RecalculateBadgesCommandValidatorTests()
    {
        _validator = new RecalculateBadgesCommandValidator();
    }

    [Fact]
    public async Task Validate_WithValidUserId_Passes()
    {
        // Arrange
        var command = new RecalculateBadgesCommand { UserId = Guid.NewGuid() };

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_WithNullUserId_Passes()
    {
        // Arrange - Null UserId means "all users"
        var command = new RecalculateBadgesCommand { UserId = null };

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_WithEmptyGuid_Fails()
    {
        // Arrange
        var command = new RecalculateBadgesCommand { UserId = Guid.Empty };

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("User ID must not be empty", result.Errors[0].ErrorMessage);
    }
}
