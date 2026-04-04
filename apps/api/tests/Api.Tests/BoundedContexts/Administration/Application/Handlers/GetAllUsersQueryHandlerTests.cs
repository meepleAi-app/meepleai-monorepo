using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetAllUsersQueryHandler.
/// Tests user listing with pagination, filtering, and sorting.
/// NOTE: Uses DbContext directly - simplified tests due to mocking complexity.
/// ISSUE-1674: Add integration tests for full pagination/filtering workflow.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAllUsersQueryHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }
    [Fact]
    public void Constructor_WithValidDbContext_CreatesInstance()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();

        // Act
        var handler = new GetAllUsersQueryHandler(context);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new GetAllUsersQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }
    [Fact]
    public void Query_WithDefaultPagination_ConstructsCorrectly()
    {
        // Act
        var query = new GetAllUsersQuery(
            Page: 1,
            Limit: 20);

        // Assert
        query.Page.Should().Be(1);
        query.Limit.Should().Be(20);
        query.SearchTerm.Should().BeNull();
        query.RoleFilter.Should().BeNull();
        query.SortBy.Should().BeNull();
        query.SortOrder.Should().Be("desc"); // Default sort order
    }

    [Fact]
    public void Query_WithSearchTerm_ConstructsCorrectly()
    {
        // Act
        var query = new GetAllUsersQuery(
            Page: 1,
            Limit: 20,
            SearchTerm: "john");

        // Assert
        query.SearchTerm.Should().Be("john");
    }

    [Fact]
    public void Query_WithRoleFilter_ConstructsCorrectly()
    {
        // Act
        var query = new GetAllUsersQuery(
            Page: 1,
            Limit: 20,
            RoleFilter: Role.Admin.Value);

        // Assert
        query.RoleFilter.Should().Be(Role.Admin.Value);
    }

    [Fact]
    public void Query_WithSorting_ConstructsCorrectly()
    {
        // Act
        var query = new GetAllUsersQuery(
            Page: 1,
            Limit: 20,
            SortBy: "email",
            SortOrder: "asc");

        // Assert
        query.SortBy.Should().Be("email");
        query.SortOrder.Should().Be("asc");
    }

    [Fact]
    public void Query_WithAllParameters_ConstructsCorrectly()
    {
        // Act
        var query = new GetAllUsersQuery(
            Page: 2,
            Limit: 50,
            SearchTerm: "test",
            RoleFilter: Role.Editor.Value,
            SortBy: "displayname",
            SortOrder: "asc");

        // Assert
        query.Page.Should().Be(2);
        query.Limit.Should().Be(50);
        query.SearchTerm.Should().Be("test");
        query.RoleFilter.Should().Be(Role.Editor.Value);
        query.SortBy.Should().Be("displayname");
        query.SortOrder.Should().Be("asc");
    }
    // NOTE: Full integration tests for Handle method (pagination, filtering, sorting)
    // should be in integration test suite due to DbContext complexity.
    // See integration-tests.yml workflow.
    //
    // Test scenarios for integration tests:
    // 1. Pagination (page 1, page 2, last page)
    // 2. Search term filtering (email contains, display name contains)
    // 3. Role filtering (admin, editor, user, all)
    // 4. Sorting (by email, displayname, role, createdAt)
    // 5. Sort order (asc, desc)
    // 6. Combined filters (search + role + sort)
    // 7. Empty result sets
    // 8. LastSeenAt calculation from active sessions
}
