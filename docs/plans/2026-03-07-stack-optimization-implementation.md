# Stack Optimization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Reduce MeepleAI from 18+ containers (~42 GB RAM) to 6 containers (~8.5 GB RAM) for EUR 9.30/month hosting.

**Architecture:** Replace 5 Python AI services with Ollama (mxbai-embed-large + Qwen2.5-1.5B), replace Qdrant with pgvector HNSW, add PdfPig for .NET-native PDF extraction, replace Traefik with Caddy, remove monitoring stack.

**Tech Stack:** .NET 9, PostgreSQL 16 + pgvector, Redis 7.4, Ollama, Caddy, Next.js standalone, UglyToad.PdfPig

**Design Doc:** `docs/plans/2026-03-07-stack-optimization-design.md`

---

## Phase 1: Eliminate Non-Essential Services (docker-compose)

### Task 1.1: Create MVP docker-compose

**Files:**
- Create: `infra/compose.mvp.yml`
- Reference: `infra/docker-compose.yml` (full enterprise version)

**Step 1: Read the current docker-compose to understand service definitions**

Read `infra/docker-compose.yml` — note service names, volumes, networks, health checks for the 6 services to KEEP: postgres, redis, ollama, api, web, and the network definition.

**Step 2: Create compose.mvp.yml with only 6 services**

Create `infra/compose.mvp.yml` containing:
- `postgres` (from lines 7-40 of original, keep pgvector image, reduce RAM limit to 1.5GB)
- `redis` (from lines 70-94, reduce maxmemory to 512mb, RAM limit 512MB)
- `ollama` (from lines 181-218, add mxbai-embed-large pull, set OLLAMA_KEEP_ALIVE=24h, RAM limit 2.5GB)
- `api` (from lines 696-760, remove depends_on for qdrant/embedding/reranker/unstructured/smoldocling, RAM limit 1.5GB)
- `web` (from lines 762-800, RAM limit 512MB)
- `caddy` (NEW service replacing traefik)

Key changes in the api service environment:
```yaml
environment:
  # Embedding: switch from external Python to Ollama mxbai
  Embedding__Provider: OllamaMxbai
  EMBEDDING_PROVIDER: OllamaMxbai
  Embedding__OllamaUrl: http://ollama:11434
  # Reranking: disabled
  ResilientRetrieval__EnableReranking: "false"
  # Qdrant: removed (pgvector used instead)
  # Remove: QDRANT_URL, Qdrant__* variables
```

**Step 3: Create Caddyfile**

Create `infra/Caddyfile`:
```
{
    email admin@meepleai.com
}

meepleai.com {
    reverse_proxy api:8080
    handle /api/* {
        reverse_proxy api:8080
    }
    handle /* {
        reverse_proxy web:3000
    }
}
```

**Step 4: Create Ollama model initialization script**

Create `infra/scripts/ollama-init.sh`:
```bash
#!/bin/bash
# Wait for Ollama to be ready
until curl -s http://localhost:11434/api/tags > /dev/null 2>&1; do
  echo "Waiting for Ollama..."
  sleep 2
done
# Pull required models
ollama pull qwen2.5:1.5b
ollama pull mxbai-embed-large
echo "Models ready."
```

**Step 5: Verify compose.mvp.yml is valid**

Run: `cd infra && docker compose -f compose.mvp.yml config --quiet`
Expected: No errors (validates YAML syntax and service references)

**Step 6: Commit**

```bash
git add infra/compose.mvp.yml infra/Caddyfile infra/scripts/ollama-init.sh
git commit -m "infra: add MVP docker-compose with 6 services, Caddyfile, Ollama init"
```

---

## Phase 2: Switch Embedding to Ollama mxbai-embed-large

### Task 2.1: Update appsettings for Ollama embedding

**Files:**
- Modify: `apps/api/src/Api/appsettings.json` (lines 46-60, Embedding section)
- Modify: `apps/api/src/Api/appsettings.Development.json` (add Embedding override)

**Step 1: Read current appsettings Embedding config**

