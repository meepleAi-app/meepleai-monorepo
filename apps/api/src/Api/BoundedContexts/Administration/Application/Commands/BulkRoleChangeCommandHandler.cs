using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for bulk role change operations.
/// Changes role for multiple users in a single transaction.
/// </summary>
internal class BulkRoleChangeCommandHandler : ICommandHandler<BulkRoleChangeCommand, BulkOperationResult>
{
    private const int MaxBulkSize = 1000;

    // ADM-002 + ADM-004: Role hierarchy levels and role-based batch size limits
    private static readonly Dictionary<string, int> RoleLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        { "user", 0 }, { "creator", 1 }, { "editor", 2 }, { "admin", 3 }, { "superadmin", 4 }
    };

    private static readonly Dictionary<string, int> MaxBulkSizeByRole = new(StringComparer.OrdinalIgnoreCase)
    {
        { "superadmin", 1000 },
        { "admin", 100 },
    };

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

        // ADM-002: Load requester to enforce privilege checks
        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken)
            .ConfigureAwait(false);
        if (requester is null)
            throw new DomainException($"Requester {command.RequesterId} not found");

        // ADM-004: Role-based batch size limit (Admin: max 100, SuperAdmin: max 1000)
        var allowedBulkSize = MaxBulkSizeByRole.GetValueOrDefault(requester.Role.Value, 100);
        if (distinctUserIds.Count > allowedBulkSize)
            throw new ForbiddenException(
                $"Bulk role change of {distinctUserIds.Count} users exceeds your role limit of {allowedBulkSize}. Contact a SuperAdmin for larger operations.");

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

        // ADM-002: Role hierarchy check — cannot assign role >= requester's own level
        var requesterLevel = RoleLevels.GetValueOrDefault(requester.Role.Value, 0);
        var targetLevel = RoleLevels.GetValueOrDefault(command.NewRole, 0);
        if (targetLevel >= requesterLevel)
            throw new ForbiddenException(
                $"Cannot bulk assign role '{command.NewRole}': you can only assign roles below your own privilege level");

        // ADM-002: Minimum SuperAdmin guard for bulk demotion (skip if assigning superadmin)
        if (!command.NewRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
        {
            var superAdminCount = await _userRepository.CountByRoleAsync(
                "superadmin", cancellationToken).ConfigureAwait(false);
            var superAdminsInBatch = 0;
            foreach (var uid in distinctUserIds)
            {
                var u = await _userRepository.GetByIdAsync(uid, cancellationToken).ConfigureAwait(false);
                if (u?.Role.Value.Equals("superadmin", StringComparison.OrdinalIgnoreCase) == true)
                    superAdminsInBatch++;
            }
            if (superAdminCount - superAdminsInBatch < 1)
                throw new ForbiddenException(
                    "Bulk operation would demote all SuperAdmins. The system requires at least one SuperAdmin.");
        }

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
                    // Sanitize error: do not leak role details (e.g. SuperAdmin status)
                    errors.Add($"Cannot change role for user {userId}");
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
            throw new DomainException("Bulk role change failed due to an internal error.", ex);
        }
#pragma warning restore CA1031
    }
}
