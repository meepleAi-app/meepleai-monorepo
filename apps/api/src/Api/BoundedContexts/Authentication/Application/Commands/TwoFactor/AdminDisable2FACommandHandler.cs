using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Handler for AdminDisable2FACommand.
/// DDD: Admin override to disable 2FA for users who lost authenticator + backup codes.
/// Requires admin authorization and sends email notification to affected user.
/// </summary>
internal class AdminDisable2FACommandHandler : ICommandHandler<AdminDisable2FACommand, AdminDisable2FAResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdminDisable2FACommandHandler> _logger;

    public AdminDisable2FACommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<AdminDisable2FACommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminDisable2FAResult> Handle(AdminDisable2FACommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            // Verify admin user exists and has admin role
            var adminUser = await _userRepository.GetByIdAsync(command.AdminUserId, cancellationToken).ConfigureAwait(false);
            if (adminUser == null)
            {
                _logger.LogWarning("Admin user {AdminUserId} not found", command.AdminUserId);
                return new AdminDisable2FAResult(Success: false, ErrorMessage: "Admin user not found");
            }

            if (!adminUser.Role.IsAdmin())
            {
                _logger.LogWarning("User {AdminUserId} attempted admin 2FA disable without admin role", command.AdminUserId);
                return new AdminDisable2FAResult(Success: false, ErrorMessage: "Unauthorized: Admin role required");
            }

            // Verify target user exists
            var targetUser = await _userRepository.GetByIdAsync(command.TargetUserId, cancellationToken).ConfigureAwait(false);
            if (targetUser == null)
            {
                _logger.LogWarning("Target user {TargetUserId} not found for admin 2FA disable", command.TargetUserId);
                return new AdminDisable2FAResult(Success: false, ErrorMessage: "Target user not found");
            }

            // Verify 2FA is enabled for target user
            if (!targetUser.IsTwoFactorEnabled)
            {
                _logger.LogWarning("2FA is not enabled for user {TargetUserId}", command.TargetUserId);
                return new AdminDisable2FAResult(Success: false, ErrorMessage: "Two-factor authentication is not enabled for this user");
            }

            // Disable 2FA with admin override flag (domain method will raise TwoFactorDisabledEvent)
            targetUser.Disable2FA(wasAdminOverride: true);

            // Persist the updated user state via repository
            await _userRepository.UpdateAsync(targetUser, cancellationToken).ConfigureAwait(false);

            // Save changes (event handler will send email notification)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Admin {AdminUserId} successfully disabled 2FA for user {TargetUserId}",
                command.AdminUserId,
                command.TargetUserId);

            return new AdminDisable2FAResult(Success: true);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception during admin 2FA disable for user {TargetUserId}", command.TargetUserId);
            return new AdminDisable2FAResult(Success: false, ErrorMessage: ex.Message);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions (DomainException) caught separately above.
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result pattern.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during admin 2FA disable for user {TargetUserId}", command.TargetUserId);
            return new AdminDisable2FAResult(Success: false, ErrorMessage: "An error occurred while disabling two-factor authentication");
        }
#pragma warning restore CA1031
    }
}
