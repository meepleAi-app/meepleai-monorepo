using System.Diagnostics.Metrics;
using System.Globalization;
using Api.Observability;
using Api.Services;
using Api.Services.Email;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// #4845. The account emails used to build their own <see cref="System.Net.Mail.SmtpClient"/> inside each method,
/// so with <c>EMAIL_PROVIDER=resend</c> the reset and verification emails still went to whatever SMTP host the
/// configuration named, and nothing could replace the transport in a test. They now go through
/// <see cref="IEmailSender"/>, like the invitation and the raw send.
/// </summary>
/// <remarks>
/// Password reset and email verification come first because an account that cannot receive them is locked out.
/// The wording of what they send is pinned in <c>EmailServiceAuthLanguageTests</c>, which reads the wire; this
/// class pins the route: which sender, to whom, from whom, and what happens when the sender fails.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4845")]
public sealed class EmailServiceAccountMailTransportTests
{
    private const string BaseUrl = "https://app.meepleai.test";
    private const string ProviderName = "test-sender-4845";

    private static readonly DateTimeOffset Now = new(2031, 5, 17, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <param name="Marker">Something the body must carry, so the test sees the mail it asked for and not just any mail.</param>
    /// <param name="ToName">What the <c>To:</c> line shows next to the address; <c>null</c> for the mails that go to a bare address.</param>
    private sealed record AccountMail(
        string Kind,
        Func<EmailService, string?, CancellationToken, Task> Send,
        string Subject,
        string Marker,
        string FailureMessage,
        string? ToName = "Marco");

    private const string To = "marco@example.test";
    private static readonly Guid SomeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // "kind" is the label the failure counter carries, so an operator can tell which mail did not leave.
    private static readonly AccountMail[] Mails =
    [
        // ── Authentication: the ones without which an account is locked out ──
        new(
            "password_reset",
            (s, lang, ct) => s.SendPasswordResetEmailAsync(To, "Marco", "tok-reset", lang, ct),
            "Reimposta la tua password di MeepleAI",
            $"{BaseUrl}/reset-password?token=tok-reset",
            "Failed to send password reset email"),
        new(
            "email_verification",
            (s, lang, ct) => s.SendVerificationEmailAsync(To, "Marco", "tok-verify", lang, ct),
            "Verifica la tua email di MeepleAI",
            $"{BaseUrl}/verify-email?token=tok-verify",
            "Failed to send verification email"),

        // ── Authentication: security notices ──
        new(
            "two_factor_disabled",
            (s, lang, ct) => s.SendTwoFactorDisabledEmailAsync(To, "Marco", false, lang, ct),
            "Autenticazione a due fattori disattivata",
            "L'autenticazione a due fattori è stata disattivata sul tuo account.",
            "Failed to send two-factor authentication disabled email"),
        new(
            "account_suspended",
            (s, lang, ct) => s.SendAccountSuspendedEmailAsync(To, "Marco", "SospesoPerSpam", lang, ct),
            "Il tuo account MeepleAI è stato sospeso",
            "SospesoPerSpam",
            "Failed to send account suspended email"),
        new(
            "account_reactivated",
            (s, lang, ct) => s.SendAccountReactivatedEmailAsync(To, "Marco", lang, ct),
            "Il tuo account MeepleAI è stato riattivato",
            "riattivato",
            "Failed to send account reactivated email"),
        new(
            "account_locked",
            (s, lang, ct) => s.SendAccountLockedEmailAsync(To, "Marco", 5, Now.UtcDateTime.AddMinutes(14.5), "203.0.113.7", lang, ct),
            "Avviso di sicurezza: il tuo account MeepleAI è stato bloccato",
            "203.0.113.7",
            "Failed to send account locked email"),
        new(
            "access_request_rejected",
            (s, _, ct) => s.SendAccessRequestRejectedEmailAsync(To, "NessunPostoMarker", ct),
            "MeepleAI — Access Request Update",
            "NessunPostoMarker",
            "Failed to send access request rejected email",
            ToName: null),

        // ── Administration ──
        new(
            "admin_share_digest",
            (s, _, ct) => s.SendAdminShareRequestDigestEmailAsync(
                To, "Marco", 7, 3, 2, new Dictionary<string, int> { ["NewGame"] = 4 }, "https://app.meepleai.test/admin/queue-marker", ct),
            "[MeepleAI Admin] Share Request Digest - 7 Pending",
            "https://app.meepleai.test/admin/queue-marker",
            "Failed to send admin digest email"),
        new(
            "shared_game_submitted",
            (s, _, ct) => s.SendSharedGameSubmittedForApprovalEmailAsync(To, "Marco", "CatanMarker", "Anna", SomeId, ct),
            "New Game Submitted for Approval",
            "CatanMarker",
            "Failed to send shared game approval notification email"),

        // ── Gamification ──
        new(
            "badge_earned",
            (s, _, ct) => s.SendBadgeEarnedEmailAsync(
                To, "Marco", "Esploratore", "Hai esplorato", null, "Gold", "#FFD700", "https://app.meepleai.test/u/marco", "Ho vinto!", ct),
            "🎉 Badge Earned: Esploratore!",
            "https://app.meepleai.test/u/marco",
            "Failed to send badge earned email for Esploratore"),
        new(
            "milestone_badge_earned",
            (s, _, ct) => s.SendMilestoneBadgeEarnedEmailAsync(
                To, "Marco", "Centurione", "Cento contributi", null, "Platinum", "Che traguardo", 100,
                "https://app.meepleai.test/u/marco", "https://app.meepleai.test/leaderboard-marker", ct),
            "🌟 Milestone Achievement: Centurione!",
            "https://app.meepleai.test/leaderboard-marker",
            "Failed to send milestone badge email for Centurione"),
        new(
            "cooldown_ended",
            (s, _, ct) => s.SendCooldownEndedEmailAsync(To, "Marco", 3, 1, "https://app.meepleai.test/library-marker", ct),
            "Ready to Contribute Again! 🎉",
            "https://app.meepleai.test/library-marker",
            "Failed to send cooldown ended email"),

        // ── PDF notifications ──
        new(
            "pdf_ready",
            (s, _, ct) => s.SendPdfReadyEmailAsync(To, "Marco", "regoleMarker.pdf", SomeId, ct),
            "Your PDF is Ready - MeepleAI",
            "regoleMarker.pdf",
            "Failed to send PDF ready email"),
        new(
            "pdf_failed",
            (s, _, ct) => s.SendPdfFailedEmailAsync(To, "Marco", "regoleMarker.pdf", "OcrFallitoMarker", ct),
            "PDF Processing Failed - MeepleAI",
            "OcrFallitoMarker",
            "Failed to send PDF failed email"),
        new(
            "pdf_retry",
            (s, _, ct) => s.SendPdfRetryEmailAsync(To, "Marco", "regoleMarker.pdf", 2, ct),
            "PDF Processing Retry - MeepleAI",
            "regoleMarker.pdf",
            "Failed to send PDF retry email"),

        // ── Share requests ──
        new(
            "share_request_created",
            (s, _, ct) => s.SendShareRequestCreatedEmailAsync(To, "Marco", "CatanMarker", "Rulebook", SomeId, ct),
            "Share Request Submitted",
            "CatanMarker",
            "Failed to send share request created email"),
        new(
            "share_request_approved",
            (s, _, ct) => s.SendShareRequestApprovedEmailAsync(To, "Marco", "CatanMarker", SomeId, SomeId, ct),
            "Contribution Approved! 🎉",
            "CatanMarker",
            "Failed to send share request approved email"),
        new(
            "share_request_rejected",
            (s, _, ct) => s.SendShareRequestRejectedEmailAsync(To, "Marco", "CatanMarker", "TroppoSimileMarker", ct),
            "Share Request Not Approved",
            "TroppoSimileMarker",
            "Failed to send share request rejected email"),
        new(
            "share_request_changes_requested",
            (s, _, ct) => s.SendShareRequestChangesRequestedEmailAsync(To, "Marco", "CatanMarker", "ServeLaPaginaMarker", SomeId, ct),
            "Changes Requested for Your Contribution",
            "ServeLaPaginaMarker",
            "Failed to send share request changes requested email"),
        new(
            "share_request_review_started",
            (s, _, ct) => s.SendShareRequestReviewStartedEmailAsync(To, "Marco", "CatanMarker", SomeId, ct),
            "Your Game Proposal Is Now Under Review",
            "CatanMarker",
            "Failed to send review started email"),
        new(
            "share_request_kb_merged",
            (s, _, ct) => s.SendShareRequestKbMergedEmailAsync(To, "Marco", "CatanMarker", SomeId, ct),
            "Your Game Proposal Has Been Merged",
            "CatanMarker",
            "Failed to send KB merged email"),
    ];

    public static TheoryData<string> MailKinds => new(Mails.Select(m => m.Kind));

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailRequest> Sent { get; } = [];

        public Exception? Failure { get; init; }

        public string ProviderName => EmailServiceAccountMailTransportTests.ProviderName;

        public Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
                throw Failure;

            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    private static IConfiguration Configuration(string? smtpPort = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FromAddress"] = "noreply@meepleai.test",
                ["Email:FromName"] = "MeepleAI Test",
                ["Email:SmtpHost"] = "127.0.0.1",
                ["Email:SmtpPort"] = smtpPort ?? "1",
                ["Email:EnableSsl"] = "false",
                ["Frontend:BaseUrl"] = BaseUrl,
            })
            .Build();

