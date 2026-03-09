# MeepleAI - Ricerca e Analisi

Analisi di fattibilita, copyright RAG, policy editori, mechanic extractor.

**Data generazione**: 8 marzo 2026

**File inclusi**: 6

---

## Indice

1. research/2026-03-07-dotnet-embedding-reranking-feasibility.md
2. research/2026-03-07-rulebook-copyright-rag-analysis.md
3. research/board-game-pdf-copyright-analysis.md
4. research/board-game-publisher-rulebook-pdf-policies.md
5. research/mechanic-extractor-implementation-plan.md
6. research/rag-copyright-legal-analysis-2026.md

---



<div style="page-break-before: always;"></div>

## research/2026-03-07-dotnet-embedding-reranking-feasibility.md

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

*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

**Option B: Semantic Kernel (recommended abstraction)**
*(blocco di codice rimosso)*

**Option C: Community package (all-MiniLM-L6-v2 only)**
*(blocco di codice rimosso)*

### 1.4 Semantic Kernel Usage Example

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

Pre-exported ONNX versions exist on HuggingFace:
- `corto-ai/bge-reranker-large-onnx` (bge-reranker-large)
- `mogolloni/bge-reranker-v2-m3-onnx` (bge-reranker-v2-m3)

### 2.2 Cross-Encoder Inference in .NET

Unlike bi-encoders (embeddings), cross-encoders take a **query-document pair** as input and output a single relevance score. The ONNX inference in C# requires:

1. Tokenize `[CLS] query [SEP] passage [SEP]` as a single input
2. Run ONNX model inference
3. Extract logit score from output tensor
4. Apply sigmoid for 0-1 relevance score

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

### Option B: ONNX Runtime in .NET (HIGH EFFORT, ARM64 RISK)

**Run models directly in the .NET process via Semantic Kernel.**

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

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


---



<div style="page-break-before: always;"></div>

## research/2026-03-07-rulebook-copyright-rag-analysis.md

# Copyright Analysis: Board Game Rulebooks in RAG Systems

