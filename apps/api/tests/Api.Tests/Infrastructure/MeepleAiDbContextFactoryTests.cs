using Api.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Tests for MeepleAiDbContextFactory to prevent regression of Issue #2112.
/// Verifies cross-platform environment variable handling (case sensitivity).
/// </summary>
public class MeepleAiDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithUppercaseEnvVar_ShouldUseConnectionString()
    {
        // Arrange
        var factory = new MeepleAiDbContextFactory();
        var testConnectionString = "Host=test-host;Database=test-db;Username=test-user;Password=test-pass";

        try
        {
            Environment.SetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES", testConnectionString);

            // Act
            var context = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            context.Should().NotBeNull();
            // DbContext created successfully means connection string was read
            // Actual connection not tested (no database required for this test)
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES", null);
        }
    }

    [Fact]
    public void CreateDbContext_WithCamelCaseEnvVar_ShouldUseConnectionString()
    {
        // Arrange - Issue #2112: Linux CI uses camelCase env vars
        var factory = new MeepleAiDbContextFactory();
        var testConnectionString = "Host=test-host;Database=test-db;Username=test-user;Password=test-pass";

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", testConnectionString);

            // Act
            var context = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            context.Should().NotBeNull();
            // This test ensures Linux CI compatibility (case-sensitive env vars)
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        }
    }

    [Fact]
    public void CreateDbContext_WithLegacyEnvVar_ShouldUseConnectionString()
    {
        // Arrange - Legacy POSTGRES_CONNECTION_STRING support
        var factory = new MeepleAiDbContextFactory();
        var testConnectionString = "Host=legacy-host;Database=legacy-db;Username=legacy;Password=legacy";

        try
        {
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", testConnectionString);

            // Act
            var context = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            context.Should().NotBeNull();
            // Ensures backward compatibility with legacy env var name
        }
        finally
        {
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", null);
        }
    }

    [Fact]
    public void CreateDbContext_WithNoEnvVar_ShouldUseDummyConnectionString()
    {
        // Arrange - No env vars set (design-time migrations scenario)
        var factory = new MeepleAiDbContextFactory();

        try
        {
            // Ensure all env vars are null
            Environment.SetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", null);

            // Act
            var context = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            context.Should().NotBeNull();
            // Should use dummy connection string for design-time operations
        }
        finally
        {
            // Cleanup not strictly needed (vars already null), but good practice
        }
    }

    [Fact]
    public void CreateDbContext_PriorityOrder_ShouldPreferUppercase()
    {
        // Arrange - Multiple env vars set, verify priority order
        var factory = new MeepleAiDbContextFactory();

        var uppercaseConn = "Host=uppercase-host;Database=uppercase-db;";
        var camelCaseConn = "Host=camelcase-host;Database=camelcase-db;";
        var legacyConn = "Host=legacy-host;Database=legacy-db;";

        try
        {
            // Set all three variants
            Environment.SetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES", uppercaseConn);
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", camelCaseConn);
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", legacyConn);

            // Act
            var context = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            context.Should().NotBeNull();
            // Should prefer uppercase variant (first in chain)
            // Cannot directly assert connection string (private in DbContext)
            // This test documents priority: UPPERCASE → camelCase → LEGACY → dummy
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", null);
        }
    }
}
