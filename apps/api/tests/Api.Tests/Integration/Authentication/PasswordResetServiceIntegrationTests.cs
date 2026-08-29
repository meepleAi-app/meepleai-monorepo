using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Issue #3882 — <c>PasswordResetService</c> non aveva alcun test: l'unica copertura era
/// <c>ResetPasswordCommandValidatorTests</c>, che valida il comando e non tocca il servizio.
///
/// <para>
/// Il servizio leggeva senza <c>.AsTracking()</c>. Con il default NoTracking della produzione
/// (PERF-06) le sue tre letture erano DETACHED, quindi <c>ResetPasswordAsync</c>:
/// <list type="bullet">
///   <item>non marcava il token usato — che quindi <b>restava riutilizzabile</b>;</item>
///   <item>non cambiava la password;</item>
///   <item>non revocava le sessioni aperte, che e' la mitigazione dichiarata dal commento
///   «Revoke all existing sessions for security».</item>
/// </list>
/// E ritornava <c>(true, userId)</c>: un reset password che riporta successo senza fare nulla.
/// </para>
///
/// <para>
/// Questi test asseriscono la <b>persistenza</b>, non la mutazione: ogni verifica rilegge dal
/// database dopo <c>ChangeTracker.Clear()</c>. Asserire sull'istanza in memoria e' esattamente
/// il modo in cui questa famiglia di difetti resta invisibile.
/// </para>
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "3882")]
public sealed class PasswordResetServiceIntegrationTests : SharedDatabaseTestBase
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string OldPassword = "OldUnusualPwd123!";
    private const string NewPassword = "NewUnusualPwd456!";

    private readonly Mock<IEmailService> _email = new();
    private readonly IPasswordHashingService _hashing = new PasswordHashingService();

    public PasswordResetServiceIntegrationTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    private PasswordResetService CreateService()
    {
        var rateLimit = new Mock<IRateLimitService>();
        rateLimit
            .Setup(r => r.CheckRateLimitAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 3, 0));

        return new PasswordResetService(
            DbContext,
            _email.Object,
            rateLimit.Object,
            _hashing,
            NullLogger<PasswordResetService>.Instance,
            TimeProvider);
    }

    /// <summary>
    /// Semina un utente con una password nota e restituisce (id, email).
    /// </summary>
    private async Task<(Guid UserId, string Email)> SeedUserAsync()
    {
        var id = Guid.NewGuid();
        var email = $"reset-{id:N}@meepleai.test";
        DbContext.Users.Add(new UserEntity
        {
            Id = id,
            Email = email,
            DisplayName = "Reset Target",
            PasswordHash = _hashing.HashSecret(OldPassword),
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow,
        });
        await DbContext.SaveChangesAsync(Ct);
        DbContext.ChangeTracker.Clear();
        return (id, email);
    }

    /// <summary>
    /// Richiede un reset e restituisce il token in chiaro, catturato dalla mail.
    /// Il servizio memorizza solo l'hash, quindi questa e' l'unica via che non simula nulla.
    /// </summary>
    private async Task<string> RequestResetAndCaptureTokenAsync(string email)
    {
        string? captured = null;
        _email
            .Setup(e => e.SendPasswordResetEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, token, _) => captured = token)
            .Returns(Task.CompletedTask);

        (await CreateService().RequestPasswordResetAsync(email, Ct)).Should().BeTrue();
        DbContext.ChangeTracker.Clear();

        captured.Should().NotBeNullOrEmpty("il token in chiaro arriva solo per email");
        return captured!;
    }

    [Fact]
    public async Task ResetPassword_PersistsTheNewPasswordHash()
    {
        await ResetDatabaseAsync();
        var (userId, email) = await SeedUserAsync();
        var token = await RequestResetAndCaptureTokenAsync(email);

        var (success, returnedUserId) = await CreateService().ResetPasswordAsync(token, NewPassword, Ct);

        success.Should().BeTrue();
        returnedUserId.Should().Be(userId);

        DbContext.ChangeTracker.Clear();
        var persisted = await DbContext.Users.FirstAsync(u => u.Id == userId, Ct);
        _hashing.VerifySecret(NewPassword, persisted.PasswordHash!)
            .Should().BeTrue("la nuova password deve essere quella persistita, non solo assegnata in memoria");
        _hashing.VerifySecret(OldPassword, persisted.PasswordHash!)
            .Should().BeFalse("la vecchia password non deve piu' funzionare");
    }

    [Fact]
    public async Task ResetPassword_MarksTheTokenUsed_SoItCannotBeReplayed()
    {
        await ResetDatabaseAsync();
        var (_, email) = await SeedUserAsync();
        var token = await RequestResetAndCaptureTokenAsync(email);

        (await CreateService().ResetPasswordAsync(token, NewPassword, Ct)).Success.Should().BeTrue();

        DbContext.ChangeTracker.Clear();
        (await DbContext.PasswordResetTokens.AnyAsync(t => !t.IsUsed, Ct))
            .Should().BeFalse("il token consumato deve risultare usato nel database");

        // L'invariante che conta: un secondo uso dello stesso link deve essere rifiutato.
        var replay = await CreateService().ResetPasswordAsync(token, "AnotherUnusualPwd789!", Ct);
        replay.Success.Should().BeFalse("un token di reset e' monouso");
    }

    [Fact]
    public async Task ResetPassword_RevokesExistingSessions()
    {
        await ResetDatabaseAsync();
        var (userId, email) = await SeedUserAsync();

        for (var i = 0; i < 3; i++)
        {
            DbContext.UserSessions.Add(new UserSessionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = $"hash-{Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                User = null!,
            });
        }
        await DbContext.SaveChangesAsync(Ct);
        DbContext.ChangeTracker.Clear();

        var token = await RequestResetAndCaptureTokenAsync(email);
        (await CreateService().ResetPasswordAsync(token, NewPassword, Ct)).Success.Should().BeTrue();

        DbContext.ChangeTracker.Clear();
        var stillActive = await DbContext.UserSessions
            .CountAsync(s => s.UserId == userId && s.RevokedAt == null, Ct);
        stillActive.Should().Be(0,
            "una password cambiata non deve lasciare in vita i cookie emessi prima: e' la mitigazione dichiarata dal servizio");
    }

    [Fact]
    public async Task RequestReset_InvalidatesThePreviousToken()
    {
        await ResetDatabaseAsync();
        var (_, email) = await SeedUserAsync();

        var firstToken = await RequestResetAndCaptureTokenAsync(email);
        var secondToken = await RequestResetAndCaptureTokenAsync(email);
        secondToken.Should().NotBe(firstToken);

        DbContext.ChangeTracker.Clear();
        (await DbContext.PasswordResetTokens.CountAsync(t => !t.IsUsed, Ct))
            .Should().Be(1, "ogni richiesta invalida le precedenti: al piu' un token attivo per utente");

        // E il link vecchio non deve funzionare piu'.
        (await CreateService().ResetPasswordAsync(firstToken, NewPassword, Ct)).Success
            .Should().BeFalse("il token superato non e' piu' spendibile");
    }
}
