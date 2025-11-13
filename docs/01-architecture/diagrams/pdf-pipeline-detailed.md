# Pipeline PDF Processing - Diagrammi Dettagliati

## 3-Stage PDF Extraction Pipeline (Fallback Architecture)

```mermaid
flowchart TD
    Start([PDF Upload<br/>POST /api/v1/ingest/pdf]) --> Save[Save PDF File<br/>Create PdfDocument entity]

    Save --> Queue[Enqueue IndexPdfCommand<br/>Background job]

    Queue --> Orchestrator[EnhancedPdfProcessingOrchestrator<br/>ExtractTextWithFallbackAsync]

    Orchestrator --> CopyStream[Copy PDF Stream<br/>to byte array<br/>for reuse across stages]

    CopyStream --> Stage1Start[🚀 STAGE 1: Unstructured]

    subgraph "Stage 1: Unstructured (Primary - 80% success)"
        Stage1Start --> S1HTTP[HTTP POST to<br/>Unstructured Service :8001]
        S1HTTP --> S1Wait[Wait response<br/>Timeout: 35s]
        S1Wait --> S1Parse[Parse response<br/>quality_score: 0.0-1.0]
        S1Parse --> S1Quality{Quality Score<br/>>= 0.80?}

        S1Quality -->|Yes ✓| S1Success[✓ STAGE 1 SUCCESS<br/>Quality: High<br/>Avg time: ~1.3s]
        S1Quality -->|No ✗| S1Fail[✗ Quality insufficient<br/>Proceed to Stage 2]
    end

    S1Success --> QualityValidation
    S1Fail --> Stage2Start

    Stage2Start[🚀 STAGE 2: SmolDocling VLM]

    subgraph "Stage 2: SmolDocling (Fallback 1 - 15% success)"
        Stage2Start --> S2Reset[Reset stream<br/>from byte array]
        S2Reset --> S2HTTP[HTTP POST to<br/>SmolDocling Service :8002]
        S2HTTP --> S2Wait[Wait response<br/>Timeout: 60s<br/>Retry: 3x exponential backoff]
        S2Wait --> S2Parse[Parse response<br/>VLM quality_score]
        S2Parse --> S2Quality{Quality Score<br/>>= 0.70?}

        S2Quality -->|Yes ✓| S2Success[✓ STAGE 2 SUCCESS<br/>Quality: Medium/High<br/>Avg time: 3-5s]
        S2Quality -->|No ✗| S2Fail[✗ Quality insufficient<br/>Proceed to Stage 3]
    end

    S2Success --> QualityValidation
    S2Fail --> Stage3Start

    Stage3Start[🚀 STAGE 3: Docnet]

    subgraph "Stage 3: Docnet (Fallback 2 - 5% success)"
        Stage3Start --> S3Reset[Reset stream<br/>from byte array]
        S3Reset --> S3Save[Save to temp file<br/>Docnet requires file path]
        S3Save --> S3Semaphore[Acquire semaphore<br/>Max 4 concurrent]
        S3Semaphore --> S3Extract[Docnet extraction<br/>Native library]
        S3Extract --> S3Assess[Assess quality<br/>chars/page calculation]
        S3Assess --> S3OCR{Should trigger<br/>OCR?<br/>< 100 chars/page}

        S3OCR -->|Yes| S3Tesseract[Tesseract OCR<br/>Full page scan]
        S3OCR -->|No| S3Done
        S3Tesseract --> S3CompareOCR{OCR better<br/>than raw?}
        S3CompareOCR -->|Yes| S3UseOCR[Use OCR result]
        S3CompareOCR -->|No| S3UseRaw[Use raw extraction]

        S3UseOCR --> S3Done[✓ STAGE 3 COMPLETE<br/>Best effort<br/>Avg time: 2-3s]
        S3UseRaw --> S3Done
        S3Done --> S3Cleanup[Cleanup temp file<br/>Release semaphore]
    end

    S3Cleanup --> QualityValidation

    QualityValidation[PdfQualityValidationDomainService<br/>ValidateExtractionQuality]

    subgraph "Quality Validation Service"
        QualityValidation --> QMetrics[Calculate 4 Quality Metrics]

        QMetrics --> M1[1. Text Coverage Score<br/>40% weight<br/>chars/page normalized]
        QMetrics --> M2[2. Structure Detection Score<br/>20% weight<br/>titles, headers, lists]
        QMetrics --> M3[3. Table Detection Score<br/>20% weight<br/>game rules tables]
        QMetrics --> M4[4. Page Coverage Score<br/>20% weight<br/>all pages processed]

        M1 --> Aggregate[Aggregate Total Score<br/>Weighted average]
        M2 --> Aggregate
        M3 --> Aggregate
        M4 --> Aggregate

        Aggregate --> ThresholdCheck{Total Score<br/>comparison}

        ThresholdCheck -->|>= 0.80| Excellent[✓ Excellent Quality<br/>Suitable for RAG]
        ThresholdCheck -->|0.70-0.80| Good[⚠ Good Quality<br/>May need review]
        ThresholdCheck -->|0.50-0.70| Poor[⚠ Poor Quality<br/>Fallback recommended]
        ThresholdCheck -->|< 0.50| Critical[✗ Critical<br/>Document may be corrupted]
    end

    Excellent --> GenerateReport
    Good --> GenerateReport
    Poor --> GenerateReport
    Critical --> GenerateReport

    GenerateReport[Generate Quality Report<br/>Recommendations + Metrics]

    GenerateReport --> UpdateEntity[Update PdfDocument Entity]

    subgraph "Entity Update"
        UpdateEntity --> SetText[ExtractedText = normalized text]
        UpdateEntity --> SetQuality[ExtractionQuality = enum]
        UpdateEntity --> SetStatus[ProcessingStatus = 'completed']
        UpdateEntity --> SetMetadata[StageUsed, Duration, Metrics]
    end

    SetMetadata --> Indexing[Start Indexing Pipeline<br/>IndexPdfCommandHandler]

    subgraph "Indexing Pipeline"
        Indexing --> Chunking[TextChunkingService<br/>Sentence-level chunking]
        Chunking --> Embedding[EmbeddingService<br/>Generate embeddings<br/>Ollama: nomic-embed-text]
        Embedding --> Qdrant[QdrantService<br/>Index vector chunks]
        Qdrant --> Complete[✓ Indexing Complete<br/>Ready for RAG queries]
    end

    Complete --> End([Pipeline Complete])

    style Stage1Start fill:#81c784
    style S1Success fill:#66bb6a
    style Stage2Start fill:#64b5f6
    style S2Success fill:#42a5f5
    style Stage3Start fill:#ffb74d
    style S3Done fill:#ffa726
    style Excellent fill:#66bb6a
    style Critical fill:#ef5350
    style Complete fill:#ab47bc
```

