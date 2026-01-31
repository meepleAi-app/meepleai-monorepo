namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// DTO for two-factor authentication status.
/// </summary>
internal class TwoFactorStatusDto
{
    public bool IsEnabled { get; set; }
    public DateTime? EnabledAt { get; set; }
    public int UnusedBackupCodesCount { get; set; }
}
