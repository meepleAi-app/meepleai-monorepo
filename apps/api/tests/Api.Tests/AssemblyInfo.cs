using Xunit;

// Enable assembly-wide test process cleanup
// The TestProcessCleanup fixture runs after all tests in the assembly complete
[assembly: AssemblyFixture(typeof(Api.Tests.TestProcessCleanup))]