## Stage Decision Tree

```mermaid
flowchart TD
    PDF[PDF Document] --> Stage1{Stage 1:<br/>Unstructured}

    Stage1 -->|Quality >= 0.80<br/>~80% of cases| Done1[✓ Use Stage 1<br/>Fast + High Quality<br/>1.3s avg]

    Stage1 -->|Quality < 0.80<br/>~20% of cases| Stage2{Stage 2:<br/>SmolDocling VLM}

    Stage2 -->|Quality >= 0.70<br/>~15% of cases| Done2[✓ Use Stage 2<br/>Complex layouts<br/>3-5s avg]

    Stage2 -->|Quality < 0.70<br/>~5% of cases| Stage3{Stage 3:<br/>Docnet + OCR}

    Stage3 --> Done3[✓ Use Stage 3<br/>Best effort<br/>2-3s avg]

    Done1 --> Report[Quality Report<br/>+ Recommendations]
    Done2 --> Report
    Done3 --> Report

    style Done1 fill:#66bb6a
    style Done2 fill:#42a5f5
    style Done3 fill:#ffa726
```

## Quality Scoring Formula

```mermaid
flowchart LR
    subgraph "Text Coverage (40%)"
        TC[chars/page]
        TC --> TCCalc{chars/page<br/>threshold}
        TCCalc -->|< 500| TC1[score = chars/500 * 0.5]
        TCCalc -->|500-1000| TC2[score = 0.5 + (chars-500)/500 * 0.5]
        TCCalc -->|>= 1000| TC3[score = 1.0]
        TC1 --> TCS[Text Coverage Score]
        TC2 --> TCS
        TC3 --> TCS
    end

    subgraph "Structure Detection (20%)"
        SD[Detected elements]
        SD --> SDE[Titles, headers,<br/>lists, paragraphs]
        SDE --> SDS[Structure Score<br/>0.0 - 1.0]
    end

    subgraph "Table Detection (20%)"
        TD[Table count]
        TD --> TDE[Game rules tables,<br/>setup tables]
        TDE --> TDS[Table Score<br/>0.0 - 1.0]
    end

    subgraph "Page Coverage (20%)"
        PC[Pages processed]
        PC --> PCCalc[processed / total]
        PCCalc --> PCS[Page Score<br/>0.0 - 1.0]
    end

    TCS --> Weighted[Weighted Sum]
    SDS --> Weighted
    TDS --> Weighted
    PCS --> Weighted

    Weighted --> Total[Total Quality Score<br/>0.0 - 1.0]

    Total --> Thresholds{Thresholds}
    Thresholds -->|>= 0.80| High[High Quality]
    Thresholds -->|0.70-0.80| Medium[Medium Quality]
    Thresholds -->|0.50-0.70| Low[Low Quality]
    Thresholds -->|< 0.50| VeryLow[Very Low Quality]

    style Weighted fill:#ffa726
    style Total fill:#66bb6a
```

