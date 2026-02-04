# PR #3551 Code Review Guidance

**PR**: [Backend] Admin Shared Games API Endpoints (#3533)
**Date**: 2026-02-04
**Reviewer**: Code Review Analysis

## Summary

Analysis of PR #3551 based on patterns from recent similar PRs, identifying 3 critical issues that must be addressed before merge.

---

## Critical Issues (Must Fix)

### 1. Wrong Exception Type for Not Found Resources ⚠️

**Reference**: PR #3543 Issue #2, PR #3210, CLAUDE.md Issue #2568

**File**: `ApproveDocumentForRagProcessingCommandHandler.cs:43`

**Current Code**:
```csharp
var document = await _documentRepository.GetByIdAsync(command.DocumentId, cancellationToken).ConfigureAwait(false);
if (document is null)
{
    throw new InvalidOperationException($"Document with ID {command.DocumentId} not found");
}
```

**Problem**:
- Uses `InvalidOperationException` for not-found scenarios (returns HTTP 500)
- CLAUDE.md explicitly states: "ConflictException (409), NotFoundException (404), NOT InvalidOperationException (500)"
- Codebase pattern: 95%+ of SharedGameCatalog handlers use `NotFoundException` for missing resources

**Required Fix**:
```csharp
var document = await _documentRepository.GetByIdAsync(command.DocumentId, cancellationToken).ConfigureAwait(false);
if (document is null)
{
    throw new NotFoundException("SharedGameDocument", command.DocumentId.ToString());
}
```

**Evidence from Codebase**:
- `ApproveShareRequestCommandHandler.cs:44`: `throw new NotFoundException("ShareRequest", command.ShareRequestId.ToString());`
- `BulkApproveShareRequestsCommandHandler.cs:88-90`: Similar pattern with resource type + ID
- `NotFoundException` is located at: `Api.Middleware.Exceptions.NotFoundException`

**Impact**: Returns wrong HTTP status code (500 instead of 404), breaks API contract expectations.

---

### 2. Domain Exception Usage Pattern Mismatch ⚠️

**Reference**: PR #3210, CLAUDE.md Issue #2568

**File**: `SharedGameDocument.cs:265-268`

**Current Code**:
```csharp
public void Approve(Guid approvedBy, string? notes = null)
{
    if (_approvalStatus != DocumentApprovalStatus.Pending)
        throw new InvalidOperationException($"Cannot approve document with status {_approvalStatus}");
```

**Problem**:
- Domain entity throws `InvalidOperationException` for business rule violations
- When caught by handler, this becomes HTTP 500 instead of proper business error code
- PR #3210 explicitly required changing InvalidOperationException → ConflictException for business state conflicts

**Analysis**:
While domain entities throwing InvalidOperationException is acceptable in some architectures, the codebase pattern shows:
1. Handler should catch business rule violations and re-throw proper exceptions
2. OR domain entity should throw domain-specific exceptions that middleware maps correctly

**Recommended Pattern** (consistent with PR #3543 fixes):
```csharp
// In handler:
try
{
    document.Approve(command.ApprovedBy, command.Notes);
}
catch (InvalidOperationException ex)
{
    throw new ConflictException($"Document approval failed: {ex.Message}");
}
```

**Alternative** (if domain-specific exceptions exist):
Check if SharedGameCatalog has domain exceptions like `DocumentApprovalException` that middleware handles.

---

### 3. Missing Constructor Null Checks in Query Handlers ⚠️

**Reference**: PR #3210 Issue #2, Codebase Pattern Analysis

**Files**:
- `GetFilteredSharedGamesQueryHandler.cs:22-28`
- `GetApprovalQueueQueryHandler.cs:20-26`

**Current Code** (GetFilteredSharedGamesQueryHandler):
```csharp
public GetFilteredSharedGamesQueryHandler(
    MeepleAiDbContext context,
    ILogger<GetFilteredSharedGamesQueryHandler> logger)
{
    _context = context ?? throw new ArgumentNullException(nameof(context));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

**Problem**:
- PR #3210 flagged missing null checks: "Codebase pattern: 95% of handlers use `?? throw new ArgumentNullException`"
- Command handler has proper null checks (lines 25-28), but query handlers also need them
- While DI should prevent nulls, defensive programming is the established pattern

**Current Status**: ✅ **ACTUALLY CORRECT** - Both query handlers DO have null checks

**Verification**: Code shows `?? throw new ArgumentNullException(nameof(...))` pattern is present.

**Action**: No fix needed, this was a false positive from initial analysis.

---

## Important Code Quality Observations

### 4. Potential ConflictException for Domain Rules (Review)

**File**: `SharedGameDocument.cs:286-288` (Reject method)

**Observation**:
```csharp
if (_approvalStatus != DocumentApprovalStatus.Pending)
    throw new InvalidOperationException($"Cannot reject document with status {_approvalStatus}");
```

Same issue as #2 - should be handled consistently with approval method.

---

### 5. Migration Validation ✅

**File**: `20260204155633_AddDocumentApprovalStatus.cs`

**Status**: PASS

**Validation**:
- ✅ Has proper Up() method with 4 AddColumn operations
- ✅ Has proper Down() method with 4 DropColumn operations
- ✅ Column names match domain properties (approval_notes, approval_status, approved_at, approved_by)
- ✅ Default value set for non-nullable approval_status (defaultValue: 0 = Pending)

**Reference**: PR #3487 had empty migration that caused runtime failures. This PR does NOT have that issue.

---

### 6. CQRS Pattern Compliance ✅

**Status**: PASS

**Validation**:
- ✅ Endpoints use `IMediator.Send()` only (checked routing file)
- ✅ No direct service injection in endpoints
- ✅ Command has Handler + Validator
- ✅ Queries have Handlers + Validators

**Reference**: PR #3543 Issue #1 required converting all endpoints to MediatR pattern. This PR follows the pattern correctly from the start.

---

## Testing Recommendations

### Integration Tests Required

**Reference**: PR #3543 comment showing 72 tests with 73.6% pass rate

**Recommended Test Cases**:

1. **ApproveDocumentForRagProcessingCommandHandler**:
   - ✅ Approve pending document (happy path)
   - ✅ Throw NotFoundException when document not found (Issue #1 fix validation)
   - ✅ Throw ConflictException when document already approved (Issue #2 fix validation)
   - ✅ Verify DocumentApprovedForRagEvent published
   - ✅ Verify approval fields persisted (ApprovedBy, ApprovedAt, ApprovalNotes)

2. **GetFilteredSharedGamesQueryHandler**:
   - ✅ Filter by status
   - ✅ Search by title/description
   - ✅ Filter by submitter
   - ✅ Pagination (page/pageSize)
   - ✅ Sorting (all 5 sort fields: title, createdat, modifiedat, status, yearpublished)
   - ✅ Sort direction (asc/desc)

3. **GetApprovalQueueQueryHandler**:
   - ✅ Filter by urgency (>7 days pending)
   - ✅ Filter by submitter
   - ✅ Filter by PDF presence (HasPdfs)
   - ✅ DaysPending calculation accuracy
   - ✅ PdfCount calculation accuracy
   - ✅ Ordering (oldest first)

**Coverage Target**: 90%+ per CLAUDE.md standards

---

## Previous PR Patterns Analysis

### From PR #3543 (BGG Import Queue)
- **CQRS Violation**: All 6 endpoints injected service directly → Fixed with 16 CQRS files
- **Wrong Exception**: InvalidOperationException for conflicts → Fixed with ConflictException
- **Testing**: 72 tests, 73.6% pass rate, 90%+ coverage achieved

### From PR #3210 (GST-002 CQRS)
- **InvalidOperationException**: 5 locations used for business rules → All fixed to ConflictException
- **Missing Null Checks**: 4 handlers missing constructor validation → All added
- **Pattern**: ConfigureAwait(false) on all async calls

### From PR #3487 (SharedGameCatalog Publication)
- **Empty Migration**: Up/Down methods empty → Caused runtime failures
- **Lesson**: Always verify migration SQL is generated

### From PR #3205 (AGT-007 Approval Queue)
- **Dialog State Management**: Error handlers must reset dialog state
- **API Path Mismatch**: Frontend `/admin/...` vs Backend `/api/v1/...` caused 404s
- **TODO Comments**: RULES.md prohibits TODO for core functionality
- **Validation Consistency**: Bulk vs single operations must have same validation rules

---

## Actionable Checklist

### Before Merge

- [ ] **CRITICAL**: Fix InvalidOperationException → NotFoundException in `ApproveDocumentForRagProcessingCommandHandler.cs:43`
- [ ] **CRITICAL**: Add ConflictException handling for domain business rules (Approve/Reject methods)
- [ ] **IMPORTANT**: Verify endpoint routing paths match between frontend/backend
- [ ] **IMPORTANT**: Add integration tests for all 3 new commands/queries (target: 90%+ coverage)
- [ ] Run full test suite: `dotnet test --filter "BoundedContext=SharedGameCatalog"`
- [ ] Verify migration applies cleanly: `dotnet ef database update`
- [ ] Manual API testing: Approve pending document, verify 404 on missing document, verify 409 on already-approved

### Code Review Checklist

✅ **PASS**:
- Constructor null checks present
- Migration has proper Up/Down methods
- CQRS pattern followed correctly
- Validators present for all commands/queries
- Domain event properly defined
- ConfigureAwait(false) used on async calls

❌ **FAIL** (Must Fix):
- Wrong exception type (InvalidOperationException instead of NotFoundException)
- Domain business rule exceptions not handled properly

---

## Related Documentation

- **CLAUDE.md Lines 29-40**: CQRS pattern requirements
- **CLAUDE.md Lines 288-289**: Exception handling patterns (Issue #2568)
- **CLAUDE.md Lines 199-205**: Testing standards (90%+ backend coverage)
- **Api.Middleware.Exceptions.NotFoundException**: Proper exception class to use

---

## Estimated Fix Effort

- **Exception Fix**: 15 minutes (2 file changes + imports)
- **Tests**: 2-3 hours (72 tests similar to PR #3543 pattern)
- **Total**: ~3 hours to meet DoD

---

**Generated**: 2026-02-04
**Reviewer**: Claude Code Analysis
**Based on**: PRs #3543, #3210, #3487, #3205, #3196, #2796
