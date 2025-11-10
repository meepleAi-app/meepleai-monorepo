using Xunit;
using Api.Tests.Fixtures;

namespace Api.Tests;

/// <summary>
/// Test collection definition for READ-ONLY admin endpoint tests.
/// These tests do NOT modify database state and can run in parallel.
///
/// Performance Optimization (Issue #829):
/// - Split from main AdminTestCollection to enable parallel execution
/// - Read-only tests are safe to parallelize (no state modification)
/// - Expected speedup: 60-70% for tests in this collection
/// </summary>
[CollectionDefinition("Admin Endpoints - Read Only")]
public class AdminTestCollectionReadOnly : ICollectionFixture<PostgresCollectionFixture>, ICollectionFixture<WebApplicationFactoryFixture>
{
    // This class has no code, and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
