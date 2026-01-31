using Xunit;

// Issue #2541: Parallel test execution enabled for performance optimization
// Tests require external Docker Compose infrastructure (see docs/05-testing/INTEGRATION_TEST_OPTIMIZATION.md)
// Set environment variables:
//   TEST_POSTGRES_CONNSTRING=Host=localhost;Port=5432;Database=test_shared;Username=admin;Password=<secret>;Ssl Mode=Disable
//   TEST_REDIS_CONNSTRING=localhost:6379
//
// [assembly: CollectionBehavior(DisableTestParallelization = true)] // REMOVED

