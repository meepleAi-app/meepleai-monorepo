namespace Api.Services;

internal interface IPasswordResetService
{
    Task<bool> RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task<bool> ValidateResetTokenAsync(string token, CancellationToken ct = default);
    Task<(bool Success, Guid? UserId)> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default);
}
