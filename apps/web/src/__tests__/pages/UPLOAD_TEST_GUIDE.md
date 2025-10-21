# Upload Page Comprehensive Test Suite Guide

## Overview

The upload.tsx page (1547 lines) is the **most critical** file in the web application, handling the entire PDF upload wizard workflow. This guide explains the comprehensive test coverage created across multiple test files.

## Test File Structure

Due to the complexity and size of the upload page, the tests are organized across multiple files:

### 1. **upload.test.tsx** (NEW - Main Test File)
- **Categories 1-5**: 45 tests covering:
  - Authentication & Authorization (8 tests)
  - Game Selection & Management (12 tests)
  - PDF Upload & Validation CLIENT-SIDE (15 tests)
  - PDF Upload & Server Response (15 tests)
  - Wizard Steps & Navigation (10 tests)

### 2. **upload.continuation.test.tsx** (NEW - Continuation Tests)
- **Categories 6-7**: 22 tests covering:
  - PDF Processing & Polling (12 tests)
  - RuleSpec Review & Edit (10 tests)

### 3. **Existing Test Files** (Keep as-is)
- `upload.game-selection.test.tsx` - BDD-style game selection tests
- `upload.pdf-upload.test.tsx` - PDF upload workflow tests
- `upload.review-edit.test.tsx` - Review and edit tests
- `upload.edge-cases.test.tsx` - Edge case handling

## Total Test Coverage

### NEW Tests (Categories 1-7): 67 tests
1. **Authentication & Authorization**: 8 tests
   - Unauthorized user handling
   - Role-based access control (Admin, Editor, Viewer)
   - Access restriction UI
   - Login state validation

2. **Game Selection & Management**: 12 tests
   - Game loading and auto-selection
   - Game confirmation workflow
   - New game creation
   - Error handling (API, network, validation)
   - Alphabetical sorting
   - Empty state handling

3. **PDF Upload & Validation (Client-Side)**: 15 tests
   - File size validation (0 bytes, >100MB)
   - MIME type validation (application/pdf vs others)
   - Magic bytes validation (%PDF- header)
   - Validation error display with red borders
   - File preview rendering
   - Language selection (en, it, de, fr, es)
   - Human-readable file sizes

4. **PDF Upload & Server Response**: 15 tests
   - Successful upload with documentId
   - Upload progress/loading states
   - HTTP error responses (400, 401, 413, 500)
   - Retry logic with exponential backoff (3 attempts)
   - Retry attempt counter display
   - Transient vs permanent error handling
   - FormData language inclusion
   - ProcessingStatus state management

5. **Wizard Steps & Navigation**: 10 tests
   - 4-step indicator rendering
   - Step highlighting (current=blue, past=green, future=gray)
   - Step advancement after upload
   - ProcessingProgress component integration
   - Fallback progress bar
   - DocumentId display
   - Review step rendering
   - Publish success message

6. **PDF Processing & Polling**: 12 tests
   - 2-second polling interval
   - ProcessingStatus updates (pending → processing → completed)
   - Auto-advance to review on completion
   - Polling stop on failure
   - Network error retry with 4s interval
   - Polling cancellation (unmount, step change)
   - Status handling (pending, processing, completed, failed)
   - Progress percentage display (20%, 65%, 100%)
   - Auto-trigger handleParse on completion

7. **RuleSpec Review & Edit**: 10 tests
   - Metadata display (gameId, version, rule count)
   - Editable rule list rendering
   - Rule text updates via textarea
   - Rule field updates (section, page, line)
   - Rule deletion
   - Rule addition with incremented IDs
   - Navigation back to parse step
   - Cancel/reset wizard
   - Publish button API call

### Existing Tests (Categories 8-10): 30+ tests
8. **PDF List & Management**: 8 tests (in existing files)
   - PDF loading for confirmed game
   - Table rendering with headers
   - Language badge display
   - File size formatting
   - Upload date formatting
   - Log URL opening in new tab
   - Retry parsing functionality
   - Error handling

9. **Multi-File Upload Integration**: 5 tests (in existing files)
   - Component rendering when game confirmed
   - Correct gameId/gameName props
   - Upload complete callback
   - Component hiding without confirmed game
   - Integration with main wizard

10. **Error Handling & Edge Cases**: 10 tests (in existing files)
    - API failure handling (games, pdfs, create, upload, parse, publish)
    - Missing data validation
    - Concurrent upload blocking
    - Wizard state reset
    - File input value reset

## Key Testing Patterns

### 1. Mock Setup
```typescript
const mockFetch = setupUploadMocks({
  auth: createAuthMock({ role: 'Admin' }),
  games: [createGameMock({ id: 'game-1' })],
  uploadResponse: { documentId: 'doc-123' },
  pdfStatusSequence: [
    { processingStatus: 'pending' },
    { processingStatus: 'completed' }
  ],
  ruleSpec: createRuleSpecMock()
});
global.fetch = mockFetch as unknown as typeof fetch;
```

### 2. User Interaction
```typescript
const user = userEvent.setup();
await user.click(button);
await user.type(input, 'text');
await user.upload(fileInput, file);
await user.selectOptions(select, 'value');
```

### 3. Polling Tests
```typescript
jest.useFakeTimers();
// ... trigger polling ...
jest.advanceTimersByTime(2000); // Advance 2 seconds
await waitFor(() => {
  expect(pollCount).toBe(1);
});
jest.useRealTimers();
```

### 4. File Creation
```typescript
function createPdfFile(name: string, sizeInBytes: number, content = '%PDF-1.4'): File {
  const blob = new Blob([content], { type: 'application/pdf' });
  Object.defineProperty(blob, 'size', { value: sizeInBytes, writable: false });
  return new File([blob], name, { type: 'application/pdf' });
}
```

