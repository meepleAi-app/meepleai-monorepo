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

    private sealed record AccountMail(
        string Kind,
        Func<EmailService, string?, Task> Send,
        string Subject,
        string Link,
        string FailureMessage);

    // "kind" is the label the failure counter carries, so an operator can tell which mail did not leave.
    private static readonly AccountMail[] Mails =
    [
        new(
            "password_reset",
            (s, lang) => s.SendPasswordResetEmailAsync("marco@example.test", "Marco", "tok-reset", lang, Ct),
            "Reimposta la tua password di MeepleAI",
            $"{BaseUrl}/reset-password?token=tok-reset",
            "Failed to send password reset email"),
        new(
            "email_verification",
            (s, lang) => s.SendVerificationEmailAsync("marco@example.test", "Marco", "tok-verify", lang, Ct),
            "Verifica la tua email di MeepleAI",
            $"{BaseUrl}/verify-email?token=tok-verify",
            "Failed to send verification email"),
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

        await mail.Send(ServiceWith(sender), "it");

        var sent = sender.Sent.Should().ContainSingle().Subject;
        sent.ToEmail.Should().Be("marco@example.test");
        sent.ToName.Should().Be("Marco", "the display name is part of the To: line the recipient sees");
        sent.FromEmail.Should().Be("noreply@meepleai.test");
        sent.FromName.Should().Be("MeepleAI Test");
        sent.Subject.Should().Be(mail.Subject);
        sent.HtmlBody.Should().Contain(mail.Link);
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ASenderFailure_ReachesTheCallerAsTheSameInvalidOperation_WithTheCauseAttached(string kind)
    {
        var mail = Mail(kind);
        var cause = new InvalidOperationException("transport down");
        var service = ServiceWith(new RecordingSender { Failure = cause });

        var act = () => mail.Send(service, "it");

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Be(mail.FailureMessage);
        thrown.Which.InnerException.Should().BeSameAs(cause);
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ASenderFailure_IsCountedUnderTheProviderAndTheKindOfMail(string kind)
    {
        var mail = Mail(kind);
        var other = Mails.Single(m => m.Kind != kind);
        using var failures = new SendFailureCapture();
        var service = ServiceWith(new RecordingSender { Failure = new InvalidOperationException("transport down") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => mail.Send(service, "it"));

        failures.CountFor(kind).Should().Be(1);
        failures.CountFor(other.Kind).Should().Be(0, "only the mail that failed is counted");
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ACancelledSend_IsNotCountedAsAFailure(string kind)
    {
        var mail = Mail(kind);
        using var failures = new SendFailureCapture();
        var service = ServiceWith(new RecordingSender { Failure = new OperationCanceledException() });

        await Assert.ThrowsAsync<InvalidOperationException>(() => mail.Send(service, "it"));

        failures.CountFor(kind).Should().Be(0, "a caller that gave up is not a transport that failed");
    }

    [Theory]
    [MemberData(nameof(MailKinds))]
    public async Task ASuccessfulSend_IsNotCountedAsAFailure(string kind)
    {
        var mail = Mail(kind);
        using var failures = new SendFailureCapture();

        await mail.Send(ServiceWith(new RecordingSender()), "it");

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
