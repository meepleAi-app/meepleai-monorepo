# PDF-08: Granular Progress Tracking Implementation

## Overview

This document describes the implementation of real-time progress tracking for PDF processing pipeline in issue #398.

## Backend Implementation

### 1. Data Models (`Models/ProcessingProgress.cs`)

- `ProcessingProgress` class: Tracks current step, percentage, elapsed time, estimated remaining time, pages processed
- `ProcessingStep` enum: Uploading (0-20%), Extracting (20-40%), Chunking (40-60%), Embedding (60-80%), Indexing (80-100%), Completed (100%), Failed
- Static helper methods:
  - `CalculatePercentComplete()`: Maps step and page progress to 0-100%
  - `EstimateTimeRemaining()`: Calculates ETA based on current progress and elapsed time

### 2. Database Changes

**Migration**: `AddProcessingProgressColumnPdf08`

- Added `ProcessingProgressJson` column to `pdf_documents` table
- `PdfDocumentEntity` has computed property `ProcessingProgress` that serializes/deserializes JSON
- Progress persists across page refreshes

### 3. Background Task Service Updates

Enhanced `BackgroundTaskService` with cancellation support:

- `ExecuteWithCancellation(taskId, taskFactory)`: Runs tasks with CancellationToken
- `CancelTask(taskId)`: Cancels running tasks by ID
- Uses `ConcurrentDictionary<string, CancellationTokenSource>` to track active tasks

### 4. PDF Processing Pipeline

Refactored `PdfStorageService` to track progress:

- New `ProcessPdfAsync()` method combines extraction, chunking, embedding, and indexing with progress tracking
- `UpdateProgressAsync()` helper writes progress to database at each step
- Handles cancellation gracefully with proper cleanup
- Error states include step where failure occurred

### 5. API Endpoints

**GET `/api/v1/pdfs/{pdfId}/progress`**
- Returns current `ProcessingProgress` JSON
- Authorization: user can view own PDFs, admins can view all
- Returns `null` if no progress data available

**DELETE `/api/v1/pdfs/{pdfId}/processing`**
- Cancels active PDF processing task
- Authorization: user can cancel own PDFs, admins can cancel all
- Returns 400 if processing already completed or failed
- Calls `BackgroundTaskService.CancelTask(pdfId)`

## Frontend Implementation (To Be Completed)

### Components Needed

1. **`ProcessingProgress.tsx`**
   - Polls `/api/v1/pdfs/{pdfId}/progress` every 2 seconds
   - Displays progress bar (0-100%)
   - Shows step-by-step indicator with checkmarks
   - Time estimate and elapsed time display
   - Cancel button with confirmation dialog
   - Success/error state rendering

2. **`upload.tsx` Integration**
   - After successful upload, show `<ProcessingProgress pdfId={uploadedPdfId} />`
   - Handle completion callback to refresh PDF list
   - Handle error callback to show user-friendly messages

### API Client Methods

```typescript
// lib/api.ts additions
export async function getPdfProgress(pdfId: string): Promise<ProcessingProgress | null> {
  return get<ProcessingProgress>(`/pdfs/${pdfId}/progress`);
}

export async function cancelPdfProcessing(pdfId: string): Promise<void> {
  return delete_(`/pdfs/${pdfId}/processing`);
}
```

### TypeScript Types

```typescript
enum ProcessingStep {
  Uploading = 0,
  Extracting = 1,
  Chunking = 2,
  Embedding = 3,
  Indexing = 4,
  Completed = 5,
  Failed = 6
}

interface ProcessingProgress {
  currentStep: ProcessingStep;
  percentComplete: number;
  elapsedTime: string; // TimeSpan formatted
  estimatedTimeRemaining: string | null;
  pagesProcessed: number;
  totalPages: number;
  startedAt: string;
  completedAt: string | null;
  errorMessage: string | null;
}
```

## Testing Strategy

### Unit Tests (`ProcessingProgressTests.cs`)
- `CalculatePercentComplete_*` tests for each step
- `EstimateTimeRemaining_*` tests for various progress scenarios
- Edge cases: 0%, 100%, invalid inputs

### Integration Tests (`PdfProgressEndpointsTests.cs`)
- Test progress endpoint authorization
- Test cancellation endpoint authorization
- Test progress retrieval for active processing
- Test cancellation flow
- Test error scenarios (PDF not found, not authorized, etc.)

### E2E Tests (`e2e/pdf-progress.spec.ts`)
- Upload PDF and verify progress updates
- Test page refresh persistence
- Test cancellation flow
- Test progress completion
- Test error handling

## Performance Considerations

- Progress updates are debounced (update at start/end of each step)
- No performance impact on actual processing (async writes)
- Polling interval: 2 seconds (configurable)
- Consider Server-Sent Events (SSE) for future optimization

## Migration Path

1. Run migration to add `ProcessingProgressJson` column
2. Existing PDFs have `null` progress (normal)
3. New uploads automatically track progress
4. Old processing code still works (ExtractTextAsync, IndexVectorsAsync) for backward compatibility

## Configuration

None required - all settings use sensible defaults.

## Monitoring

Progress tracking generates these logs:

- `LogDebug`: Progress updates for each step
- `LogInformation`: Task start/completion/cancellation
- `LogWarning`: Cancellation requests for non-existent tasks
- `LogError`: Processing failures with step information

## Future Enhancements

- Server-Sent Events (SSE) instead of polling
- WebSocket support for real-time updates
- Historical processing time data for better ETA
- Progress persistence to Redis for distributed systems
- Retry mechanism for failed steps
