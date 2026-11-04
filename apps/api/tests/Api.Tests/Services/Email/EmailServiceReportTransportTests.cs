using System.Diagnostics.Metrics;
using Api.Observability;
using Api.Services;
using Api.Services.Email;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// #4845. The report emails were the last on a hand-built <c>SmtpClient</c>: they carry an attachment and go to
/// several recipients. They now go through <see cref="IEmailSender"/> like every other mail. The report is sent to
/// each of its recipients on one message (the same <c>To:</c> line as before); the failure notice is best-effort
/// and swallows a failure, which is then only a counter and a log line.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4845")]
public sealed class EmailServiceReportTransportTests
{
    private const string ProviderName = "test-sender-4845-reports";
    private static readonly byte[] Content = [0x25, 0x50, 0x44, 0x46, 0x01, 0x02, 0x03];
    private static readonly string[] Recipients = ["first@example.test", "second@example.test", "third@example.test"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailRequest> Sent { get; } = [];

        public Exception? Failure { get; init; }

        public string ProviderName => EmailServiceReportTransportTests.ProviderName;

        public Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
                throw Failure;

            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    private static EmailService ServiceWith(IEmailSender sender) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:FromAddress"] = "noreply@meepleai.test",
                    ["Email:FromName"] = "MeepleAI Test",
                })
                .Build(),
            NullLogger<EmailService>.Instance,
            sender);

    [Fact]
    public async Task TheReport_GoesToEveryRecipientOnOneMessage_WithTheFileAttached()
    {
        var sender = new RecordingSender();

        await ServiceWith(sender).SendReportEmailAsync(
            Recipients, "WeeklyReportMarker", "A description", Content, "weekly.pdf", Content.Length, Ct);

        var sent = sender.Sent.Should().ContainSingle("one message, not one per recipient").Subject;
        sent.ToEmail.Should().Be("first@example.test");
        sent.AdditionalToEmails.Should().Equal("second@example.test", "third@example.test");
        sent.ToName.Should().BeNull("the recipients of a report are addresses, not names");
        sent.FromEmail.Should().Be("noreply@meepleai.test");
        sent.Subject.Should().Be("[MeepleAI] Report Ready: WeeklyReportMarker");
        sent.HtmlBody.Should().Contain("WeeklyReportMarker");
        var attachment = sent.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().Be("weekly.pdf");
        attachment.Content.Should().Equal(Content);
    }

    [Fact]
    public async Task TheReport_ToOneRecipient_HasNoAdditionalRecipients()
    {
        var sender = new RecordingSender();

        await ServiceWith(sender).SendReportEmailAsync(
            ["only@example.test"], "R", "D", Content, "r.pdf", Content.Length, Ct);

        sender.Sent.Single().AdditionalToEmails.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task TheReport_WithNoRecipients_SendsNothing()
    {
        var sender = new RecordingSender();

        await ServiceWith(sender).SendReportEmailAsync([], "R", "D", Content, "r.pdf", Content.Length, Ct);

        sender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task TheReport_TooLargeToAttach_IsRefusedBeforeAnythingIsSent()
    {
        var sender = new RecordingSender();

        var act = () => ServiceWith(sender).SendReportEmailAsync(
            Recipients, "R", "D", Content, "r.pdf", 11L * 1024 * 1024, Ct);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exceeds email attachment limit*");
        sender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task TheReport_AFailure_ReachesTheCallerAsTheSameInvalidOperation_AndIsCounted()
    {
        var cause = new InvalidOperationException("transport down");
        using var failures = new FailureCapture();

        var act = () => ServiceWith(new RecordingSender { Failure = cause }).SendReportEmailAsync(
            Recipients, "WeeklyReportMarker", "D", Content, "r.pdf", Content.Length, Ct);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Be("Failed to send report email for WeeklyReportMarker");
        thrown.Which.InnerException.Should().BeSameAs(cause);
        failures.CountFor("report").Should().Be(1);
        failures.Total.Should().Be(1, "counted once, under its own kind and no other");
    }

    [Fact]
    public async Task TheFailureNotice_GoesToEveryRecipient_WithNoAttachment()
    {
        var sender = new RecordingSender();

        await ServiceWith(sender).SendReportFailureEmailAsync(Recipients, "WeeklyReportMarker", "BoomMarker", Ct);

        var sent = sender.Sent.Should().ContainSingle().Subject;
        sent.ToEmail.Should().Be("first@example.test");
        sent.AdditionalToEmails.Should().Equal("second@example.test", "third@example.test");
        sent.Subject.Should().Be("[MeepleAI] Report Generation Failed: WeeklyReportMarker");
        sent.HtmlBody.Should().Contain("BoomMarker");
        sent.Attachments.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task TheFailureNotice_AFailureOfItsOwn_IsSwallowed_ButCounted()
    {
        // Best-effort by design (it runs in a background job that is already reporting a failure), so nothing
        // reaches the caller: the counter is what lets anyone see that the notice itself did not leave.
        using var failures = new FailureCapture();

        var act = () => ServiceWith(new RecordingSender { Failure = new InvalidOperationException("down") })
            .SendReportFailureEmailAsync(Recipients, "R", "E", Ct);

        await act.Should().NotThrowAsync();
        failures.CountFor("report_failure").Should().Be(1);
        failures.Total.Should().Be(1, "counted once, under its own kind and no other");
    }

    [Fact]
    public async Task TheFailureNotice_WithNoRecipients_SendsNothing()
    {
        var sender = new RecordingSender();

        await ServiceWith(sender).SendReportFailureEmailAsync([], "R", "E", Ct);

        sender.Sent.Should().BeEmpty();
    }

    /// <summary>Counts failures under THIS test class's provider name only (other tests run in parallel).</summary>
    private sealed class FailureCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly Dictionary<string, int> _byKind = [];
        private readonly object _gate = new();

        public FailureCapture()
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

        public int Total
        {
            get
            {
                lock (_gate)
                {
                    return _byKind.Values.Sum();
                }
            }
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
