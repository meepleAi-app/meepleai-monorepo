using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for bulk approval of share requests.
/// Issue #2893: All-or-nothing transaction - if any approval fails, entire batch rolls back.
///
/// ARCHITECTURE DECISION: Uses strict all-or-nothing transaction semantics (differs from BulkPasswordReset/BulkRoleChange
/// which allow partial success). Rationale: Share request approval is a coordinated editorial decision where
/// partial failures could create inconsistent catalog state. Per DoD requirement: "Transaction: All or nothing".
/// </summary>
internal sealed class BulkApproveShareRequestsCommandHandler : ICommandHandler<BulkApproveShareRequestsCommand, BulkOperationResult>
{
    private const int MaxBulkSize = 20;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkApproveShareRequestsCommandHandler> _logger;

    public BulkApproveShareRequestsCommandHandler(
        IShareRequestRepository shareRequestRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        ILogger<BulkApproveShareRequestsCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkOperationResult> Handle(
        BulkApproveShareRequestsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validation: Check bulk size limit (also in validator, but double-check for safety)
        if (command.ShareRequestIds.Count > MaxBulkSize)
        {
            _logger.LogWarning(
                "Bulk approve operation exceeds limit: {Count} > {MaxLimit}",
                command.ShareRequestIds.Count, MaxBulkSize);

            return new BulkOperationResult(
                TotalRequested: command.ShareRequestIds.Count,
                SuccessCount: 0,
                FailedCount: command.ShareRequestIds.Count,
                Errors: new[] { $"Bulk operation exceeds maximum limit of {MaxBulkSize} share requests" }
            );
        }

        // Remove duplicates with warning
        var distinctIds = command.ShareRequestIds.Distinct().ToList();
        if (distinctIds.Count != command.ShareRequestIds.Count)
        {
            _logger.LogWarning(
                "Bulk approve request contains {DuplicateCount} duplicate share request IDs",
                command.ShareRequestIds.Count - distinctIds.Count);
        }

        _logger.LogInformation(
            "Editor {EditorId} initiating bulk approval of {Count} share requests",
            command.EditorId, distinctIds.Count);

        var errors = new List<string>();
        var successCount = 0;

        try
        {
            // Batch retrieval optimization: Get all share requests in single query (Issue #2893 N+1 fix)
            var shareRequestsDict = await _shareRequestRepository.GetByIdsForUpdateAsync(
                distinctIds,
                cancellationToken).ConfigureAwait(false);

            // Validate all IDs found (all-or-nothing requirement)
            var missingIds = distinctIds.Except(shareRequestsDict.Keys).ToList();
            if (missingIds.Count > 0)
            {
                var missingIdsStr = string.Join(", ", missingIds);
                throw new NotFoundException(
                    "ShareRequest",
                    $"Share requests not found: {missingIdsStr}");
            }

            // Process all share requests in a single transaction (all-or-nothing)
            foreach (var shareRequestId in distinctIds)
            {
                var shareRequest = shareRequestsDict[shareRequestId];

                // Approve the request (domain validates and raises events)
                // Will throw if: not in InReview status, lock expired, reviewer mismatch, etc.
                shareRequest.Approve(
                    command.EditorId,
                    command.TargetSharedGameId,
                    command.AdminNotes);

                _shareRequestRepository.Update(shareRequest);

                // Create audit log entry for each approval
                var auditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
                    id: Guid.NewGuid(),
                    userId: command.EditorId,
                    action: "share_request_approved",
                    resource: "ShareRequest",
                    result: "success",
                    resourceId: shareRequestId.ToString(),
                    details: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        shareRequestId,
                        targetSharedGameId = command.TargetSharedGameId,
                        adminNotes = command.AdminNotes,
                        approvedAt = DateTime.UtcNow
                    }),
                    ipAddress: "editor-action"
                );

                await _auditLogRepository.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
                successCount++;
            }

            // All-or-nothing: Commit only if all succeeded
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Bulk approval completed successfully: {Count} share requests approved by editor {EditorId}",
                successCount, command.EditorId);

            return new BulkOperationResult(
                TotalRequested: command.ShareRequestIds.Count,
                SuccessCount: successCount,
                FailedCount: 0,
                Errors: errors
            );
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: ALL-OR-NOTHING TRANSACTION PATTERN
        // Any exception triggers automatic transaction rollback via UnitOfWork.
        // Catches domain exceptions (invalid state, lock expired) and infrastructure exceptions (DB, network)
        // to return consistent BulkOperationResult with full failure indication.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Bulk approval failed for editor {EditorId} - transaction rolled back. {Count} share requests not approved.",
                command.EditorId, distinctIds.Count);

            // All-or-nothing failure
            return new BulkOperationResult(
                TotalRequested: command.ShareRequestIds.Count,
                SuccessCount: 0,
                FailedCount: command.ShareRequestIds.Count,
                Errors: new[] { $"Bulk operation failed: {ex.Message}" }
            );
        }
#pragma warning restore CA1031
    }
}
