using Xunit;

// Configure parallel test execution (TEST-02 #610)
// MaxParallelThreads: 4 (optimal for multi-core systems)
// Improves throughput by 2-4x on 4+ core machines
[assembly: CollectionBehavior(
    DisableTestParallelization = false,
    MaxParallelThreads = 4
)]

// Enable assembly-wide test process cleanup (xUnit v3 native support)
// The TestProcessCleanup fixture runs after all tests in the assembly complete
[assembly: AssemblyFixture(typeof(Api.Tests.TestProcessCleanup))]
