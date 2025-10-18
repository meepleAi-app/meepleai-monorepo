# PDF-09: Pre-upload PDF Validation Implementation

## Overview

Implementation of comprehensive PDF validation for the MeepleAI monorepo, providing both client-side and server-side validation before PDF upload processing. This feature ensures that invalid files are rejected early with clear, actionable error messages.

**Issue**: #399
**Feature**: PDF-09 - Add pre-upload PDF validation
**Implementation Date**: 2025-10-17

## Business Value

- **Improved UX**: Immediate feedback on file issues before upload starts
- **Reduced Server Load**: Invalid files rejected client-side before network transfer
- **Better Error Handling**: Clear, actionable error messages for users
- **API Protection**: Server-side validation prevents malicious/invalid uploads

## Architecture

### Components Implemented

1. **Backend Service**: `PdfValidationService` - Comprehensive PDF validation logic
2. **Configuration**: `PdfProcessingConfiguration` - Configurable validation thresholds
3. **API Integration**: Updated `/api/v1/ingest/pdf` endpoint with validation
4. **Frontend Validation**: Client-side validation in `upload.tsx`
5. **Test Coverage**: 42+ tests (33 unit + 9 integration)

## Implementation Details

### 1. PdfValidationService

**Location**: `apps/api/src/Api/Services/PdfValidationService.cs`

**Interface**:
```csharp
public interface IPdfValidationService
{
    Task<PdfValidationResult> ValidateAsync(Stream pdfStream, string fileName, CancellationToken ct = default);
    PdfValidationResult ValidateFileSize(long fileSizeBytes);
    PdfValidationResult ValidateMimeType(string contentType);
}
```

**Validation Rules**:
- **File Size**: Max 100MB (configurable)
- **MIME Type**: `application/pdf` only
- **Magic Bytes**: File must start with `%PDF-`
- **Page Count**: Min 1, Max 500 pages (configurable)
- **PDF Version**: Min 1.4 (configurable)
- **PDF Structure**: Valid PDF that Docnet.Core can parse

**Key Features**:
- Thread-safe using semaphore for Docnet.Core access
- Extracts metadata (page count, PDF version, file size)
- Returns structured error dictionary for multiple validation failures
- Temporary file handling with automatic cleanup
- Comprehensive logging with correlation IDs

### 2. Configuration

**Location**: `apps/api/src/Api/appsettings.json`

```json
{
  "PdfProcessing": {
    "MaxFileSizeBytes": 104857600,    // 100 MB
    "MaxPageCount": 500,
    "MinPageCount": 1,
    "MinPdfVersion": "1.4",
    "AllowedContentTypes": [ "application/pdf" ]
  }
}
```

**Configuration Class**:
```csharp
public class PdfProcessingConfiguration
{
    public long MaxFileSizeBytes { get; set; } = 104857600;
    public int MaxPageCount { get; set; } = 500;
    public int MinPageCount { get; set; } = 1;
    public string MinPdfVersion { get; set; } = "1.4";
    public List<string> AllowedContentTypes { get; set; } = new() { "application/pdf" };
}
```json
### 3. Server-Side Endpoint Integration

**Location**: `apps/api/src/Api/Program.cs` (line 1810+)

**Validation Flow**:
1. Check file presence
2. Validate file size
3. Validate MIME type
4. Deep validate PDF content (magic bytes, structure, page count, version)
5. Return 400 Bad Request with structured error on failure
6. Proceed to upload on success

**Error Response Format**:
```json
{
  "error": "validation_failed",
  "details": {
    "fileSize": "File size (150 MB) exceeds maximum of 100 MB",
    "pageCount": "PDF has 600 pages, maximum allowed is 500",
    "pdfVersion": "PDF version 1.2 is not supported, minimum version is 1.4"
  }
}
```json
### 4. Client-Side Validation

**Location**: `apps/web/src/pages/upload.tsx`

**Validation Steps**:
1. MIME type check (`application/pdf`)
2. File size check (max 100MB)
3. Magic bytes validation (`%PDF-` header)
4. Real-time validation on file selection
5. Display validation errors before upload

**User Experience**:
- File input disabled during validation
- Clear error messages with red border
- Success indicator with green text
- Validation happens immediately on file selection
- Upload button disabled if validation fails

**Validation Functions**:
```typescript
// Constants
const MAX_PDF_SIZE_BYTES = 104857600; // 100 MB
const ALLOWED_MIME_TYPES = ['application/pdf'];
const PDF_MAGIC_BYTES = '%PDF-';

// Validation logic
async function validatePdfFile(file: File): Promise<ValidationResult> {
  const errors: Record<string, string> = {};

  // MIME type check
  if (!ALLOWED_MIME_TYPES.includes(file.type)) {
    errors.fileType = `File must be a PDF (type: ${file.type})`;
  }

  // Size check
  if (file.size > MAX_PDF_SIZE_BYTES) {
    const sizeMB = (file.size / 1024 / 1024).toFixed(1);
    errors.fileSize = `File size (${sizeMB} MB) exceeds maximum of 100 MB`;
  }

  // Magic bytes check
  const header = await readFileHeader(file, 5);
  if (header !== PDF_MAGIC_BYTES) {
    errors.fileFormat = 'Invalid PDF file format';
  }

  return { isValid: Object.keys(errors).length === 0, errors };
}
```

## Testing

### Unit Tests (33 tests)

**Location**: `apps/api/tests/Api.Tests/PdfValidationServiceTests.cs`

