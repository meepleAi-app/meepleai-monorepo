# Board Game AI Implementation Notes - Technology Consolidation

**Status**: Implementation Guide
**Version**: 1.0
**Date**: 2025-01-15
**Critical**: READ BEFORE IMPLEMENTING BOARD GAME AI FEATURES

---

## 🚨 IMPORTANT: Technology Stack Corrections

The Board Game AI documentation suite (11 documents created 2025-01-15) originally proposed a **separate Python/FastAPI system**.

**DECISION (2025-01-15)**: **Integrate Board Game AI features into existing MeepleAI (ASP.NET Core 9.0)** instead.

This document provides **technology corrections** to apply when reading the Board Game AI documentation.

---

## Technology Translation Table

When reading Board Game AI docs, apply these translations:

| Original Proposal | ✅ ACTUAL IMPLEMENTATION (Consolidated) | Rationale |
|-------------------|----------------------------------------|-----------|
| **Backend: Python 3.11 FastAPI** | **ASP.NET Core 9.0 (C#)** | Reuse existing system (90%+ coverage, DDD, production-ready) |
| **Vector DB: ChromaDB → Weaviate** | **Qdrant (existing)** | Already deployed, hybrid search support v1.7+ |
| **LLM: Direct OpenAI + Anthropic APIs** | **OpenRouter API (unified)** | Single API for multiple models, automatic fallback |
| **LLM Fallback: None** | **Ollama (mistral + llama3)** | Free self-hosted option, cost control |
| **PDF: LLMWhisperer + SmolDocling** | **LLMWhisperer + SmolDocling + Docnet.Core** | Keep existing Docnet as final fallback |
| **Embeddings: multilingual-e5-large (local)** | **OpenRouter API + Ollama fallback** | Feature-flagged: API (accuracy) vs local (free) |
| **Frontend: Next.js 14 + React 18** | **Next.js 16 + React 19** | Use latest versions (existing system already updated) |
| **Deployment: New Docker Compose** | **Existing Docker Compose (extend)** | Reuse infra, add pdf-processor service |

---

## PDF Processing Pipeline (3-Stage Hybrid)

### Implementation Strategy

**Stage 1: LLMWhisperer** (Cloud API - C# Integration)
```csharp
// Infrastructure/Adapters/LlmWhispererPdfExtractor.cs
public class LlmWhispererPdfExtractor : IPdfTextExtractor
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public async Task<PdfExtractionResult> ExtractTextAsync(Stream pdfStream, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(pdfStream), "file", "document.pdf");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.llmwhisperer.com/v1/extract");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = content;

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LlmWhispererResponse>(ct);

        return new PdfExtractionResult
        {
            Text = result.Text,
            QualityScore = result.QualityScore,
            PageCount = result.PageCount,
            Method = PdfProcessingMethod.LlmWhisperer
        };
    }
}
```

**Stage 2: SmolDocling** (Python Microservice)

**NEW Python Service**: `apps/pdf-processor/`
```python
# apps/pdf-processor/main.py
from fastapi import FastAPI, UploadFile, File
from docling import DocumentConverter
import uvicorn

app = FastAPI()
converter = DocumentConverter()

@app.post("/api/v1/convert")
async def convert_pdf(file: UploadFile = File(...)):
    result = converter.convert(file.file)
    return {
        "text": result.document.export_to_markdown(),
        "page_count": len(result.document.pages),
        "method": "smoldocling"
    }

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8001)
```

**C# Client**:
```csharp
// Infrastructure/Adapters/SmolDoclingPdfExtractor.cs
public class SmolDoclingPdfExtractor : IPdfTextExtractor
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceUrl;  // http://pdf-processor:8001

    public async Task<PdfExtractionResult> ExtractTextAsync(Stream pdfStream, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(pdfStream), "file", "document.pdf");

        using var response = await _httpClient.PostAsync(
            $"{_serviceUrl}/api/v1/convert",
            content,
            ct
        );

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SmolDoclingResponse>(ct);

        return new PdfExtractionResult
        {
            Text = result.Text,
            QualityScore = 0.75m,  // Estimated
            PageCount = result.PageCount,
            Method = PdfProcessingMethod.SmolDocling
        };
    }
}
```

**Stage 3: Docnet.Core** (Existing - Keep as Final Fallback)
```csharp
// Already implemented in:
// BoundedContexts/DocumentProcessing/Infrastructure/Adapters/DocnetPdfTextExtractor.cs
// - Proven working (DDD Phase 4, 85/86 tests passing)
// - Fast (~5-10s per rulebook)
// - Zero cost (self-hosted)
```

**Docker Compose Update**:
```yaml
# infra/docker-compose.yml - ADD new service
services:
  pdf-processor:
    build: ../apps/pdf-processor
    ports:
      - "8001:8001"
    environment:
      - LOG_LEVEL=INFO
      - CUDA_VISIBLE_DEVICES=0  # GPU support (optional, fallback to CPU)
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]  # If GPU available
```

---

## OpenRouter Integration (Feature-Flagged)

### Configuration (appsettings.json)

```json
// NOTE: This configuration structure is DEPRECATED (v1.0)
// See docs/03-api/ai-provider-configuration.md for current BGAI-021/022 structure
{
  "AI": {
    "Provider": "OpenRouter",  // "OpenRouter" or "Ollama"
    "OpenRouter": {
      // API key configured via environment variable: OPENROUTER_API_KEY or OPENROUTER_API_KEY_FILE
      "Enabled": true,
      "BaseUrl": "https://openrouter.ai/api/v1",
      "Models": {
        "Primary": "openai/gpt-4-turbo",
        "Validation": "anthropic/claude-3.5-sonnet",
        "Fallback": "google/gemini-pro-1.5",
        "Embedding": "openai/text-embedding-3-large"
      },
      "MultiModelValidation": true,
      "ConsensusSimilarityThreshold": 0.90,
      "TimeoutSeconds": 30
    },
    "Ollama": {
      "Enabled": true,
      "BaseUrl": "http://ollama:11434",
      "Models": {
        "Primary": "mistral:7b-instruct-v0.3-q4_K_M",
        "Validation": "llama3.1:8b-instruct-q4_K_M",
        "Embedding": "nomic-embed-text"
      },
      "MultiModelValidation": true,
      "ConsensusSimilarityThreshold": 0.85
    }
  }
}
```

### Adaptive LLM Service Implementation

```csharp
// BoundedContexts/KnowledgeBase/Application/Services/AdaptiveLlmService.cs
public class AdaptiveLlmService : ILlmService
{
    private readonly IOpenRouterClient _openRouter;
    private readonly IOllamaClient _ollama;
    private readonly IConfigurationService _config;
    private readonly ILogger<AdaptiveLlmService> _logger;

    public async Task<LlmResponse> GenerateAsync(
        string prompt,
        LlmOptions options,
        CancellationToken ct = default)
    {
        var provider = await _config.GetValueAsync<string>("AI:Provider", "OpenRouter");

        // Try OpenRouter first (if enabled and available)
        if (provider == "OpenRouter")
        {
            var openRouterEnabled = await _config.GetValueAsync<bool>("AI:OpenRouter:Enabled", true);

            if (openRouterEnabled)
            {
                try
                {
                    var model = options.Model ?? await _config.GetValueAsync<string>("AI:OpenRouter:Models:Primary");

                    return await _openRouter.GenerateAsync(new OpenRouterRequest
                    {
                        Model = model,
                        Messages = new[] { new Message { Role = "user", Content = prompt } },
                        Temperature = options.Temperature ?? 0.1m,
                        MaxTokens = options.MaxTokens ?? 2048
                    }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OpenRouter failed, falling back to Ollama");
                }
            }
        }

        // Fallback to Ollama (free, self-hosted)
        _logger.LogInformation("Using Ollama for LLM generation (provider: {Provider})", provider);

        var ollamaModel = options.Model ?? await _config.GetValueAsync<string>("AI:Ollama:Models:Primary");

        return await _ollama.GenerateAsync(new OllamaRequest
        {
            Model = ollamaModel,
            Prompt = prompt,
            Temperature = options.Temperature ?? 0.1m,
            Options = new OllamaOptions { NumPredict = options.MaxTokens ?? 2048 }
        }, ct);
    }
}
```

---

## Qdrant Enhancements (Existing Vector DB)

### Hybrid Search Configuration

Qdrant v1.7+ supports hybrid search (sparse + dense vectors). Existing MeepleAI already uses Qdrant, extend for Board Game AI features:

```csharp
// BoundedContexts/KnowledgeBase/Infrastructure/QdrantVectorStore.cs
public class QdrantVectorStore : IVectorStore
{
    public async Task<List<SearchResult>> HybridSearchAsync(
        string query,
        string gameId,
        int topK = 10,
        CancellationToken ct = default)
    {
        // Generate dense vector (semantic search)
        var denseVector = await _embeddings.GenerateEmbeddingAsync(query, ct);

        // Generate sparse vector (keyword search - BM25)
        var sparseVector = GenerateSparseVector(query);  // TF-IDF or BM25

        // Qdrant hybrid search (RRF fusion built-in)
        var searchRequest = new SearchRequest
        {
            CollectionName = "rulebook_chunks",
            Vector = denseVector,
            SparseVector = sparseVector,
            Limit = topK,
            Filter = new Filter
            {
                Must = new List<Condition>
                {
                    new FieldCondition { Key = "game_id", Match = new MatchValue { Value = gameId } }
                }
            },
            ScoreThreshold = 0.70f,  // Minimum similarity
            WithPayload = true,
            WithVector = false
        };

        var results = await _qdrantClient.SearchAsync(searchRequest, ct);

        return results.Select(r => new SearchResult
        {
            ChunkId = r.Id.ToString(),
            Text = r.Payload["text"].StringValue,
            Page = (int)r.Payload["page"].IntegerValue,
            Similarity = (decimal)r.Score,
            GameId = gameId
        }).ToList();
    }
}
```

---

## Italian Localization Strategy

### i18n Implementation (ASP.NET Core)

```csharp
// Program.cs - ADD
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "it-IT", "en-US", "fr-FR", "de-DE", "es-ES" };
    options.SetDefaultCulture("it-IT");  // Italian default
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
});

var app = builder.Build();

app.UseRequestLocalization();  // Enable i18n middleware
```

**Resource Files**:
```
Resources/
├── BoardGameTerms.it.resx   # Italian game terminology
├── BoardGameTerms.en.resx   # English fallback
├── BoardGameTerms.fr.resx   # French (Phase 4)
└── BoardGameTerms.de.resx   # German (Phase 4)
```

**Terminology Glossary** (BoardGameTerms.it.resx):
```xml
<data name="Meeple" xml:space="preserve">
  <value>Meeple</value>
  <comment>Universal term, no translation</comment>
</data>
<data name="WorkerPlacement" xml:space="preserve">
  <value>Worker Placement|Piazzamento Lavoratori</value>
  <comment>Preserve English + Italian alternative</comment>
</data>
<data name="DeckBuilding" xml:space="preserve">
  <value>Deck Building|Costruzione Mazzo</value>
</data>
```

---

## Cost Analysis (Revised with Consolidation)

### OpenRouter vs Ollama Cost Comparison

**Scenario: 1,000 MAU, avg 50 queries/user/month = 50,000 queries/month**

| Provider | Cost/Query | Monthly Cost (50K queries) | Accuracy | Latency |
|----------|-----------|---------------------------|----------|---------|
| **OpenRouter (GPT-4 + Claude)** | $0.04 | $2,000 | 95%+ | 2.5s P95 |
| **OpenRouter (GPT-4 only)** | $0.02 | $1,000 | 85-90% | 1.5s P95 |
| **Ollama (mistral + llama3)** | $0.00 | $0 (GPU €50/mo) | 75-80% | 3.0s P95 |

**Hybrid Strategy (Recommended)**:
- **Free tier users** (10 queries/day): Ollama (free, acceptable 75-80% accuracy)
- **Premium users** (unlimited): OpenRouter (95%+ accuracy, worth the cost)
- **Critical queries** (competitive play): OpenRouter + multi-model validation
- **Simple queries** (setup, basic): OpenRouter GPT-3.5 (cheaper, still good)

**Expected Cost**:
- 70% queries from free users → Ollama (€50/mo GPU)
- 30% queries from premium → OpenRouter ($600/mo = €550)
- **Total**: €600/month vs €1,850 if all OpenRouter (67% savings)

---

## Integration with Existing Bounded Contexts

### DocumentProcessing Context (Extend Existing)

**Current State** (DDD Phase 4 Complete):
- ✅ PdfDocument aggregate
- ✅ PdfValidationDomainService
- ✅ DocnetPdfTextExtractor, ITextPdfTableExtractor
- ✅ 85/86 tests passing

**ADD Board Game AI Features**:
```
BoundedContexts/DocumentProcessing/
├── Domain/
│   └── Services/
│       └── PdfQualityAssessmentService.cs  # NEW: Quality scoring 0.0-1.0
├── Infrastructure/
│   └── Adapters/
│       ├── LlmWhispererPdfExtractor.cs  # NEW: Stage 1
│       └── SmolDoclingPdfExtractor.cs   # NEW: Stage 2 (Python client)
└── Application/
    └── Services/
        └── EnhancedPdfProcessingOrchestrator.cs  # NEW: 3-stage fallback logic
```

---

### KnowledgeBase Context (Extend Existing)

**Current State**:
- ✅ VectorDocument aggregate
- ✅ RagService, EmbeddingService, QdrantService
- ✅ ChatThread for conversation history

**ADD Board Game AI Features**:
```
BoundedContexts/KnowledgeBase/
├── Application/
│   └── Services/
│       ├── MultiModelValidationService.cs  # NEW: GPT-4 + Claude consensus
│       ├── ConfidenceValidationService.cs  # NEW: Threshold 0.70
│       ├── CitationValidationService.cs    # NEW: Page + snippet verification
│       ├── HallucinationDetectionService.cs # NEW: Forbidden keywords
│       └── AdaptiveLlmService.cs  # NEW: OpenRouter + Ollama routing
└── Infrastructure/
    └── Clients/
        ├── OpenRouterClient.cs  # NEW: Unified LLM API client
        └── OllamaClient.cs      # NEW: Free self-hosted LLM
```

---

## Implementation Phases (Revised Timeline)

### Phase 1: Core Features (Sprints 1-6, 12 weeks)

**Sprint 1-2: PDF Processing Enhancement**
- [ ] LlmWhispererPdfExtractor (C# HttpClient)
- [ ] SmolDocling Python microservice (Docker container)
- [ ] EnhancedPdfProcessingOrchestrator (3-stage fallback)
- [ ] Docker Compose: Add pdf-processor service
- [ ] Tests: 15 unit + 8 integration

**Sprint 3-4: OpenRouter Integration**
- [ ] OpenRouterClient implementation (unified API)
- [ ] OllamaClient implementation (fallback)
- [ ] AdaptiveLlmService (feature-flagged routing)
- [ ] Configuration: Dynamic AI provider selection
- [ ] Tests: 12 unit + 6 integration

**Sprint 5-6: Multi-Model Validation**
- [ ] MultiModelValidationService (consensus logic)
- [ ] ConfidenceValidationService (threshold enforcement)
- [ ] CitationValidationService (page verification)
- [ ] HallucinationDetectionService (forbidden keywords)
- [ ] Integration: Extend RagService with validation pipeline
- [ ] Tests: 20 unit + 10 integration

**Deliverables**:
- ✅ 3-stage PDF pipeline working (LLMWhisperer → SmolDocling → Docnet)
- ✅ Multi-model validation (OpenRouter: GPT-4 + Claude)
- ✅ Ollama fallback configured (free operation mode)
- ✅ 90%+ test coverage maintained
- ✅ Existing features unaffected (backward compatible)

---

## Development Guidelines

### When Implementing Board Game AI Features:

1. **Code Location**: Add to existing bounded contexts (DocumentProcessing, KnowledgeBase)
   - NOT new separate backend/API
   - Follow existing DDD patterns

2. **Language**: C# (.NET 9.0)
   - NOT Python (except SmolDocling microservice)
   - Use existing patterns (async/await, IDisposable, nullable refs)

3. **Testing**: xUnit + Testcontainers
   - NOT pytest (except Python microservice)
   - 90%+ coverage requirement

4. **API Endpoints**: Extend existing `/api/v1/*` routes
   - NOT new FastAPI application
   - Use Minimal APIs pattern

5. **Database**: Extend existing EF Core entities
   - NOT new ORM/database
   - Use migrations: `dotnet ef migrations add <Name>`

6. **Configuration**: Use existing ConfigurationService
   - Dynamic runtime config (database-driven)
   - Feature flags for Board Game AI features

---

## Environment Variables (Updated)

```bash
# .env.dev - ADD to existing
LLMWHISPERER_API_KEY=<get from https://llmwhisperer.com>
OPENROUTER_API_KEY=<get from https://openrouter.ai>

# Ollama (if running locally for free fallback)
OLLAMA_BASE_URL=http://ollama:11434

# Feature Flags (ConfigurationService)
FEATURES_MULTI_MODEL_VALIDATION=true
FEATURES_ITALIAN_LOCALIZATION=true
FEATURES_ENHANCED_PDF_PROCESSING=true
```

---

## Migration from Proposal to Implementation

### Documents Updated (2025-01-15):

✅ **board-game-ai-strategic-roadmap.md**:
- Added system integration notice (ASP.NET Core, not Python)
- Updated Sprint milestones (reuse existing infra)
- Updated tech stack (OpenRouter, Qdrant, existing services)

✅ **board-game-ai-architecture-overview.md**:
- Updated technology stack table (ASP.NET Core, Qdrant, OpenRouter)
- Updated Phase 2 (enhancements vs new infrastructure)

✅ **adr-001-hybrid-rag-architecture.md**:
- Updated code examples (Python → C#)
- Added OpenRouter + Ollama fallback strategy
- Updated cost mitigation (feature flags)

✅ **board-game-ai-consolidation-strategy.md**:
- NEW: Comprehensive consolidation decision document
- Integration approach, cost savings (34-48%), technology alignment

### Documents Pending Update:

⚠️ **adr-002-multilingual-embedding-strategy.md**:
- Update: multilingual-e5-large (local) → OpenRouter API (feature-flagged) + Ollama fallback
- Add: C# code examples

⚠️ **adr-003-pdf-processing-pipeline.md**:
- Already correct (LLMWhisperer + SmolDocling)
- Add: Stage 3 Docnet.Core fallback
- Update: C# integration code examples

⚠️ **board-game-ai-api-specification.md**:
- Update: Endpoints extend existing `/api/v1/*` (not new API)
- Add: Integration with existing auth (API keys, sessions, OAuth)

⚠️ **board-game-ai-testing-strategy.md**:
- Update: xUnit + Testcontainers (not pytest)
- Add: Integration with existing test infrastructure

⚠️ **board-game-ai-deployment-guide.md**:
- Update: Extend existing Docker Compose (not new stack)
- Add: pdf-processor service deployment

⚠️ **board-game-ai-business-plan.md**:
- Update: Cost structure (reuse existing infra = lower costs)
- Update: Team (C#/.NET devs, not Python devs)

---

## Quick Reference: Code Patterns

### Making OpenRouter API Calls (C#)

```csharp
using var client = _httpClientFactory.CreateClient("OpenRouter");

var request = new OpenRouterRequest
{
    Model = "openai/gpt-4-turbo",
    Messages = new[]
    {
        new Message { Role = "system", Content = systemPrompt },
        new Message { Role = "user", Content = userQuery }
    },
    Temperature = 0.1m,
    MaxTokens = 2048
};

using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
{
    Content = JsonContent.Create(request)
};
httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
httpRequest.Headers.Add("HTTP-Referer", "https://meepleai.dev");  // Required by OpenRouter

using var response = await client.SendAsync(httpRequest, ct);
var result = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(ct);

return result.Choices[0].Message.Content;
```

### Making Ollama API Calls (C#)

```csharp
using var client = _httpClientFactory.CreateClient("Ollama");

var request = new OllamaGenerateRequest
{
    Model = "mistral:7b-instruct-v0.3-q4_K_M",
    Prompt = userQuery,
    Temperature = 0.1m,
    Options = new { num_predict = 2048 }
};

using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
{
    Content = JsonContent.Create(request)
};

using var response = await client.SendAsync(httpRequest, ct);
var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(ct);

return result.Response;
```

---

## Testing Strategy

### Unit Tests (C# xUnit)

```csharp
// tests/Api.Tests/BoundedContexts/KnowledgeBase/MultiModelValidationServiceTests.cs
public class MultiModelValidationServiceTests
{
    [Fact]
    public async Task ValidateWithConsensus_WhenHighSimilarity_ReturnsConsensusReached()
    {
        // Arrange
        var mockOpenRouter = new Mock<IOpenRouterClient>();
        mockOpenRouter
            .Setup(x => x.GenerateAsync(It.IsAny<OpenRouterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = "No, Standard Projects can only be used during your turn." });

        var mockEmbeddings = new Mock<IEmbeddingService>();
        mockEmbeddings
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.5f, 0.5f, 0.5f });  // Same embedding (high similarity)

        var service = new MultiModelValidationService(
            mockOpenRouter.Object,
            mockEmbeddings.Object,
            Mock.Of<ILogger<MultiModelValidationService>>()
        );

        var primaryResponse = new GeneratedResponse
        {
            Answer = "No, Standard Projects can only be used during your turn.",
            Confidence = 0.85m
        };

        // Act
        var result = await service.ValidateWithConsensusAsync(
            "Can I use Standard Projects after passing?",
            primaryResponse,
            new List<RetrievedChunk>()
        );

        // Assert
        result.Passed.Should().BeTrue();
        result.Layer.Should().Be("multi_model_consensus");
        result.Metadata.Should().ContainKey("similarity");
    }
}
```

---

## Summary: Key Implementation Decisions

| Decision Area | Board Game AI Proposal | ✅ Actual Implementation | Why |
|---------------|----------------------|-------------------------|-----|
| **Backend** | Python FastAPI (new) | ASP.NET Core 9.0 (extend existing) | Reuse 90%+ coverage, DDD architecture |
| **Vector DB** | ChromaDB → Weaviate (new) | Qdrant (existing) | Already deployed, hybrid search support |
| **PDF Processing** | LLMWhisperer + SmolDocling | LLMWhisperer + SmolDocling + **Docnet.Core fallback** | Keep proven fallback |
| **LLM** | Direct APIs | **OpenRouter** (unified) + **Ollama** (free) | Cost control, automatic fallback |
| **Embeddings** | multilingual-e5-large (local) | **OpenRouter API** + **Ollama** fallback | Feature-flagged: accuracy vs cost |
| **Frontend** | Next.js 14 + React 18 | Next.js 16 + React 19 (existing) | Latest versions already in use |
| **Deployment** | New Docker Compose | Extend existing (add pdf-processor) | Reuse monitoring, observability |
| **Timeline** | 6 months MVP | **3-4 months** (faster with reuse) | Leverage existing services |
| **Cost** | €10K/month | **€6-7K/month** (34-48% savings) | Consolidation efficiency |

---

## Next Steps (Immediate)

### This Week:
1. ✅ Read this implementation notes document
2. ✅ Read [Consolidation Strategy](./architecture/board-game-ai-consolidation-strategy.md)
3. [ ] Review updated roadmap + architecture docs
4. [ ] Create GitHub Issues for Sprint 1 (LLMWhisperer + SmolDocling integration)
5. [ ] Setup LLMWhisperer trial account (validate free tier)
6. [ ] Prototype SmolDocling Python service (test on 3 Italian rulebooks)

### Next Sprint (Weeks 2-3):
1. [ ] Implement LlmWhispererPdfExtractor (C# adapter)
2. [ ] Deploy SmolDocling microservice (Docker + GPU config)
3. [ ] Integrate with EnhancedPdfProcessingOrchestrator
4. [ ] Write tests (90%+ coverage)
5. [ ] Measure quality improvement (Docnet baseline vs LLMWhisperer)

---

**Document Metadata**:
- **Version**: 1.0
- **Purpose**: Implementation Guide (Critical Corrections to Board Game AI Docs)
- **Audience**: Development Team
- **Status**: MUST READ before implementing Board Game AI features
- **Related**: [Consolidation Strategy](./architecture/board-game-ai-consolidation-strategy.md)
