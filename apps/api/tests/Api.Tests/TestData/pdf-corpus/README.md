# PDF Test Corpus Organization

**Purpose**: Systematic PDF extraction testing with real backend validation

## Structure

```
pdf-corpus/
├── gold-standards.json     # Expected extraction results for validation
└── README.md              # This file

Actual PDFs stored in: ../../../../data/rulebook/
```

## Complexity Tiers

### Simple (5 PDFs) - 92%+ accuracy expected
**Characteristics**: Text-only or minimal visuals, single-column layout, clear fonts

| PDF | Size | Pages | Language | Key Features |
|-----|------|-------|----------|--------------|
| carcassonne_rulebook.pdf | 219 KB | 8 | EN | Clean layout, minimal tables |
| scacchi-fide_2017_rulebook.pdf | 602 KB | 15 | **IT** | Italian language test |
| splendor_rulebook.pdf | 583 KB | 4 | EN | High-quality text |
| ticket-to-ride_rulebook.pdf | 1.8 MB | 8 | EN | Colored backgrounds |
| azul_rulebook.pdf | 2.1 MB | 4 | EN | Visual-heavy, clear text |

**Use for**: Baseline accuracy testing, language validation, quick smoke tests

### Moderate (4 PDFs) - 85%+ accuracy expected
**Characteristics**: Multi-column layout, moderate tables, mixed content

| PDF | Size | Pages | Language | Key Features |
|-----|------|-------|----------|--------------|
| wingspan_en_rulebook.pdf | 4.8 MB | 16 | EN | Bird card tables, icons |
| 7-wonders_rulebook.pdf | 8.9 MB | 12 | EN | Complex card layouts |
| agricola_rulebook.pdf | 8.8 MB | 20 | EN | Dense text, action tables |
| pandemic_rulebook.pdf | 8.9 MB | 16 | EN | Card grids, clean overall |

**Use for**: Multi-column handling, table extraction, medium complexity validation

### Complex (3 PDFs) - 80%+ accuracy expected
**Characteristics**: Heavy tables, diagrams, iconography, complex layouts

| PDF | Size | Pages | Language | Key Features |
|-----|------|-------|----------|--------------|
| cantan_en_rulebook.pdf | 12 MB | 16 | EN | Setup diagrams, visual-heavy |
| root_rulebook.pdf | 18 MB | 24 | EN | Asymmetric rules, faction tables |
| barrage_rulebook.pdf | 21 MB | 28 | EN | Heavy iconography, complex boards |

**Use for**: Layout analysis stress testing, iconography handling, complex table extraction

### Edge Cases (1 PDF) - 75%+ accuracy expected
**Characteristics**: Very large files, stress tests, extreme complexity

| PDF | Size | Pages | Language | Key Features |
|-----|------|-------|----------|--------------|
| terraforming-mars_rulebook.pdf | 38 MB | 32 | EN | 200+ card reference, huge file |

**Use for**: Performance benchmarking, memory stress testing, maximum complexity validation

## Validation Criteria

### Accuracy Calculation
```
Accuracy Score = LevenshteinRatio(extracted_text, gold_standard_text)

Where gold_standard_text is manually verified correct extraction
```

### Required Validations
1. **Page Count**: Extracted pages ±1 from expected
2. **Word Count**: Extracted words ±15% from expected
3. **Key Phrases**: All keyPhrases detected (case-insensitive)
4. **Quality Score**: Meets or exceeds minAccuracyScore
5. **Performance**: P95 latency < extractionTimeP95Ms

### Quality Thresholds by Tier
- **Simple**: ≥ 92% accuracy
- **Moderate**: ≥ 85% accuracy
- **Complex**: ≥ 80% accuracy
- **Edge Cases**: ≥ 75% accuracy

## Usage in Tests

### Example: Validate Simple PDF Extraction
```csharp
[Theory]
[MemberData(nameof(SimplePdfTestCases))]
public async Task ExtractText_SimplePdfs_MeetsAccuracyThreshold(
    string pdfPath, int expectedPages, double minAccuracy, string[] keyPhrases)
{
    // Arrange
    var goldStandard = LoadGoldStandard(pdfPath);

    // Act
    var result = await _extractor.ExtractTextAsync(pdfPath, CancellationToken.None);

    // Assert
    result.Metadata.PageCount.Should().BeCloseTo(expectedPages, 1);
    keyPhrases.Should().AllSatisfy(phrase =>
        result.Text.Should().Contain(phrase, AtLeast.Once()));

    var accuracy = CalculateAccuracy(result.Text, goldStandard.ExpectedText);
    accuracy.Should().BeGreaterOrEqualTo(minAccuracy);
}
```

### Example: Performance Benchmark
```csharp
[Fact]
public async Task PerformanceBenchmark_AllComplexityTiers_WithinP95Latency()
{
    var goldStandards = LoadAllGoldStandards();

    foreach (var (pdfPath, standard) in goldStandards)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _extractor.ExtractTextAsync(pdfPath, CancellationToken.None);
        stopwatch.Stop();

        _output($"{Path.GetFileName(pdfPath)}: {stopwatch.ElapsedMilliseconds}ms " +
               $"(P95: {standard.ExtractionTimeP95Ms}ms, " +
               $"Accuracy: {CalculateAccuracy(result.Text, standard.ExpectedText):P2})");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(standard.ExtractionTimeP95Ms,
            $"{pdfPath} exceeded P95 latency");
    }
}
```

## Hybrid Testing Strategy

### When to Use Mocks
- ✅ **Unit tests** (handlers, validators, domain logic)
- ✅ **Integration tests** (PDF upload → database persistence)
- ✅ **Quick feedback** (developer workflow, fast CI checks)

### When to Use Real PDFs
- ✅ **E2E critical paths** (complete pipeline validation)
- ✅ **Performance benchmarks** (real extraction latency)
- ✅ **Accuracy validation** (OCR quality, layout analysis)
- ✅ **Regression tests** (prevent quality degradation)
- ✅ **PR validation** (labeled with `pdf-processing`)

## CI/CD Integration

### Standard CI Run (Fast)
```bash
# No PDF services - uses mocks
dotnet test --filter "Category=Integration&Category!=PDF"
# Execution time: ~2 minutes
```

### PDF Processing PR (Comprehensive)
```bash
# Enable PDF services for real validation
export TEST_PDF_SERVICES=true
dotnet test --filter "Category=PDF"
# Execution time: ~5 minutes (includes container startup)
```

### Performance Benchmarking (On-Demand)
```bash
# Run only performance benchmark tests
export TEST_PDF_SERVICES=true
dotnet test --filter "Category=PDF&TestType=Performance"
# Execution time: ~8 minutes (all PDFs benchmarked)
```

## Maintenance

### Adding New PDFs
1. Add PDF to `data/rulebook/`
2. Manually extract gold standard text (100% accurate reference)
3. Add entry to `gold-standards.json` with expected metrics
4. Classify into appropriate complexity tier
5. Run validation test to verify accuracy threshold

### Updating Gold Standards
1. When extraction quality improves, update minAccuracyScore
2. When performance improves, update extractionTimeP95Ms
3. Document changes in git commit message
4. Revalidate all tests pass with new thresholds

## Related Documentation
- [Testcontainers PDF Services](../../../docs/05-testing/backend/testcontainers-pdf-services.md)
- [Backend Testing Patterns](../../../docs/05-testing/backend/backend-testing-patterns.md)
- [PDF Processing Architecture](../../../docs/01-architecture/bounded-contexts/document-processing.md)
