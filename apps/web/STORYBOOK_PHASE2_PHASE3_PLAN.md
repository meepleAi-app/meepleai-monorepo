# Storybook Phase 2 & 3 Implementation Plan

**Goal**: Create 50+ stories total to reach comprehensive Storybook coverage

**Status**: ✅ Phase 1 Complete (31 UI stories) → 📋 Phase 2 & 3 In Progress (27 custom/form stories)

---

## Phase 2: Custom Components (Target: 16 stories)

### PDF Components (5 stories)

#### 1. PdfPreview.stories.tsx
**Location**: `apps/web/src/components/pdf/PdfPreview.stories.tsx`

**Props**: `file: File`, `onClose?: () => void`

**Stories**:
- Default: Standard PDF preview with navigation
- Loading: Initial PDF load state
- Error: Failed PDF load with error message
- MultiPage: PDF with multiple pages showing pagination
- Zoomed: PDF at 200% zoom level
- DarkTheme: Preview on dark background

**Fixed Data**:
```typescript
// Create mock File object
const mockPdfFile = new File(
  [new Uint8Array()],
  'game-rules.pdf',
  { type: 'application/pdf', lastModified: new Date('2024-01-15T10:00:00Z').getTime() }
);
```

#### 2. PdfTable.stories.tsx
**Location**: `apps/web/src/components/pdf/PdfTable.stories.tsx`

**Props**: `pdfs: PdfDocument[]`, `loading?: boolean`, `error?: string | null`, `retryingPdfId?: string | null`, `onRetryParsing?: (pdf) => void`, `onOpenLog?: (pdf) => void`

**Stories**:
- Default: Table with 3 PDFs
- Loading: Loading skeleton state
- Error: Error state
- Empty: No PDFs uploaded
- WithRetrying: One PDF in retry state
- AllStatuses: PDFs with different statuses (pending, completed, failed)
- DarkTheme: Table on dark background

**Fixed Data**:
```typescript
const mockPdfs: PdfDocument[] = [
  {
    id: '1',
    fileName: 'gloomhaven-rules.pdf',
    fileSizeBytes: 5242880, // 5 MB
    uploadedAt: '2024-01-15T10:00:00Z',
    uploadedByUserId: 'user-123',
    language: 'en',
    status: 'completed',
    logUrl: '/logs/1'
  },
  {
    id: '2',
    fileName: 'wingspan-expansion.pdf',
    fileSizeBytes: 3145728, // 3 MB
    uploadedAt: '2024-01-14T15:30:00Z',
    uploadedByUserId: 'user-123',
    language: 'it',
    status: 'pending',
    logUrl: null
  },
  {
    id: '3',
    fileName: 'terraforming-mars-rules.pdf',
    fileSizeBytes: 7340032, // 7 MB
    uploadedAt: '2024-01-13T09:15:00Z',
    uploadedByUserId: 'user-123',
    language: 'de',
    status: 'failed',
    logUrl: '/logs/3'
  }
];
```

#### 3. PdfTableRow.stories.tsx
**Location**: `apps/web/src/components/pdf/PdfTableRow.stories.tsx`

**Props**: `pdf: PdfDocument`, `isRetrying?: boolean`, `onRetryParsing?: (pdf) => void`, `onOpenLog?: (pdf) => void`

**Stories**:
- Default: Standard row
- Completed: Completed status with English language
- Failed: Failed status with error
- Pending: Pending status
- Retrying: Retrying state with spinner
- LargeFile: PDF with large file size (90 MB)
- MultipleLanguages: Rows showing all language badges (en, it, de, fr, es)
- DarkTheme: Row on dark background

**Fixed Data**: Use individual PDF objects from mockPdfs above

#### 4. PdfUploadForm.stories.tsx
**Location**: `apps/web/src/components/pdf/PdfUploadForm.stories.tsx`

**Props**: `gameId: string`, `gameName: string`, `onUploadSuccess: (documentId: string) => void`, `onUploadError: (error: CategorizedError) => void`, `onUploadStart?: () => void`

**Stories**:
- Default: Empty form ready for upload
- FileSelected: Form with file selected
- Uploading: Upload in progress
- ValidationError: Form with validation errors (file too large)
- RetryingUpload: Upload retry in progress
- SuccessfulUpload: Upload completed successfully
- DarkTheme: Form on dark background

**Fixed Data**:
```typescript
const mockGame = {
  id: 'game-123',
  name: 'Gloomhaven'
};
```

#### 5. PdfViewerModal.stories.tsx
**Location**: `apps/web/src/components/pdf/PdfViewerModal.stories.tsx`

**Props**: `open: boolean`, `onOpenChange: (open: boolean) => void`, `pdfUrl: string`, `initialPage?: number`, `documentName?: string`