Read `apps/api/src/Api/appsettings.json` — find the `"Embedding"` section (around line 46).

**Step 2: Update Embedding section for OllamaMxbai**

Change the Embedding section in `appsettings.json`:
```json
"Embedding": {
  "Provider": "OllamaMxbai",
  "FallbackProvider": "OllamaNomic",
  "EnableFallback": true,
  "BatchSize": 1,
  "MaxRetries": 3,
  "TimeoutSeconds": 120,
  "Model": "mxbai-embed-large",
  "Dimensions": 1024,
  "OllamaUrl": "http://localhost:11434",
  "HuggingFaceApiKey": null,
  "HuggingFaceEndpoint": null
}
```

Key changes:
- `Provider`: `"External"` -> `"OllamaMxbai"`
- `FallbackProvider`: `"OpenRouterSmall"` -> `"OllamaNomic"`
- `BatchSize`: `10` -> `1` (Ollama is sequential)
- `TimeoutSeconds`: `60` -> `120` (Ollama cold start may be slow)
- `Model`: `null` -> `"mxbai-embed-large"`
- `Dimensions`: `null` -> `1024`

**Step 3: Verify the EmbeddingConfiguration class supports OllamaMxbai**

Read `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/EmbeddingConfiguration.cs` — confirm `EmbeddingProviderType.OllamaMxbai` exists and maps to `mxbai-embed-large` with 1024 dimensions.

**Step 4: Verify OllamaEmbeddingProvider handles OllamaMxbai type**

Read `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/Providers/OllamaEmbeddingProvider.cs` — confirm constructor accepts `EmbeddingProviderType.OllamaMxbai` (line 26-28 already validates this).

**Step 5: Commit**

```bash
git add apps/api/src/Api/appsettings.json
git commit -m "config(embedding): switch from external Python to Ollama mxbai-embed-large 1024d"
```

### Task 2.2: Disable reranker via config

**Files:**
- Modify: `apps/api/src/Api/appsettings.json` (lines 332-346, ResilientRetrieval section)

**Step 1: Read current reranking config**

Read `apps/api/src/Api/appsettings.json` — find `"ResilientRetrieval"` section.

**Step 2: Set EnableReranking to false**

Change in `appsettings.json`:
```json
"ResilientRetrieval": {
  "EnableReranking": false,
  "CandidateMultiplier": 3,
  "FailureThreshold": 3,
  "HealthCheckIntervalSeconds": 30,
  "CacheTtlSeconds": 300
}
```

Only change: `"EnableReranking": true` -> `"EnableReranking": false`

**Step 3: Verify ResilientRetrievalService handles disabled reranking**

Read `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Reranking/ResilientRetrievalService.cs` — confirm it checks `EnableReranking` and falls back to RRF scores when disabled.

**Step 4: Commit**

```bash
git add apps/api/src/Api/appsettings.json
git commit -m "config(reranker): disable reranking, use RRF graceful degradation"
```

---

## Phase 3: Replace Qdrant with pgvector

### Task 3.1: Write PgVectorStoreAdapter tests

**Files:**
- Reference: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/External/IQdrantVectorStoreAdapter.cs`
- Reference: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/External/QdrantVectorStoreAdapter.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/PgVectorStoreAdapterTests.cs`

**Step 1: Read the IQdrantVectorStoreAdapter interface**

Read the interface to understand all methods that PgVectorStoreAdapter must implement:
- `SearchAsync(gameId, queryVector, topK, minScore, documentIds?, cancellationToken)`
- `IndexBatchAsync(embeddings, cancellationToken)`
- `DeleteByVectorDocumentIdAsync(vectorDocumentId, cancellationToken)`
- `CollectionExistsAsync(gameId, cancellationToken)`
- `EnsureCollectionExistsAsync(gameId, vectorDimension, cancellationToken)`

**Step 2: Read the existing QdrantVectorStoreAdapter for behavior reference**

Read the implementation to understand:
- How search filters by gameId
- How batch indexing maps chunks to points
- How deletion works