## Text Processing Domain Service

```mermaid
flowchart TD
    Raw[Raw Extracted Text] --> Normalize[PdfTextProcessingDomainService<br/>NormalizeText]

    subgraph "Normalization Steps"
        Normalize --> Step1[1. Normalize line endings<br/>\r\n → \n]
        Step1 --> Step2[2. Remove excessive whitespace<br/>Multiple spaces → single space]
        Step2 --> Step3[3. Fix broken words<br/>Rejoin mid-word breaks]
        Step3 --> Step4[4. Normalize newlines<br/>Max 2 consecutive]
        Step4 --> Step5[5. Unicode normalization<br/>Form C canonical]
        Step5 --> Step6[6. Remove zero-width chars<br/>Cleanup invisible chars]
    end

    Step6 --> Assess[AssessQuality<br/>Calculate quality enum]

    subgraph "Quality Assessment"
        Assess --> CalcCPP[Calculate chars/page]
        CalcCPP --> Check{chars/page<br/>value?}
        Check -->|> 1000| QHigh[Quality: HIGH]
        Check -->|> 200| QMedium[Quality: MEDIUM]
        Check -->|> 50| QLow[Quality: LOW]
        Check -->|<= 50| QVeryLow[Quality: VERY_LOW]
    end

    QHigh --> Map[Map to Score]
    QMedium --> Map
    QLow --> Map
    QVeryLow --> Map

    subgraph "Score Mapping"
        Map --> MapH[High → 0.85]
        Map --> MapM[Medium → 0.70]
        Map --> MapL[Low → 0.50]
        Map --> MapVL[VeryLow → 0.25]
    end

    MapH --> Result[Quality Score Result]
    MapM --> Result
    MapL --> Result
    MapVL --> Result

    style Normalize fill:#4fc3f7
    style Assess fill:#ffa726
    style Result fill:#66bb6a
```

## OCR Decision Logic (Stage 3)

