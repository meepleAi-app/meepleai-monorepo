using Api.BoundedContexts.Administration.Application.Handlers;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.Unit.Administration;

/// <summary>
/// Unit tests for GetUserLibraryStatsQueryHandler.
/// Note: Full workflow testing is covered by integration tests.
/// These tests validate basic handler construction.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class GetUserLibraryStatsQueryHandlerTests
{
    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetUserLibraryStatsQueryHandler(null!));
    }
}
