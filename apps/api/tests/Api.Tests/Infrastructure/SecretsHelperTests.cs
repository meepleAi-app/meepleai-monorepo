using System;
using System.IO;
using Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Tests for SecretsHelper (SEC-708: Docker Secrets implementation).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SecretsHelperTests
{
    [Fact]
    public void GetSecretOrValue_ReadFromFile_Success()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "test-secret-value\n");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("MY_SECRET_FILE", tempFile)
                })
                .Build();

            // Act
            var result = SecretsHelper.GetSecretOrValue(config, "MY_SECRET", NullLogger.Instance);

            // Assert
            result.Should().Be("test-secret-value");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetSecretOrValue_ReadFromDirectConfig_Success()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("MY_SECRET", "direct-value")
            })
            .Build();

        // Act
        var result = SecretsHelper.GetSecretOrValue(config, "MY_SECRET", NullLogger.Instance);

        // Assert
        result.Should().Be("direct-value");
    }

    [Fact]
    public void GetSecretOrValue_FileNotFound_Throws()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("MY_SECRET_FILE", "/nonexistent/path/secret.txt")
            })
            .Build();

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            SecretsHelper.GetSecretOrValue(config, "MY_SECRET", NullLogger.Instance));
    }

    [Fact]
    public void GetSecretOrValue_EmptyFile_Throws()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "   \n   ");  // Whitespace only

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("MY_SECRET_FILE", tempFile)
                })
                .Build();

            // Act & Assert - IOException wraps InvalidOperationException
            Assert.Throws<IOException>(() =>
                SecretsHelper.GetSecretOrValue(config, "MY_SECRET", NullLogger.Instance));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetSecretOrValue_NotFound_Required_Throws()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            SecretsHelper.GetSecretOrValue(config, "MY_SECRET", NullLogger.Instance, required: true));
    }

    [Fact]
    public void GetSecretOrValue_NotFound_Optional_ReturnsNull()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        // Act
        var result = SecretsHelper.GetSecretOrValue(config, "MY_SECRET", NullLogger.Instance, required: false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSecretOrValue_PrioritizeFileOverDirect()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "file-value");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("MY_SECRET_FILE", tempFile),
                    new KeyValuePair<string, string?>("MY_SECRET", "direct-value")
                })
                .Build();

            // Act
            var result = SecretsHelper.GetSecretOrValue(config, "MY_SECRET", NullLogger.Instance);

            // Assert - should use file value, not direct
            result.Should().Be("file-value");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildPostgresConnectionString_Success()
    {
        // Arrange - Save and clear environment variables to isolate from CI
        var originalHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
        var originalPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");
        var originalDb = Environment.GetEnvironmentVariable("POSTGRES_DB");
        var originalUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
        var tempFile = Path.GetTempFileName();
        try
        {
            // Clear CI environment variables that would override test config
            Environment.SetEnvironmentVariable("POSTGRES_HOST", null);
            Environment.SetEnvironmentVariable("POSTGRES_PORT", null);
            Environment.SetEnvironmentVariable("POSTGRES_DB", null);
            Environment.SetEnvironmentVariable("POSTGRES_USER", null);

            File.WriteAllText(tempFile, "secure-db-password");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("POSTGRES_HOST", "testhost"),
                    new KeyValuePair<string, string?>("POSTGRES_PORT", "5433"),
                    new KeyValuePair<string, string?>("POSTGRES_DB", "testdb"),
                    new KeyValuePair<string, string?>("POSTGRES_USER", "testuser"),
                    new KeyValuePair<string, string?>("POSTGRES_PASSWORD_FILE", tempFile)
                })
                .Build();

            // Act
            var connectionString = SecretsHelper.BuildPostgresConnectionString(config, NullLogger.Instance);

            // Assert
            connectionString.Should().Contain("Host=testhost");
            connectionString.Should().Contain("Port=5433");
            connectionString.Should().Contain("Database=testdb");
            connectionString.Should().Contain("Username=testuser");
            connectionString.Should().Contain("Password=secure-db-password");
        }
        finally
        {
            File.Delete(tempFile);
            // Restore original environment variables
            Environment.SetEnvironmentVariable("POSTGRES_HOST", originalHost);
            Environment.SetEnvironmentVariable("POSTGRES_PORT", originalPort);
            Environment.SetEnvironmentVariable("POSTGRES_DB", originalDb);
            Environment.SetEnvironmentVariable("POSTGRES_USER", originalUser);
        }
    }

    [Fact]
    public void BuildPostgresConnectionString_UsesDefaults()
    {
        // Arrange - Save and clear environment variables to isolate from CI
        var originalHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
        var originalPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");
        var originalDb = Environment.GetEnvironmentVariable("POSTGRES_DB");
        var originalUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
        var tempFile = Path.GetTempFileName();
        try
        {
            // Clear CI environment variables to test actual defaults
            Environment.SetEnvironmentVariable("POSTGRES_HOST", null);
            Environment.SetEnvironmentVariable("POSTGRES_PORT", null);
            Environment.SetEnvironmentVariable("POSTGRES_DB", null);
            Environment.SetEnvironmentVariable("POSTGRES_USER", null);

            File.WriteAllText(tempFile, "password");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("POSTGRES_PASSWORD_FILE", tempFile)
                })
                .Build();

            // Act
            var connectionString = SecretsHelper.BuildPostgresConnectionString(config, NullLogger.Instance);

            // Assert - should use defaults (Issue #2152: default username is now 'postgres')
            // Note: Default host is 'localhost' for local development; Docker sets POSTGRES_HOST=postgres
            connectionString.Should().Contain("Host=localhost");
            connectionString.Should().Contain("Port=5432");
            connectionString.Should().Contain("Database=meepleai");
            connectionString.Should().Contain("Username=postgres");
            connectionString.Should().Contain("Password=password");
        }
        finally
        {
            File.Delete(tempFile);
            // Restore original environment variables
            Environment.SetEnvironmentVariable("POSTGRES_HOST", originalHost);
            Environment.SetEnvironmentVariable("POSTGRES_PORT", originalPort);
            Environment.SetEnvironmentVariable("POSTGRES_DB", originalDb);
            Environment.SetEnvironmentVariable("POSTGRES_USER", originalUser);
        }
    }

    [Fact]
    public void BuildPostgresConnectionString_NoPassword_ReturnsNull()
    {
        // Arrange
        // Issue #2287: Clear POSTGRES_PASSWORD env var to test isolation from CI environment
        var originalPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", null);
            var config = new ConfigurationBuilder().Build();

            // Act
            var result = SecretsHelper.BuildPostgresConnectionString(config, NullLogger.Instance);

            // Assert - should return null when no password configured (line 140 in SecretsHelper.cs)
            result.Should().BeNull();
        }
        finally
        {
            // Restore original value
            Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", originalPassword);
        }
    }
}

