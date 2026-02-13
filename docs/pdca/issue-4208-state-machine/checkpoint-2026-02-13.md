# Checkpoint: Issue #4208 - PDF State Machine

**Date**: 2026-02-13 11:00
**Branch**: feature/issue-4208-pdf-state-machine
**Status**: 60% complete (infrastructure exists, need tests + auto-retry job)

## Completed Analysis
- ✅ State machine EXISTS (`TransitionTo()` + `ValidateStateTransition()`)
- ✅ Error categorization EXISTS (5 categories)
- ✅ Manual retry EXISTS (endpoint + handler)
- ✅ Domain events EXISTS (StateChanged, Failed, RetryInitiated)

## Remaining Work
1. **Automatic Retry Job** (~2h):
   - Pattern: `CalculateTrendingJob.cs` (Quartz.NET IJob)
   - Logic: exponential backoff (1s, 2s, 4s)
   - Filter: Network, Service, Unknown errors only
   - Schedule: every 5min

2. **Unit Tests** (~2h):
   - 15+ state transition tests
   - Error categorization tests
   - File: `PdfDocumentTests.cs` (currently has only Language tests)

3. **Integration Tests** (~1h):
   - Background job execution
   - Full retry flow E2E

4. **DoD Verification** (~1h):
   - Coverage check
   - Acceptance criteria validation

## Next Commands
```bash
# Resume session
git checkout feature/issue-4208-pdf-state-machine

# Create background job
# File: apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/RetryFailedPdfsJob.cs

# Add tests
# File: apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocumentTests.cs

# Run tests
dotnet test --filter "FullyQualifiedName~PdfDocument"
```

## Key Files
- Domain Entity: `Domain/Entities/PdfDocument.cs` (has Retry(), CanRetry(), TransitionTo())
- Error Enum: `Domain/Enums/ErrorCategory.cs`
- Manual Retry: `Application/Commands/RetryPdfProcessingCommandHandler.cs`
- Pattern Reference: `SharedGameCatalog/Application/Jobs/CalculateTrendingJob.cs`

## Estimated Completion
**Time**: 4-6 hours remaining
**Next Session**: Fresh start recommended (token conservation)
