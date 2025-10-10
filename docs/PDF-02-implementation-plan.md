# PDF-02: Parsing testo + OCR - Piano di Implementazione

**Issue**: #275
**Component**: PDF
**Type**: Feature
**Priority**: P0
**Effort**: 5
**Dependencies**: PDF-01 (✅ CLOSED)

## Obiettivo
Estrazione testo con fallback OCR; normalizzazione paragrafi.

**Acceptance Criteria**: >90% testo parsato su PDF di esempio.

---

## Analisi Stato Attuale

### Implementazione Esistente
File: `apps/api/src/Api/Services/PdfTextExtractionService.cs`

#### Funzionalità Presenti ✅
1. **Estrazione Testo Base** (linee 67-89)
   - Usa Docnet.Core per estrarre testo embedded dai PDF
   - Supporto multi-pagina
   - Dimensioni pagina: 1080x1920

2. **Normalizzazione Testo** (linee 94-123)
   - Normalizzazione line endings (`\r\n` → `\n`)
   - Rimozione spazi multipli: `[ \t]+` → ` `
   - Merge paragrafi spezzati: `([a-z,])\n([a-z])` → `$1 $2`
   - Riduzione newline multipli: `\n{3,}` → `\n\n`
   - Trim linee individuali

3. **Test Coverage** ✅
   - 23 test unitari in `PdfTextExtractionServiceTests.cs`
   - Test per validazione, estrazione, normalizzazione, error handling

#### Limitazioni Attuali ❌
1. **Nessun fallback OCR**
   - PDFs scansionati non sono supportati
   - PDFs con immagini di testo non vengono estratti
   - Impossibile raggiungere >90% coverage su tutti i PDF

2. **Normalizzazione Paragrafi Limitata**
   - Regex semplice per merge paragrafi (linea 109)
   - Non gestisce layout complessi (colonne, tabelle)
   - Non rileva headers/footers

3. **Nessuna Metrica di Qualità**
   - Non misura % di testo estratto vs testo totale
   - Nessuna confidence score

### PDF di Test Disponibili
- `data/Test-EN-LIBELLUD_HARMONIES_RULES_EN.pdf` (7 pagine, PDF 1.4, ~3MB)
- `data/Test-EN-LorenzoRules.pdf` (PDF 1.7, ~9.4MB)

---

## Piano di Implementazione

### Fase 1: Infrastruttura OCR (Effort: 2)

#### 1.1 Aggiungere Dipendenze NuGet
**File**: `apps/api/src/Api/Api.csproj`

```xml
<PackageReference Include="Tesseract" Version="5.2.0" />
```

**Note**:
- Tesseract 5.2.0 è stabile per .NET 8
- Supporta LSTM neural networks (Tesseract 5) + legacy engine (Tesseract 3)
- Richiede language data files (tessdata)

#### 1.2 Setup Tessdata
**Directory**: `apps/api/src/Api/tessdata/`

1. Download tessdata da: https://github.com/tesseract-ocr/tessdata
2. Lingue necessarie:
   - `eng.traineddata` (English - prioritario)
   - `ita.traineddata` (Italian - futuro)
3. Configurare `Copy to Output Directory: Always` nel .csproj

```xml
<ItemGroup>
  <None Include="tessdata\**\*" CopyToOutputDirectory="Always" />
</ItemGroup>
```

#### 1.3 Creare IOcrService Interface
**File**: `apps/api/src/Api/Services/IOcrService.cs`

```csharp
namespace Api.Services;

public interface IOcrService
{
    /// <summary>
    /// Performs OCR on a PDF page and returns extracted text
    /// </summary>
    Task<OcrResult> ExtractTextFromPageAsync(
        string pdfPath,
        int pageIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Performs OCR on entire PDF document
    /// </summary>
    Task<OcrResult> ExtractTextFromPdfAsync(
        string pdfPath,
        CancellationToken ct = default);
}

public record OcrResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string ExtractedText { get; init; } = string.Empty;
    public float MeanConfidence { get; init; } // 0.0 - 1.0
    public int PageCount { get; init; }
}
```

#### 1.4 Implementare TesseractOcrService
**File**: `apps/api/src/Api/Services/TesseractOcrService.cs`

