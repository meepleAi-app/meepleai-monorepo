namespace Api.Services.Email;

/// <summary>
/// Builds HTML email bodies for all notification types.
/// Responsible only for template rendering — no SMTP knowledge.
/// </summary>
internal interface IEmailTemplateService
{
    string BuildVerificationEmailBody(string userName, string verifyUrl);
    string BuildPasswordResetEmailBody(string userName, string resetUrl);
    string BuildTwoFactorDisabledEmailBody(string userName, bool wasAdminOverride);

    string BuildReportEmailBody(
        string reportName,
        string reportDescription,
        string fileName,
        long fileSizeBytes);

    string BuildReportFailureEmailBody(string reportName, string errorMessage);

    string BuildShareRequestCreatedEmailBody(
        string userName,
        string gameTitle,
        string contributionType,
        Guid shareRequestId);

    string BuildShareRequestApprovedEmailBody(
        string userName,
        string gameTitle,
        Guid sharedGameId,
        Guid userId);

    string BuildShareRequestRejectedEmailBody(
        string userName,
        string gameTitle,
        string reason);

    string BuildShareRequestChangesRequestedEmailBody(
        string userName,
        string gameTitle,
        string feedback,
        Guid shareRequestId);

    string BuildAdminShareRequestDigestEmailBody(
        string adminName,
        int totalPending,
        int oldestPendingDays,
        int createdToday,
        Dictionary<string, int> pendingByType,
        string reviewQueueUrl);

    string BuildBadgeEarnedEmailBody(
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string badgeTierColor,
        string profileUrl,
        string shareText);

    string BuildMilestoneBadgeEarnedEmailBody(
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string milestoneMessage,
        int totalContributions,
        string profileUrl,
        string leaderboardUrl);

    string BuildCooldownEndedEmailBody(
        string userName,
        int remainingMonthly,
        int remainingPending,
        string libraryUrl);

    string BuildAccountSuspendedEmailBody(string userName, string? reason);
    string BuildAccountReactivatedEmailBody(string userName);

    string BuildAccountLockedEmailBody(
        string userName,
        int failedAttempts,
        DateTime lockedUntil,
        string? ipAddress);

    string BuildShareRequestReviewStartedEmailBody(
        string userName,
        string gameTitle,
        Guid shareRequestId);

    string BuildShareRequestKbMergedEmailBody(
        string userName,
        string gameTitle,
        Guid sharedGameId);

    string BuildSharedGameSubmittedEmailBody(
        string adminName,
        string gameTitle,
        string submitterName,
        string reviewUrl);

    string BuildPdfReadyEmailBody(
        string userName,
        string fileName,
        Guid pdfDocumentId);

    string BuildPdfFailedEmailBody(
        string userName,
        string fileName,
        string errorMessage);

    string BuildPdfRetryEmailBody(
        string userName,
        string fileName,
        int retryCount);

    string BuildInvitationEmailBody(string role, string inviteLink, string invitedByName);

    string BuildEnhancedInvitationEmailBody(
        string displayName,
        string role,
        string setupLink,
        string invitedByName,
        string? customMessage,
        DateTime expiresAt);
}
