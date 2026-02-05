namespace Api.Services;

internal interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, CancellationToken ct = default);
    Task SendTwoFactorDisabledEmailAsync(string toEmail, string toName, bool wasAdminOverride, CancellationToken ct = default);

    // ISSUE-3071: Email verification
    Task SendVerificationEmailAsync(string toEmail, string toName, string verificationToken, CancellationToken ct = default);

    // ISSUE-918: Email delivery integration for reports
    Task SendReportEmailAsync(
        IReadOnlyList<string> recipients,
        string reportName,
        string reportDescription,
        byte[] reportContent,
        string fileName,
        long fileSizeBytes,
        CancellationToken ct = default);

    Task SendReportFailureEmailAsync(
        IReadOnlyList<string> recipients,
        string reportName,
        string errorMessage,
        CancellationToken ct = default);

    // ISSUE-2739: Share request notifications
    Task SendShareRequestCreatedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string contributionType,
        Guid shareRequestId,
        CancellationToken ct = default);

    Task SendShareRequestApprovedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        Guid sharedGameId,
        Guid userId,
        CancellationToken ct = default);

    Task SendShareRequestRejectedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string reason,
        CancellationToken ct = default);

    Task SendShareRequestChangesRequestedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string feedback,
        Guid shareRequestId,
        CancellationToken ct = default);

    // ISSUE-2740: Admin share request digest email
    Task SendAdminShareRequestDigestEmailAsync(
        string toEmail,
        string toName,
        int totalPending,
        int oldestPendingDays,
        int createdToday,
        Dictionary<string, int> pendingByType,
        string reviewQueueUrl,
        CancellationToken ct = default);

    // ISSUE-2741: Badge earned emails
    Task SendBadgeEarnedEmailAsync(
        string toEmail,
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string badgeTierColor,
        string profileUrl,
        string shareText,
        CancellationToken ct = default);

    Task SendMilestoneBadgeEarnedEmailAsync(
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
        CancellationToken ct = default);

    // ISSUE-2742: Rate limit cooldown email
    Task SendCooldownEndedEmailAsync(
        string toEmail,
        string userName,
        int remainingMonthly,
        int remainingPending,
        string libraryUrl,
        CancellationToken ct = default);

    // ISSUE-2886: User suspension emails
    Task SendAccountSuspendedEmailAsync(
        string toEmail,
        string userName,
        string? reason,
        CancellationToken ct = default);

    Task SendAccountReactivatedEmailAsync(
        string toEmail,
        string userName,
        CancellationToken ct = default);

    // ISSUE-3676: Account lockout notification
    Task SendAccountLockedEmailAsync(
        string toEmail,
        string userName,
        int failedAttempts,
        DateTime lockedUntil,
        string? ipAddress,
        CancellationToken ct = default);
}
