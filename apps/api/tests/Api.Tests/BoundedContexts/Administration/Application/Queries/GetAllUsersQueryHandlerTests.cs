using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Moq;
using Xunit;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

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
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetAllUsersQueryHandler(null!));
    }
    [Fact]
    public void Query_WithDefaultPagination_ConstructsCorrectly()
    {
        // Act
        var query = new GetAllUsersQuery(
            Page: 1,
            Limit: 20);

        // Assert
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.Limit);
        Assert.Null(query.SearchTerm);
        Assert.Null(query.RoleFilter);
        Assert.Null(query.SortBy);
        Assert.Equal("desc", query.SortOrder); // Default sort order
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
        Assert.Equal("john", query.SearchTerm);
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
        Assert.Equal(Role.Admin.Value, query.RoleFilter);
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
        Assert.Equal("email", query.SortBy);
        Assert.Equal("asc", query.SortOrder);
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
        Assert.Equal(2, query.Page);
        Assert.Equal(50, query.Limit);
        Assert.Equal("test", query.SearchTerm);
        Assert.Equal(Role.Editor.Value, query.RoleFilter);
        Assert.Equal("displayname", query.SortBy);
        Assert.Equal("asc", query.SortOrder);
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
