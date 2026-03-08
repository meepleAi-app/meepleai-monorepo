# Feasibility Research: .NET Native Embedding & Reranking

**Date**: 2026-03-07
**Confidence Level**: High (85%) - based on official documentation, NuGet packages, and HuggingFace model cards
**Status**: Complete

---

## Executive Summary

Running embedding and reranker models directly in .NET is **production-feasible today** via multiple approaches. The most mature path is **Semantic Kernel + ONNX Runtime**, which provides a first-party Microsoft abstraction over ONNX models. **Ollama** is the strongest alternative for unified LLM + embedding serving, and now supports reranking via Qwen3 models. The primary risk is **ARM64 (aarch64) support** for ONNX Runtime NuGet packages, which requires careful package selection or building from source.

### Recommendation Matrix

| Approach | Embedding | Reranking | ARM64 | Production Ready | Effort |
|----------|-----------|-----------|-------|-----------------|--------|
| Semantic Kernel + ONNX | Yes | Manual | Risky | Yes (alpha pkg) | Medium |
| Raw ONNX Runtime | Yes | Yes | Risky | Yes | High |
| Ollama (unified) | Yes | Yes (Qwen3) | Yes | Yes | Low |
| Current (Python sidecar) | Yes | Yes | Yes | Yes (proven) | Zero |

**Winner for MeepleAI**: **Ollama as unified provider** (embedding + reranking + LLM) with Python sidecar as fallback. Eliminates two Python microservices while maintaining quality.

---

## 1. Embedding in .NET via ONNX Runtime

### 1.1 Can sentence-transformers models run in .NET?

**Yes.** Any sentence-transformers model can be exported to ONNX format and loaded via ONNX Runtime in C#. The pipeline is:

1. Export model to ONNX using `optimum-cli` or Python script
2. Load ONNX model + tokenizer vocab in C#
3. Run inference via `Microsoft.ML.OnnxRuntime`

```bash
# Export any sentence-transformers model to ONNX
pip install optimum[onnxruntime]
optimum-cli export onnx --model sentence-transformers/all-MiniLM-L6-v2 ./onnx-output/
```

### 1.2 Pre-exported ONNX Models on HuggingFace

These are ready to download and use directly -- no export step needed:

| Model | HuggingFace ONNX Repo | Size (fp32) | Size (int8) | Dimensions | Params |
|-------|----------------------|-------------|-------------|------------|--------|
| all-MiniLM-L6-v2 | `onnx-models/all-MiniLM-L6-v2-onnx`, `Xenova/all-MiniLM-L6-v2` | ~90 MB | ~23 MB | 384 | 22M |
| multilingual-e5-large | `Teradata/multilingual-e5-large` (ONNX included) | ~1,059 MB | ~266 MB | 1024 | 278M |
| nomic-embed-text-v1.5 | `michael-sigamani/nomic-embed-text-onnx` | ~550 MB | ~140 MB | 768 | 137M |
| bge-micro-v2 | Available via Semantic Kernel examples | ~50 MB | ~13 MB | 384 | 17M |

### 1.3 Required NuGet Packages

**Option A: Raw ONNX Runtime (full control)**
```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.24.2" />
<PackageReference Include="Microsoft.ML.OnnxRuntime.Managed" Version="1.24.2" />
<!-- For tokenization: -->
<PackageReference Include="BERTTokenizers" Version="1.2.0" />
<!-- Or use SharpToken / custom vocab loader -->
```

**Option B: Semantic Kernel (recommended abstraction)**
```xml
<PackageReference Include="Microsoft.SemanticKernel.Connectors.Onnx" Version="1.39.0-alpha" />
<!-- Pulls in ONNX Runtime transitively -->
<!-- Note: Still in -alpha prerelease -->
```

**Option C: Community package (all-MiniLM-L6-v2 only)**
```xml
<PackageReference Include="AllMiniLmL6V2Sharp" Version="0.0.3" />
<!-- .NET Standard 2.1, includes tokenizer, does NOT include ONNX model file -->
```

