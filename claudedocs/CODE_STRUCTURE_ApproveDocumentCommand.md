# Code Structure - ApproveDocumentForRagProcessingCommand

## CQRS Pipeline Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                      CQRS Pipeline                             │
└─────────────────────────────────────────────────────────────────┘

Endpoint Handler
    ↓
    ├─→ Create Command
    │   ApproveDocumentForRagProcessingCommand(DocumentId, ApprovedBy, Notes)
    │
    ├─→ IMediator.Send(command)
    │
    ├─→ Validator (FluentValidation)
    │   └─→ DocumentId not empty
    │   └─→ ApprovedBy not empty
    │   └─→ Notes ≤ 500 chars
    │
    ├─→ Handler (ICommandHandler)
    │   └─→ Load document from repository
    │   └─→ Call document.Approve(approvedBy, notes)
    │   └─→ Save via UnitOfWork
    │   └─→ Publish event
    │
    └─→ Event (INotification)
        DocumentApprovedForRagEvent
        └─→ Event Handlers (subscribed via MediatR)
```

## File Structure

### 1. Command (ICommand<Unit>)
**File**: `ApproveDocumentForRagProcessingCommand.cs`

```csharp
internal record ApproveDocumentForRagProcessingCommand(
    Guid DocumentId,
    Guid ApprovedBy,
    string? Notes = null) : ICommand<Unit>;
```

**Key Features**:
- Immutable record (value object)
- Implements ICommand<Unit> interface
- Three parameters: document to approve, who approves, optional notes
- Used throughout CQRS pipeline

**Usage in Endpoint**:
```csharp
var command = new ApproveDocumentForRagProcessingCommand(
    documentId: documentId,
    approvedBy: userId,
    notes: approvalNotes);