**Stories**:
- Default: Modal with PDF viewer
- SpecificPage: Opens to page 5
- Loading: PDF loading state
- Error: Failed to load PDF
- Zoomed: PDF at 150% zoom
- MobileThumbnails: Mobile view with thumbnail sidebar
- DarkTheme: Modal on dark background

**Fixed Data**:
```typescript
// Mock PDF URL (can use public domain PDF or placeholder)
const mockPdfUrl = 'https://example.com/sample.pdf';
```

---

### Upload Components (4 stories)

#### 6. UploadQueue.stories.tsx
**Location**: `apps/web/src/components/upload/UploadQueue.stories.tsx`

**Stories**:
- Empty: No files in queue
- SingleFile: One file uploading
- MultipleFiles: 5 files in various states
- AllCompleted: All files completed successfully
- WithFailures: Some files failed
- Mixed StatusesPending, uploading, processing, success, failed, cancelled
- DarkTheme: Queue on dark background

**Fixed Data**:
```typescript
const mockQueueItems: UploadQueueItem[] = [
  {
    id: '1',
    file: new File([new Uint8Array()], 'gloomhaven.pdf', { type: 'application/pdf' }),
    status: 'uploading',
    progress: 65,
    error: null,
    retryCount: 0,
    correlationId: null
  },
  {
    id: '2',
    file: new File([new Uint8Array()], 'wingspan.pdf', { type: 'application/pdf' }),
    status: 'pending',
    progress: 0,
    error: null,
    retryCount: 0,
    correlationId: null
  },
  {
    id: '3',
    file: new File([new Uint8Array()], 'terraforming-mars.pdf', { type: 'application/pdf' }),
    status: 'failed',
    progress: 45,
    error: 'Upload timeout',
    retryCount: 2,
    correlationId: 'err-abc123'
  }
];

const mockStats: UploadQueueStats = {
  total: 5,
  pending: 1,
  uploading: 1,
  processing: 1,
  succeeded: 1,
  failed: 1,
  cancelled: 0
};
```

#### 7. UploadQueueItem.stories.tsx
**Location**: `apps/web/src/components/upload/UploadQueueItem.stories.tsx`

**Stories**:
- Pending: File waiting to upload
- Uploading: File uploading with progress
- Processing: File being processed
- Success: Successfully uploaded
- Failed: Failed with error message
- Cancelled: Cancelled upload
- Retrying: Retry attempt indicator
- DarkTheme: Item on dark background

**Fixed Data**: Use individual items from mockQueueItems above

#### 8. UploadSummary.stories.tsx
**Location**: `apps/web/src/components/upload/UploadSummary.stories.tsx`

**Stories**:
- AllSucceeded: All files uploaded successfully
- SomeFailures: Some files failed
- MixedResults: Mix of success, failure, cancelled
- SingleFileSuccess: Single file succeeded
- AllFailed: All files failed
- DarkTheme: Summary on dark background

**Fixed Data**: Various mockStats configurations

#### 9. MultiFileUpload.stories.tsx
**Location**: `apps/web/src/components/upload/MultiFileUpload.stories.tsx`

**Stories**:
- Default: Empty upload zone
- FilesSelected: Files ready to upload
- Uploading: Files uploading
- AutoUploadDisabled: Manual upload mode
- ValidationErrors: Files with validation errors
- DragActive: Drag-over state
- DarkTheme: Upload zone on dark background

**Fixed Data**: Same as UploadQueue

---

### Game Components (1 story)

#### 10. GamePicker.stories.tsx
**Location**: `apps/web/src/components/game/GamePicker.stories.tsx`

**Stories**:
- Default: Game picker with selection
- Empty: No games available
- Creating: Creating new game
- Selected: Game selected with confirmation
- ValidationError: Create form with error
- Loading: Loading state
- ManyGames: 10 games in dropdown
- DarkTheme: Picker on dark background

**Fixed Data**:
```typescript
const mockGames: GameSummary[] = [
  { id: '1', title: 'Gloomhaven', createdAt: '2024-01-15T10:00:00Z' },
  { id: '2', title: 'Wingspan', createdAt: '2024-01-14T15:30:00Z' },
  { id: '3', title: 'Terraforming Mars', createdAt: '2024-01-13T09:15:00Z' },
  { id: '4', title: 'Spirit Island', createdAt: '2024-01-12T14:20:00Z' },
  { id: '5', title: 'Scythe', createdAt: '2024-01-11T11:45:00Z' }
];
```

---

### Error Components (6 stories)

#### 11. ErrorDisplay.stories.tsx
**Location**: `apps/web/src/components/errors/ErrorDisplay.stories.tsx`

