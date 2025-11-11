# MeepleAI System Architecture - Italian Board Game Rules Assistant

**Status**: Approved for Implementation
**Version**: 1.0
**Date**: 2025-01-15
**Architecture Owner**: Engineering Lead

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Architecture Principles](#architecture-principles)
3. [High-Level Architecture](#high-level-architecture)
4. [Component Architecture](#component-architecture)
5. [Data Flow](#data-flow)
6. [Technology Stack](#technology-stack)
7. [Security Architecture](#security-architecture)
8. [Deployment Architecture](#deployment-architecture)
9. [Architecture Decision Records (ADRs)](#architecture-decision-records)

---

## System Overview

### Purpose

MeepleAI è un sistema AI per assistenza su regolamenti di giochi da tavolo, con focus sul mercato italiano. Il sistema utilizza Retrieval Augmented Generation (RAG) per fornire risposte accurate (target >95%) basate su rulebook ufficiali, con citazioni esatte e gestione esplicita dell'incertezza.

### Core Requirements

**Functional**:
- Question answering su regolamenti board games (italiano primario, multilingua futuro)
- PDF upload e indicizzazione automatica
- Citation tracking (page number + text snippet)
- Explicit uncertainty handling (confidence scoring)

**Non-Functional**:
- **Accuracy**: >95% su golden dataset (1000 Q&A pairs)
- **Hallucination Rate**: <3% (zero fabricazioni critiche)
- **Latency**: P95 <3 secondi per query complesse
- **Uptime**: >99.5% SLA (production)
- **Scalability**: Support 10,000 MAU by Phase 4

### Key Constraints

1. **"One Mistake Ruins Session"**: Accuracy bar significantly higher than typical AI applications
2. **Cost Control**: LLM API costs scale with users (mitigation: semantic caching, smart routing)
3. **Open-Source Core**: RAG pipeline must be open-source (Apache 2.0), proprietary = licensed content + premium features
4. **Italian-First**: Multilingual architecture but Italian language/culture optimized from day 1

---

## Architecture Principles

### 1. Quality Over Speed

**Principle**: Correctness > Latency. Users prefer 3s accurate answer vs 1s wrong answer.

**Implications**:
- Multi-model validation (GPT-4 + Claude consensus) adds ~500ms but prevents hallucinations
- Citation verification required for every response (page number + text snippet matching)
- Confidence threshold enforced (≥0.70 or explicit uncertainty)

**Trade-offs Accepted**:
- Increased latency for complex queries (up to 4-5s for triple validation)
- Higher API costs (2x per query for dual LLM validation)

---

### 2. Defense-in-Depth for Hallucination Prevention

**Principle**: No single validation mechanism sufficient. Layer multiple independent checks.

**Five-Layer Defense**:
1. **Confidence Threshold**: Reject if <0.70 (explicit "Non ho informazioni sufficienti")
2. **Multi-Model Consensus**: GPT-4 + Claude responses must agree (cosine similarity ≥0.90)
3. **Citation Verification**: Page number exists + text snippet matches retrieved chunk
4. **Forbidden Keywords**: Detect fabricated terms (500+ blocklist from known hallucination cases)
5. **User Feedback**: Negative reports → admin review → blocklist update + regression test

**Failure Mode**: If any layer fails, escalate to next layer or return explicit uncertainty

---

### 3. Progressive Enhancement

**Principle**: Start simple, add complexity only when validated need exists.

**Phase Evolution**:
- **Phase 1 (MVP)**: Single LLM (GPT-4), basic confidence scoring, manual annotation
- **Phase 2 (Production)**: Dual LLM validation (GPT-4 + Claude), semantic caching, circuit breakers
- **Phase 3 (Advanced)**: Hybrid search (vector + keyword RRF), fine-tuned embeddings, A/B testing
- **Phase 4 (Platform)**: Multi-language support, API ecosystem, community contributions

**Anti-Pattern Avoided**: Building complex architecture before validating product-market fit

---

### 4. Open Core, Proprietary Premium

**Principle**: Core RAG pipeline open-source (community trust), premium features proprietary (sustainable monetization).

**Open-Source Components** (Apache 2.0):
- PDF processing pipeline (LLMWhisperer → SmolDocling → chunking)
- Embedding generation (multilingual-e5-large adapter)
- Vector search (Weaviate integration)
- Multi-model validation framework
- 5-metric testing & evaluation tools

**Proprietary Components** (Closed-Source):
- Publisher-licensed rulebook content (encrypted at rest)
- Fine-tuned Italian embeddings (trained on licensed corpus)
- Premium API rate limits & SLA guarantees
- White-label B2B customization
- Advanced analytics dashboards

---

### 5. Fail-Safe, Not Fail-Secure

**Principle**: When failures occur, degrade gracefully with explicit user communication.

**Failure Handling**:
- **LLM API Down**: Circuit breaker → fallback cache (semantic similarity ≥0.90) → different provider (Claude if GPT-4 down) → "Servizio temporaneamente non disponibile"
- **Vector DB Unavailable**: PostgreSQL FTS keyword fallback → cached results → "Modalità ridotta attiva. Accuracy potrebbe essere inferiore."
- **Low Confidence (<0.70)**: Explicit "Non ho informazioni sufficienti" + suggest consulting rulebook page X

**Anti-Pattern Avoided**: Silent failures or fabricated answers to maintain apparent functionality

---

## High-Level Architecture

### System Context Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         MEEPLEAI SYSTEM                             │
│                                                                     │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐        │
│  │   Web App    │    │  Mobile App  │    │   API        │        │
│  │  (Next.js)   │    │   (PWA +     │    │  Clients     │        │
│  │              │    │  Capacitor)  │    │  (3rd party) │        │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘        │
│         │                   │                    │                 │
│         └───────────────────┴────────────────────┘                 │
│                             │                                       │
│                             ▼                                       │
│                    ┌─────────────────┐                             │
│                    │   API Gateway   │                             │
│                    │  (FastAPI +     │                             │
│                    │   Rate Limit)   │                             │
│                    └────────┬────────┘                             │
│                             │                                       │
│         ┌───────────────────┼───────────────────┐                  │
│         │                   │                   │                  │
│         ▼                   ▼                   ▼                  │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐          │
│  │   Question   │   │  Rulebook    │   │    Admin     │          │
│  │   Answering  │   │  Indexing    │   │  Dashboard   │          │
│  │   Service    │   │  Service     │   │              │          │
│  └──────┬───────┘   └──────┬───────┘   └──────────────┘          │
│         │                   │                                       │
│         └───────────────────┘                                       │
│                     │                                               │
│         ┌───────────┼───────────────┐                              │
│         │           │               │                              │
│         ▼           ▼               ▼                              │
│  ┌──────────┐ ┌──────────┐  ┌──────────────┐                     │
│  │  Vector  │ │   LLM    │  │  PostgreSQL  │                     │
│  │    DB    │ │   APIs   │  │   (FTS +     │                     │
│  │ (Weaviate)│ │(GPT-4 +  │  │  Metadata)   │                     │
│  │          │ │ Claude)  │  │              │                     │
│  └──────────┘ └──────────┘  └──────────────┘                     │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

External Dependencies:
- OpenAI API (GPT-4 Turbo primary)
- Anthropic API (Claude 3.5 Sonnet validation)
- LLMWhisperer (PDF processing free tier)
```

### Logical Architecture Layers

```
┌───────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                         │
│  Web (Next.js) │ Mobile (PWA+Capacitor) │ Public API (REST)  │
└───────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌───────────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                           │
│  Q&A Service │ Indexing Service │ Validation Service         │
│  Citation Service │ Confidence Scoring │ Admin Service       │
└───────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌───────────────────────────────────────────────────────────────┐
│                     DOMAIN LAYER                              │
│  RAG Pipeline │ PDF Processing │ Embedding Generation        │
│  Multi-Model Validation │ Hallucination Detection            │
└───────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌───────────────────────────────────────────────────────────────┐
│                 INFRASTRUCTURE LAYER                          │
│  Vector DB (Weaviate) │ Cache (Redis) │ DB (PostgreSQL)      │
│  LLM APIs (OpenAI, Anthropic) │ Monitoring (Prometheus)      │
└───────────────────────────────────────────────────────────────┘
```

---

## Component Architecture

### 1. Question Answering Service

**Responsibilities**:
- Accept user question + game context
- Orchestrate RAG pipeline (retrieve → generate → validate)
- Return answer + confidence + citations + sources

**Key Components**:

#### 1.1 Query Processor
```python
class QueryProcessor:
    def process(self, question: str, game_id: str) -> ProcessedQuery:
        # Query normalization (lowercase, trim, remove special chars)
        normalized = self.normalize(question)

        # Language detection (Italian vs English)
        language = self.detect_language(normalized)

        # Query expansion (synonyms, related terms)
        expanded = self.expand_query(normalized, language)

        # Game context enrichment (retrieve game metadata)
        game_context = self.get_game_context(game_id)

        return ProcessedQuery(
            original=question,
            normalized=normalized,
            language=language,
            expanded_terms=expanded,
            game_context=game_context
        )
```

#### 1.2 Retrieval Service
```python
class RetrievalService:
    def retrieve(self, query: ProcessedQuery, top_k: int = 10) -> List[RetrievedChunk]:
        # Vector search (Weaviate)
        vector_results = self.vector_search.search(
            query_embedding=self.embedder.embed(query.normalized),
            game_id=query.game_context.id,
            top_k=top_k
        )

        # Keyword search (PostgreSQL FTS) - Phase 3+
        if self.hybrid_search_enabled:
            keyword_results = self.keyword_search.search(
                query_text=query.normalized,
                game_id=query.game_context.id,
                top_k=top_k
            )

            # RRF fusion (70% vector + 30% keyword)
            results = self.rrf_fusion(
                vector_results,
                keyword_results,
                k=60  # RRF parameter
            )
        else:
            results = vector_results

        # Filter by minimum similarity threshold
        filtered = [r for r in results if r.similarity >= 0.80]

        return filtered[:top_k]
```

#### 1.3 Generation Service
```python
class GenerationService:
    def generate(self, query: ProcessedQuery, context: List[RetrievedChunk]) -> GeneratedResponse:
        # Construct prompt with context
        prompt = self.prompt_builder.build(
            question=query.original,
            context_chunks=context,
            game_name=query.game_context.name,
            language=query.language
        )

        # Primary LLM call (GPT-4 Turbo)
        primary_response = self.llm_client.generate(
            prompt=prompt,
            model="gpt-4-turbo-2024-04-09",
            temperature=0.1,  # Low for determinism
            max_tokens=1024
        )

        # Parse structured response (answer + confidence + citations)
        parsed = self.response_parser.parse(primary_response)

        return GeneratedResponse(
            answer=parsed.answer,
            confidence=parsed.confidence,
            raw_citations=parsed.citations,
            primary_llm_response=primary_response
        )
```

#### 1.4 Validation Service
```python
class ValidationService:
    def validate(self,
                 query: ProcessedQuery,
                 generated: GeneratedResponse,
                 context: List[RetrievedChunk]) -> ValidatedResponse:

        validation_results = {}

        # Layer 1: Confidence Threshold
        validation_results['confidence_pass'] = generated.confidence >= 0.70

        # Layer 2: Multi-Model Consensus (if confidence marginal)
        if 0.70 <= generated.confidence < 0.85:
            validation_results['consensus'] = self.multi_model_consensus(
                query, generated, context
            )
        else:
            validation_results['consensus'] = True  # Skip if very confident

        # Layer 3: Citation Verification
        validation_results['citations_valid'] = self.verify_citations(
            generated.raw_citations, context
        )

        # Layer 4: Forbidden Keywords
        validation_results['no_hallucination_keywords'] = not self.detect_forbidden_keywords(
            generated.answer
        )

        # Aggregate validation
        all_pass = all(validation_results.values())

        if all_pass:
            return ValidatedResponse(
                answer=generated.answer,
                confidence=generated.confidence,
                citations=self.format_citations(generated.raw_citations),
                status='VALIDATED'
            )
        else:
            # Explicit uncertainty
            return ValidatedResponse(
                answer=self.uncertainty_message(query.language),
                confidence=generated.confidence,
                citations=[],
                status='UNCERTAIN',
                validation_failures=validation_results
            )

    def multi_model_consensus(self, query, generated, context) -> bool:
        # Validation LLM call (Claude 3.5 Sonnet)
        validation_prompt = self.prompt_builder.build_validation(
            question=query.original,
            primary_answer=generated.answer,
            context_chunks=context
        )

        validation_response = self.claude_client.generate(
            prompt=validation_prompt,
            model="claude-3-5-sonnet-20240620",
            temperature=0.1,
            max_tokens=1024
        )

        # Calculate semantic similarity between responses
        similarity = self.calculate_cosine_similarity(
            self.embedder.embed(generated.answer),
            self.embedder.embed(validation_response)
        )

        # Consensus threshold
        return similarity >= 0.90
```

---

### 2. Rulebook Indexing Service

**Responsibilities**:
- Accept PDF upload
- Extract text (multi-stage fallback: LLMWhisperer → SmolDocling → dots.ocr)
- Chunk text (sentence-aware, 256-768 chars)
- Generate embeddings (multilingual-e5-large)
- Index to vector DB (Weaviate)
- Store metadata (PostgreSQL)

**Key Components**:

#### 2.1 PDF Processing Pipeline
```python
class PdfProcessingPipeline:
    def process(self, pdf_path: str, game_id: str) -> ProcessingResult:
        # Stage 1: LLMWhisperer (preserves layout)
        try:
            text = self.llm_whisperer.extract(pdf_path)
            method = 'llmwhisperer'
        except Exception as e:
            logger.warning(f"LLMWhisperer failed: {e}, falling back to SmolDocling")

            # Stage 2: SmolDocling (vision-language model)
            try:
                text = self.smol_docling.extract(pdf_path)
                method = 'smoldocling'
            except Exception as e2:
                logger.warning(f"SmolDocling failed: {e2}, falling back to dots.ocr")

                # Stage 3: dots.ocr (multilingual OCR)
                text = self.dots_ocr.extract(pdf_path)
                method = 'dots_ocr'

        # Validate extraction quality
        if len(text) < 100:
            raise InsufficientTextError("Extracted text too short, PDF may be corrupted")

        return ProcessingResult(
            text=text,
            page_count=self.get_page_count(pdf_path),
            method=method,
            quality_score=self.assess_quality(text)
        )
```

#### 2.2 Text Chunking Service
```python
class TextChunkingService:
    def chunk(self, text: str, game_id: str) -> List[TextChunk]:
        # Sentence tokenization (NLTK for Italian)
        sentences = self.sentence_tokenizer.tokenize(text)

        chunks = []
        current_chunk = []
        current_length = 0

        for sentence in sentences:
            sentence_length = len(sentence)

            # If adding sentence exceeds max chunk size
            if current_length + sentence_length > self.max_chunk_size:
                # Save current chunk
                if current_chunk:
                    chunks.append(TextChunk(
                        text=' '.join(current_chunk),
                        game_id=game_id,
                        start_sentence_idx=len(chunks),
                        sentence_count=len(current_chunk)
                    ))

                # Start new chunk
                current_chunk = [sentence]
                current_length = sentence_length
            else:
                # Add sentence to current chunk
                current_chunk.append(sentence)
                current_length += sentence_length

        # Save last chunk
        if current_chunk:
            chunks.append(TextChunk(
                text=' '.join(current_chunk),
                game_id=game_id,
                start_sentence_idx=len(chunks),
                sentence_count=len(current_chunk)
            ))

        # Add overlap (20% of chunk size)
        overlapped_chunks = self.add_overlap(chunks, overlap_ratio=0.20)

        return overlapped_chunks
```

#### 2.3 Embedding Service
```python
class EmbeddingService:
    def __init__(self):
        # multilingual-e5-large (1024 dimensions)
        self.model = SentenceTransformer('intfloat/multilingual-e5-large')

        # Fine-tuned model path (Phase 3+)
        self.finetuned_model_path = None

    def embed_batch(self, texts: List[str]) -> np.ndarray:
        # Use fine-tuned model if available
        if self.finetuned_model_path and os.path.exists(self.finetuned_model_path):
            model = SentenceTransformer(self.finetuned_model_path)
        else:
            model = self.model

        # Generate embeddings (batch for efficiency)
        embeddings = model.encode(
            texts,
            batch_size=32,
            show_progress_bar=True,
            normalize_embeddings=True  # Cosine similarity
        )

        return embeddings
```

---

### 3. Admin Dashboard Service

**Responsibilities**:
- Manage games catalog (CRUD operations)
- Upload/delete rulebooks
- Monitor system metrics (accuracy, latency, uptime)
- A/B testing framework (Phase 3+)
- User management (Phase 2+)

**Key Features**:
- Real-time accuracy dashboard (5-metric framework)
- Alerting configuration (Prometheus rules)
- Query analytics (most common questions, low confidence queries)
- Publisher B2B analytics (query volume per game, revenue tracking)

---

## Data Flow

### Question Answering Flow (Happy Path)

```
1. User Input
   └─> Question: "Posso usare Standard Projects dopo aver passato?"
   └─> Game: "Terraforming Mars"

2. Query Processing
   └─> Normalize: "posso usare standard projects dopo aver passato"
   └─> Language: Italian (detected)
   └─> Game Context: { id: "tm", name: "Terraforming Mars", publisher: "FryxGames" }

3. Retrieval (Vector Search)
   └─> Embedding: [0.23, -0.15, 0.89, ...] (1024 dims)
   └─> Weaviate Query: { vector: [...], game_id: "tm", top_k: 10 }
   └─> Results: [
        { text: "Durante il proprio turno, prima di effettuare 1 o 2 azioni...", page: 8, similarity: 0.92 },
        { text: "Standard Projects possono essere usati qualsiasi numero di volte...", page: 8, similarity: 0.89 },
        ...
   ]

4. Generation (GPT-4 Turbo)
   └─> Prompt: "Sei un esperto di Terraforming Mars. Rispondi alla domanda basandoti sui seguenti estratti dal regolamento: [context]. Domanda: [question]"
   └─> Response: {
        answer: "No, Standard Projects possono essere usati solo durante il proprio turno, prima di passare.",
        confidence: 0.85,
        citations: [{ page: 8, snippet: "Durante il proprio turno, prima di effettuare..." }]
   }

5. Validation (Multi-Layer)
   ├─> Confidence Check: 0.85 >= 0.70 ✅
   ├─> Multi-Model Consensus: Claude validates (similarity: 0.93) ✅
   ├─> Citation Verification: Page 8 exists, snippet matches ✅
   ├─> Forbidden Keywords: None detected ✅
   └─> User Feedback: (post-response collection)

6. Response to User
   └─> Answer: "No, Standard Projects possono essere usati solo durante il proprio turno, prima di passare."
   └─> Confidence: 0.85 (displayed as badge: "Alta confidenza")
   └─> Citations: [📄 Regolamento pag. 8] (clickable → PDF viewer)
   └─> Feedback Prompt: "Questa risposta è stata utile? 👍 👎"
```

### Rulebook Indexing Flow

```
1. Upload
   └─> User uploads PDF: "terraforming_mars_italiano.pdf" (2.3 MB, 24 pages)
   └─> Server validates: File size ≤ 50 MB ✅, Extension .pdf ✅

2. PDF Processing (Multi-Stage Fallback)
   ├─> Stage 1: LLMWhisperer (success, 60s)
   │   └─> Text: "Regolamento di Terraforming Mars\n\nIntroduzione\nNel 2400..." (23,450 chars)
   │   └─> Quality Score: 0.92 (high)
   │
   └─> (Skip Stage 2/3, Stage 1 succeeded)

3. Text Chunking (Sentence-Aware)
   └─> Sentences: 487 (NLTK Italian tokenizer)
   └─> Chunks: 89 (avg 263 chars, 20% overlap)
   └─> Example Chunk: "Durante il proprio turno, prima di effettuare 1 o 2 azioni, il giocatore può utilizzare qualsiasi numero di Progetti Standard."

4. Embedding Generation
   └─> Model: multilingual-e5-large
   └─> Batch Size: 32 (3 batches for 89 chunks)
   └─> Embeddings: 89 × 1024 dimensions
   └─> Time: ~15 seconds

5. Vector DB Indexing (Weaviate)
   └─> Collection: "rulebook_chunks"
   └─> Metadata: { game_id: "tm", page: 8, chunk_idx: 12, language: "it" }
   └─> Batch Insert: 89 objects
   └─> Time: ~5 seconds

6. PostgreSQL Metadata Storage
   └─> Table: rulebooks
   └─> Record: {
        id: UUID,
        game_id: "tm",
        filename: "terraforming_mars_italiano.pdf",
        page_count: 24,
        chunk_count: 89,
        processing_method: "llmwhisperer",
        quality_score: 0.92,
        indexed_at: "2025-01-15T10:30:00Z"
   }

7. Notification
   └─> User: "Indicizzazione completata! 89 sezioni analizzate da Terraforming Mars."
   └─> Admin: Email notification (new rulebook indexed)
```

---

## Technology Stack

### Phase 1 (MVP)

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Backend Framework** | ASP.NET Core 9.0 (C#) | Existing system, DDD architecture, 90%+ test coverage, production-ready |
| **Frontend Framework** | Next.js 16 + React 19 | Latest stable, existing infrastructure, SSR/SSG capabilities |
| **PDF Processing** | LLMWhisperer (primary) | Layout preservation for LLM, free tier 100 pages/day |
| | SmolDocling (fallback) | Vision-language model (Python microservice), GPU-accelerated |
| | Docnet.Core + iText7 (final) | Existing implementation, proven fallback, table extraction |
| **Embeddings** | OpenRouter API (feature-flagged) | text-embedding-3-large via unified API |
| | Ollama nomic-embed-text (fallback) | Free self-hosted option, 768 dims |
| **Vector DB** | Qdrant (existing) | Already deployed, hybrid search support v1.7+, production-proven |
| **LLM Primary** | OpenRouter: gpt-4-turbo | Unified API, automatic fallback, cost tracking |
| **LLM Validation** | OpenRouter: claude-3.5-sonnet | Multi-model consensus via same API |
| **LLM Fallback** | Ollama: mistral:7b + llama3.1:8b | Free self-hosted, lower accuracy trade-off (75-80% vs 95%) |
| **Cache** | Redis HybridCache (existing) | L1 memory + L2 Redis, add semantic layer for LLM responses |
| **Database** | PostgreSQL 16 (existing) | ACID, EF Core 9.0, Full-Text Search for hybrid (Phase 3) |
| **Deployment** | Docker Compose (existing) | Proven local dev + staging, Kubernetes-ready |
| **Monitoring** | Prometheus + Grafana (existing) | OpenTelemetry, Seq, Jaeger already configured |

### Phase 2 (Production)

**Enhancements** (Building on Existing Infrastructure):
- **LLM Validation**: OpenRouter multi-model (GPT-4 + Claude + Gemini ensemble)
- **Cache**: Redis Cluster (3 nodes, HA) - already planned
- **Vector DB**: Qdrant optimization (hybrid search, Italian-specific collections)
- **Database**: PostgreSQL replication (already available in existing system)
- **Orchestration**: Kubernetes (AWS EKS or DigitalOcean)
- **IaC**: Terraform (infrastructure as code)
- **Monitoring**: Prometheus + Grafana + PagerDuty
- **Tracing**: OpenTelemetry → Jaeger
- **CDN**: Cloudflare (static assets, DDoS protection)

### Phase 3 (Advanced)

**Additions**:
- **Hybrid Search**: PostgreSQL FTS + Weaviate RRF fusion
- **Fine-Tuned Embeddings**: Custom Italian model (contrastive learning)
- **A/B Testing**: Custom framework (model version comparison)
- **ML Ops**: MLflow (experiment tracking, model registry)

### Phase 4 (Platform)

**Additions**:
- **API Gateway**: Kong or AWS API Gateway (rate limiting, auth)
- **SDK Libraries**: Python, JavaScript, .NET (auto-generated from OpenAPI)
- **Multi-Language**: French, German, Spanish fine-tuned embeddings

---

## Security Architecture

### Authentication & Authorization

**User Authentication**:
- Phase 1 (MVP): Simple session cookies (httpOnly, secure, SameSite=strict)
- Phase 2+: JWT tokens + refresh tokens (OAuth 2.0 Authorization Code flow)
- Phase 4: OAuth providers (Google, Discord for board game communities)

**API Authentication**:
- API Keys (UUID format, hashed in database PBKDF2)
- Rate limiting per API key (10 req/min free, unlimited premium)

**Authorization Roles**:
- **Anonymous**: No queries (require signup)
- **Free User**: 10 queries/day
- **Premium User**: Unlimited queries, mobile app access
- **Admin**: Full system access (user management, analytics, indexing)
- **Publisher**: B2B access (white-label integration, analytics for their games)

### Data Security

**Data at Rest**:
- PostgreSQL: Encryption at rest (AWS RDS encryption, Azure SQL TDE)
- Weaviate: Disk encryption (Kubernetes persistent volume encryption)
- Redis: Optional encryption (Phase 2+, if storing sensitive user data)
- **Licensed Rulebooks**: Encrypted with AES-256 (separate key per publisher, stored in AWS Secrets Manager)

**Data in Transit**:
- TLS 1.3 for all external traffic (HTTPS enforced)
- Internal service-to-service: mTLS (Kubernetes service mesh, Phase 2+)

**Secrets Management**:
- Phase 1: Environment variables (`.env` file, not committed)
- Phase 2+: AWS Secrets Manager or HashiCorp Vault
- API Keys: Rotated quarterly (automated via Terraform)

### Security Best Practices

**Input Validation**:
- PDF upload: Magic bytes validation (not just extension), size limit 50 MB
- Query input: SQL injection prevention (parameterized queries), XSS sanitization (frontend)
- Rate limiting: IP-based (100 req/hour unauthenticated), user-based (10/min free tier)

**Dependency Scanning**:
- Snyk (Python dependencies, weekly scans)
- Trivy (Docker images, CI/CD integration)
- Dependabot (GitHub, auto-PR for security patches)

**Monitoring & Incident Response**:
- Security alerts (PagerDuty integration)
- Audit logs (PostgreSQL, all admin actions logged)
- Intrusion detection (CloudFlare WAF, Phase 2+)

---

## Deployment Architecture

### Phase 1 (MVP) - Docker Compose

```yaml
# docker-compose.yml
version: '3.9'

services:
  api:
    build: ./backend
    ports: ["8000:8000"]
    depends_on: [postgres, redis, weaviate]
    environment:
      - POSTGRES_URL=postgresql://user:pass@postgres:5432/meepleai
      - REDIS_URL=redis://redis:6379
      - WEAVIATE_URL=http://weaviate:8080
      - OPENAI_API_KEY=${OPENAI_API_KEY}

  frontend:
    build: ./frontend
    ports: ["3000:3000"]
    depends_on: [api]
    environment:
      - NEXT_PUBLIC_API_BASE=http://localhost:8000

  postgres:
    image: postgres:16-alpine
    volumes: ["postgres_data:/var/lib/postgresql/data"]
    environment:
      - POSTGRES_DB=meepleai
      - POSTGRES_USER=user
      - POSTGRES_PASSWORD=pass

  redis:
    image: redis:7-alpine
    volumes: ["redis_data:/data"]

  weaviate:
    image: semitechnologies/weaviate:1.23.0
    ports: ["8080:8080"]
    volumes: ["weaviate_data:/var/lib/weaviate"]
    environment:
      - AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED=true
      - PERSISTENCE_DATA_PATH=/var/lib/weaviate

volumes:
  postgres_data:
  redis_data:
  weaviate_data:
```

**Deployment Target**: DigitalOcean App Platform (managed Docker Compose)

---

### Phase 2 (Production) - Kubernetes

```yaml
# k8s/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: meepleai-api
spec:
  replicas: 3  # HA setup
  selector:
    matchLabels:
      app: meepleai-api
  template:
    metadata:
      labels:
        app: meepleai-api
    spec:
      containers:
      - name: api
        image: meepleai/api:v1.2.0
        ports:
        - containerPort: 8000
        env:
        - name: POSTGRES_URL
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: postgres-url
        - name: OPENAI_API_KEY
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: openai-api-key
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8000
          initialDelaySeconds: 10
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: meepleai-api
spec:
  selector:
    app: meepleai-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8000
  type: LoadBalancer
```

**Infrastructure Components**:
- **Kubernetes**: AWS EKS (managed control plane) or DigitalOcean Kubernetes
- **Database**: AWS RDS PostgreSQL (Multi-AZ for HA)
- **Cache**: AWS ElastiCache Redis (cluster mode)
- **Vector DB**: Weaviate StatefulSet (persistent volumes, 3 replicas)
- **Ingress**: NGINX Ingress Controller + cert-manager (Let's Encrypt SSL)
- **CI/CD**: GitHub Actions → Docker build → ECR → Kubernetes rolling update

---

## Architecture Decision Records

### Index of ADRs

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](./adr-001-hybrid-rag-architecture.md) | Hybrid RAG Architecture with Multi-Model Validation | Accepted | 2025-01-15 |
| [ADR-002](./adr-002-multilingual-embedding-strategy.md) | Multilingual Embedding Strategy (Italian-First) | Accepted | 2025-01-15 |
| [ADR-003](./adr-003-pdf-processing-pipeline.md) | PDF Processing Pipeline with Vision-Language Models | Accepted | 2025-01-15 |
| [ADR-004](./adr-004-vector-db-selection.md) | Vector Database Selection (Weaviate vs Pinecone) | Accepted | 2025-01-15 |
| [ADR-005](./adr-005-llm-strategy.md) | LLM Strategy (GPT-4 Primary + Claude Validation) | Accepted | 2025-01-15 |
| [ADR-006](./adr-006-caching-strategy.md) | Semantic Caching Strategy (Redis FAISS-based) | Accepted | 2025-01-15 |

**Full ADR documents** available in `docs/architecture/` subdirectory.

---

## Scalability Considerations

### Vertical Scaling (Phase 2)

**API Server**:
- Start: 2 vCPU, 4 GB RAM (handles ~50 RPS)
- Scale to: 4 vCPU, 8 GB RAM (handles ~100 RPS)
- Kubernetes HPA (Horizontal Pod Autoscaler): CPU target 70% → scale 1-10 pods

**Database (PostgreSQL)**:
- Start: db.t3.medium (2 vCPU, 4 GB RAM)
- Scale to: db.t3.large (2 vCPU, 8 GB RAM) or db.m5.large (2 vCPU, 8 GB RAM)
- Read replicas: Add 1-2 replicas for read-heavy workload (queries >> writes)

**Vector DB (Weaviate)**:
- Start: 4 GB RAM, 50 GB SSD
- Scale to: 16 GB RAM, 200 GB SSD (1M+ vectors)
- Sharding: Phase 4 (10M+ vectors, multi-node cluster)

### Horizontal Scaling (Phase 3-4)

**API Servers**:
- Stateless design (all state in database/cache)
- Kubernetes autoscaling: 3-10 pods (CPU-based)
- Load balancer: AWS ALB or Kubernetes Ingress

**Cache (Redis)**:
- Single instance (Phase 1-2)
- Cluster mode (Phase 3+, 3 master + 3 replica nodes)
- Cache invalidation: TTL-based (1 hour) + manual purge API

**Database (PostgreSQL)**:
- Connection pooling (PgBouncer, 100 connections max)
- Partitioning: `rulebooks` table by `game_id` (Phase 4, 1000+ games)
- Full-text search offloading: Elasticsearch (if PostgreSQL FTS bottleneck)

### Performance Targets

| Phase | MAU | Concurrent Users | RPS | P95 Latency | Uptime |
|-------|-----|------------------|-----|-------------|--------|
| **Phase 1 (MVP)** | 100 | 10 | 5 | <5s | 99% |
| **Phase 2 (Production)** | 1,000 | 50 | 20 | <3s | 99.5% |
| **Phase 3 (Growth)** | 5,000 | 200 | 100 | <3s | 99.5% |
| **Phase 4 (Scale)** | 10,000 | 500 | 200 | <3s | 99.9% |

---

## Future Architecture Evolution

### Phase 5+ (Hypothetical)

**Multi-Region Deployment**:
- Latency reduction: EU (Italy), NA (US), APAC (Japan for board game market)
- Data residency: GDPR compliance (EU user data stays in EU)
- Active-active: Read replicas in each region, write to primary (async replication)

**Microservices Decomposition**:
- Current: Monolith (FastAPI app, all services in one codebase)
- Future: Split into services (Q&A Service, Indexing Service, Admin Service)
- Communication: gRPC for internal, REST for external
- Service mesh: Istio (mTLS, observability, traffic management)

**AI/ML Pipeline**:
- Feature store: Feast (centralized feature management)
- Model serving: TorchServe or TensorFlow Serving (custom models)
- A/B testing: Multi-armed bandit (explore-exploit for model selection)
- AutoML: Experiment with auto-tuning hyperparameters (Phase 5+)

**Real-Time Features**:
- WebSocket support: Live game state tracking (BoardGameArena integration)
- Streaming responses: SSE (Server-Sent Events) for token-by-token LLM output
- Collaborative features: Multiple users querying same game simultaneously

---

## Appendix A: Technology Selection Criteria

### Vector Database Comparison

| Criterion | ChromaDB | Weaviate | Pinecone | Qdrant |
|-----------|----------|----------|----------|--------|
| **Deployment** | Embedded/Docker | Docker/K8s | Cloud SaaS | Docker/K8s/Cloud |
| **Performance** | Medium (local) | High (optimized) | Very High (managed) | High |
| **Hybrid Search** | No | Yes (BM25+vector) | No (vector only) | Yes (custom) |
| **Cost** | Free (OSS) | Free (self-hosted) | $70+/month | Free (self-hosted) |
| **Community** | Growing | Large, active | Largest (VC-backed) | Medium |
| **Our Choice** | MVP (Phase 1) | Production (Phase 2+) | Evaluated, not chosen | Alternative option |

**Decision**: ChromaDB (MVP simplicity) → Weaviate (production hybrid search + self-hosted cost control)

---

### LLM Selection Rationale

**Primary (GPT-4 Turbo)**:
- ✅ Highest accuracy on complex reasoning tasks
- ✅ Broad knowledge (board games included in training)
- ✅ Cost-effective vs GPT-4 (60% cheaper, similar quality)
- ✅ Strong Italian language support
- ⚠️ Hallucination risk (mitigated via validation layers)

**Validation (Claude 3.5 Sonnet)**:
- ✅ Different architecture (diversity reduces correlated errors)
- ✅ Strong reasoning capabilities
- ✅ Lower hallucination rate (Anthropic's Constitutional AI)
- ⚠️ Higher cost than GPT-4 Turbo (justified for critical validation)

**Considered Alternatives**:
- **Mistral Large**: French company, strong EU data residency, but lower accuracy on Italian
- **Llama 3 70B**: Open-source, self-hostable, but requires significant GPU infra
- **Gemini Pro**: Google, strong multilingual, but API stability concerns (new product)

---

## Appendix B: Monitoring & Observability

### Metrics (Prometheus)

**Application Metrics**:
```python
# Custom metrics in FastAPI app
from prometheus_client import Counter, Histogram, Gauge

# Question answering
qa_requests_total = Counter('qa_requests_total', 'Total Q&A requests', ['game_id', 'language'])
qa_duration_seconds = Histogram('qa_duration_seconds', 'Q&A request duration', ['game_id'])
qa_confidence_score = Histogram('qa_confidence_score', 'Confidence score distribution')
qa_validation_failures = Counter('qa_validation_failures_total', 'Validation failures', ['layer'])

# Indexing
indexing_jobs_total = Counter('indexing_jobs_total', 'Total indexing jobs', ['game_id', 'status'])
indexing_duration_seconds = Histogram('indexing_duration_seconds', 'Indexing job duration', ['method'])
chunks_indexed_total = Counter('chunks_indexed_total', 'Total chunks indexed', ['game_id'])

# LLM API
llm_api_calls_total = Counter('llm_api_calls_total', 'Total LLM API calls', ['provider', 'model'])
llm_api_duration_seconds = Histogram('llm_api_duration_seconds', 'LLM API latency', ['provider'])
llm_api_tokens_total = Counter('llm_api_tokens_total', 'Total tokens used', ['provider', 'type'])
llm_api_errors_total = Counter('llm_api_errors_total', 'LLM API errors', ['provider', 'error_type'])

# System
active_users_gauge = Gauge('active_users', 'Currently active users')
cache_hit_rate = Gauge('cache_hit_rate', 'Semantic cache hit rate')
```

**Infrastructure Metrics** (auto-collected):
- CPU/Memory/Disk/Network (node-exporter)
- PostgreSQL: connections, queries/sec, cache hit ratio (postgres-exporter)
- Redis: memory usage, commands/sec, hit rate (redis-exporter)
- Weaviate: vector count, query latency (built-in /metrics)

### Alerting Rules (Prometheus)

```yaml
# prometheus-rules.yml
groups:
- name: meepleai_critical
  interval: 30s
  rules:
  - alert: HighAccuracyDrop
    expr: rate(qa_validation_failures_total[5m]) > 0.10  # >10% failure rate
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: "Accuracy drop detected: >10% validation failures"
      description: "Validation failure rate {{ $value }} over last 5 minutes"

  - alert: HighHallucinationRate
    expr: rate(qa_validation_failures_total{layer="forbidden_keywords"}[1h]) > 0.03
    for: 10m
    labels:
      severity: critical
    annotations:
      summary: "Hallucination rate >3% detected"

  - alert: LLMAPIDown
    expr: rate(llm_api_errors_total[2m]) > 0.50  # >50% error rate
    for: 2m
    labels:
      severity: critical
    annotations:
      summary: "LLM API {{ $labels.provider }} down or degraded"

  - alert: DatabaseConnectionPoolExhausted
    expr: pg_stat_database_numbackends / pg_settings_max_connections > 0.90
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: "PostgreSQL connection pool >90% utilized"

- name: meepleai_warning
  interval: 1m
  rules:
  - alert: HighLatency
    expr: histogram_quantile(0.95, rate(qa_duration_seconds_bucket[5m])) > 3.0
    for: 10m
    labels:
      severity: warning
    annotations:
      summary: "P95 latency >3s (target: <3s)"

  - alert: LowCacheHitRate
    expr: cache_hit_rate < 0.40  # Target: 40-60%
    for: 15m
    labels:
      severity: warning
    annotations:
      summary: "Cache hit rate {{ $value }} below target 40%"
```

### Dashboards (Grafana)

**Dashboard 1: Question Answering Overview**
- Requests/sec (time series)
- P50/P95/P99 latency (time series)
- Confidence score distribution (histogram)
- Validation failure rate by layer (stacked area chart)
- Top games by query volume (bar chart)

**Dashboard 2: System Health**
- CPU/Memory/Disk usage (gauge + time series)
- API uptime (SLA percentage)
- Database connections (gauge)
- Cache hit rate (gauge + time series)
- Error rate (time series by endpoint)

**Dashboard 3: LLM API Usage**
- API calls by provider (stacked area: GPT-4 vs Claude)
- Token usage (total + by type: input/output)
- Cost estimation ($ spent, cumulative)
- API latency by provider (multi-line)
- Circuit breaker state (annotation markers)

**Dashboard 4: Business Metrics**
- Daily/Monthly Active Users (DAU/MAU)
- Premium conversion rate (gauge)
- Query volume by game (top 10 bar chart)
- User retention cohorts (heatmap)
- Revenue (MRR, ARR projections)

---

**Document Metadata**:
- **Version**: 1.0
- **Last Updated**: 2025-01-15
- **Next Review**: 2025-04-15 (quarterly)
- **Approvers**: Engineering Lead, CTO, Product Lead
- **Status**: APPROVED for Phase 1 Implementation
