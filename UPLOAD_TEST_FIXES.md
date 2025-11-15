# Upload Test Fixes Summary

## Test File: `apps/web/src/__tests__/pages/upload.test.tsx`

### Issues Fixed

#### 1. **Button Text Changes**
- Changed `{ name: /Create/i }` to `{ name: 'Create' }` for exact match
- Button text in GamePicker component is exactly "Create", not "Create Game"

#### 2. **PDF Validation Error Messages**
- Updated error message expectations to match actual validation messages:
  - Empty file: `File is empty (0 bytes)`
  - Invalid type: `Invalid file type. Expected PDF, got...`
  - Invalid PDF: `File does not appear to be a valid PDF (invalid header)`
  - Size exceeded: `File size...exceeds maximum`

#### 3. **File Selection Feedback**
- File selection success shows: `✓ filename (size)` format
- Example: `✓ test.pdf (2 KB)` instead of `Selected: test.pdf (2.0 KB)`

#### 4. **CSS Class vs Inline Styles**
- Changed from checking inline styles `toHaveStyle({ border: '1px solid #d93025' })`
- To checking CSS class: `toHaveClass('border-destructive')`

#### 5. **MultiFileUpload Component Integration**
- Fixed file input selector: changed from `getByTestId('file-input')` to `getByLabelText(/File input for PDF upload/i)`
- The component uses aria-label, not data-testid for the file input

#### 6. **Game Info Badge Location**
- Game info badge is inside MultiFileUpload component
- Uses `data-testid="game-info-badge"` and shows format: `Target Game: {gameName} ({gameId})`

#### 7. **Wizard Step Labels**
- Removed expectations for "Step 2: Parse PDF" label
- UI shows just "Document ID: xxx" after upload
- Review step shows "Review & Edit Rules" not "Step 3: Review"
- Publish step shows "Published Successfully" not "Step 4: Published Successfully"

#### 8. **Language Selection**
- Updated to use Shadcn Select pattern:
  - Click to open dropdown
  - Wait for options to appear
  - Click option to select
  - Check text content, not select value

#### 9. **Upload Error Handling**
- Errors appear in file queue in MultiFileUpload
- Error format: filename appears with error message below
- Removed expectations for "Error:" prefix

#### 10. **Test Context Setup**
- Added missing `pdfs: { pdfs: [] }` to mock setups where needed
- Ensures MultiFileUpload component receives proper props

### Test Categories Fixed
1. ✅ Authentication & Authorization (8 tests)
2. ✅ Game Selection & Management (12 tests)
3. ✅ PDF Upload & Validation CLIENT-SIDE (15 tests)
4. ✅ PDF Upload & Server Response (15 tests)
5. ✅ Wizard Steps & Navigation (10 tests)
6. ✅ Additional categories remain as placeholders

### Key Patterns Applied
- Use exact text matches for buttons when possible
- Check for actual UI text, not conceptual labels
- Understand component hierarchy (MultiFileUpload contains game badge)
- Use proper selectors (aria-label vs data-testid)
- Match actual error message formats
- Account for Shadcn UI component behaviors

### Running the Tests
```bash
# From repository root
cd apps/web
pnpm test src/__tests__/pages/upload.test.tsx --no-coverage

# Or use the batch file (Windows)
test-upload.bat
```

### Expected Result
All tests in upload.test.tsx should now pass with 0 failures.