**Coverage**:
- File size validation (5 tests): zero, negative, exceeds max, valid, at maximum
- MIME type validation (5 tests): empty, null, invalid, valid, case-insensitive
- PDF stream validation (11 tests): null stream, empty filename, valid PDF, invalid magic bytes, corrupted PDF, page count limits, metadata extraction
- Configuration tests (1 test): default values
- Logging tests (2 tests): success logging, failure logging
- Result factory tests (2 tests): success/failure creation
- Edge cases (7 tests): multiple errors, version validation, etc.

### Integration Tests (9 tests)

**Location**: `apps/api/tests/Api.Tests/PdfUploadValidationIntegrationTests.cs`

**Coverage**:
- File size exceeds maximum → 400 with validation_failed
- Invalid MIME type → 400 with fileType error
- Invalid PDF magic bytes → 400 with fileFormat error
- Empty file → 400
- No file provided → 400
- Valid PDF → 200 with documentId
- Error response structure validation
- Editor authorization check
- Multiple validation failures

**Test Patterns**:
- BDD-style (Given-When-Then)
- Full HTTP request/response testing
- Database verification
- Structured error response validation

## API Changes

### Endpoint: POST /api/v1/ingest/pdf

**Before**:
- Basic file checks in PdfStorageService
- Generic error messages
- Limited validation

**After**:
- Comprehensive validation before processing
- Structured error responses
- Configurable validation rules
- Clear, actionable error messages

## Configuration Changes

### Dependency Injection

**Location**: `apps/api/src/Api/Program.cs`

```csharp
// Configuration binding (line 94)
builder.Services.Configure<PdfProcessingConfiguration>(
    builder.Configuration.GetSection("PdfProcessing"));

// Service registration (line 170)
builder.Services.AddScoped<IPdfValidationService, PdfValidationService>();
```sql
## Error Handling

### Validation Error Types

1. **fileSize**: File size validation failures
2. **fileType**: MIME type validation failures
3. **fileFormat**: PDF magic bytes validation failures
4. **pageCount**: Page count limit violations
5. **pdfVersion**: PDF version compatibility issues
6. **pdfStructure**: PDF structure/corruption issues
7. **stream**: Null stream errors
8. **fileName**: Empty filename errors

### Logging

All validation failures are logged at Warning level with:
- File name
- Validation errors (as structured object)
- Correlation ID (inherited from request)

Success validations are logged at Information level with:
- File name
- Page count
- PDF version

## Performance Considerations

### Validation Overhead

- **File Size**: O(1) - Instant
- **MIME Type**: O(1) - Instant
- **Magic Bytes**: O(1) - Read 5 bytes
- **Deep Validation**: O(n) - Depends on file size and complexity
  - Typically adds <100ms for normal PDFs
  - Uses temporary file to avoid memory pressure
  - Semaphore prevents concurrent Docnet.Core access

### Optimizations

- Early exit on simple validations (size, MIME)
- Temporary file cleanup in finally blocks
- Stream position reset for further processing
- Configurable limits to prevent abuse

## Security Considerations

1. **File Size Limits**: Prevents DoS via large file uploads
2. **Magic Bytes Check**: Prevents disguised file uploads
3. **MIME Type Validation**: Ensures only PDFs are processed
4. **Structure Validation**: Prevents malformed/malicious PDFs
5. **Error Messages**: Don't leak sensitive system information
6. **Logging**: Includes validation failures for security monitoring

## Migration Notes

### Backward Compatibility

- ✅ Existing uploads continue to work
- ✅ No database schema changes
- ✅ No breaking API changes
- ✅ Configuration has sensible defaults

### Deployment Checklist

1. Update `appsettings.json` with `PdfProcessing` section
2. Deploy backend with new validation service
3. Deploy frontend with client-side validation
4. Monitor logs for validation failures
5. Adjust thresholds if needed based on real-world usage

## Monitoring & Observability

### Metrics to Monitor

- Validation failure rate by error type
- Average validation time
- Client-side vs server-side rejection ratio
- File size distribution
- PDF version distribution

### Log Queries

```
// Find all validation failures
ProcessingStatus == "Warning" AND Message contains "PDF validation failed"

// Find large file uploads
fileSize > 50MB

// Find ancient PDF versions
pdfVersion < "1.4"
```

## Known Limitations

1. **PDF Version Detection**: Only checks header version, not actual features used
2. **Encrypted PDFs**: Not explicitly validated (may fail during text extraction)
3. **Page Count**: Requires opening PDF (adds latency)
4. **Windows vs Linux**: Some validation tests require libgdiplus (skipped on Windows dev)

## Future Enhancements

### Potential Improvements

1. **PDF/A Validation**: Check for archival standards compliance
2. **Metadata Extraction**: Extract more PDF metadata (author, creation date, etc.)
3. **Content Validation**: Check for text content (not just structure)
4. **Asynchronous Validation**: Move deep validation to background task
5. **Caching**: Cache validation results for duplicate files
6. **Progressive Enhancement**: Add more detailed client-side checks

## References

- Issue #399: https://github.com/DegrassiAaron/meepleai-monorepo/issues/399
- PDF Specification: https://www.adobe.com/content/dam/acom/en/devnet/pdf/pdfs/PDF32000_2008.pdf
- Docnet.Core: https://github.com/BobLd/Docnet

## Related Documentation

- [SECURITY.md](../SECURITY.md) - Security policies
- [code-coverage.md](../code-coverage.md) - Testing guidelines
- [CLAUDE.md](../../CLAUDE.md) - Project structure and conventions

---

**Implementation Status**: ✅ Complete
**Test Coverage**: ✅ 42 tests (100% of acceptance criteria)
**Documentation**: ✅ Complete
**Ready for Review**: ✅ Yes
