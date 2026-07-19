using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DatabaseSync;

/// <summary>
/// Integration tests for <see cref="CompareTableDataHandler"/> against a real PostgreSQL backend.
///
/// Issue #3210: requesting a diff for a table that does not exist is a 404 Not Found, not a 500.
/// Before this fix the handler threw a bare <see cref="InvalidOperationException"/> ("Table does not
/// exist on local database: ...") which the middleware mapped to 500.
///
/// A unit/mock test cannot catch this: the handler runs raw information_schema queries on a live
/// <c>NpgsqlConnection</c> obtained from the DbContext, so only a real relational provider exercises
/// the table-existence check. The handler is <c>internal</c>, so it is constructed inside the test
/// (via InternalsVisibleTo) rather than exposed through the public base-class type parameter.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DatabaseSync")]
[Trait("Issue", "3210")]
public sealed class CompareTableDataHandlerIntegrationTests
    : IntegrationTestBase<MeepleAiDbContext>
{
    // The local-table-missing branch throws before the remote connector is ever used,
    // so an unconfigured mock is sufficient.
    private readonly Mock<IRemoteDatabaseConnector> _remoteConnectorMock = new();

    protected override string DatabaseName => "test_compare_table_data";

    // TRepository is bound to the (public) DbContext; the handler under test is built per-test.
    protected override MeepleAiDbContext CreateRepository(MeepleAiDbContext dbContext) => dbContext;

    [Fact]
    public async Task Handle_LocalTableDoesNotExist_ThrowsNotFoundException()
    {
        var handler = new CompareTableDataHandler(DbContext, _remoteConnectorMock.Object);
        var query = new CompareTableDataQuery("nonexistent_table_xyz");

        Func<Task> act = () => handler.Handle(query, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
