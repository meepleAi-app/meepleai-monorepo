using Api.BoundedContexts.Administration.Application.Commands.ImportRagData;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Commands;

/// <summary>
/// Unit tests for <see cref="ImportRagDataCommandValidator"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class ImportRagDataCommandValidatorTests
{
    private readonly ImportRagDataCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidSnapshotPath_ShouldPass()
    {
        // Arrange
        var command = new ImportRagDataCommand
        {
            SnapshotPath = "rag-exports/2026-03-28T12-00-00Z",
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySnapshotPath_ShouldFail()
    {
        // Arrange
        var command = new ImportRagDataCommand
        {
            SnapshotPath = string.Empty,
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ImportRagDataCommand.SnapshotPath));
    }

    [Fact]
    public void Validate_WhitespaceOnlySnapshotPath_ShouldFail()
    {
        // Arrange
        var command = new ImportRagDataCommand
        {
            SnapshotPath = "   ",
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ImportRagDataCommand.SnapshotPath));
    }

    [Theory]
    [InlineData("rag-exports/2026-01-01")]
    [InlineData("rag-exports/snapshot-001")]
    [InlineData("any-non-empty-path")]
    public void Validate_NonEmptySnapshotPath_ShouldPass(string snapshotPath)
    {
        // Arrange
        var command = new ImportRagDataCommand { SnapshotPath = snapshotPath };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
