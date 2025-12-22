using System;
using System.IO;
using Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
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
            Assert.Equal("test-secret-value", result);
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
        Assert.Equal("direct-value", result);
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
        Assert.Null(result);
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
            Assert.Equal("file-value", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildPostgresConnectionString_Success()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
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
            Assert.Contains("Host=testhost", connectionString);
            Assert.Contains("Port=5433", connectionString);
            Assert.Contains("Database=testdb", connectionString);
            Assert.Contains("Username=testuser", connectionString);
            Assert.Contains("Password=secure-db-password", connectionString);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildPostgresConnectionString_UsesDefaults()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
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
            Assert.Contains("Host=postgres", connectionString);
            Assert.Contains("Port=5432", connectionString);
            Assert.Contains("Database=meepleai", connectionString);
            Assert.Contains("Username=postgres", connectionString);
            Assert.Contains("Password=password", connectionString);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildPostgresConnectionString_NoPassword_Throws()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            SecretsHelper.BuildPostgresConnectionString(config, NullLogger.Instance));
    }
}

