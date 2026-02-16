# PDF Wizard - Frontend Components

4-step wizard for creating shared game entries from PDF rulebooks.

## Architecture

```
page.tsx (CreateFromPdfPage)
├── WizardProgress (step indicator)
├── Step1PdfUpload
├── Step2PreviewExtracted
├── Step3BggMatch
└── Step4Confirm
```

**State**: `usePdfWizardStore` (Zustand) at `lib/stores/pdf-wizard-store.ts`

**API Base**: `POST /api/v1/admin/games/wizard/*`

## Components

### page.tsx

Main page component. Manages step navigation and resets store on mount.

### Step1PdfUpload

**Props**: `{ onNext: () => void }`

Handles PDF upload with drag-and-drop (react-dropzone). Features:
- File validation: PDF only, max 100 MB
- Upload progress tracking via `usePdfProgress` hook
- Automatic extraction polling after upload
- Quality badge display (green/yellow/red based on confidence score)

**API call**: `POST /upload-pdf` (multipart/form-data)

### Step2PreviewExtracted

**Props**: `{ onNext: () => void, onSkipBgg: () => void, onBack: () => void }`

Displays extracted metadata with editable fields. Features:
- Title, Year, Players, Playing Time, Age, Description fields
- Duplicate warning display
- Option to skip BGG enrichment

### Step3BggMatch

**Props**: `{ onNext: () => void, onBack: () => void }`

BGG search and enrichment. Features:
- Search autocomplete for BGG games
- Manual BGG ID entry tab
- Enriched data preview
- Conflict display (PDF vs BGG mismatches)

**API calls**: `POST /extract-metadata`, `POST /enrich-from-bgg`

### Step4Confirm

**Props**: `{ onBack: () => void, userRole: string }`

Final confirmation and import. Features:
- Merged data summary view
- Role-based submission (Admin: direct, Editor: approval request)

**API call**: `POST /confirm-import`

## State Management

```typescript
// lib/stores/pdf-wizard-store.ts
interface PdfWizardStore {
  currentStep: number;          // 1-4
  pdfDocumentId: string | null; // Step 1 result
  qualityScore: number;         // Extraction confidence
  extractedTitle: string;       // Step 2 title
  manualFields: ManualFields;   // Step 2 overrides
  duplicateWarnings: string[];  // Step 2 warnings
  selectedBggId: number | null; // Step 3 selection
  bggDetails: BggGameDetails | null;
  // Actions
  setCurrentStep(step: number): void;
  setStep1Data(data): void;
  setStep2Data(data): void;
  setStep3Data(data): void;
  reset(): void;
}
```

## Utilities

| File | Function | Purpose |
|------|----------|---------|
| `lib/utils/upload.ts` | `uploadChunks()` | Chunked multipart upload for large PDFs |
| `lib/utils/extraction.ts` | `pollExtractionStatus()` | Polls extraction status until complete |
| `hooks/usePdfProgress.ts` | `usePdfProgress()` | Upload/extraction progress tracking |

## Tests

Each step has a dedicated test file in `steps/__tests__/`:
- `Step1PdfUpload.test.tsx`: Upload validation, progress, quality display
- `Step2PreviewExtracted.test.tsx`: Field editing, duplicate warnings
- `Step3BggMatch.test.tsx`: Search, manual ID, conflict display
- `Step4Confirm.test.tsx`: Submission, role-based behavior

**Run**: `cd apps/web && pnpm test -- --testPathPattern="create-from-pdf"`