### 1.4 Semantic Kernel Usage Example

```csharp
using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0070 // Alpha API
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddBertOnnxTextEmbeddingGeneration(
    onnxModelPath: "/models/all-MiniLM-L6-v2/model.onnx",
    vocabPath: "/models/all-MiniLM-L6-v2/vocab.txt"
);

var kernel = kernelBuilder.Build();
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
var embeddings = await embeddingService.GenerateEmbeddingsAsync(["Hello world"]);
// Returns ReadOnlyMemory<float> with 384 dimensions
```

The Semantic Kernel ONNX connector implements `IEmbeddingGenerator<string, Embedding<float>>` from `Microsoft.Extensions.AI`, making it compatible with the standardized .NET AI abstraction.

### 1.5 Performance Comparison

| Metric | Python (sentence-transformers) | ONNX in .NET | Notes |
|--------|-------------------------------|--------------|-------|
| Single sentence latency | ~15-25ms (GPU) / ~50-80ms (CPU) | ~10-20ms (CPU, optimized) | ONNX Runtime often faster on CPU |
| Throughput (CPU) | ~50 sentences/sec | ~50-80 sentences/sec | ONNX Runtime has better CPU optimization |
| Cold start | 2-5 seconds | 1-3 seconds | No Python interpreter overhead |
| Batch processing | Native batching | Manual batching needed | Python has better batch tooling |
| Memory overhead | ~200-500 MB (Python runtime + model) | ~50-150 MB (model only) | No Python/PyTorch overhead in .NET |

### 1.6 RAM Usage for Common Models (ONNX Format)

| Model | fp32 RAM | int8/quantized RAM | Notes |
|-------|----------|-------------------|-------|
| all-MiniLM-L6-v2 | ~90-120 MB | ~25-45 MB | Best for speed/size tradeoff |
| nomic-embed-text-v1.5 | ~550-700 MB | ~140-200 MB | Good quality, larger |
| multilingual-e5-large | ~1,060-1,300 MB | ~270-350 MB | Best multilingual, heavy |
| bge-micro-v2 | ~50-70 MB | ~15-25 MB | Smallest, lower quality |

Add ~20% overhead for ONNX Runtime session management.

---

## 2. Reranker in .NET via ONNX

### 2.1 Can cross-encoder models be exported to ONNX?

**Yes.** Cross-encoders export cleanly to ONNX via `optimum-cli`:

```bash
optimum-cli export onnx --task text-classification -m BAAI/bge-reranker-v2-m3 ./reranker-onnx/
# Produces: model.onnx + tokenizer.json
```

Pre-exported ONNX versions exist on HuggingFace:
- `corto-ai/bge-reranker-large-onnx` (bge-reranker-large)
- `mogolloni/bge-reranker-v2-m3-onnx` (bge-reranker-v2-m3)

### 2.2 Cross-Encoder Inference in .NET

Unlike bi-encoders (embeddings), cross-encoders take a **query-document pair** as input and output a single relevance score. The ONNX inference in C# requires:

1. Tokenize `[CLS] query [SEP] passage [SEP]` as a single input
2. Run ONNX model inference
3. Extract logit score from output tensor
4. Apply sigmoid for 0-1 relevance score

```csharp
// Pseudocode for cross-encoder inference
using var session = new InferenceSession("bge-reranker-v2-m3.onnx");
foreach (var passage in passages)
{
    var tokens = tokenizer.Encode(query, passage); // pair encoding
    var inputs = new List<NamedOnnxValue>
    {
        NamedOnnxValue.CreateFromTensor("input_ids", tokens.InputIds),
        NamedOnnxValue.CreateFromTensor("attention_mask", tokens.AttentionMask),
    };
    using var results = session.Run(inputs);
    var logits = results.First().AsEnumerable<float>().First();
    var score = 1f / (1f + MathF.Exp(-logits)); // sigmoid
}
```