**Responsabilità**:
1. Rendering PDF page → immagine (Docnet.Core)
2. OCR su immagine con Tesseract
3. Calcolo confidence score
4. Gestione risorse (Dispose pattern)

**Configurazione**:
- Language: eng (default), supporto multi-lingua futuro
- Engine mode: LSTM + Legacy fallback
- Page Segmentation Mode: Auto (PSM 3)
- DPI: 300 (standard per OCR di qualità)

**Pseudo-codice**:
```csharp
public class TesseractOcrService : IOcrService, IDisposable
{
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly string _tessdataPath;
    private TesseractEngine? _engine;

    public async Task<OcrResult> ExtractTextFromPageAsync(string pdfPath, int pageIndex, CancellationToken ct)
    {
        // 1. Render PDF page to Pix using Docnet.Core
        // 2. Process Pix with TesseractEngine.Process()
        // 3. Extract text + confidence
        // 4. Return OcrResult
    }
}
```

#### 1.5 Registrare Servizio in DI
**File**: `apps/api/src/Api/Program.cs` (linea ~100-139)

```csharp
builder.Services.AddSingleton<IOcrService, TesseractOcrService>();
```

**Test**: `apps/api/tests/Api.Tests/TesseractOcrServiceTests.cs`
- Test con PDF semplice
- Test con immagine scansionata
- Test confidence scoring
- Test error handling

---

### Fase 2: Integrazione Fallback OCR (Effort: 2)

#### 2.1 Modificare PdfTextExtractionService
**File**: `apps/api/src/Api/Services/PdfTextExtractionService.cs`

**Strategia Fallback**:
1. **Tentativo 1**: Estrazione Docnet standard
2. **Rilevamento Low-Quality**: Se `chars/page < 100` → attivare OCR
3. **Tentativo 2**: OCR su pagine con scarso testo
4. **Merge**: Combinare risultati Docnet + OCR

**Pseudocodice**:
```csharp
public class PdfTextExtractionService
{
    private readonly IOcrService _ocrService;
    private const int MIN_CHARS_PER_PAGE_THRESHOLD = 100;

    public async Task<PdfTextExtractionResult> ExtractTextAsync(string filePath, CancellationToken ct)
    {
        // 1. Tentativo estrazione Docnet
        var (rawText, pageCount) = ExtractRawText(filePath);

        // 2. Valutazione qualità
        var charsPerPage = rawText.Length / Math.Max(pageCount, 1);

        // 3. OCR fallback se necessario
        if (charsPerPage < MIN_CHARS_PER_PAGE_THRESHOLD)
        {
            _logger.LogInformation("Low text density ({CharsPerPage} chars/page), attempting OCR fallback", charsPerPage);
            var ocrResult = await _ocrService.ExtractTextFromPdfAsync(filePath, ct);

            if (ocrResult.Success && ocrResult.ExtractedText.Length > rawText.Length)
            {
                _logger.LogInformation("OCR produced better results (conf: {Confidence})", ocrResult.MeanConfidence);
                rawText = ocrResult.ExtractedText;
            }
        }

        // 4. Normalizzazione
        var normalizedText = NormalizeText(rawText);

        return PdfTextExtractionResult.CreateSuccess(normalizedText, pageCount, normalizedText.Length);
    }
}
```

**Configurazione**:
- Threshold configurabile via `appsettings.json`:
  ```json
  "PdfExtraction": {
    "MinCharsPerPageForOcr": 100,
    "OcrEnabled": true,
    "OcrLanguage": "eng"
  }
  ```

#### 2.2 Update Result Model
**File**: `apps/api/src/Api/Services/PdfTextExtractionService.cs`

```csharp
public record PdfTextExtractionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string ExtractedText { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public int CharacterCount { get; init; }

    // NUOVO
    public bool UsedOcr { get; init; }
    public float? OcrConfidence { get; init; } // 0.0 - 1.0
}
```

**Test**: `apps/api/tests/Api.Tests/PdfTextExtractionServiceOcrTests.cs`
- Test OCR fallback activation
- Test threshold configuration
- Test merge Docnet + OCR
- Test confidence reporting

---

### Fase 3: Miglioramento Normalizzazione Paragrafi (Effort: 1)