```mermaid
flowchart TD
    Docnet[Docnet Extraction<br/>Complete] --> Chars[Count characters<br/>extracted]

    Chars --> Pages[Get page count]

    Pages --> Calc[Calculate<br/>chars/page]

    Calc --> Threshold{chars/page<br/>< 100?}

    Threshold -->|No| UseDocnet[✓ Use Docnet result<br/>Sufficient quality]

    Threshold -->|Yes| CheckOCR{OCR enabled<br/>in config?}

    CheckOCR -->|No| UseDocnet

    CheckOCR -->|Yes| TriggerOCR[🔍 Trigger Tesseract OCR]

    subgraph "OCR Processing"
        TriggerOCR --> OCRScan[Full page scan<br/>Image to text]
        OCRScan --> OCRNormalize[Normalize OCR text]
        OCRNormalize --> OCRQuality[Assess OCR quality]
    end

    OCRQuality --> Compare{OCR quality<br/>> Docnet quality?}

    Compare -->|Yes| UseOCR[✓ Use OCR result<br/>Better quality]
    Compare -->|No| UseDocnet

    UseDocnet --> Output[Final Extraction Result]
    UseOCR --> Output

    style TriggerOCR fill:#ffa726
    style UseOCR fill:#66bb6a
    style UseDocnet fill:#81c784
```

## Concurrency Control (Docnet Semaphore)

```mermaid
sequenceDiagram
    participant R1 as Request 1
    participant R2 as Request 2
    participant R3 as Request 3
    participant R4 as Request 4
    participant R5 as Request 5
    participant S as SemaphoreSlim(4,4)
    participant D as Docnet Library

    Note over S: Initial count: 4

    R1->>S: WaitAsync()
    S-->>R1: Acquired (count: 3)
    R1->>D: Extract PDF 1

    R2->>S: WaitAsync()
    S-->>R2: Acquired (count: 2)
    R2->>D: Extract PDF 2

    R3->>S: WaitAsync()
    S-->>R3: Acquired (count: 1)
    R3->>D: Extract PDF 3

    R4->>S: WaitAsync()
    S-->>R4: Acquired (count: 0)
    R4->>D: Extract PDF 4

    Note over S: Semaphore full

    R5->>S: WaitAsync()
    Note over R5: BLOCKED<br/>Waiting for slot

    D-->>R1: Complete
    R1->>S: Release()
    Note over S: count: 1

    S-->>R5: Acquired (count: 0)
    R5->>D: Extract PDF 5

    D-->>R2: Complete
    R2->>S: Release()

    D-->>R3: Complete
    R3->>S: Release()

    D-->>R4: Complete
    R4->>S: Release()

    D-->>R5: Complete
    R5->>S: Release()

    Note over S: Final count: 4
```

**Rationale**: Docnet.Core is NOT thread-safe. Semaphore limits to max 4 concurrent operations to prevent crashes.

## Pipeline Performance Metrics

```mermaid
graph TB
    subgraph "Stage 1: Unstructured"
        S1Metric1[Success Rate: ~80%]
        S1Metric2[Avg Latency: 1.3s]
        S1Metric3[Quality Threshold: >= 0.80]
        S1Metric4[Timeout: 35s]
    end

    subgraph "Stage 2: SmolDocling"
        S2Metric1[Success Rate: ~15% fallback]
        S2Metric2[Avg Latency: 3-5s]
        S2Metric3[Quality Threshold: >= 0.70]
        S2Metric4[Timeout: 60s + retry]
    end

    subgraph "Stage 3: Docnet"
        S3Metric1[Success Rate: ~5% fallback]
        S3Metric2[Avg Latency: 2-3s]
        S3Metric3[Quality: Best effort]
        S3Metric4[OCR: If < 100 chars/page]
    end

    subgraph "Overall Pipeline"
        OMetric1[Total Success: 100%]
        OMetric2[P50 Latency: 1.5s]
        OMetric3[P95 Latency: 5s]
        OMetric4[P99 Latency: 8s]
    end

    S1Metric1 --> Aggregate[Pipeline Aggregate]
    S2Metric1 --> Aggregate
    S3Metric1 --> Aggregate
    Aggregate --> OMetric1

    style S1Metric1 fill:#66bb6a
    style S2Metric1 fill:#42a5f5
    style S3Metric1 fill:#ffa726
    style OMetric1 fill:#ab47bc
```