### 2.3 Performance for Real-Time Reranking (15-20 passages)

| Model | Params | ONNX Size | Per-Pair Latency (CPU) | 20 Passages Total | Practical? |
|-------|--------|-----------|----------------------|-------------------|------------|
| bge-reranker-base | 278M | ~1.1 GB | ~30-50ms | ~600ms-1s | Yes |
| bge-reranker-v2-m3 | 568M | ~2.2 GB | ~50-80ms | ~1-1.6s | Marginal |
| bge-reranker-large | 560M | ~2.2 GB | ~50-80ms | ~1-1.6s | Marginal |

**Verdict**: bge-reranker-base is practical for real-time reranking of 15-20 passages on CPU (~600ms-1s total). The v2-m3 and large variants are borderline -- acceptable for async but tight for real-time UX. Quantized (int8) versions would reduce latency by ~30-40%.

**Important**: There is no Semantic Kernel abstraction for cross-encoder reranking. You must use raw ONNX Runtime or a custom wrapper.

---

## 3. .NET AI Libraries Assessment

### 3.1 Microsoft.Extensions.AI

- **Status**: Generally Available (GA), no longer preview
- **Interface**: `IEmbeddingGenerator<string, Embedding<float>>`
- **Local embedding support**: Not directly -- it defines abstractions, not implementations
- **Relevance**: Semantic Kernel's ONNX connector implements this interface
- **No reranker abstraction** exists in Microsoft.Extensions.AI

### 3.2 Semantic Kernel ONNX Connector

| Aspect | Details |
|--------|---------|
| Package | `Microsoft.SemanticKernel.Connectors.Onnx` |
| Version | 1.39.0-alpha (prerelease) |
| Target | .NET 8.0+ and .NET Standard 2.0 |
| Supports | BERT-family embedding models via ONNX |
| Method | `AddBertOnnxTextEmbeddingGeneration()` |
| Implements | `IEmbeddingGenerator<string, Embedding<float>>` |
| Maturity | Alpha -- API may change, but functional |

**Risk**: The `-alpha` tag means the API could change. However, Microsoft has been actively developing this for 15+ versions, suggesting commitment.

### 3.3 Other .NET Approaches

| Library | Embedding Support | Notes |
|---------|------------------|-------|
| ML.NET | Via ONNX Scoring | Lower-level, more boilerplate |
| TorchSharp | Full PyTorch in .NET | Heavy, complex, not recommended for inference-only |
| AllMiniLmL6V2Sharp | all-MiniLM-L6-v2 only | Community package, limited scope |
| SentenceTransformersCSharp | Via ONNX + tokenizer | .NET Framework 4.6.1 target, legacy |

---

## 4. Ollama as Unified Provider

### 4.1 Embedding Support

Ollama provides embeddings via the `/api/embed` endpoint (previously `/api/embeddings`):

```bash
curl http://localhost:11434/api/embed -d '{
  "model": "nomic-embed-text",
  "input": "Hello world"
}'
```

**Available embedding models:**

| Model | Size (Download) | RAM Required | Dimensions | Context | Quality (MTEB) |
|-------|----------------|-------------|------------|---------|---------------|
| nomic-embed-text | 274 MB | ~500 MB | 768 | 8192 | 95.2 avg |
| nomic-embed-text-v2-moe | ~600 MB | ~1 GB | 768 | 8192 | Higher than v1 |
| mxbai-embed-large | 670 MB | ~1.2 GB | 1024 | 512 | 94.8 avg |
| all-minilm (via Ollama) | 46 MB | ~150 MB | 384 | 256 | Lower |

### 4.2 Quality: Ollama vs sentence-transformers

- Cosine similarity between Ollama and sentence-transformers implementations is **very close**
- Same vectorstore queries return the **same chunks** (occasionally in different order)
- Minor numerical differences in raw float values but semantically equivalent
- nomic-embed-text **surpasses** OpenAI text-embedding-ada-002 and text-embedding-3-small

