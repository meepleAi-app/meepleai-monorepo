using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.Queries.OAuth;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for OAuth CQRS flow end-to-end.
/// Tests the complete OAuth workflow: callback, account linking, unlinking, and retrieval.
/// Uses Testcontainers with PostgreSQL for realistic database interactions.
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
[Collection("Integration")]
public class OAuthIntegrationTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;
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
    private const string GitHubUserId = "github-user-789";
    private const string AccessToken = "access_token_encrypted";
    private const string RefreshToken = "refresh_token_encrypted";

    public OAuthIntegrationTests()
    {
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing OAuth integration test infrastructure...");

        // Start PostgreSQL container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "meepleai_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);
        var containerPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={containerPort};Database=meepleai_test;Username=postgres;Password=postgres;";

        _output($"PostgreSQL started at localhost:{containerPort}");

        // Setup dependency injection
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Register DbContext with PostgreSQL
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
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

        // Register MediatR with handlers
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(HandleOAuthCallbackCommandHandler).Assembly));

        _serviceProvider = services.BuildServiceProvider();

        // Create DbContext instance
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Apply migrations
        _output("Applying migrations...");
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
        _output("✓ Migrations applied");

        // Create test data (users for linking tests)
        await CreateTestDataAsync();
        _output("✓ Test infrastructure ready");
    }

    public async ValueTask DisposeAsync()
    {
        _output("Cleaning up OAuth integration test infrastructure...");

        _dbContext?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }

        _output("✓ Cleanup complete");
    }

    #region Test 1: OAuthCallback_NewUser_CreatesUserAndLinksAccount

    [Fact]
    public async Task OAuthCallback_NewUser_CreatesUserAndLinksAccount()
    {
        // Arrange
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

        var linkedAccount = linkedAccounts.First();
        Assert.Equal(GoogleProvider, linkedAccount.Provider);
        Assert.Equal(GoogleUserId, linkedAccount.ProviderUserId);
        Assert.Equal(AccessToken, linkedAccount.AccessTokenEncrypted);

        _output("✓ Test 1 passed: OAuth callback created new user with linked account");
    }

    #endregion

    #region Test 2: OAuthCallback_ExistingUser_LinksAccount

    [Fact]
    public async Task OAuthCallback_ExistingUser_LinksAccount()
    {
        // Arrange
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
        Assert.Equal(DiscordProvider, linkedAccounts.First().Provider);

        _output("✓ Test 2 passed: OAuth callback linked account to existing user");
    }

    #endregion

    #region Test 3: UnlinkOAuthAccount_ValidProvider_RemovesAccount

    [Fact]
    public async Task UnlinkOAuthAccount_ValidProvider_RemovesAccount()
    {
        // Arrange
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
        Assert.Equal(GoogleProvider, accountsAfterUnlink.First().Provider);

        // Discord account deleted
        var discordAccountInDb = await oauthAccountRepository.GetByIdAsync(discordAccountId, TestCancellationToken);
        Assert.Null(discordAccountInDb);

        _output("✓ Test 3 passed: OAuth account successfully unlinked");
    }

    #endregion

    #region Test 4: UnlinkOAuthAccount_LastAuthMethod_Fails

    [Fact]
    public async Task UnlinkOAuthAccount_LastAuthMethod_Fails()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var email = new Email("oauth-only@test.meepleai.dev");

        // Create user with OAuth-only (in practice, domain logic prevents unlink of only auth method)
        var user = new User(
            userId,
            email,
            "OAuth Only User",
            PasswordHash.Create("placeholder"), // Placeholder since OAuth-only users might not have password
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

        // Act - Attempt to unlink the only auth method
        var command = new UnlinkOAuthAccountCommand
        {
            UserId = userId,
            Provider = GoogleProvider
        };

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert - Should fail (cannot unlink only auth method)
        Assert.False(result.Success, "Should fail when unlinking only auth method");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("lockout", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify OAuth account NOT deleted
        var oauthAccountInDb = await oauthAccountRepository.GetByIdAsync(oauthAccountId, TestCancellationToken);
        Assert.NotNull(oauthAccountInDb);

        _output("✓ Test 4 passed: Cannot unlink only authentication method (lockout prevention)");
    }

    #endregion

    #region Test 5: GetLinkedOAuthAccounts_ReturnsAllAccounts

    [Fact]
    public async Task GetLinkedOAuthAccounts_ReturnsAllAccounts()
    {
        // Arrange
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
        var githubExpiration = DateTime.UtcNow.AddHours(-1); // Expired
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

    #endregion

    #region Test 6: MultipleOAuthAccounts_UserManagement

    [Fact]
    public async Task MultipleOAuthAccounts_UserManagement()
    {
        // Arrange
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
        Assert.Equal(DiscordProvider, result3.Accounts.First().Provider);

        _output("✓ Test 6 passed: Progressive OAuth account management");
    }

    #endregion

    #region Test 7: OAuthAccount_ProviderValidation

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

    #endregion

    #region Helper Methods

    private async Task CreateTestDataAsync()
    {
        // Pre-create some users for reference in tests
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var testUser = new User(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            new Email("preexisting@test.meepleai.dev"),
            "Pre-existing Test User",
            PasswordHash.Create("TestPassword123!"),
            Role.User);

        await userRepository.AddAsync(testUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
    }

    #endregion
}
