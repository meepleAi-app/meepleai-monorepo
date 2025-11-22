# Backend Implementation Plan - Board Game AI

**Last Updated**: 2025-11-12
**Status**: 🟡 MONTH 4-6 IN PROGRESS - Phase 0-3 Complete!
**Timeline**: 28 settimane (~7 mesi) - **Weeks 15-28 remaining**
**Focus**: Quality Framework → Dataset → Polish → Launch

---

## 📊 Current Status

**Overall Backend Progress**: ~40/~78 backend issues complete (~51%)

| Phase | Issues | Complete | Status |
|-------|--------|----------|--------|
| **Phase 0** | ~10 | ~10 | ✅ COMPLETE (100%) |
| **Month 1** | ~13 | ~13 | ✅ COMPLETE (100%) |
| **Month 2** | ~12 | ~12 | ✅ COMPLETE (100%) |
| **Month 3** | ~13 | ~13 | ✅ COMPLETE (100%) |
| **Month 4** | ~14 | ~7 | 🟡 IN PROGRESS (50%) |
| **Month 5** | ~10 | ~5 | 🟡 IN PROGRESS (50%) |
| **Month 6** | ~6 | ~3 | 🟡 IN PROGRESS (50%) |

**Achievement**: Phase 0-3 fully operational! PDF pipeline, LLM integration, and validation framework all live.

**Current Sprint**: Month 4-6 (Quality + Dataset + Polish) - 31 issues remaining

---

## ✅ PHASE 0: ARCHITECTURE FOUNDATION - COMPLETE

### Sprint 1: Architecture Decision & DDD Migration ✅

#### Issue #925: AI Agents Architecture Decision ✅ CLOSED
**Status**: ✅ CLOSED
**Priority**: Was 🔴 CRITICAL (now complete)
**Duration**: Completed
**Achievement**: Architecture decisions finalized
**Deliverables** (Achieved):
- ADR documenting AI agent architecture
- Decision: Monolithic vs Microservices approach for AI agents
- Component diagram showing agent interactions
- Technology stack decisions (Ollama, OpenRouter, etc.)

**Tasks**:
1. Review existing RAG architecture (1h)
2. Research AI agent patterns (4h)
3. Evaluate scalability requirements (2h)
4. Draft ADR with 3 architecture options (4h)
5. Stakeholder review and decision (2h)
6. Finalize and document chosen architecture (2h)

**Success Criteria**:
- ✅ ADR approved and documented
- ✅ Clear architectural direction for BGAI
- ✅ No architectural refactoring needed in Month 1-6

---