## Error Handling Strategy

```mermaid
flowchart TD
    Request[PDF Extraction Request] --> Try1[Try Stage 1<br/>Unstructured]

    Try1 --> Check1{Success?}

    Check1 -->|Yes| Quality1{Quality<br/>>= 0.80?}
    Check1 -->|Error: Timeout| Log1[Log error:<br/>Unstructured timeout]
    Check1 -->|Error: HTTP| Log2[Log error:<br/>Service unavailable]
    Check1 -->|Error: JSON| Log3[Log error:<br/>Invalid response]

    Quality1 -->|Yes| Done
    Quality1 -->|No| Try2

    Log1 --> Try2[Try Stage 2<br/>SmolDocling]
    Log2 --> Try2
    Log3 --> Try2

    Try2 --> Check2{Success?}

    Check2 -->|Yes| Quality2{Quality<br/>>= 0.70?}
    Check2 -->|Error: Timeout| Log4[Log error:<br/>SmolDocling timeout]
    Check2 -->|Error: Circuit Open| Log5[Log error:<br/>Circuit breaker open]
    Check2 -->|Error: Retry Failed| Log6[Log error:<br/>Max retries exceeded]

    Quality2 -->|Yes| Done
    Quality2 -->|No| Try3

    Log4 --> Try3[Try Stage 3<br/>Docnet]
    Log5 --> Try3
    Log6 --> Try3

    Try3 --> Check3{Success?}

    Check3 -->|Yes| Done[✓ Return Result<br/>Any quality]
    Check3 -->|Error: Native Crash| Recover1[Catch SEHException]
    Check3 -->|Error: File Access| Recover2[Catch IOException]
    Check3 -->|Error: Unsupported| Recover3[Catch NotSupportedException]

    Recover1 --> Fallback[Final Fallback:<br/>Return empty with error]
    Recover2 --> Fallback
    Recover3 --> Fallback

    Fallback --> UpdateFailed[Update PdfDocument<br/>Status: 'failed'<br/>ErrorMessage: details]

    UpdateFailed --> NotifyAdmin[Send Alert<br/>Admin notification]

    style Done fill:#66bb6a
    style UpdateFailed fill:#ef5350
    style NotifyAdmin fill:#ffa726
```

## Configuration Options

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Orchestrator",
      "UnstructuredService": {
        "BaseUrl": "http://unstructured-service:8001",
        "TimeoutSeconds": 35,
        "MaxRetries": 3,
        "Strategy": "fast",
        "Language": "ita"
      },
      "SmolDoclingService": {
        "BaseUrl": "http://smoldocling-service:8002",
        "TimeoutSeconds": 60,
        "MaxRetries": 3,
        "CircuitBreakerThreshold": 5,
        "CircuitBreakerTimeoutSeconds": 30
      },
      "DocnetExtractor": {
        "MaxConcurrentOperations": 4,
        "EnableOcrFallback": true,
        "OcrThresholdCharsPerPage": 100
      }
    },
    "Quality": {
      "MinimumThreshold": 0.80,
      "WarningThreshold": 0.70,
      "CriticalThreshold": 0.50,
      "MinCharsPerPage": 500,
      "IdealCharsPerPage": 1000
    },
    "Chunking": {
      "Strategy": "Sentence",
      "ChunkSize": 1000,
      "ChunkOverlap": 200
    }
  }
}
```

---

**Versione**: 1.0
**Data**: 2025-11-13
**Pipeline**: 3-Stage PDF Extraction with Quality-Based Fallback