#### 3.1 Enhanced Paragraph Normalization
**File**: `apps/api/src/Api/Services/PdfTextExtractionService.cs` (linee 94-123)

**Miglioramenti**:

1. **Header/Footer Detection**
   ```csharp
   // Rimuovere header/footer ripetuti (es. "Page 1", "Copyright 2023")
   private string RemoveHeaders(string text)
   {
       // Analizzare prime/ultime righe di ogni pagina
       // Rimuovere righe che si ripetono >50% delle pagine
   }
   ```

2. **Smart Paragraph Merging**
   ```csharp
   // Attuale (linea 109): Regex.Replace(text, @"([a-z,])\n([a-z])", "$1 $2")

   // Migliorato:
   // - Merge solo se riga non termina con punteggiatura (., !, ?, :)
   // - Non merge se next line è indentata (probabile nuovo paragrafo)
   // - Non merge se all-caps (probabile title)
   ```

3. **Hyphenation Handling**
   ```csharp
   // Fix parole spezzate: "strate-\ngic" → "strategic"
   text = Regex.Replace(text, @"(\w+)-\s*\n\s*(\w+)", "$1$2");
   ```

4. **Bullet Point Preservation**
   ```csharp
   // Preservare liste:
   // - Item 1
   // - Item 2
   // Non fare merge se linea inizia con: -, •, *, 1., a), etc.
   ```

**Test**: Aggiungere test a `PdfTextExtractionServiceTests.cs`
- Test hyphenation fix
- Test bullet point preservation
- Test header removal
- Test smart paragraph merge

---

### Fase 4: Misurazione Qualità e Coverage (Effort: 0.5)

#### 4.1 Coverage Metrics
**File**: `apps/api/tests/Api.Tests/RealPdfExtractionTests.cs` (già creato)

**Metriche**:
1. **Characters per Page**: `totalChars / pageCount`
   - POOR: <100 chars/page → likely scanned, needs OCR
   - FAIR: 100-500 chars/page
   - GOOD: 500-1500 chars/page
   - EXCELLENT: >1500 chars/page

2. **Extraction Coverage** (manuale):
   - Leggere PDF originale
   - Contare parole campione (es. keywords specifiche)
   - Verificare presenza in testo estratto
   - Target: >90% keyword match

3. **OCR Confidence** (se applicato):
   - Target: >0.85 mean confidence

#### 4.2 Baseline Tests
Eseguire test su PDFs reali:
```bash
dotnet test --filter "RealPdfExtractionTests" --verbosity detailed
```

Output atteso:
```
=== HARMONIES RULES EXTRACTION RESULTS ===
Success: True
Pages: 7
Characters: 8450
Chars/Page: 1207
Quality: GOOD
Used OCR: False

=== LORENZO RULES EXTRACTION RESULTS ===
Success: True
Pages: 24
Characters: 18200
Chars/Page: 758
Quality: GOOD
Used OCR: False
```

#### 4.3 Acceptance Test
Creare test per verificare AC:
```csharp
[Fact]
public async Task PDF02_AcceptanceCriteria_MoreThan90PercentParsed()
{
    // Per ogni PDF di test
    // 1. Estrarre testo
    // 2. Validare coverage >90% (keyword match)
    // 3. Assert success
}
```

---

### Fase 5: Ottimizzazioni e Tuning (Effort: 0.5)

#### 5.1 Performance
1. **Caching OCR Results**
   - Evitare re-OCR degli stessi PDF
   - Cache in `VectorDocumentEntity` o file system

2. **Parallel Page Processing**
   ```csharp
   var tasks = Enumerable.Range(0, pageCount)
       .Select(i => _ocrService.ExtractTextFromPageAsync(pdfPath, i, ct))
       .ToArray();
   var results = await Task.WhenAll(tasks);
   ```

3. **Resource Management**
   - Dispose TesseractEngine properly
   - Limit concurrent OCR operations (semaphore)

#### 5.2 Configurazione Avanzata
**File**: `appsettings.json`

