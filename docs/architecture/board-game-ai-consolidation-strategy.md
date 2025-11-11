# Board Game AI Feature Integration - Consolidation Strategy

**Status**: Approved for Implementation
**Version**: 2.0 (Revised with Consolidation)
**Date**: 2025-01-15
**Decision**: Integrate Board Game AI features into existing MeepleAI (ASP.NET Core)

---

## Executive Decision: Enhance Existing System

### ✅ APPROVED: Integrate Board Game AI features into MeepleAI ASP.NET Core

**Rationale**:
- Existing system production-ready (90%+ coverage, DDD architecture, 7 bounded contexts)
- Infrastructure proven (PostgreSQL 16, Qdrant, Redis, observability stack)
- Team expertise in C#/.NET (avoid learning curve)
- Faster time-to-market (extend vs rebuild)
- Single codebase maintenance (reduce complexity)

**Approach**: Add Board Game AI **quality innovations** as new features:
1. ✅ Multi-model LLM validation (OpenRouter: GPT-4 + Claude consensus)
2. ✅ Enhanced PDF processing (LLMWhisperer + SmolDocling pipeline)
3. ✅ Italian-first localization (multilingual embeddings + terminology)
4. ✅ 5-metric quality framework (Accuracy, Hallucination, Confidence, Citation, Latency)
5. ✅ Publisher partnership features (white-label, B2B analytics)

---

## Consolidated Technology Stack

### Backend: ASP.NET Core 9.0 (ENHANCED)

**Core Framework**:
- ASP.NET Core 9.0 Minimal APIs (existing)
- EF Core 9.0 (existing)
- DDD Architecture with 7 bounded contexts (existing)
- **NEW**: Multi-model validation service (OpenRouter API)
- **NEW**: Italian localization services