**Verdict**: Quality is production-equivalent. No meaningful degradation vs Python sentence-transformers.

### 4.3 Reranking via Ollama (NEW - 2025/2026)

Ollama now supports reranker models via Qwen3:

| Model | Params | Size | Languages | Context |
|-------|--------|------|-----------|---------|
| qwen3-reranker:0.6b | 0.6B | ~400 MB | 100+ | 32k |
| qwen3-reranker:4b | 4B | ~2.5 GB | 100+ | 32k |
| qwen3-reranker:8b | 8B | ~5 GB | 100+ | 32k |

**Important caveat**: Ollama does NOT have a native `/api/rerank` endpoint. Reranking is done by:
1. Passing query+document pairs to the model
2. Extracting relevance scores from model output
3. Custom scoring logic in your application

This is less convenient than a dedicated cross-encoder API but functionally works.

### 4.4 Ollama as Unified LLM + Embedding + Reranker

**Yes, Ollama can serve all three roles:**

```yaml
# Single Ollama instance serving:
LLM:       llama3.2:3b or qwen2.5:7b    # Chat/generation
Embedding: nomic-embed-text              # Vector embeddings
Reranker:  qwen3-reranker:0.6b           # Passage reranking
```

**RAM budget for Hetzner CAX:**

| CAX Server | RAM | Can Run |
|-----------|-----|---------|
| CAX11 (4 vCPU) | 8 GB | nomic-embed-text (500MB) + qwen3-reranker:0.6b (400MB) + small LLM (3b) |
| CAX21 (4 vCPU) | 8 GB | Same as above |
| CAX31 (8 vCPU) | 16 GB | nomic-embed-text + qwen3-reranker:4b + medium LLM (7b) |
| CAX41 (16 vCPU) | 32 GB | Full stack with larger models |

### 4.5 Embedding Performance on ARM64

| Metric | Value |
|--------|-------|
| Latency (single sentence) | 15-50ms on ARM64 CPU |
| Throughput (nomic-embed-text) | ~9,340 tokens/sec (M2 Max benchmark; ARM64 server will be lower) |
| Throughput (mxbai-embed-large) | ~6,780 tokens/sec (M2 Max benchmark) |

Expect Hetzner CAX (Ampere Altra) to achieve roughly 60-80% of Apple Silicon performance for embedding workloads.

---

## 5. ARM64 Compatibility Assessment

### 5.1 ONNX Runtime on ARM64

| Component | ARM64 Support | Notes |
|-----------|---------------|-------|
| ONNX Runtime C/C++ library | Yes (linux-aarch64 binaries published) | Official releases include aarch64 |
| Microsoft.ML.OnnxRuntime NuGet | **Partial/Risky** | Historically x64/x86 only for .NET; recent versions improving |
| Microsoft.ML.OnnxRuntime.Extensions | Yes (arm64 supported) | Explicitly lists arm64 |
| Python onnxruntime | Yes (wheels for aarch64) | Proven on ARM64 |

**Risk**: The main `Microsoft.ML.OnnxRuntime` NuGet package has had issues with ARM64 Linux native library loading. Recent versions (1.24.x) have fixed some of these issues, but thorough testing on Hetzner CAX is required before committing to this path.

**Mitigation**: If NuGet package fails on ARM64:
1. Build ONNX Runtime from source for linux-aarch64
2. Use the native C library directly via P/Invoke
3. Fall back to Ollama (which handles ARM64 natively)

### 5.2 Ollama on ARM64

| Aspect | Status |
|--------|--------|
| Official ARM64 Linux builds | Yes (aarch64 packages available) |
| Hetzner CAX compatibility | Yes (confirmed by community) |
| Performance | CPU-only (no GPU on CAX), ~80% of x86 performance |
| Installation | `curl -fsSL https://ollama.com/install.sh | sh` |

**Ollama is the safest ARM64 choice** -- fully tested, widely deployed on aarch64.

