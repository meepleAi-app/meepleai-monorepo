using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserNotifications.Domain;

[Trait("Category", TestCategories.Unit)]
public sealed class EmailQueueItemTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private const string ToAddress = "test@example.com";
    private const string Subject = "Test Subject";
    private const string HtmlBody = "<html><body>Hello</body></html>";

    [Fact]
    public void Create_SetsPropertiesCorrectly()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);

        item.Id.Should().NotBeEmpty();
        item.UserId.Should().Be(_userId);
        item.To.Should().Be(ToAddress);
        item.Subject.Should().Be(Subject);
        item.HtmlBody.Should().Be(HtmlBody);
        item.Status.Should().Be(EmailQueueStatus.Pending);
        item.RetryCount.Should().Be(0);
        item.MaxRetries.Should().Be(3);
        item.NextRetryAt.Should().BeNull();
        item.ErrorMessage.Should().BeNull();
        item.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        item.ProcessedAt.Should().BeNull();
        item.FailedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyTo_ThrowsArgumentException(string? to)
    {
        var act = () => EmailQueueItem.Create(_userId, to!, Subject, HtmlBody);
        act.Should().Throw<ArgumentException>().WithParameterName("to");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptySubject_ThrowsArgumentException(string? subject)
    {
        var act = () => EmailQueueItem.Create(_userId, ToAddress, subject!, HtmlBody);
        act.Should().Throw<ArgumentException>().WithParameterName("subject");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyHtmlBody_ThrowsArgumentException(string? htmlBody)
    {
        var act = () => EmailQueueItem.Create(_userId, ToAddress, Subject, htmlBody!);
        act.Should().Throw<ArgumentException>().WithParameterName("htmlBody");
    }

    [Fact]
    public void MarkAsProcessing_FromPending_SetsProcessingStatus()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);

        item.MarkAsProcessing();

        item.Status.Should().Be(EmailQueueStatus.Processing);
    }

    [Fact]
    public void MarkAsProcessing_FromSent_ThrowsInvalidOperationException()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);
        item.MarkAsProcessing();
        item.MarkAsSent(DateTime.UtcNow);

        var act = () => item.MarkAsProcessing();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsSent_SetsStatusAndProcessedAt()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);
        item.MarkAsProcessing();
        var processedAt = DateTime.UtcNow;

        item.MarkAsSent(processedAt);

        item.Status.Should().Be(EmailQueueStatus.Sent);
        item.ProcessedAt.Should().Be(processedAt);
        item.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAsSent_FromPending_ThrowsInvalidOperationException()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);

        var act = () => item.MarkAsSent(DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsFailed_FirstRetry_SetsFailedStatusAndNextRetryAt()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);
        item.MarkAsProcessing();

        item.MarkAsFailed("SMTP timeout");

        item.Status.Should().Be(EmailQueueStatus.Failed);
        item.RetryCount.Should().Be(1);
        item.ErrorMessage.Should().Be("SMTP timeout");
        item.FailedAt.Should().NotBeNull();
        item.NextRetryAt.Should().NotBeNull();
        // First retry delay is 1 minute
        item.NextRetryAt!.Value.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(1), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void MarkAsFailed_SecondRetry_UsesExponentialBackoff()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);

        // First failure
        item.MarkAsProcessing();
        item.MarkAsFailed("Error 1");

        // Second failure
        item.MarkAsProcessing();
        item.MarkAsFailed("Error 2");

        item.Status.Should().Be(EmailQueueStatus.Failed);
        item.RetryCount.Should().Be(2);
        // Second retry delay is 5 minutes
        item.NextRetryAt!.Value.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void MarkAsFailed_ExceedsMaxRetries_MovesToDeadLetter()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);

        // Exhaust all 3 retries
        for (var i = 0; i < 3; i++)
        {
            item.MarkAsProcessing();
            item.MarkAsFailed($"Error {i + 1}");
        }

        item.Status.Should().Be(EmailQueueStatus.DeadLetter);
        item.RetryCount.Should().Be(3);
        item.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsDeadLetter_SetsStatusAndClearsNextRetry()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);
        item.MarkAsProcessing();
        item.MarkAsFailed("Error");

        item.MarkAsDeadLetter();

        item.Status.Should().Be(EmailQueueStatus.DeadLetter);
        item.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void ResetToPending_FromDeadLetter_ResetsStatus()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);
        item.MarkAsProcessing();
        item.MarkAsFailed("Error");
        item.MarkAsDeadLetter();

        item.ResetToPending();

        item.Status.Should().Be(EmailQueueStatus.Pending);
        item.NextRetryAt.Should().BeNull();
        item.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ResetToPending_FromFailed_ResetsStatus()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);
        item.MarkAsProcessing();
        item.MarkAsFailed("Error");

        item.ResetToPending();

        item.Status.Should().Be(EmailQueueStatus.Pending);
    }

    [Fact]
    public void ResetToPending_FromPending_ThrowsInvalidOperationException()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);

        var act = () => item.ResetToPending();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResetToPending_FromSent_ThrowsInvalidOperationException()
    {
        var item = EmailQueueItem.Create(_userId, ToAddress, Subject, HtmlBody);
        item.MarkAsProcessing();
        item.MarkAsSent(DateTime.UtcNow);

        var act = () => item.ResetToPending();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reconstitute_RestoresAllProperties()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var processedAt = DateTime.UtcNow;

        var item = EmailQueueItem.Reconstitute(
            id, userId, "user@test.com", "Subject", "<html/>",
            EmailQueueStatus.Sent, 1, 3, null, null,
            createdAt, processedAt, null);

        item.Id.Should().Be(id);
        item.UserId.Should().Be(userId);
        item.To.Should().Be("user@test.com");
        item.Status.Should().Be(EmailQueueStatus.Sent);
        item.RetryCount.Should().Be(1);
        item.CreatedAt.Should().Be(createdAt);
        item.ProcessedAt.Should().Be(processedAt);
    }
}
