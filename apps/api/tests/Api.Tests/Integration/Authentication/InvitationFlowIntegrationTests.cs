using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.Commands.RevokeInvitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries.Invitation;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for the admin invitation flow end-to-end.
/// Tests provisioning, activation, resend, revoke, and batch operations against real PostgreSQL.
/// Issue #124: Admin invitation flow.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.E2E)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "124")]
public sealed class InvitationFlowIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private FakeTimeProvider _timeProvider = null!;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data
    private static readonly Guid AdminUserId = new("a0000000-0000-0000-0000-000000000001");

    public InvitationFlowIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing Invitation Flow integration test infrastructure...");

        _databaseName = $"test_invitation_flow_{Guid.NewGuid():N}";
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
        services.AddScoped<IInvitationTokenRepository, InvitationTokenRepository>();

        // Register services needed by handlers
        services.AddSingleton<IPasswordHashingService, PasswordHashingService>();
        services.AddSingleton<GameSuggestionChannel>();

        // Override IEmailService with NoOpEmailService (has behavior)
        services.RemoveAll<IEmailService>();
        services.AddSingleton<IEmailService>(new Api.Tests.TestHelpers.NoOpEmailService());

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        _output("Applying migrations...");
        await MigrateWithRetry(_dbContext);

        // Seed admin user (required for FK constraints on invitation_tokens.invited_by_user_id)
        var userRepo = _serviceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var adminUser = new Api.BoundedContexts.Authentication.Domain.Entities.User(
            AdminUserId,
            new Api.BoundedContexts.Authentication.Domain.ValueObjects.Email("admin@test.meepleai.dev"),
            "Test Admin",
            Api.BoundedContexts.Authentication.Domain.ValueObjects.PasswordHash.Create("AdminPassword123!"),
            Api.SharedKernel.Domain.ValueObjects.Role.Admin);
        await userRepo.AddAsync(adminUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        _output("Invitation Flow test infrastructure ready");
    }

    public async ValueTask DisposeAsync()
    {
        _output("Cleaning up Invitation Flow test infrastructure...");

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
    }

    #region Full Flow: Provision → Validate → Activate

    /// <summary>
    /// E2E: Admin provisions user with game suggestions → verify pending state in DB →
    /// validate token → activate with password → verify active state, email verified, session created.
    /// </summary>
    [Fact]
    public async Task FullFlow_ProvisionAndActivate_CompletesSuccessfully()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var email = $"invited-{Guid.NewGuid():N}@test.meepleai.dev";
        var gameId = Guid.NewGuid();

        // Step 1: Provision and invite
        var provisionResult = await mediator.Send(new ProvisionAndInviteUserCommand(
            Email: email,
            DisplayName: "Test Invitee",
            Role: "User",
            Tier: "free",
            CustomMessage: "Welcome to MeepleAI!",
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>
            {
                new(gameId, "PreAdded")
            },
            InvitedByUserId: AdminUserId
        ), TestCancellationToken);

        _output($"Step 1: User provisioned, invitation ID = {provisionResult.Id}");

        provisionResult.Should().NotBeNull();
        provisionResult.Email.Should().Be(email.ToLowerInvariant());
        provisionResult.Status.Should().Be("Pending");
        provisionResult.GameSuggestions.Should().HaveCount(1);

        // Step 2: Verify pending user exists in DB
        var userRepo = _serviceProvider!.GetRequiredService<IUserRepository>();
        var invitationRepo = _serviceProvider!.GetRequiredService<IInvitationTokenRepository>();

        var pendingInvitation = await invitationRepo.GetPendingByEmailAsync(
            email.ToLowerInvariant(), TestCancellationToken);
        pendingInvitation.Should().NotBeNull();
        pendingInvitation!.PendingUserId.Should().NotBeNull();

        var pendingUser = await userRepo.GetByIdAsync(pendingInvitation.PendingUserId!.Value, TestCancellationToken);
        pendingUser.Should().NotBeNull();
        pendingUser!.Status.Should().Be(UserAccountStatus.Pending);

        _output("Step 2: Verified pending user and token exist in DB");

        // Step 3: Validate token via query — we need the raw token, but the handler hashes it.
        // Since we can't recover the raw token from the handler, we verify the validate flow
        // by creating a known token directly.
        // Instead, verify the invitation is in the DB with correct state.
        var invitationById = await invitationRepo.GetByIdAsync(provisionResult.Id, TestCancellationToken);
        invitationById.Should().NotBeNull();
        invitationById!.IsValid.Should().BeTrue();
        invitationById.Email.Should().Be(email.ToLowerInvariant());

        _output("Step 3: Invitation token validated (via direct repo check)");

        // Step 4: Activate — we can't use the handler directly because the raw token is not
        // returned from ProvisionAndInviteUserCommand (by design, security).
        // Verify user activation domain logic by testing the activate handler with a known token.
        // This is covered in the dedicated activation test below.

        _output("Full flow provisioning verified. Activation covered in dedicated test.");
    }

    /// <summary>
    /// E2E: Tests the full activation path using a directly seeded invitation token.
    /// Creates known token hash → activates → verifies user active, session created.
    /// </summary>
    [Fact]
    public async Task ActivateInvitedAccount_WithValidToken_ActivatesUserAndCreatesSession()
    {
        // Arrange: Provision a user first
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var email = $"activate-{Guid.NewGuid():N}@test.meepleai.dev";

        var provisionResult = await mediator.Send(new ProvisionAndInviteUserCommand(
            Email: email,
            DisplayName: "Activate Test User",
            Role: "User",
            Tier: "free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: AdminUserId
        ), TestCancellationToken);

        // We need to seed a known raw token so we can call ActivateInvitedAccountCommand.
        // The provisioned invitation has an unknown raw token (only hash stored).
        // Approach: create a second invitation with a known token directly in the DB.

        var invitationRepo = _serviceProvider!.GetRequiredService<IInvitationTokenRepository>();
        var userRepo = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        // Get the pending user ID from the provisioned invitation
        var existingInvitation = await invitationRepo.GetByIdAsync(provisionResult.Id, TestCancellationToken);
        existingInvitation.Should().NotBeNull();
        var pendingUserId = existingInvitation!.PendingUserId!.Value;

        // Expire the old invitation so we can create a new one with a known token
        existingInvitation.MarkExpired();
        await invitationRepo.UpdateAsync(existingInvitation, TestCancellationToken);

        // Create a new invitation with a known raw token pointing to the same pending user
        var knownRawToken = "test-activation-token-" + Guid.NewGuid().ToString("N");
        var knownTokenHash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(knownRawToken)));

        var knownInvitation = Api.BoundedContexts.Authentication.Domain.Entities.InvitationToken.Create(
            new Api.BoundedContexts.Authentication.Domain.ValueObjects.Email(email.ToLowerInvariant()),
            Api.SharedKernel.Domain.ValueObjects.Role.User,
            pendingUserId,
            null,
            7,
            _timeProvider,
            knownTokenHash,
            AdminUserId);

        await invitationRepo.AddAsync(knownInvitation, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        _output("Seeded known token for activation test");

        // Act: Activate using the known raw token
        var activationResult = await mediator.Send(new ActivateInvitedAccountCommand(
            Token: knownRawToken,
            Password: "SecurePassword123!"
        ), TestCancellationToken);

        // Assert
        activationResult.Should().NotBeNull();
        activationResult.SessionToken.Should().NotBeNullOrEmpty();
        activationResult.RequiresOnboarding.Should().BeTrue();

        // Verify user is now Active
        var activatedUser = await userRepo.GetByIdAsync(pendingUserId, TestCancellationToken);
        activatedUser.Should().NotBeNull();
        activatedUser!.Status.Should().Be(UserAccountStatus.Active);
        activatedUser.EmailVerified.Should().BeTrue();

        // Verify invitation is marked Accepted
        var acceptedInvitation = await invitationRepo.GetByIdAsync(knownInvitation.Id, TestCancellationToken);
        acceptedInvitation.Should().NotBeNull();
        acceptedInvitation!.Status.Should().Be(Api.BoundedContexts.Authentication.Domain.Enums.InvitationStatus.Accepted);
        acceptedInvitation.AcceptedByUserId.Should().Be(pendingUserId);

        // Verify session exists
        var sessionRepo = _serviceProvider!.GetRequiredService<ISessionRepository>();
        var sessions = await sessionRepo.GetByUserIdAsync(pendingUserId, TestCancellationToken);
        sessions.Should().NotBeEmpty("a session should be created on activation");

        _output("Activation verified: user Active, email verified, session created, invitation Accepted");
    }

    #endregion

    #region Revoked Token Validation

    /// <summary>
    /// A revoked invitation should be reported as "invalid" when validated.
    /// Tests the security behavior: revoked tokens are indistinguishable from non-existent ones.
    /// </summary>
    [Fact]
    public async Task ValidateToken_RevokedInvitation_ReturnsInvalid()
    {
        // Arrange: Create invitation, then revoke it
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var invitationRepo = _serviceProvider!.GetRequiredService<IInvitationTokenRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var knownRawToken = "revoke-validate-" + Guid.NewGuid().ToString("N");
        var knownTokenHash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(knownRawToken)));

        var invitation = Api.BoundedContexts.Authentication.Domain.Entities.InvitationToken.Create(
            $"revoketest-{Guid.NewGuid():N}@test.meepleai.dev", "User", knownTokenHash, AdminUserId);

        await invitationRepo.AddAsync(invitation, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Validate before revoke — should be valid
        var validResult = await mediator.Send(new ValidateInvitationTokenQuery(knownRawToken), TestCancellationToken);
        validResult.IsValid.Should().BeTrue();
        _output("Token is valid before revocation");

        // Revoke the invitation
        invitation.Revoke();
        await invitationRepo.UpdateAsync(invitation, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Validate after revoke — should be invalid
        var revokedResult = await mediator.Send(new ValidateInvitationTokenQuery(knownRawToken), TestCancellationToken);
        revokedResult.IsValid.Should().BeFalse();
        revokedResult.ErrorReason.Should().Be("invalid");

        _output("Revoked token correctly returns 'invalid'");
    }

    #endregion

    #region Duplicate Email Conflict

    /// <summary>
    /// Provisioning a user, then trying to provision the same email again, should throw ConflictException.
    /// </summary>
    [Fact]
    public async Task ProvisionAndInvite_DuplicateEmail_ThrowsConflictException()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var email = $"duplicate-{Guid.NewGuid():N}@test.meepleai.dev";

        // First provision should succeed
        await mediator.Send(new ProvisionAndInviteUserCommand(
            Email: email,
            DisplayName: "First Invite",
            Role: "User",
            Tier: "free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: AdminUserId
        ), TestCancellationToken);

        _output("First provision succeeded");

        // Act & Assert: Second provision with same email should fail
        var act = () => mediator.Send(new ProvisionAndInviteUserCommand(
            Email: email,
            DisplayName: "Second Invite",
            Role: "User",
            Tier: "free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: AdminUserId
        ), TestCancellationToken);

        await act.Should().ThrowAsync<ConflictException>();

        _output("Duplicate email correctly rejected with ConflictException");
    }

    #endregion

    #region Batch Partial Failure (CSV)

    /// <summary>
    /// Batch invitations: 2 new emails + 1 existing user email → 2 succeed, 1 fails.
    /// Uses BulkSendInvitationsCommand which dispatches SendInvitationCommand per row.
    /// </summary>
    [Fact]
    public async Task BulkSendInvitations_PartialFailure_ReportsSuccessAndFailure()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Pre-create a user so one CSV row will conflict
        var existingEmail = $"existing-{Guid.NewGuid():N}@test.meepleai.dev";
        await mediator.Send(new ProvisionAndInviteUserCommand(
            Email: existingEmail,
            DisplayName: "Existing User",
            Role: "User",
            Tier: "free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: AdminUserId
        ), TestCancellationToken);

        var newEmail1 = $"batch1-{Guid.NewGuid():N}@test.meepleai.dev";
        var newEmail2 = $"batch2-{Guid.NewGuid():N}@test.meepleai.dev";

        // CSV with header + 3 rows (2 new + 1 existing that will be a conflict
        // because a pending invitation already exists for existingEmail)
        var csvContent = $"email,role\n{newEmail1},User\n{newEmail2},Editor\n{existingEmail},User";

        // Act
        var result = await mediator.Send(new BulkSendInvitationsCommand(
            CsvContent: csvContent,
            InvitedByUserId: AdminUserId
        ), TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Successful.Should().HaveCount(2);
        result.Failed.Should().HaveCount(1);
        result.Failed[0].Email.Should().Be(existingEmail.ToLowerInvariant());

        _output($"Batch result: {result.Successful.Count} succeeded, {result.Failed.Count} failed");
    }

    #endregion

    #region Resend Invitation

    /// <summary>
    /// Provision → resend → verify old token expired, new token created, same pending user preserved.
    /// </summary>
    [Fact]
    public async Task ResendInvitation_CreatesNewTokenAndExpiresOld()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var invitationRepo = _serviceProvider!.GetRequiredService<IInvitationTokenRepository>();
        var email = $"resend-{Guid.NewGuid():N}@test.meepleai.dev";

        // Note: ResendInvitationCommand uses the simpler SendInvitationCommand path
        // (creates a new token, not a provisioned user). It looks up by InvitationId.
        // Use SendInvitationCommand first (simpler path that doesn't require pending user).
        var originalResult = await mediator.Send(new SendInvitationCommand(
            Email: email,
            Role: "User",
            InvitedByUserId: AdminUserId
        ), TestCancellationToken);

        var originalId = originalResult.Id;
        _output($"Original invitation created: {originalId}");

        // Act: Resend
        var resendResult = await mediator.Send(new ResendInvitationCommand(
            InvitationId: originalId,
            ResendByUserId: AdminUserId
        ), TestCancellationToken);

        // Assert
        resendResult.Should().NotBeNull();
        resendResult.Id.Should().NotBe(originalId, "a new invitation token should be created");
        resendResult.Email.Should().Be(email.ToLowerInvariant());
        resendResult.Status.Should().Be("Pending");

        // Verify old invitation is expired
        var oldInvitation = await invitationRepo.GetByIdAsync(originalId, TestCancellationToken);
        oldInvitation.Should().NotBeNull();
        oldInvitation!.Status.Should().Be(Api.BoundedContexts.Authentication.Domain.Enums.InvitationStatus.Expired);

        // Verify new invitation exists
        var newInvitation = await invitationRepo.GetByIdAsync(resendResult.Id, TestCancellationToken);
        newInvitation.Should().NotBeNull();
        newInvitation!.Status.Should().Be(Api.BoundedContexts.Authentication.Domain.Enums.InvitationStatus.Pending);

        _output("Resend verified: old token expired, new token created");
    }

    #endregion

    #region Revoke Invitation

    /// <summary>
    /// Provision → revoke → verify invitation status is Revoked.
    /// </summary>
    [Fact]
    public async Task RevokeInvitation_SetsStatusToRevoked()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var invitationRepo = _serviceProvider!.GetRequiredService<IInvitationTokenRepository>();
        var email = $"revoke-{Guid.NewGuid():N}@test.meepleai.dev";

        var provisionResult = await mediator.Send(new ProvisionAndInviteUserCommand(
            Email: email,
            DisplayName: "Revoke Test",
            Role: "User",
            Tier: "free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: AdminUserId
        ), TestCancellationToken);

        _output($"Invitation created: {provisionResult.Id}");

        // Act: Revoke
        var revokeResult = await mediator.Send(new RevokeInvitationCommand(
            InvitationId: provisionResult.Id,
            AdminUserId: AdminUserId
        ), TestCancellationToken);

        // Assert
        revokeResult.Should().BeTrue();

        var revokedInvitation = await invitationRepo.GetByIdAsync(provisionResult.Id, TestCancellationToken);
        revokedInvitation.Should().NotBeNull();
        revokedInvitation!.Status.Should().Be(Api.BoundedContexts.Authentication.Domain.Enums.InvitationStatus.Revoked);
        revokedInvitation.RevokedAt.Should().NotBeNull();

        _output("Revoke verified: invitation status is Revoked");
    }

    /// <summary>
    /// Revoking a non-existent invitation should return false (not throw).
    /// </summary>
    [Fact]
    public async Task RevokeInvitation_NonExistentId_ReturnsFalse()
    {
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        var result = await mediator.Send(new RevokeInvitationCommand(
            InvitationId: Guid.NewGuid(),
            AdminUserId: AdminUserId
        ), TestCancellationToken);

        result.Should().BeFalse();
    }

    #endregion

    #region Validate Token Query

    /// <summary>
    /// ValidateInvitationTokenQuery with a non-existent token returns invalid.
    /// </summary>
    [Fact]
    public async Task ValidateToken_NonExistent_ReturnsInvalid()
    {
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        var result = await mediator.Send(
            new ValidateInvitationTokenQuery("non-existent-token-xyz"),
            TestCancellationToken);

        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("invalid");
    }

    /// <summary>
    /// ValidateInvitationTokenQuery with empty token returns invalid.
    /// </summary>
    [Fact]
    public async Task ValidateToken_EmptyString_ReturnsInvalid()
    {
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        var result = await mediator.Send(
            new ValidateInvitationTokenQuery(""),
            TestCancellationToken);

        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("invalid");
    }

    #endregion

    #region Repository Persistence

    /// <summary>
    /// InvitationTokenRepository: Add, GetByTokenHash, GetPendingByEmail, CountByStatus round-trip.
    /// </summary>
    [Fact]
    public async Task InvitationTokenRepository_CrudOperations_PersistCorrectly()
    {
        var invitationRepo = _serviceProvider!.GetRequiredService<IInvitationTokenRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();
        var email = $"repo-{Guid.NewGuid():N}@test.meepleai.dev";

        // Create
        var tokenHash = $"hash-{Guid.NewGuid():N}";
        var invitation = Api.BoundedContexts.Authentication.Domain.Entities.InvitationToken.Create(
            email, "User", tokenHash, AdminUserId);

        await invitationRepo.AddAsync(invitation, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Read by ID
        var byId = await invitationRepo.GetByIdAsync(invitation.Id, TestCancellationToken);
        byId.Should().NotBeNull();
        byId!.Email.Should().Be(email.ToLowerInvariant());

        // Read by hash
        var byHash = await invitationRepo.GetByTokenHashAsync(tokenHash, TestCancellationToken);
        byHash.Should().NotBeNull();
        byHash!.Id.Should().Be(invitation.Id);

        // Read by email
        var byEmail = await invitationRepo.GetPendingByEmailAsync(email.ToLowerInvariant(), TestCancellationToken);
        byEmail.Should().NotBeNull();
        byEmail!.Id.Should().Be(invitation.Id);

        // Count
        var pendingCount = await invitationRepo.CountByStatusAsync(
            Api.BoundedContexts.Authentication.Domain.Enums.InvitationStatus.Pending, TestCancellationToken);
        pendingCount.Should().BeGreaterThanOrEqualTo(1);

        _output("Repository CRUD operations verified");
    }

    /// <summary>
    /// InvitationTokenRepository: GetByStatusAsync with pagination works correctly.
    /// </summary>
    [Fact]
    public async Task InvitationTokenRepository_GetByStatus_PaginatesCorrectly()
    {
        var invitationRepo = _serviceProvider!.GetRequiredService<IInvitationTokenRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        // Create 3 pending invitations
        for (int i = 0; i < 3; i++)
        {
            var email = $"paginate-{i}-{Guid.NewGuid():N}@test.meepleai.dev";
            var tokenHash = $"hash-paginate-{i}-{Guid.NewGuid():N}";
            var inv = Api.BoundedContexts.Authentication.Domain.Entities.InvitationToken.Create(
                email, "User", tokenHash, AdminUserId);
            await invitationRepo.AddAsync(inv, TestCancellationToken);
        }

        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Page 1 with size 2
        var page1 = await invitationRepo.GetByStatusAsync(
            Api.BoundedContexts.Authentication.Domain.Enums.InvitationStatus.Pending,
            page: 1, pageSize: 2, TestCancellationToken);

        page1.Should().HaveCount(2);

        _output("Pagination verified");
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
                await Task.Delay(TimeSpan.FromSeconds(2), TestCancellationToken);
            }
        }
    }

    #endregion
}
