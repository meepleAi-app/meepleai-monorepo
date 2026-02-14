# Issue #4163: Step 2 - Metadata Extraction Implementation

## Implementation Tasks

### Task 1: Extend ExtractedMetadata Interface ⏱️ 5min
**File**: `apps/web/src/stores/useGameImportWizardStore.ts`

Add to `ExtractedMetadata`:
```typescript
yearPublished?: number;
minAge?: number;
confidence?: number; // 0-100 (converted from backend 0.0-1.0)
```

**Status**: ⏳ Pending

---

### Task 2: Create ConfidenceBadge Component ⏱️ 10min
**Files**:
- `apps/web/src/components/ui/feedback/ConfidenceBadge.tsx`
- `apps/web/src/components/ui/feedback/__tests__/ConfidenceBadge.test.tsx`

**Features**:
- Props: `confidence: number` (0-100), `size?: 'sm' | 'md'`
- Color mapping:
  - ≥80: Green (`bg-green-100 text-green-900`)
  - 50-79: Yellow (`bg-yellow-100 text-yellow-900`)
  - <50: Red (`bg-red-100 text-red-900`)
- Tooltip with quality description
- Reuse StatusBadge pattern

**Status**: ⏳ Pending

---

### Task 3: Create useExtractMetadata Hook ⏱️ 15min
**File**: `apps/web/src/hooks/queries/useExtractMetadata.ts`

**API Contract**:
- Endpoint: `POST /api/v1/admin/games/wizard/extract-metadata`
- Input: `{ filePath: string }`
- Output: `GameMetadataDto` (Title, Year, MinPlayers, MaxPlayers, PlayingTime, MinAge, Description, ConfidenceScore)

**Features**:
- `useMutation` with documentId parameter
- Map `GameMetadataDto` → `ExtractedMetadata` (convert ConfidenceScore 0.0-1.0 → 0-100)
- Loading/error states
- Follow `useUploadPdf` pattern

**Status**: ⏳ Pending

---

### Task 4: Create Step2MetadataExtraction Component ⏱️ 30min
**File**: `apps/web/src/app/(authenticated)/admin/games/import/wizard/steps/Step2MetadataExtraction.tsx`

**Features**:
- **Loading State**: Skeleton or Spinner with "Extracting metadata..." message
- **Display Section**:
  - ConfidenceBadge at top
  - Card with metadata fields
- **Editable Fields** (Input components):
  - Title (text)
  - Year (number)
  - Min Players (number)
  - Max Players (number)
  - Playing Time (number, suffix "min")
  - Min Age (number, suffix "+")
  - Description (textarea)
- **Integration**:
  - `useExtractMetadata` hook
  - `useGameImportWizardStore` for setExtractedMetadata
  - Auto-trigger extraction on mount with uploadedPdf.id
- **Pattern**: Follow Step1UploadPdf structure

**Status**: ⏳ Pending

---

### Task 5: Create Component Test ⏱️ 20min
**File**: `apps/web/__tests__/admin/games/import/steps/Step2MetadataExtraction.test.tsx`

**Test Cases**:
1. **Loading State**: Shows skeleton/spinner during extraction
2. **Success State**: Displays extracted metadata with correct values
3. **Confidence Badge**: Shows correct color (green/yellow/red)
4. **Editable Fields**: Can modify each field
5. **Store Integration**: Calls setExtractedMetadata on completion
6. **Error Handling**: Shows error alert on extraction failure

**Coverage Target**: ≥85%

**Status**: ⏳ Pending

---

## Total Estimate
⏱️ **80 minutes** (within 1-day estimate)

## Dependencies
- Backend: `POST /api/v1/admin/games/wizard/extract-metadata` ✅ (exists)
- Step 1: Upload PDF component ✅ (exists)
- Store: `useGameImportWizardStore` ✅ (exists)

## Validation
- [ ] All metadata fields displayed
- [ ] Confidence badge color-coded
- [ ] Fields editable with validation
- [ ] Loading UX smooth
- [ ] Test coverage ≥85%
- [ ] TypeScript errors resolved
- [ ] Follows project patterns
