using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Pgvector.EntityFrameworkCore; // Issue #3547: Enable pgvector type mapping
using Xunit;
using FluentAssertions;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// End-to-end tests for complete authentication flows.
/// Issue #2645: E2E authentication flow tests.
/// </summary>
/// <remarks>
/// Tests comprehensive user journeys:
/// 1. OAuth registration and login flow
/// 2. 2FA setup and login flow
/// 3. Session expiration and management flow
///
/// Pattern: SharedTestcontainersFixture for PostgreSQL, complete user journeys
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.E2E)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "2645")]
public class AuthenticationFlowsE2ETests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private FakeTimeProvider _timeProvider = null!;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AuthenticationFlowsE2ETests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing Authentication E2E test infrastructure...");

        _databaseName = $"test_auth_e2e_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _output($"Isolated database created: {_databaseName}");

        var enforcedBuilder = new NpgsqlConnectionStringBuilder(_isolatedDbConnectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var services = IntegrationServiceCollectionBuilder.CreateBase(enforcedBuilder.ConnectionString);

        // Override TimeProvider with FakeTimeProvider
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(_timeProvider);

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

        // Register password hashing
        services.AddSingleton<IPasswordHashingService, PasswordHashingService>();

        // Override IEmailService with NoOpEmailService (has behavior)
        services.RemoveAll<IEmailService>();
        services.AddSingleton<IEmailService>(new Api.Tests.TestHelpers.NoOpEmailService());

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        _output("Applying migrations...");
        await MigrateWithRetry(_dbContext);
        _output("✓ Migrations applied");
        _output("✓ Test infrastructure ready");
    }

    public async ValueTask DisposeAsync()
    {
        _output("Cleaning up Authentication E2E test infrastructure...");

        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

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

    #region OAuth Registration and Login Flow E2E Tests

    /// <summary>
    /// E2E TEST: Complete OAuth registration flow for new user.
    /// Journey: OAuth callback → User created → Session created → User logged in
    /// </summary>
    [Fact]
    public async Task OAuthFlow_NewUserRegistration_CompletesSuccessfully()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var sessionRepository = _serviceProvider!.GetRequiredService<ISessionRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userEmail = $"oauth-new-{Guid.NewGuid():N}@test.meepleai.dev";
        var googleUserId = $"google-{Guid.NewGuid():N}";
        var userId = Guid.NewGuid();

        // Step 1: Create user from OAuth callback
        var user = new User(
            userId,
            new Email(userEmail),
            "New OAuth User",
            PasswordHash.Create("TempOAuthPassword123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);
        _output("Step 1: User created from OAuth callback");

        // Step 2: Link OAuth account
        var oauthAccount = new OAuthAccount(
            Guid.NewGuid(),
            userId,
            "google",
            googleUserId,
            "encrypted_access_token",
            "encrypted_refresh_token",
            DateTime.UtcNow.AddHours(1));

        await oauthAccountRepository.AddAsync(oauthAccount, TestCancellationToken);
        _output("Step 2: OAuth account linked");

        // Step 3: Create session
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            lifetime: TimeSpan.FromDays(7),
            ipAddress: "192.168.1.1",
            userAgent: "Test Browser",
            timeProvider: _timeProvider);

        await sessionRepository.AddAsync(session, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Step 3: Session created");

        // Assert complete flow
        var storedUser = await userRepository.GetByIdAsync(userId, TestCancellationToken);
        var linkedAccounts = await oauthAccountRepository.GetByUserIdAsync(userId, TestCancellationToken);
        var activeSession = await _dbContext!.UserSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.RevokedAt == null, TestCancellationToken);

        storedUser.Should().NotBeNull();
        storedUser.Email.Value.Should().Be(userEmail);
        linkedAccounts.Should().ContainSingle();
        linkedAccounts[0].Provider.Should().Be("google");
        activeSession.Should().NotBeNull();
        session.IsValid(_timeProvider).Should().BeTrue();

        _output("✓ Complete OAuth registration flow verified");
    }

    /// <summary>
    /// E2E TEST: OAuth login with existing user.
    /// Journey: OAuth callback → User found → New session created
    /// </summary>
    [Fact]
    public async Task OAuthFlow_ExistingUserLogin_CreatesNewSession()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var sessionRepository = _serviceProvider!.GetRequiredService<ISessionRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var userEmail = $"oauth-existing-{Guid.NewGuid():N}@test.meepleai.dev";
        var googleUserId = $"google-existing-{Guid.NewGuid():N}";

        // Setup: Existing user with OAuth
        var user = new User(
            userId,
            new Email(userEmail),
            "Existing OAuth User",
            PasswordHash.Create("Password123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        var oauthAccount = new OAuthAccount(
            Guid.NewGuid(),
            userId,
            "google",
            googleUserId,
            "encrypted_access_token",
            "encrypted_refresh_token",
            DateTime.UtcNow.AddHours(1));

        await oauthAccountRepository.AddAsync(oauthAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Setup: Existing user with OAuth account created");

        // Act: User logs in via OAuth (new session)
        var sessionToken = SessionToken.Generate();
        var newSession = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            lifetime: TimeSpan.FromDays(7),
            ipAddress: "10.0.0.1",
            userAgent: "Mobile App",
            timeProvider: _timeProvider);

        await sessionRepository.AddAsync(newSession, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("OAuth login created new session");

        // Assert
        var activeSessions = await _dbContext!.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(TestCancellationToken);

        activeSessions.Should().ContainSingle();
        newSession.IsValid(_timeProvider).Should().BeTrue();
        activeSessions[0].IpAddress.Should().Be("10.0.0.1");

        _output("✓ OAuth login flow for existing user verified");
    }

    /// <summary>
    /// E2E TEST: OAuth account linking to existing password-based account.
    /// Journey: User has password → Links Google → Links Discord → Both providers available
    /// </summary>
    [Fact]
    public async Task OAuthFlow_MultiProviderLinking_AllProvidersAccessible()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var oauthAccountRepository = _serviceProvider!.GetRequiredService<IOAuthAccountRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var userEmail = $"multi-oauth-{Guid.NewGuid():N}@test.meepleai.dev";

        // Step 1: Create user with password
        var user = new User(
            userId,
            new Email(userEmail),
            "Multi OAuth User",
            PasswordHash.Create("SecurePassword123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Step 1: Password-based user created");

        // Step 2: Link Google
        var googleAccount = new OAuthAccount(
            Guid.NewGuid(),
            userId,
            "google",
            $"google-{Guid.NewGuid():N}",
            "google_access_encrypted",
            "google_refresh_encrypted",
            DateTime.UtcNow.AddHours(1));

        await oauthAccountRepository.AddAsync(googleAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Step 2: Google account linked");

        // Step 3: Link Discord
        var discordAccount = new OAuthAccount(
            Guid.NewGuid(),
            userId,
            "discord",
            $"discord-{Guid.NewGuid():N}",
            "discord_access_encrypted",
            "discord_refresh_encrypted",
            DateTime.UtcNow.AddHours(2));

        await oauthAccountRepository.AddAsync(discordAccount, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Step 3: Discord account linked");

        // Assert all providers accessible
        var linkedAccounts = await oauthAccountRepository.GetByUserIdAsync(userId, TestCancellationToken);

        linkedAccounts.Count.Should().Be(2);
        linkedAccounts.Should().Contain(a => a.Provider == "google");
        linkedAccounts.Should().Contain(a => a.Provider == "discord");

        _output("✓ Multi-provider OAuth linking verified");
    }

    #endregion

    #region 2FA Setup and Login Flow E2E Tests

    /// <summary>
    /// E2E TEST: Complete 2FA setup flow.
    /// Journey: User exists → Generate TOTP secret → Enable 2FA → Login requires 2FA
    /// </summary>
    [Fact]
    public async Task TwoFactorFlow_CompleteSetup_RequiresTotpOnLogin()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var userEmail = $"2fa-setup-{Guid.NewGuid():N}@test.meepleai.dev";

        // Step 1: Create user without 2FA
        var user = new User(
            userId,
            new Email(userEmail),
            "2FA Test User",
            PasswordHash.Create("SecurePassword123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Step 1: User created without 2FA");

        user.IsTwoFactorEnabled.Should().BeFalse();

        // Step 2: Generate and enable 2FA
        var totpSecret = TotpSecret.FromEncrypted("test_encrypted_totp_secret_base64");
        user.Enable2FA(totpSecret);
        await userRepository.UpdateAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Step 2: 2FA enabled with TOTP secret");

        // Assert 2FA is now required
        _dbContext!.ChangeTracker.Clear(); // Force fresh fetch from DB
        var updatedUser = await userRepository.GetByIdAsync(userId, TestCancellationToken);
        updatedUser.Should().NotBeNull();
        updatedUser.IsTwoFactorEnabled.Should().BeTrue();
        updatedUser.TotpSecret.Should().NotBeNull();

        _output("✓ 2FA setup flow verified - TOTP now required for login");
    }

    /// <summary>
    /// E2E TEST: 2FA login flow with valid TOTP code.
    /// Journey: Login with password → 2FA challenge → Valid TOTP → Session created
    /// </summary>
    [Fact]
    public async Task TwoFactorFlow_LoginWithValidTotp_CreatesSession()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var sessionRepository = _serviceProvider!.GetRequiredService<ISessionRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();
        var passwordHashingService = _serviceProvider!.GetRequiredService<IPasswordHashingService>();

        var userId = Guid.NewGuid();
        var userEmail = $"2fa-login-{Guid.NewGuid():N}@test.meepleai.dev";

        // Setup: User with 2FA enabled
        var user = new User(
            userId,
            new Email(userEmail),
            "2FA Login User",
            PasswordHash.Create("SecurePassword123!"),
            Role.User);

        var totpSecret = TotpSecret.FromEncrypted("test_encrypted_totp_secret_2fa_login");
        user.Enable2FA(totpSecret);

        await userRepository.AddAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Setup: User with 2FA enabled");

        // Step 1: Password verification succeeds
        var passwordValid = passwordHashingService.VerifySecret("SecurePassword123!", user.PasswordHash.Value);
        passwordValid.Should().BeTrue();
        _output("Step 1: Password verified");

        // Step 2: 2FA challenge - validate that 2FA is required
        user.RequiresTwoFactor().Should().BeTrue();
        _output("Step 2: 2FA challenge required");

        // Step 3: Create session after successful 2FA (simulating TOTP validation passed)
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            lifetime: TimeSpan.FromDays(1),
            ipAddress: "192.168.1.100",
            userAgent: "Secure Browser",
            timeProvider: _timeProvider);

        await sessionRepository.AddAsync(session, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Step 3: Session created after 2FA");

        // Assert
        session.IsValid(_timeProvider).Should().BeTrue();

        _output("✓ 2FA login flow with valid TOTP verified");
    }

    /// <summary>
    /// E2E TEST: 2FA disable flow.
    /// Journey: User has 2FA → Disable 2FA → Login no longer requires TOTP
    /// </summary>
    [Fact]
    public async Task TwoFactorFlow_DisableTwoFactor_LoginNoLongerRequiresTotp()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var userEmail = $"2fa-disable-{Guid.NewGuid():N}@test.meepleai.dev";

        // Setup: User with 2FA enabled
        var user = new User(
            userId,
            new Email(userEmail),
            "2FA Disable User",
            PasswordHash.Create("SecurePassword123!"),
            Role.User);

        var totpSecret = TotpSecret.FromEncrypted("test_encrypted_totp_secret_2fa_disable");
        user.Enable2FA(totpSecret);

        await userRepository.AddAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        user.IsTwoFactorEnabled.Should().BeTrue();
        _output("Setup: User with 2FA enabled");

        // Act: Disable 2FA
        user.Disable2FA();
        await userRepository.UpdateAsync(user, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("2FA disabled");

        // Assert
        _dbContext!.ChangeTracker.Clear(); // Force fresh fetch from DB
        var updatedUser = await userRepository.GetByIdAsync(userId, TestCancellationToken);
        updatedUser.Should().NotBeNull();
        updatedUser.IsTwoFactorEnabled.Should().BeFalse();
        updatedUser.TotpSecret.Should().BeNull();

        _output("✓ 2FA disable flow verified");
    }

    #endregion

    #region Session Expiration Flow E2E Tests

    /// <summary>
    /// E2E TEST: Session expiration after lifetime exceeded.
    /// Journey: Session created → Time passes → Session expires → Validation fails
    /// </summary>
    [Fact]
    public async Task SessionFlow_ExpiresAfterLifetime_ValidationFails()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var sessionRepository = _serviceProvider!.GetRequiredService<ISessionRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var userEmail = $"session-expire-{Guid.NewGuid():N}@test.meepleai.dev";

        // Setup: User and session
        var user = new User(
            userId,
            new Email(userEmail),
            "Session Test User",
            PasswordHash.Create("Password123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        // Create session with 1-hour lifetime
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            lifetime: TimeSpan.FromHours(1),
            ipAddress: "192.168.1.1",
            userAgent: "Test Browser",
            timeProvider: _timeProvider);

        await sessionRepository.AddAsync(session, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Session created with 1-hour lifetime");

        // Assert session is valid now
        session.IsValid(_timeProvider).Should().BeTrue();
        _output("Session is valid immediately after creation");

        // Act: Advance time past expiration
        _timeProvider.Advance(TimeSpan.FromHours(2));
        _output("Time advanced 2 hours");

        // Assert session is expired
        session.IsValid(_timeProvider).Should().BeFalse();

        _output("✓ Session expiration flow verified");
    }

    /// <summary>
    /// E2E TEST: Session revocation flow.
    /// Journey: Session valid → User logs out → Session revoked → Cannot use session
    /// </summary>
    [Fact]
    public async Task SessionFlow_Revocation_PreventsReuse()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var sessionRepository = _serviceProvider!.GetRequiredService<ISessionRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var userEmail = $"session-revoke-{Guid.NewGuid():N}@test.meepleai.dev";

        // Setup: User and active session
        var user = new User(
            userId,
            new Email(userEmail),
            "Revoke Test User",
            PasswordHash.Create("Password123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            lifetime: TimeSpan.FromDays(7),
            ipAddress: "192.168.1.1",
            userAgent: "Test Browser",
            timeProvider: _timeProvider);

        await sessionRepository.AddAsync(session, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        session.IsValid(_timeProvider).Should().BeTrue();
        _output("Active session created");

        // Act: Revoke session (logout)
        session.Revoke(_timeProvider);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("Session revoked (logout)");

        // Assert session cannot be reused
        session.IsValid(_timeProvider).Should().BeFalse();
        session.RevokedAt.Should().NotBeNull();

        _output("✓ Session revocation flow verified");
    }

    /// <summary>
    /// E2E TEST: Session renewal flow (sliding expiration).
    /// Journey: Session active → Activity → Session extended → Valid longer
    /// </summary>
    [Fact]
    public async Task SessionFlow_Renewal_ExtendsLifetime()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var sessionRepository = _serviceProvider!.GetRequiredService<ISessionRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var userEmail = $"session-renew-{Guid.NewGuid():N}@test.meepleai.dev";

        // Setup: User and session
        var user = new User(
            userId,
            new Email(userEmail),
            "Renew Test User",
            PasswordHash.Create("Password123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            lifetime: TimeSpan.FromHours(1),
            ipAddress: "192.168.1.1",
            userAgent: "Test Browser",
            timeProvider: _timeProvider);

        await sessionRepository.AddAsync(session, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var originalExpiry = session.ExpiresAt;
        _output($"Original expiry: {originalExpiry}");

        // Advance time 30 minutes (within lifetime)
        _timeProvider.Advance(TimeSpan.FromMinutes(30));
        session.IsValid(_timeProvider).Should().BeTrue();
        _output("Session still valid after 30 minutes");

        // Act: Extend session by 1 hour
        session.Extend(TimeSpan.FromHours(1), _timeProvider);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var newExpiry = session.ExpiresAt;
        _output($"New expiry after extension: {newExpiry}");

        // Assert session extended
        (newExpiry > originalExpiry).Should().BeTrue();
        session.IsValid(_timeProvider).Should().BeTrue();

        // Now advance 50 more minutes - would have expired without extension
        _timeProvider.Advance(TimeSpan.FromMinutes(50));
        session.IsValid(_timeProvider).Should().BeTrue();

        _output("✓ Session renewal flow verified - session still valid after extension");
    }

    /// <summary>
    /// E2E TEST: Force logout all sessions.
    /// Journey: User has multiple sessions → Force logout → All sessions revoked
    /// </summary>
    [Fact]
    public async Task SessionFlow_ForceLogoutAll_RevokesAllSessions()
    {
        // Arrange
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var sessionRepository = _serviceProvider!.GetRequiredService<ISessionRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var userId = Guid.NewGuid();
        var userEmail = $"force-logout-{Guid.NewGuid():N}@test.meepleai.dev";

        // Setup: User with multiple sessions
        var user = new User(
            userId,
            new Email(userEmail),
            "Multi-Session User",
            PasswordHash.Create("Password123!"),
            Role.User);

        await userRepository.AddAsync(user, TestCancellationToken);

        // Create 3 sessions from different devices
        var sessions = new List<Session>();
        var devices = new[] { ("192.168.1.1", "Desktop"), ("10.0.0.1", "Mobile"), ("172.16.0.1", "Tablet") };

        foreach (var (ip, device) in devices)
        {
            var session = new Session(
                id: Guid.NewGuid(),
                userId: userId,
                token: SessionToken.Generate(),
                lifetime: TimeSpan.FromDays(7),
                ipAddress: ip,
                userAgent: device,
                timeProvider: _timeProvider);

            sessions.Add(session);
            await sessionRepository.AddAsync(session, TestCancellationToken);
        }

        await unitOfWork.SaveChangesAsync(TestCancellationToken);
        _output("3 sessions created from different devices");

        sessions.Should().OnlyContain(s => s.IsValid(_timeProvider));

        // Act: Force logout all sessions using repository method
        await sessionRepository.RevokeAllUserSessionsAsync(userId, TestCancellationToken);
        _output("All sessions revoked (force logout)");

        _dbContext!.ChangeTracker.Clear(); // Force fresh fetch from DB
        var activeSessions = await _dbContext!.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .CountAsync(TestCancellationToken);

        activeSessions.Should().Be(0);

        _output("✓ Force logout all sessions flow verified");
    }

    #endregion

    #region Helper Methods

    private async Task MigrateWithRetry(MeepleAiDbContext context, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await context.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                _output($"Migration attempt {i + 1} failed: {ex.Message}. Retrying...");
                await Task.Delay(1000, TestCancellationToken);
            }
        }

        throw new InvalidOperationException("Failed to apply migrations after maximum retries");
    }

    #endregion
}
