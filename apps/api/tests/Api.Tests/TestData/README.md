# Test Data for PDF Page-Aware Extraction Tests

## Overview

This directory contains test data for the AI-08 page-aware PDF extraction feature tests.

## PDF Test File Generation

For AI-08 tests (`PdfTextExtractionServicePagedTests.cs` and `PdfPageNumberIntegrationTests.cs`), we **dynamically generate test PDFs** using QuestPDF rather than committing binary PDF files to the repository.

### Why Dynamic Generation?

1. **No binary files in Git**: Keeps repository clean and version control efficient
2. **Reproducible**: Tests generate same PDFs every time
3. **Flexible**: Easy to modify page content, count, and structure
4. **Already proven**: QuestPDF is already used successfully in `PdfTextExtractionServiceTests.cs`

### QuestPDF Test PDF Generation Pattern

```csharp
private void CreateMultiPagePdf(string filePath, params string[] pageContents)
{
    Document.Create(container =>
    {
        foreach (var content in pageContents)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text(content);
            });
        }
    }).GeneratePdf(filePath);
}

// Usage example:
var pdfPath = CreateTempPdfPath();
CreateMultiPagePdf(pdfPath,
    "Page 1: Setup instructions",
    "Page 2: Gameplay rules",
    "Page 3: Winning conditions");
```

### Test PDF Characteristics

**Unit Tests** (`PdfTextExtractionServicePagedTests.cs`):
- 3-page PDF: "Page one...", "Page two...", "Page three..."
- Empty page testing: Pages with blank content (`""`)
- Single-page PDF: One page only
- Large PDF: 10 pages with sequential numbering
- Corrupted PDF: Invalid text file (not real PDF)

**Integration Tests** (`PdfPageNumberIntegrationTests.cs`):
- Multi-page PDFs with game-specific content
- Chess rulebook simulation (3 pages)
- Large rulebook (10 pages)
- PDFs with empty pages (testing page number preservation)

### License Configuration

All tests using QuestPDF must include:

```csharp
public PdfTextExtractionServicePagedTests()
{
    // Configure QuestPDF for testing (community license)
    QuestPDF.Settings.License = LicenseType.Community;

    // ... rest of setup
}
```

### CI/CD Considerations

- QuestPDF works in CI (Linux) - already proven in existing tests
- Tests marked with `Skip = "RED phase"` initially (TDD RED phase)
- Native PDF libraries (libgdiplus) installed via GitHub Actions workflow
- Tests will be unskipped during GREEN phase implementation

## Other Test Data

- `rag-evaluation-dataset.json`: RAG system offline evaluation queries and ground truth (AI-06)

## Future Test Data

When adding new test data:
1. **Prefer dynamic generation** over static files when possible
2. If static files are necessary (e.g., real-world PDFs), keep them small (<1MB)
3. Document the source and purpose of static files
4. Consider compression for large datasets

## References

- QuestPDF Documentation: https://www.questpdf.com/
- Existing PDF generation: `apps/api/tests/Api.Tests/PdfTextExtractionServiceTests.cs`
- TDD RED-GREEN-REFACTOR: `docs/issue/ai-08-page-aware-extraction.md`
