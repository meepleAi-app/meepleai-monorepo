using Xunit;

// Disable test parallelization for integration tests that use database
[assembly: CollectionBehavior(DisableTestParallelization = true)]
