using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for bulk password reset operations.
/// Resets passwords for multiple users in a single transaction.
/// </summary>
internal class BulkPasswordResetCommandHandler : ICommandHandler<BulkPasswordResetCommand, BulkOperationResult>
{
    private const int MaxBulkSize = 1000;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkPasswordResetCommandHandler> _logger;

    public BulkPasswordResetCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<BulkPasswordResetCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkOperationResult> Handle(BulkPasswordResetCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Validation: Check bulk size limit
        if (command.UserIds == null || command.UserIds.Count == 0)
        {
            throw new DomainException("UserIds list cannot be null or empty");
        }

        if (command.UserIds.Count > MaxBulkSize)
        {
            throw new DomainException($"Bulk operation exceeds maximum limit of {MaxBulkSize} users");
        }

        // Validation: Check for duplicate user IDs
        var distinctUserIds = command.UserIds.Distinct().ToList();
        if (distinctUserIds.Count != command.UserIds.Count)
        {
            _logger.LogWarning("Bulk password reset request contains {DuplicateCount} duplicate user IDs",
                command.UserIds.Count - distinctUserIds.Count);
        }

        // Validation: Password requirements
        if (string.IsNullOrWhiteSpace(command.NewPassword) || command.NewPassword.Length < 8)
        {
            throw new DomainException("Password must be at least 8 characters long");
        }

        _logger.LogInformation("Admin {RequesterId} initiating bulk password reset for {Count} users",
            command.RequesterId, distinctUserIds.Count);

        var errors = new List<string>();
        var successCount = 0;

        try
        {
            // Create password hash once (same for all users)
            var newPasswordHash = PasswordHash.Create(command.NewPassword);

            // Process all users in a single transaction
            foreach (var userId in distinctUserIds)
            {
                try
                {
                    var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
                    if (user == null)
                    {
                        errors.Add($"User {userId} not found");
                        continue;
                    }

                    user.UpdatePassword(newPasswordHash);
                    await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
                    successCount++;
                }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
                // HANDLER BOUNDARY: BULK OPERATION PATTERN - Individual password reset failure handling
                // Catches all exceptions during password reset (validation, DB constraints, etc.)
                // to collect errors without stopping batch processing. Each failure is logged
                // and added to error list for reporting. Allows partial success in bulk operation.
#pragma warning restore S125
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
                    errors.Add($"User {userId}: {ex.Message}");
                }
#pragma warning restore CA1031
            }

            // Commit transaction if any success
            if (successCount > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Bulk password reset completed: {SuccessCount} succeeded, {FailedCount} failed",
                    successCount, errors.Count);
            }
            else
            {
                _logger.LogWarning("Bulk password reset failed: no users updated");
            }

            return new BulkOperationResult(
                TotalRequested: command.UserIds.Count,
                SuccessCount: successCount,
                FailedCount: errors.Count,
                Errors: errors
            );
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - Wraps unexpected infrastructure failures
        // Generic catch wraps unexpected exceptions (DB, network, memory) in DomainException
        // for consistent API error handling. Logs with full context before wrapping.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during bulk password reset");
            throw new DomainException($"Bulk password reset failed: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }
}
