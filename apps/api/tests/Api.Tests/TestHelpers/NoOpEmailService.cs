using Api.Services;

namespace Api.Tests.TestHelpers;

/// <summary>
/// No-op implementation of IEmailService for testing.
/// All methods complete successfully without sending actual emails.
/// </summary>
internal class NoOpEmailService : IEmailService
{
    public Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendTwoFactorDisabledEmailAsync(string toEmail, string toName, bool wasAdminOverride, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendReportEmailAsync(
        IReadOnlyList<string> recipients,
        string reportName,
        string reportDescription,
        byte[] reportContent,
        string fileName,
        long fileSizeBytes,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendReportFailureEmailAsync(
        IReadOnlyList<string> recipients,
        string reportName,
        string errorMessage,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendShareRequestCreatedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string contributionType,
        Guid shareRequestId,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendShareRequestApprovedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        Guid sharedGameId,
        Guid userId,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendShareRequestRejectedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string reason,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendShareRequestChangesRequestedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string feedback,
        Guid shareRequestId,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendAdminShareRequestDigestEmailAsync(
        string toEmail,
        string toName,
        int totalPending,
        int oldestPendingDays,
        int createdToday,
        Dictionary<string, int> pendingByType,
        string reviewQueueUrl,
        CancellationToken ct = default)
        => Task.CompletedTask;
}