### 5.3 .NET on ARM64

| Component | ARM64 Support |
|-----------|---------------|
| .NET 9 Runtime | Yes (official linux-arm64 builds) |
| ASP.NET Core on ARM64 | Yes (production ready) |
| EF Core on ARM64 | Yes |
| Semantic Kernel | Yes (managed code, platform independent) |

.NET itself runs perfectly on ARM64. The only risk is the ONNX Runtime native library.

---

## 6. Practical Recommendations for MeepleAI

### Option A: Ollama Unified (RECOMMENDED)

**Eliminate both Python microservices. Use Ollama for everything.**

```
Current Architecture:
  .NET API --> embedding-service (Python/FastAPI) --> sentence-transformers
  .NET API --> reranker-service (Python/FastAPI) --> cross-encoder

Proposed Architecture:
  .NET API --> Ollama (single process)
               |- nomic-embed-text (embeddings)
               |- qwen3-reranker:0.6b (reranking)
               |- llama3.2 / qwen2.5 (LLM)
```

**Pros:**
- Eliminates 2 Python microservices (embedding-service, reranker-service)
- Single dependency for all AI inference
- Native ARM64 support
- Simple deployment (single binary)
- ~500 MB RAM for embeddings (vs ~500 MB for Python + sentence-transformers)

**Cons:**
- Qwen3-reranker quality vs bge-reranker-v2-m3 needs validation
- No native rerank API in Ollama (requires custom implementation)
- Vendor lock to Ollama's model format (GGUF)

**RAM Budget (CAX31 - 16 GB):**
```
PostgreSQL:     ~2 GB
Qdrant:         ~2 GB
Redis:          ~0.5 GB
.NET API:       ~0.5 GB
Ollama:         ~3 GB (nomic-embed + qwen3-reranker:0.6b + small LLM)
Next.js:        ~0.5 GB
OS + overhead:  ~2 GB
--------------------------
Total:          ~11 GB (5 GB headroom)
```

### Option B: ONNX Runtime in .NET (HIGH EFFORT, ARM64 RISK)

**Run models directly in the .NET process via Semantic Kernel.**

```csharp
// In your DI setup:
services.AddBertOnnxTextEmbeddingGeneration(
    onnxModelPath: "/models/nomic-embed-text/model.onnx",
    vocabPath: "/models/nomic-embed-text/vocab.txt"
);
```

**Pros:**
- Zero external dependencies (models loaded in-process)
- Lowest possible latency (no HTTP overhead)
- Microsoft-supported stack

**Cons:**
- ARM64 NuGet package compatibility is uncertain
- Reranker requires custom ONNX Runtime code (no Semantic Kernel support)
- Larger .NET process memory footprint
- Alpha-stage NuGet package
- Tokenizer compatibility must be validated per model

### Option C: Hybrid (PRAGMATIC FALLBACK)

Keep Python embedding-service but switch reranker to Ollama Qwen3:

```
.NET API --> embedding-service (Python, proven)
.NET API --> Ollama (qwen3-reranker + LLM)
```

This reduces from 2 Python services to 1 while using proven embedding infrastructure.

---

## 7. Migration Path (if choosing Option A)

### Phase 1: Add Ollama Embedding Support
1. Add `IOllamaEmbeddingService` interface in .NET
2. Implement HTTP client calling `POST /api/embed` with `nomic-embed-text`
3. Run both Python and Ollama in parallel, compare embeddings
4. Validate cosine similarity > 0.95 for same inputs

### Phase 2: Add Ollama Reranking
1. Implement `IOllamaRerankerService` using `qwen3-reranker:0.6b`
2. Compare reranking quality vs current bge-reranker-v2-m3
3. Validate on MeepleAI test queries (board game rules retrieval)

### Phase 3: Switch Over
1. Configure feature flag for embedding provider (Python vs Ollama)
2. Deploy Ollama alongside existing services
3. Gradually shift traffic
4. Remove Python services after validation

