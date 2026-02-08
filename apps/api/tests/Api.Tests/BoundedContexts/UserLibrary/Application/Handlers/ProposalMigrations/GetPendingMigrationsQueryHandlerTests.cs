using Api.BoundedContexts.UserLibrary.Application.Queries.ProposalMigrations;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.ProposalMigrations;

/// <summary>
/// Unit tests for GetPendingMigrationsQuery.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetPendingMigrationsQueryHandlerTests
{
    [Fact]
    public void GetPendingMigrationsQuery_CreatesWithUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var query = new GetPendingMigrationsQuery(UserId: userId);

        // Assert
        query.UserId.Should().Be(userId);
    }
}
