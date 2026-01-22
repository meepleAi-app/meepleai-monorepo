using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestHistory;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestHistory;

/// <summary>
/// Unit tests for GetShareRequestHistoryQueryValidator.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetShareRequestHistoryQueryValidatorTests
{
    private readonly GetShareRequestHistoryQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuery_ShouldPass()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyShareRequestId_ShouldFail()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ShareRequestId");
    }

    [Fact]
    public void Validate_ErrorMessageShouldBeDescriptive()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.First().ErrorMessage.Should().Contain("ShareRequestId");
    }
}