#### Issue #940: Migrate PdfTextExtractionService to IPdfTextExtractor
**Status**: 🔴 BLOCKED (Waiting for #925 decision)
**Priority**: 🔴 CRITICAL (BLOCKER for Month 1)
**Duration**: 2-3 giorni
**Dependencies**: #925 (architecture clarity) - MUST WAIT
**Blocks**: All Month 1 PDF processing issues (#946-957)
**Can Start**: After #925 complete (Week 2)
**Deliverables**:
- `IPdfTextExtractor` interface definition
- Adapter implementation for existing service
- Unit tests (90%+ coverage)
- Migration documentation

**Tasks**:
1. Define `IPdfTextExtractor` interface (2h)
   ```csharp
   public interface IPdfTextExtractor
   {
       Task<PdfExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken ct);
       bool CanHandle(PdfDocument document);
       int Priority { get; }
   }
   ```
2. Create adapter for existing `PdfTextExtractionService` (4h)
3. Update DI registration in `Program.cs` (1h)
4. Write unit tests (4h)
5. Update integration tests (2h)
6. Documentation and code review (2h)

**Success Criteria**:
- ✅ Clean interface abstraction
- ✅ Zero breaking changes to existing functionality
- ✅ 90%+ test coverage
- ✅ Ready for Month 1 multi-extractor strategy

---

## MONTH 1: PDF PROCESSING PIPELINE (Settimane 3-6)

### Sprint 3-4: Multi-Extractor Implementation

#### Issue #946: Docker Configuration for pdf-processor
**Priority**: 🟡 HIGH
**Duration**: 1-2 giorni
**Dependencies**: None (can run parallel)
**Deliverables**:
- `docker-compose.yml` service definition
- Dockerfile for pdf-processor
- Environment configuration
- Health check endpoints

**Tasks**:
1. Create Dockerfile for Python pdf-processor (2h)
2. Add service to `docker-compose.yml` (1h)
3. Configure networking and volumes (2h)
4. Implement health check endpoint (1h)
5. Test local deployment (2h)
6. Documentation (1h)

---

#### Issue #953: UnstructuredPdfExtractor (C# Client)
**Priority**: 🟡 HIGH
**Duration**: 3-4 giorni
**Dependencies**: #946 (Docker setup), #940 (interface)
**Deliverables**:
- `UnstructuredPdfExtractor` implementing `IPdfTextExtractor`
- HTTP client for Python service
- Retry logic and error handling
- Configuration via appsettings.json

**Tasks**:
1. Implement `UnstructuredPdfExtractor` class (4h)
2. HTTP client with Polly retry policies (3h)
3. Response deserialization and mapping (2h)
4. Configuration integration (1h)
5. Error handling and logging (2h)
6. Code review (1h)

**Code Structure**:
```csharp
public class UnstructuredPdfExtractor : IPdfTextExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UnstructuredPdfExtractor> _logger;

    public int Priority => 1; // Primary extractor

    public async Task<PdfExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken ct)
    {
        // HTTP call to Python service
        // Error handling with Polly
        // Parse and return result
    }
}
```

---

#### Issue #954: Unit Tests for Unstructured Integration
**Priority**: 🟢 MEDIUM
**Duration**: 1-2 giorni
**Dependencies**: #953
**Deliverables**: 12 test cases covering happy path, errors, retries

**Test Cases**:
1. Successful extraction
2. HTTP timeout handling
3. Retry on transient failure
4. Malformed response handling
5. Service unavailable scenario
6. Large PDF handling
7. Corrupted PDF handling
8. Configuration validation
9. Cancellation token support
10. Connection pool exhaustion
11. Rate limiting behavior
12. Memory leak prevention

---

#### Issue #947: SmolDoclingPdfExtractor
**Priority**: 🟡 HIGH
**Duration**: 3-4 giorni
**Dependencies**: #946, #940
**Deliverables**:
- `SmolDoclingPdfExtractor` implementing `IPdfTextExtractor`
- Integration with SmolDocling service
- Priority: 2 (fallback to Unstructured)

**Tasks**: Similar to #953 but for SmolDocling service

---

#### Issue #948: SmolDocling Integration Tests
**Priority**: 🟢 MEDIUM
**Duration**: 1-2 giorni
**Dependencies**: #947
**Deliverables**: 10 integration test cases

---

#### Issue #949: EnhancedPdfProcessingOrchestrator (3-Stage Fallback)
**Priority**: 🔴 CRITICAL
**Duration**: 3-4 giorni
**Dependencies**: #953, #947 (both extractors ready)
**Deliverables**:
- Orchestrator with priority-based fallback
- Quality scoring logic
- Comprehensive logging
- Performance metrics

**Tasks**:
1. Implement orchestrator class (4h)
2. Priority sorting and fallback logic (3h)
3. Quality scoring (confidence calculation) (3h)
4. Telemetry and logging (2h)
5. Unit tests (4h)
6. Documentation (2h)

**Algorithm**:
```csharp
public async Task<PdfExtractionResult> ProcessAsync(Stream pdfStream)
{
    var extractors = _extractors.OrderBy(e => e.Priority);

    foreach (var extractor in extractors)
    {
        try
        {
            var result = await extractor.ExtractAsync(pdfStream, ct);

            if (result.QualityScore >= _minQualityThreshold)
            {
                _logger.LogInformation("Extraction succeeded with {Extractor}", extractor.GetType().Name);
                return result;
            }

            _logger.LogWarning("Quality too low ({Score}), trying next extractor", result.QualityScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extractor {Name} failed", extractor.GetType().Name);
        }
    }

    throw new PdfExtractionException("All extractors failed");
}
```

---

#### Issue #950: End-to-End Pipeline Tests
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #949
**Deliverables**:
- 8 E2E test scenarios
- Testcontainers for Docker services
- Performance benchmarks

**Test Scenarios**:
1. Simple PDF → Primary extractor success
2. Complex PDF → Fallback to secondary
3. Corrupted PDF → All extractors fail gracefully
4. Large PDF (100+ pages) → Performance validation
5. Scanned PDF → OCR handling
6. Multi-language PDF → UTF-8 encoding
7. Password-protected PDF → Error handling
8. Concurrent requests → Thread safety

---

#### Issue #951: PDF Quality Validation (≥0.80 Score)
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #949
**Deliverables**:
- Quality scoring algorithm
- Validation rules (text length, structure, encoding)
- Acceptance tests with real PDFs

**Quality Metrics**:
- Text extraction completeness (40%)
- Character encoding validity (20%)
- Structure preservation (tables, lists) (20%)
- Metadata extraction (10%)
- Performance (extraction time) (10%)

---

#### Issue #955: Bug Fixes and Edge Cases
**Priority**: 🟢 MEDIUM
**Duration**: 2-3 giorni
**Dependencies**: #950 (E2E tests reveal bugs)
**Deliverables**: Bug fixes for issues found during testing

---

#### Issue #956: Code Review Checklist
**Priority**: 🟢 LOW
**Duration**: 1 giorno
**Dependencies**: All Month 1 issues
**Deliverables**:
- Code review checklist document
- PR review with checklist applied
- Refactoring if needed

---

#### Issue #957: Documentation
**Priority**: 🟢 MEDIUM
**Duration**: 1-2 giorni
**Dependencies**: All Month 1 issues
**Deliverables**:
- README for PDF processing module
- API documentation (XML comments)
- Architecture diagram
- Deployment guide

---

## MONTH 2: LLM INTEGRATION (Settimane 7-10)

### Sprint 5-6: Adaptive LLM Service

#### Issue #958: Evaluate LLM Strategy
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: None
**Deliverables**:
- Strategy document comparing:
  - **Option A**: Ollama-only (local, cost-effective)
  - **Option B**: OpenRouter + Ollama (hybrid, fallback)
- Cost analysis
- Performance benchmarks
- Final recommendation

**Evaluation Criteria**:
- Latency (P50, P95, P99)
- Cost per 1000 requests
- Availability and reliability
- Model quality (GPT-4, Claude, Llama)
- Scalability

---

#### Issue #959: OllamaClient Implementation
**Priority**: 🔴 CRITICAL
**Duration**: 3-4 giorni
**Dependencies**: #958 (strategy decision)
**Deliverables**:
- `OllamaClient` class with HTTP client
- Model management (download, load, unload)
- Request/response handling
- Configuration integration

**Tasks**:
1. Implement `IOllamaClient` interface (2h)
2. HTTP client for Ollama API (4h)
3. Model management endpoints (3h)
4. Streaming response support (3h)
5. Error handling and retries (2h)
6. Configuration (appsettings.json) (1h)

---

#### Issue #960: OpenRouterClient (if Option B)
**Priority**: 🟡 HIGH (conditional)
**Duration**: 3-4 giorni
**Dependencies**: #958 (if Option B chosen)
**Deliverables**:
- `OpenRouterClient` implementing `ILlmClient`
- API key management
- Rate limiting handling
- Cost tracking

---

#### Issue #961: Unit Tests for LLM Clients
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: #959, #960
**Deliverables**: 15+ unit tests per client

---

#### Issue #962: AdaptiveLlmService (Routing Logic)
**Priority**: 🔴 CRITICAL
**Duration**: 3-4 giorni
**Dependencies**: #959, #960 (clients ready)
**Deliverables**:
- `AdaptiveLlmService` with intelligent routing
- Fallback logic (primary → secondary)
- Circuit breaker pattern
- Performance monitoring

**Routing Algorithm**:
```csharp
public async Task<LlmResponse> GenerateAsync(LlmRequest request)
{
    var provider = _config.PrimaryProvider; // Ollama or OpenRouter

    try
    {
        var client = _providers[provider];
        return await client.GenerateAsync(request, ct);
    }
    catch (Exception ex) when (provider != _config.FallbackProvider)
    {
        _logger.LogWarning(ex, "Primary provider {Provider} failed, trying fallback", provider);

        var fallbackClient = _providers[_config.FallbackProvider];
        return await fallbackClient.GenerateAsync(request, ct);
    }
}
```

---

#### Issue #963: Feature Flag AI:Provider Configuration
**Priority**: 🟢 MEDIUM
**Duration**: 1 giorno
**Dependencies**: #962
**Deliverables**:
- `AI:Provider` configuration section
- Runtime provider switching
- Admin UI for configuration (optional)

**Configuration Example**:
```json
{
  "AI": {
    "Provider": {
      "Primary": "Ollama",
      "Fallback": "OpenRouter",
      "Models": {
        "Ollama": "llama3:70b",
        "OpenRouter": "anthropic/claude-3.5-sonnet"
      }
    }
  }
}
```

---

#### Issue #964: Integration Tests for Adaptive Routing
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #962
**Deliverables**: 10 integration test scenarios

---

#### Issue #965: Replace RagService LLM Calls
**Priority**: 🔴 CRITICAL
**Duration**: 3-4 giorni
**Dependencies**: #962 (AdaptiveLlmService ready)
**Deliverables**:
- Refactor `RagService` to use `AdaptiveLlmService`
- Remove direct OpenRouter dependencies
- Update all LLM call sites

**Migration Pattern**:
```csharp
// Before
var response = await _openRouterClient.GenerateAsync(prompt);

// After
var response = await _adaptiveLlmService.GenerateAsync(new LlmRequest
{
    Prompt = prompt,
    Model = "default", // Uses configured model
    MaxTokens = 1000
});
```

---

#### Issue #966: Backward Compatibility Testing
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: #965
**Deliverables**:
- Regression tests ensuring RAG still works
- Performance comparison (before/after)
- No breaking changes to API contracts

---

#### Issue #967: Performance Testing (P95 <3s)
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #965
**Deliverables**:
- Load testing with k6 or NBomber
- P50, P95, P99 latency measurements
- Throughput analysis (requests/second)
- Optimization recommendations

**Performance Targets**:
- P50: <1.5s
- P95: <3s
- P99: <5s
- Throughput: 50+ req/s

---

#### Issue #968: Cost Tracking
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: #962
**Deliverables**:
- Cost calculation per request
- Prometheus metrics for cost tracking
- Daily/monthly cost reports

**Metrics**:
- `llm_cost_total` (counter)
- `llm_cost_per_request` (histogram)
- `llm_provider_usage` (gauge)

---

#### Issue #969: LLM Integration Documentation + ADR
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: All Month 2 issues
**Deliverables**:
- ADR: LLM strategy decision
- API documentation
- Configuration guide
- Cost optimization tips

---

## MONTH 3: MULTI-MODEL VALIDATION (Settimane 11-14)

### Sprint 7-8: 5-Layer Validation Pipeline

#### Issue #970: ConfidenceValidationService
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: Month 2 (LLM integration)
**Deliverables**:
- Service validating confidence ≥0.70
- Configurable threshold
- Logging and metrics

**Implementation**:
```csharp
public class ConfidenceValidationService : IValidationLayer
{
    public async Task<ValidationResult> ValidateAsync(RagResponse response)
    {
        if (response.Confidence < _minThreshold)
        {
            return ValidationResult.Failed($"Confidence {response.Confidence} below threshold {_minThreshold}");
        }

        return ValidationResult.Success();
    }
}
```

---

#### Issue #971: CitationValidationService
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: Month 2
**Deliverables**:
- Verify all citations exist in source documents
- Check citation format validity
- Detect hallucinated citations

**Validation Logic**:
1. Extract all citations from response
2. Query vector DB for source documents
3. Verify citation text exists in documents
4. Calculate citation accuracy score

---

#### Issue #972: HallucinationDetectionService
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: Month 2
**Deliverables**:
- Forbidden keyword detection
- Confidence phrase analysis
- Factual consistency checks

**Forbidden Patterns**:
- "Non so", "Non posso rispondere"
- "Probabilmente", "Forse", "Potrebbe essere"
- Generic disclaimers without content

---

#### Issue #973: Unit Tests for 3 Validation Layers
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #970, #971, #972
**Deliverables**: 30+ unit tests (10 per layer)

---

#### Issue #974: MultiModelValidationService (GPT-4 + Claude)
**Priority**: 🔴 CRITICAL
**Duration**: 3-4 giorni
**Dependencies**: #962 (AdaptiveLlmService)
**Deliverables**:
- Parallel query to GPT-4 and Claude
- Consensus calculation
- Conflict resolution logic

**Consensus Algorithm**:
```csharp
public async Task<ValidationResult> ValidateAsync(RagResponse response)
{
    var tasks = new[]
    {
        _llmService.GenerateAsync(request with { Model = "gpt-4" }),
        _llmService.GenerateAsync(request with { Model = "claude-3.5-sonnet" })
    };

    var responses = await Task.WhenAll(tasks);

    var similarity = CalculateCosineSimilarity(responses[0], responses[1]);

    if (similarity >= 0.90)
    {
        return ValidationResult.Success();
    }

    return ValidationResult.Failed($"Model disagreement: similarity {similarity}");
}
```

---

#### Issue #975: Consensus Similarity Calculation (≥0.90)
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: #974
**Deliverables**:
- Cosine similarity implementation
- Embedding generation for responses
- Threshold configuration

---

#### Issue #976: Consensus Validation Tests (18 tests)
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #974, #975
**Deliverables**: 18 test cases covering agreement/disagreement

---

#### Issue #977: Wire All 5 Validation Layers in RAG
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: #970-#976 (all layers ready)
**Deliverables**:
- Integration in `RagService`
- Pipeline execution order
- Short-circuit on first failure (optional)
- Comprehensive logging

**Pipeline**:
```csharp
var validationLayers = new IValidationLayer[]
{
    _confidenceValidation,      // Layer 1
    _citationValidation,        // Layer 2
    _hallucinationDetection,    // Layer 3
    _multiModelConsensus,       // Layer 4
    _customBusinessRules        // Layer 5
};

foreach (var layer in validationLayers)
{
    var result = await layer.ValidateAsync(response, ct);

    if (!result.IsValid)
    {
        _logger.LogWarning("Validation failed at {Layer}: {Reason}", layer.Name, result.Reason);
        return RagResponse.Invalid(result.Reason);
    }
}
```

---

#### Issue #978: End-to-End Testing
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: #977
**Deliverables**: 12 E2E scenarios with all validation layers

---

#### Issue #979: Parallel Validation Optimization
**Priority**: 🟢 MEDIUM
**Duration**: 2-3 giorni
**Dependencies**: #977
**Deliverables**:
- Parallel execution of independent layers
- Performance improvement (30-50% faster)

**Optimization**:
```csharp
// Independent layers can run in parallel
var independentTasks = new[]
{
    _confidenceValidation.ValidateAsync(response),
    _citationValidation.ValidateAsync(response),
    _hallucinationDetection.ValidateAsync(response)
};

var results = await Task.WhenAll(independentTasks);

// Then run dependent layer
var consensusResult = await _multiModelConsensus.ValidateAsync(response);
```

---

#### Issue #980: Bug Fixes
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: #978 (E2E reveals bugs)

---

#### Issue #981: Accuracy Baseline Measurement (80%+)
**Priority**: 🟡 HIGH
**Duration**: 1 giorno
**Dependencies**: #977
**Deliverables**:
- Test dataset with ground truth
- Accuracy calculation
- Baseline report

---

#### Issue #982: Update ADRs
**Priority**: 🟢 LOW
**Duration**: 1 giorno
**Dependencies**: All Month 3 issues
**Deliverables**: ADR documenting validation architecture

---

## MONTH 4: QUALITY FRAMEWORK (Settimane 15-18)

### Sprint 9-10: Monitoring & Metrics

#### Issue #983: Extend PromptEvaluationService (5-Metric Framework)
**Priority**: 🟡 HIGH
**Duration**: 3-4 giorni
**Dependencies**: Month 3 (validation layers)
**Deliverables**:
- 5-metric framework implementation
- Integration with validation pipeline
- Prometheus metrics export

**5 Metrics**:
1. **Accuracy** - % correct answers vs ground truth
2. **Precision@10** - Relevance of top 10 retrieved documents
3. **MRR (Mean Reciprocal Rank)** - Position of first correct document
4. **Confidence** - Average confidence score
5. **Hallucination Rate** - % responses with hallucinations

---

#### Issue #984: Automated Evaluation Job (Weekly Cron)
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: #983
**Deliverables**:
- Hangfire job for weekly evaluation
- Email notifications with results
- Trend analysis (week-over-week)

---

#### Issue #985: Prometheus Metrics (BGAI-Specific)
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #983
**Deliverables**:
- Custom Prometheus metrics
- Metric collection middleware
- OpenTelemetry integration

**Metrics**:
```csharp
// Counters
bgai_questions_total
bgai_validation_failures_total

// Histograms
bgai_response_latency_seconds
bgai_confidence_score

// Gauges
bgai_accuracy_percentage
bgai_hallucination_rate
```

---

#### Issue #986: Grafana Dashboard
**Priority**: 🟢 MEDIUM
**Duration**: 2-3 giorni
**Dependencies**: #985
**Deliverables**:
- Grafana dashboard JSON
- 5-metric gauges
- Trend graphs
- Alert rules

---

#### Issue #987: Quality Framework Integration Tests
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: #983-#986

---

## MONTH 5: GOLDEN DATASET (Settimane 19-22)

### Sprint 11-12: Dataset Annotation & API

#### Issues #996-998: Dataset Annotation
**Priority**: 🟡 HIGH
**Duration**: 3-4 giorni per gioco
**Dependencies**: None (can run parallel)
**Deliverables**:
- **#996**: Terraforming Mars - 20 Q&A pairs
- **#997**: Wingspan - 15 Q&A pairs
- **#998**: Azul - 15 Q&A pairs

**Annotation Format** (JSON):
```json
{
  "game": "Terraforming Mars",
  "questions": [
    {
      "id": "tm_001",
      "question": "Come si calcola il punteggio finale?",
      "answer": "Il punteggio finale è dato da: Terraforming Rating + VP dalle carte + VP dalle milestone + VP dalle awards + VP dalle città + VP dai greenery tiles.",
      "citations": [
        {
          "page": 12,
          "section": "Fine Partita",
          "text": "Sommare i punti..."
        }
      ],
      "difficulty": "medium",
      "category": "scoring"
    }
  ]
}
```

---

#### Issue #999: Quality Test Implementation
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: #996-998 (dataset ready)
**Deliverables**:
- Automated test runner
- Accuracy calculation vs ground truth
- Detailed error reports

---

#### Issue #1000: Run First Accuracy Test
**Priority**: 🟡 HIGH
**Duration**: 1 giorno
**Dependencies**: #999
**Deliverables**:
- Baseline accuracy measurement
- Error analysis
- Improvement recommendations

---

#### Issue #1006: Backend API Integration (/api/v1/board-game-ai/ask)
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: Month 4 (quality framework)
**Deliverables**:
- REST API endpoint
- Request/response DTOs
- Swagger documentation
- CORS configuration

**API Contract**:
```csharp
[HttpPost("ask")]
public async Task<ActionResult<AskResponse>> AskQuestion(
    [FromBody] AskRequest request,
    CancellationToken ct)
{
    var response = await _ragService.GenerateResponseAsync(
        request.Question,
        request.GameId,
        ct);

    return Ok(new AskResponse
    {
        Answer = response.Answer,
        Confidence = response.Confidence,
        Citations = response.Citations,
        ValidationResults = response.ValidationResults
    });
}
```

---

#### Issue #1007: Streaming SSE Support
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #1006
**Deliverables**:
- SSE endpoint for streaming responses
- Chunked response generation
- Client-side event handling

**SSE Implementation**:
```csharp
[HttpGet("ask/stream")]
public async IAsyncEnumerable<string> AskQuestionStream(
    [FromQuery] string question,
    [FromQuery] int gameId,
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var chunk in _ragService.GenerateStreamingResponseAsync(question, gameId, ct))
    {
        yield return $"data: {JsonSerializer.Serialize(chunk)}\n\n";
    }
}
```

---

#### Issue #1008: Error Handling and Retry Logic
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: #1006
**Deliverables**:
- Global exception handling middleware
- Retry policies with Polly
- User-friendly error messages

---

#### Issue #1009: Month 5 E2E Testing
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #1006-1008
**Deliverables**: 15 E2E test scenarios

---

## MONTH 6: FINAL POLISH (Settimane 23-28)

### Sprint 13-14: Dataset Expansion & Performance

#### Issues #1010-1012: Expanded Dataset
**Priority**: 🟡 HIGH
**Duration**: 4-5 giorni per batch
**Dependencies**: None
**Deliverables**:
- **#1010**: Scythe, Catan, Pandemic (30 Q&A)
- **#1011**: 7 Wonders, Agricola, Splendor (30 Q&A)
- **#1012**: Adversarial dataset (50 synthetic queries)

**Adversarial Examples**:
- Trick questions
- Ambiguous phrasing
- Multi-game confusion
- Edge cases

---

#### Issue #1017: Game Catalog Page Backend
**Priority**: 🟢 MEDIUM
**Duration**: 2-3 giorni
**Dependencies**: Dataset complete
**Deliverables**:
- `/api/v1/board-game-ai/games` endpoint
- Game metadata (name, description, PDF status)
- Search and filtering

---

#### Issue #1018: E2E Testing (Question → PDF Citation)
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: All Month 6 backend
**Deliverables**: 20 comprehensive E2E tests

---

#### Issue #1019: Accuracy Validation (80% Target)
**Priority**: 🔴 CRITICAL
**Duration**: 1-2 giorni
**Dependencies**: #1010-1012 (100 Q&A ready)
**Deliverables**:
- Final accuracy measurement on 100 Q&A
- Error analysis
- Performance report

**Success Criteria**: ✅ 80%+ accuracy on golden dataset

---

#### Issue #1020: Performance Testing (P95 <3s)
**Priority**: 🟡 HIGH
**Duration**: 1-2 giorni
**Dependencies**: All Month 6
**Deliverables**:
- Load testing results
- Performance optimization
- Scalability recommendations

---

#### Issue #1021: Final Bug Fixes
**Priority**: 🟢 MEDIUM
**Duration**: 2-3 giorni
**Dependencies**: #1018-1020 (testing reveals bugs)

---

#### Issue #1022: Documentation Updates
**Priority**: 🟢 MEDIUM
**Duration**: 1-2 giorni
**Dependencies**: All Month 6
**Deliverables**:
- User guide (Italian)
- API documentation (Swagger)
- Deployment guide
- Troubleshooting FAQ

---

#### Issue #1023: Phase 1A Completion Checklist
**Priority**: 🟡 HIGH
**Duration**: 1 giorno
**Dependencies**: ALL issues
**Deliverables**:
- Completion checklist
- Sign-off document
- Lessons learned
- Phase 1B planning

---

## SUCCESS METRICS

### Technical Metrics
- ✅ PDF extraction success rate: ≥95%
- ✅ RAG response latency (P95): <3s
- ✅ Accuracy on golden dataset: ≥80%
- ✅ Test coverage: ≥90%
- ✅ Hallucination rate: <3%
- ✅ Uptime: ≥99.5%

### Business Metrics
- ✅ 9 board games fully supported
- ✅ 100+ Q&A pairs validated
- ✅ Italian UI complete
- ✅ Production-ready for alpha users

---

## RISK MITIGATION

### Technical Risks
1. **PDF extraction quality** → Mitigated by 3-stage fallback
2. **LLM hallucinations** → Mitigated by 5-layer validation
3. **Performance issues** → Mitigated by early load testing
4. **Integration complexity** → Mitigated by incremental integration

### Operational Risks
1. **Cost overruns (LLM)** → Mitigated by cost tracking and Ollama fallback
2. **Scalability** → Mitigated by performance testing in Month 6
3. **Data quality** → Mitigated by rigorous annotation process

---

**End of Backend Implementation Plan**