    private static EmailService ServiceWith(IEmailSender sender) =>
        new(Configuration(), NullLogger<EmailService>.Instance, sender, new FakeTimeProvider(Now));

    private static AccountMail Mail(string kind) => Mails.Single(m => m.Kind == kind);

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task TheMailGoesThroughTheInjectedSender_ToTheRecipientByNameAndAddress(string kind)
    {
        var mail = Mail(kind);
        var sender = new RecordingSender();

        await mail.Send(ServiceWith(sender), "it", Ct);

        var sent = sender.Sent.Should().ContainSingle().Subject;
        sent.ToEmail.Should().Be(To);
        sent.ToName.Should().Be(mail.ToName, "the display name is part of the To: line the recipient sees");
        sent.FromEmail.Should().Be("noreply@meepleai.test");
        sent.FromName.Should().Be("MeepleAI Test");
        sent.Subject.Should().Be(mail.Subject);
        sent.HtmlBody.Should().Contain(mail.Marker);
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ASenderFailure_ReachesTheCallerAsTheSameInvalidOperation_WithTheCauseAttached(string kind)
    {
        var mail = Mail(kind);
        var cause = new InvalidOperationException("transport down");
        var service = ServiceWith(new RecordingSender { Failure = cause });

        var act = () => mail.Send(service, "it", Ct);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Be(mail.FailureMessage);
        thrown.Which.InnerException.Should().BeSameAs(cause);
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ASenderFailure_IsCountedUnderTheProviderAndTheKindOfMail(string kind)
    {
        var mail = Mail(kind);
        var other = Mails.First(m => m.Kind != kind);
        using var failures = new SendFailureCapture();
        var service = ServiceWith(new RecordingSender { Failure = new InvalidOperationException("transport down") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => mail.Send(service, "it", Ct));

        failures.CountFor(kind).Should().Be(1);
        failures.CountFor(other.Kind).Should().Be(0, "only the mail that failed is counted");
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ASendTheCallerCancelled_IsNotCountedAsAFailure(string kind)
    {
        var mail = Mail(kind);
        using var failures = new SendFailureCapture();
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        var service = ServiceWith(new RecordingSender { Failure = new OperationCanceledException(caller.Token) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => mail.Send(service, "it", caller.Token));

        failures.CountFor(kind).Should().Be(0, "a caller that gave up is not a transport that failed");
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ATransportTimeout_IsCountedAsAFailure_EvenThoughItIsACancellationExceptionType(string kind)
    {
        // A typed HttpClient that times out throws TaskCanceledException (an OperationCanceledException) while
        // the caller's own token is still live: the provider did not answer, and that is exactly what the counter
        // is for. Telling the two apart by the exception type would count neither or both.
        var mail = Mail(kind);
        using var failures = new SendFailureCapture();
        var service = ServiceWith(new RecordingSender { Failure = new TaskCanceledException("The request timed out.") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => mail.Send(service, "it", Ct));

        failures.CountFor(kind).Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ASuccessfulSend_IsNotCountedAsAFailure(string kind)
    {
        var mail = Mail(kind);
        using var failures = new SendFailureCapture();

        await mail.Send(ServiceWith(new RecordingSender()), "it", Ct);

        failures.CountFor(kind).Should().Be(0);
    }

    [Fact]
    public async Task WithoutAnInjectedSender_TheMailStillLeavesBySmtp_WithTheRecipientsNameOnTheToLine()
    {
        await using var smtp = new FakeSmtpServer();
        var service = new EmailService(
            Configuration(smtp.Port.ToString(CultureInfo.InvariantCulture)),
            NullLogger<EmailService>.Instance,
            sender: null,
            new FakeTimeProvider(Now));

        await service.SendPasswordResetEmailAsync("marco@example.test", "Marco", "tok-reset", "it", Ct);

        smtp.ServerErrors.Should().BeEmpty();
        var received = smtp.Received.Should().ContainSingle().Subject;
        received.Headers["To"].Should().Be("\"Marco\" <marco@example.test>");
        received.Headers["From"].Should().Be("\"MeepleAI Test\" <noreply@meepleai.test>");
    }

    [Fact]
    public async Task TheSmtpSender_PutsTheRecipientsNameOnTheToLine_WhenItHasOne_AndOnlyTheAddressWhenItDoesNot()
    {
        await using var smtp = new FakeSmtpServer();
        var sender = new SmtpEmailSender(Configuration(smtp.Port.ToString(CultureInfo.InvariantCulture)), NullLogger<SmtpEmailSender>.Instance);

        await sender.SendAsync(Request(toName: "Marco"), Ct);
        await sender.SendAsync(Request(toName: null), Ct);

        smtp.ServerErrors.Should().BeEmpty();
        smtp.Received.Select(m => m.Headers["To"]).Should().Equal("\"Marco\" <marco@example.test>", "marco@example.test");
    }

    private static EmailRequest Request(string? toName) => new()
    {
        FromEmail = "noreply@meepleai.test",
        FromName = "MeepleAI Test",
        ToEmail = "marco@example.test",
        ToName = toName,
        Subject = "Subject",
        HtmlBody = "<p>Body</p>",
    };

    /// <summary>Counts failures under THIS test's provider name only (other tests run in parallel).</summary>
    private sealed class SendFailureCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly Dictionary<string, int> _byKind = [];
        private readonly object _gate = new();

        public SendFailureCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name == "meepleai.email.send_failures.total")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                string? provider = null;
                string? kind = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "provider")
                        provider = tag.Value?.ToString();
                    else if (tag.Key == "kind")
                        kind = tag.Value?.ToString();
                }

                if (provider != ProviderName || kind is null)
                    return;

                lock (_gate)
                {
                    _byKind[kind] = _byKind.GetValueOrDefault(kind) + (int)value;
                }
            });
            _listener.Start();
        }

        public int CountFor(string kind)
        {
            lock (_gate)
            {
                return _byKind.GetValueOrDefault(kind);
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
