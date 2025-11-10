using Xunit;
using Api.Tests.Fixtures;

namespace Api.Tests;

/// <summary>
/// Test collection definition for WRITE admin endpoint tests.
/// These tests MODIFY database state and may have concurrency considerations.
///
/// Performance Optimization (Issue #829):
/// - ExecuteDeleteAsync optimizations applied (85-90% faster cleanup)
/// - DisableParallelization TEMPORARILY RESTORED due to CI resource contention
/// - Parallel execution caused 17min+ timeouts even with SQL fixes
/// - Root issue: GitHub Actions 2-core runner + 125 tests + heavy DB operations
///
/// Future: Re-enable parallel after implementing:
/// - Test sharding across multiple CI jobs
/// - Shared test user patterns (reduce PBKDF2 overhead)
/// - DbContext pooling optimizations
/// </summary>
[CollectionDefinition("Admin Endpoints", DisableParallelization = true)]
public class AdminTestCollection : ICollectionFixture<PostgresCollectionFixture>, ICollectionFixture<WebApplicationFactoryFixture>
{
    // This class has no code, and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
