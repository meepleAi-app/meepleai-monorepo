# AI-07: RAG Optimization Phase 1 - Technical Design

**Status**: Draft
**Created**: 2025-01-18
**Owner**: AI/RAG Team
**Related Issues**: #467, #468, #469, #470

---

## Executive Summary

This document outlines the technical design for Phase 1 of the RAG (Retrieval-Augmented Generation) optimization project, targeting a **+30-40% improvement** in answer quality metrics through three Quick Win optimizations:

1. **Advanced Prompt Engineering** - Few-shot learning and structured templates
2. **Adaptive Semantic Chunking** - Context-aware text segmentation
3. **LLM-Based Query Expansion** - Multi-query retrieval with score fusion

**Current Metrics** (AI-06 baseline):
- Precision@5: 0.70
- Mean Reciprocal Rank (MRR): 0.60
- Latency p95: ~1800ms
- Success Rate: 95%+

**Target Metrics** (Phase 1 goals):
- Precision@5: **0.90+** (+28% improvement)
- MRR: **0.75+** (+25% improvement)
- Latency p95: **≤2000ms** (maintain)
- Success Rate: **95%+** (maintain)

**Timeline**: 2-3 weeks
**Budget**: $2-3/month additional operational costs
**Impact**: High (direct user experience improvement)

---

## 1. Current System Architecture

### 1.1 RAG Pipeline Overview

```
┌─────────────┐
│ User Query  │
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────────────────┐
│         StreamingQaService                  │
│  ┌───────────────────────────────────────┐ │
│  │ 1. Query → RagService.SearchAsync()   │ │
│  │    └─ EmbeddingService (nomic-embed)  │ │
│  │    └─ QdrantService (vector search)   │ │
│  │                                         │ │
│  │ 2. Retrieved Chunks (top 10)          │ │
│  │                                         │ │
│  │ 3. Prompt Construction                │ │
│  │    └─ Generic template                │ │
│  │    └─ Inject RAG context              │ │
│  │                                         │ │
│  │ 4. LLM Generation                      │ │
│  │    └─ LlmService (OpenRouter)         │ │
│  │    └─ StreamAsync() → SSE             │ │
│  └───────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
       │
       ▼
┌─────────────┐
│ SSE Stream  │
│ to Client   │
└─────────────┘
```

### 1.2 Text Processing Pipeline

```
┌──────────────┐
│ PDF Upload   │
└──────┬───────┘
       │
       ▼
┌────────────────────────────────────────┐
│ PdfTextExtractionService (Docnet)     │
│ Output: Full text with page numbers   │
└────────────┬───────────────────────────┘
             │
             ▼
┌────────────────────────────────────────┐
│ TextChunkingService (Fixed-Size)      │
│ Strategy: 512 chars, 50 char overlap  │
│ Output: ~200 chunks per 100-page PDF  │
└────────────┬───────────────────────────┘
             │
             ▼
┌────────────────────────────────────────┐
│ EmbeddingService (nomic-embed-text)   │
│ Model: 768-dim vectors                │
└────────────┬───────────────────────────┘
             │
             ▼
┌────────────────────────────────────────┐
│ QdrantService.IndexTextChunksAsync()  │
│ Collection: per-game partitioning     │
│ Distance: Cosine similarity           │
└────────────────────────────────────────┘
```

### 1.3 Current Limitations

| Component | Limitation | Impact on Quality |
|-----------|-----------|-------------------|
| **Chunking** | Fixed 512-char splits | Breaks mid-sentence, loses context |
| | No semantic awareness | Important rules split across chunks |
| | No list/table preservation | Numbered steps fragmented |
| **Retrieval** | Dense vectors only | Misses exact keyword matches |
| | No query preprocessing | Short queries underperform |
| | No reranking | Semantic drift in top-k results |
| **Prompting** | Generic template | No domain-specific guidance |
| | No few-shot examples | Inconsistent answer formatting |
| | No chain-of-thought | Struggles with complex reasoning |
| **Evaluation** | IR metrics only (P@K, MRR) | No semantic quality measures |
| | No faithfulness checks | Potential hallucinations undetected |

---

## 2. Optimization #1: Advanced Prompt Engineering

**Issue**: #468
**Effort**: 3-4 days
**Cost**: $0/month
**Expected Gain**: +10-15% answer accuracy

### 2.1 Design Rationale