**Stories**:
- NetworkError: Network error with retry
- ValidationError: Validation error
- AuthenticationError: 401 unauthorized
- ServerError: 500 internal server error
- NotFoundError: 404 not found
- RateLimitError: 429 rate limit
- WithTechnicalDetails: Error with technical details shown
- WithRetryCount: Error showing retry attempts
- DarkTheme: Error on dark background

**Fixed Data**:
```typescript
const mockNetworkError: CategorizedError = {
  category: 'network',
  message: 'Unable to connect to server',
  canRetry: true,
  suggestions: [
    'Check your internet connection',
    'Try again in a few moments',
    'Contact support if the problem persists'
  ],
  correlationId: 'net-xyz789',
  statusCode: null,
  technicalMessage: 'ECONNREFUSED: Connection refused at port 8080'
};
```

#### 12. SimpleErrorMessage.stories.tsx
#### 13. ErrorBoundary.stories.tsx
#### 14. RateLimitBanner.stories.tsx
#### 15. RateLimitedButton.stories.tsx
#### 16. RouteErrorBoundary.stories.tsx

(Similar pattern for each - multiple states, dark theme)

---

### Loading Components (5 stories)

#### 17. LoadingButton.stories.tsx
**Location**: `apps/web/src/components/loading/LoadingButton.stories.tsx`

**Stories**:
- Default: Normal button
- Loading: Button in loading state
- LoadingWithText: Loading with custom text
- Disabled: Disabled button
- AllVariants: All button variants (default, destructive, outline, etc.) in loading state
- AllSizes: All sizes in loading state
- DarkTheme: Loading button on dark background

#### 18. Spinner.stories.tsx
#### 19. SkeletonLoader.stories.tsx
#### 20. TypingIndicator.stories.tsx
#### 21. MessageAnimator.stories.tsx

(Similar pattern for each)

---

## Phase 3: Form Components (6 stories)

### Form Components from Shadcn/UI

#### 22-27. Form.stories.tsx, FormControl.stories.tsx, FormDescription.stories.tsx, FormError.stories.tsx, FormField.stories.tsx, FormLabel.stories.tsx

**Pattern**:
- Default state
- With validation error
- Disabled state
- Required field
- Complex form example (in Form.stories.tsx)
- DarkTheme variant

---

## Implementation Template

### Standard Story Structure

```typescript
import type { Meta, StoryObj } from '@storybook/react';
import { ComponentName } from './ComponentName';

/**
 * ComponentName - Brief description
 *
 * ## Features
 * - Feature 1
 * - Feature 2
 * - Feature 3
 *
 * ## Accessibility
 * - ✅ Keyboard navigation
 * - ✅ ARIA labels
 * - ✅ Focus management
 */
const meta = {
  title: 'Category/ComponentName',
  component: ComponentName,
  parameters: {
    layout: 'centered', // or 'padded', 'fullscreen'
    docs: {
      description: {
        component: 'Detailed component description...',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    // Define argTypes with descriptions
  },
} satisfies Meta<typeof ComponentName>;

export default meta;
type Story = StoryObj<typeof meta>;

// CRITICAL: Use FIXED data only
const FIXED_DATE = new Date('2024-01-15T10:00:00Z');

export const Default: Story = {
  args: {
    // Fixed props only
  },
};

export const DarkTheme: Story = {
  args: {
    // Same props
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
```

---

## Checklist for Each Story File

- [ ] Fixed data only (NO Math.random(), Date.now(), dynamic dates)
- [ ] All variants/states covered
- [ ] Dark theme story included
- [ ] TypeScript with `satisfies Meta<typeof Component>`
- [ ] Comprehensive JSDoc with features and accessibility
- [ ] Mock functions for callbacks (using action() addon)
- [ ] Proper component import path
- [ ] 6-8 stories minimum per component

---

## Priority Order

1. ✅ **PDF Components** (5 stories) - Core feature showcase
2. ✅ **Upload Components** (4 stories) - Complex interaction patterns
3. ✅ **Game Components** (1 story) - Simple but important
4. **Error Components** (6 stories) - User experience critical
5. **Loading Components** (5 stories) - UX feedback
6. **Form Components** (6 stories) - Foundation components

---

## Next Steps

1. Create PDF component stories first (highest visual impact)
2. Create Upload component stories (complex state management)
3. Create remaining custom component stories
4. Create Form component stories (foundation)
5. Verify 50+ total stories achieved
6. Test all stories in Storybook dev server
7. Enable visual regression testing with Chromatic

---

## Story Count Tracker

**Phase 1 (UI Components)**: 31 stories ✅
**Phase 2 (Custom Components)**: 21 stories (target)
**Phase 3 (Form Components)**: 6 stories (target)

**Total Target**: 58 stories
**Current**: 31 stories (53% complete)
**Remaining**: 27 stories (47%)
