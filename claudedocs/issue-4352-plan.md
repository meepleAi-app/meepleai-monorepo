# Issue #4352: Backend - Bulk Import JSON Command

## Implementation Plan

**Confidence**: 95%
**Estimated Time**: 1 day
**Epic**: #4136

---

## Design Decisions

### Input Format
**Choice**: Command accepts `string JsonContent` (JSON as string)

**Rationale**:
- Clean CQRS: command contains data, handler does logic
- Easy unit testing with JSON strings
- Controller reads file → passes string to command
- Can provide detailed JSON parsing errors

**Rejected**:
- ❌ IFormFile: couples to HTTP layer
- ❌ Pre-parsed List<DTO>: loses JSON error context

### DTOs

```csharp
// Result DTO
public record BulkImportResult
{
    public int Total { get; init; }
    public int Enqueued { get; init; }
    public int Skipped { get; init; }        // Duplicates
    public int Failed { get; init; }
    public List<BulkImportError> Errors { get; init; }
}

// Error DTO
public record BulkImportError
{
    public int? BggId { get; init; }
    public string? GameName { get; init; }
    public string Reason { get; init; }
    public string ErrorType { get; init; }  // Duplicate|InvalidJson|ApiError
}

// Internal parsing DTO
internal record BggGameJsonDto
{
    public int BggId { get; init; }
    public string Name { get; init; }
}
```

### Best-Effort Strategy

```csharp
foreach (var game in parsedGames)
{
    try
    {
        // Check duplicate
        if (existingBggIds.Contains(game.BggId))
        {
            result.Skipped++;
            result.Errors.Add(/* Duplicate error */);
            continue; // Skip but continue
        }

        // Enqueue
        await _queueService.EnqueueAsync(game.BggId, ct);
        result.Enqueued++;
    }
    catch (Exception ex)
    {
        result.Failed++;
        result.Errors.Add(/* Error */);
        // Continue processing
    }
}
```

---

## Implementation Tasks

### 1. Create DTOs
**File**: `BoundedContexts/SharedGameCatalog/Application/DTOs/BulkImport/`
- [x] BulkImportResult.cs
- [x] BulkImportError.cs

### 2. Create Command
**File**: `BoundedContexts/SharedGameCatalog/Application/Commands/`
- [x] EnqueueBggBatchFromJsonCommand.cs

### 3. Create Validator
**File**: `BoundedContexts/SharedGameCatalog/Application/Commands/`
- [x] EnqueueBggBatchFromJsonCommandValidator.cs
- Validation:
  - NotEmpty JSON
  - Valid JSON format
  - Contains bggId + name fields
  - Max size: 10MB

### 4. Create Handler
**File**: `BoundedContexts/SharedGameCatalog/Application/Commands/`
- [x] EnqueueBggBatchFromJsonCommandHandler.cs
- Logic:
  1. Parse JSON → List<BggGameJsonDto>
  2. Query duplicates: WHERE BggId IN (parsedIds)
  3. Build HashSet<int> existingBggIds
  4. Best-effort loop: skip duplicates, continue on errors
  5. Return BulkImportResult

### 5. Unit Tests
**File**: `Api.Tests/BoundedContexts/SharedGameCatalog/Application/`
- [x] EnqueueBggBatchFromJsonCommandValidatorTests.cs
  - Valid JSON
  - Invalid JSON format
  - Missing bggId field
  - Missing name field
  - Empty JSON
- [x] EnqueueBggBatchFromJsonCommandHandlerTests.cs
  - Happy path: all enqueued
  - Duplicates: skipped with errors
  - Mixed: some duplicates, some new
  - JSON parsing errors
  - Best-effort: individual failures

### 6. Integration Tests
**File**: `Api.Tests/BoundedContexts/SharedGameCatalog/Integration/`
- [x] EnqueueBggBatchFromJsonIntegrationTests.cs
  - Full flow with real DB
  - Duplicate detection via repository
  - Queue service integration

---

## Test Coverage Target

**Backend**: ≥90% (3 test files, ~15 test cases)

---

## Dependencies

- ✅ IBggImportQueueService (exists)
- ✅ ISharedGameRepository (exists - for duplicate check)
- ✅ EnqueueBggBatchCommand pattern (exists - reference)

---

## Acceptance Criteria

- [x] JSON parsing robusto
- [x] Duplicate skip con log
- [x] Best-effort strategy funzionante
- [x] Test coverage ≥90%
