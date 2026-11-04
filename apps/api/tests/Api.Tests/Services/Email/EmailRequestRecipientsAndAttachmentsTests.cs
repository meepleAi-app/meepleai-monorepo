using System.Globalization;
using Api.Services.Email;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Resend;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// #4845. The report emails (<c>SendReportEmailAsync</c>) carry an attachment and go to several recipients, which a
/// single-recipient, attachment-less <see cref="EmailRequest"/> could not say. The last family still on a
/// hand-built <c>SmtpClient</c> could not move to the single sender until both transports could carry them.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4845")]
public sealed class EmailRequestRecipientsAndAttachmentsTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x01, 0x02, 0x03];

    private static EmailRequest Request(
        IReadOnlyList<string>? additional = null,
        IReadOnlyList<EmailRequestAttachment>? attachments = null) => new()
    {
        FromEmail = "noreply@meepleai.test",
        FromName = "MeepleAI Test",
        ToEmail = "first@example.test",
        AdditionalToEmails = additional,
        Subject = "Report Ready",
        HtmlBody = "<p>Body</p>",
        Attachments = attachments,
    };

    // ── SMTP ─────────────────────────────────────────────────────────────────

    private static SmtpEmailSender SmtpTo(FakeSmtpServer smtp) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:SmtpHost"] = "127.0.0.1",
                    ["Email:SmtpPort"] = smtp.Port.ToString(CultureInfo.InvariantCulture),
                    ["Email:EnableSsl"] = "false",
                })
                .Build(),
            NullLogger<SmtpEmailSender>.Instance);

    [Fact]
    public async Task Smtp_PutsEveryRecipientOnTheToLine_ThatIsTheOneThingTheyAllSee()
    {
        await using var smtp = new FakeSmtpServer();

        await SmtpTo(smtp).SendAsync(
            Request(additional: ["second@example.test", "third@example.test"]), TestContext.Current.CancellationToken);

        smtp.ServerErrors.Should().BeEmpty();
        var received = smtp.Received.Should().ContainSingle().Subject;
        received.Headers["To"].Should().Be("first@example.test, second@example.test, third@example.test");
    }

    [Fact]
    public async Task Smtp_WithNoAdditionalRecipients_IsTheMailOfBefore()
    {
        await using var smtp = new FakeSmtpServer();

        await SmtpTo(smtp).SendAsync(Request(), TestContext.Current.CancellationToken);

        var received = smtp.Received.Should().ContainSingle().Subject;
        received.Headers["To"].Should().Be("first@example.test");
        received.Headers["Content-Type"].Should().StartWith("text/html", "no attachment: no multipart envelope");
    }

    [Fact]
    public async Task Smtp_AttachesTheFile_UnderItsName_WithItsBytes()
    {
        await using var smtp = new FakeSmtpServer();

        await SmtpTo(smtp).SendAsync(
            Request(attachments: [new EmailRequestAttachment("report-2031.pdf", Pdf)]),
            TestContext.Current.CancellationToken);

        smtp.ServerErrors.Should().BeEmpty();
        var received = smtp.Received.Should().ContainSingle().Subject;
        received.Headers["Content-Type"].Should().StartWith("multipart/mixed");
        received.Body.Should().Contain("report-2031.pdf");
        received.Body.Should().Contain(Convert.ToBase64String(Pdf));
        received.Body.Should().Contain("<p>Body</p>", "the HTML part is still there beside the attachment");
    }

    // ── Resend ───────────────────────────────────────────────────────────────

    private (ResendEmailSender Sender, Func<EmailMessage> Sent) ResendCapturing()
    {
        EmailMessage? captured = null;
        var resend = new Mock<IResend>();
        resend
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(new ResendResponse<Guid>(Guid.NewGuid(), new ResendRateLimit()));
        var sender = new ResendEmailSender(
            resend.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<ILogger<ResendEmailSender>>());
        return (sender, () => captured ?? throw new InvalidOperationException("nothing was sent"));
    }

    [Fact]
    public async Task Resend_AddsEveryRecipient()
    {
        var (sender, sent) = ResendCapturing();

        await sender.SendAsync(
            Request(additional: ["second@example.test", "third@example.test"]), TestContext.Current.CancellationToken);

        sent().To.Select(a => a.Email).Should().Equal("first@example.test", "second@example.test", "third@example.test");
    }

    [Fact]
    public async Task Resend_WithNoAdditionalRecipients_AddsOnlyTheFirst()
    {
        var (sender, sent) = ResendCapturing();

        await sender.SendAsync(Request(), TestContext.Current.CancellationToken);

        sent().To.Select(a => a.Email).Should().Equal("first@example.test");
        sent().Attachments.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Resend_AttachesTheFile_UnderItsName_WithItsBytes()
    {
        var (sender, sent) = ResendCapturing();

        await sender.SendAsync(
            Request(attachments: [new EmailRequestAttachment("report-2031.pdf", Pdf)]),
            TestContext.Current.CancellationToken);

        var attachment = sent().Attachments.Should().ContainSingle().Subject;
        attachment.Filename.Should().Be("report-2031.pdf");
        attachment.Content!.Value.AsByteArray().Should().Equal(Pdf);
    }
}
