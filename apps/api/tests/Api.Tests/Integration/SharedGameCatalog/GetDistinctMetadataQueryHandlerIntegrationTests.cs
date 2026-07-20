using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Issue #3228: <see cref="GetDistinctMetadataQueryHandler"/> ran 4 queries with Task.WhenAll on a
/// single scoped <see cref="MeepleAiDbContext"/>. EF Core's ConcurrencyDetector forbids concurrent
/// operations on the same context and throws InvalidOperationException ("A second operation was
/// started on this context instance before a previous operation completed").
///
/// Only a real relational provider exercises the concurrency guard (a mock/in-memory context does
/// not), so this is an integration test. Before the fix the handler throws; after serializing the
/// queries it completes.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3228")]
public sealed class GetDistinctMetadataQueryHandlerIntegrationTests : IntegrationTestBase<MeepleAiDbContext>
{
    protected override string DatabaseName => "test_distinct_metadata_concurrency";

    protected override MeepleAiDbContext CreateRepository(MeepleAiDbContext dbContext) => dbContext;

    [Fact]
    public async Task Handle_MultipleQueries_DoesNotThrowConcurrencyException()
    {
        var handler = new GetDistinctMetadataQueryHandler(DbContext);

        // With Task.WhenAll on one scoped context this trips EF's ConcurrencyDetector and throws;
        // serialized queries complete normally and return a non-null DTO (empty lists on a fresh DB).
        var result = await handler.Handle(new GetDistinctMetadataQuery(), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Categories.Should().NotBeNull();
        result.Mechanics.Should().NotBeNull();
        result.Designers.Should().NotBeNull();
        result.Publishers.Should().NotBeNull();
    }
}