**Date**: 2026-03-07
**Scope**: US, EU, and Italian copyright law as applied to processing board game rulebook PDFs in a Retrieval Augmented Generation (RAG) system
**Confidence Level**: High (based on published case law, statutory text, and authoritative guidance from the U.S. Copyright Office, EU Directive text, and Italian Law No. 132/2025)
**Disclaimer**: This is a legal research summary, not legal advice. Consult qualified legal counsel for specific situations.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Copyright Fundamentals: Game Rules vs. Rulebooks](#2-copyright-fundamentals-game-rules-vs-rulebooks)
3. [US Copyright Law](#3-us-copyright-law)
4. [EU Copyright Law](#4-eu-copyright-law)
5. [Italian Copyright Law](#5-italian-copyright-law)
6. [Fair Use / Fair Dealing Analysis for RAG](#6-fair-use--fair-dealing-analysis-for-rag)
7. [Key Legal Risks by Scenario](#7-key-legal-risks-by-scenario)
8. [Risk Mitigation Strategies](#8-risk-mitigation-strategies)
9. [Conclusions and Recommendations](#9-conclusions-and-recommendations)
10. [Sources](#10-sources)

---

## 1. Executive Summary

The legal landscape for using board game rulebook PDFs in a RAG system is nuanced and jurisdiction-dependent. The core distinction is:

- **Game mechanics/rules** (the systems and processes): **NOT copyrightable** in any jurisdiction studied
- **Rulebook text** (the specific written expression, layout, illustrations, flavor text): **IS copyrightable**

This means that while anyone can freely describe how to play a game in their own words, the specific text of a publisher's rulebook is protected expression. Using that protected text in a RAG system involves reproduction and potentially derivative work creation, triggering copyright analysis.

**Key findings by jurisdiction**:

| Jurisdiction | Framework | RAG Risk Level | Key Exception |
|---|---|---|---|
| **US** | Fair use (case-by-case) | MODERATE-HIGH | Transformative use defense, but US Copyright Office skeptical of RAG |
| **EU** | TDM exceptions (Arts. 3-4, Dir. 2019/790) | LOW-MODERATE | Article 4 permits commercial TDM unless rightholder opts out |
| **Italy** | Art. 70-septies (L. 633/1941, as amended) | LOW-MODERATE | Follows EU TDM framework; criminal penalties for violations |

**Bottom line for MeepleAI**: A RAG system where users upload their own PDFs for personal Q&A assistance carries the lowest legal risk. A system that centrally downloads and processes publisher PDFs for all users carries the highest risk and requires careful compliance with TDM opt-out mechanisms (EU/Italy) or a strong fair use argument (US).

---

## 2. Copyright Fundamentals: Game Rules vs. Rulebooks

### 2.1 The Idea-Expression Dichotomy

Copyright law across all studied jurisdictions draws a fundamental line between **ideas** (unprotectable) and **expression** (protectable). For board games:

**NOT copyrightable** (ideas, systems, methods of operation):
- Game mechanics (turn structure, victory conditions, resource management systems)
- Rules as abstract concepts ("roll dice, move that many spaces")
- Mathematical formulas and scoring systems
- Game strategies and optimal play patterns

**IS copyrightable** (expression):
- The specific written text of a rulebook (word choice, sentence structure, explanatory prose)
- Illustrations, diagrams, and graphic design of the rulebook
- Flavor text, thematic narrative, and world-building content
- Layout and typographic arrangement (in some jurisdictions)
- Example scenarios and walkthroughs (as literary expression)

This distinction is codified in 17 U.S.C. Section 102(b):

> "In no case does copyright protection for an original work of authorship extend to any idea, procedure, process, system, method of operation, concept, principle, or discovery, regardless of the form in which it is described, explained, illustrated, or embodied in such work."

### 2.2 Practical Implications

A board game rulebook is a mixed work. When a RAG system chunks and embeds a rulebook PDF, it captures both:
- Unprotectable mechanical descriptions (the rules themselves)
- Protectable expression (how those rules are explained, the creative choices in presentation)

The legal risk is concentrated in the **reproduction and storage of the expressive elements**, not the mechanical content.

---

## 3. US Copyright Law

### 3.1 Key Case Law

#### Baker v. Selden, 101 U.S. 99 (1879)

The foundational case establishing that copyright in a book describing a system (bookkeeping) does not grant exclusive rights to the system itself. The Supreme Court held that the **description** of a method is copyrightable, but the **method itself** requires patent protection. This case is directly analogous to game rulebooks: the text describing how to play is protectable expression, but the gameplay system is not.

**Relevance to MeepleAI**: A RAG system can freely answer "how do you play Catan?" by synthesizing game mechanics. It cannot reproduce substantial portions of the Catan rulebook verbatim.

#### Lotus Development Corp. v. Borland International, Inc., 49 F.3d 807 (1st Cir. 1995)

Extended Baker v. Selden to hold that a software menu command hierarchy is an uncopyrightable "method of operation." Affirmed by an equally divided Supreme Court (no precedential value nationally, but influential). The First Circuit held that functional elements serving as the means to operate something are excluded from copyright protection.

**Relevance to MeepleAI**: Reinforces that game mechanics, as the "method of operation" of a game, are not copyrightable. The specific way those mechanics are presented in a rulebook remains protected.

#### ABA Article: "It's How You Play the Game" (2024)

A 2024 American Bar Association article specifically argues that video game rules are not expression protected by copyright law, extending the Baker/Lotus line of reasoning to the gaming context. Courts analyzing copyright infringement of games must filter out similarities between rules and mechanics, giving them no weight in the infringement analysis.

### 3.2 US Copyright Office AI Reports (2024-2025)

The US Copyright Office released a three-part study on AI and copyright:

- **Part 1** (July 2024): Digital replicas/deepfakes
- **Part 2** (January 2025): Copyrightability of AI-generated outputs
- **Part 3** (May 2025): Generative AI training and fair use

**Part 3 is directly relevant to RAG systems.** Key findings:

1. **RAG involves reproduction**: The Office found that RAG operates in two steps: (a) copying source materials into a retrieval database, and (b) outputting retrieved content in response to queries. Both steps involve reproduction of copyrighted works.

2. **RAG is less likely to be transformative**: The Office stated that "use of RAG is less likely to be transformative where the purpose is to generate outputs that summarize or provide abridged versions of retrieved copyrighted works, such as news articles, as opposed to hyperlinks."

3. **Market substitution risk**: The Office noted that "retrieval of copyrighted works by RAG can also result in market substitution" -- meaning the RAG output could substitute for reading the original work.

4. **No blanket fair use**: The Office rejected the idea that all AI uses of copyrighted material are fair use, calling it a "highly fact-specific inquiry."

### 3.3 The Four-Factor Fair Use Test (17 U.S.C. Section 107)

Applied to a rulebook RAG system:

| Factor | Analysis | Favors |
|---|---|---|
| **1. Purpose and character of use** | Q&A assistance is arguably transformative (new purpose: interactive help vs. passive reading). But the Copyright Office is skeptical of RAG summarization as transformative. Commercial use weighs against. | Mixed |
| **2. Nature of the copyrighted work** | Rulebooks are factual/instructional (favors fair use) but contain creative expression (illustrations, flavor text). | Slightly favors fair use |
| **3. Amount and substantiality** | RAG systems typically chunk and store the entire work. Even if only portions are retrieved, the entire work was copied into the database. | Weighs against fair use |
| **4. Market effect** | A RAG system answering rules questions could reduce need to consult the original rulebook. However, publishers often provide free PDFs, suggesting limited commercial market harm. | Mixed to slightly against |

**Overall US fair use assessment**: UNCERTAIN. The case is defensible but not clearly favorable, especially after the Copyright Office's skeptical stance on RAG in Part 3 of its AI report.

### 3.4 Dow Jones v. Perplexity AI (S.D.N.Y., filed Oct. 2024)

The first major US lawsuit specifically targeting RAG technology. Dow Jones and NY Post alleged that Perplexity AI:
- Scraped copyrighted news articles into a RAG database
- Generated summaries that substituted for reading the originals
- Allowed users to "skip the links" to original content

As of August 2025, the court denied Perplexity's motion to dismiss. The case is ongoing. While this involves news content (not game rulebooks), the legal theories are directly applicable to any RAG system that stores and retrieves copyrighted text.

**Relevance to MeepleAI**: This case could set significant precedent for RAG copyright liability in the US. The key risk factor is **market substitution** -- does the RAG answer replace the need to read the original?

---

## 4. EU Copyright Law

### 4.1 Directive 2019/790 (Copyright in the Digital Single Market)

The EU takes a fundamentally different approach from the US. Rather than relying on a general fair use defense, the EU provides **specific statutory exceptions** for text and data mining (TDM).

#### Article 2(2) -- Definition of TDM

> "Text and data mining means any automated analytical technique aimed at analysing text and data in digital form in order to generate information which includes but is not limited to patterns, trends and correlations."

RAG processing (chunking, embedding, indexing, retrieval) clearly falls within this definition.

#### Article 3 -- Scientific Research Exception

Permits TDM by research organizations and cultural heritage institutions for scientific purposes, without any opt-out possibility for rightholders. **Not applicable** to a commercial product like MeepleAI.

#### Article 4 -- General TDM Exception (Commercial Use)

This is the key provision for MeepleAI:

> Permits TDM for **any purpose** (including commercial) on works to which the user has **lawful access**, **unless** the rightholder has **expressly reserved their rights** in an "appropriate manner, such as machine-readable means."

**Key elements**:

1. **Lawful access**: The person performing TDM must have legitimate access to the content. Freely available publisher PDFs satisfy this. User-purchased/downloaded PDFs also satisfy this.

2. **Opt-out mechanism**: Rightholders can opt out of Article 4 TDM by:
   - robots.txt directives (most common for online content)
   - TDMRep protocol headers
   - Terms of service/use restrictions (a German court ruled that natural-language ToS can constitute machine-readable opt-out)
   - AI-specific metadata tags

3. **No opt-out = TDM permitted**: If a publisher provides a free PDF without any TDM reservation, processing it in a RAG system is **legally permitted under EU law**.

#### AI Act Interaction

The EU AI Act (2024) explicitly references Article 4 of the DSM Directive, confirming that TDM exceptions apply to AI training, including generative AI and RAG systems. This legislative intent is clear and documented in recitals and cross-references.

### 4.2 Practical Application to Board Game Rulebooks

Most board game publishers:
- Provide rulebook PDFs freely on their websites
- Do not include TDM reservation metadata
- Do not have robots.txt blocking AI crawlers on PDF download pages
- Do not include opt-out clauses in terms of service regarding TDM

**Under current EU law, processing freely available rulebook PDFs in a RAG system is likely permitted under Article 4, absent explicit opt-out by the publisher.**

However, this could change if publishers begin implementing opt-out mechanisms.

---

## 5. Italian Copyright Law

### 5.1 Legge 633/1941 (as amended by Law No. 132/2025)

Italy became the **first EU Member State** to enact comprehensive national AI legislation (Law No. 132 of September 23, 2025, effective October 10, 2025). This law modifies the Italian Copyright Act in significant ways:

#### Article 70-septies (New)

Permits reproduction and extraction of text/data from works lawfully available online or in databases through AI models and systems (including generative AI), provided compliance with:

- **Article 70-ter**: TDM for scientific research by research organizations/cultural heritage institutions (mirrors EU Art. 3)
- **Article 70-quater**: TDM for any purpose including commercial, unless the rightholder has exercised the opt-out (mirrors EU Art. 4)

#### Key Italian Specifics

1. **Explicit AI coverage**: Unlike the EU Directive which left some ambiguity about AI applicability, Italy's Art. 70-septies explicitly states the exception covers "AI models and systems, including generative AI." This provides strong legal clarity.

2. **Criminal penalties**: Italy introduced **criminal sanctions** for TDM violations. Article 171, paragraph 1, now includes a new letter a-ter establishing criminal offenses for unauthorized TDM. This is a significant escalation beyond the EU's civil enforcement framework.

3. **Human authorship reinforcement**: Article 1 of L. 633/1941 was modified to specify that works created with AI aid are protected "provided they constitute the result of the author's intellectual work." This is relevant to whether RAG outputs could themselves be copyrightable.

4. **Opt-out requirement**: Consistent with EU law, Italian TDM is only permitted if the rightholder has NOT opted out. The criminal penalties make compliance with opt-out mechanisms particularly critical in Italy.

### 5.2 "Libero Utilizzo" (Free Use) -- Articles 65-71

Italy does not have a general "fair use" doctrine like the US. Instead, it has a closed list of specific exceptions ("eccezioni e limitazioni"):

- **Art. 65**: Free reproduction of articles on current events in periodicals
- **Art. 68**: Personal reproduction for personal use (single copy)
- **Art. 70**: Quotation for criticism, discussion, teaching (with attribution, within limits)

**Art. 68 (personal reproduction)** could support the scenario where a user uploads their own PDF for personal RAG-assisted Q&A, as this is analogous to making a personal copy for private study. However, the systematic commercial processing of that copy by a platform operator may not fall under this exception.

### 5.3 Practical Implications for MeepleAI in Italy

- Processing freely available rulebook PDFs: **Permitted** under Art. 70-septies/70-quater, absent publisher opt-out
- Must implement **opt-out checking** before processing any content
- **Criminal liability risk** if TDM is performed on opted-out content
- User-uploaded PDFs for personal use: Lower risk under Art. 68, but the platform's systematic processing may not be covered

---

## 6. Fair Use / Fair Dealing Analysis for RAG

### 6.1 Is Chunking and Embedding Transformative?

**Arguments FOR transformativeness**:
- The purpose changes from "reading a manual" to "interactive AI-assisted Q&A"
- Embeddings are mathematical vectors, not human-readable text -- they represent semantic meaning, not literal expression
- The system generates new, synthesized answers rather than reproducing the original
- The use is functional/utilitarian (helping users understand rules during gameplay)

**Arguments AGAINST transformativeness**:
- The US Copyright Office (Part 3, 2025) explicitly stated RAG summarization is "less likely to be transformative"
- Chunks stored in the retrieval database ARE literal copies of the copyrighted text
- Retrieved chunks may be reproduced near-verbatim in system outputs
- The purpose (explaining rules) closely mirrors the original purpose of the rulebook

### 6.2 Does RAG Compete with the Original?

**Arguments that it does NOT compete**:
- Many publishers provide rulebook PDFs for free (no lost sales)
- Players still need the physical game (the rulebook supports the product, not the reverse)
- RAG provides contextual Q&A during gameplay, a different use case from reading a manual cover-to-cover
- The RAG system enhances the gameplay experience, potentially increasing game sales

**Arguments that it DOES compete**:
- Some publishers sell premium digital rulebooks or companion apps
- The RAG system could replace the need to consult the rulebook entirely
- Publisher-specific FAQ pages and BGG forums serve a similar Q&A function
- Third-party tutorial content creators (YouTube, blogs) could claim market harm

### 6.3 Embeddings vs. Full Text Storage

This is a critical technical-legal distinction:

| Storage Type | Copyright Risk | Reasoning |
|---|---|---|
| **Vector embeddings only** | LOWER | Embeddings are mathematical representations; they cannot be reversed to reconstruct the original text. Similar to the "intermediate copy" doctrine. |
| **Chunks of original text** | HIGHER | Literal copies of copyrighted expression stored in the retrieval database. This is straightforward reproduction. |
| **Full original PDF stored** | HIGHEST | Complete reproduction of the copyrighted work, even if only portions are retrieved. |

**For MeepleAI's RAG system**: The system stores text chunks for retrieval, which constitutes reproduction of copyrighted expression. This is the most legally sensitive component.

**Potential mitigation**: If the system could answer questions using ONLY embeddings (without retrieving literal text chunks), the copyright argument becomes significantly weaker. However, current RAG architectures require text chunk retrieval for quality answers.

---

## 7. Key Legal Risks by Scenario

### Scenario A: Downloading publisher-provided free PDFs and processing in RAG

| Factor | Risk Assessment |
|---|---|
| **US Law** | MODERATE-HIGH. Fair use is uncertain. The Copyright Office's RAG skepticism, combined with Dow Jones v. Perplexity precedent risk, creates significant exposure. The "free PDF" factor helps (no market harm to sales) but the systematic copying into a commercial database weighs against. |
| **EU Law** | LOW-MODERATE. Article 4 TDM exception likely applies if publishers have not opted out. Must verify no opt-out exists (robots.txt, ToS, metadata). If no opt-out, this is legally permitted. |
| **Italian Law** | LOW-MODERATE. Art. 70-septies/70-quater mirrors EU framework. Criminal penalties for violations elevate the consequences of getting it wrong. Must implement robust opt-out checking. |

**Overall risk**: MODERATE. Defensible in the EU/Italy under TDM exceptions if opt-out compliance is rigorous. Higher risk in the US without clear fair use protection.

### Scenario B: Users uploading their own purchased/downloaded PDFs for personal RAG use

| Factor | Risk Assessment |
|---|---|
| **US Law** | LOW-MODERATE. Strongest fair use argument: personal/educational purpose, user already has lawful copy, transformative use for personal Q&A. But the platform operator (MeepleAI) is still making copies. |
| **EU Law** | LOW. User has lawful access. Processing for personal use. Platform acts as a tool/service. Art. 4 TDM exception applies. Private copying exceptions (Art. 5(2)(b) InfoSoc Directive) may also apply. |
| **Italian Law** | LOW. Art. 68 (personal reproduction) + Art. 70-septies coverage. User-initiated, personal purpose, lawful access. |

**Overall risk**: LOW. This is the safest scenario. The user has lawful access, the purpose is personal, and the platform acts as a processing tool rather than a content distributor.

### Scenario C: Caching/storing chunks of rulebook text for retrieval

| Factor | Risk Assessment |
|---|---|
| **US Law** | MODERATE-HIGH. Literal copying into a database is reproduction. Duration of storage matters -- temporary/cache copies may be treated differently than permanent storage. Amount stored (full work vs. selective chunks) matters. |
| **EU Law** | LOW-MODERATE. TDM exception explicitly permits "acts of reproduction and extraction." Storage of extracted data is covered as necessary for TDM activities, subject to opt-out compliance. Art. 4 requires lawful access. |
| **Italian Law** | LOW-MODERATE. Art. 70-septies covers "reproduction and extraction." Same framework as EU. Criminal penalties for violations. |

**Overall risk**: MODERATE. Storage of text chunks is the most legally exposed technical component. EU/Italy provide clearer statutory authorization than the US.

---

## 8. Risk Mitigation Strategies

### 8.1 Technical Mitigations

1. **User-upload model preferred**: Design the primary flow around users uploading their own PDFs rather than centrally downloading publisher PDFs. This shifts the copyright analysis toward personal use.

2. **Opt-out compliance system**: Before processing any publisher PDF, check:
   - robots.txt for TDM/AI-related directives
   - Publisher terms of service for TDM reservations
   - File metadata for TDM opt-out tags
   - Maintain a blocklist of publishers who have opted out

3. **Minimize stored text**: Store the smallest chunks necessary for quality retrieval. Consider whether embedding-only retrieval is technically feasible for any use cases.

4. **Output controls**: Implement guardrails preventing the system from reproducing large verbatim passages from rulebooks. Paraphrase and synthesize rather than quote.

5. **Attribution**: Always cite the source rulebook and publisher in RAG outputs. Include links to the original PDF where available.

6. **Ephemeral processing**: For user-uploaded PDFs, consider processing and generating embeddings in-session without permanent storage of text chunks. Delete source text after embedding generation.

### 8.2 Legal/Business Mitigations

1. **Terms of Service**: Clearly state that users are responsible for having lawful access to uploaded PDFs. Include appropriate DMCA/notice-and-takedown procedures.

2. **Publisher partnerships**: Proactively reach out to major board game publishers to obtain explicit permission or licensing agreements for rulebook processing. Frame it as a value-add that drives game engagement.

3. **Licensing framework**: Consider implementing a licensing system where publishers can opt-in to have their rulebooks included, potentially with revenue sharing or promotional benefits.

4. **Geographic compliance**: Implement jurisdiction-aware processing. EU/Italy TDM compliance may differ from US requirements.

5. **Documentation**: Maintain records of:
   - When and how each PDF was obtained
   - Publisher opt-out status at time of processing
   - User consent and upload records
   - Processing logs for compliance auditing

### 8.3 Architecture Recommendations

*(blocco di codice rimosso)*

---

## 9. Conclusions and Recommendations

### 9.1 Summary of Legal Position

1. **Game mechanics are free to use** -- no copyright protection in any jurisdiction. A RAG system can describe how to play any game without copyright concern, provided it uses its own words.

2. **Rulebook text is copyrighted** -- the specific expression in a publisher's rulebook is protected. Copying it into a RAG database is reproduction. Retrieving and displaying it is potentially creating derivative works.

3. **EU/Italy offer clearer legal footing** than the US -- the TDM exceptions (Art. 4 DSM Directive / Art. 70-septies L. 633/1941) provide explicit statutory permission for commercial TDM, subject to opt-out compliance. The US relies on uncertain fair use analysis, and the Copyright Office has expressed skepticism about RAG's transformative nature.

4. **User-upload model is safest** across all jurisdictions -- personal use, lawful access, and user-initiated processing provide the strongest legal position.

5. **Opt-out compliance is mandatory in the EU/Italy** -- and carries criminal penalties in Italy for violations.

### 9.2 Specific Recommendations for MeepleAI

| Priority | Recommendation |
|---|---|
| **CRITICAL** | Implement user-upload as the primary PDF ingestion path |
| **CRITICAL** | Build opt-out checking system before any centralized PDF processing |
| **CRITICAL** | Implement output guardrails to prevent verbatim reproduction |
| **HIGH** | Add attribution and source links to all RAG responses |
| **HIGH** | Include DMCA/takedown mechanism in platform ToS |
| **HIGH** | Scope text chunk storage to individual users (not shared) |
| **MEDIUM** | Explore publisher partnership/licensing program |
| **MEDIUM** | Implement ephemeral processing option (delete source text after embedding) |
| **MEDIUM** | Document compliance procedures for audit trail |
| **LOW** | Monitor Dow Jones v. Perplexity AI outcome for US precedent |
| **LOW** | Track EU TDM opt-out standardization developments |

### 9.3 Confidence Assessment

| Area | Confidence | Basis |
|---|---|---|
| Game mechanics not copyrightable | 95% | Settled law across all jurisdictions |
| Rulebook text is copyrightable | 95% | Standard copyright doctrine |
| EU TDM exception covers RAG | 85% | Statutory text + AI Act cross-reference, but limited case law |
| Italian criminal penalties apply | 90% | Explicit statutory text (Law No. 132/2025) |
| US fair use is uncertain for RAG | 90% | Copyright Office Part 3 + pending litigation |
| User-upload model is lowest risk | 85% | Consistent across jurisdictions but not tested in court |

---

## 10. Sources

### Case Law
- [Baker v. Selden, 101 U.S. 99 (1879)](https://en.wikipedia.org/wiki/Baker_v._Selden)
- [Lotus Development Corp. v. Borland International, Inc., 49 F.3d 807 (1st Cir. 1995)](https://www.bitlaw.com/source/cases/copyright/Lotus.html)
- [Dow Jones & Co. v. Perplexity AI, Inc. (S.D.N.Y. 2024)](https://law.justia.com/cases/federal/district-courts/new-york/nysdce/1:2024cv07984/630270/65/)

### US Copyright Office Reports
- [Copyright and Artificial Intelligence -- Official Page](https://www.copyright.gov/ai/)
- [Part 3: Generative AI Training (Pre-Publication, May 2025)](https://www.copyright.gov/ai/Copyright-and-Artificial-Intelligence-Part-3-Generative-AI-Training-Report-Pre-Publication-Version.pdf)
- [US Copyright Office AI Study -- All Parts](https://www.copyright.gov/policy/artificial-intelligence/)

### US Copyright Office Analysis (Law Firm Summaries)
- [Mayer Brown: Copyright Office Weighs in on Fair Use for Generative AI Training](https://www.mayerbrown.com/en/insights/publications/2025/05/united-states-copyright-office-weighs-in-on-fair-use-defense-for-generative-ai-training)
- [Sidley Austin: Generative AI Meets Copyright Scrutiny (Part III Highlights)](https://www.sidley.com/en/insights/newsupdates/2025/05/generative-ai-meets-copyright-scrutiny)
- [Skadden: Copyright Office Report on AI Training and Fair Use](https://www.skadden.com/insights/publications/2025/05/copyright-office-report)
- [McDermott: US Copyright Office Report on Copyrighted Material in AI Training](https://www.mwe.com/insights/us-copyright-office-issues-report-addressing-use-of-copyrighted-material-to-train-generative-ai-systems/)
- [Authors Guild: What Authors Should Know About Part 3](https://authorsguild.org/news/us-copyright-office-ai-report-part-3-what-authors-should-know/)

### EU Copyright Directive
- [Text and Data Mining: Articles 3 and 4 (Geiger, Frosio, Bulayenko -- SSRN)](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=3470653)
- [Knowledge Rights 21: Why EU TDM Exceptions Apply to AI](https://knowledgerights21.org/news-story/eu-tdm-exceptions-can-be-used-for-ai/)
- [Kluwer Copyright Blog: The New Copyright Directive TDM Articles](https://legalblogs.wolterskluwer.com/copyright-blog/the-new-copyright-directive-text-and-data-mining-articles-3-and-4/)
- [Oxford Academic: TDM Opt-Out in Article 4(3) CDSMD](https://academic.oup.com/jiplp/article/19/5/453/7614898)
- [Kluwer Copyright Blog: TDM Opt-Out -- Five Problems, One Solution](https://legalblogs.wolterskluwer.com/copyright-blog/the-tdm-opt-out-in-the-eu-five-problems-one-solution/)
- [Orrick: First Significant EU Decision on Data Mining for AI Training](https://www.orrick.com/en/Insights/2024/10/Significant-EU-Decision-Concerning-Data-Mining-and-Dataset-Creation-to-Train-AI)
- [TechnoLlama: EU TDM Exception and AI Training](https://www.technollama.co.uk/we-need-to-talk-about-the-eu-tdm-exception-and-ai-training)
- [EU Parliament: AI and Copyright -- Training of GPAI (2025)](https://www.europarl.europa.eu/RegData/etudes/ATAG/2025/769585/EPRS_ATA(2025)769585_EN.pdf)

### Italian Copyright Law
- [Cleary Gottlieb: Italy Adopts First National AI Law](https://www.clearygottlieb.com/news-and-insights/publication-listing/italy-adopts-the-first-national-ai-law-in-europe-complementing-the-eu-ai-act)
- [Hogan Lovells: Copyright Provisions in the New Italian AI Law](https://www.hoganlovells.com/en/publications/copyright-provisions-in-the-new-italian-ailaw-reinforcing-human-authorship-and-text-and-data-mining)
- [Trademark Lawyer Magazine: Italy's New Copyright Rules in AI Law](https://trademarklawyermagazine.com/italys-new-copyright-rules-in-the-first-national-ai-law-by-an-eu-member-state/)
- [Kluwer Copyright Blog: New Italian Law on AI -- A General Framework](https://legalblogs.wolterskluwer.com/copyright-blog/new-italian-law-on-ai-a-general-framework/)
- [Communia: Italy Updates Copyright Law for AI](https://communia-association.org/2025/10/01/italy-updates-its-copyright-law-to-address-ai/)
- [Norton Rose Fulbright: Italy Enacts Law No. 132/2025](https://www.nortonrosefulbright.com/en/knowledge/publications/9bfedfea/italy-enacts-law-no-132-2025-on-artificial-intelligence-sector-rules-and-next-steps)
- [WIPO Lex: Law No. 633/1941 (consolidated text)](https://www.wipo.int/wipolex/en/legislation/details/21564)

### Board Game Copyright
- [ABA: Not Playing Around -- Board Games and IP Law](https://www.americanbar.org/groups/intellectual_property_law/resources/landslide/archive/not-playing-around-board-games-intellectual-property-law/)
- [ABA: Why Videogame Rules Are Not Expression Protected by Copyright](https://www.americanbar.org/groups/intellectual_property_law/resources/landslide/archive/why-videogame-rules-are-not-expression-protected-copyright-law/)
- [Legal Moves Law Firm: Are Board Games Copyrighted? (2025)](https://legalmoveslawfirm.com/board-games-copyrighted/)
- [Meeple Mountain: Board Game Designer's Guide to IP Law](https://www.meeplemountain.com/articles/the-board-game-designers-guide-to-intellectual-property-law/)
- [Vanderbilt Journal of Entertainment & Technology Law: Patenting Games -- Baker v. Selden Revisited](https://scholarship.law.vanderbilt.edu/cgi/viewcontent.cgi?article=1337&context=jetlaw)

### RAG and AI Copyright
- [36kr: New Copyright Concerns in RAG -- What You Need to Know](https://eu.36kr.com/en/p/3422429684387205)
- [Asia IP Law: The Latest Rage Called RAG](https://www.asiaiplaw.com/section/in-depth/the-latest-rage-called-rag)
- [Norton Rose Fulbright: Practical Commentary on Copyright and Generative AI Training](https://www.nortonrosefulbright.com/en/knowledge/publications/87200379/practical-commentary-regarding-copyright-and-generative-ai-training)
- [Arxiv: Incorporating Legal Structure in RAG for Copyright Fair Use](https://arxiv.org/abs/2505.02164)
- [News/Media Alliance: Copyright Office AI Report Press Release](https://www.newsmediaalliance.org/copyright-office-ai-report-press-release/)
- [Loeb & Loeb: Dow Jones v. Perplexity AI Analysis](https://www.loeb.com/en/insights/publications/2025/08/dow-jones-and-company-inc-v-perplexity-ai-inc)
- [Copyright Alliance: AI Copyright Lawsuit Developments in 2025](https://copyrightalliance.org/ai-copyright-lawsuit-developments-2025/)
- [ArentFox Schiff: News Corp Continues Battle Against Perplexity AI](https://www.afslaw.com/perspectives/ai-law-blog/generative-ai-meets-generative-litigation-news-corp-continues-its-battle)

### Embeddings and Vector Storage
- [Kluwer Copyright Blog: Memorisation in Generative Models and EU Copyright Law](https://legalblogs.wolterskluwer.com/copyright-blog/memorisation-in-generative-models-and-eu-copyright-law-an-interdisciplinary-view/)
- [IPWatchdog: The Licensing Vector -- A Fair Approach to Content Use in LLMs](https://ipwatchdog.com/2024/04/10/licensing-vector-fair-approach-content-use-llms-2/id=175202/)
- [EFF: Don't Make Embedding Illegal (March 2026)](https://www.eff.org/deeplinks/2026/03/eff-court-dont-make-embedding-illegal)

---

*Research conducted: 2026-03-07*
*Methodology: Multi-source web research using legal databases, law firm publications, government reports, and academic papers. All findings cross-referenced across multiple sources.*


---



<div style="page-break-before: always;"></div>

## research/board-game-pdf-copyright-analysis.md

# Board Game PDF Copyright Analysis for MeepleAI RAG System

## Expert Spec Panel Review

**Panel**: Karl Wiegers (Requirements), Martin Fowler (Architecture), Michael Nygard (Operations/Risk), Lisa Crispin (Quality), + Legal Domain Experts
**Mode**: Discussion + Critique
**Focus**: Compliance, Architecture, Risk
**Date**: 2026-03-07

---

## Executive Summary

MeepleAI's use of board game rulebook PDFs in its RAG system raises significant copyright questions. The legal landscape is **actively evolving** with major cases (NYT v. OpenAI, Cohere v. Publishers, Thomson Reuters v. Ross) shaping precedent in 2025-2026. The analysis identifies **5 legal solutions** (not just 2):

### The 5 Legal Solutions

| # | Solution | Risk | Scalability | Cost |
|---|----------|------|------------|------|
| 1 | Publisher licensing (explicit consent) | **ZERO** | Medium (depends on deals) | High |
| 2 | User uploads own PDF (with warranty) | **LOW** | High (self-service) | Low |
| 3 | **Original rewrite of game mechanics** | **VERY LOW** | Medium (requires work) | Medium |
| 4 | CC / Open License content | **ZERO** | Low (few titles) | Low |
| 5 | Public domain games | **ZERO** | Low (classic games) | Low |

### Discarded Options (Too Risky)

| Option | Risk | Why |
|--------|------|-----|
| Platform scrapes publisher PDFs without consent | **HIGH** (criminal in Italy) | Art. 171(1)(a-ter) criminal penalties |
| Use BGG-hosted PDFs | **HIGH** (TOS violation) | BGG explicitly prohibits AI/LLM usage |
| EU Art. 4 TDM (no publisher opt-out) | **MEDIUM** (uncertain) | Must monitor each publisher; criminal risk |

**Critical finding**: Italy's new AI law (Law 132/2025, effective Oct 2025) adds **criminal penalties** for TDM violations where opt-out rights have been exercised, making compliance essential for an Italy-based service.

### Key Strategic Insight: Solution 3

**Game mechanics are NOT copyrightable** — only the specific textual expression is (Baker v. Selden, 1879). MeepleAI can legally create its own original descriptions of how games work:

*(blocco di codice rimosso)*

Same mechanics, original expression = **no copyright violation**. This creates a **proprietary knowledge base owned by MeepleAI** with zero dependency on publisher consent.

---

## Critical Question: Can We Download PDFs, Rewrite, and Feed to AI?

Three variants with very different legal profiles:

| Variant | Process | TDM? | Risk |
|---------|---------|------|------|
| **A: Fully automated** (AI reads PDF and rewrites) | Download PDF → LLM processes copyrighted text → Generates rewrite | **YES** (Art. 70-septies applies) | **MEDIUM-HIGH** — Even if output is original, input-side is regulated TDM |
| **B: Human reads and rewrites** (Solution 3) | Human plays game → Reads rulebook → Writes original explanation | **NO** (No AI processes copyrighted text) | **VERY LOW** — Same as Wikipedia describing game rules |
| **C: Human + AI assistant** (hybrid) | Human reads + takes notes → AI helps draft from human notes → Human reviews | **NO** (AI reads human notes, not PDF) | **LOW** — AI never sees the copyrighted text directly |

**Key distinction**: Italian law (Art. 70-septies) regulates "reproductions and extractions through AI models and systems." Variant A triggers this because the AI directly processes the copyrighted PDF. Variants B and C do not, because the AI only processes human-authored notes. **The legal boundary is: does the AI ever read the copyrighted text? If yes = TDM. If no = safe.**

> **NYGARD**: "Variant A is a trap. It looks efficient but creates the same legal exposure as direct RAG ingestion. The whole point of Solution 3 is that a HUMAN reads the rulebook and writes original content. If you automate that with AI reading the PDF, you lose the legal protection."

---

## Dual-Mode Architecture: Viewer vs. AI Knowledge Base

The platform must maintain a **strict separation** between PDF viewing (display only) and AI-powered Q&A (from the proprietary knowledge base).

### PDF Usage Rules

| PDF Usage Scenario | Legal? | Notes |
|-------------------|--------|-------|
| User views their own uploaded PDF | **YES** | Like any cloud storage (Google Drive, Dropbox) |
| Link to publisher's PDF on their site | **YES** | Hyperlink is not copying. "Rules at catan.com" |
| MeepleAI downloads and re-distributes publisher PDF to all users | **NO** | Unauthorized distribution = copyright infringement |
| Embed publisher PDF in iframe | **GRAY** | Depends on jurisdiction. Better to link. |
| User views PDF alongside AI answers (from proprietary KB) | **YES** | User sees their file + AI answers from separate KB |

### Architecture Overview

*(blocco di codice rimosso)*

**The separation is the legal firewall.** The PDF viewer is a passive display tool (like Google Drive). The AI knowledge base uses only legally sourced content. The AI never reads PDFs from the viewer.

### Example User Workflow: Playing Catan

| Step | Without Upload | With Upload |
|------|---------------|-------------|
| User asks: "How do I trade?" | AI answers from proprietary rewrite (Tier 1B). Link: "See official rules at catan.com" | AI answers from rewrite + user's PDF chunks. User can view PDF in viewer. |
| User asks: "What about the robber?" | AI answers from MeepleAI's original explanation. No copyrighted text used. | AI combines proprietary rewrite + user's uploaded content for more precise answer. |
| Publisher partnership (future) | AI adds official FAQ, errata, designer notes from Catan Studio. | All three sources: rewrite + user PDF + official. |

---

## 1. Copyright Fundamentals: Game Rules vs. Rulebooks

### The Idea/Expression Dichotomy

**Game mechanics (ideas)** are NOT copyrightable:
- The rules of Catan (place settlements, trade resources) cannot be owned via copyright
- US precedent: *Baker v. Selden* (1879) — methods and systems are not protectable
- *Lotus v. Borland* (1995) — functional elements (menu hierarchies) not copyrightable

**Rulebook text (expression)** IS copyrightable:
- The specific words, illustrations, layout, examples, flavor text in a rulebook are protected
- The creative choices in HOW rules are explained constitute original expression
- Art, diagrams, graphic design, narrative elements — all protected

> **WIEGERS**: "This distinction is critical for requirements. The system must be designed around what it CAN legally use (game mechanics, factual information) vs. what carries copyright risk (specific textual expression). Every requirement must specify whether it operates on ideas or expression."

### What This Means for RAG

A RAG system that stores and retrieves **chunks of rulebook text** is reproducing copyrighted expression. This is fundamentally different from a system that merely "knows" game rules.

---

## 2. Legal Framework Analysis

### 2.1 US Copyright Law — Fair Use (17 U.S.C. Section 107)

The four-factor test applied to MeepleAI's RAG use:

| Factor | Assessment | Risk |
|--------|-----------|------|
| **1. Purpose & Character** | Commercial service; arguably transformative (Q&A vs. reading rulebook) | MEDIUM |
| **2. Nature of Work** | Rulebooks are factual/instructional (favors fair use) but contain creative expression | MEDIUM-LOW |
| **3. Amount Used** | RAG chunks = substantial portions of the work | HIGH |
| **4. Market Effect** | Could substitute for purchasing/reading the rulebook | HIGH |

**Key US Cases (2025-2026)**:

- **Thomson Reuters v. Ross Intelligence** (D. Del., Feb 2025): Court REJECTED fair use for AI system trained on Westlaw headnotes. First ruling against AI fair use defense. Ross copied 2,243 headnotes — held not transformative. **Directly relevant** to MeepleAI storing rulebook chunks.

- **Advanced Local Media v. Cohere** (S.D.N.Y., Nov 2025): Court denied dismissal of RAG-specific copyright claims. "Substitutive summaries" — non-verbatim outputs that mirror expressive structure — may infringe. **RAG technology specifically scrutinized**.

- **NYT v. OpenAI** (S.D.N.Y., ongoing): Main copyright claims proceeding. OpenAI ordered to produce 20M ChatGPT logs. Implications for any system that retrieves/reproduces copyrighted text.

- **Bartz v. Anthropic** (June 2025): First ruling finding AI training IS fair use — but ONLY for legally acquired works. Pirated content is NOT protected. Settled Aug 2025. Favorable for lawfully accessed content.

- **Perplexity AI lawsuits**: WSJ and NY Post sued Perplexity (RAG-based search). Allegations that RAG retrieval of news articles constitutes infringement.

- **US Copyright Office Part 3 Report** (May 2025): Explicitly states RAG "is less likely to be transformative" than training. Both copying into retrieval DB and outputting content are "potential copyright infringements which do not qualify as fair use." Critical guidance against unlicensed RAG.

> **NYGARD**: "The Thomson Reuters and Cohere rulings are red flags. Courts are NOT treating RAG as automatically fair use. The market substitution argument is strong — if MeepleAI answers rulebook questions, users may not need to read the actual rulebook, directly impacting the publisher's market."

### 2.2 EU Copyright Directive 2019/790 — TDM Exceptions

The EU framework is MORE structured than US fair use:

**Article 3 — Research Exception**:
- TDM permitted for research organizations and cultural heritage institutions
- For scientific research purposes only
- Requires lawful access to the works
- **MeepleAI cannot use this**: It's a commercial service, not a research institution

**Article 4 — General TDM Exception**:
- TDM permitted for ANY purpose (including commercial)
- **UNLESS** rights holders have "expressly reserved their rights in an appropriate manner"
- For online content: opt-out must be in "machine-readable means" (robots.txt, meta tags, TDM headers)
- **Hamburg Court (Dec 2025)**: Ruled that non-machine-readable opt-outs are INVALID

**Implications for MeepleAI**:
- If publisher PDFs are hosted online WITHOUT machine-readable opt-out → Article 4 MAY permit TDM
- If publisher has robots.txt/TDM reservation → CANNOT use under Article 4
- Must check each publisher's opt-out status individually
- "Lawful access" requirement: must access content legally (not through scraping behind paywalls)

**EU AI Act (Article 53, effective Aug 2025)**:
- AI providers must comply with EU copyright law
- Must "identify and respect" opt-out reservations under Article 4(3)
- Code of Best Practices published July 2025

### 2.3 Italian Copyright Law (Legge 633/1941 + Law 132/2025)

**CRITICAL for MeepleAI** (Italian-based service):

Italy adopted its national AI law (Law 132/2025) effective **October 10, 2025**, which AMENDS the Copyright Law:

**Article 70-ter**: TDM for research organizations — scientific research only, no opt-out needed
**Article 70-quater**: TDM for any purpose — BUT rights holders can opt-out ("expressly reserve rights")
**Article 70-septies** (NEW): Explicitly extends TDM rules to AI systems including generative ones:
> "Reproductions and extractions from works or materials contained in networks or databases to which lawful access is permitted, for the purposes of text and data mining using AI models and systems (including generative ones), are permitted in accordance with Articles 70-ter and 70-quater."

**Article 171(1)(a-ter)** (NEW): **CRIMINAL PENALTIES** for TDM violations through AI systems — unauthorized scraping and abusive data mining where opt-out rights have been exercised.

> **FOWLER**: "The Italian law is the most specific framework we're dealing with. Article 70-septies explicitly covers AI systems doing TDM. The criminal penalty provision (171(1)(a-ter)) elevates this from a civil liability concern to a criminal one. Architecture decisions must treat this as a hard constraint, not a soft preference."

**Summary of Italian framework**:
| Use Case | Permitted? | Condition |
|----------|-----------|-----------|
| TDM for scientific research | Yes | Must be research org |
| TDM for commercial AI (no opt-out) | Yes | Lawful access required |
| TDM for commercial AI (opt-out present) | **NO** | Criminal penalties apply |
| User processes their own purchased PDF | Likely yes | Personal use + lawful access |

---

## 3. Publisher Landscape Analysis

### 3.1 How Publishers Distribute Rulebook PDFs

| Publisher | Free PDF Available? | Where? | Notable Policy |
|-----------|-------------------|--------|----------------|
| **Catan Studio** | Yes | catan.com/game-rules | Free download, "for those who misplaced them" |
| **Fantasy Flight Games** (Asmodee) | Yes | images-cdn.fantasyflightgames.com | CDN-hosted, freely accessible |
| **Days of Wonder** (Asmodee) | Yes | Via BGG file uploads | Publisher-uploaded |
| **Stonemaier Games** | Yes | stonemaiergames.com | Known for openness |
| **Czech Games Edition** | Yes | Via BGG | Also uses AI validators internally |
| **Renegade Game Studios** | Yes | Via BGG | Uses internal AI on own errata |
| **CMON** | Mixed | Some via BGG | Varies by title |
| **Hasbro/WotC** | Selective | hasbro.com, OGL for D&D | D&D SRD 5.1 under CC-BY-4.0 (only major open-licensed content) |
| **Ravensburger** | Limited | Some titles only | Forced removal of AI art from licensed product (2024). Restrictive. |
| **Games Workshop** | Limited | Some titles | Comprehensive AI ban across all design (Jan 2026) |
| **Leder Games** | Yes | ledergames.com/resources | Free downloads available |

**Key observation**: Most publishers make rulebook PDFs freely available, but "freely downloadable" does NOT mean "freely usable in AI systems." The license to download is typically for personal reading, not for commercial processing.

**Industry anti-AI stance (2024-2026)**:
- **Stonemaier Games** (Apr 2024): "does not, has not, and will not use any form of AI to replace or augment creative work"
- **Ravensburger** (Mar 2024): Forced removal of AI art from licensed Puerto Rico product
- **Asmodee**: No AI art policy across all productions
- **Games Workshop** (Jan 2026): Comprehensive ban on AI across all design processes
- **Fantasy Flight Games**: IP policy explicitly bans their IP in "software applications of any kind"
- **Hasbro/WotC**: Mixed signals (WotC anti-AI for published work; CEO admits internal use)

**NOTE**: While these policies primarily target AI-generated art, no publisher has explicitly addressed third-party AI text mining of rulebooks for RAG/chatbot purposes. The prohibition is inferred from general copyright + personal-use restrictions.

**Only open-licensed content**: D&D SRD 5.1 (CC-BY-4.0) and content under ORC License. No major commercial board game publisher releases rulebooks under Creative Commons.

### 3.2 BoardGameGeek (BGG) Policies

BGG is the largest repository of board game rulebook PDFs. Their TOS (updated) states:

- **Explicitly prohibits** using the website "to train or otherwise use as data for an AI or LLM system"
- **Prohibits** automated scraping beyond what a human can produce
- **Prohibits** commercial use without written authorization
- Rulebooks uploaded to BGG are often uploaded by publishers themselves or with tacit consent

> **CRISPIN**: "BGG's explicit AI prohibition is a hard blocker. Any approach that involves scraping BGG for rulebook PDFs is clearly against their TOS. Even if PDFs were originally uploaded by publishers, BGG's TOS governs access through their platform."

### 3.3 Existing Competitors and Their Approaches

Several AI rulebook assistants already exist, with varying copyright approaches:

| Product | Approach | Copyright Strategy |
|---------|----------|-------------------|
| **RulesBot.ai** | Pre-indexed rulebooks | Unclear licensing model |
| **Ludomentor** (Awaken Realms) | Publisher's own games | First-party content (no copyright issue) |
| **Board Game Assistant** | Publisher partnerships | Licensed content |
| **Boardside** | "Official rulebooks only" | Claims to use only official sources |
| **Rulebook.gg** | AI drafting tool | Different use case (creation, not Q&A) |

> **FOWLER**: "The market is validating the use case, but the successful approaches either (a) use first-party content (Ludomentor/Awaken Realms doing their own games) or (b) establish publisher partnerships (Board Game Assistant). The 'just index everything' approach has obvious legal exposure."

---

## 4. The 5 Legal Solutions — Detailed Analysis

### Solution 1: Publisher Licensing (ZERO RISK)

Formal agreements with publishers granting MeepleAI the right to ingest and serve their rulebook content.

| Aspect | Details |
|--------|---------|
| **Legal basis** | Contractual license — explicit consent eliminates all copyright risk |
| **Model** | Revenue share, value exchange (analytics on rule confusion), or free partnership |
| **Precedent** | Board Game Assistant uses publisher partnerships; Ludomentor (Awaken Realms) uses first-party content |
| **Scalability** | Limited by business development speed; high value for top 50-100 games |

### Solution 2: User Uploads Own PDF (LOW RISK)

Users who own a board game upload their copy of the rulebook PDF. Per-user processing, per-user storage.

| Dimension | Analysis |
|-----------|----------|
| **US Fair Use** | Stronger. Personal/educational use, user already purchased the game, not redistributing. |
| **EU/Italian TDM** | User has lawful access (purchased game). Processing for personal use. |
| **Platform Liability** | DMCA Section 512(c) safe harbor may apply — user-directed storage. |
| **Analogy** | Google NotebookLM, Notion AI, ChatGPT file upload — widely accepted model. |
| **Market Effect** | Minimal — user already bought the game. RAG enhances their experience. |
| **Thin Copyright** | Rulebooks are factual/instructional — "thin copyright" with weaker protection. |

> **WIEGERS**: "Scenario B has well-established analogies: personal document AI assistants. Key requirements: (1) user uploads, (2) per-user isolation, (3) no cross-user sharing, (4) user warranty of rights, (5) DMCA compliance."

### Solution 3: Original Rewrite of Game Mechanics (VERY LOW RISK) ← KEY INSIGHT

**Game mechanics are NOT copyrightable** — only the specific textual expression is. MeepleAI can legally create its own original descriptions of how games work, using completely different words from the publisher's rulebook.

| Aspect | Details |
|--------|---------|
| **Legal basis** | Baker v. Selden (1879): methods/systems not copyrightable. Copyright protects expression, not ideas. |
| **Implementation** | Team (or AI + human review) writes original rule explanations. Describe mechanics without copying publisher text. |
| **Scalability** | Medium — requires editorial work per game. Prioritize top 50-100 popular games. |
| **Advantage** | Creates **PROPRIETARY knowledge base** owned by MeepleAI. No dependency on publisher consent or user uploads. |
| **Precedent** | Wikipedia describes game rules without copyright issues. BGG wiki pages summarize rules in original text. |
| **Risk** | Very low. Only risk: if rewrite is "substantially similar" to original (easily avoidable with review). |

> **FOWLER**: "This is the game-changer. Solution 3 creates a proprietary asset that MeepleAI owns. Combined with user uploads for the long tail and publisher deals for premium content, this gives you three independent content sources with zero legal dependency on any single one."

### Solution 4: Creative Commons / Open License (ZERO RISK)

| Content | License | Scope |
|---------|---------|-------|
| D&D SRD 5.1 | CC-BY-4.0 | Core D&D rules, monsters, spells — freely usable with attribution |
| ORC License games | ORC License | Pathfinder ecosystem, various RPG publishers |
| CC-licensed indie games | Various CC | Small but growing catalog |

### Solution 5: Public Domain Games (ZERO RISK)

Games whose copyright has expired (EU: 70 years after author death, US: pre-1929):
- Chess, Go, Backgammon, Checkers, Mancala and all classic abstract games
- Traditional card games (Poker, Bridge, Rummy, etc.)
- Classic party games with expired copyright

---

## 5. Expert Panel Synthesis

### 5.1 Convergent Insights (All Experts Agree)

1. **5 legal solutions exist** — NOT just 2 (licensing + user upload)
2. **Solution 3 (original rewrite) is the strategic differentiator** — creates proprietary IP
3. **BGG is off-limits** for any automated processing
4. **Italian criminal penalties** (Art. 171(1)(a-ter)) make compliance non-negotiable
5. **The legal landscape is actively hostile** to unlicensed commercial RAG (2025-2026 trend)
6. **A hybrid architecture combining multiple solutions** is stronger than any single approach

### 5.2 The Hybrid Strategy

> **FOWLER**: "The hybrid model is the winning strategy: original rewrites for the top 50 games (proprietary KB), publisher partnerships for premium content with errata/FAQ, user uploads for the long tail of 50,000+ games. Three independent content sources with zero legal dependency on any single one."

> **NYGARD**: "Solution 3 eliminates the single point of failure. If a publisher partnership falls through, or if a court rules against user-upload models, you still have your proprietary rewrites. That operational resilience is worth the editorial investment."

> **CRISPIN**: "The original rewrite approach also improves quality. Publisher rulebooks are often poorly written. MeepleAI could become known for BETTER rule explanations than the originals — that is a competitive advantage, not just a legal workaround."

### 5.3 Key Strategic Insight: Mechanics Are Free

The fundamental legal principle that unlocks MeepleAI's strategy: **game mechanics are ideas, and ideas cannot be copyrighted**. Only the specific words used to describe them are protected. This means MeepleAI can build a comprehensive knowledge base of how every game works, written in its own original language, without any copyright concern.

---

## 6. Recommended Architecture for MeepleAI

### Tier 1A: User-Uploaded Content (Launch MVP)

*(blocco di codice rimosso)*

**Requirements**:
- [ ] User TOS: warranty of ownership/rights, indemnification clause
- [ ] Per-user content isolation (no cross-pollination between users)
- [ ] DMCA takedown procedure and designated agent
- [ ] Content retention policy (auto-delete after N days of inactivity)
- [ ] No caching of full text — store only embeddings + minimal chunks
- [ ] User can delete their content at any time

### Tier 1B: Original Mechanics Rewrite (Launch MVP — Top 50 Games)

*(blocco di codice rimosso)*

**Requirements**:
- [ ] Proprietary content owned by MeepleAI (no copyright dependency)
- [ ] Editorial process: play game → write rules in original language → peer review
- [ ] AI-assisted drafting with human review to ensure originality
- [ ] Similarity check against publisher text (must be substantially different)
- [ ] Priority: BGG Top 50 games + trending new releases
- [ ] Multi-language support (IT, EN, DE, FR, ES) for each game
- [ ] Community contribution model: users can suggest corrections/improvements

### Tier 2: Licensed Publisher Content (Phase 2 — 3-6 months)

*(blocco di codice rimosso)*

**Requirements**:
- [ ] Formal licensing agreements with publishers
- [ ] Publisher content management dashboard
- [ ] Revenue sharing or value exchange model
- [ ] Attribution and source citation in responses
- [ ] Publisher analytics (most asked questions, rule confusion areas)
- [ ] Value proposition: MeepleAI improves game accessibility → more sales

### Tier 3: Open + Public Domain Content (Parallel Track)

*(blocco di codice rimosso)*

**Requirements**:
- [ ] D&D SRD 5.1 (CC-BY-4.0), ORC License content, CC-licensed indie games
- [ ] Public domain: Chess, Go, Backgammon, classic card games
- [ ] Community contribution model with license verification
- [ ] Good for onboarding and demonstrating platform value

---

## 7. TOS and Legal Safeguards

### Required TOS Clauses

1. **User Warranty**: "You represent and warrant that you have all necessary rights, licenses, and permissions to upload content to MeepleAI, including copyright ownership or a valid license."

2. **Indemnification**: "You agree to indemnify and hold harmless MeepleAI from any claims arising from your uploaded content, including copyright infringement claims."

3. **DMCA Compliance**: "MeepleAI respects intellectual property rights. If you believe content on our platform infringes your copyright, please contact our designated DMCA agent at [email]."

4. **No Redistribution**: "Content you upload is processed solely for your personal use. MeepleAI does not share, redistribute, or make your uploaded content available to other users."

5. **Data Retention**: "Uploaded content and derived data (embeddings, chunks) are retained for [N] days and automatically deleted upon account closure or inactivity."

6. **AI Disclaimer**: "MeepleAI provides AI-assisted responses based on uploaded content. Responses may contain errors. Always refer to the official rulebook for definitive rules."

### Technical Safeguards

1. **Per-user vector namespaces** in Qdrant — strict tenant isolation
2. **No full-text storage** — only embeddings + minimal context chunks
3. **Chunk size limits** — small enough to be non-substitutive (e.g., 200-500 tokens)
4. **Response attribution** — always cite "Based on your uploaded rulebook, page X"
5. **Rate limiting** on uploads — prevent bulk processing
6. **File type validation** — accept only PDF, reject bulk archives
7. **robots.txt compliance** — if crawling publisher sites (Tier 2 only, with agreement)

---

## 8. Risk Matrix

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Publisher DMCA takedown (Tier 1) | Low (user content) | Medium | DMCA compliance, user warranty |
| Criminal liability Italy (Art. 171) | Very Low (user uploads) | Very High | No platform-side scraping without license |
| User uploads pirated content | Medium | Low-Medium | TOS warranty, DMCA process |
| Competitor with publisher deals outpaces us | High | Medium | Pursue publisher partnerships (Tier 2) |
| EU TDM opt-out landscape changes | Medium | Medium | Architecture supports per-publisher config |
| US court ruling against RAG fair use | Medium-High | High | User-upload model is more defensible |

---

## 9. Action Items

### Immediate (Pre-Launch)

1. **Legal**: Engage Italian IP attorney for compliance with Law 132/2025
2. **Architecture**: Implement per-user content isolation in Qdrant
3. **TOS**: Draft user warranty, indemnification, DMCA procedures
4. **Technical**: Minimize stored text (embeddings-first, minimal chunks)
5. **Editorial**: Begin original rewrite of top 10 game mechanics (Catan, Ticket to Ride, Carcassonne, Pandemic, Azul, 7 Wonders, Wingspan, Codenames, Splendor, Dixit)
6. **Content**: Ingest D&D SRD 5.1 (CC-BY-4.0) + public domain games as launch KB

### Short-Term (1-3 months post-launch)

7. **Editorial**: Expand original rewrites to top 50 games (BGG ranking)
8. **Business**: Approach 3-5 friendly publishers for pilot partnerships
9. **Community**: Launch community contribution model for rewrite suggestions
10. **Compliance**: Register DMCA designated agent with US Copyright Office
11. **QA**: Implement automated similarity checker (original vs publisher text)

### Medium-Term (3-12 months)

12. **Business model**: Develop publisher value proposition (rule confusion analytics, FAQ)
13. **Scale licensing**: Expand publisher partnerships based on pilot results
14. **Scale rewrites**: Top 200 games with community + AI-assisted editorial pipeline
15. **Multi-language**: Translate original rewrites to IT, EN, DE, FR, ES
16. **Legal audit**: Quarterly review of TDM opt-out landscape and case law

---

## Sources

### Legal Cases
- [Thomson Reuters v. Ross Intelligence (Feb 2025)](https://www.dwt.com/blogs/artificial-intelligence-law-advisor/2025/02/reuters-ross-court-ruling-ai-copyright-fair-use)
- [Advanced Local Media v. Cohere (Nov 2025)](https://copyrightlately.com/court-rules-ai-news-summaries-may-infringe-copyright/)
- [NYT v. OpenAI (ongoing)](https://www.npr.org/2025/03/26/nx-s1-5288157/new-york-times-openai-copyright-case-goes-forward)
- [Hamburg Court TDM Opt-Out Ruling (Dec 2025)](https://www.insidetechlaw.com/blog/2025/12/machine-readable-opt-outs-and-ai-training-hamburg-court-clarifies-copyright-exceptions)

### EU/Italian Law
- [Italy AI Law - Cleary Gottlieb](https://www.clearygottlieb.com/news-and-insights/publication-listing/italy-adopts-the-first-national-ai-law-in-europe-complementing-the-eu-ai-act)
- [Italian TDM Implementation - COMMUNIA](https://communia-association.org/2022/12/14/italian-implementation-of-the-new-eu-tdm-exceptions/)
- [EU AI Act Copyright Compliance - IAPP](https://iapp.org/news/a/the-eu-ai-copyright-playbook-the-tdm-exception-and-ai-act-s-transparency-requirements)
- [EU TDM Opt-Out Problems - Kluwer](https://legalblogs.wolterskluwer.com/copyright-blog/the-tdm-opt-out-in-the-eu-five-problems-one-solution/)

### Board Game Copyright
- [Board Games and Copyright - ABA Landslide](https://www.americanbar.org/groups/intellectual_property_law/resources/landslide/archive/not-playing-around-board-games-intellectual-property-law/)
- [Board Game IP Guide - Meeple Mountain](https://www.meeplemountain.com/articles/the-board-game-designers-guide-to-intellectual-property-law/)
- [Are Board Games Copyrighted - Legal Moves](https://legalmoveslawfirm.com/board-games-copyrighted/)

### RAG Copyright Analysis
- [RAG Copyright Concerns - 36kr](https://eu.36kr.com/en/p/3422429684387205)
- [RAG Copyright Analysis - Asia IP Law](https://www.asiaiplaw.com/section/in-depth/the-latest-rage-called-rag)
- [RAG as Copyright Frontier - LinkedIn](https://www.linkedin.com/pulse/rag-new-frontier-copyright-battle-genai-kurt-sutter-dqlfe)
- [Fair Use in RAG - arXiv](https://arxiv.org/html/2505.02164v1)

### Platform Policies
- [BGG Terms of Service](https://boardgamegeek.com/terms)
- [DMCA Section 512 Safe Harbors](https://www.copyright.gov/512/)
- [DMCA Safe Harbors for AI - Oxford Academic](https://academic.oup.com/jiplp/article/20/9/605/8221820)

### Existing Products
- [RulesBot.ai](https://www.rulesbot.ai/)
- [Ludomentor (Awaken Realms)](https://play.google.com/store/apps/details?id=com.awakenrealms.ludomentor)
- [Board Game Assistant](https://www.boardgameassistant.ai/)
- [Boardside](https://boardgamegeek.com/thread/3631492/boardside-ai-app-for-board-game-rules)


---



<div style="page-break-before: always;"></div>

## research/board-game-publisher-rulebook-pdf-policies.md

# Board Game Publisher Rulebook PDF Distribution & Third-Party Usage Policies

**Research Date**: 2026-03-07
**Confidence Level**: High (75-85%) for publisher PDF policies; Moderate (60-70%) for TOS specifics on AI/text mining
**Methodology**: Web search across publisher websites, BGG forums, industry news, legal analysis

---

## Executive Summary

Most major board game publishers freely distribute rulebook PDFs on their websites and/or BoardGameGeek (BGG). However, these are provided for **personal use only**, and no major publisher explicitly authorizes third-party AI/LLM training on their content. The board game industry has taken a strongly **anti-generative-AI** stance since 2024, with publishers like Stonemaier Games, Ravensburger, Asmodee, and Games Workshop issuing explicit bans on AI in their creative processes. BGG's Terms of Service **explicitly prohibit** using their platform to train AI/LLM systems. Open licensing (OGL, Creative Commons, ORC) exists primarily in the tabletop RPG space, not mainstream board games.

---

## 1. Publisher PDF Distribution Practices

### Publisher-by-Publisher Analysis

| Publisher | Free PDFs? | Where Hosted | Notes |
|-----------|-----------|--------------|-------|
| **Hasbro/WotC** | Yes (selective) | wizards.com, media.wizards.com | D&D Basic Rules free; SRD 5.1 under CC-BY-4.0 |
| **Asmodee Group** | Yes | fantasyflightgames.com (Product Document Archive) | FFG maintains extensive archive; Catan Studio hosts on catan.com |
| **Fantasy Flight Games** | Yes | images-cdn.fantasyflightgames.com | Comprehensive Product Document Archive with multilingual PDFs |
| **Catan Studio** | Yes | catan.com/understand-catan/game-rules | Official rulebooks freely downloadable |
| **Ravensburger** | Limited | Individual game pages | Less centralized than other publishers |
| **CMON** | Yes | cmon.com, cmon-files.s3.amazonaws.com | PDFs hosted on AWS S3; available for Zombicide, Marvel United, etc. |
| **Stonemaier Games** | Yes | stonemaiergames.com | Each game has rules page with PDFs, FAQs, quick-reference guides |
| **Czech Games Edition** | Yes | czechgames.com/en/downloads/ | Dedicated downloads page |
| **Devir** | Limited | devirgames.com, BGG | Less centralized; some via BGG community uploads |
| **Restoration Games** | Yes | restorationgames.com (direct PDF links) | PDFs hosted in wp-content directory |
| **Leder Games** | Yes | ledergames.com/pages/resources | Comprehensive resources page with rulebooks, errata, learn-to-play guides |
| **Rio Grande Games** | Limited | Primarily via BGG | Less direct hosting than other publishers |

### Key Observations

1. **Industry standard**: Providing free rulebook PDFs has become the norm. Publishers view it as customer service (replacing lost rulebooks) and marketing (preview before purchase).

2. **Distribution channels**: Publishers typically use their own CDN/website, plus allow uploads to BGG. Some rely on BGG as a secondary/primary distribution channel.

3. **Third-party aggregators**: Sites like cdn.1j1ju.com (1jour-1jeu.com) host many rulebook PDFs, though their authorization status varies.

4. **Emerging alternatives**: Rulepop (rulepop.com) offers interactive, always-updated rule references as an alternative to static PDFs. Stonemaier Games' Vantage uses this platform.

---

## 2. Terms of Service Analysis

### 2.1 Fantasy Flight Games / Asmodee — Community IP Policy

The most explicit policy comes from FFG/Asmodee's "Guidelines for Community Use of Our Intellectual Property":

**Allowed**:
- Fan-dedicated web pages
- Homebrew scenarios and special rulesets
- Custom accessories for personal use
- Artwork, fan fiction
- Must label as "unofficial" or "fan-made"
- Must include proper copyright notices

**Prohibited**:
- Selling fan creations using their IP
- Creating online versions of games (Tabletop Simulator, Vassal)
- Digitalized card game versions for download
- 3D print files of miniatures
- **Software applications of any kind** (for licensing/business reasons)

**Key restriction**: "FFG cannot allow their intellectual property in software applications of any kind for licensing and business reasons." This would likely encompass AI applications that ingest their rulebook content.

**Source**: [FFG IP Policy PDF](https://images-cdn.fantasyflightgames.com/filer_public/fa/b1/fab15a15-94a6-404c-ab86-6a3b0e77a7a0/ip_policy_031419_final_v21.pdf)

### 2.2 Stonemaier Games

- Rulebooks provided for **personal use only** (stated on rules pages).
- Explicit anti-AI statement: "Generative AI? Not for Us!" blog post (April 2024).
- Policy: "does not, has not, and will not use any form of AI to replace or augment creative work."
- No specific TOS language about text mining, but the personal-use restriction and anti-AI stance signal opposition.

**Source**: [Stonemaier AI Policy](https://stonemaiergames.com/generative-ai-not-for-us/)

### 2.3 Hasbro / Wizards of the Coast

- D&D content has the most permissive licensing via Creative Commons (SRD 5.1 under CC-BY-4.0).
- However, Hasbro's general content remains under standard copyright.
- WotC's AI policy for products: "artists, writers, and creatives contributing to D&D TTRPG must refrain from using AI generative tools to create final D&D products."
- Conflicting signals: Hasbro CEO stated the company has "already been using AI" internally, while WotC FAQ reaffirms anti-AI stance for published products.
- No specific TOS language found addressing third-party AI training on their rulebook PDFs.

### 2.4 Ravensburger

- Enforces anti-AI policy through licensing agreements (forced Awaken Realms to pull AI art from Puerto Rico 1897 campaign).
- Stated position: "generative AI cannot be used in any part of the art process."
- General Terms of Use do not specifically address AI/text mining in public-facing documents.

### 2.5 Asmodee Group

- Confirmed policy: "not to use AI art in any of its own productions."
- Terms of Service reference French LCEN law and EU Digital Services Act.
- No specific publicly available clause found regarding third-party AI training on downloaded content.
- General copyright protections apply to all content.

### 2.6 Games Workshop (Warhammer — included for industry context)

- January 2026: CEO Kevin Rountree announced comprehensive AI ban.
- "Does not allow AI-generated content or AI to be used in their design processes or its unauthorised use outside of GW."
- Explicitly bans AI use "including in any of their competitions."
- Investing in hiring more human creatives rather than AI.

---

## 3. BoardGameGeek (BGG) Policies

### 3.1 Terms of Service — AI Prohibition

BGG's Terms of Service contain an **explicit prohibition**:

> "You agree not to use the Geek Websites to train or otherwise use as data for an AI (Artificial Intelligence) or Large Language Model (LLM) system."

This is unambiguous and applies to all content on the platform, including uploaded rulebook PDFs.

**Source**: [BGG Terms of Service](https://boardgamegeek.com/terms)

### 3.2 File Upload & Copyright

- BGG's file upload policy states users "shall not upload User Submissions containing material that is copyrighted unless they are the owner of such rights or have permissions."
- In practice, publishers upload or authorize uploads of their rulebook PDFs to BGG.
- Community discussion indicates a gray area: some uploads are publisher-authorized, others are community-contributed.

**Source**: [BGG File Upload Discussion](https://boardgamegeek.com/thread/1349333/file-upload-copyright-bgg-terms-of-service)

### 3.3 XML API Terms

- Registration required for API access.
- **Scraping is prohibited**.
- Commercial use requires a separate commercial license from BGG.
- API provides game metadata but NOT file/PDF downloads.

**Source**: [BGG XML API Terms](https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use) | [BGG XML API Commercial Use](https://boardgamegeek.com/wiki/page/BGG_XML_API_Commercial_Use)

### 3.4 Community Rules on AI

BGG Community Rules include a section on "AI generated content," indicating platform-level governance of AI usage beyond just the TOS.

---

## 4. Open Gaming Licenses & Creative Commons

### 4.1 D&D System Reference Document (SRD 5.1) — CC-BY-4.0

The most significant open license in tabletop gaming:
- January 2023: WotC released SRD 5.1 under **irrevocable Creative Commons Attribution 4.0 (CC-BY-4.0)**.
- April 2025: SRD 5.2 also released under Creative Commons.
- Allows: sharing, adapting, commercial use — with attribution.
- This is specifically for the **rules mechanics** and system reference, NOT for full published books, artwork, or Product Identity.

### 4.2 Open Game License (OGL 1.0a)

- Original license from 2000, enables use of D&D-derived game mechanics.
- Distinguishes between "Open Game Content" (freely usable) and "Product Identity" (restricted).
- Hundreds of games published under OGL.
- After the 2023 controversy (WotC attempted OGL 1.1 revision), the original OGL 1.0a remains valid.

### 4.3 Open RPG Creative License (ORC)

- Developed by Paizo with 1,500+ publisher support, drafted by Azora Law.
- System-agnostic, perpetual, irrevocable open gaming license.
- Owned by a neutral legal entity (planned transfer to a nonprofit like Linux Foundation).
- Supported by: Paizo, Kobold Press, Chaosium, Green Ronin, Legendary Games, Rogue Genius Games.
- Final text submitted to Library of Congress.

**Source**: [ORC License](https://paizo.com/orclicense)

### 4.4 Creative Commons Board Games

- A GeekList on BGG catalogs [Creative Commons/Open Source Games](https://boardgamegeek.com/geeklist/33151/creative-commonsopen-source-games).
- Most CC-licensed games are indie/community projects (e.g., Sovereign under CC-BY-SA).
- FATE RPG system is under Creative Commons.
- **No major commercial board game publisher** (Asmodee, Ravensburger, CMON, etc.) releases their board game rulebooks under CC or open licenses.

### 4.5 Key Distinction: RPGs vs. Board Games

Open licensing is primarily an **RPG phenomenon**. Board games have fundamentally different IP structures:
- Board game **mechanics** are generally not copyrightable (per US law).
- Board game **rulebook text, artwork, and creative expression** ARE copyrighted.
- There is no board game equivalent of the OGL/ORC/CC-SRD ecosystem.

---

## 5. Industry Trends 2024-2026: AI and Board Game Content

### 5.1 Publisher Anti-AI Statements (Timeline)

| Date | Publisher | Action |
|------|-----------|--------|
| Mar 2024 | **Ravensburger** | Forced Awaken Realms to pull AI art from Puerto Rico 1897 |
| Apr 2024 | **Stonemaier Games** | Published "Generative AI? Not for Us!" blog post |
| 2024 | **Asmodee** | Confirmed no AI art policy for all productions |
| Summer 2024 | **Spiel Essen** | Stopped using AI images for marketing |
| Nov 2024 | **Wise Wizard Games** | Defended AI art use (Star Realms) — faced backlash |
| Jan 2026 | **Games Workshop** | CEO announced comprehensive AI ban across all processes |
| Ongoing | **WotC/Hasbro** | Contradictory: WotC anti-AI for published work; Hasbro CEO admits internal AI use |

### 5.2 AI Rulebook Assistant Apps

Several AI-powered rulebook assistants have emerged, raising legal and accuracy concerns:

- **RulesBot.ai**: AI chatbot for board game rules queries.
- **Boardside**: Claims to use ONLY official rulebooks, errata, and FAQs.
- **BoardGameAssistant.ai**: General AI board game assistant.
- **NotebookLM**: Google's tool being used by BGG community members for rulebook Q&A.

**Legal gray area**: These apps ingest copyrighted rulebook PDFs to build their knowledge bases. No publisher has explicitly authorized this use, and BGG's TOS would prohibit sourcing content from their platform for this purpose.

**Accuracy concern**: A February 2024 incident at a Root tournament in Portland highlighted risks — AI-paraphrased rules caused competitive disputes by subtly altering rule semantics.

### 5.3 Broader AI-Copyright Litigation Context

- 70+ copyright infringement lawsuits filed against AI companies as of 2025-2026.
- No board-game-specific lawsuits found, but the broader legal landscape is relevant.
- The EU Digital Services Act and French LCEN (referenced in Asmodee's TOS) may provide additional protections.
- US Copyright Office issued reports on AI and copyright in 2025.

### 5.4 Academic Research

- "Boardwalk: Towards a Framework for Creating Board Games with LLMs" (2025, arxiv.org) — academic paper on using LLMs for board game design, indicating growing research interest.

---

## 6. Implications for MeepleAI

### Risk Assessment

| Activity | Risk Level | Rationale |
|----------|-----------|-----------|
| Linking to publisher-hosted PDFs | LOW | Standard web linking; no content ingestion |
| Downloading PDFs for user's personal reference | LOW | Consistent with publisher intent |
| Ingesting PDFs into RAG/vector DB for AI assistant | HIGH | No publisher authorization; likely violates copyright |
| Scraping BGG for rulebook PDFs | VERY HIGH | Explicitly prohibited by BGG TOS |
| Using CC-licensed content (D&D SRD) | LOW | Explicitly permitted under CC-BY-4.0 with attribution |
| User-uploaded PDFs for personal AI assistant | MODERATE | User's personal use; platform liability questions |

### Recommended Approach

1. **User-uploaded content model**: Let users upload their own rulebook PDFs (which they obtained legitimately) for personal AI-assisted rules lookup. This follows the personal-use paradigm that publishers intend.

2. **Do NOT scrape or bulk-download** from BGG or publisher sites.

3. **Seek publisher partnerships** for authorized content ingestion — some publishers (via Rulepop/Boardside partnerships) are already exploring this space.

4. **Leverage CC-licensed content** (D&D SRD 5.1/5.2) as a demonstration use case where licensing is clear.

5. **Clear user attribution** and proper copyright notices per FFG/Asmodee IP policies.

6. **Monitor the legal landscape** — the AI-copyright space is evolving rapidly (2024-2026).

---

## 7. Key Gaps and Uncertainties

- **Specific TOS language on text mining**: Most publishers' TOS do not explicitly address AI/text mining. The prohibition is inferred from general copyright protection and anti-AI statements.
- **EU Text and Data Mining exception**: The EU DSA/Copyright Directive includes a text and data mining exception for research purposes; commercial applicability is debated.
- **Publisher partnership willingness**: No data found on whether publishers would license content for AI assistant use cases (as opposed to generative AI art).
- **Evolving policies**: The space is moving fast. Policies from 2024 may already be outdated.

---

## Sources

### Publisher Websites & Policies
- [Leder Games Resources](https://ledergames.com/pages/resources)
- [FFG Product Document Archive](https://www.fantasyflightgames.com/en/more/product-document-archive/)
- [FFG IP Policy (PDF)](https://images-cdn.fantasyflightgames.com/filer_public/fa/b1/fab15a15-94a6-404c-ab86-6a3b0e77a7a0/ip_policy_031419_final_v21.pdf)
- [Catan Game Rules](https://www.catan.com/understand-catan/game-rules)
- [Stonemaier Games Scythe Rules](https://stonemaiergames.com/games/scythe/rules-and-print-play/)
- [Czech Games Edition Downloads](https://czechgames.com/en/downloads/)
- [Devir Games](https://devirgames.com/)
- [Stonemaier "Generative AI? Not for Us!"](https://stonemaiergames.com/generative-ai-not-for-us/)

### BoardGameGeek
- [BGG Terms of Service](https://boardgamegeek.com/terms)
- [BGG Community Rules](https://boardgamegeek.com/community_rules)
- [BGG XML API Terms of Use](https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use)
- [BGG XML API Commercial Use](https://boardgamegeek.com/wiki/page/BGG_XML_API_Commercial_Use)
- [BGG File Upload / Copyright Discussion](https://boardgamegeek.com/thread/1349333/file-upload-copyright-bgg-terms-of-service)
- [BGG Legality of Posting Rulebooks](https://boardgamegeek.com/thread/3031595/legality-of-posting-a-rulebook)

### Open Licensing
- [ORC License (Paizo)](https://paizo.com/orclicense)
- [Open Game License (Wikipedia)](https://en.wikipedia.org/wiki/Open_Game_License)
- [WotC OGL/CC Announcement](https://www.enworld.org/threads/wotc-backs-down-original-ogl-to-be-left-untouched-whole-5e-rules-released-as-creative-commons.694850/)
- [Creative Commons / Open Source Games (BGG GeekList)](https://boardgamegeek.com/geeklist/33151/creative-commonsopen-source-games)
- [Copyright, Trademark, and OGL (Meeple Mountain)](https://www.meeplemountain.com/articles/copyright-trademark-and-open-game-licenses/)

### Industry News & AI Policy
- [Games Workshop Bans AI (Board Game Wire)](https://boardgamewire.com/index.php/2026/01/13/games-workshop-bans-ai-use-in-its-designs-celebrates-record-half-year-results/)
- [Ravensburger/Puerto Rico AI Art Incident](https://boardgamewire.com/index.php/2024/03/02/awaken-realms-pulls-ai-art-from-deluxe-puerto-rico-kickstarter-after-ravensburger-steps-in/)
- [Stonemaier Anti-AI (Board Game Wire)](https://boardgamewire.com/index.php/2024/04/24/no-ai-art-wingspan-scythe-maker-stonemaier-games-draws-hard-line-on-using-ai-in-creative-work/)
- [WotC AI Policy Controversy (GeekWire)](https://www.geekwire.com/2024/wizards-of-the-coast-will-adjust-generative-ai-policy-for-magic-following-controversy/)
- [Hasbro CEO AI Confirmation (Game Rant)](https://gamerant.com/hasbro-ceo-ai-use-confirmation-dungeons-and-dragons-magic-the-gathering/)
- [WotC Generative AI Art FAQ](https://dnd-support.wizards.com/hc/en-us/articles/26243094975252-Generative-AI-art-FAQ)
- [Board Games Copyright 2026 Legal Guide](https://legalmoveslawfirm.com/board-games-copyrighted/)
- [AI Copyright Lawsuit Developments 2025](https://copyrightalliance.org/ai-copyright-lawsuit-developments-2025/)

### AI Rulebook Apps
- [RulesBot.ai](https://www.rulesbot.ai/)
- [Boardside (BGG Thread)](https://boardgamegeek.com/thread/3631492/boardside-ai-app-for-board-game-rules)
- [BoardGameAssistant.ai](https://www.boardgameassistant.ai/)
- [AI Tool for Rulebooks (BGG Thread)](https://boardgamegeek.com/thread/3346052/ai-tool-to-readlearnrefresh-rulebooks)


---



<div style="page-break-before: always;"></div>

## research/mechanic-extractor-implementation-plan.md

# Mechanic Extractor — Implementation Plan

## Overview
Admin page implementing Variant C (human + AI assistant) workflow to extract game mechanics from rulebook PDFs and produce copyright-compliant `RulebookAnalysis` entries.

**Branch**: `feature/mechanic-extractor` (parent: `main-dev`)
**Route**: `/admin/knowledge-base/mechanic-extractor`

## Implementation Phases

### Phase 1: Backend Domain & Commands
1. **MechanicDraft entity** — New entity in SharedGameCatalog with notes/draft fields per section
2. **Repository + EF Config** — `IMechanicDraftRepository`, EF mapping, migration
3. **SaveMechanicDraftCommand** — Create/update draft (auto-save)
4. **GetMechanicDraftQuery** — Load draft for game
5. **AiAssistMechanicDraftCommand** — Send human notes to LLM, receive original text
6. **FinalizeMechanicAnalysisCommand** — Convert draft to RulebookAnalysis.CreateManual()
7. **Admin endpoints** — AdminMechanicExtractorEndpoints.cs with RequireAdminSession

### Phase 2: Frontend
8. **Page route + NavConfig** — /admin/knowledge-base/mechanic-extractor
9. **API client + Zod schemas** — mechanicExtractorClient methods + schemas
10. **SplitPanelLayout + PdfViewerPanel** — Resizable split, client-side PDF viewer
11. **MechanicEditorPanel + tabs** — 6 tabs with notes/draft UI
12. **AiAssist + DraftPreview** — AI assist button, accept/reject flow, auto-save
13. **Finalize + Review** — Save & Activate, review page

### Phase 3: Testing
14. **Backend unit tests**
15. **Frontend component tests**


---



<div style="page-break-before: always;"></div>

## research/rag-copyright-legal-analysis-2026.md

# RAG Systems and Copyrighted Content: Legal Analysis and Best Practices

**Research Date**: 2026-03-07
**Scope**: Copyright law implications for RAG systems processing copyrighted content (board game rulebooks)
**Jurisdiction Focus**: United States (primary), EU (secondary)
**Confidence Level**: High (based on 2024-2026 case law, Copyright Office reports, law firm analyses)

---

## Executive Summary

RAG systems occupy a legally distinct and more precarious position than general AI model training when it comes to copyright law. While courts have begun finding AI training itself to be fair use (Bartz v. Anthropic, Kadrey v. Meta), RAG-based retrieval and output of copyrighted content faces significantly higher legal risk because it more closely resembles traditional copying and market substitution. For MeepleAI's use case -- processing user-uploaded board game rulebooks -- the risk profile is moderate but manageable through proper legal architecture.

**Key Findings**:
1. The U.S. Copyright Office explicitly distinguishes RAG from training and views RAG as less likely to qualify as fair use
2. The first RAG-specific lawsuit (Advance Local Media v. Cohere) survived a motion to dismiss in 2025
3. User-uploaded content with proper TOS shifts significant liability, but does not eliminate it entirely
4. Technical measures (chunking strategy, no full-text storage) meaningfully reduce risk exposure
5. The "personal use by licensee" argument is the strongest defense for MeepleAI's model

---

## 1. AI Training vs. RAG: The Critical Legal Distinction

### 1.1 How Copyright Law Treats AI Training

Courts are converging on the view that AI model training on copyrighted works can constitute fair use, primarily because it is **transformative** -- converting individual works into statistical weights that generate novel outputs.

**Bartz v. Anthropic (N.D. Cal., June 2025)**: Judge Alsup ruled that using books to train Claude was fair use, calling AI training "quintessentially transformative" and "spectacularly so." However, he critically distinguished between:
- **Legally acquired works** used for training = fair use
- **Pirated works** (from shadow libraries like LibGen) used for training = NOT fair use

The case settled in August 2025 after class certification.

**Kadrey v. Meta (N.D. Cal., 2025)**: Similarly found AI training to be fair use on substantially similar reasoning.

### 1.2 How Copyright Law Treats RAG Systems

RAG faces a fundamentally different analysis because it does not merely learn from copyrighted works -- it **stores**, **retrieves**, and **outputs** portions of them.

**U.S. Copyright Office Part 3 Report (May 2025)**: The Office drew an explicit distinction:

> "RAG systems function in two steps: the system first copies the source materials into a retrieval database, and then, when prompted by a user query, outputs them again. While such an architecture improves accuracy, both the initial unauthorized reproduction and the later relaying of that material are potential copyright infringements which do not qualify as fair use."

> "The use of RAG is less likely to be transformative where the purpose is to generate outputs that summarize or provide abridged versions of retrieved copyrighted works."

This is a significant policy signal. The Copyright Office views RAG as:
1. Involving **two acts of copying** (ingestion into database + output to user)
2. **Less transformative** than training because it directly relays source content
3. **More likely to cause market harm** because outputs substitute for originals

### 1.3 Embedding Generation

The legal status of embeddings (vector representations) is unsettled but trending toward risk:

- Embeddings preserve semantic relationships from original content
- Technical research shows "extraction attacks" can recover significant portions of source text
- The Copyright Office notes that "tokenized datasets constitute reproductions" -- embeddings are a form of tokenization
- However, embeddings alone are not human-readable copies, which may provide some defense
- No court has directly ruled on whether generating embeddings alone constitutes infringement

**Risk Assessment**: Embeddings occupy a gray area. They are clearly derived from copyrighted works, but whether the derivation constitutes "copying" under copyright law remains unresolved. Storing only embeddings (without source text chunks) would reduce but not eliminate risk.

### 1.4 Why the Distinction Matters

| Factor | AI Training | RAG System |
|--------|------------|------------|
| Transformativeness | High (statistical model) | Low-Medium (retrieves/summarizes) |
| Copying | Intermediate (into weights) | Direct (into database + output) |
| Market substitution | Low (generates novel content) | High (can substitute for original) |
| Copyright Office view | "May be fair use" | "Less likely fair use" |
| Court rulings | Trending toward fair use | First case survived MTD (Cohere) |

---

## 2. Key AI Copyright Cases (2024-2026)

### 2.1 NYT v. OpenAI (S.D.N.Y., filed Dec 2023)

**Status**: In discovery phase as of January 2026.

**Key Developments**:
- April 2025: Court narrowed claims, dismissed several, focusing case primarily on fair use
- November 2025: Court ordered OpenAI to produce 20 million ChatGPT logs (not just cherry-picked conversations)
- January 2026: Judge Stein affirmed the discovery order over OpenAI's privacy objections
- No fair use ruling expected until summer 2026 at the earliest

**Implications for RAG**: The case primarily addresses training, but discovery of output logs showing verbatim reproduction is directly relevant to RAG systems that retrieve and display copyrighted text.

### 2.2 Authors Guild v. OpenAI (S.D.N.Y., filed Sept 2023)

**Status**: Consolidated with other cases; in discovery phase.

**Key Developments**:
- April 2025: Transferred and consolidated in S.D.N.Y.
- October 2025: Judge Stein denied OpenAI's motion to dismiss output infringement claims, finding authors may be able to prove ChatGPT outputs are "similar enough" to violate copyrights
- Court explicitly declined to opine on fair use at this stage
- Discovery conferences ongoing (January-February 2026)

**Implications for RAG**: The court's willingness to allow output-based infringement claims to proceed is directly relevant. RAG outputs that closely track source material face similar exposure.

### 2.3 Thomson Reuters v. Ross Intelligence (D. Del., filed Dec 2020)

**Status**: Partial summary judgment granted February 2025; trial on remaining issues scheduled May 2025.

**Key Ruling**: First court to **reject** fair use for AI training in the context of a competitive product.

**Critical Findings**:
- Ross used Westlaw headnotes (via a third party) to build a competing legal search AI
- Court found the use was **commercial** and lacked a "further purpose or different character" because Ross's product directly competed with Westlaw
- Court recognized an **obvious potential market for licensing copyrighted material for AI training**
- The competitive relationship between the original and the AI product was decisive

**Implications for RAG**: This is the most directly relevant precedent for RAG systems. Ross built what was essentially a RAG-like search tool that competed with the source. The court's emphasis on competitive relationship and the existence of licensing markets is critical. MeepleAI's position differs because it does not compete with rulebook publishers, but the case shows courts will scrutinize whether the AI product substitutes for purchasing/accessing the original.

### 2.4 Getty Images v. Stability AI (UK High Court, Nov 2025)

**Status**: Judgment issued November 4, 2025.

**Key Ruling**: Getty largely lost -- the court found:
- AI model weights are **not a "copy"** of training images
- Models contain "statistically trained parameters, not stored copies"
- Getty abandoned its primary copyright claim because training did not occur in the UK
- Limited trademark infringement found

**Implications for RAG**: The UK ruling that model weights are not copies supports the argument that embeddings may not constitute copies. However, RAG systems store more than just weights -- they store retrievable text chunks, which is a different (and riskier) proposition.

### 2.5 Advance Local Media v. Cohere -- The First RAG Case (S.D.N.Y., filed Feb 2025)

**Status**: Motion to dismiss denied November 2025; proceeding to discovery.

**This is the most important case for RAG systems.** Fourteen publishers (Forbes, Conde Nast, LA Times, The Atlantic, etc.) sued Cohere alleging its RAG system:
- Accesses publishers' sites and incorporates content into responses
- Produces "verbatim copies, substantial excerpts, and substitutive summaries"
- Bypasses paywalls
- Over 4,000 allegedly infringed works identified

**Court's Ruling on MTD**:
- Denied dismissal on direct infringement, secondary infringement, and Lanham Act claims
- Found that "in some of the examples, the outputs were nearly identical to the underlying work, lifting several paragraphs in their entirety"
- Rejected Cohere's argument that summaries differed in "tone, style, length and sentence structure"

**Implications for RAG**: This case establishes that RAG systems face viable copyright claims when they retrieve and output substantial portions of copyrighted works. The critical distinction for MeepleAI is that Cohere scraped publishers' content without permission, while MeepleAI processes user-uploaded content.

---

## 3. RAG-Specific Legal Analysis

### 3.1 Does Storing Text Chunks in a Vector Database Constitute "Copying"?

**Answer: Almost certainly yes, in the traditional copyright sense.**

Under 17 U.S.C. 106, copyright holders have the exclusive right to "reproduce the copyrighted work in copies." Storing text chunks -- even broken into segments -- creates reproductions. The Copyright Office's Part 3 report explicitly identifies "copying source materials into a retrieval database" as the first act of potential infringement in RAG systems.

**Mitigating factors**:
- If chunks are small enough, they may fall below the threshold of substantial similarity
- If chunks are stored only temporarily (caching), they may qualify for the ephemeral copy exception
- If the user uploaded the content, the platform may argue it is the user who made the copy

### 3.2 Is Retrieving and Displaying Snippets Fair Use?

**Analysis under the four fair use factors (17 U.S.C. 107):**

| Factor | Analysis for MeepleAI | Risk Level |
|--------|----------------------|------------|
| **Purpose and character** | Commercial service, but does not compete with rulebook sales. Purpose is to help users understand games they already own. Some transformative element (answering questions vs. reading rules). | Medium |
| **Nature of the work** | Rulebooks are factual/instructional, not highly creative. Factual works receive thinner protection. | Low |
| **Amount used** | RAG retrieves chunks, not full works. But cumulative retrieval could reconstruct substantial portions. | Medium |
| **Market effect** | Users have already purchased the game. The service does not substitute for buying the rulebook. | Low |

**Overall Fair Use Assessment**: Moderate-to-favorable. The factual nature of rulebooks and the fact that users have already purchased the game are strong factors. The key risk is if the system can output large portions of verbatim text.

### 3.3 Does Purpose Affect the Analysis?

**Yes, significantly.** MeepleAI's purpose -- helping users understand games they already own -- is distinct from cases like Cohere (substituting for news articles) or Ross (competing with Westlaw). Courts consider:

- **Non-competitive purpose**: MeepleAI does not compete with game publishers
- **Complementary use**: The service adds value to the purchased product
- **User benefit**: Helps users get more value from games they already bought
- **No market substitution**: Nobody buys a rulebook to read it like a book; the RAG system does not replace the product

### 3.4 User-Uploaded Content: The "User is the Licensee" Argument

**This is MeepleAI's strongest legal position.**

When users upload their own rulebooks:
1. They typically own a physical copy (implied license to personal use)
2. They are making the copy, not MeepleAI
3. The platform is providing a tool, similar to a scanner or note-taking app
4. The user's personal use purpose is stronger than the platform's commercial purpose

**Analogies**:
- Google Books: Court found that scanning books for search indexing was fair use (Authors Guild v. Google, 2d Cir. 2015)
- Personal cloud storage: Users storing their own purchased content is generally accepted
- Note-taking apps: Evernote, OneNote process user-uploaded documents without copyright issues

**Limitations of this argument**:
- It weakens if MeepleAI uses uploaded content across users (one user's upload benefits all users)
- It weakens if MeepleAI retains content after the user leaves
- It weakens if uploaded content is used to train models

---

## 4. Safe Harbor and Platform Liability

### 4.1 DMCA Safe Harbor (17 U.S.C. 512)

DMCA safe harbor protects platforms from liability for user-uploaded infringing content, provided they:

1. **Designate a DMCA agent** with the Copyright Office
2. **Implement a repeat infringer policy**
3. **Respond expeditiously** to takedown notices
4. **Do not have actual knowledge** of infringement
5. **Do not receive financial benefit** directly attributable to infringing activity they could control

**Application to MeepleAI**:
- Safe harbor likely applies to user-uploaded PDFs (users upload, platform hosts)
- The platform must not actively encourage uploading copyrighted content
- Processing content with AI may complicate the "passive intermediary" assumption
- As of 2025, courts and scholars note that "generative AI overturns the passive-intermediary assumptions that underlie the DMCA safe harbour"

**Proposed "AI Harbour" Framework** (academic, 2025): Scholars have proposed tiered duties:
- Data suppliers: provenance disclosure and transparency
- Developers: dataset curation, memorization-mitigation, watermarking
- Deployers: dynamic filtering, complaint handling, repeat-infringer policies

**Key Risk**: If MeepleAI processes uploaded content to generate responses (not merely hosting it), a court might find the platform is doing more than passively hosting, potentially weakening the safe harbor defense. The tighter compliance timelines being demanded in 2025 also increase operational burden.

### 4.2 EU Digital Services Act (DSA)

The DSA (fully applicable since February 2024) imposes obligations on platforms hosting user content:

- **Notice and action mechanisms**: Must provide easy-to-use systems for reporting illegal content
- **Transparency reporting**: Must publish regular reports on content moderation
- **Algorithmic accountability**: Must explain how AI systems recommend or process content
- **Due diligence obligations**: Scale with platform size

**EU AI Act Copyright Provisions (effective August 2025)**:
- GPAI providers must comply with copyright law, including EU text and data mining exceptions
- Must respect "opt-out" reservations by rights holders against TDM
- Must produce sufficiently detailed summaries of training data used
- The EU approach is more prescriptive than the U.S. fair use framework

**Implications for MeepleAI**: If operating in the EU, MeepleAI would need to respect TDM opt-outs from publishers and provide transparency about how uploaded content is processed. The DSA's notice-and-action requirements would apply.

### 4.3 Platform vs. User Responsibility

| Scenario | Likely Liability Bearer | Legal Basis |
|----------|------------------------|-------------|
| User uploads own purchased rulebook | User (personal copy) | Fair use / implied license |
| User uploads pirated PDF | User (primary); Platform (if knew/should have known) | DMCA 512 |
| Platform uses upload across all users | Platform (beyond user's personal use) | Direct infringement |
| Platform trains models on uploads | Platform (new use beyond user authorization) | Reproduction right |
| Platform stores only embeddings from upload | Unclear (gray area) | No direct precedent |

---

## 5. Best Practices from Similar Services

### 5.1 Google NotebookLM

**Model**: Users upload their own documents; AI processes them for Q&A and summarization.

**Key TOS provisions**:
- Users must have "necessary rights to upload or share content" and content must be "lawful"
- Google does not claim ownership of generated content
- User data is not used to train models (for Workspace/Education accounts)
- Feedback may be reviewed
- Users indemnify Google for claims arising from their use
- DMCA compliance with notice-and-takedown

**Relevance**: NotebookLM is the closest comparable to MeepleAI's model. Google's approach relies heavily on (a) user certification of rights and (b) not using content for training.

### 5.2 ChatGPT (OpenAI)

**Model**: Users can upload documents for analysis; also scrapes web for training.

**Key provisions**:
- Users retain ownership of inputs and outputs (subject to applicable law)
- Users represent they have rights to input content
- OpenAI may use inputs for model improvement (can opt out with API/Enterprise)
- Indemnification clause for user-provided content

### 5.3 Legal Research AI (Westlaw AI, Lexis+ AI Protege)

**Model**: These services process exclusively **licensed** content.

**Key approach**:
- Thomson Reuters and LexisNexis own or license all content their AI processes
- Westlaw AI is "grounded in trusted, authoritative content" that TR has rights to
- Lexis+ AI Protege is trained on "primary law and limited secondary sources owned by Lexis"
- Neither processes user-uploaded third-party copyrighted content

**Lesson**: The legally safest approach is to process only content you own or license. This is why TR sued Ross -- Ross tried to build a competing product without licensing.

### 5.4 Common TOS Patterns Across Services

1. **User certification**: Users affirm they have rights to uploaded content
2. **Indemnification**: Users agree to hold the platform harmless for IP claims
3. **No training pledge**: Content not used for model training without consent
4. **DMCA compliance**: Designated agent, takedown procedures, repeat infringer policy
5. **Content restrictions**: Prohibited from uploading content without rights
6. **Limitation of liability**: Platform liability capped, typically at subscription fees paid

---

## 6. Practical Risk Mitigation Strategies for MeepleAI

### 6.1 Terms of Service Provisions (HIGH PRIORITY)

**Required clauses**:

1. **User Certification of Rights**:
   > "By uploading content, you represent and warrant that you own or have the legal right to use and process such content, including but not limited to copyright rights. You represent that you have lawfully obtained any rulebooks, manuals, or game documents you upload."

2. **Indemnification**:
   > "You agree to indemnify, defend, and hold harmless MeepleAI from any claims, damages, losses, or expenses arising from your upload of content to which you do not have the requisite rights."

3. **No Training Pledge**:
   > "User-uploaded content is processed solely to provide you with AI-assisted game guidance. Your uploaded content is not used to train our AI models and is not shared with other users."

4. **Content Scope Limitation**:
   > "This service is designed to process board game rulebooks and related game materials that you have lawfully purchased or obtained."

5. **DMCA Notice Procedure**:
   > Full DMCA takedown procedure, designated agent registration, repeat infringer policy

6. **Acceptable Use**:
   > "You may not upload content that you do not have the right to reproduce, including pirated, unlicensed, or unauthorized copies of copyrighted materials."

### 6.2 Technical Measures (HIGH PRIORITY)

1. **No full-text storage**: Store only embeddings and minimal text chunks necessary for retrieval. Do not retain complete document text after processing.

2. **Chunk size limits**: Keep retrieved chunks small (e.g., 200-500 tokens). Smaller chunks reduce the risk of reproducing "substantial" portions.

3. **Output guardrails**: Limit the amount of verbatim text that can appear in a single response. Paraphrase rather than quote when possible.

4. **Per-user isolation**: Each user's uploaded content should be isolated. One user's upload must never augment another user's queries.

5. **Retention limits**: Auto-delete uploaded content and derived data after a reasonable period (e.g., 90 days of inactivity).

6. **No cross-training**: Never use user-uploaded content to fine-tune or train models.

7. **Watermark/source detection**: Optionally detect and refuse clearly pirated content (e.g., PDFs with piracy watermarks or from known piracy sources).

### 6.3 DMCA Compliance (HIGH PRIORITY)

1. **Register a DMCA agent** with the U.S. Copyright Office
2. **Publish a DMCA policy** on the website
3. **Implement takedown procedures**: Respond to valid takedown notices within 24-48 hours
4. **Repeat infringer policy**: Terminate accounts of users who repeatedly upload infringing content
5. **Counter-notification process**: Allow users to contest takedowns
6. **Logging**: Maintain records of takedown notices and responses

### 6.4 Content Licensing (MEDIUM PRIORITY)

1. **Publisher partnerships**: Approach board game publishers for explicit licenses to process their rulebooks. Many publishers (especially indie publishers) may welcome this as it increases game adoption.

2. **Open/CC-licensed content**: Prioritize processing content under Creative Commons or similar open licenses.

3. **Publisher opt-out mechanism**: Allow publishers to register their works and opt out of processing.

4. **Revenue sharing**: Consider models where publishers receive compensation for licensed content processing.

### 6.5 User Communication (MEDIUM PRIORITY)

1. **Clear upload prompts**: When users upload, display: "Please only upload rulebooks for games you own."

2. **FAQ**: Publish a clear FAQ explaining what happens to uploaded content, how it's processed, and when it's deleted.

3. **Privacy policy**: Explain data handling, retention, and deletion in the privacy policy.

### 6.6 Risk Tier Assessment for MeepleAI

| Risk Factor | MeepleAI's Position | Risk Level |
|-------------|---------------------|------------|
| Content type | Factual/instructional (rulebooks) | LOW |
| Market substitution | None (complements game purchase) | LOW |
| Who uploads | Users upload their own purchased content | LOW |
| Verbatim reproduction | RAG chunks may contain verbatim text | MEDIUM |
| Cross-user sharing | If one upload serves all users: HIGH | VARIES |
| Content retention | If stored indefinitely: MEDIUM | VARIES |
| Publisher relationship | Non-competitive, complementary | LOW |
| Scale | Small platform vs. major publisher catalogs | LOW |

**Overall Risk**: LOW-MEDIUM with proper safeguards implemented.

---

## 7. Conclusions and Recommendations

### 7.1 Legal Landscape Summary

The legal landscape for RAG and copyright is evolving rapidly but trending toward:
- **Training = generally fair use** (Bartz, Kadrey)
- **RAG = higher risk** than training (Copyright Office Part 3, Cohere lawsuit)
- **User-uploaded content = significant protection** if properly structured (DMCA safe harbor, TOS)
- **Competitive products = highest risk** (Thomson Reuters v. Ross)
- **Complementary products = lower risk** (MeepleAI's position)

### 7.2 Priority Actions for MeepleAI

**Immediate (before launch)**:
1. Implement comprehensive TOS with user certification, indemnification, and acceptable use provisions
2. Register a DMCA agent with the Copyright Office
3. Implement per-user content isolation (no cross-user sharing of uploaded content)
4. Implement output guardrails limiting verbatim reproduction
5. Publish DMCA policy and takedown procedures

**Short-term (within 3 months of launch)**:
1. Implement automated content retention limits
2. Begin outreach to board game publishers for partnership/licensing
3. Implement publisher opt-out mechanism
4. Review and optimize chunk sizes for legal risk minimization

**Medium-term (6-12 months)**:
1. Monitor Cohere case and NYT v. OpenAI for new precedents
2. Consider EU compliance if serving European users (DSA, AI Act)
3. Explore content licensing agreements with major publishers
4. Regular legal review of RAG output patterns for infringement risk

### 7.3 The Strongest Legal Argument for MeepleAI

MeepleAI's best legal position rests on the combination of:

1. **User agency**: Users upload content they own/purchased
2. **Factual content**: Rulebooks are factual/instructional, receiving thin copyright protection
3. **Non-competitive purpose**: Service complements rather than substitutes for game purchases
4. **Personal use**: Processing serves the user's personal understanding of their own games
5. **Platform neutrality**: MeepleAI provides the tool; users choose what to upload
6. **DMCA compliance**: Full safe harbor procedures in place

This combination -- lawful user + factual content + non-competitive purpose + DMCA compliance -- provides a strong multilayered defense.

---

## Sources

### Case Law
- [Bartz v. Anthropic - Landmark Fair Use Ruling](https://www.afslaw.com/perspectives/alerts/landmark-ruling-ai-copyright-fair-use-vs-infringement-bartz-v-anthropic)
- [Bartz v. Anthropic - Settlement](https://www.insidetechlaw.com/blog/2025/09/bartz-v-anthropic-settlement-reached-after-landmark-summary-judgment-and-class-certification)
- [Thomson Reuters v. Ross Intelligence](https://www.dwt.com/blogs/artificial-intelligence-law-advisor/2025/02/reuters-ross-court-ruling-ai-copyright-fair-use)
- [Thomson Reuters v. Ross - Reed Smith Analysis](https://www.reedsmith.com/en/perspectives/2025/03/court-ai-fair-use-thomson-reuters-enterprise-gmbh-ross-intelligence)
- [Advance Local Media v. Cohere - RAG Lawsuit](https://copyrightlately.com/court-rules-ai-news-summaries-may-infringe-copyright/)
- [Advance Local Media v. Cohere - MTD Denied](https://www.newsmediaalliance.org/judge-denies-cohere-motion-to-dismiss/)
- [Getty Images v. Stability AI - UK Ruling](https://www.mayerbrown.com/en/insights/publications/2025/11/getty-images-v-stability-ai-what-the-high-courts-decision-means-for-rights-holders-and-ai-developers)
- [Authors Guild v. OpenAI - MTD Denied](https://www.cullenllp.com/blog/ai-lawsuits-are-coming-court-denies-openais-motion-to-dismiss-claims-that-chatgpt-infringed-game-of-thrones-authors-rights/)
- [OpenAI 20M Logs Discovery Order](https://natlawreview.com/article/openai-loses-privacy-gambit-20-million-chatgpt-logs-likely-headed-copyright-plaintiffs)
- [Status of All 51 AI Copyright Lawsuits](https://chatgptiseatingtheworld.com/2025/10/08/status-of-all-51-copyright-lawsuits-v-ai-oct-8-2025-no-more-decisions-on-fair-use-in-2025/)

### Government and Policy
- [U.S. Copyright Office Part 3 Report - AI Training (May 2025)](https://www.copyright.gov/ai/Copyright-and-Artificial-Intelligence-Part-3-Generative-AI-Training-Report-Pre-Publication-Version.pdf)
- [Copyright Office AI Study Portal](https://www.copyright.gov/policy/artificial-intelligence/)
- [Sidley Austin - Copyright Office Part 3 Analysis](https://www.sidley.com/en/insights/newsupdates/2025/05/generative-ai-meets-copyright-scrutiny)
- [Authors Guild - Copyright Office Report Analysis](https://authorsguild.org/news/us-copyright-office-ai-report-part-3-what-authors-should-know/)
- [Perkins Coie - Copyright Office Analysis](https://perkinscoie.com/insights/update/copyright-office-stakes-out-position-use-works-ai-training)
- [EU AI Act Copyright Consultations](https://www.insideprivacy.com/artificial-intelligence/european-commission-launches-consultations-on-the-eu-ai-acts-copyright-provisions-and-ai-regulatory-sandboxes/)
- [EU Digital Services Act](https://digital-strategy.ec.europa.eu/en/policies/digital-services-act)
- [EU Parliament Copyright Proposals](https://www.globalpolicywatch.com/2026/02/european-parliament-proposes-changes-to-copyright-protection-in-the-age-of-generative-ai/)

### Law Firm Analysis and Commentary
- [Norton Rose Fulbright - Copyright and AI Training](https://www.nortonrosefulbright.com/en/knowledge/publications/87200379/practical-commentary-regarding-copyright-and-generative-ai-training)
- [DMCA Safe Harbor and AI (2025)](https://patentpc.com/blog/dmca-safe-harbor-and-the-rise-of-ai-content-whats-changing-in-2025)
- [DMCA Safe Harbor for AI Platforms](https://patentpc.com/blog/dmca-safe-harbor-rules-and-their-application-to-ai-platforms)
- [AI Harbour Proposal (Oxford Academic)](https://academic.oup.com/jiplp/article/20/9/605/8221820)
- [RAG Copyright Concerns (36Kr)](https://eu.36kr.com/en/p/3422429684387205)
- [RAG Legal Considerations (Legal Foundations UK)](https://legalfoundations.org.uk/blog/legal-considerations-with-retrieval-augmented-generation-rag/)
- [Asia IP - RAG Copyright Analysis](https://www.asiaiplaw.com/section/in-depth/the-latest-rage-called-rag)
- [Perplexity RAG Copyright Paths](https://themediabrain.substack.com/p/perplexitys-use-of-rag-opens-up-3)
- [Fair Use in AI Training - Skadden Analysis](https://www.skadden.com/insights/publications/2025/07/fair-use-and-ai-training)
- [AI Copyright 2025 Year in Review](https://copyrightalliance.org/ai-copyright-lawsuit-developments-2025/)
- [TechPolicy - Missing Fair Use Argument for AI Summaries](https://www.techpolicy.press/the-missing-fair-use-argument-in-the-copyright-battle-over-ai-summaries/)

### Platform Terms and Industry
- [Google NotebookLM Terms](https://support.google.com/notebooklm/answer/16164461)
- [NotebookLM Additional Terms (via U. Oslo)](https://www.uio.no/english/services/it/ai/notebooklm/help/terms-and-conditions.html)
- [AI Platform Ownership Rules Comparison](https://terms.law/2025/04/09/navigating-ai-platform-policies-who-owns-ai-generated-content/)
- [AI TOS Fine Print Analysis](https://www.termsfeed.com/blog/ai-terms-service-fine-print/)
- [Westlaw and Lexis AI Updates](https://usfblogs.usfca.edu/ziefbrief/2026/02/02/legal-research-ai-update-latest-versions-from-westlaw-and-lexis/)
- [IPWatchdog - Licensing Vector for AI](https://ipwatchdog.com/2024/04/10/licensing-vector-fair-approach-content-use-llms-2/id=175202/)
- [AI Copyright Litigation to Licensing](https://ipwatchdog.com/2026/02/15/ai-copyright-how-lessons-litigation-pave-way-licensing/)

### Academic
- [Harvard JOLT - RAG for Legal Work](https://jolt.law.harvard.edu/digest/retrieval-augmented-generation-rag-towards-a-promising-llm-architecture-for-legal-work)
- [Copyright Safety for Generative AI (Houston Law Review)](https://houstonlawreview.org/article/92126-copyright-safety-for-generative-ai)
- [Penn State Law Review - AI and Copyright Fine Print](https://www.pennstatelawreview.org/wp-content/uploads/2025/05/1.-Kim_577-605.pdf)
- [Springer - Copyright and ML Model Lifecycle](https://link.springer.com/article/10.1007/s40319-023-01419-3)
- [CRS Report - Generative AI and Copyright Law](https://www.congress.gov/crs-product/LSB10922)

---

*Disclaimer: This research document is for informational purposes only and does not constitute legal advice. MeepleAI should consult with qualified intellectual property counsel before implementing any legal strategy.*


---

