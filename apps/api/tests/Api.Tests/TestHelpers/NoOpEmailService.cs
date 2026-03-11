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

    // ISSUE-3071: Email verification
    public Task SendVerificationEmailAsync(string toEmail, string toName, string verificationToken, CancellationToken ct = default)
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

    public Task SendBadgeEarnedEmailAsync(
        string toEmail,
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string badgeTierColor,
        string profileUrl,
        string shareText,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendMilestoneBadgeEarnedEmailAsync(
        string toEmail,
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string milestoneMessage,
        int totalContributions,
        string profileUrl,
        string leaderboardUrl,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendCooldownEndedEmailAsync(
        string toEmail,
        string userName,
        int remainingMonthly,
        int remainingPending,
        string libraryUrl,
        CancellationToken ct = default)
        => Task.CompletedTask;

    // ISSUE-2886: User suspension emails
    public Task SendAccountSuspendedEmailAsync(
        string toEmail,
        string userName,
        string? reason,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendAccountReactivatedEmailAsync(
        string toEmail,
        string userName,
        CancellationToken ct = default)
        => Task.CompletedTask;

    // ISSUE-3676: Account lockout notification
    public Task SendAccountLockedEmailAsync(
        string toEmail,
        string userName,
        int failedAttempts,
        DateTime lockedUntil,
        string? ipAddress,
        CancellationToken ct = default)
        => Task.CompletedTask;

    // ISSUE-3668: Game proposal lifecycle notification emails
    public Task SendShareRequestReviewStartedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        Guid shareRequestId,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendShareRequestKbMergedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        Guid sharedGameId,
        CancellationToken ct = default)
        => Task.CompletedTask;

    // ISSUE-4159: Shared game approval workflow - Admin notification
    public Task SendSharedGameSubmittedForApprovalEmailAsync(
        string toEmail,
        string toName,
        string gameTitle,
        string submitterName,
        Guid gameId,
        CancellationToken ct = default)
        => Task.CompletedTask;

    // ISSUE-4220: PDF notification emails
    public Task SendPdfReadyEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        Guid pdfDocumentId,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendPdfFailedEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        string errorMessage,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendPdfRetryEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        int retryCount,
        CancellationToken ct = default)
        => Task.CompletedTask;

    // ISSUE-124: Invitation system emails
    public Task SendInvitationEmailAsync(
        string toEmail,
        string role,
        string inviteLink,
        string invitedByName,
        CancellationToken ct = default)
        => Task.CompletedTask;

    // ISSUE-4417: Raw email sending for queue processor
    public Task SendRawEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
        => Task.CompletedTask;
}
