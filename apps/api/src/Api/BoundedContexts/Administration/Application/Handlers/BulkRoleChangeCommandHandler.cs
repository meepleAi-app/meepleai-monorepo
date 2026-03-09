using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for bulk role change operations.
/// Changes role for multiple users in a single transaction.
/// </summary>
internal class BulkRoleChangeCommandHandler : ICommandHandler<BulkRoleChangeCommand, BulkOperationResult>
{
    private const int MaxBulkSize = 1000;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkRoleChangeCommandHandler> _logger;

    public BulkRoleChangeCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<BulkRoleChangeCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkOperationResult> Handle(BulkRoleChangeCommand command, CancellationToken cancellationToken)
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
            _logger.LogWarning("Bulk role change request contains {DuplicateCount} duplicate user IDs",
                command.UserIds.Count - distinctUserIds.Count);
        }

        // Validation: Role must be valid
        Role newRole;
        try
        {
            newRole = Role.Parse(command.NewRole);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: VALIDATION PATTERN - Role parsing failure handling
        // Wraps Role.Parse exceptions (ArgumentException, FormatException) in DomainException
        // for consistent API validation error response. Preserves inner exception for diagnostics.
#pragma warning restore S125
        catch (Exception ex)
        {
            throw new DomainException($"Invalid role: {command.NewRole}", ex);
        }
#pragma warning restore CA1031

        _logger.LogInformation("Admin {RequesterId} initiating bulk role change for {Count} users to role {Role}",
            command.RequesterId, distinctUserIds.Count, command.NewRole);

        var errors = new List<string>();
        var successCount = 0;

        try
        {
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

                    user.UpdateRole(newRole);
                    await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
                    successCount++;
                }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
                // HANDLER BOUNDARY: BULK OPERATION PATTERN - Individual role change failure handling
                // Catches all exceptions during role update (validation, DB constraints, etc.)
                // to collect errors without stopping batch processing. Each failure is logged
                // and added to error list for reporting. Allows partial success in bulk operation.
#pragma warning restore S125
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error changing role for user {UserId}", userId);
                    errors.Add($"User {userId}: {ex.Message}");
                }
#pragma warning restore CA1031
            }

            // Commit transaction if any success
            if (successCount > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Bulk role change completed: {SuccessCount} succeeded, {FailedCount} failed",
                    successCount, errors.Count);
            }
            else
            {
                _logger.LogWarning("Bulk role change failed: no users updated");
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
            _logger.LogError(ex, "Critical error during bulk role change");
            throw new DomainException($"Bulk role change failed: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }
}