Current prompts are generic and lack domain-specific guidance. Research shows that:
- **Few-shot examples** improve task performance by 15-20% ([Brown et al., 2020](https://arxiv.org/abs/2005.14165))
- **Structured prompts** with explicit instructions reduce hallucinations by 30% ([OpenAI, 2023](https://platform.openai.com/docs/guides/prompt-engineering))
- **Chain-of-thought reasoning** improves multi-step problem solving ([Wei et al., 2022](https://arxiv.org/abs/2201.11903))

### 2.2 Prompt Template Structure

#### Current Prompt (Simplified)
```
You are a helpful assistant. Answer the user's question using the provided context.

Context: [RAG chunks]

Question: [user query]
```

#### Proposed Prompt (Enhanced)
```
# Role & Expertise
You are an expert board game rules assistant with deep knowledge of tabletop games.
Your goal is to provide accurate, citation-backed answers from official rulebooks.

# Instructions
1. Answer ONLY using the provided rulebook excerpts below
2. Cite page numbers in format [p.X] after each statement
3. If information is missing, state "This is not covered in the available rules"
4. For ambiguous questions, ask clarifying questions before answering
5. Use a confident tone for clear rules, cautious tone when interpreting edge cases

# Examples of Good Answers

Q: How do I set up a Chess game?
A: To set up Chess, place the board so each player has a white square on their right [p.5].
Place rooks in the corners, knights next to them, bishops next to knights, queen on her color
(white queen on white square), and king on the remaining square [p.5-6]. Pawns fill the second
rank [p.6]. White moves first [p.7].

Q: Can I move my pawn backward in Chess?
A: No, pawns can never move backward [p.12]. Pawns move forward one square, or two squares
on their first move [p.12]. They capture diagonally forward [p.13].

Q: What is en passant?
A: En passant is a special pawn capture [p.14]. If an enemy pawn moves two squares forward
from its starting position and lands beside your pawn, you may capture it "in passing" by
moving diagonally to the square it skipped over [p.14]. This must be done immediately on
your next turn or the right is lost [p.14].

# Rulebook Context
{context}

# User Question
{question}

# Your Answer
(Provide your answer following the instructions above. Start with a direct answer,
then elaborate with details and citations.)
```

### 2.3 Implementation Architecture

```
┌─────────────────────────────────────────────┐
│         PromptTemplateService               │
├─────────────────────────────────────────────┤
│                                             │
│  + GetTemplate(gameType, questionType)     │
│  + RenderPrompt(template, context, query)  │
│  + ValidatePrompt(prompt) → errors         │
│                                             │
│  Templates stored in:                       │
│  - appsettings.json (RagPrompts section)   │
│  - Config/PromptTemplates/*.txt files      │
│                                             │
│  Template Variables:                        │
│  - {context}: RAG chunks with citations    │
│  - {question}: User query                  │
│  - {game_name}: Current game               │
│  - {confidence}: Retrieval confidence      │
│  - {few_shot_examples}: Dynamic examples   │
└─────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────┐
│      StreamingQaService Integration         │
├─────────────────────────────────────────────┤
│                                             │
│  AskStreamAsync(gameId, query, chatId):    │
│                                             │
│  1. Retrieve RAG context                   │
│  2. Classify question type:                │
│     - Setup query                          │
│     - Gameplay rule                        │
│     - Winning condition                    │
│     - Edge case / special move             │
│  3. Select appropriate template + examples │
│  4. Render final prompt                    │
│  5. Call LlmService.GenerateStreamAsync()  │
└─────────────────────────────────────────────┘
```

### 2.4 Few-Shot Example Categories

| Question Type | Example Count | Source |
|---------------|---------------|--------|
| **Setup** | 3-5 | Chess setup, Tic-Tac-Toe setup, generic board setup |
| **Gameplay Rules** | 5-7 | Piece movement (Chess), turn structure, valid actions |
| **Winning Conditions** | 3-4 | Checkmate, three-in-a-row, point thresholds |
| **Edge Cases** | 5-6 | En passant, castling, stalemate, draw conditions |

### 2.5 Configuration Schema

**File**: `apps/api/src/Api/appsettings.json`

```json
{
  "RagPrompts": {
    "DefaultTemplate": "generic",
    "Templates": {
      "generic": {
        "SystemPrompt": "You are an expert board game rules assistant...",
        "FewShotExamples": [
          {
            "question": "How do I set up Chess?",
            "answer": "To set up Chess, place the board so each player...",
            "category": "setup"
          }
        ],
        "MaxExamples": 3,
        "IncludeChainOfThought": false
      },
      "chess": {
        "SystemPrompt": "You are a Chess rules expert...",
        "FewShotExamples": [/* chess-specific */],
        "MaxExamples": 5,
        "IncludeChainOfThought": true
      }
    },
    "QuestionClassification": {
      "Enabled": true,
      "Keywords": {
        "setup": ["setup", "start", "begin", "initial", "place"],
        "gameplay": ["move", "turn", "action", "play", "can I"],
        "winning": ["win", "victory", "checkmate", "three in a row"],
        "edge_case": ["special", "exception", "what if", "happens when"]
      }
    }
  }
}
```

### 2.6 Testing Strategy

**Unit Tests** (`PromptTemplateServiceTests.cs`):
- Template loading and validation
- Variable substitution (context, question, game name)
- Few-shot example selection by question type
- Prompt length limits (LLM token constraints)

**Integration Tests** (`StreamingQaServiceTests.cs` updates):
- End-to-end prompt rendering in QA flow
- Compare answer quality with/without few-shot examples
- Validate citation format in LLM responses

**Evaluation Tests** (`RagEvaluationServiceTests.cs` updates):
- Run evaluation with old vs. new prompts
- Measure P@K, MRR improvement
- Add semantic similarity metric (cosine similarity between answer and ground truth)

---

## 3. Optimization #2: Adaptive Semantic Chunking

**Issue**: #469
**Effort**: 4-5 days
**Cost**: $0/month
**Expected Gain**: +10-15% P@5/Recall

### 3.1 Design Rationale

Fixed-size chunking (512 chars) has critical flaws:
- **Mid-sentence breaks**: "The rook can move horizontally or vert-" (chunk boundary) "-ically any number of squares."
- **Context loss**: Rule explanations split across chunks reduce retrieval relevance
- **List fragmentation**: Numbered setup steps separated into different chunks

**Research Evidence**:
- LangChain's RecursiveCharacterTextSplitter improves chunk coherence by 40% ([LangChain Blog, 2023](https://blog.langchain.dev/improving-document-retrieval-with-semantic-chunking/))
- Semantic chunking increases Recall@10 by 15-20% in information retrieval tasks ([Anthropic, 2024](https://www.anthropic.com/news/contextual-retrieval))

### 3.2 Recursive Splitting Algorithm

**Strategy**: Split by semantic boundaries in priority order:

```
1. Paragraph boundaries (double newline: \n\n)
   ├─ If chunk > max_size → proceed to step 2
   └─ If chunk < max_size → DONE

2. Sentence boundaries (period + space/newline: \.[\s\n])
   ├─ If sentence > max_size → proceed to step 3
   └─ If sentence < max_size → DONE

3. Clause boundaries (comma, semicolon, colon: [,;:]\s)
   ├─ If clause > max_size → proceed to step 4
   └─ If clause < max_size → DONE

4. Character split (fallback, preserve words)
   └─ Split at word boundaries (spaces)
```

**Special Handling**:
- **Numbered/Bulleted Lists**: Keep entire list together if <max_size
- **Tables**: Preserve table structure (detect Markdown tables)
- **Headings**: Always start chunk with heading context
- **Code blocks**: Keep code blocks intact (rare in rulebooks, but future-proof)

### 3.3 Contextual Overlap Strategy

**Problem**: Chunks in isolation lack context
**Solution**: Prepend previous chunk's last sentence to current chunk

**Example**:

**Without Overlap**:
```
Chunk 1: "...The king moves one square in any direction. The queen is the most powerful piece."
Chunk 2: "It can move any number of squares horizontally, vertically, or diagonally."
```
(Reader doesn't know "It" refers to the queen)

**With Overlap**:
```
Chunk 1: "...The king moves one square in any direction. The queen is the most powerful piece."
Chunk 2: "The queen is the most powerful piece. It can move any number of squares horizontally, vertically, or diagonally."
```
(Context preserved)

**Implementation**:
- Extract last complete sentence from previous chunk
- Prepend to current chunk
- Mark overlap region in metadata (for debugging)
- Typical overlap: 50-150 tokens (~1-2 sentences)

### 3.4 Configuration Schema

**File**: `apps/api/src/Api/appsettings.json`

```json
{
  "TextChunking": {
    "Strategy": "Semantic",  // "Semantic" or "Fixed" (backward compat)
    "TargetChunkSize": 800,   // Increased from 512
    "MinChunkSize": 400,      // Prevent tiny orphan chunks
    "MaxChunkSize": 1200,     // Hard limit for edge cases
    "OverlapTokens": 100,     // ~1-2 sentences
    "OverlapStrategy": "LastSentence",  // or "FixedTokens"
    "PreserveBoundaries": {
      "Paragraphs": true,
      "Sentences": true,
      "Lists": true,          // Keep numbered/bulleted lists together
      "Tables": true,
      "CodeBlocks": true
    },
    "SplitPriority": [
      "paragraph",    // Try splitting on \n\n first
      "sentence",     // Then on .\s
      "clause",       // Then on ,;:
      "word"          // Finally on word boundaries
    ]
  }
}
```

### 3.5 Implementation Architecture

```
┌────────────────────────────────────────────┐
│         IChunkingStrategy (interface)      │
├────────────────────────────────────────────┤
│  + ChunkText(text, config) → Chunk[]      │
│  + GetChunkMetadata(chunk) → Metadata     │
└────────────────────────────────────────────┘
         △                        △
         │                        │
    ┌────┴─────┐         ┌────────┴─────────┐
    │          │         │                  │
┌───┴─────────────┐  ┌──┴──────────────────────┐
│ FixedSize       │  │ SemanticChunking        │
│ ChunkingStrategy│  │ Strategy (NEW)          │
├─────────────────┤  ├─────────────────────────┤
│ (Current impl)  │  │ + SplitByParagraphs()   │
│ 512 chars       │  │ + SplitBySentences()    │
│ 50 char overlap │  │ + SplitByClauses()      │
└─────────────────┘  │ + PreserveLists()       │
                     │ + AddContextualOverlap()│
                     └─────────────────────────┘
                              │
                              ▼
                     ┌─────────────────────────┐
                     │ TextChunkingService     │
                     ├─────────────────────────┤
                     │ - IChunkingStrategy     │
                     │ + ChunkTextAsync()      │
                     │   └─ Delegates to       │
                     │      selected strategy  │
                     └─────────────────────────┘
```

**Strategy Selection** (DI in `Program.cs`):
```csharp
services.AddSingleton<IChunkingStrategy>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var strategy = config["TextChunking:Strategy"];

    return strategy switch
    {
        "Semantic" => new SemanticChunkingStrategy(config),
        "Fixed" => new FixedSizeChunkingStrategy(config),
        _ => throw new InvalidOperationException($"Unknown strategy: {strategy}")
    };
});
```

### 3.6 Migration Strategy

**Problem**: Existing PDFs indexed with fixed-size chunks
**Solution**: Provide migration script for re-indexing

**Migration Script**: `tools/reindex-pdfs.ps1`
```powershell
# Re-index all PDFs with new chunking strategy
param(
    [switch]$DryRun,
    [string]$GameId = $null  # Optional: re-index specific game
)

# 1. Backup current Qdrant collections
# 2. Delete old chunks from DB (vector_documents table)
# 3. Re-run PDF processing pipeline with new chunking
# 4. Validate chunk count and quality
# 5. Run RAG evaluation to compare metrics
```

**Rollback Plan**:
- Keep old chunks in DB (soft delete with `is_active=false`)
- Toggle between old/new chunks via feature flag
- If evaluation shows regression, rollback to fixed-size

### 3.7 Testing Strategy

**Unit Tests** (`SemanticChunkingStrategyTests.cs`):
- Edge cases: single-sentence paragraphs, very long sentences, no paragraph breaks
- List preservation: numbered lists, bulleted lists, nested lists
- Overlap validation: last sentence extraction, prepending logic
- Size constraints: min/max chunk size enforcement

**Integration Tests** (`TextChunkingServiceTests.cs`):
- Real rulebook excerpts (Chess, Tic-Tac-Toe)
- Compare chunk boundaries: fixed vs. semantic
- Measure chunk coherence (readability scores)
- Performance: chunking speed <500ms per page

**Evaluation Tests**:
- Re-index Chess rulebook with semantic chunking
- Run RAG evaluation on 24-query test set
- Compare P@K, Recall@K with baseline
- Validate no regression in latency

---

## 4. Optimization #3: LLM-Based Query Expansion

**Issue**: #470
**Effort**: 5-6 days
**Cost**: $2-3/month
**Expected Gain**: +8-12% Recall

### 4.1 Design Rationale

**Problem Examples**:
| Short Query | Missing Context | Retrieval Issue |
|-------------|-----------------|-----------------|
| "castling" | Doesn't mention "king" or "rook" | Misses chunks about king-rook movement |
| "check" | Ambiguous: "check" vs "checkmate" | Retrieves irrelevant chunks |
| "pawn promotion" | British: "pawn" vs American: "piece" | Synonym mismatch |

**Research Evidence**:
- Query expansion increases Recall@10 by 10-15% in IR benchmarks ([Nogueira et al., 2019](https://arxiv.org/abs/1904.08375))
- LLM-based expansion outperforms WordNet/thesaurus by 20% ([Jagerman et al., 2023](https://arxiv.org/abs/2305.03653))
- Multi-query retrieval with score fusion improves nDCG by 12% ([Weaviate Blog, 2024](https://weaviate.io/blog/hybrid-search-fusion-algorithms))

### 4.2 Query Expansion Workflow

```
┌───────────────┐
│  User Query   │
│  "castling"   │
└───────┬───────┘
        │
        ▼
┌─────────────────────────────────────────────┐
│    QueryExpansionService                    │
├─────────────────────────────────────────────┤
│                                             │
│ 1. Check Redis cache for expansion         │
│    Key: "qe:hash(castling)"                │
│    └─ CACHE HIT → return cached variants   │
│    └─ CACHE MISS → proceed to LLM         │
│                                             │
│ 2. Prompt LLM (Claude Haiku - cheap):      │
│    "Expand this board game question with   │
│     2-3 synonymous variations:             │
│     Original: castling"                     │
│                                             │
│ 3. Parse LLM response:                     │
│    - "castling"                            │
│    - "king-rook special move"              │
│    - "castle move in chess"                │
│                                             │
│ 4. Store in Redis cache (TTL: 1 hour)     │
└───────────┬─────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────┐
│       Multi-Query Retrieval                 │
├─────────────────────────────────────────────┤
│                                             │
│ For each query variant:                    │
│   1. Generate embedding                     │
│   2. Search Qdrant (top-20 per variant)    │
│                                             │
│ Results:                                    │
│   Query 1: [chunk_A:0.89, chunk_B:0.85...] │
│   Query 2: [chunk_C:0.91, chunk_A:0.80...] │
│   Query 3: [chunk_B:0.88, chunk_D:0.82...] │
└───────────┬─────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────┐
│   Reciprocal Rank Fusion (RRF)             │
├─────────────────────────────────────────────┤
│                                             │
│ RRF Score = Σ (1 / (k + rank_i))          │
│   where k=60 (standard constant)           │
│                                             │
│ chunk_A: 1/(60+1) + 1/(60+5) = 0.0313     │
│ chunk_B: 1/(60+2) + 1/(60+1) = 0.0323     │
│ chunk_C: 1/(60+1) = 0.0164                │
│ chunk_D: 1/(60+7) = 0.0149                │
│                                             │
│ Merged ranking:                             │
│   1. chunk_B (0.0323)                      │
│   2. chunk_A (0.0313)                      │
│   3. chunk_C (0.0164)                      │
│   4. chunk_D (0.0149)                      │
│                                             │
│ Return top-10 to QA service                │
└─────────────────────────────────────────────┘
```

### 4.3 LLM Expansion Prompt

**Model**: `anthropic/claude-3-haiku` (cheap, fast)

**Prompt**:
```
You are a query expansion expert for a board game rules assistant.

Your task: Generate 2-3 alternative phrasings of the user's question that preserve the
original intent but use different vocabulary or phrasing.

Guidelines:
- Include synonyms (e.g., "piece" → "figure", "win" → "victory")
- Expand abbreviations (e.g., "Q" → "Queen")
- Add context if query is ambiguous (e.g., "check" → "putting king in check")
- Keep alternatives concise (max 15 words each)
- Do NOT change the question's core meaning

Original Question: {query}

Output Format:
1. [original query]
2. [variation 1]
3. [variation 2]

Examples:

Original: "castling"
1. castling
2. king-rook special move
3. castle move in chess

Original: "pawn promotion"
1. pawn promotion
2. promoting a pawn to another piece
3. pawn reaching the opposite end

Original: "check"
1. check
2. putting the opponent's king in check
3. attacking the king

Your Output:
```

**Response Parsing**:
- Extract numbered list
- Validate each variant (min 2 words, max 15 words)
- Fallback: Use original query only if LLM fails

### 4.4 Redis Caching Strategy

**Cache Key Format**: `qe:{sha256(query)}`

**Cache Structure**:
```json
{
  "original_query": "castling",
  "variations": [
    "castling",
    "king-rook special move",
    "castle move in chess"
  ],
  "generated_at": "2025-01-18T10:30:00Z",
  "model": "anthropic/claude-3-haiku",
  "tokens_used": 85
}
```

**TTL Strategy**:
- Common queries (high frequency): 24 hours
- Uncommon queries: 1 hour
- Adaptive TTL based on cache hit rate (future optimization)

**Cache Hit Rate Target**: >60% (most users ask similar questions)

### 4.5 Reciprocal Rank Fusion (RRF)

**Algorithm** ([Cormack et al., 2009](https://plg.uwaterloo.ca/~gvcormac/cormacksigir09-rrf.pdf)):
```
RRF(chunk_d) = Σ_{q ∈ queries} (1 / (k + rank_q(d)))

where:
  - k = 60 (standard constant, balances score distribution)
  - rank_q(d) = rank of document d in results for query q
  - Σ sums across all query variations
```

**Why RRF over other fusion methods?**
- **Simple**: No parameter tuning required
- **Effective**: Outperforms weighted sum in most IR benchmarks
- **Robust**: Handles score scale differences across queries
- **Fast**: O(n log n) complexity for n results

**Alternative Considered**: CombSUM (weighted score combination)
- Rejected because: Requires score normalization, sensitive to outliers

### 4.6 Configuration Schema

**File**: `apps/api/src/Api/appsettings.json`

```json
{
  "QueryExpansion": {
    "Enabled": true,
    "Model": "anthropic/claude-3-haiku",
    "MaxVariations": 3,
    "MinQueryLength": 5,        // Don't expand very short queries
    "MaxQueryLength": 100,      // Truncate very long queries
    "FusionMethod": "RRF",      // "RRF" or "CombSUM"
    "RrfConstant": 60,          // k parameter in RRF formula
    "TopKPerQuery": 20,         // Retrieve 20 chunks per variant
    "FinalTopK": 10,            // Return 10 chunks after fusion
    "Caching": {
      "Enabled": true,
      "TtlSeconds": 3600,       // 1 hour default
      "MaxCacheSize": 10000,    // Max cached expansions
      "KeyPrefix": "qe"
    },
    "CostControl": {
      "MaxTokensPerExpansion": 150,
      "MonthlyBudgetUsd": 5.0,
      "AlertThresholdUsd": 4.0
    }
  }
}
```

### 4.7 Cost Analysis

**Model Pricing** (Claude Haiku via OpenRouter):
- Input: $0.25 per 1M tokens
- Output: $1.25 per 1M tokens

**Per-Query Costs**:
```
Prompt: ~50 tokens (expansion instructions + query)
Response: ~100 tokens (3 variations)
Total: 150 tokens/query

Cost per query:
  = (50 × $0.25/1M) + (100 × $1.25/1M)
  = $0.0000125 + $0.0000125
  = $0.000025 (2.5¢ per 1000 queries)
```

**Monthly Cost Projections**:
| Queries/Month | Cache Hit Rate | LLM Calls | Cost |
|---------------|----------------|-----------|------|
| 500 | 0% | 500 | $0.01 |
| 1,000 | 50% | 500 | $0.01 |
| 1,000 | 0% | 1,000 | $0.03 |
| 5,000 | 60% | 2,000 | $0.05 |
| 10,000 | 60% | 4,000 | $0.10 |
| 50,000 | 70% | 15,000 | $0.38 |
| 100,000 | 75% | 25,000 | $0.63 |

**Conservative Estimate**: $2-3/month assumes:
- 10,000-20,000 queries/month
- 60% cache hit rate
- Includes buffer for cache misses and testing

### 4.8 Implementation Architecture

```
┌────────────────────────────────────────────┐
│      QueryExpansionService                 │
├────────────────────────────────────────────┤
│ - ILlmService llmService                   │
│ - IDistributedCache cache (Redis)         │
│ - ILogger logger                           │
│ - QueryExpansionConfig config             │
│                                            │
│ + ExpandQueryAsync(query)                 │
│   → string[] variations                   │
│                                            │
│ + GetCachedExpansion(query)               │
│   → string[]? (null if cache miss)        │
│                                            │
│ + GenerateExpansionWithLlm(query)         │
│   → string[] (calls LLM)                  │
│                                            │
│ + CacheExpansion(query, variations)       │
│   → void (stores in Redis)                │
└────────────────────────────────────────────┘
         │
         ▼
┌────────────────────────────────────────────┐
│          RagService (Modified)             │
├────────────────────────────────────────────┤
│                                            │
│ SearchAsync(gameId, query, topK):         │
│                                            │
│ 1. IF QueryExpansion.Enabled:             │
│    a. Expand query → [q1, q2, q3]        │
│    b. For each qi:                        │
│         - Generate embedding              │
│         - Search Qdrant (top-20)          │
│    c. Merge results with RRF              │
│    d. Return top-10 fused chunks          │
│                                            │
│ 2. ELSE (expansion disabled):             │
│    a. Search with original query          │
│    b. Return top-10 chunks                │
└────────────────────────────────────────────┘
```

**DI Registration** (`Program.cs`):
```csharp
// Query expansion service
services.AddSingleton<IQueryExpansionService, QueryExpansionService>();

// Redis distributed cache (already configured)
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["REDIS_URL"];
});
```

### 4.9 Monitoring & Observability

**Custom Metrics** (added to `MeepleAiMetrics.cs`):
```csharp
// Query expansion metrics
public static readonly Counter<long> QueryExpansionsTotal =
    Meter.CreateCounter<long>("meepleai.query_expansion.total",
        description: "Total query expansions attempted");

public static readonly Counter<long> QueryExpansionCacheHits =
    Meter.CreateCounter<long>("meepleai.query_expansion.cache_hits",
        description: "Query expansion cache hits");

public static readonly Counter<long> QueryExpansionLlmCalls =
    Meter.CreateCounter<long>("meepleai.query_expansion.llm_calls",
        description: "LLM calls for query expansion");

public static readonly Histogram<double> QueryExpansionTokens =
    Meter.CreateHistogram<double>("meepleai.query_expansion.tokens",
        description: "Tokens used per query expansion");

public static readonly Histogram<double> QueryExpansionCostUsd =
    Meter.CreateHistogram<double>("meepleai.query_expansion.cost_usd",
        description: "Cost per query expansion in USD");
```

**Grafana Dashboard Panel**:
- Query expansion rate (enabled vs. disabled queries)
- Cache hit rate (target: >60%)
- LLM call rate and token usage
- Monthly cost projection
- Expansion latency (target: <300ms p95)

**Alerts**:
- Monthly cost exceeds $5 → Send email to ops team
- Cache hit rate drops below 40% → Investigate cache eviction
- Expansion latency p95 > 500ms → Consider caching strategy

### 4.10 Testing Strategy

**Unit Tests** (`QueryExpansionServiceTests.cs`):
- Cache hit/miss scenarios
- LLM response parsing (valid, malformed, empty)
- Cost calculation accuracy
- Configuration validation (enabled/disabled, max variations)

**Integration Tests** (`QueryExpansionIntegrationTests.cs`):
- Real LLM calls with Claude Haiku
- Redis cache read/write/invalidation
- TTL expiration behavior
- Concurrent cache access (thread safety)

**RRF Tests** (`RagServiceTests.cs` updates):
- Score fusion correctness (manual calculation vs. implementation)
- Deduplication (same chunk from multiple queries)
- Ranking stability (deterministic results)

**Cost Tests**:
- Mock 1000 queries, measure total tokens
- Validate cost projection formula
- Test monthly budget enforcement (fail-safe: disable expansion if over budget)

**Evaluation Tests**:
- Run RAG evaluation with expansion enabled/disabled
- Compare Recall@K (expect +8-12% improvement)
- Measure latency impact (expect +200-300ms due to multi-query)

---

## 5. Evaluation & Metrics

### 5.1 Baseline Metrics (AI-06 Current)

From `tests/Api.Tests/TestData/rag-evaluation-dataset.json` (24 queries):

| Metric | Value | Notes |
|--------|-------|-------|
| Precision@1 | 0.75 | 18/24 queries have relevant chunk in top-1 |
| Precision@3 | 0.72 | |
| **Precision@5** | **0.70** | **Primary success metric** |
| Precision@10 | 0.68 | |
| Recall@5 | 0.65 | Many relevant chunks missed |
| Recall@10 | 0.78 | |
| **MRR** | **0.60** | **Primary ranking metric** |
| Latency p50 | 850ms | |
| Latency p95 | 1,800ms | |
| Latency p99 | 2,100ms | |
| Success Rate | 96% | 23/24 queries succeeded |

### 5.2 Target Metrics (Phase 1 Goals)

| Metric | Baseline | Target | Improvement | Primary Driver |
|--------|----------|--------|-------------|----------------|
| **P@5** | 0.70 | **0.90** | **+28%** | Semantic chunking + Query expansion |
| **MRR** | 0.60 | **0.75** | **+25%** | Prompt engineering + Query expansion |
| Recall@10 | 0.78 | **0.88** | **+13%** | Query expansion (multi-query retrieval) |
| Latency p95 | 1,800ms | **≤2,000ms** | **0%** | (Maintain, accept +200ms for expansion) |
| Success Rate | 96% | **≥95%** | **0%** | (Maintain) |

### 5.3 Semantic Quality Metrics (New in AI-07)

Beyond IR metrics, add semantic evaluation using sentence transformers:

| Metric | Definition | Target | How to Measure |
|--------|-----------|--------|----------------|
| **Answer Faithfulness** | Generated answer grounded in RAG context | >0.85 | Cosine similarity between answer and concatenated chunks |
| **Answer Relevancy** | Answer addresses user question | >0.80 | Cosine similarity between answer and question |
| **Contextual Precision** | Relevant chunks ranked higher | >0.75 | Inverse of average rank of relevant chunks |
| **Contextual Recall** | All relevant chunks retrieved | >0.70 | % of ground truth chunks in top-10 |

**Implementation**:
```csharp
public class SemanticEvaluationMetrics
{
    private readonly IEmbeddingService _embeddingService;

    public async Task<double> CalculateFaithfulness(string answer, List<string> chunks)
    {
        var answerEmbedding = await _embeddingService.GenerateEmbeddingAsync(answer);
        var contextEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            string.Join(" ", chunks)
        );
        return CosineSimilarity(answerEmbedding, contextEmbedding);
    }

    public async Task<double> CalculateRelevancy(string answer, string question)
    {
        var answerEmbedding = await _embeddingService.GenerateEmbeddingAsync(answer);
        var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question);
        return CosineSimilarity(answerEmbedding, questionEmbedding);
    }
}
```

### 5.4 Evaluation Workflow

**Automated CI Evaluation** (`.github/workflows/ci.yml`):
```yaml
- name: Run RAG Evaluation
  run: |
    dotnet test --filter "FullyQualifiedName~RagEvaluationIntegrationTests" \
      --logger "console;verbosity=detailed" \
      /p:CollectCoverage=false

- name: Generate Evaluation Report
  run: |
    # Upload markdown report as artifact
    # Fail build if metrics below thresholds
```

**Manual Evaluation** (during development):
```bash
# Run evaluation with specific chunking strategy
cd apps/api
dotnet test --filter "RagEvaluationIntegrationTests" -- \
  TextChunking__Strategy=Semantic

# Generate detailed report
pwsh tools/run-rag-evaluation.ps1 -GenerateReport -OutputPath ./eval-report.md
```

### 5.5 A/B Testing Strategy

**Goal**: Validate improvements in production with real users

**Implementation**:
1. **Feature Flags** (via `appsettings.json` or LaunchDarkly):
   ```json
   {
     "FeatureFlags": {
       "PromptEngineering": { "Enabled": true, "Rollout": 100 },
       "SemanticChunking": { "Enabled": true, "Rollout": 50 },
       "QueryExpansion": { "Enabled": true, "Rollout": 25 }
     }
   }
   ```

2. **User Assignment**:
   - Hash user ID → assign to control (baseline) or treatment (Phase 1)
   - Track variant in AI request logs (`variant` column)

3. **Metrics Collection**:
   - Log user feedback (thumbs up/down on answers)
   - Track session length, queries per session
   - Measure answer acceptance rate (user doesn't rephrase)

4. **Analysis** (after 2 weeks, min 1000 queries per variant):
   - Compare answer quality ratings (treatment vs. control)
   - Statistical significance test (t-test, p<0.05)
   - Latency impact analysis (acceptable if <20% regression)

---

## 6. Implementation Plan

### 6.1 Phase 1A: Prompt Engineering (Week 1)

**Days 1-2**: Design & Configuration
- [ ] Create `PromptTemplateService.cs` with template loading
- [ ] Define template schema in `appsettings.json`
- [ ] Write 15+ few-shot examples (5 per game: Chess, Tic-Tac-Toe, generic)
- [ ] Implement question classification (keywords → category)

**Days 3-4**: Integration & Testing
- [ ] Integrate `PromptTemplateService` into `StreamingQaService`
- [ ] Write 20+ unit tests (`PromptTemplateServiceTests.cs`)
- [ ] Update integration tests with new prompts
- [ ] Run RAG evaluation: measure P@K, MRR improvement

**Deliverable**: PR #1 with prompt engineering implementation

---

### 6.2 Phase 1B: Semantic Chunking (Week 2)

**Days 1-2**: Algorithm Implementation
- [ ] Create `IChunkingStrategy` interface
- [ ] Implement `SemanticChunkingStrategy` with recursive splitting
- [ ] Add list/table/heading preservation logic
- [ ] Implement contextual overlap (last sentence prepending)

**Days 3-4**: Testing & Migration
- [ ] Write 25+ unit tests (`SemanticChunkingStrategyTests.cs`)
- [ ] Create migration script `tools/reindex-pdfs.ps1`
- [ ] Re-index Chess + Tic-Tac-Toe PDFs in test environment
- [ ] Run RAG evaluation: validate +10-15% improvement
- [ ] Document migration guide in `docs/migrations/`

**Day 5**: Rollout
- [ ] Deploy to staging with feature flag (50% traffic)
- [ ] Monitor chunk quality and retrieval metrics
- [ ] Gradual rollout to 100% if metrics improve

**Deliverable**: PR #2 with semantic chunking + migration guide

---

### 6.3 Phase 1C: Query Expansion (Week 3)

**Days 1-2**: Core Service
- [ ] Create `QueryExpansionService.cs` with LLM integration
- [ ] Implement Redis caching (key format, TTL strategy)
- [ ] Add cost tracking and budget enforcement
- [ ] Write expansion prompt and response parser

**Days 3-4**: RagService Integration
- [ ] Modify `RagService.SearchAsync()` for multi-query retrieval
- [ ] Implement RRF score fusion algorithm
- [ ] Add deduplication logic (same chunk from multiple queries)
- [ ] Write 20+ unit tests (`QueryExpansionServiceTests.cs`)

**Days 5-6**: Testing & Monitoring
- [ ] Integration tests with real LLM and Redis
- [ ] Cost estimation tests (mock 1000 queries)
- [ ] Add Grafana dashboard panels for expansion metrics
- [ ] Set up alerts (monthly budget, cache hit rate)
- [ ] Run RAG evaluation: validate +8-12% Recall improvement

**Deliverable**: PR #3 with query expansion + monitoring

---

### 6.4 Phase 1D: Evaluation & Documentation (Ongoing)

**Parallel to 1A-1C**:
- [ ] Update `RagEvaluationService` with semantic metrics
- [ ] Create A/B testing infrastructure (feature flags)
- [ ] Write technical documentation (`docs/technic/ai-07-rag-optimization-phase1.md`)
- [ ] Create migration guide for semantic chunking
- [ ] Update API docs (Swagger annotations for new configs)

**After PRs Merged**:
- [ ] Run comprehensive evaluation (all optimizations enabled)
- [ ] Generate benchmark report (baseline vs. Phase 1)
- [ ] Update `docs/ai-06-rag-evaluation.md` with new metrics
- [ ] Create Phase 2 roadmap document

---

## 7. Risk Assessment & Mitigation

### 7.1 Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Prompt changes degrade quality** | Medium | High | A/B test with 25% rollout, rollback if metrics regress |
| **Semantic chunking breaks edge cases** | Medium | Medium | Comprehensive unit tests, fallback to fixed-size |
| **Query expansion increases latency** | High | Medium | Cache expansions (60%+ hit rate), monitor p95 latency |
| **LLM costs exceed budget** | Low | Medium | Enforce $5/month cap, disable expansion if exceeded |
| **Migration corrupts existing data** | Low | High | Soft delete old chunks, rollback script, dry-run mode |

### 7.2 Operational Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Redis cache downtime** | Low | Medium | Graceful degradation (skip expansion), monitor cache health |
| **OpenRouter API rate limits** | Low | Medium | Implement exponential backoff, fallback to no expansion |
| **Qdrant re-indexing downtime** | Low | High | Blue-green deployment (keep old chunks active during migration) |
| **Increased infrastructure costs** | Medium | Low | Monitor monthly costs, set budget alerts |

### 7.3 Rollback Plan

**If metrics regress after deployment**:

1. **Immediate** (< 5 minutes):
   - Disable feature flags in `appsettings.json`
   - Restart API pods to load old config

2. **Short-term** (< 1 hour):
   - Revert PR via Git revert
   - Redeploy previous version
   - Restore old Qdrant chunks (if semantic chunking)

3. **Post-mortem** (< 1 week):
   - Analyze evaluation logs to identify root cause
   - Fix issues in development environment
   - Re-test before next deployment attempt

---

## 8. Success Criteria & KPIs

### 8.1 Technical Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Precision@5 | 0.70 | **≥0.90** | RAG evaluation test suite |
| MRR | 0.60 | **≥0.75** | RAG evaluation test suite |
| Recall@10 | 0.78 | **≥0.88** | RAG evaluation test suite |
| Latency p95 | 1,800ms | **≤2,000ms** | Prometheus metrics |
| Success Rate | 96% | **≥95%** | RAG evaluation test suite |
| Answer Faithfulness | N/A | **≥0.85** | Semantic evaluation (new) |
| Answer Relevancy | N/A | **≥0.80** | Semantic evaluation (new) |

### 8.2 User Experience Metrics (A/B Test)

| Metric | Control | Treatment | Target Lift |
|--------|---------|-----------|-------------|
| Answer Satisfaction (👍 rate) | Baseline | TBD | **+15%** |
| Query Reformulation Rate | Baseline | TBD | **-20%** (fewer rephrases) |
| Session Length (avg) | Baseline | TBD | **+10%** (more engagement) |
| Queries per Session | Baseline | TBD | **+5%** |

### 8.3 Operational Metrics

| Metric | Target | Alerts |
|--------|--------|--------|
| Monthly LLM Cost | **<$5** | Alert if >$4, disable expansion if >$5 |
| Query Expansion Cache Hit Rate | **>60%** | Alert if <40% |
| Semantic Chunking Performance | **<500ms per page** | Alert if p95 >1s |
| Qdrant Re-indexing Time | **<2 hours** (Chess + Tic-Tac-Toe) | Manual monitoring |

### 8.4 Definition of Success

**Phase 1 is successful if**:
1. ✅ P@5 ≥ 0.90 AND MRR ≥ 0.75 (technical metrics)
2. ✅ Latency p95 ≤ 2,000ms (performance maintained)
3. ✅ User satisfaction +10%+ in A/B test (UX improvement)
4. ✅ Monthly costs <$5 (budget maintained)
5. ✅ No critical bugs or data corruption (reliability)

**If any criteria fails**: Iterate on specific optimization, do NOT proceed to Phase 2.

---

## 9. Future Work (Phase 2 & Beyond)

### 9.1 Phase 2: Hybrid Search & Reranking (Month 3-4)

**Estimated Impact**: +20-30% Recall, +15-25% Precision@K

1. **Hybrid Search (BM25 + Dense Vectors)**
   - Qdrant sparse vector support
   - Weight tuning (0.7 dense + 0.3 BM25)
   - Effort: 7-10 days

2. **Cohere Rerank API**
   - Cross-encoder reranking of top-20 results
   - Cost: ~$1/1000 queries
   - Effort: 5-7 days

### 9.2 Phase 3: Advanced Semantic Evaluation (Month 5)

**Estimated Impact**: Faithfulness >0.90, Reduce hallucinations

1. **Ragas Framework Integration**
   - Faithfulness, Answer Relevancy, Context Precision/Recall
   - LLM-as-a-judge evaluation
   - Effort: 5-7 days

2. **Hallucination Detection**
   - Compare answer spans to RAG chunks (token overlap)
   - Flag unsupported claims
   - Effort: 3-5 days

### 9.3 Phase 4: Experimental Techniques (Month 6)

**Estimated Impact**: Incremental (+5-10%), exploratory

1. **HyDE (Hypothetical Document Embeddings)**
   - LLM generates hypothetical answer → embed → search
   - Effort: 4-6 days

2. **Contextual Chunking (Anthropic Style)**
   - Prepend chunk with document summary
   - Effort: 5-7 days

3. **Multi-Embedding Strategy**
   - Ensemble of multiple embedding models
   - Task-specific embeddings (setup vs. gameplay)
   - Effort: 7-10 days

### 9.4 Fine-Tuning (Only if Phase 1-3 Insufficient)

**Estimated Cost**: $5K-15K (data annotation + compute)
**Estimated Impact**: +10-20% (diminishing returns)
**Effort**: 3-6 months

**Not recommended** unless RAG optimizations plateau and user needs justify cost.

---

## 10. References & Resources

### 10.1 Research Papers

1. **Brown et al. (2020)**: "Language Models are Few-Shot Learners" (GPT-3 paper)
   - https://arxiv.org/abs/2005.14165
   - Key insight: Few-shot examples improve performance by 15-20%

2. **Wei et al. (2022)**: "Chain-of-Thought Prompting Elicits Reasoning in LLMs"
   - https://arxiv.org/abs/2201.11903
   - Key insight: Step-by-step reasoning improves complex Q&A

3. **Cormack et al. (2009)**: "Reciprocal Rank Fusion outperforms Condorcet and individual rank learning"
   - https://plg.uwaterloo.ca/~gvcormac/cormacksigir09-rrf.pdf
   - Key insight: RRF is simple, effective, and robust

4. **Nogueira et al. (2019)**: "Document Expansion by Query Prediction"
   - https://arxiv.org/abs/1904.08375
   - Key insight: Query expansion increases Recall by 10-15%

5. **Jagerman et al. (2023)**: "Query Expansion by Prompting Large Language Models"
   - https://arxiv.org/abs/2305.03653
   - Key insight: LLM-based expansion outperforms traditional methods by 20%

### 10.2 Technical Blogs

1. **LangChain**: "Improving Document Retrieval with Semantic Chunking"
   - https://blog.langchain.dev/improving-document-retrieval-with-semantic-chunking/
   - RecursiveCharacterTextSplitter algorithm

2. **Anthropic**: "Contextual Retrieval"
   - https://www.anthropic.com/news/contextual-retrieval
   - Prepending context to chunks improves recall by 15-20%

3. **Weaviate**: "Hybrid Search Fusion Algorithms"
   - https://weaviate.io/blog/hybrid-search-fusion-algorithms
   - Comparison of RRF, CombSUM, and other fusion methods

4. **OpenAI**: "Prompt Engineering Guide"
   - https://platform.openai.com/docs/guides/prompt-engineering
   - Best practices for prompt design

### 10.3 Internal Documentation

- `docs/ai-06-rag-evaluation.md` - Current evaluation system
- `docs/observability.md` - Monitoring and logging (OPS-01)
- `docs/ops-02-opentelemetry-design.md` - Metrics and tracing (OPS-02)
- `docs/database-schema.md` - Database schema reference
- `schemas/README.md` - RuleSpec v0 schema

### 10.4 Related Issues

- #467: AI-07 Parent Epic
- #468: AI-07.1 Prompt Engineering
- #469: AI-07.2 Semantic Chunking
- #470: AI-07.3 Query Expansion
- #466: AI-06 RAG Evaluation System (baseline)

---

## Appendix A: Configuration Examples

### A.1 Complete appsettings.json (AI-07 Additions)

```json
{
  "RagPrompts": {
    "DefaultTemplate": "generic",
    "Templates": {
      "generic": {
        "SystemPrompt": "You are an expert board game rules assistant...",
        "FewShotExamples": [
          {
            "question": "How do I set up Chess?",
            "answer": "To set up Chess, place the board so each player has a white square on their right [p.5]...",
            "category": "setup"
          }
        ],
        "MaxExamples": 3,
        "IncludeChainOfThought": false
      }
    },
    "QuestionClassification": {
      "Enabled": true,
      "Keywords": {
        "setup": ["setup", "start", "begin", "initial"],
        "gameplay": ["move", "turn", "action", "play"],
        "winning": ["win", "victory", "checkmate"],
        "edge_case": ["special", "exception", "what if"]
      }
    }
  },
  "TextChunking": {
    "Strategy": "Semantic",
    "TargetChunkSize": 800,
    "MinChunkSize": 400,
    "MaxChunkSize": 1200,
    "OverlapTokens": 100,
    "OverlapStrategy": "LastSentence",
    "PreserveBoundaries": {
      "Paragraphs": true,
      "Sentences": true,
      "Lists": true,
      "Tables": true
    },
    "SplitPriority": ["paragraph", "sentence", "clause", "word"]
  },
  "QueryExpansion": {
    "Enabled": true,
    "Model": "anthropic/claude-3-haiku",
    "MaxVariations": 3,
    "MinQueryLength": 5,
    "FusionMethod": "RRF",
    "RrfConstant": 60,
    "TopKPerQuery": 20,
    "FinalTopK": 10,
    "Caching": {
      "Enabled": true,
      "TtlSeconds": 3600,
      "KeyPrefix": "qe"
    },
    "CostControl": {
      "MaxTokensPerExpansion": 150,
      "MonthlyBudgetUsd": 5.0,
      "AlertThresholdUsd": 4.0
    }
  }
}
```

### A.2 Feature Flags for A/B Testing

```json
{
  "FeatureFlags": {
    "PromptEngineering": {
      "Enabled": true,
      "Rollout": 100,
      "Description": "Advanced prompt templates with few-shot examples"
    },
    "SemanticChunking": {
      "Enabled": true,
      "Rollout": 50,
      "Description": "Adaptive semantic chunking vs. fixed-size"
    },
    "QueryExpansion": {
      "Enabled": true,
      "Rollout": 25,
      "Description": "LLM-based query expansion with RRF fusion"
    }
  }
}
```

---

## Appendix B: Example Evaluation Report

```markdown
# RAG Evaluation Report - Phase 1 (AI-07)

**Date**: 2025-01-25
**Dataset**: 24 queries (Chess + Tic-Tac-Toe)
**Configuration**: All Phase 1 optimizations enabled

## Summary

| Metric | Baseline (AI-06) | Phase 1 (AI-07) | Improvement |
|--------|------------------|-----------------|-------------|
| Precision@5 | 0.70 | **0.92** | **+31%** ✅ |
| MRR | 0.60 | **0.78** | **+30%** ✅ |
| Recall@10 | 0.78 | **0.89** | **+14%** ✅ |
| Latency p95 | 1,800ms | 1,950ms | +8% ✅ |
| Success Rate | 96% | 96% | 0% ✅ |

**Goals Met**: 5/5 ✅ **PHASE 1 SUCCESS**

## Breakdown by Optimization

| Optimization | P@5 Gain | MRR Gain | Notes |
|--------------|----------|----------|-------|
| Prompt Engineering | +12% | +18% | Few-shot examples improved answer formatting |
| Semantic Chunking | +14% | +8% | Better chunk boundaries improved retrieval |
| Query Expansion | +9% | +6% | Multi-query retrieval increased recall |
| **Combined** | **+31%** | **+30%** | Synergistic effects observed |

## Cost Analysis

- Query expansion: $2.30/month (11,500 queries)
- Cache hit rate: 64% (target: >60%) ✅
- LLM tokens used: 1.72M (within budget) ✅

## Recommendations

1. ✅ Deploy to production with 100% rollout
2. ✅ Proceed to Phase 2 (Hybrid Search + Reranking)
3. Monitor user satisfaction metrics for 2 weeks
4. Update quality gates: P@5 ≥ 0.90, MRR ≥ 0.75
```

---

## Document Changelog

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-01-18 | AI/RAG Team | Initial draft - Phase 1 design |

---

**End of Document**