await mediator.Send(command);
```

### 2. Validator (FluentValidation)
**File**: `ApproveDocumentForRagProcessingCommandValidator.cs`

```csharp
internal sealed class ApproveDocumentForRagProcessingCommandValidator
    : AbstractValidator<ApproveDocumentForRagProcessingCommand>
{
    public ApproveDocumentForRagProcessingCommandValidator()
    {
        // Rule 1: DocumentId not empty
        RuleFor(x => x.DocumentId)
            .NotEqual(Guid.Empty)
            .WithMessage("DocumentId is required");

        // Rule 2: ApprovedBy not empty (audit trail)
        RuleFor(x => x.ApprovedBy)
            .NotEqual(Guid.Empty)
            .WithMessage("ApprovedBy is required for audit trail");

        // Rule 3: Notes optional but max 500 chars if provided
        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
```

**Validation Flow**:
1. Auto-registered in DI container
2. Invoked by MediatR pipeline before handler
3. Throws ValidationException if any rule fails
4. Return structured validation errors to client

### 3. Handler (ICommandHandler<T, Unit>)
**File**: `ApproveDocumentForRagProcessingCommandHandler.cs`

```csharp
internal sealed class ApproveDocumentForRagProcessingCommandHandler
    : ICommandHandler<ApproveDocumentForRagProcessingCommand, Unit>
{
    // Dependencies
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<...> _logger;

    // Constructor with null-checking
    public ApproveDocumentForRagProcessingCommandHandler(...)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(...);
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(...);
        _mediator = mediator ?? throw new ArgumentNullException(...);
        _logger = logger ?? throw new ArgumentNullException(...);
    }

    // Handler execution
    public async Task<Unit> Handle(
        ApproveDocumentForRagProcessingCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate command not null
        ArgumentNullException.ThrowIfNull(command);

        // 2. Log operation
        _logger.LogInformation("Approving document: {DocumentId}", command.DocumentId);

        // 3. Load document
        var document = await _documentRepository
            .GetByIdAsync(command.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
            throw new InvalidOperationException(
                $"Document with ID {command.DocumentId} not found");

        // 4. Call domain method (validates state)
        document.Approve(command.ApprovedBy, command.Notes);

        // 5. Save to database
        await _unitOfWork.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        // 6. Publish domain event
        await _mediator.Publish(
            new DocumentApprovedForRagEvent(
                command.DocumentId,
                document.SharedGameId,
                document.PdfDocumentId,
                command.ApprovedBy,
                document.ApprovedAt ?? DateTime.UtcNow),
            cancellationToken)
            .ConfigureAwait(false);

        // 7. Log completion
        _logger.LogInformation(
            "Document approved: {DocumentId} by {ApprovedBy}",
            command.DocumentId, command.ApprovedBy);

        // 8. Return success
        return Unit.Value;
    }
}
```

**Execution Sequence**:
1. Load aggregate (SharedGameDocument)
2. Invoke domain behavior (Approve method)
3. Persist changes (UnitOfWork.SaveChanges)
4. Publish event (IMediator.Publish)
5. Return control to endpoint

### 4. Event (INotification)
**File**: `DocumentApprovedForRagEvent.cs`

```csharp
public record DocumentApprovedForRagEvent(
    Guid DocumentId,
    Guid SharedGameId,
    Guid PdfDocumentId,
    Guid ApprovedBy,
    DateTime ApprovedAt) : INotification;
```

**Event Properties**:
- DocumentId: The approved document
- SharedGameId: The game context
- PdfDocumentId: PDF reference
- ApprovedBy: Audit trail - who approved
- ApprovedAt: Audit trail - when approved

**Event Subscribers** (examples):
- RAG Processing Service: Trigger document indexing
- Notification Service: Send approval confirmation
- Audit Logger: Record approval event

## Domain Integration

### SharedGameDocument Entity
The handler calls the domain method:

```csharp
// In domain/entity
public void Approve(Guid approvedBy, string? notes = null)
{
    // Validation: Only pending documents can be approved
    if (_approvalStatus != DocumentApprovalStatus.Pending)
        throw new InvalidOperationException(
            $"Cannot approve document with status {_approvalStatus}");

    // Validation: Approver must be valid
    if (approvedBy == Guid.Empty)
        throw new ArgumentException("ApprovedBy cannot be empty", nameof(approvedBy));

    // State change
    _approvalStatus = DocumentApprovalStatus.Approved;
    _approvedBy = approvedBy;
    _approvedAt = DateTime.UtcNow;
    _approvalNotes = notes;
}
```

## Dependency Injection

All components auto-register in DI container:

```csharp
// In DI configuration (MediatR auto-discovery)
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Validator auto-discovered
services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Repository registered
services.AddScoped<ISharedGameDocumentRepository, SharedGameDocumentRepository>();

// UnitOfWork registered
services.AddScoped<IUnitOfWork, UnitOfWork>();

// Logging auto-available via ILogger
services.AddLogging();
```

## Error Handling

The implementation handles three error scenarios:

### 1. Command Validation Errors
```csharp
// Thrown by validator
ValidationException
├── DocumentId is required
├── ApprovedBy is required
└── Notes cannot exceed 500 characters

// Returns to client: 400 Bad Request
```

### 2. Domain Errors
```csharp
// Thrown by handler or domain
InvalidOperationException
├── Document with ID {id} not found
└── Cannot approve document with status {status}

// Returns to client: 400 Bad Request or 404 Not Found
```

### 3. Infrastructure Errors
```csharp
// Thrown by repository/database
DbUpdateException, TimeoutException, etc.

// Returns to client: 500 Internal Server Error
```

## Async/Await Pattern

All operations follow best practices:

```csharp
// ConfigureAwait(false) prevents unnecessary thread switching
await _documentRepository.GetByIdAsync(...).ConfigureAwait(false);
await _unitOfWork.SaveChangesAsync(...).ConfigureAwait(false);
await _mediator.Publish(...).ConfigureAwait(false);
```

## Logging

Three logging points for observability:

```csharp
// 1. Entry point
_logger.LogInformation("Approving document for RAG processing: {DocumentId}",
    command.DocumentId);

// 2. Success point
_logger.LogInformation("Document approved successfully: {DocumentId} by {ApprovedBy}",
    command.DocumentId, command.ApprovedBy);

// Errors logged automatically by framework
```

## Testing Strategy

### Unit Tests
```csharp
// Test validator
[Fact]
public void Validator_ShouldFail_WhenDocumentIdEmpty()
{
    var cmd = new ApproveDocumentForRagProcessingCommand(
        Guid.Empty, userId, "notes");
    var validator = new ApproveDocumentForRagProcessingCommandValidator();
    var result = validator.Validate(cmd);
    Assert.False(result.IsValid);
}

// Test handler
[Fact]
public async Task Handler_ShouldThrow_WhenDocumentNotFound()
{
    var handler = new ApproveDocumentForRagProcessingCommandHandler(
        mockRepository, mockUnitOfWork, mockMediator, mockLogger);
    var cmd = new ApproveDocumentForRagProcessingCommand(
        Guid.NewGuid(), userId, "notes");

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => handler.Handle(cmd, CancellationToken.None));
}
```

### Integration Tests
```csharp
// Test full pipeline
[Fact]
public async Task Pipeline_ShouldApproveDocument_AndPublishEvent()
{
    // Arrange: Create document in database
    var document = SharedGameDocument.Create(...);
    await repo.AddAsync(document);
    await uow.SaveChangesAsync();

    // Act: Send command through MediatR
    var cmd = new ApproveDocumentForRagProcessingCommand(
        document.Id, adminId, "Good to process");
    await mediator.Send(cmd);

    // Assert: Verify document updated
    var updated = await repo.GetByIdAsync(document.Id);
    Assert.Equal(DocumentApprovalStatus.Approved, updated.ApprovalStatus);
    Assert.Equal(adminId, updated.ApprovedBy);
}
```

## Performance Characteristics

- **Command Creation**: O(1) - simple record instantiation
- **Validation**: O(n) - where n = number of rules (typically 3)
- **Handler Execution**: O(1) - single document lookup + save
- **Event Publishing**: O(m) - where m = number of subscribers
- **Total Latency**: <100ms typical (I/O bound on database)

## References

- **Pattern**: CQRS (Command Query Responsibility Segregation)
- **Framework**: MediatR for command/event dispatching
- **Validation**: FluentValidation for declarative rules
- **Repository**: Generic IRepository<T> pattern
- **Unit of Work**: Manages transaction boundaries
- **Logging**: Structured logging via ILogger<T>
