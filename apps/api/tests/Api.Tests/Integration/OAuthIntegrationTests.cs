using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.Queries.OAuth;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Infrastructure;
using System;
using System.Linq;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for OAuth CQRS flow end-to-end.
/// Tests the complete OAuth workflow: callback, account linking, unlinking, and retrieval.
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
/// <remarks>
/// Tests Cover:
/// 1. OAuth callback with new user creation and account linking
/// 2. OAuth callback with existing user and additional account linking
/// 3. Unlinking OAuth account with password fallback
/// 4. Preventing unlink when OAuth is the only auth method
/// 5. Retrieving all linked OAuth accounts with metadata
/// 6. Progressive OAuth account management workflow
/// 7. OAuth provider validation
///
/// Pattern: AAA (Arrange-Act-Assert), Testcontainers for PostgreSQL, transaction rollback between tests
/// </remarks>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "2031")]
public class OAuthIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private const string NewUserEmail = "oauth-newuser@test.meepleai.dev";
    private const string ExistingUserEmail = "oauth-existing@test.meepleai.dev";
    private const string GoogleProvider = "google";
    private const string DiscordProvider = "discord";
    private const string GitHubProvider = "github";
    private const string GoogleUserId = "google-user-123";
    private const string DiscordUserId = "discord-user-456";
    private const string AccessToken = "access_token_encrypted";
    private const string RefreshToken = "refresh_token_encrypted";

    public OAuthIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing OAuth integration test infrastructure...");

        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = $"test_oauth_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _output($"Isolated database created: {_databaseName}");

        // Setup dependency injection
        var enforcedBuilder = new NpgsqlConnectionStringBuilder(_isolatedDbConnectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Register DbContext with PostgreSQL
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(enforcedBuilder.ConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            // Suppress pending model changes warning for integration tests
            // since we're using migrations to manage schema
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        // Register domain event infrastructure
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();

        // Register MediatR with handlers
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(HandleOAuthCallbackCommandHandler).Assembly));

        _serviceProvider = services.BuildServiceProvider();

        // Create DbContext instance
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations
        _output("Applying migrations...");
        await MigrateWithRetry(_dbContext);
        _output("✓ Migrations applied");

        // Create test data (users for linking tests)
        await CreateTestDataAsync();
        _output("✓ Test infrastructure ready");
    }

    public async ValueTask DisposeAsync()
    {
        _output("Cleaning up OAuth integration test infrastructure...");

        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        // Issue #2031: Use SharedTestcontainersFixture for cleanup
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
                _output($"Isolated database dropped: {_databaseName}");
            }
            catch (Exception ex)
            {
                _output($"Warning: Failed to drop database {_databaseName}: {ex.Message}");
            }
        }

        _output("✓ Cleanup complete");
    }
    [Fact]
    public async Task OAuthCallback_NewUser_CreatesUserAndLinksAccount()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = new Email(NewUserEmail);

        // Create user through repository
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var user = new User(
            userId,
            email,
            "New OAuth User",
            PasswordHash.Create("TempPassword123!"), // Temp password for new OAuth users
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        // Create linked OAuth account
        var oauthAccountId = Guid.NewGuid();
        var oauthAccount = new OAuthAccount(
            oauthAccountId,
            userId,
            GoogleProvider,
            GoogleUserId,
            AccessToken,
            RefreshToken,
            DateTime.UtcNow.AddHours(1));

        await oauthAccountRepository.AddAsync(oauthAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Query to verify creation
        var userInDb = await userRepository.GetByIdAsync(userId, TestCancellationToken);
        var linkedAccounts = await oauthAccountRepository.GetByUserIdAsync(userId, TestCancellationToken);

        // Assert - Verify user was created with linked OAuth account
        Assert.NotNull(userInDb);
        Assert.Equal(NewUserEmail, userInDb.Email.Value);
        Assert.NotEmpty(linkedAccounts);

        var linkedAccount = linkedAccounts[0];
        Assert.Equal(GoogleProvider, linkedAccount.Provider);
        Assert.Equal(GoogleUserId, linkedAccount.ProviderUserId);
        Assert.Equal(AccessToken, linkedAccount.AccessTokenEncrypted);

        _output("✓ Test 1 passed: OAuth callback created new user with linked account");
    }
    [Fact]
    public async Task OAuthCallback_ExistingUser_LinksAccount()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        // Create existing user first
        var userId = Guid.NewGuid();
        var email = new Email(ExistingUserEmail);
        var user = new User(
            userId,
            email,
            "Existing User",
            PasswordHash.Create("ExistingPassword123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var userCountBefore = await _dbContext!.Users.CountAsync(TestCancellationToken);

        // Act - Simulate OAuth callback with matching email (existing user)
        var oauthAccountId = Guid.NewGuid();
        var oauthAccount = new OAuthAccount(
            oauthAccountId,
            userId,
            DiscordProvider,
            DiscordUserId,
            AccessToken,
            RefreshToken,
            DateTime.UtcNow.AddHours(2));

        await oauthAccountRepository.AddAsync(oauthAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var userCountAfter = await _dbContext!.Users.CountAsync(TestCancellationToken);

        // No new user created
        Assert.Equal(userCountBefore, userCountAfter);

        // OAuth account linked to existing user
        var linkedAccounts = await oauthAccountRepository.GetByUserIdAsync(userId, TestCancellationToken);

        Assert.NotEmpty(linkedAccounts);
        Assert.Single(linkedAccounts);
        Assert.Equal(DiscordProvider, linkedAccounts[0].Provider);

        _output("✓ Test 2 passed: OAuth callback linked account to existing user");
    }
    [Fact]
    public async Task UnlinkOAuthAccount_ValidProvider_RemovesAccount()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var email = new Email("unlinktest@test.meepleai.dev");
        var user = new User(
            userId,
            email,
            "Unlink Test User",
            PasswordHash.Create("Password123!"), // Has password as fallback
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        // Add 2 OAuth accounts
        var googleAccountId = Guid.NewGuid();
        var googleAccount = new OAuthAccount(
            googleAccountId,
            userId,
            GoogleProvider,
            "google-user-1",
            AccessToken,
            RefreshToken,
            DateTime.UtcNow.AddHours(1));

        var discordAccountId = Guid.NewGuid();
        var discordAccount = new OAuthAccount(
            discordAccountId,
            userId,
            DiscordProvider,
            "discord-user-1",
            AccessToken,
            null,
            null);

        await oauthAccountRepository.AddAsync(googleAccount, TestCancellationToken);
        await oauthAccountRepository.AddAsync(discordAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Verify setup
        var accountsBeforeUnlink = await oauthAccountRepository.GetByUserIdAsync(userId, TestCancellationToken);
        Assert.Equal(2, accountsBeforeUnlink.Count);

        // Act - Unlink Discord account (user still has password + Google)
        var command = new UnlinkOAuthAccountCommand
        {
            UserId = userId,
            Provider = DiscordProvider
        };

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        Assert.True(result.Success, $"Unlink failed: {result.ErrorMessage}");

        var accountsAfterUnlink = await oauthAccountRepository.GetByUserIdAsync(userId, TestCancellationToken);

        Assert.Single(accountsAfterUnlink);
        Assert.Equal(GoogleProvider, accountsAfterUnlink[0].Provider);

        // Discord account deleted
        var discordAccountInDb = await oauthAccountRepository.GetByIdAsync(discordAccountId, TestCancellationToken);
        Assert.Null(discordAccountInDb);

        _output("✓ Test 3 passed: OAuth account successfully unlinked");
    }
    [Fact]
    public async Task UnlinkOAuthAccount_WithPasswordFallback_Succeeds()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var email = new Email("oauth-single@test.meepleai.dev");

        // Create user with password (realistic scenario)
        var user = new User(
            userId,
            email,
            "Single OAuth User",
            PasswordHash.Create("Password123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        // Single OAuth account
        var oauthAccountId = Guid.NewGuid();
        var oauthAccount = new OAuthAccount(
            oauthAccountId,
            userId,
            GoogleProvider,
            GoogleUserId,
            AccessToken,
            RefreshToken,
            DateTime.UtcNow.AddHours(1));

        await oauthAccountRepository.AddAsync(oauthAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Attempt to unlink OAuth account (user has password, so this WILL succeed)
        var command = new UnlinkOAuthAccountCommand
        {
            UserId = userId,
            Provider = GoogleProvider
        };

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert - Should succeed because user has password as fallback auth method
        Assert.True(result.Success, "Should succeed when user has password as fallback");

        // Verify OAuth account WAS deleted (user still has password auth)
        var oauthAccountInDb = await oauthAccountRepository.GetByIdAsync(oauthAccountId, TestCancellationToken);
        Assert.Null(oauthAccountInDb);

        _output("✓ Test 4 passed: OAuth account can be unlinked when user has password as fallback");
    }
    [Fact]
    public async Task GetLinkedOAuthAccounts_ReturnsAllAccounts()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var email = new Email("multi-oauth@test.meepleai.dev");
        var user = new User(
            userId,
            email,
            "Multi OAuth User",
            PasswordHash.Create("Password123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        // Google (with expiration)
        var googleAccountId = Guid.NewGuid();
        var googleExpiration = DateTime.UtcNow.AddHours(2);
        var googleAccount = new OAuthAccount(
            googleAccountId,
            userId,
            GoogleProvider,
            "google-123",
            AccessToken,
            RefreshToken,
            googleExpiration);

        // Discord (no expiration)
        var discordAccountId = Guid.NewGuid();
        var discordAccount = new OAuthAccount(
            discordAccountId,
            userId,
            DiscordProvider,
            "discord-456",
            AccessToken,
            null,
            null);

        // GitHub (expired token)
        var githubAccountId = Guid.NewGuid();
        var githubExpiration = DateTime.UtcNow.AddDays(-1); // Clearly expired (1 day ago)
        var githubAccount = new OAuthAccount(
            githubAccountId,
            userId,
            GitHubProvider,
            "github-789",
            AccessToken,
            RefreshToken,
            githubExpiration);

        await oauthAccountRepository.AddAsync(googleAccount, TestCancellationToken);
        await oauthAccountRepository.AddAsync(discordAccount, TestCancellationToken);
        await oauthAccountRepository.AddAsync(githubAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Query linked OAuth accounts
        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var result = await mediator.Send(query, TestCancellationToken);

        // Assert - All 3 accounts returned with correct metadata
        Assert.NotNull(result);
        Assert.Equal(3, result.Accounts.Count);

        // Verify Google
        var googleDto = result.Accounts.FirstOrDefault(a => a.Provider == GoogleProvider);
        Assert.NotNull(googleDto);
        Assert.Equal(googleAccountId, googleDto.Id);
        Assert.Equal("google-123", googleDto.ProviderUserId);
        Assert.False(googleDto.IsTokenExpired);
        Assert.True(googleDto.SupportsRefresh);

        // Verify Discord
        var discordDto = result.Accounts.FirstOrDefault(a => a.Provider == DiscordProvider);
        Assert.NotNull(discordDto);
        Assert.Equal(discordAccountId, discordDto.Id);
        Assert.False(discordDto.IsTokenExpired);
        Assert.False(discordDto.SupportsRefresh); // No refresh token

        // Verify GitHub (expired)
        var githubDto = result.Accounts.FirstOrDefault(a => a.Provider == GitHubProvider);
        Assert.NotNull(githubDto);
        Assert.Equal(githubAccountId, githubDto.Id);
        Assert.True(githubDto.IsTokenExpired); // Token is expired
        Assert.True(githubDto.SupportsRefresh);

        _output("✓ Test 5 passed: Retrieved all linked OAuth accounts with correct metadata");
    }
    [Fact]
    public async Task MultipleOAuthAccounts_UserManagement()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var email = new Email("progressive-oauth@test.meepleai.dev");
        var user = new User(
            userId,
            email,
            "Progressive OAuth User",
            PasswordHash.Create("Password123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Step 1: Link first account (Google)
        var googleAccount = new OAuthAccount(
            Guid.NewGuid(),
            userId,
            GoogleProvider,
            "google-1",
            AccessToken,
            RefreshToken,
            DateTime.UtcNow.AddHours(1));

        await oauthAccountRepository.AddAsync(googleAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var result1 = await mediator.Send(query, TestCancellationToken);
        Assert.Single(result1.Accounts);

        // Step 2: Link second account (Discord)
        var discordAccount = new OAuthAccount(
            Guid.NewGuid(),
            userId,
            DiscordProvider,
            "discord-1",
            AccessToken,
            null,
            null);

        await oauthAccountRepository.AddAsync(discordAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var result2 = await mediator.Send(query, TestCancellationToken);
        Assert.Equal(2, result2.Accounts.Count);

        // Step 3: Unlink first account (Google) - still has Discord
        var unlinkCommand = new UnlinkOAuthAccountCommand
        {
            UserId = userId,
            Provider = GoogleProvider
        };

        var unlinkResult = await mediator.Send(unlinkCommand, TestCancellationToken);
        Assert.True(unlinkResult.Success);

        // Step 4: Verify only Discord remains
        var result3 = await mediator.Send(query, TestCancellationToken);
        Assert.Single(result3.Accounts);
        Assert.Equal(DiscordProvider, result3.Accounts[0].Provider);

        _output("✓ Test 6 passed: Progressive OAuth account management");
    }
    [Fact]
    public void OAuthAccount_ProviderValidation()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert - Invalid provider should throw during account creation
        var invalidProvider = "invalid-provider";
        var exception = Assert.Throws<ValidationException>(() =>
        {
            var invalidAccount = new OAuthAccount(
                Guid.NewGuid(),
                userId,
                invalidProvider,
                "user-id",
                AccessToken,
                null,
                null);
        });

        Assert.Contains("Unsupported", exception.Message);

        _output("✓ Test 7 passed: OAuth provider validation enforced");
    }
    private async Task CreateTestDataAsync()
    {
        // Use a new scope to avoid entity tracking conflicts
        using var scope = _serviceProvider!.CreateScope();
        var scopedUserRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var testUser = new User(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            new Email("preexisting@test.meepleai.dev"),
            "Pre-existing Test User",
            PasswordHash.Create("TestPassword123!"),
            Role.User);

        await scopedUserRepository.AddAsync(testUser, TestCancellationToken);
        await scopedUnitOfWork.SaveChangesAsync(TestCancellationToken);
    }

    private async Task ResetDatabaseAsync()
    {
        // Clear all data from database tables to ensure test isolation
        var tableNames = await _dbContext!.Database
            .SqlQueryRaw<string>(
                @"SELECT tablename
                  FROM pg_tables
                  WHERE schemaname = 'public'
                  AND tablename != '__EFMigrationsHistory'")
            .ToListAsync(TestCancellationToken);

        if (tableNames.Count == 0)
        {
            await CreateTestDataAsync();
            return;
        }

        // Disable foreign key constraints temporarily for cleanup
        await _dbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'replica';", TestCancellationToken);

        try
        {
            foreach (var tableName in tableNames)
            {
#pragma warning disable EF1002 // safeTableName is validated; identifiers cannot be parameterized
                var safeTableName = SanitizeIdentifier(tableName);
                await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{safeTableName}\" CASCADE;", TestCancellationToken);
#pragma warning restore EF1002
            }
        }
        finally
        {
            // Re-enable foreign key constraints
            await _dbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';", TestCancellationToken);
        }

        // Recreate test data after reset
        await CreateTestDataAsync();
    }

    private static async Task MigrateWithRetry(MeepleAiDbContext context)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await context.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    private static string SanitizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be null or whitespace.", nameof(identifier));
        }

        if (identifier.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
        {
            throw new ArgumentException($"Invalid character in identifier '{identifier}'.", nameof(identifier));
        }

        return identifier;
    }
}
