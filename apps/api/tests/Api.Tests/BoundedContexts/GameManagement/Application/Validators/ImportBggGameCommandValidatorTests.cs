using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Validators;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Unit tests for ImportBggGameCommandValidator.
/// Game Night Improvvisata - E1-2: Import BGG game with tier enforcement.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class ImportBggGameCommandValidatorTests
{
    private readonly ImportBggGameCommandValidator _sut = new();

    [Fact]
    public async Task Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new ImportBggGameCommand(
            UserId: Guid.NewGuid(),
            BggId: 12345);

        // Act
        var result = await _sut.ValidateAsync(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange
        var command = new ImportBggGameCommand(
            UserId: Guid.Empty,
            BggId: 12345);

        // Act
        var result = await _sut.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ImportBggGameCommand.UserId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Validate_WithInvalidBggId_FailsValidation(int bggId)
    {
        // Arrange
        var command = new ImportBggGameCommand(
            UserId: Guid.NewGuid(),
            BggId: bggId);

        // Act
        var result = await _sut.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ImportBggGameCommand.BggId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(999999)]
    public async Task Validate_WithValidBggId_PassesValidation(int bggId)
    {
        // Arrange
        var command = new ImportBggGameCommand(
            UserId: Guid.NewGuid(),
            BggId: bggId);

        // Act
        var result = await _sut.ValidateAsync(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WithBothInvalid_ReportsMultipleErrors()
    {
        // Arrange
        var command = new ImportBggGameCommand(
            UserId: Guid.Empty,
            BggId: 0);

        // Act
        var result = await _sut.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        result.Errors.Count.Should().Be(2);
    }
}
