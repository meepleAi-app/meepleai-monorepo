using Api.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Infrastructure.Configuration;

/// <summary>
/// Unit tests for SecretLoader
/// </summary>
/// <remarks>
/// ISSUE-2510: Secrets management system validation
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public sealed class SecretLoaderTests : IDisposable
{
    private readonly string _testSecretsDirectory;
    private readonly ILogger<SecretLoaderTests> _logger;
    private bool _disposed;

    public SecretLoaderTests()
    {
        // Create temporary directory for test secrets
        _testSecretsDirectory = Path.Combine(Path.GetTempPath(), $"meepleai-test-secrets-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSecretsDirectory);

        // Create mock logger
        var mockLogger = new Mock<ILogger<SecretLoaderTests>>();
        _logger = mockLogger.Object;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Cleanup test directory
            if (Directory.Exists(_testSecretsDirectory))
            {
                Directory.Delete(_testSecretsDirectory, recursive: true);
            }
        }

        _disposed = true;
    }

    [Fact]
    public void LoadAndValidate_WhenDirectoryNotExists_ShouldReturnAllSecretsAsMissing()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid()}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsDirectory"] = nonExistentDir
            })
            .Build();

        var loader = new SecretLoader(configuration, _logger);

        // Act
        var result = loader.LoadAndValidate();

        // Assert
        result.IsValid.Should().BeFalse("directory does not exist, so critical secrets are missing");
        result.MissingCritical.Should().NotBeEmpty("should have critical secrets missing");
        result.LoadedSecrets.Should().BeEmpty("no secrets loaded from non-existent directory");
    }

    [Fact]
    public void LoadAndValidate_WhenCriticalSecretMissing_ShouldReturnInvalid()
    {
        // Arrange
        var configuration = CreateConfiguration(_testSecretsDirectory);
        var loader = new SecretLoader(configuration, _logger);

        // Don't create any secret files - all missing

        // Act
        var result = loader.LoadAndValidate();

        // Assert
        result.IsValid.Should().BeFalse("critical secrets are missing");
        result.MissingCritical.Should().Contain(key => key.Contains("database.secret:POSTGRES_USER"));
        result.MissingCritical.Should().Contain(key => key.Contains("redis.secret:REDIS_PASSWORD"));
        result.MissingCritical.Should().Contain(key => key.Contains("jwt.secret:JWT_SECRET_KEY"));
    }

    [Fact]
    public void LoadAndValidate_WhenAllCriticalSecretsPresent_ShouldReturnValid()
    {
        // Arrange
        var configuration = CreateConfiguration(_testSecretsDirectory);
        var loader = new SecretLoader(configuration, _logger);

        // Create all critical secret files
        CreateSecretFile("database.secret", "POSTGRES_USER=testuser", "POSTGRES_PASSWORD=testpass", "POSTGRES_DB=testdb");
        CreateSecretFile("redis.secret", "REDIS_PASSWORD=redispass");
        CreateSecretFile("qdrant.secret", "QDRANT_API_KEY=qdrantkey");
        CreateSecretFile("jwt.secret", "JWT_SECRET_KEY=jwtsecret", "JWT_ISSUER=issuer", "JWT_AUDIENCE=audience");
        CreateSecretFile("admin.secret", "ADMIN_EMAIL=admin@test.com", "ADMIN_PASSWORD=AdminPass123", "ADMIN_DISPLAY_NAME=Admin", "INITIAL_ADMIN_EMAIL=admin@test.com", "INITIAL_ADMIN_DISPLAY_NAME=Admin");
        CreateSecretFile("embedding-service.secret", "EMBEDDING_SERVICE_API_KEY=embedkey");

        // Act
        var result = loader.LoadAndValidate();

        // Assert
        result.IsValid.Should().BeTrue("all critical secrets are present");
        result.MissingCritical.Should().BeEmpty("no critical secrets missing");
        result.LoadedSecrets.Should().Contain("database");
        result.LoadedSecrets.Should().Contain("redis");
        result.LoadedSecrets.Should().Contain("jwt");
    }

    [Fact]
    public void LoadAndValidate_WhenImportantSecretMissing_ShouldBeValidWithWarnings()
    {
        // Arrange
        var configuration = CreateConfiguration(_testSecretsDirectory);
        var loader = new SecretLoader(configuration, _logger);

        // Create only critical secrets
        CreateSecretFile("database.secret", "POSTGRES_USER=testuser", "POSTGRES_PASSWORD=testpass", "POSTGRES_DB=testdb");
        CreateSecretFile("redis.secret", "REDIS_PASSWORD=redispass");
        CreateSecretFile("qdrant.secret", "QDRANT_API_KEY=qdrantkey");
        CreateSecretFile("jwt.secret", "JWT_SECRET_KEY=jwtsecret", "JWT_ISSUER=issuer", "JWT_AUDIENCE=audience");
        CreateSecretFile("admin.secret", "ADMIN_EMAIL=admin@test.com", "ADMIN_PASSWORD=AdminPass123", "ADMIN_DISPLAY_NAME=Admin", "INITIAL_ADMIN_EMAIL=admin@test.com", "INITIAL_ADMIN_DISPLAY_NAME=Admin");
        CreateSecretFile("embedding-service.secret", "EMBEDDING_SERVICE_API_KEY=embedkey");

        // Don't create openrouter.secret (Important level)

        // Act
        var result = loader.LoadAndValidate();

        // Assert
        result.IsValid.Should().BeTrue("critical secrets present, Important can be missing");
        result.HasWarnings.Should().BeTrue("Important secrets are missing");
        result.MissingImportant.Should().Contain(key => key.Contains("openrouter.secret"));
    }

    [Fact]
    public void LoadAndValidate_WhenSecretFileHasComments_ShouldIgnoreComments()
    {
        // Arrange
        var configuration = CreateConfiguration(_testSecretsDirectory);
        var loader = new SecretLoader(configuration, _logger);

        // Create secret file with comments and empty lines
        CreateSecretFile("database.secret",
            "# PostgreSQL credentials",
            "",
            "POSTGRES_USER=testuser",
            "# This is the password",
            "POSTGRES_PASSWORD=testpass",
            "",
            "POSTGRES_DB=testdb"
        );
        CreateSecretFile("redis.secret", "REDIS_PASSWORD=redispass");
        CreateSecretFile("qdrant.secret", "QDRANT_API_KEY=qdrantkey");
        CreateSecretFile("jwt.secret", "JWT_SECRET_KEY=jwtsecret", "JWT_ISSUER=issuer", "JWT_AUDIENCE=audience");
        CreateSecretFile("admin.secret", "ADMIN_EMAIL=admin@test.com", "ADMIN_PASSWORD=AdminPass123", "ADMIN_DISPLAY_NAME=Admin", "INITIAL_ADMIN_EMAIL=admin@test.com", "INITIAL_ADMIN_DISPLAY_NAME=Admin");
        CreateSecretFile("embedding-service.secret", "EMBEDDING_SERVICE_API_KEY=embedkey");

        // Act
        var result = loader.LoadAndValidate();

        // Assert
        result.IsValid.Should().BeTrue("should parse correctly despite comments");
        result.LoadedSecrets.Should().Contain("database");
    }

    [Fact]
    public void LoadAndValidate_WhenSecretFileHasInvalidFormat_ShouldLogWarningAndContinue()
    {
        // Arrange
        var configuration = CreateConfiguration(_testSecretsDirectory);
        var loader = new SecretLoader(configuration, _logger);

        // Create secret file with invalid lines
        CreateSecretFile("database.secret",
            "POSTGRES_USER=testuser",
            "INVALID_LINE_WITHOUT_EQUALS",
            "POSTGRES_PASSWORD=testpass",
            "=VALUE_WITHOUT_KEY",
            "POSTGRES_DB=testdb"
        );
        CreateSecretFile("redis.secret", "REDIS_PASSWORD=redispass");
        CreateSecretFile("qdrant.secret", "QDRANT_API_KEY=qdrantkey");
        CreateSecretFile("jwt.secret", "JWT_SECRET_KEY=jwtsecret", "JWT_ISSUER=issuer", "JWT_AUDIENCE=audience");
        CreateSecretFile("admin.secret", "ADMIN_EMAIL=admin@test.com", "ADMIN_PASSWORD=AdminPass123", "ADMIN_DISPLAY_NAME=Admin", "INITIAL_ADMIN_EMAIL=admin@test.com", "INITIAL_ADMIN_DISPLAY_NAME=Admin");
        CreateSecretFile("embedding-service.secret", "EMBEDDING_SERVICE_API_KEY=embedkey");

        // Act
        var result = loader.LoadAndValidate();

        // Assert - Should still be valid as required keys are present
        result.IsValid.Should().BeTrue("invalid lines are logged but don't fail validation if required keys present");
        result.LoadedSecrets.Should().Contain("database");
    }

    [Fact]
    public void LoadAndValidate_WhenSecretKeyHasWhitespace_ShouldTrimCorrectly()
    {
        // Arrange
        var configuration = CreateConfiguration(_testSecretsDirectory);
        var loader = new SecretLoader(configuration, _logger);

        // Create secret file with whitespace
        CreateSecretFile("database.secret",
            "  POSTGRES_USER  =  testuser  ",
            "POSTGRES_PASSWORD=testpass",
            "POSTGRES_DB=testdb"
        );
        CreateSecretFile("redis.secret", "REDIS_PASSWORD=redispass");
        CreateSecretFile("qdrant.secret", "QDRANT_API_KEY=qdrantkey");
        CreateSecretFile("jwt.secret", "JWT_SECRET_KEY=jwtsecret", "JWT_ISSUER=issuer", "JWT_AUDIENCE=audience");
        CreateSecretFile("admin.secret", "ADMIN_EMAIL=admin@test.com", "ADMIN_PASSWORD=AdminPass123", "ADMIN_DISPLAY_NAME=Admin", "INITIAL_ADMIN_EMAIL=admin@test.com", "INITIAL_ADMIN_DISPLAY_NAME=Admin");
        CreateSecretFile("embedding-service.secret", "EMBEDDING_SERVICE_API_KEY=embedkey");

        // Act
        var result = loader.LoadAndValidate();

        // Assert
        result.IsValid.Should().BeTrue("whitespace should be trimmed correctly");
        result.LoadedSecrets.Should().Contain("database");
    }

    [Fact]
    public void LoadAndValidate_WhenSecretValueIsEmpty_ShouldTreatAsMissing()
    {
        // Arrange
        var configuration = CreateConfiguration(_testSecretsDirectory);
        var loader = new SecretLoader(configuration, _logger);

        // Create secret file with empty value
        CreateSecretFile("database.secret",
            "POSTGRES_USER=testuser",
            "POSTGRES_PASSWORD=",  // Empty value
            "POSTGRES_DB=testdb"
        );
        CreateSecretFile("redis.secret", "REDIS_PASSWORD=redispass");
        CreateSecretFile("qdrant.secret", "QDRANT_API_KEY=qdrantkey");
        CreateSecretFile("jwt.secret", "JWT_SECRET_KEY=jwtsecret", "JWT_ISSUER=issuer", "JWT_AUDIENCE=audience");
        CreateSecretFile("admin.secret", "ADMIN_EMAIL=admin@test.com", "ADMIN_PASSWORD=AdminPass123", "ADMIN_DISPLAY_NAME=Admin", "INITIAL_ADMIN_EMAIL=admin@test.com", "INITIAL_ADMIN_DISPLAY_NAME=Admin");
        CreateSecretFile("embedding-service.secret", "EMBEDDING_SERVICE_API_KEY=embedkey");

        // Act
        var result = loader.LoadAndValidate();

        // Assert
        result.IsValid.Should().BeFalse("empty value should be treated as missing");
        result.MissingCritical.Should().Contain(key => key.Contains("POSTGRES_PASSWORD"));
    }

    [Fact]
    public void LoadAndValidate_ShouldCountTotalProcessedCorrectly()
    {
        // Arrange
        var configuration = CreateConfiguration(_testSecretsDirectory);
        var loader = new SecretLoader(configuration, _logger);

        // Create some secrets
        CreateSecretFile("database.secret", "POSTGRES_USER=testuser", "POSTGRES_PASSWORD=testpass", "POSTGRES_DB=testdb");
        CreateSecretFile("redis.secret", "REDIS_PASSWORD=redispass");
        CreateSecretFile("qdrant.secret", "QDRANT_API_KEY=qdrantkey");
        CreateSecretFile("jwt.secret", "JWT_SECRET_KEY=jwtsecret", "JWT_ISSUER=issuer", "JWT_AUDIENCE=audience");
        CreateSecretFile("admin.secret", "ADMIN_EMAIL=admin@test.com", "ADMIN_PASSWORD=AdminPass123", "ADMIN_DISPLAY_NAME=Admin", "INITIAL_ADMIN_EMAIL=admin@test.com", "INITIAL_ADMIN_DISPLAY_NAME=Admin");
        CreateSecretFile("embedding-service.secret", "EMBEDDING_SERVICE_API_KEY=embedkey");
        // openrouter.secret missing (Important)

        // Act
        var result = loader.LoadAndValidate();

        // Assert
        result.TotalProcessed.Should().BeGreaterThan(0, "should count all processed secrets");
        result.LoadedSecrets.Count.Should().Be(6, "6 secret files loaded");
        result.MissingImportant.Should().NotBeEmpty("openrouter is missing");
    }

    // Helper methods
    private IConfiguration CreateConfiguration(string secretsDirectory)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretsDirectory"] = secretsDirectory
            })
            .Build();
    }

    private void CreateSecretFile(string fileName, params string[] lines)
    {
        var filePath = Path.Combine(_testSecretsDirectory, fileName);
        File.WriteAllLines(filePath, lines);
    }
}