**PDF Processing** (3-Stage Pipeline):
- **Stage 1 (Primary)**: LLMWhisperer API (C# HttpClient integration)
- **Stage 2 (Fallback)**: SmolDocling Python microservice (FastAPI, GPU-enabled)
- **Stage 3 (Final)**: Docnet.Core + iText7 (existing implementation)

**LLM Strategy** (Feature-Flagged):
- **OpenRouter Enabled** (default for accuracy):
  - Primary: `openai/gpt-4-turbo` via OpenRouter
  - Validation: `anthropic/claude-3.5-sonnet` via OpenRouter
  - Fallback: `google/gemini-pro-1.5` via OpenRouter
- **OpenRouter Disabled** (free/open-source fallback):
  - Primary: Ollama + `mistral:7b-instruct` (self-hosted, free)
  - Validation: Ollama + `llama3.1:8b` (diversity, free)
  - Trade-off: Lower accuracy (~75-80% vs 95%+ with OpenRouter)

---

## PDF Processing Pipeline (HYBRID APPROACH)

### Architecture Decision: LLMWhisperer + SmolDocling + Docnet.Core

**Stage 1: LLMWhisperer** (PRIMARY - Cloud API)
```csharp
// Infrastructure/Adapters/LlmWhispererAdapter.cs
public class LlmWhispererPdfExtractor : IPdfTextExtractor
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public async Task<PdfExtractionResult> ExtractTextAsync(Stream pdfStream)
    {
        var apiKey = _config["LlmWhisperer:ApiKey"];

        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(pdfStream), "file", "document.pdf");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.llmwhisperer.com/v1/extract")
        {
            Headers = { { "Authorization", $"Bearer {apiKey}" } },
            Content = content
        };

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LlmWhispererResponse>();

        return new PdfExtractionResult
        {
            Text = result.Text,
            QualityScore = result.QualityScore,
            PageCount = result.PageCount,
            Method = "LlmWhisperer"
        };
    }
}
```

**Configuration** (appsettings.json):
```json
{
  "LlmWhisperer": {
    "ApiKey": "${LLMWHISPERER_API_KEY}",
    "Enabled": true,
    "TimeoutSeconds": 120,
    "QualityThreshold": 0.80
  }
}
```

---

**Stage 2: SmolDocling** (SECONDARY - Python Microservice)

**Python Microservice** (apps/pdf-processor/):
```python
# apps/pdf-processor/main.py
from fastapi import FastAPI, UploadFile, File, HTTPException
from docling import DocumentConverter
import uvicorn
import logging

app = FastAPI(title="SmolDocling PDF Processor", version="1.0.0")
converter = DocumentConverter()
logger = logging.getLogger(__name__)

@app.post("/api/v1/convert")
async def convert_pdf(file: UploadFile = File(...)):
    """Convert PDF to markdown using SmolDocling vision-language model."""
    try:
        # Convert PDF
        logger.info(f"Processing PDF: {file.filename}")
        result = converter.convert(file.file)

        # Export as markdown
        markdown_text = result.document.export_to_markdown()

        return {
            "text": markdown_text,
            "page_count": len(result.document.pages),
            "format": "markdown",
            "method": "smoldocling"
        }
    except Exception as e:
        logger.error(f"SmolDocling processing failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/health")
async def health():
    return {"status": "healthy", "service": "smoldocling-processor"}

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8001)
```

**C# Client** (Infrastructure/Adapters/SmolDoclingAdapter.cs):
```csharp
public class SmolDoclingPdfExtractor : IPdfTextExtractor
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public async Task<PdfExtractionResult> ExtractTextAsync(Stream pdfStream)
    {
        var smolDoclingUrl = _config["SmolDocling:ServiceUrl"] ?? "http://pdf-processor:8001";

        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(pdfStream), "file", "document.pdf");

        using var response = await _httpClient.PostAsync(
            $"{smolDoclingUrl}/api/v1/convert",
            content
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new PdfProcessingException($"SmolDocling service returned {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<SmolDoclingResponse>();

        return new PdfExtractionResult
        {
            Text = result.Text,
            QualityScore = 0.75m,  // Estimated
            PageCount = result.PageCount,
            Method = "SmolDocling"
        };
    }
}
```

**Docker Compose** (infra/docker-compose.yml - ADD):
```yaml
services:
  pdf-processor:
    build: ../apps/pdf-processor
    ports:
      - "8001:8001"
    environment:
      - LOG_LEVEL=INFO
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]  # Requires NVIDIA GPU or CPU fallback
```

---

**Stage 3: Docnet.Core + iText7** (EXISTING - Keep as Final Fallback)
- Already implemented: `PdfTextExtractionService`, `PdfTableExtractionService`
- No changes needed (DDD Phase 4 complete, 85/86 tests passing)
- Purpose: Proven fallback quando Stage 1+2 fail

---

### LLM Strategy: OpenRouter (Optional) + Ollama Fallback (Free)

**Feature Flag Pattern** (Dynamic Configuration):

```csharp
// appsettings.json
{
  "AI": {
    "Provider": "OpenRouter",  // "OpenRouter" or "Ollama"
    "OpenRouter": {
      "ApiKey": "${OPENROUTER_API_KEY}",
      "Enabled": true,
      "Models": {
        "Primary": "openai/gpt-4-turbo",
        "Validation": "anthropic/claude-3.5-sonnet",
        "Fallback": "google/gemini-pro-1.5"
      },
      "MultiModelValidation": true,
      "ConsensusSimilarityThreshold": 0.90
    },
    "Ollama": {
      "Enabled": true,
      "BaseUrl": "http://ollama:11434",
      "Models": {
        "Primary": "mistral:7b-instruct-v0.3-q4_K_M",
        "Validation": "llama3.1:8b-instruct-q4_K_M"
      },
      "MultiModelValidation": true,
      "ConsensusSimilarityThreshold": 0.85
    }
  }
}
```

**Adaptive LLM Service**:

```csharp
// Services/AdaptiveLlmService.cs
public class AdaptiveLlmService : ILlmService
{
    private readonly IOpenRouterClient _openRouter;
    private readonly IOllamaClient _ollama;
    private readonly IConfigurationService _config;

    public async Task<LlmResponse> GenerateAsync(string prompt, LlmOptions options)
    {
        var provider = await _config.GetValueAsync<string>("AI:Provider", "OpenRouter");

        if (provider == "OpenRouter" && await IsOpenRouterAvailable())
        {
            return await _openRouter.GenerateAsync(prompt, options);
        }
        else
        {
            // Fallback to Ollama (free, self-hosted)
            _logger.LogWarning("OpenRouter unavailable or disabled, using Ollama fallback");
            return await _ollama.GenerateAsync(prompt, options);
        }
    }

    public async Task<MultiModelValidationResult> ValidateWithConsensusAsync(
        string question,
        string primaryAnswer,
        List<string> contextChunks)
    {
        var provider = await _config.GetValueAsync<string>("AI:Provider", "OpenRouter");
        var validationEnabled = await _config.GetValueAsync<bool>($"AI:{provider}:MultiModelValidation", true);

        if (!validationEnabled)
        {
            return new MultiModelValidationResult { ConsensusReached = true, Skipped = true };
        }

        // Get models configuration
        var primaryModel = await _config.GetValueAsync<string>($"AI:{provider}:Models:Primary");
        var validationModel = await _config.GetValueAsync<string>($"AI:{provider}:Models:Validation");

        // Call validation model
        var validationPrompt = BuildValidationPrompt(question, primaryAnswer, contextChunks);

        LlmResponse validationResponse;
        if (provider == "OpenRouter")
        {
            validationResponse = await _openRouter.GenerateAsync(validationPrompt, new LlmOptions
            {
                Model = validationModel,  // "anthropic/claude-3.5-sonnet"
                Temperature = 0.1m,
                MaxTokens = 1024
            });
        }
        else
        {
            validationResponse = await _ollama.GenerateAsync(validationPrompt, new LlmOptions
            {
                Model = validationModel,  // "llama3.1:8b"
                Temperature = 0.1m
            });
        }

        // Calculate consensus similarity
        var similarity = await CalculateSemanticSimilarityAsync(primaryAnswer, validationResponse.Content);
        var threshold = await _config.GetValueAsync<decimal>($"AI:{provider}:ConsensusSimilarityThreshold", 0.90m);

        return new MultiModelValidationResult
        {
            ConsensusReached = similarity >= threshold,
            Similarity = similarity,
            Threshold = threshold,
            ValidationModel = validationModel,
            ValidationResponse = validationResponse.Content
        };
    }
}
```

---

### Vector Database: Qdrant (KEEP EXISTING)

**Decision**: Continue using Qdrant (already deployed, working)

**Enhancements for Board Game AI**:
- ADD: Hybrid search support (Qdrant sparse vectors + dense vectors)
- ADD: Italian-optimized collections (language-specific indexing)
- ADD: Metadata filtering (game_id, publisher, language, difficulty)

**No Migration Needed**: Qdrant already supports all required features (hybrid search in v1.7+)

---

### Embeddings: OpenRouter + Ollama Fallback

**Feature-Flagged Strategy**:

```csharp
// Services/AdaptiveEmbeddingService.cs
public class AdaptiveEmbeddingService : IEmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var provider = await _config.GetValueAsync<string>("AI:EmbeddingProvider", "OpenRouter");

        if (provider == "OpenRouter" && await IsOpenRouterAvailable())
        {
            // OpenRouter: text-embedding-3-large or multilingual-e5-large
            var model = await _config.GetValueAsync<string>("AI:OpenRouter:EmbeddingModel", "openai/text-embedding-3-large");
            return await _openRouter.GenerateEmbeddingAsync(text, model);
        }
        else
        {
            // Ollama fallback: nomic-embed-text (free, 768 dims)
            _logger.LogWarning("OpenRouter unavailable, using Ollama embeddings");
            return await _ollama.GenerateEmbeddingAsync(text, "nomic-embed-text");
        }
    }
}
```

**Configuration**:
```json
{
  "AI": {
    "EmbeddingProvider": "OpenRouter",
    "OpenRouter": {
      "EmbeddingModel": "openai/text-embedding-3-large",
      "Dimensions": 3072
    },
    "Ollama": {
      "EmbeddingModel": "nomic-embed-text",
      "Dimensions": 768
    }
  }
}
```

---

## Updated Architecture Overview

### Consolidated System Architecture

```
┌────────────────────────────────────────────────────────────────┐
│              MEEPLEAI (Enhanced with Board Game AI)            │
└────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                       │
│  Next.js 16 + React 19 (existing) + Italian i18n (NEW)         │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      API LAYER (ASP.NET Core 9)                 │
│  Minimal APIs + CORS + Auth + Rate Limiting (existing)         │
│  + Multi-Model Validation Endpoints (NEW)                      │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
          ▼                   ▼                   ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────┐
│ PDF Processing   │  │  RAG Pipeline    │  │   Admin      │
│ (3-Stage)        │  │  (Enhanced)      │  │  Dashboard   │
│                  │  │                  │  │              │
│ 1.LLMWhisperer   │  │ Multi-Model      │  │ Quality      │
│ 2.SmolDocling    │  │ Validation       │  │ Metrics      │
│ 3.Docnet.Core    │  │ (GPT-4+Claude)   │  │ (5-metric)   │
└────────┬─────────┘  └────────┬─────────┘  └──────────────┘
         │                     │
         └──────────┬──────────┘
                    │
        ┌───────────┼────────────┐
        │           │            │
        ▼           ▼            ▼
┌──────────┐  ┌──────────┐  ┌──────────┐
│ Qdrant   │  │OpenRouter│  │PostgreSQL│
│ (Vector) │  │ OR       │  │ (Metadata│
│          │  │ Ollama   │  │ + FTS)   │
└──────────┘  └──────────┘  └──────────┘
```

---

## Implementation Roadmap (Revised)

### Phase 1: Core Enhancements (Months 1-3, Sprint-based)

**Sprint 1-2: PDF Processing Pipeline** (2 weeks)
- [ ] Implement LlmWhispererPdfExtractor (C# HttpClient integration)
- [ ] Deploy SmolDocling Python microservice (Docker container with GPU support)
- [ ] Integrate with existing DocumentProcessing bounded context
- [ ] Update PdfProcessingOrchestrator to use 3-stage fallback
- [ ] Tests: 20 unit + 10 integration (existing pattern)

**Sprint 3-4: Multi-Model LLM Validation** (2 weeks)
- [ ] Implement AdaptiveLlmService (OpenRouter + Ollama fallback)
- [ ] Add MultiModelValidationService (consensus at 0.90 similarity threshold)
- [ ] Feature flag: `Features:MultiModelValidation` (ConfigurationService)
- [ ] Add to KnowledgeBase bounded context (RAG enhancement)
- [ ] Tests: 15 unit + 8 integration

**Sprint 5-6: Italian Localization** (2 weeks)
- [ ] Add i18n support (Microsoft.Extensions.Localization)
- [ ] Italian terminology glossary (500+ game-specific terms)
- [ ] Query expansion service (English game terms → Italian equivalents)
- [ ] Frontend: Italian UI strings (it.json resource files)
- [ ] Tests: 10 unit (terminology handling)

**Success Criteria**:
- ✅ PDF processing: 3-stage pipeline working, quality score tracked
- ✅ Multi-model validation: GPT-4 + Claude consensus at 0.90 similarity
- ✅ Italian: UI fully localized, terminology preserved
- ✅ Tests: 90%+ coverage maintained
- ✅ Performance: P95 latency ≤5s (acceptable for Phase 1)

---

### Phase 2: Quality Framework (Months 4-6)

**Sprint 7-8: 5-Metric Testing Framework** (2 weeks)
- [ ] Extend existing PromptEvaluationService (ADMIN-01 Phase 4 foundation)
- [ ] Add metrics: Accuracy, Hallucination, Confidence, Citation, Latency
- [ ] Golden dataset: 100 Italian Q&A pairs (10 games)
- [ ] Automated weekly evaluation (background service)
- [ ] Admin UI: `/admin/board-game-quality` dashboard

**Sprint 9-10: Hallucination Prevention** (2 weeks)
- [ ] Forbidden keywords detector (500+ blocklist from research)
- [ ] Citation verification service (page number + snippet matching)
- [ ] Confidence threshold enforcer (≥0.70 or explicit uncertainty)
- [ ] User feedback integration (thumbs up/down → quality improvement)

**Sprint 11-12: Performance Optimization** (2 weeks)
- [ ] Semantic cache (Redis FAISS-based, reuse HybridCache infrastructure)
- [ ] Smart model routing (simple queries → GPT-3.5, complex → GPT-4)
- [ ] Async processing (offload validation to background jobs if non-critical)
- [ ] Target: P95 latency ≤3s

**Success Criteria**:
- ✅ Accuracy ≥90% on 100 Q&A golden dataset
- ✅ Hallucination rate ≤5%
- ✅ P95 latency ≤3s
- ✅ Cache hit rate 40-60% (semantic cache)

---

### Phase 3: Publisher Features (Months 7-9)

**Sprint 13-14: White-Label Integration** (2 weeks)
- [ ] Embeddable widget (iframe, customizable CSS)
- [ ] Publisher-specific branding (logo, colors via config)
- [ ] Analytics dashboard per publisher (query volume, popular games)
- [ ] Revenue share tracking

**Sprint 15-16: B2B API** (2 weeks)
- [ ] Publisher API keys (separate from user API keys)
- [ ] Rate limiting per publisher (ConfigurationService)
- [ ] Webhook notifications (new query, low confidence alert)

---

## Technology Alignment Summary

| Component | Board Game AI Proposal | MeepleAI Consolidation | Change Required |
|-----------|----------------------|------------------------|-----------------|
| **Backend** | Python FastAPI | ASP.NET Core 9.0 ✅ | Update docs: Python → C# |
| **PDF Processing** | LLMWhisperer + SmolDocling | ✅ Both integrated (3-stage) | Implement LLMWhisperer + SmolDocling |
| **LLM Provider** | Direct GPT-4 + Claude APIs | OpenRouter ✅ (supports both) | Update docs: Add OpenRouter as unified API |
| **LLM Fallback** | None | Ollama (mistral + llama3) ✅ | Add Ollama integration |
| **Vector DB** | ChromaDB → Weaviate | Qdrant ✅ (existing) | Update docs: Weaviate → Qdrant |
| **Embeddings** | multilingual-e5-large (local) | OpenRouter API ✅ + Ollama fallback | Feature-flag both options |
| **Frontend** | Next.js 14 + React 18 | Next.js 16 + React 19 ✅ | Update docs: 14→16, 18→19 |
| **Database** | PostgreSQL 16 | PostgreSQL 16 ✅ | No change |
| **Cache** | Redis semantic | Redis HybridCache ✅ + semantic layer | Add semantic cache on top |
| **Deployment** | Docker Compose → Kubernetes | Docker Compose ✅ (K8s ready) | No change |

---

## Cost Analysis (Revised with Consolidation)

### Phase 1 (MVP) Monthly Costs:

| Category | Original Proposal (Python) | Consolidated (ASP.NET) | Savings |
|----------|---------------------------|------------------------|---------|
| **Team** | €8,000 (2-3 Python devs) | €5,000 (extend existing C# team) | **-€3,000** |
| **Infrastructure** | €500 (separate stack) | €200 (reuse existing + GPU €50) | **-€300** |
| **LLM API** | €1,500 (GPT-4 + Claude direct) | €1,500 (OpenRouter) OR €0 (Ollama fallback) | **€0 to -€1,500** |
| **Tools** | €300 | €100 (reuse existing) | **-€200** |
| **Total** | €10,300/month | €6,800/month (OpenRouter) OR €5,300/month (Ollama) | **-€3,500 to -€5,000** |

**Savings**: 34-48% cost reduction by consolidating into existing system

**Additional Benefits**:
- Single deployment pipeline (vs managing two)
- Single monitoring stack (existing Prometheus + Grafana)
- Single team expertise (C#/.NET)
- Faster development (reuse existing services, DDD contexts)

---

## Migration from Proposal to Implementation

### Documentation Updates Required:

1. **Strategic Roadmap** - Update:
   - Backend: Python FastAPI → ASP.NET Core 9.0
   - Team: Python devs → C#/.NET devs (existing team extended)
   - Timeline: Faster (reuse vs rebuild) - 4 months vs 6 months Phase 1

2. **Architecture Overview** - Update:
   - Remove Python/FastAPI sections
   - Add: Integration with existing DDD bounded contexts
   - PDF Pipeline: LLMWhisperer + SmolDocling + Docnet.Core (3-stage)
   - LLM: OpenRouter (feature-flagged) + Ollama fallback

3. **API Specification** - Update:
   - Endpoints already exist in existing system (extend, not create)
   - Add: Multi-model validation response fields
   - Add: Italian language support fields

4. **ADR-001** - Update:
   - Implementation: C# instead of Python code examples
   - OpenRouter API pattern instead of direct provider calls

5. **ADR-003 (PDF)** - Update:
   - Keep LLMWhisperer + SmolDocling as proposed
   - ADD Stage 3: Existing Docnet.Core fallback
   - Integration: C# orchestration of Python microservice (SmolDocling)

---

## Next Actions (Immediate)

### Week 1: Update Documentation

- [ ] Create this consolidation strategy document ✅ (DONE)
- [ ] Update board-game-ai-strategic-roadmap.md (Python → ASP.NET Core)
- [ ] Update board-game-ai-architecture-overview.md (consolidated stack)
- [ ] Update ADRs with C# code examples
- [ ] Add banner to all Board Game AI docs: "Integrated into existing MeepleAI (ASP.NET Core)"

### Week 2: Technical Prototyping

- [ ] LLMWhisperer C# client (HttpClient integration)
- [ ] SmolDocling Python microservice (FastAPI + Docker)
- [ ] Test 3-stage pipeline on 5 Italian rulebooks (Terraforming Mars, Wingspan, Azul, Scythe, Catan)
- [ ] Measure quality scores per stage (validate 95%+ achievable)

### Week 3-4: Implementation Planning

- [ ] Break down into GitHub Issues (LLMWhisperer integration, SmolDocling service, multi-model validation)
- [ ] Assign to existing team (leverage C# expertise)
- [ ] Create Sprint 1 plan (2-week iteration)
- [ ] Setup project board (GitHub Projects)

---

**Document Metadata**:
- **Version**: 2.0 (Revised - Consolidation Strategy)
- **Last Updated**: 2025-01-15
- **Decision**: APPROVED - Enhance Existing MeepleAI System
- **Approvers**: CTO, Engineering Lead, Product Lead
- **Status**: READY FOR IMPLEMENTATION