```json
{
  "PdfExtraction": {
    "Docnet": {
      "PageDimensions": {
        "Width": 1080,
        "Height": 1920
      }
    },
    "Ocr": {
      "Enabled": true,
      "MinCharsPerPageThreshold": 100,
      "DefaultLanguage": "eng",
      "AdditionalLanguages": ["ita"],
      "EngineMode": "LstmOnly",
      "PageSegmentationMode": "Auto",
      "DPI": 300,
      "ParallelPages": 4
    },
    "Normalization": {
      "RemoveHeaders": true,
      "FixHyphenation": true,
      "SmartParagraphMerge": true,
      "PreserveBullets": true
    }
  }
}
```

---

## Timeline e Priorità

### Sprint 1 (Effort: 2)
- ✅ Fase 1.1-1.2: Setup Tesseract + tessdata
- ✅ Fase 1.3-1.4: IOcrService + TesseractOcrService
- ✅ Fase 1.5: DI registration + basic tests

**Output**: OCR service funzionante e testato

### Sprint 2 (Effort: 2)
- ✅ Fase 2.1: Integrazione fallback in PdfTextExtractionService
- ✅ Fase 2.2: Update result model
- ✅ Tests OCR integration

**Output**: Fallback OCR operativo

### Sprint 3 (Effort: 1)
- ✅ Fase 3.1: Enhanced normalization
- ✅ Fase 4.1-4.3: Coverage metrics + acceptance tests

**Output**: >90% coverage verificato

### Sprint 4 (Effort: 0.5 - opzionale)
- Fase 5: Ottimizzazioni e tuning

---

## Rischi e Mitigazioni

### Rischio 1: Tessdata File Size
**Problema**: tessdata files sono grandi (~10-50MB per lingua)
**Mitigazione**:
- Usare solo `eng.traineddata` inizialmente
- Valutare `eng.traineddata` best/fast versions
- Escludere da version control, download in CI/CD

### Rischio 2: OCR Performance
**Problema**: OCR è lento su PDFs grandi
**Mitigazione**:
- Applicare solo a pagine con low text density
- Parallel processing con semaphore limit
- Background job processing per PDFs grandi

### Rischio 3: OCR Accuracy
**Problema**: OCR può produrre testo errato (confidence bassa)
**Mitigazione**:
- Check mean confidence threshold (es. >0.7)
- Fallback a Docnet result se OCR confidence troppo bassa
- Logging dettagliato per debugging

### Rischio 4: Linux/Docker Compatibility
**Problema**: Tesseract richiede native libraries
**Mitigazione**:
- Verificare Tesseract in Dockerfile
- Aggiungere libtesseract-dev al container
- Test in ambiente Docker simile a prod

---

## Definition of Done

- [ ] IOcrService interface implementata
- [ ] TesseractOcrService con Tesseract 5.2.0
- [ ] Fallback OCR integrato in PdfTextExtractionService
- [ ] Threshold configurabile via appsettings.json
- [ ] Enhanced paragraph normalization (hyphenation, headers, bullets)
- [ ] Test coverage >80% per nuovo codice
- [ ] RealPdfExtractionTests passing su entrambi i PDF di test
- [ ] Coverage >90% misurato e verificato
- [ ] Documentazione API aggiornata
- [ ] CI/CD passing con nuovi test
- [ ] Docker build successful con Tesseract dependencies

---

## Note per l'Implementazione

### Esempio Docnet → Pix per OCR
```csharp
using var library = DocLib.Instance;
using var docReader = library.GetDocReader(pdfPath, new PageDimensions(1080, 1920));
using var pageReader = docReader.GetPageReader(pageIndex);

// Render page to Pix for Tesseract
var width = pageReader.GetPageWidth();
var height = pageReader.GetPageHeight();
// ... convert to Pix format ...
```

### Esempio Tesseract Usage
```csharp
using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
using var pix = Pix.LoadFromFile(imagePath);
using var page = engine.Process(pix);

var text = page.GetText();
var confidence = page.GetMeanConfidence();

return new OcrResult
{
    Success = true,
    ExtractedText = text,
    MeanConfidence = confidence
};
```

---

## Riferimenti

- Issue: #275 (PDF-02)
- Dependency: #274 (PDF-01) ✅ CLOSED
- Tesseract.NET: https://github.com/charlesw/tesseract
- Tessdata: https://github.com/tesseract-ocr/tessdata
- Docnet.Core: https://github.com/GowenGit/docnet
- Test PDFs: `./data/Test-EN-*.pdf`