### Phase 4: ARM64 Deployment
1. Test Ollama on Hetzner CAX instance
2. Benchmark embedding throughput on Ampere Altra
3. Validate RAM usage under load
4. Deploy to production

---

## Sources

### ONNX Runtime & .NET
- [ONNX Runtime C# Getting Started](https://onnxruntime.ai/docs/get-started/with-csharp.html)
- [ONNX Runtime BERT NLP C# Tutorial](https://onnxruntime.ai/docs/tutorials/csharp/bert-nlp-csharp-console-app.html)
- [Microsoft.ML.OnnxRuntime NuGet 1.24.2](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime)
- [ONNX Runtime Memory Tuning](https://onnxruntime.ai/docs/performance/tune-performance/memory.html)
- [ONNX Runtime ARM64 Build Instructions](https://onnxruntime.ai/docs/build/inferencing.html)

### Semantic Kernel
- [Semantic Kernel Embedding Generation](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/embedding-generation/)
- [BertOnnxTextEmbeddingGeneration API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.semantickernel.onnxkernelbuilderextensions.addbertonnxtextembeddinggeneration)
- [Microsoft.SemanticKernel.Connectors.Onnx NuGet](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.Onnx/1.39.0-alpha)
- [Implementing Embeddings via ONNX with Semantic Kernel (2025)](https://elguerre.com/2025/05/25/implementing-embeddings-via-onnx-with-semantic-kernel-for-local-rag-solutions-in-net/)
- [Semantic Kernel + Microsoft.Extensions.AI Integration](https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-and-microsoft-extensions-ai-better-together-part-2/)

### Pre-exported ONNX Models
- [all-MiniLM-L6-v2 ONNX](https://huggingface.co/onnx-models/all-MiniLM-L6-v2-onnx)
- [Xenova/all-MiniLM-L6-v2](https://huggingface.co/Xenova/all-MiniLM-L6-v2)
- [nomic-embed-text ONNX](https://huggingface.co/michael-sigamani/nomic-embed-text-onnx)
- [bge-reranker-large ONNX](https://huggingface.co/corto-ai/bge-reranker-large-onnx)
- [bge-reranker-v2-m3 ONNX](https://huggingface.co/mogolloni/bge-reranker-v2-m3-onnx)

### Ollama
- [Ollama Embedding Models Blog](https://ollama.com/blog/embedding-models)
- [nomic-embed-text on Ollama](https://ollama.com/library/nomic-embed-text)
- [mxbai-embed-large on Ollama](https://ollama.com/library/mxbai-embed-large)
- [Qwen3 Reranker on Ollama](https://www.glukhov.org/post/2025/06/qwen3-embedding-qwen3-reranker-on-ollama/)
- [Ollama Embedding Discrepancies vs SentenceTransformers](https://github.com/ollama/ollama/issues/7085)
- [Ollama Rerankers Issue #3749](https://github.com/ollama/ollama/issues/3749)

### ARM64
- [Hetzner ARM64 Cloud Servers](https://www.hetzner.com/press-release/arm64-cloud)
- [Hetzner CAX ARM64 Performance Review](https://blog.webp.se/hetzner-arm64-en/)
- [ONNX on ARM Learning Path](https://learn.arm.com/learning-paths/mobile-graphics-and-gaming/onnx/01_fundamentals/)
- [ONNX Runtime ARM64 Benchmark on Azure Cobalt](https://learn.arm.com/learning-paths/servers-and-cloud-computing/onnx-on-azure/benchmarking/)

### Community Packages
- [AllMiniLmL6V2Sharp NuGet](https://www.nuget.org/packages/AllMiniLmL6V2Sharp)
- [AllMiniLML6v2Sharp GitHub](https://github.com/ksanman/AllMiniLML6v2Sharp)
- [SentenceTransformersCSharp NuGet](https://www.nuget.org/packages/SentenceTransformersCSharp/1.0.4)
