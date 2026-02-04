# ApproveDocumentForRagProcessingCommand - Implementation Summary

**Issue**: #3533
**Status**: ✅ Completed
**Date**: 2026-02-04

## Overview

Implemented the complete CQRS command pipeline for approving documents for RAG processing, following the established MeepleAI patterns and architecture guidelines.

## Files Created

### 1. Command Definition
**File**: `Apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ApproveDocumentForRagProcessingCommand.cs`

```csharp
internal record ApproveDocumentForRagProcessingCommand(
    Guid DocumentId,
    Guid ApprovedBy,
    string? Notes = null) : ICommand<Unit>;
```

**Key Aspects**:
- Implements `ICommand<Unit>` for commands that don't return a value
- DocumentId: The document to approve
- ApprovedBy: Audit trail tracking who approved the document
- Notes: Optional approval notes (max 500 chars)

### 2. Command Validator
**File**: `Apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ApproveDocumentForRagProcessingCommandValidator.cs`

**Validation Rules**:
- ✅ DocumentId cannot be empty
- ✅ ApprovedBy cannot be empty (required for audit trail)
- ✅ Notes limited to 500 characters
- ✅ Notes only validated if provided

### 3. Command Handler
**File**: `Apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ApproveDocumentForRagProcessingCommandHandler.cs`

**Execution Flow**:
1. Load document by ID from repository
2. Call domain method: `document.Approve(approvedBy, notes)`
3. Save changes via UnitOfWork
4. Publish `DocumentApprovedForRagEvent` domain event
5. Return Unit.Value

**Key Characteristics**:
- Follows established handler pattern from `ApproveDeleteRequestCommandHandler`
- All async operations use `.ConfigureAwait(false)`
- Comprehensive logging at key steps
- Null-checking on command parameter
- Publishes domain events for event-driven architecture

### 4. Domain Event
**File**: `Apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/DocumentApprovedForRagEvent.cs`

```csharp
public record DocumentApprovedForRagEvent(
    Guid DocumentId,
    Guid SharedGameId,
    Guid PdfDocumentId,
    Guid ApprovedBy,
    DateTime ApprovedAt) : INotification;
```

**Event Data**:
- DocumentId: The approved document
- SharedGameId: The game this document belongs to
- PdfDocumentId: Reference to the PDF
- ApprovedBy: Admin who approved
- ApprovedAt: Timestamp of approval

## Domain Model Integration

The implementation leverages existing domain methods on `SharedGameDocument`:

```csharp
public void Approve(Guid approvedBy, string? notes = null)
{
    if (_approvalStatus != DocumentApprovalStatus.Pending)
        throw new InvalidOperationException($"Cannot approve document with status {_approvalStatus}");

    if (approvedBy == Guid.Empty)
        throw new ArgumentException("ApprovedBy cannot be empty", nameof(approvedBy));

    _approvalStatus = DocumentApprovalStatus.Approved;
    _approvedBy = approvedBy;
    _approvedAt = DateTime.UtcNow;
    _approvalNotes = notes;
}
```

**Safety Guarantees**:
- Only Pending documents can be approved
- Prevents re-approval of approved/rejected documents
- Validates approver ID
- Sets server-side timestamp

## CQRS Pattern Compliance

✅ **Command**: Immutable record with clear intent
✅ **Validator**: FluentValidation with specific rules
✅ **Handler**: Implements `ICommandHandler<TCommand, TResponse>`
✅ **Event**: Domain event published after state change
✅ **Logging**: Structured logging at key steps
✅ **Repository**: Uses `ISharedGameDocumentRepository`
✅ **Unit of Work**: Uses `IUnitOfWork` for transaction management

## Usage Pattern in Endpoints

The command is used via `IMediator.Send()` in endpoint handlers:

```csharp
private static async Task<IResult> HandleApproveDocument(
    Guid documentId,
    ApproveDocumentRequest request,
    IMediator mediator,
    CancellationToken ct)
{
    var command = new ApproveDocumentForRagProcessingCommand(
        documentId,
        request.ApprovedBy,
        request.Notes);

    try
    {
        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}
```

## Build Status

✅ **Build**: Successful
✅ **Compilation**: No errors or warnings
✅ **Dependencies**: All correctly resolved

## Testing Recommendations

### Unit Tests
- ✅ Validator: Verify all validation rules
- ✅ Handler: Test successful approval flow
- ✅ Handler: Test document not found scenario
- ✅ Handler: Test invalid state transitions
- ✅ Domain: Test Approve() method boundary conditions

### Integration Tests
- ✅ Database: Verify approval status persisted
- ✅ Event: Verify domain event published
- ✅ Repository: Test document retrieval and updates

### E2E Tests
- ✅ Endpoint: POST /documents/{id}/approve
- ✅ Authorization: Admin-only access
- ✅ Response validation: 204 No Content on success
- ✅ Error handling: 404 for missing document, 400 for invalid state

## Related Components

**Dependencies**:
- `ISharedGameDocumentRepository`: Get/update documents
- `IUnitOfWork`: Transaction management
- `IMediator`: Event publishing
- `ILogger`: Structured logging
- `FluentValidation`: Command validation

**References**:
- `SharedGameDocument.Approve()`: Domain method
- `DocumentApprovalStatus`: Enum for approval states
- `SharedGameDocumentRepository`: Persistence implementation

## Architecture Alignment

✅ **Bounded Context**: SharedGameCatalog
✅ **Layer**: Application (CQRS pattern)
✅ **DDD Principles**: Domain method invocation
✅ **Event Sourcing**: Domain event published
✅ **Audit Trail**: ApprovedBy tracked
✅ **Error Handling**: Domain exceptions thrown

## Next Steps

After this implementation, the following tasks depend on this completion:

1. **Task #5**: Create GetFilteredSharedGamesQuery (dependencies: none)
2. **Task #6**: Create GetApprovalQueueQuery (dependencies: none)
3. **Task #7**: Add GET /api/v1/admin/shared-games endpoint (dependencies: #5)
4. **Task #8**: Add POST /documents/{docId}/approve endpoint (dependencies: **#4** ✅)
5. **Task #9**: Add GET /approval-queue endpoint (dependencies: #6)
6. **Task #10**: Write integration tests (dependencies: all)

## Quality Metrics

| Metric | Status |
|--------|--------|
| Build Successful | ✅ |
| No Compilation Errors | ✅ |
| CQRS Pattern Compliance | ✅ |
| Domain Integration | ✅ |
| Logging Coverage | ✅ |
| Error Handling | ✅ |
| Validator Coverage | ✅ |
| Event Publishing | ✅ |

## Files Summary

| File | Location | Status |
|------|----------|--------|
| Command | `Application/Commands/ApproveDocumentForRagProcessingCommand.cs` | ✅ Created |
| Validator | `Application/Commands/ApproveDocumentForRagProcessingCommandValidator.cs` | ✅ Created |
| Handler | `Application/Commands/ApproveDocumentForRagProcessingCommandHandler.cs` | ✅ Created |
| Event | `Domain/Events/DocumentApprovedForRagEvent.cs` | ✅ Created |

## Notes

- Implementation follows the exact pattern of existing commands like `ApproveDeleteRequestCommand`
- Uses proper async/await with ConfigureAwait(false) for efficiency
- Event is published for downstream event handlers to trigger RAG processing
- All validation happens both at command validator and domain model levels
- Audit trail properly maintained via ApprovedBy and ApprovedAt fields
