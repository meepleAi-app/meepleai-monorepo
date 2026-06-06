using Api.BoundedContexts.Administration.Application.Commands.Providers;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Services.Providers.Probe;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application;

/// <summary>
/// Unit tests for <see cref="RotateProviderKeyCommandHandler"/>. Issue #1859.
/// Scenarios:
/// - HappyPath: superadmin, probe Success, previous active row deactivated, new row added, save called.
/// - HappyPath_FirstRotation: no previous active row → previousCredentialId is null.
/// - NonSuperAdmin: caller is admin → ForbiddenException.
/// - UserNotFound: GetByIdAsync returns null → NotFoundException.
/// - RecentRotation: last rotation &lt;24h ago → ConflictException + no probe.
/// - ProbeFailure: probe outcome non-Success → ProviderProbeFailedException + no DB write.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class RotateProviderKeyCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly IProviderCredentialRepository _repo;
    private readonly IUserRepository _userRepo;
    private readonly IProviderProbeExecutorFactory _probeFactory;
    private readonly IProviderProbeExecutor _probeExecutor;
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RotateProviderKeyCommandHandler _sut;

    public RotateProviderKeyCommandHandlerTests()
    {
        _repo = Substitute.For<IProviderCredentialRepository>();
        _userRepo = Substitute.For<IUserRepository>();
        _probeFactory = Substitute.For<IProviderProbeExecutorFactory>();
        _probeExecutor = Substitute.For<IProviderProbeExecutor>();
        _protectionProvider = new EphemeralDataProtectionProvider();
        _timeProvider = new FakeTimeProvider(FixedNow);

        _probeFactory.GetExecutor(Arg.Any<string>()).Returns(_probeExecutor);
        // Default probe success
        _probeExecutor
            .ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProbeExecutionResult(
                ProbeOutcome.Success, null, null, LatencyMs: 100, ModelAvailable: null)));

        _sut = new RotateProviderKeyCommandHandler(
            _repo,
            _userRepo,
            _probeFactory,
            _protectionProvider,
            _timeProvider,
            NullLogger<RotateProviderKeyCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_HappyPath_PersistsNewActive_DeactivatesOld_ReturnsFingerprint()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var requester = CreateUser(requesterId, "root@test.com", Role.SuperAdmin);
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _repo.GetLastRotationAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        var previousId = Guid.NewGuid();
        var previousActive = CreateProviderCredential("deepseek", "old-cipher", previousId);
        _repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(previousActive));

        ProviderCredential? captured = null;
        await _repo.AddAsync(Arg.Do<ProviderCredential>(c => captured = c), Arg.Any<CancellationToken>());

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert response
        result.ProviderName.Should().Be("deepseek");
        result.NewKeyFingerprint.Should().Be("sk-de..2345");
        result.RotatedAt.Should().Be(FixedNow.UtcDateTime);
        result.PreviousKeyDisabledAt.Should().Be(FixedNow.UtcDateTime);

        // Assert flow
        await _probeExecutor.Received(1)
            .ExecuteAsync("sk-deepseek-newkey-12345", null, Arg.Any<CancellationToken>());
        previousActive.IsActive.Should().BeFalse("previous credential should have been deactivated");
        captured.Should().NotBeNull();
        captured!.ProviderName.Value.Should().Be("deepseek");
        captured.Fingerprint.Value.Should().Be("sk-de..2345");
        captured.IsActive.Should().BeTrue();
        captured.PreviousCredentialId.Should().Be(previousId);
        captured.RotatedByUserId.Should().Be(requesterId);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FirstRotation_NoPrevious_PreviousIdIsNull()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "root@test.com", Role.SuperAdmin));
        _repo.GetLastRotationAsync("openrouter", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));
        _repo.GetActiveAsync("openrouter", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        ProviderCredential? captured = null;
        await _repo.AddAsync(Arg.Do<ProviderCredential>(c => captured = c), Arg.Any<CancellationToken>());

        var command = new RotateProviderKeyCommand(
            "openrouter",
            "sk-or-v1-newkey1234",
            "openrouter",
            requesterId);

        await _sut.Handle(command, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.PreviousCredentialId.Should().BeNull();
        captured.IsActive.Should().BeTrue();
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequestingUserNotFound_ThrowsNotFound()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(null));

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{requesterId}*");

        await _repo.DidNotReceive().AddAsync(Arg.Any<ProviderCredential>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonSuperAdmin_ThrowsForbidden_NoDbWrite()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "admin@test.com", Role.Admin));

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);
        (await act.Should().ThrowAsync<ForbiddenException>())
            .Which.Message.Should().Contain("superadmins");

        await _repo.DidNotReceive().AddAsync(Arg.Any<ProviderCredential>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _probeExecutor.DidNotReceive()
            .ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RegularUser_ThrowsForbidden()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "u@test.com", Role.User));

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_RecentRotation_ThrowsConflict_NoProbe_NoDbWrite()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "root@test.com", Role.SuperAdmin));

        // Last rotation 12h ago — within 24h cooldown
        _timeProvider.SetUtcNow(FixedNow);
        var twelveHoursAgo = new FakeTimeProvider(FixedNow.AddHours(-12));
        var recent = CreateProviderCredential("deepseek", "cipher", Guid.NewGuid(), twelveHoursAgo);
        _repo.GetLastRotationAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(recent));

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);
        (await act.Should().ThrowAsync<ConflictException>())
            .Which.Message.Should().Contain("less than 24h ago");

        await _probeExecutor.DidNotReceive()
            .ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().AddAsync(Arg.Any<ProviderCredential>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LastRotationExactly24hAgo_NotBlocked()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "root@test.com", Role.SuperAdmin));

        // Boundary: exactly 24h ago — cooldown is < 24h, so 24h is allowed
        var exactlyADayAgo = new FakeTimeProvider(FixedNow.AddHours(-24));
        var prior = CreateProviderCredential("deepseek", "cipher", Guid.NewGuid(), exactlyADayAgo);
        _repo.GetLastRotationAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(prior));
        _repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ProbeUnauthorized_ThrowsProviderProbeFailed_NoDbWrite()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "root@test.com", Role.SuperAdmin));
        _repo.GetLastRotationAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        _probeExecutor
            .ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProbeExecutionResult(
                ProbeOutcome.Unauthorized, "unauthorized", "Provider rejected token", 50, null)));

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ProviderProbeFailedException>()).Which;
        ex.ProviderName.Should().Be("deepseek");

        await _repo.DidNotReceive().AddAsync(Arg.Any<ProviderCredential>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProbeTimeout_ThrowsProviderProbeFailed()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "root@test.com", Role.SuperAdmin));
        _repo.GetLastRotationAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        _probeExecutor
            .ExecuteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProbeExecutionResult(
                ProbeOutcome.Timeout, "timeout", "Probe exceeded 5s", 5000, null)));

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ProviderProbeFailedException>();
    }

    [Fact]
    public async Task Handle_NoProbeExecutorRegistered_ThrowsProviderCredentialNotConfigured()
    {
        // CLAUDE.md issue #2568: handler maps the misconfiguration to
        // ProviderCredentialNotConfiguredException (HTTP 503), not InvalidOperationException (500).
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "root@test.com", Role.SuperAdmin));
        _repo.GetLastRotationAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        _probeFactory.GetExecutor("deepseek").Returns((IProviderProbeExecutor?)null);

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-deepseek-newkey-12345",
            "deepseek",
            requesterId);

        var act = async () => await _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ProviderCredentialNotConfiguredException>();
    }

    [Fact]
    public async Task Handle_EncryptsKeyViaDataProtector_CiphertextStored()
    {
        var requesterId = Guid.NewGuid();
        _userRepo.GetByIdAsync(requesterId, Arg.Any<CancellationToken>())
            .Returns(CreateUser(requesterId, "root@test.com", Role.SuperAdmin));
        _repo.GetLastRotationAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));
        _repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        ProviderCredential? captured = null;
        await _repo.AddAsync(Arg.Do<ProviderCredential>(c => captured = c), Arg.Any<CancellationToken>());

        var command = new RotateProviderKeyCommand(
            "deepseek",
            "sk-plaintext-key-12345",
            "deepseek",
            requesterId);

        await _sut.Handle(command, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.EncryptedApiKey.Should().NotBe("sk-plaintext-key-12345",
            "the stored value must be ciphertext, not plaintext");

        // Verify decryption round-trip
        var protector = _protectionProvider.CreateProtector(RotateProviderKeyCommandHandler.DataProtectionPurpose);
        protector.Unprotect(captured.EncryptedApiKey).Should().Be("sk-plaintext-key-12345");
    }

    private static User CreateUser(Guid id, string email, Role role) =>
        new(
            id: id,
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("UnusualPwd123!"),
            role: role);

    private static ProviderCredential CreateProviderCredential(
        string providerName,
        string ciphertext,
        Guid? overrideId = null,
        FakeTimeProvider? createdAt = null)
    {
        var credential = ProviderCredential.Create(
            ProviderName.Create(providerName),
            ciphertext,
            KeyFingerprint.FromPlaintext("sk-aaaaa-bbbb"),
            Guid.NewGuid(),
            previousCredentialId: null,
            timeProvider: createdAt ?? new FakeTimeProvider(FixedNow.AddDays(-5)));

        if (overrideId is { } id)
        {
            // Force a specific Id for assertions that need to identify the previous row.
            // The aggregate generates a fresh Guid in Create; we patch via reflection only
            // for the test arrangement (production never mutates Id).
            typeof(ProviderCredential)
                .GetProperty(nameof(ProviderCredential.Id))!
                .SetValue(credential, id);
        }

        return credential;
    }
}
