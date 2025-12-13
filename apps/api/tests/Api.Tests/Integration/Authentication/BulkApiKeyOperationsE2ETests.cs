using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.ApiKeys;
using Api.BoundedContexts.Authentication.Application.Handlers;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// E2E Integration tests for bulk API key operations (Issue #907).
/// Tests complete workflows with Testcontainers: CSV import/export, key generation, security validation.
/// Uses real PostgreSQL container for realistic database interactions.
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. Complete E2E flow: CSV import → key generation → database persistence → CSV export
/// 2. Key security: unique key generation, hash verification, plaintext key returned once
/// 3. CSV parsing: quoted fields, special characters, date formats
/// 4. Error handling: validation errors, duplicate key names, non-existent users
/// 5. Data integrity: expiry dates, metadata JSON, scopes validation
/// 6. Performance: 500 API key bulk operations within acceptable time limits
///
/// Pattern: AAA (Arrange-Act-Assert), Testcontainers for PostgreSQL
/// Execution Time Target: <20s for full suite
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Type", "E2E")]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "907")]
public sealed class BulkApiKeyOperationsE2ETests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IApiKeyRepository? _apiKeyRepository;
    private IUserRepository? _userRepository;
    private IUnitOfWork? _unitOfWork;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public BulkApiKeyOperationsE2ETests()
    {
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing bulk API key operations E2E test infrastructure...");

        // Start isolated Postgres container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "bulk_apikeys_e2e_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);
        var containerPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={containerPort};Database=bulk_apikeys_e2e_test;Username=postgres;Password=postgres;";

        _output($"PostgreSQL started at localhost:{containerPort}");

        // Setup dependency injection
        var enforcedBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        var services = new ServiceCollection();

        // DbContext
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(enforcedBuilder.ConnectionString)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Repositories and Unit of Work
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Logging
        services.AddLogging(builder => builder.AddConsole());

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _apiKeyRepository = serviceProvider.GetRequiredService<IApiKeyRepository>();
        _userRepository = serviceProvider.GetRequiredService<IUserRepository>();
        _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        // Run migrations
        await _dbContext.Database.EnsureDeletedAsync(TestCancellationToken);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        _output("E2E test infrastructure initialized successfully");
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }

        _output("E2E test infrastructure disposed");
    }

    #region E2E Flow: CSV Import → Database → CSV Export

    [Fact]
    public async Task E2E_BulkImport_Then_Export_ShouldRoundTripCorrectly()
    {
        // Arrange: Create test user
        var user = CreateTestUser("apikey@e2etest.com", "API Key User", Role.User);
        await _userRepository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var adminId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{user.Id},Production Key 1,read:games;write:games,{expiresAt},{{""env"":""prod""}}
{user.Id},Development Key 2,read:games,{expiresAt},{{""env"":""dev""}}
{user.Id},Test Key 3,read:games,{expiresAt},null";

        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var importHandler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var importCommand = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act 1: Import
        var importResult = await importHandler.Handle(importCommand, TestCancellationToken);

        // Assert 1: Import success with plaintext keys returned
        importResult.Should().NotBeNull();
        importResult.TotalRequested.Should().Be(3);
        importResult.SuccessCount.Should().Be(3);
        importResult.FailedCount.Should().Be(0);
        importResult.Errors.Should().BeEmpty();
        importResult.Data.Should().HaveCount(3);

        // Verify plaintext keys are returned (security requirement: shown once)
        importResult.Data.Should().AllSatisfy(dto =>
        {
            dto.PlaintextKey.Should().NotBeNullOrWhiteSpace();
            dto.PlaintextKey.Should().StartWith("mpl_");
        });

        // Act 2: Verify database persistence
        var allKeys = await _dbContext!.ApiKeys.Where(k => k.UserId == user.Id).ToListAsync(TestCancellationToken);

        // Assert 2: API keys persisted correctly (hashed, not plaintext)
        allKeys.Should().HaveCount(3);
        allKeys.Should().Contain(k => k.KeyName == "Production Key 1");
        allKeys.Should().Contain(k => k.KeyName == "Development Key 2");
        allKeys.Should().Contain(k => k.KeyName == "Test Key 3");

        // Verify keys are hashed in database
        allKeys.Should().AllSatisfy(key =>
        {
            key.KeyHash.Should().NotBeNullOrWhiteSpace();
            key.KeyHash.Should().NotStartWith("mpl_"); // Hash, not plaintext
        });

        // Act 3: Export
        var exportLogger = new Mock<ILogger<BulkExportApiKeysQueryHandler>>();
        var exportHandler = new BulkExportApiKeysQueryHandler(_apiKeyRepository!, exportLogger.Object);
        var exportQuery = new BulkExportApiKeysQuery(user.Id, null, null);
        var exportResult = await exportHandler.Handle(exportQuery, TestCancellationToken);

        // Assert 3: Export contains all keys (without plaintext keys - security)
        exportResult.Should().NotBeNullOrWhiteSpace();
        var exportLines = exportResult.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        exportLines.Should().HaveCountGreaterThanOrEqualTo(4); // Header + 3 keys
        exportResult.Should().Contain("Production Key 1");
        exportResult.Should().Contain("Development Key 2");
        exportResult.Should().Contain("Test Key 3");

        // Verify plaintext keys NOT exported (security)
        foreach (var dto in importResult.Data)
        {
            exportResult.Should().NotContain(dto.PlaintextKey);
        }
    }

    #endregion

    #region E2E: Key Generation and Security

    [Fact]
    public async Task E2E_BulkImport_ShouldGenerateUniqueKeysForEachImport()
    {
        // Arrange: Create test user
        var user = CreateTestUser("uniquekeys@test.com", "Unique Keys User", Role.User);
        await _userRepository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var adminId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{user.Id},Unique Key 1,read:games,{expiresAt},null
{user.Id},Unique Key 2,read:games,{expiresAt},null
{user.Id},Unique Key 3,read:games,{expiresAt},null";

        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var handler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: All plaintext keys are unique
        result.Data.Should().HaveCount(3);
        var plaintextKeys = result.Data.Select(d => d.PlaintextKey).ToList();
        plaintextKeys.Should().OnlyHaveUniqueItems();

        // Verify all keys follow format: mpl_{env}_{base64}
        plaintextKeys.Should().AllSatisfy(key =>
        {
            key.Should().MatchRegex(@"^mpl_[a-z]+_[A-Za-z0-9+/=]+$");
        });
    }

    [Fact]
    public async Task E2E_ImportedKeys_ShouldBeVerifiableWithPlaintextKey()
    {
        // Arrange: Create test user
        var user = CreateTestUser("verifiable@test.com", "Verifiable User", Role.User);
        await _userRepository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var adminId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{user.Id},Verifiable Key,read:games,{expiresAt},null";

        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var handler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act: Import
        var result = await handler.Handle(command, TestCancellationToken);
        var plaintextKey = result.Data.First().PlaintextKey;

        // Assert: Key can be verified using hash
        var storedKey = await _dbContext!.ApiKeys.FirstAsync(k => k.KeyName == "Verifiable Key", TestCancellationToken);
        var keyBytes = Convert.FromBase64String(plaintextKey);
        var computedHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(keyBytes));
        var isValid = storedKey.KeyHash == computedHash;

        isValid.Should().BeTrue();
    }

    #endregion

    #region E2E: CSV Parsing Edge Cases

    [Fact]
    public async Task E2E_BulkImport_WithQuotedFieldsAndSpecialChars_ShouldParseCorrectly()
    {
        // Arrange: Create test user
        var user = CreateTestUser("specialchars@test.com", "Special Chars User", Role.User);
        await _userRepository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var adminId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{user.Id},""Key with, comma"",read:games,{expiresAt},""{{""""type"""":""""special""""}}""
{user.Id},Key with semicolon,read:games;write:games,{expiresAt},null";

        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var handler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);

        // Verify key names parsed correctly
        var keys = await _dbContext!.ApiKeys.Where(k => k.UserId == user.Id).ToListAsync(TestCancellationToken);
        keys.Should().Contain(k => k.KeyName == "Key with, comma");
        keys.Should().Contain(k => k.KeyName == "Key with semicolon");

        // Verify scopes parsed correctly
        var multiScopeKey = keys.First(k => k.KeyName == "Key with semicolon");
        multiScopeKey.Scopes.Should().Contain("read:games");
        multiScopeKey.Scopes.Should().Contain("write:games");
    }

    [Fact]
    public async Task E2E_BulkImport_WithNullExpiryAndMetadata_ShouldHandleNullValues()
    {
        // Arrange: Create test user
        var user = CreateTestUser("nullvalues@test.com", "Null Values User", Role.User);
        await _userRepository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var adminId = Guid.NewGuid();
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{user.Id},Never Expires Key,read:games,,null
{user.Id},No Metadata Key,read:games,,";

        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var handler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);

        // Verify null handling
        var keys = await _dbContext!.ApiKeys.Where(k => k.UserId == user.Id).ToListAsync(TestCancellationToken);
        keys.Should().AllSatisfy(key =>
        {
            key.ExpiresAt.Should().BeNull();
            key.Metadata.Should().BeNullOrEmpty();
        });
    }

    #endregion

    #region E2E: Error Handling

    [Fact]
    public async Task E2E_BulkImport_WithNonExistentUser_ShouldFail()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{nonExistentUserId},Invalid User Key,read:games,{expiresAt},null";

        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var handler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        exception.Message.Should().Contain("do not exist");
    }

    [Fact]
    public async Task E2E_BulkImport_WithDuplicateKeyNameInDatabase_ShouldFail()
    {
        // Arrange: Create user and existing API key
        var user = CreateTestUser("duplicate@test.com", "Duplicate User", Role.User);
        await _userRepository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var (existingKey, _) = ApiKey.Create(
            id: Guid.NewGuid(),
            userId: user.Id,
            keyName: "Duplicate Key",
            scopes: "read:games",
            expiresAt: DateTime.UtcNow.AddYears(1),
            metadata: null
        );
        await _apiKeyRepository!.AddAsync(existingKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var adminId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{user.Id},Duplicate Key,read:games,{expiresAt},null";

        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var handler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        exception.Message.Should().Contain("already exist");
    }

    [Fact]
    public async Task E2E_BulkImport_WithPastExpiryDate_ShouldSkipInvalidRow()
    {
        // Arrange: Create test user
        var user = CreateTestUser("pastexpiry@test.com", "Past Expiry User", Role.User);
        await _userRepository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var adminId = Guid.NewGuid();
        var pastDate = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd HH:mm:ss");
        var futureDate = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var csvContent = $@"userId,keyName,scopes,expiresAt,metadata
{user.Id},Valid Key,read:games,{futureDate},null
{user.Id},Expired Key,read:games,{pastDate},null";

        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var handler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: Only valid key imported
        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors.First().Should().Contain("must be in the future");
    }

    #endregion

    #region E2E: Performance Test

    [Fact]
    public async Task E2E_BulkImport_With500ApiKeys_ShouldCompleteWithinTimeLimit()
    {
        // Arrange: Create test users
        var users = new List<User>();
        for (int i = 1; i <= 10; i++)
        {
            var user = CreateTestUser($"perfuser{i}@test.com", $"Perf User {i}", Role.User);
            users.Add(user);
            await _userRepository!.AddAsync(user, TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Generate CSV with 500 API keys (50 per user)
        var csvLines = new List<string> { "userId,keyName,scopes,expiresAt,metadata" };
        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        
        foreach (var user in users)
        {
            for (int j = 1; j <= 50; j++)
            {
                csvLines.Add($"{user.Id},Perf Key {j} for {user.Email.Value},read:games,{expiresAt},null");
            }
        }
        var csvContent = string.Join("\n", csvLines);

        var adminId = Guid.NewGuid();
        var logger = new Mock<ILogger<BulkImportApiKeysCommandHandler>>();
        var handler = new BulkImportApiKeysCommandHandler(_apiKeyRepository!, _userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportApiKeysCommand(csvContent, adminId);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await handler.Handle(command, TestCancellationToken);
        stopwatch.Stop();

        // Assert: Performance
        result.SuccessCount.Should().Be(500);
        result.FailedCount.Should().Be(0);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // <10s for 500 keys

        // Verify database
        var keyCount = await _dbContext!.ApiKeys.CountAsync(TestCancellationToken);
        keyCount.Should().Be(500);

        _output($"500 API keys imported in {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region E2E: Export Filtering

    [Fact]
    public async Task E2E_BulkExport_WithIsActiveFilter_ShouldOnlyExportActiveKeys()
    {
        // Arrange: Create user and API keys
        var user = CreateTestUser("activefilter@test.com", "Active Filter User", Role.User);
        await _userRepository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var (activeKey, _) = ApiKey.Create(Guid.NewGuid(), user.Id, "Active Key", "read:games", DateTime.UtcNow.AddYears(1), null);
        var (expiredKey, _) = ApiKey.Create(Guid.NewGuid(), user.Id, "Expired Key", "read:games", DateTime.UtcNow.AddDays(-1), null);

        await _apiKeyRepository!.AddAsync(activeKey, TestCancellationToken);
        await _apiKeyRepository!.AddAsync(expiredKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var logger = new Mock<ILogger<BulkExportApiKeysQueryHandler>>();
        var handler = new BulkExportApiKeysQueryHandler(_apiKeyRepository!, logger.Object);
        var query = new BulkExportApiKeysQuery(user.Id, true, null);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().Contain("Active Key");
        result.Should().NotContain("Expired Key");

        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2); // Header + 1 active key
    }

    #endregion

    #region Helper Methods

    private User CreateTestUser(string email, string displayName, Role role)
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("TempPassword123!"),
            role: role
        );
    }

    #endregion
}