**Step 3: Write unit tests for PgVectorStoreAdapter**

Create test file with tests for:
- `SearchAsync_ReturnsRankedResults_FilteredByGameId`
- `SearchAsync_RespectsMinScore`
- `SearchAsync_FiltersbyDocumentIds`
- `IndexBatchAsync_InsertsVectors`
- `DeleteByVectorDocumentIdAsync_RemovesVectors`
- `CollectionExistsAsync_ReturnsTrueWhenVectorsExist`

Use Testcontainers with PostgreSQL + pgvector for integration tests.

**Step 4: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "PgVectorStoreAdapter" -v n`
Expected: FAIL (PgVectorStoreAdapter class does not exist yet)

**Step 5: Commit failing tests**

```bash
git add apps/api/tests/
git commit -m "test(pgvector): add PgVectorStoreAdapter integration tests (red)"
```

### Task 3.2: Implement PgVectorStoreAdapter

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`

**Step 1: Read VectorDocumentEntity to understand the existing schema**

Read the entity that maps to the `vector_documents` table — note column names, types, relationships.

**Step 2: Implement PgVectorStoreAdapter**

Create `PgVectorStoreAdapter` implementing `IQdrantVectorStoreAdapter` (reuse interface — rename later if needed):

Key implementation details:
- Use `MeepleAiDbContext` for DB access
- `SearchAsync`: Raw SQL with pgvector cosine distance `<=>` operator
  ```sql
  SELECT id, embedding <=> @query AS distance, ...
  FROM vector_documents
  WHERE game_id = @gameId AND (1 - (embedding <=> @query)) >= @minScore
  ORDER BY embedding <=> @query
  LIMIT @topK
  ```
