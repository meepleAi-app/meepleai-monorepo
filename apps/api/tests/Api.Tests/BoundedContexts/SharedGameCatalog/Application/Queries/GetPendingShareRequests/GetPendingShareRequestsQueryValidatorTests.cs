using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;

/// <summary>
/// Unit tests for GetPendingShareRequestsQueryValidator.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetPendingShareRequestsQueryValidatorTests
{
    private readonly GetPendingShareRequestsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuery_ShouldPass()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(
            ShareRequestStatus.Pending,
            ContributionType.NewGame,
            "test search",
            ShareRequestSortField.CreatedAt,
            SortDirection.Ascending,
            1,
            20);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullFilters_ShouldPass()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithInvalidPageNumber_ShouldFail(int pageNumber)
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null, PageNumber: pageNumber);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageNumber");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public void Validate_WithInvalidPageSize_ShouldFail(int pageSize)
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null, PageSize: pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    [Theory]
    [InlineData(ShareRequestStatus.Approved)]
    [InlineData(ShareRequestStatus.Rejected)]
    [InlineData(ShareRequestStatus.Withdrawn)]
    public void Validate_WithTerminalStatus_ShouldFail(ShareRequestStatus status)
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(status, null, null);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StatusFilter.Value");
    }

    [Theory]
    [InlineData(ShareRequestStatus.Pending)]
    [InlineData(ShareRequestStatus.InReview)]
    [InlineData(ShareRequestStatus.ChangesRequested)]
    public void Validate_WithActiveStatus_ShouldPass(ShareRequestStatus status)
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(status, null, null);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithSearchTermTooLong_ShouldFail()
    {
        // Arrange
        var longSearchTerm = new string('a', 201);
        var query = new GetPendingShareRequestsQuery(null, null, longSearchTerm);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SearchTerm");
    }

    [Fact]
    public void Validate_WithSearchTermAt200Chars_ShouldPass()
    {
        // Arrange
        var validSearchTerm = new string('a', 200);
        var query = new GetPendingShareRequestsQuery(null, null, validSearchTerm);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_WithValidPageSize_ShouldPass(int pageSize)
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null, PageSize: pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