## Mock Components

All external components are mocked to isolate upload.tsx logic:

```typescript
// Mock next/dynamic for PdfPreview (SSR handling)
jest.mock('next/dynamic', () => ({
  __esModule: true,
  default: () => () => <div data-testid="pdf-preview-mock">PDF Preview Mock</div>
}));

// Mock MultiFileUpload
jest.mock('@/components/MultiFileUpload', () => ({
  MultiFileUpload: ({ gameId, gameName, onUploadComplete }) => (
    <div data-testid="multi-file-upload" data-game-id={gameId}>
      <button onClick={onUploadComplete}>Trigger Upload Complete</button>
    </div>
  )
}));

// Mock ProcessingProgress
jest.mock('@/components/ProcessingProgress', () => ({
  ProcessingProgress: ({ pdfId, onComplete, onError }) => (
    <div data-testid="processing-progress" data-pdf-id={pdfId}>
      <button onClick={onComplete}>Trigger Complete</button>
      <button onClick={() => onError('Test error')}>Trigger Error</button>
    </div>
  )
}));

// Mock ErrorDisplay
jest.mock('@/components/ErrorDisplay', () => ({
  ErrorDisplay: ({ error, onRetry, onDismiss, showTechnicalDetails }) => (
    <div data-testid="error-display">
      <p>{error.message}</p>
      {showTechnicalDetails && <p>Technical Details Shown</p>}
      {onRetry && <button onClick={onRetry}>Retry</button>}
      {onDismiss && <button onClick={onDismiss}>Dismiss</button>}
    </div>
  )
}));
```

## Running the Tests

### Run All Upload Tests
```bash
cd apps/web
pnpm test upload
```

### Run Specific Test File
```bash
pnpm test upload.test.tsx
pnpm test upload.continuation.test.tsx
pnpm test upload.game-selection.test.tsx
```

### Run With Coverage
```bash
pnpm test:coverage upload
```

### Watch Mode
```bash
pnpm test --watch upload
```

## Coverage Goals

- **Target Coverage**: 90%+ (Jest threshold)
- **Critical Paths**: 100% coverage for:
  - Authentication/Authorization flow
  - PDF validation (client + server)
  - Upload retry logic
  - Polling mechanism
  - Wizard step transitions

## Implementation Notes

### 1. Fake Timers
Polling tests use `jest.useFakeTimers()` to control time-based operations:
```typescript
beforeEach(() => {
  jest.useFakeTimers();
});

afterEach(() => {
  jest.useRealTimers();
});

// In test
jest.advanceTimersByTime(2000); // Advance by 2 seconds
```

### 2. Async Operations
Always use `waitFor` for async state updates:
```typescript
await waitFor(() => {
  expect(screen.getByText(/expected text/i)).toBeInTheDocument();
}, { timeout: 5000 });
```

### 3. File Upload Testing
File uploads require proper File object construction with magic bytes:
```typescript
const file = createPdfFile('test.pdf', 1024); // 1KB PDF
await user.upload(fileInput, file);
```

### 4. Mock Fetch Stateful Behavior
For polling tests, use closures to maintain state:
```typescript
let pollCount = 0;
const mockFetch = jest.fn().mockImplementation((url: string) => {
  if (url.includes('/pdfs/doc-123/text')) {
    pollCount++;
    return Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ processingStatus: pollCount >= 3 ? 'completed' : 'processing' })
    } as Response);
  }
  // ... other routes
});
```

## Maintenance

### Adding New Tests
1. Identify the category (1-10)
2. Add test to appropriate file
3. Follow established patterns
4. Use descriptive test names
5. Verify mocks are correctly set up

### Updating Existing Tests
1. Check for breaking changes in upload.tsx
2. Update mocks if API contracts change
3. Adjust polling intervals if POLL_INTERVAL_MS changes
4. Update validation thresholds if MAX_PDF_SIZE_BYTES changes

### Common Issues

#### Issue: Tests timeout
**Solution**: Check for missing `jest.useRealTimers()` cleanup

#### Issue: Polling doesn't advance
**Solution**: Ensure `jest.advanceTimersByTime()` is called after action triggers polling

#### Issue: File upload fails validation
**Solution**: Verify File object has correct `type` and `size` properties

#### Issue: Mock fetch not matching route
**Solution**: Check URL matching logic in MockApiRouter, ensure pattern matches actual request

## Integration with CI/CD

These tests run automatically in the `ci-web` workflow:

```yaml
- name: Run tests
  run: pnpm test --coverage
  env:
    CI: true
```

Coverage reports are uploaded as artifacts for analysis.

## Future Enhancements

### Planned Test Coverage (Categories 8-10)
Still need to implement in upload.continuation.test.tsx:
- **PDF List & Management**: 8 tests
- **Multi-File Upload Integration**: 5 tests
- **Error Handling & Edge Cases**: 10 tests

These follow the same patterns as categories 1-7 and should be added to complete the comprehensive suite.

### Accessibility Testing
Consider adding:
- ARIA attribute validation
- Keyboard navigation tests
- Screen reader compatibility
- Focus management

### Performance Testing
Consider adding:
- Large file handling (stress tests)
- Multiple concurrent uploads
- Memory leak detection
- Polling performance under load

## References

- **Source File**: `apps/web/src/pages/upload.tsx` (1547 lines)
- **Mock Utilities**: `apps/web/src/__tests__/utils/mock-api-router.ts`
- **Mock Presets**: `apps/web/src/__tests__/utils/mock-api-presets.ts`
- **Upload Mocks**: `apps/web/src/__tests__/fixtures/upload-mocks.ts`
- **Jest Config**: `apps/web/jest.config.js`
- **Coverage Report**: `apps/web/coverage/lcov-report/index.html`
