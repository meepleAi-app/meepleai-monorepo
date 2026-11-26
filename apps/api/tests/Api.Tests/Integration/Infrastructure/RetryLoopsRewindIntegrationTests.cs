using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

/// <summary>
/// #5162 — the code paths that open the retrying execution strategy themselves, outside the unit of work. After a
/// transient failure the strategy runs their delegate again, and before #5162 the retry found the tracker the failed
/// attempt had left. Each test injects one failure and asserts the outcome a single clean attempt would have had.
/// </summary>
/// <remarks>
/// The host enables the strategy (<c>IntegrationServiceCollectionBuilder.CreateBase</c>); under the <c>Testing</c>
/// environment the delegate runs once and none of this can be seen.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedKernel")]
[Trait("Issue", "5162")]
public sealed class RetryLoopsRewindIntegrationTests : IAsyncLifetime
{
    private const string Marker = "@retry-5162.test";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly TransientDatabaseFaults _faults = new();
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RetryLoopsRewindIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_retryloops_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.ConfigureDbContext<MeepleAiDbContext>(options => options.AddInterceptors(_faults.Save, _faults.Commit));
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await TestMigrationHelper.MigrateWithRetryAsync(scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();
        try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
        catch { /* ignore cleanup errors */ }
    }

    // ── TotpService: a backup code is single-use ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The commit of the code's use failed. The retry read the code again — unused in the database — and identity
    /// resolution handed back the tracked instance, already <c>IsUsed = true</c>: no change, only <c>UsedAt</c> was
    /// written, and the code could be used again.
    /// </summary>
    [Fact]
    public async Task ABackupCode_WhoseFirstCommitFailed_IsStillSingleUse()
    {
        const string code = "RETRY-5162";
        var userId = await SeedUserAsync($"totp{Marker}", twoFactorEnabled: true);
        await SeedBackupCodeAsync(userId, code);

        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            _faults.Commit.Armed = true;
            (await NewTotpService(scope.ServiceProvider).VerifyBackupCodeAsync(userId, code, Ct))
                .Should().BeTrue("the retry commits the use the first attempt could not");
            _faults.Commit.Tripped.Should().BeTrue("the first commit must have failed, or this proves nothing");
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().UserBackupCodes
                .AsNoTracking().SingleAsync(c => c.UserId == userId, Ct);
            stored.IsUsed.Should().BeTrue("the use is written by the retry");

            (await NewTotpService(scope.ServiceProvider).VerifyBackupCodeAsync(userId, code, Ct))
                .Should().BeFalse("a backup code is accepted once");
        }
    }

    // ── HandleOAuthCallbackCommandHandler: the provider is asked once ────────────────────────────────────────

    /// <summary>
    /// The commit of a first sign-in failed. The retry ran the whole callback again, the single-use state
    /// included, and answered «Possible CSRF attack» to a sign-in that only needed the commit again.
    /// </summary>
    [Fact]
    public async Task AFirstOAuthSignIn_WhoseCommitFailed_SignsIn()
    {
        var email = $"oauth-commit{Marker}";
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var oauth = SingleUseProvider("state-commit", "code-commit", email);

        _faults.Commit.Armed = true;
        var result = await NewOAuthHandler(scope.ServiceProvider, oauth).Handle(Callback("state-commit", "code-commit"), Ct);

        _faults.Commit.Tripped.Should().BeTrue("the first commit must have failed, or this proves nothing");
        result.Success.Should().BeTrue(result.ErrorMessage);
        (await CountUsersAsync(email)).Should().Be(1);
        (await CountOAuthAccountsAsync(email)).Should().Be(1);
        oauth.Verify(s => s.ExchangeCodeForTokenAsync("google", "code-commit"), Times.Once());
    }

    /// <summary>
    /// The save of the new user failed for a moment. The callback caught it as any other error and ended the
    /// sign-in, so the strategy never retried it; it now goes up to the strategy, and the retry does not find the
    /// failed attempt's user still <c>Added</c> next to its own.
    /// </summary>
    [Fact]
    public async Task AFirstOAuthSignIn_WhoseSaveFailed_SignsInWithOneUser()
    {
        var email = $"oauth-save{Marker}";
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var oauth = SingleUseProvider("state-save", "code-save", email);

        _faults.Save.Armed = true;
        var result = await NewOAuthHandler(scope.ServiceProvider, oauth).Handle(Callback("state-save", "code-save"), Ct);

        _faults.Save.Tripped.Should().BeTrue("the first save must have failed, or this proves nothing");
        result.Success.Should().BeTrue(result.ErrorMessage);
        (await CountUsersAsync(email)).Should().Be(1);
        (await CountOAuthAccountsAsync(email)).Should().Be(1);
    }

    // ── ReindexDocumentCommandHandler: the reset is staged before the strategy ───────────────────────────────

    /// <summary>
    /// The reset of the document is staged before the strategy and saved inside it. The commit failed after the save
    /// had accepted it, so the retry wrote nothing for the document and committed the new job next to a document
    /// still <c>Ready</c>.
    /// </summary>
    [Fact]
    public async Task AReindex_WhoseCommitFailed_ResetsTheDocumentWithItsJob()
    {
        var userId = await SeedUserAsync($"reindex{Marker}", twoFactorEnabled: false);
        var pdfId = await SeedReadyPdfAsync(userId);

        await using (var scope = _serviceProvider!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var handler = new ReindexDocumentCommandHandler(
                db, EnqueuesOnTheSameContext(db).Object, NullLogger<ReindexDocumentCommandHandler>.Instance);

            _faults.Commit.Armed = true;
            await handler.Handle(new ReindexDocumentCommand(pdfId, "v2"), Ct);
            _faults.Commit.Tripped.Should().BeTrue("the first commit must have failed, or this proves nothing");
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == pdfId, Ct);
            pdf.ProcessingState.Should().Be("Pending", "the reset commits with the job, or not at all");
            pdf.IndexerVersion.Should().Be("v2");
            pdf.ProcessingError.Should().BeNull();
            (await db.Set<ProcessingJobEntity>().AsNoTracking().CountAsync(j => j.PdfDocumentId == pdfId, Ct)).Should().Be(1);
        }
    }

    // ── Wiring ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static TotpService NewTotpService(IServiceProvider services)
    {
        var db = services.GetRequiredService<MeepleAiDbContext>();
        var rateLimit = new Mock<IRateLimitService>();
        rateLimit
            .Setup(r => r.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(Allowed: true, TokensRemaining: 5, RetryAfterSeconds: 0));
        var redis = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(new Mock<StackExchange.Redis.IDatabase>().Object);

        return new TotpService(
            db,
            Mock.Of<IEncryptionService>(),
            new AuditService(db, NullLogger<AuditService>.Instance),
            new PasswordHashingService(),
            rateLimit.Object,
            Mock.Of<IAlertingService>(),
            redis.Object,
            NullLogger<TotpService>.Instance);
    }

    /// <summary>A provider whose state, like the real one, is valid once.</summary>
    private static Mock<IOAuthService> SingleUseProvider(string state, string code, string email)
    {
        var oauth = new Mock<IOAuthService>();
        oauth.SetupSequence(s => s.ValidateStateAsync(state)).ReturnsAsync(true).ReturnsAsync(false);
        oauth.Setup(s => s.ExchangeCodeForTokenAsync("google", code))
            .ReturnsAsync(new OAuthTokenResponse("access", "refresh", 3600, "Bearer"));
        oauth.Setup(s => s.GetUserInfoAsync("google", "access"))
            .ReturnsAsync(new OAuthUserInfo($"google-{code}", email, "Ada"));
        return oauth;
    }

    private static HandleOAuthCallbackCommandHandler NewOAuthHandler(IServiceProvider services, Mock<IOAuthService> oauth)
    {
        var db = services.GetRequiredService<MeepleAiDbContext>();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResponse(null!, "session-token", DateTime.UtcNow.AddDays(1)));
        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("encrypted");

        return new HandleOAuthCallbackCommandHandler(
            oauth.Object,
            mediator.Object,
            NullLogger<HandleOAuthCallbackCommandHandler>.Instance,
            encryption.Object,
            TimeProvider.System,
            db,
            new UserRepository(db, services.GetRequiredService<IDomainEventCollector>()),
            Mock.Of<IAuditLogger>());
    }

    private static HandleOAuthCallbackCommand Callback(string state, string code) => new()
    {
        Provider = "google",
        Code = code,
        State = state,
        IpAddress = "127.0.0.1",
        UserAgent = "retry-5162",
    };

    /// <summary>The enqueue as the real handler does it: a job on the same context, saved under the ambient transaction.</summary>
    private static Mock<IMediator> EnqueuesOnTheSameContext(MeepleAiDbContext db)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async (IRequest<Guid> request, CancellationToken ct) =>
            {
                var command = (EnqueuePdfCommand)request;
                var job = new ProcessingJobEntity
                {
                    PdfDocumentId = command.PdfDocumentId,
                    UserId = command.UserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.Set<ProcessingJobEntity>().Add(job);
                await db.SaveChangesAsync(ct);
                return job.Id;
            });
        return mediator;
    }

    private async Task<Guid> SeedUserAsync(string email, bool twoFactorEnabled)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "x",
            DisplayName = "Retry probe",
            IsTwoFactorEnabled = twoFactorEnabled,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task SeedBackupCodeAsync(Guid userId, string code)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.UserBackupCodes.Add(new UserBackupCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = new PasswordHashingService().HashSecret(code),
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> SeedReadyPdfAsync(Guid userId)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "retry-5162.pdf",
            FilePath = "pdfs/retry-5162.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessingError = "an old error",
            IndexerVersion = "v1",
        };
        db.PdfDocuments.Add(pdf);
        await db.SaveChangesAsync(Ct);
        return pdf.Id;
    }

    private async Task<int> CountUsersAsync(string email)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Users.AsNoTracking()
            .CountAsync(u => u.Email == email, Ct);
    }

    private async Task<int> CountOAuthAccountsAsync(string email)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().OAuthAccounts.AsNoTracking()
            .CountAsync(a => a.User.Email == email, Ct);
    }
}
