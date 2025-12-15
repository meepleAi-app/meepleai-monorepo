namespace Api.Services;

internal interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, CancellationToken ct = default);
    Task SendTwoFactorDisabledEmailAsync(string toEmail, string toName, bool wasAdminOverride, CancellationToken ct = default);

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
}