- `IndexBatchAsync`: EF Core bulk insert of VectorDocumentEntity rows
- `DeleteByVectorDocumentIdAsync`: EF Core delete by ID
- `CollectionExistsAsync`: Check if any vectors exist for gameId
- `EnsureCollectionExistsAsync`: No-op (pgvector doesn't need collection creation)

**Step 3: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "PgVectorStoreAdapter" -v n`
Expected: PASS

**Step 4: Register in DI**

Modify `KnowledgeBaseServiceExtensions.cs`:
- Replace: `services.AddScoped<IQdrantVectorStoreAdapter, QdrantVectorStoreAdapter>();`
- With: `services.AddScoped<IQdrantVectorStoreAdapter, PgVectorStoreAdapter>();`

**Step 5: Commit**

```bash
git add apps/api/src/ apps/api/tests/
git commit -m "feat(pgvector): implement PgVectorStoreAdapter replacing Qdrant"
```

### Task 3.3: Add HNSW index migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/{timestamp}_AddHnswVectorIndex.cs` (via dotnet ef)

**Step 1: Create EF migration for HNSW index**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddHnswVectorIndex`

**Step 2: Edit the migration to add HNSW index**

The auto-generated migration will be empty. Add the HNSW index creation:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Create HNSW index for vector similarity search
    migrationBuilder.Sql(@"
        CREATE INDEX IF NOT EXISTS ix_vector_documents_embedding_hnsw
        ON vector_documents
        USING hnsw (embedding vector_cosine_ops)
        WITH (m = 16, ef_construction = 200);
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP INDEX IF EXISTS ix_vector_documents_embedding_hnsw;");
}
```

**Step 3: Apply migration locally**

Run: `cd apps/api/src/Api && dotnet ef database update`
Expected: Migration applied, index created

**Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "migration: add HNSW index on vector_documents.embedding for pgvector"
```

### Task 3.4: Remove Qdrant dependencies

**Files:**
- Modify: `apps/api/src/Api/Api.csproj` (remove Qdrant.Client NuGet)
- Modify: `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs` (remove Qdrant HTTP client)
- Modify: `apps/api/src/Api/appsettings.json` (remove Qdrant config section)

**Step 1: Remove Qdrant.Client NuGet package**

Run: `cd apps/api/src/Api && dotnet remove package Qdrant.Client`

**Step 2: Remove Qdrant HTTP client registration**

Read `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs` — find and remove the `AddHttpClient("Qdrant")` registration block.

**Step 3: Remove Qdrant config from appsettings**

Read `apps/api/src/Api/appsettings.json` — find and remove any `"Qdrant"` or `"QdrantUrl"` configuration sections.

**Step 4: Build to verify no compilation errors**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded. If Qdrant.Client types are referenced elsewhere, those files need updating (replace with pgvector calls).

**Step 5: Run full test suite**

Run: `cd apps/api && dotnet test --filter "Category!=Integration" -v n`
Expected: All unit tests pass (integration tests may need Qdrant-specific mocks updated)

**Step 6: Commit**

```bash
git add apps/api/
git commit -m "refactor(qdrant): remove Qdrant.Client dependency, use pgvector exclusively"
```

---

## Phase 4: PDF Processing Transition

### Task 4.1: Add PdfPig NuGet and implement PdfPigTextExtractor

**Files:**
- Modify: `apps/api/src/Api/Api.csproj` (add UglyToad.PdfPig)
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/PdfPigTextExtractor.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/PdfPigTextExtractorTests.cs`

**Step 1: Add PdfPig NuGet package**

Run: `cd apps/api/src/Api && dotnet add package UglyToad.PdfPig`

**Step 2: Read IPdfTextExtractor interface**

Read `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/IPdfTextExtractor.cs` to understand the contract.

**Step 3: Write failing tests**

Create test for:
- `ExtractText_FromSimplePdf_ReturnsText`
- `ExtractText_FromEmptyPdf_ReturnsEmpty`
- `ExtractChunks_RespectsMaxChunkSize`

Use a small test PDF file or generate one in-memory with PdfPig.

**Step 4: Implement PdfPigTextExtractor**

```csharp
internal sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public async Task<PdfExtractionResult> ExtractAsync(
        Stream pdfStream, PdfExtractionOptions options, CancellationToken ct)
    {
        using var document = PdfDocument.Open(pdfStream);
        var fullText = new StringBuilder();
        var chunks = new List<TextChunk>();

        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            fullText.AppendLine(pageText);
            // Chunk by paragraphs or fixed size
            chunks.AddRange(ChunkText(pageText, page.Number, options.MaxChunkSize));
        }

        return new PdfExtractionResult
        {
            Text = fullText.ToString(),
            Chunks = chunks,
            PageCount = document.NumberOfPages,
            QualityScore = CalculateQuality(fullText.ToString(), document.NumberOfPages)
        };
    }
}
```

**Step 5: Run tests**

Run: `cd apps/api && dotnet test --filter "PdfPigTextExtractor" -v n`
Expected: PASS

**Step 6: Register in DI as the default extractor**

Modify the PDF processing DI registration to use PdfPigTextExtractor as primary:
```csharp
services.AddKeyedScoped<IPdfTextExtractor, PdfPigTextExtractor>("pdfpig");
```

**Step 7: Update PdfProcessing config in appsettings.json**

```json
"PdfProcessing": {
  "Extractor": {
    "Provider": "PdfPig"
  }
}
```

**Step 8: Commit**

```bash
git add apps/api/
git commit -m "feat(pdf): add PdfPig .NET extractor replacing Python unstructured service"
```

### Task 4.2: Create offline PDF pre-processing script

**Files:**
- Create: `scripts/preprocess-pdfs.py`
- Create: `scripts/import-chunks.sh`

**Step 1: Create Python pre-processing script**

This script runs on the dev machine (NOT in production) using the existing unstructured library:

```python
"""
Offline PDF pre-processing for top board games.
Run on dev machine: python scripts/preprocess-pdfs.py ./pdfs/ ./output/
Outputs JSON files with chunks ready for DB import.
"""
import sys, json, os
from unstructured.partition.pdf import partition_pdf
from unstructured.chunking.title import chunk_by_title

def process_pdf(pdf_path, output_dir):
    elements = partition_pdf(pdf_path, strategy="fast")
    chunks = chunk_by_title(elements, max_characters=2000, overlap=200)
    result = {
        "source_file": os.path.basename(pdf_path),
        "chunks": [{"text": str(c), "page": getattr(c.metadata, 'page_number', None),
                     "type": type(c).__name__} for c in chunks]
    }
    out_path = os.path.join(output_dir, f"{os.path.splitext(os.path.basename(pdf_path))[0]}.json")
    with open(out_path, 'w') as f:
        json.dump(result, f, indent=2)
    print(f"Processed {pdf_path} -> {len(result['chunks'])} chunks")

if __name__ == "__main__":
    pdf_dir, output_dir = sys.argv[1], sys.argv[2]
    os.makedirs(output_dir, exist_ok=True)
    for f in os.listdir(pdf_dir):
        if f.endswith('.pdf'):
            process_pdf(os.path.join(pdf_dir, f), output_dir)
```

**Step 2: Commit**

```bash
git add scripts/
git commit -m "scripts: add offline PDF pre-processing for dev machine"
```

---

## Phase 5: Update Background Services and Cleanup

### Task 5.1: Disable Qdrant-dependent background services

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`

**Step 1: Read KnowledgeBaseServiceExtensions.cs**

Identify any hosted services or Quartz jobs that depend on Qdrant, embedding-service, or reranker-service.

**Step 2: Guard background services**

For any services that call Qdrant directly, either:
- Update them to use the new PgVectorStoreAdapter (via the same interface), or
- Disable them via configuration if not needed for MVP

Most services use the `IQdrantVectorStoreAdapter` interface, so they should work automatically with the new PgVectorStoreAdapter.

**Step 3: Build and run unit tests**

Run: `cd apps/api/src/Api && dotnet build && cd ../../../.. && cd apps/api && dotnet test --filter "Category=Unit" -v n`
Expected: Build + all unit tests pass

**Step 4: Commit**

```bash
git add apps/api/
git commit -m "refactor: update background services for pgvector, remove Qdrant deps"
```

### Task 5.2: Update LLM routing for MVP models

**Files:**
- Modify: `apps/api/src/Api/appsettings.json` (LlmRouting section)

**Step 1: Update LlmRouting to use Qwen2.5-1.5B for local inference**

```json
"LlmRouting": {
  "AnonymousModel": "qwen2.5:1.5b",
  "UserModel": "qwen2.5:1.5b",
  "EditorModel": "qwen2.5:1.5b",
  "AdminModel": "deepseek/deepseek-chat",
  "PremiumModel": "deepseek/deepseek-chat"
}
```

Changes:
- Anonymous/User/Editor: `meta-llama/llama-3.3-70b-instruct:free` / `llama3:8b` -> `qwen2.5:1.5b` (local Ollama)
- Admin/Premium: Keep cloud models but use DeepSeek (cheaper than Claude)

**Step 2: Update budget limits for MVP scale**

```json
"LlmBudgetAlerts": {
  "DailyBudgetUsd": 5.00,
  "MonthlyBudgetUsd": 50.00,
  "Thresholds": {
    "Warning": 0.80,
    "Critical": 0.95
  }
}
```

Changes: $50/day -> $5/day, $1000/month -> $50/month (matches EUR 16-26 budget)

**Step 3: Commit**

```bash
git add apps/api/src/Api/appsettings.json
git commit -m "config(llm): update routing for MVP models, reduce budget limits"
```

---

## Phase 6: Integration Testing and Deploy Preparation

### Task 6.1: Create MVP smoke test

**Files:**
- Create: `scripts/smoke-test-mvp.sh`

**Step 1: Create smoke test script**

```bash
#!/bin/bash
set -e
echo "=== MeepleAI MVP Smoke Test ==="

API="http://localhost:8080"
TIMEOUT=10

# 1. API health
echo -n "API health... "
curl -sf --max-time $TIMEOUT "$API/health" > /dev/null && echo "OK" || echo "FAIL"

# 2. Ollama models loaded
echo -n "Ollama LLM model... "
curl -sf --max-time $TIMEOUT "http://localhost:11434/api/tags" | grep -q "qwen2.5" && echo "OK" || echo "FAIL"

echo -n "Ollama embed model... "
curl -sf --max-time $TIMEOUT "http://localhost:11434/api/tags" | grep -q "mxbai" && echo "OK" || echo "FAIL"

# 3. PostgreSQL + pgvector
echo -n "PostgreSQL pgvector... "
PGPASSWORD=test psql -h localhost -U meepleai -d meepleai -c "SELECT vector_dims('[1,2,3]'::vector);" > /dev/null 2>&1 && echo "OK" || echo "FAIL"

# 4. Redis
echo -n "Redis... "
redis-cli ping > /dev/null 2>&1 && echo "OK" || echo "FAIL"

# 5. Frontend
echo -n "Frontend... "
curl -sf --max-time $TIMEOUT "http://localhost:3000" > /dev/null && echo "OK" || echo "FAIL"

echo "=== Smoke Test Complete ==="
```

**Step 2: Commit**

```bash
git add scripts/smoke-test-mvp.sh
git commit -m "scripts: add MVP smoke test for 6-service stack"
```

### Task 6.2: Update project documentation

**Files:**
- Modify: `CLAUDE.md` (update Quick Reference, Stack section, Docker commands)

**Step 1: Read current CLAUDE.md**

Note sections that reference eliminated services (Qdrant, Python services, monitoring).

**Step 2: Add MVP deployment section to CLAUDE.md**

Add a new section or update existing:
```markdown
### MVP Deployment (6 services)
| Service | Purpose |
|---------|---------|
| PostgreSQL + pgvector | DB + vector search |
| Redis | Cache + sessions |
| Ollama | LLM + embeddings |
| .NET API | Backend |
| Next.js | Frontend |
| Caddy | Reverse proxy |

Start: `cd infra && docker compose -f compose.mvp.yml up -d`
```

**Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md with MVP deployment instructions"
```

### Task 6.3: Final build verification

**Step 1: Full backend build**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded, 0 errors

**Step 2: Run unit tests**

Run: `cd apps/api && dotnet test --filter "Category=Unit" -v n`
Expected: All tests pass

**Step 3: Frontend build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeded

**Step 4: Start MVP stack locally**

Run: `cd infra && docker compose -f compose.mvp.yml up -d`
Expected: All 6 containers healthy

**Step 5: Run smoke test**

Run: `bash scripts/smoke-test-mvp.sh`
Expected: All checks OK

**Step 6: Tag the release**

```bash
git tag -a v0.1.0-mvp -m "MVP stack: 6 services, pgvector, Ollama embedding+LLM"
```

---

## Summary: Task Dependency Graph

```
Phase 1 (docker-compose)
  └─ Task 1.1: Create MVP docker-compose + Caddyfile

Phase 2 (config changes) — can run in parallel
  ├─ Task 2.1: Switch embedding to Ollama mxbai
  └─ Task 2.2: Disable reranker

Phase 3 (pgvector) — sequential, depends on Phase 2
  ├─ Task 3.1: Write PgVectorStoreAdapter tests (red)
  ├─ Task 3.2: Implement PgVectorStoreAdapter (green)
  ├─ Task 3.3: Add HNSW index migration
  └─ Task 3.4: Remove Qdrant dependencies

Phase 4 (PDF) — can run in parallel with Phase 3
  ├─ Task 4.1: PdfPig extractor + tests
  └─ Task 4.2: Offline pre-processing script

Phase 5 (cleanup) — depends on Phase 3 + 4
  ├─ Task 5.1: Update background services
  └─ Task 5.2: Update LLM routing config

Phase 6 (verification) — depends on all above
  ├─ Task 6.1: Smoke test script
  ├─ Task 6.2: Update documentation
  └─ Task 6.3: Final build + integration verification
```

**Estimated total effort: 12-15 working days**

**Parallelization opportunities:**
- Phase 2 tasks can run in parallel
- Phase 3 and Phase 4 can run in parallel
- Phase 1 has no dependencies, start immediately
