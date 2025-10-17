namespace Api.Services;

public interface IPasswordResetService
{
    Task<bool> RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task<bool> ValidateResetTokenAsync(string token, CancellationToken ct = default);
    Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default);
}
