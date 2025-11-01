using Xunit;

namespace Api.Tests;

/// <summary>
/// Test collection definition for admin endpoint tests.
/// Tests in this collection will not run in parallel with each other,
/// preventing resource contention and deadlocks on the shared WebApplicationFactory.
/// </summary>
[CollectionDefinition("Admin Endpoints", DisableParallelization = true)]
public class AdminTestCollection : ICollectionFixture<WebApplicationFactoryFixture>
{
    // This class has no code, and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
