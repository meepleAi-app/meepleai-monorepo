# Board Game AI Execution Calendar - Parallel Frontend/Backend Tracks

**Timeline**: Jan 15, 2025 - Jun 27, 2025 (Phase 1 MVP)
**Strategy**: Parallel execution where possible, sequential for dependencies
**Team**: 2 Backend + 1 Frontend + 0.5 DevOps + 0.5 QA = 4 FTE

---

## Overview: Parallel Execution Strategy

```
BACKEND TRACK (2 Engineers)          FRONTEND TRACK (1 Engineer)
════════════════════════════          ═══════════════════════════

Sprint 1-2: PDF Pipeline             Sprint 1-2: Foundation
├─ LLMWhisperer integration    ||    ├─ shadcn/ui setup (#927)
├─ SmolDocling microservice    ||    ├─ Design tokens (#928)
└─ 3-stage orchestrator        ||    └─ Base components

Sprint 3-4: Multi-Model LLM          Sprint 3-4: Q&A Interface
├─ OpenRouter client           ||    ├─ Question input form
├─ Ollama fallback            ||    ├─ Streaming response
└─ Consensus validation        ||    └─ Citation links

Sprint 5-6: Quality Framework        Sprint 5-6: Italian UI
├─ 5-metric testing           ||    ├─ Italian i18n strings
├─ Golden dataset             ||    ├─ PDF viewer component
└─ Hallucination detection     ||    └─ Mobile responsive

Sprint 7-8: Beta Support             Sprint 7-8: UX Polish
├─ Performance optimization    ||    ├─ Feedback widgets
├─ API refinements            ||    ├─ Loading states
└─ Bug fixes                   ||    └─ Error messaging

Sprint 9-10: Iteration               Sprint 9-10: Beta UX
├─ Beta feedback impl         ||    ├─ User feedback impl
├─ Accuracy improvements      ||    ├─ Mobile testing
└─ Scale testing               ||    └─ Accessibility (WCAG)
```

---

## Phase 1 Calendar (23 Weeks)

### Pre-Sprint: Setup Week (Jan 15-26, 2025)

**Week 1: Jan 15-19 (Setup & Prototyping)**

| Day | Backend Track | Frontend Track | DevOps/QA |
|-----|--------------|----------------|-----------|
| **Wed 15** | LLMWhisperer trial signup, test 3 PDFs | Review Board Game AI docs | Ollama model pull (mistral, llama3) |
| **Thu 16** | SmolDocling Python prototype (local) | shadcn/ui research, dependency check | Docker Compose planning (pdf-processor) |
| **Fri 17** | OpenRouter account + $50 credits | Design token strategy (Italian theme) | - |

**Week 2: Jan 20-26 (Sprint Planning)**

| Day | Backend Track | Frontend Track | Shared |
|-----|--------------|----------------|--------|
| **Mon 20** | GitHub Issues creation (Sprint 1 backend tasks) | GitHub Issues creation (Sprint 1 frontend tasks) | - |
| **Tue 21** | Sprint 1 backend task breakdown (8-12 tasks) | Sprint 1 frontend task breakdown (6-8 tasks) | - |
| **Wed 22** | Development environment setup verification | Development environment setup | - |
| **Thu 23** | **SPRINT 1 PLANNING MEETING** (4 hours, full team) | | Sprint goals, velocity, assignments |
| **Fri 24** | Pre-sprint setup (dependencies, tooling) | Pre-sprint setup | - |

---

### Sprint 1-2: Foundation (Jan 27 - Feb 21, 4 weeks)

#### Sprint 1: PDF Pipeline + Frontend Foundation (Jan 27 - Feb 7)

**BACKEND TRACK (2 Engineers, 100% allocation)**:

**Backend Engineer 1: LLMWhisperer Integration**
- [ ] **#BGAI-1**: Create LlmWhispererPdfExtractor (C# HttpClient) - **3 days**
  - Interface: `IPdfTextExtractor`
  - API: POST to https://api.llmwhisperer.com/v1/extract
  - Response parsing, error handling
  - Unit tests (10 tests, mock API responses)

- [ ] **#BGAI-2**: LLMWhisperer configuration (ConfigurationService) - **1 day**
  - appsettings.json: ApiKey, Enabled, Timeout, QualityThreshold
  - Feature flag: `Features:EnhancedPdfProcessing`

- [ ] **#BGAI-3**: Integration tests (Testcontainers) - **2 days**
  - Test real LLMWhisperer API (sample PDFs)
  - Quality score validation (≥0.80 threshold)
  - Fallback trigger scenarios

**Backend Engineer 2: SmolDocling Python Microservice**
- [ ] **#BGAI-4**: Create pdf-processor Python service (FastAPI) - **2 days**
  - `apps/pdf-processor/main.py`
  - `/api/v1/convert` endpoint
  - Docling integration, error handling
  - Health check endpoint

- [ ] **#BGAI-5**: SmolDoclingPdfExtractor C# client - **2 days**
  - HttpClient calling Python service
  - Multipart/form-data upload
  - Response parsing, timeout handling
  - Unit tests (8 tests, mock Python service)

- [ ] **#BGAI-6**: Docker Compose integration - **1 day**
  - Add pdf-processor service to docker-compose.yml
  - GPU configuration (optional, CPU fallback)
  - Health check, networking
  - Local testing (docker compose up -d)

**FRONTEND TRACK (1 Engineer, 50% allocation)**:

- [ ] **#926 (Part 1)**: Install shadcn/ui - **1 day**
  - `npx shadcn-ui@latest init`
  - Configure tailwind.config.js
  - Install base components (button, card, input, form)

- [ ] **#928**: Design tokens migration to CSS variables - **2 days**
  - Define Italian color palette (board game themed)
  - Typography scale (readable for rules text)
  - Spacing/sizing tokens
  - Dark/light mode variables (minimal for Phase 1)

- [ ] **#BGAI-7**: Create BoardGameAI page structure - **2 days**
  - `/pages/board-game-ai/index.tsx` (game catalog)
  - `/pages/board-game-ai/[gameId].tsx` (Q&A interface)
  - Layout, routing, navigation
  - Placeholder UI (implement Sprint 3-4)

**PARALLEL EXECUTION**: Backend + Frontend 100% independent (no blocking)

**Sprint 1 Review** (Feb 7):
- Demo: 3-stage PDF pipeline working (LLMWhisperer → SmolDocling → Docnet)
- Demo: Frontend foundation (shadcn/ui components, design tokens)
- Velocity: Completed story points vs planned
- Sprint 2 adjustments

---

#### Sprint 2: PDF Orchestration + Italian i18n (Feb 10 - Feb 21)

**BACKEND TRACK**:

**Backend Engineer 1: PDF Orchestration**
- [ ] **#940**: Migrate existing PdfTextExtractionService to IPdfTextExtractor - **2 days**
  - Refactor existing Docnet.Core service
  - Implement interface (existing DDD issue, prioritized)

- [ ] **#BGAI-8**: EnhancedPdfProcessingOrchestrator - **3 days**
  - 3-stage fallback logic (LLMWhisperer → SmolDocling → Docnet)
  - Quality threshold enforcement (≥0.80, ≥0.70, ≥0.50)
  - Metrics: Prometheus counters per stage, quality histograms
  - Integration tests (15 tests, all 3 stages)

**Backend Engineer 2: Italian Terminology**
- [ ] **#BGAI-9**: Italian terminology glossary service - **2 days**
  - Resource files: BoardGameTerms.it.resx (500+ terms)
  - Query expansion (English game terms → Italian equivalents)
  - Example: "meeple" → ["meeple", "pedina", "segnalino"]

- [ ] **#BGAI-10**: i18n middleware configuration - **1 day**
  - RequestLocalizationOptions (it-IT default)
  - Culture detection (Accept-Language header)
  - Testing (10 tests, language switching)

- [ ] **#BGAI-11**: Golden dataset schema + first 20 Q&A - **2 days**
  - JSON schema definition
  - Annotate 20 Q&A for Terraforming Mars (Italian)
  - Test data fixtures for quality tests

**FRONTEND TRACK (1 Engineer, 100% allocation)**:

- [ ] **#BGAI-12**: Italian i18n setup (React Intl) - **2 days**
  - Install react-intl, configure provider
  - Italian translation files (it.json)
  - Language switcher component (it/en toggle)

- [ ] **#BGAI-13**: Base UI components (shadcn/ui) - **3 days**
  - QuestionInputForm (textarea, submit button, loading state)
  - GameSelector (dropdown, search, filters)
  - ResponseCard (answer display, confidence badge, citations)
  - PDF viewer integration research (react-pdf or PDF.js)

**Sprint 2 Review** (Feb 21):
- Demo: Process full Italian rulebook (Terraforming Mars end-to-end)
- Demo: Italian UI components (functional mockups)
- Metrics: PDF processing time per stage, quality scores
- Go/No-Go: Phase 1 on track? (adjust Sprint 3 if needed)

---

### Sprint 3-4: Multi-Model Validation + Q&A UI (Feb 24 - Mar 21, 4 weeks)

#### Sprint 3: OpenRouter Integration (Feb 24 - Mar 7)

**BACKEND TRACK**:

**Backend Engineer 1: OpenRouter Client**
- [ ] **#BGAI-14**: OpenRouterClient implementation - **3 days**
  - HttpClient wrapper, authentication (Bearer token)
  - Chat completions endpoint (/chat/completions)
  - Streaming support (SSE, optional Phase 1)
  - Error handling (429 rate limit, 503 service unavailable)
  - Unit tests (12 tests, mock API responses)

- [ ] **#BGAI-15**: Ollama client implementation - **2 days**
  - HttpClient wrapper for Ollama API
  - Generate endpoint (/api/generate)
  - Embeddings endpoint (/api/embeddings)
  - Unit tests (8 tests)

**Backend Engineer 2: Adaptive LLM Service**
- [ ] **#BGAI-16**: AdaptiveLlmService (OpenRouter + Ollama routing) - **3 days**
  - Feature flag: `AI:Provider` ("OpenRouter" or "Ollama")
  - Automatic fallback (OpenRouter unavailable → Ollama)
  - Model selection (Primary, Validation, Fallback)
  - Configuration: Dynamic model IDs via ConfigurationService
  - Unit tests (15 tests, both providers)

- [ ] **#BGAI-17**: Integration with existing RagService - **2 days**
  - Replace existing LlmService calls with AdaptiveLlmService
  - Backward compatibility (existing features unaffected)
  - Integration tests (10 tests, RAG pipeline end-to-end)

**FRONTEND TRACK (100% allocation)**:

- [ ] **#BGAI-18**: Q&A Interface implementation - **4 days**
  - Question input form (textarea, game selector, submit)
  - Streaming response display (token-by-token, optional Phase 1)
  - Loading states (skeleton loaders)
  - Error states (API errors, network failures)
  - Jest tests (20 tests, React Testing Library)

- [ ] **#BGAI-19**: Citation viewer component - **1 day**
  - Clickable citations (page number links)
  - Tooltip on hover (snippet preview)
  - Integration with PDF viewer (jump to page)

**PARALLEL**: Backend + Frontend fully independent

---

#### Sprint 4: Multi-Model Validation + Response UI (Mar 10 - Mar 21)

**BACKEND TRACK**:

**Backend Engineer 1: Validation Services**
- [ ] **#BGAI-20**: ConfidenceValidationService - **1 day**
  - Threshold enforcement (≥0.70)
  - Explicit uncertainty message generation
  - Unit tests (8 tests)

- [ ] **#BGAI-21**: CitationValidationService - **2 days**
  - Page number existence check
  - Text snippet fuzzy matching (Levenshtein distance)
  - Unit tests (12 tests, various edge cases)

- [ ] **#BGAI-22**: HallucinationDetectionService - **2 days**
  - Forbidden keywords blocklist (seed 100 terms from research)
  - Pattern matching, case-insensitive
  - Admin UI for blocklist management (simple CRUD)
  - Unit tests (10 tests)

**Backend Engineer 2: Multi-Model Consensus**
- [ ] **#BGAI-23**: MultiModelValidationService - **4 days**
  - Consensus logic (GPT-4 + Claude via OpenRouter)
  - Similarity calculation (cosine similarity on embeddings)
  - Threshold: 0.90 (configurable)
  - Adaptive validation (skip if confidence ≥0.90)
  - Unit tests (18 tests, consensus scenarios)

- [ ] **#BGAI-24**: Integration with RagService (full pipeline) - **1 day**
  - Wire up all 5 validation layers
  - End-to-end test (question → validated response)
  - Performance testing (latency targets)

**FRONTEND TRACK**:

- [ ] **#BGAI-25**: Response display with validation status - **2 days**
  - Answer text formatting (markdown support)
  - Confidence badge (low/medium/high with colors)
  - Validation status indicators (5 layers visual)
  - Explicit uncertainty UI (suggestions, alternative actions)

- [ ] **#BGAI-26**: PDF viewer integration - **3 days**
  - react-pdf or PDF.js evaluation
  - Page navigation (jump to citation page)
  - Highlight snippet on page (optional, if time permits)
  - Mobile-optimized viewer
  - Jest tests (15 tests)

**Sprint 4 Review** (Mar 21):
- Demo: Full Q&A flow (question → multi-model validation → response with citations → PDF viewer)
- Metrics: Consensus similarity distribution, validation pass rates
- Performance: P95 latency (target <5s Phase 1)

---

### Sprint 5-6: Quality & Italian Localization (Mar 24 - Apr 18, 4 weeks)

#### Sprint 5: 5-Metric Framework (Mar 24 - Apr 4)

**BACKEND TRACK**:

**Backend Engineer 1: Quality Metrics**
- [ ] **#BGAI-27**: Extend PromptEvaluationService (5-metric framework) - **3 days**
  - Accuracy metric (keyword matching)
  - Hallucination metric (forbidden keywords)
  - Confidence metric (average RAG search quality)
  - Citation metric (page validation correctness)
  - Latency metric (P95 response time)
  - Unit tests (15 tests, each metric)

- [ ] **#BGAI-28**: Automated weekly evaluation job - **2 days**
  - Background service (runs every Sunday 2 AM)
  - Evaluate on golden dataset (100 Q&A by Sprint 6)
  - Results stored in database (prompt_evaluation_results table)
  - Alert if metrics below threshold (email to admin)

**Backend Engineer 2: Golden Dataset Creation**
- [ ] **#BGAI-29**: Golden dataset annotation (50 Q&A pairs) - **4 days**
  - Games: Terraforming Mars (20), Wingspan (15), Azul (15)
  - Categories: gameplay (20), setup (15), endgame (10), edge cases (5)
  - Difficulty: easy (30), medium (15), hard (5)
  - Annotation tool: JSON editor + validation schema
  - Storage: `tests/data/golden_dataset.json`

- [ ] **#BGAI-30**: Quality test implementation - **1 day**
  - Pytest test suite (runs golden dataset evaluation)
  - Assert thresholds (Accuracy ≥80%, Hallucination ≤10%)
  - CI integration (nightly run)

**FRONTEND TRACK (100% allocation)**:

- [ ] **#BGAI-31**: Italian UI strings (complete translation) - **2 days**
  - All interface text in Italian (buttons, labels, messages)
  - Resource files: it.json (200+ strings)
  - Explicit uncertainty messages (Italian phrasing)
  - Error messages (user-friendly Italian)

- [ ] **#BGAI-32**: Game catalog page (/board-game-ai) - **3 days**
  - Game grid/list view (cards with cover images)
  - Search/filter (name, genre, player count)
  - Italian game names and descriptions
  - Link to Q&A interface per game
  - Jest tests (12 tests)

**PARALLEL**: Backend metrics + Frontend Italian UI independent

---

#### Sprint 6: Dataset Completion + Mobile (Apr 7 - Apr 18)

**BACKEND TRACK**:

**Backend Engineer 1: Golden Dataset Expansion**
- [ ] **#BGAI-33**: Annotate remaining 50 Q&A pairs - **3 days**
  - Games: Scythe (10), Catan (10), Pandemic (10), 7 Wonders (10), Agricola (10)
  - Total: 100 Q&A pairs complete
  - Peer review by QA engineer (accuracy validation)

- [ ] **#BGAI-34**: Adversarial dataset creation (50 synthetic queries) - **2 days**
  - Fabricated rules (20 tests, trigger hallucination detection)
  - Ambiguous questions (15 tests, test disambiguation)
  - Cross-game confusion (15 tests, game detection)

**Backend Engineer 2: Performance Optimization**
- [ ] **#BGAI-35**: Semantic caching (Redis FAISS-based) - **3 days**
  - Query embedding similarity search (threshold 0.95)
  - Cache storage (Redis, TTL 1 hour)
  - Hit/miss rate tracking (Prometheus metrics)
  - Unit tests (12 tests, cache scenarios)

- [ ] **#BGAI-36**: Smart model routing (cost optimization) - **2 days**
  - Simple query detection (heuristic: length, keywords)
  - Route to gpt-3.5-turbo (cheaper) vs gpt-4-turbo
  - Configurable thresholds
  - A/B test framework setup (Phase 2 feature, foundation only)

**FRONTEND TRACK**:

- [ ] **#BGAI-37**: Mobile responsive optimization - **3 days**
  - Test viewports: 320px (iPhone SE), 375px (iPhone 12), 768px (iPad)
  - Touch targets ≥44x44px (WCAG compliance)
  - PDF viewer mobile UX (swipe pagination)
  - Jest + Playwright tests (15 tests, mobile viewports)

- [ ] **#BGAI-38**: Feedback widget (thumbs up/down) - **1 day**
  - Post-response feedback prompt
  - POST /api/v1/qa/feedback endpoint integration
  - Confirmation toast ("Grazie per il feedback!")
  - Jest tests (6 tests)

- [ ] **#BGAI-39**: Loading & error states polish - **1 day**
  - Skeleton loaders (question processing)
  - Error messages (Italian, user-friendly)
  - Retry mechanisms (network failures)
  - Empty states (no results, no games indexed)

**Sprint 5-6 Review** (Apr 18):
- Demo: 100 Q&A golden dataset complete, 5-metric framework running
- Demo: Full Italian mobile UI (iPhone responsive)
- Metrics: Quality test results (Accuracy ≥80%? Hallucination ≤10%?)
- Go/No-Go Gate 1: Tech viable for 95%+ accuracy? (If no, pivot; if yes, continue)

---

### Sprint 7-8: Beta Preparation (Apr 21 - May 16, 4 weeks)

#### Sprint 7: La Tana Partnership + Backend Refinement (Apr 21 - May 2)

**BACKEND TRACK**:

**Backend Engineer 1: API Refinement**
- [ ] **#BGAI-40**: Rate limiting per tier (extend existing) - **1 day**
  - Free: 10 queries/day, Premium: unlimited
  - Per-user tracking (ConfigurationService)
  - 429 error responses with Retry-After header

- [ ] **#BGAI-41**: Analytics endpoints (admin dashboard) - **2 days**
  - GET /api/v1/admin/board-game-ai/metrics
  - Query volume, accuracy, latency stats
  - By game, by language breakdown
  - Jest tests (10 tests)

- [ ] **#BGAI-42**: Bug fixes from internal testing - **2 days**
  - Code review findings
  - Edge cases from golden dataset failures
  - Performance bottlenecks

**Backend Engineer 2: Monitoring & Observability**
- [ ] **#BGAI-43**: Prometheus metrics (Board Game AI specific) - **2 days**
  - bgai_qa_requests_total (counter)
  - bgai_validation_pass_rate (gauge per layer)
  - bgai_pdf_processing_duration (histogram per stage)
  - bgai_llm_cost_usd (counter, OpenRouter vs Ollama)

- [ ] **#BGAI-44**: Grafana dashboard (Board Game AI) - **1 day**
  - Panel 1: Q&A requests/sec (time series)
  - Panel 2: 5-metric framework gauges
  - Panel 3: Validation funnel (% passing each layer)
  - Panel 4: Cost tracking (OpenRouter daily spend)

- [ ] **#BGAI-45**: Alerting rules (Prometheus) - **2 days**
  - HighAccuracyDrop (accuracy <80% for 10 min)
  - HighHallucinationRate (>10% for 1 hour)
  - HighLatency (P95 >5s for 15 min)
  - LLMAPIDown (OpenRouter errors >50% for 2 min)

**FRONTEND TRACK**:

- [ ] **#BGAI-46**: Beta signup page - **2 days**
  - `/pages/beta-signup.tsx`
  - Google Forms embed or custom form
  - Email collection (MailChimp/SendGrid integration)
  - Waitlist management

- [ ] **#BGAI-47**: Onboarding flow for beta users - **2 days**
  - Welcome screen (expectations, how to use)
  - First query tutorial (step-by-step)
  - Feedback channels (Discord link, email)

- [ ] **#BGAI-48**: Admin analytics UI - **1 day**
  - `/pages/admin/board-game-ai-analytics.tsx`
  - 5-metric framework display (charts)
  - Query volume by game (table)
  - Export CSV functionality

**PARALLEL**: Backend monitoring + Frontend beta UX independent

---

#### Sprint 8: Beta Launch (May 5 - May 16)

**ALL TEAM**:

**Week 1 (May 5-9): Beta Recruitment**
- [ ] **#BGAI-49**: La Tana dei Goblin announcement (Product Lead, 1 day)
  - Forum post (partnership announcement, beta program details)
  - Discord message (#annunci channel)
  - Social media (Twitter/X, Facebook board game groups)

- [ ] **Backend**: Beta environment deployment (DevOps, 2 days)
  - Staging: staging.meepleai.dev
  - Monitoring active (Prometheus, Grafana)
  - 10 games indexed (Italian rulebooks)

- [ ] **Frontend**: Beta landing page polish (1 day)
  - Hero section (value proposition)
  - How it works (3-step diagram)
  - FAQ (accuracy expectations, supported games)
  - CTA: Beta signup button

**Week 2 (May 12-16): Beta Testing**
- [ ] **#BGAI-50**: Beta user support (All team, 50% allocation)
  - Discord channel monitoring (#beta-support)
  - Bug reports triage (GitHub Issues)
  - Quick fixes (P0 bugs within 24 hours)
  - Daily standups (beta progress, blockers)

- [ ] **#BGAI-51**: Feedback collection (QA Lead, 100%)
  - User interviews (5-10 users, 30 min each)
  - Survey distribution (Google Forms, 20 questions)
  - Analytics review (usage patterns, query types)
  - Accuracy spot-checking (sample 50 user queries, manual validation)

**Sprint 7-8 Review** (May 16):
- **CRITICAL**: 100 beta users recruited? (If <50, extend beta 2 more weeks)
- User satisfaction ≥4.0/5.0? (If <3.5, major pivot needed)
- Accuracy ≥80%? (If <70%, investigate quality issues)
- Top 5 user feature requests (prioritize for Sprint 9-10)

---

### Sprint 9-10: Iteration & Polish (May 19 - Jun 13, 4 weeks)

#### Sprint 9: Beta Feedback Implementation (May 19 - May 30)

**BACKEND TRACK** (Prioritized by user feedback):

- [ ] **#BGAI-52**: Top 3 user-requested backend features - **5 days**
  - Example: Query history (save past questions)
  - Example: Game comparison (rules differences between editions)
  - Example: Bulk Q&A (multiple questions at once)
  - Implementation based on feedback survey results

**FRONTEND TRACK**:

- [ ] **#BGAI-53**: Top 3 user-requested UI improvements - **5 days**
  - Example: Bookmark/save favorite responses
  - Example: Share citation (copy link, social media)
  - Example: Dark mode toggle (if highly requested)

**QA** (100% allocation):
- [ ] **#BGAI-54**: Regression testing (all 100 Q&A golden dataset) - **2 days**
- [ ] **#BGAI-55**: Accuracy validation (sample 100 real user queries) - **2 days**
- [ ] **#BGAI-56**: Performance testing (simulate 50 concurrent users) - **1 day**

---

#### Sprint 10: Phase 1 Wrap-Up (Jun 2 - Jun 13)

**BACKEND TRACK**:

- [ ] **#BGAI-57**: Bug fixes (P1-P2 priority from beta) - **3 days**
- [ ] **#BGAI-58**: Performance optimization - **2 days**
  - Cache hit rate improvement (target 40%)
  - Latency reduction (P95 <4s goal)
  - Database query optimization

**FRONTEND TRACK**:

- [ ] **#BGAI-59**: UX polish (animations, transitions) - **2 days**
- [ ] **#BGAI-60**: Accessibility audit (WCAG 2.1 AA) - **2 days**
  - Screen reader testing (NVDA)
  - Keyboard navigation
  - Color contrast (Italian design tokens)
- [ ] **#BGAI-61**: E2E tests (Playwright) - **1 day**
  - Happy path: Upload rulebook → Ask question → View citation
  - Uncertainty path: Low confidence → Explicit message
  - Mobile path: 375px viewport full journey

**ALL TEAM**:
- [ ] **#BGAI-62**: Documentation finalization - **1 day**
  - Update CLAUDE.md (Board Game AI features)
  - API documentation (Swagger/OpenAPI)
  - User guide (Italian)

- [ ] **Phase 1 Retrospective** (Jun 13, 4 hours):
  - What worked well? (keep for Phase 2)
  - What didn't work? (change for Phase 2)
  - Velocity actual vs planned (on track? adjust Phase 2 estimates)
  - Lessons learned (document for future)

**Sprint 9-10 Review** (Jun 13):
- **Go/No-Go Gate 2**: Proceed to Phase 2?
  - 100 beta users achieved? ✅/❌
  - Accuracy ≥80%? ✅/❌
  - User satisfaction ≥4.0/5.0? ✅/❌
  - Uptime ≥99% during beta? ✅/❌
- **Decision**: If all ✅ → Phase 2 (Publisher outreach), if ❌ → Iterate 2-4 more weeks

---

## Parallel Execution Opportunities

### High Parallelism Sprints (No Dependencies)

**Sprint 1** (Week 1-2):
- Backend 1: LLMWhisperer ⚡ PARALLEL with Backend 2: SmolDocling
- Backend ⚡ PARALLEL with Frontend: Foundation
- **Efficiency**: 100% parallel, no blocking

**Sprint 3** (Week 7-8):
- Backend 1: OpenRouter ⚡ PARALLEL with Backend 2: AdaptiveLlmService
- Backend ⚡ PARALLEL with Frontend: Q&A Interface
- **Efficiency**: 100% parallel

**Sprint 5** (Week 11-12):
- Backend 1: Metrics ⚡ PARALLEL with Backend 2: Golden Dataset
- Backend ⚡ PARALLEL with Frontend: Italian UI
- **Efficiency**: 100% parallel

---

### Sequential Dependencies (Must Wait)

**Sprint 2** (Week 3-4):
- Backend: EnhancedPdfProcessingOrchestrator **DEPENDS ON** Sprint 1 (LLMWhisperer + SmolDocling clients)
- **Dependency**: Sprint 1 must complete first

**Sprint 4** (Week 9-10):
- Backend: MultiModelValidationService **DEPENDS ON** Sprint 3 (OpenRouter client)
- **Dependency**: Sprint 3 OpenRouter integration must work

**Sprint 6** (Week 13-14):
- Backend: Semantic caching **DEPENDS ON** Sprint 5 (embeddings working)
- Frontend: PDF viewer **DEPENDS ON** Sprint 4 (citations API)

---

## Resource Leveling (Avoid Overallocation)

### Frontend Engineer Allocation by Sprint

| Sprint | Allocation | Focus | Rationale |
|--------|-----------|-------|-----------|
| **Sprint 1-2** | 50% | Foundation (shadcn/ui, tokens) | Backend needs more resources (2 engineers) |
| **Sprint 3-4** | 100% | Q&A Interface + PDF viewer | Core UI implementation |
| **Sprint 5-6** | 100% | Italian localization + mobile | Critical for Italian market |
| **Sprint 7-8** | 100% | Beta UX, accessibility | User-facing polish |
| **Sprint 9-10** | 75% | Iteration, bug fixes | Some capacity for Phase 2 planning |

**Total Frontend Effort**: ~8.25 FTE-weeks (balanced for 1 engineer across 10 sprints)

---

### Backend Engineer Allocation by Sprint

| Sprint | Backend 1 | Backend 2 | Combined Load |
|--------|-----------|-----------|---------------|
| **Sprint 1** | 100% (LLMWhisperer) | 100% (SmolDocling) | 2.0 FTE (balanced) |
| **Sprint 2** | 100% (Orchestrator) | 100% (i18n) | 2.0 FTE |
| **Sprint 3** | 100% (OpenRouter) | 100% (AdaptiveLlm) | 2.0 FTE |
| **Sprint 4** | 100% (Validation services) | 100% (Multi-model) | 2.0 FTE |
| **Sprint 5** | 100% (Metrics) | 100% (Golden dataset) | 2.0 FTE |
| **Sprint 6** | 75% (Dataset expand) | 100% (Performance) | 1.75 FTE |
| **Sprint 7** | 75% (API refinement) | 75% (Monitoring) | 1.5 FTE (ramp-down for beta support) |
| **Sprint 8** | 50% (Beta support) | 50% (Beta support) | 1.0 FTE |
| **Sprint 9-10** | 75% (Iteration) | 75% (Bug fixes) | 1.5 FTE |

**Total Backend Effort**: ~17.75 FTE-weeks (87.5% utilization average, healthy)

---

## Issue Dependency Graph

```
Sprint 1 Foundation
├─ #BGAI-1 (LLMWhisperer) ────────┐
├─ #BGAI-4 (SmolDocling service) ─┤
├─ #BGAI-5 (SmolDocling client) ──┤
└─ #926 Part 1 (shadcn/ui) ───────┼─ PARALLEL (no dependencies)
                                  │
Sprint 2 Integration              │
├─ #940 (IPdfTextExtractor) ◄─────┘ DEPENDS ON Sprint 1
├─ #BGAI-8 (Orchestrator) ◄──────── DEPENDS ON #940
└─ #BGAI-12 (i18n setup) ────────── PARALLEL

Sprint 3 LLM                      │
├─ #BGAI-14 (OpenRouter) ─────────┤
├─ #BGAI-15 (Ollama) ─────────────┤
├─ #BGAI-16 (AdaptiveLlm) ◄───────┘ DEPENDS ON #BGAI-14, #BGAI-15
└─ #BGAI-18 (Q&A UI) ──────────────── PARALLEL

Sprint 4 Validation               │
├─ #BGAI-23 (MultiModel) ◄────────┘ DEPENDS ON Sprint 3 OpenRouter
├─ #BGAI-20,21,22 (Validators) ──── PARALLEL with #BGAI-23
└─ #BGAI-26 (PDF viewer) ──────────── PARALLEL

Sprint 5-6 Quality                │
├─ #BGAI-27 (5-metric) ───────────┤
├─ #BGAI-29 (Golden dataset) ─────┤
├─ #BGAI-35 (Semantic cache) ◄────┘ DEPENDS ON embeddings working
└─ #BGAI-37 (Mobile responsive) ──── PARALLEL
```

---

## Sprint Velocity Assumptions

**Backend Engineer Velocity**:
- Story points: 5-8 per sprint (2 weeks)
- Complexity: Medium-high (new integrations, API clients)
- Uncertainty: Sprint 1-2 higher (learning LLMWhisperer, SmolDocling), Sprint 3+ lower

**Frontend Engineer Velocity**:
- Story points: 5-7 per sprint (2 weeks)
- Complexity: Medium (shadcn/ui familiar, Italian i18n straightforward)
- Uncertainty: Lower (established patterns, design mockups available)

**Velocity Tracking**: After Sprint 2, adjust estimates based on actual completion

---

## Communication Cadence

### Daily (Sprints 1-10)
- **9:00 AM**: Standup (15 min, async Slack if distributed team)
  - Yesterday completed
  - Today planned
  - Blockers

### Weekly (Every Monday)
- **Sprint Progress Review** (30 min)
  - Completed vs planned (on track?)
  - Burndown chart review
  - Adjust priorities if needed

### Bi-Weekly (End of Sprint)
- **Sprint Review** (1 hour, Fridays)
  - Demo deliverables to stakeholders
  - Metrics review (velocity, quality, performance)
  - Sprint retrospective (15 min)
  - Next sprint planning (30 min)

### Monthly (Last Friday)
- **Phase 1 Progress Review** (30 min, with leadership)
  - Overall status (Green/Yellow/Red)
  - Budget burn vs projections
  - Risks & mitigation
  - Go/No-Go gate check (if applicable)

---

## Critical Path Items (Cannot Delay)

🚨 **Sprint 1-2**: PDF processing MUST work (foundation for everything else)
🚨 **Sprint 3-4**: Multi-model validation MUST achieve >80% accuracy (MVP validation)
🚨 **Sprint 5-6**: Golden dataset MUST reach 100 Q&A (quality measurement)
🚨 **Sprint 7-8**: La Tana partnership MUST sign (100 beta users dependency)

**If any critical path delays >1 week**: Escalate to leadership, adjust Phase 1 timeline

---

## Success Criteria (Phase 1 Exit Gate - Jun 27, 2025)

### Must-Have (Non-Negotiable)

- ✅ 100 beta users recruited (La Tana dei Goblin confirmed)
- ✅ Accuracy ≥80% (validated on 100 Q&A golden dataset)
- ✅ Hallucination ≤10% (validated on 50 adversarial queries)
- ✅ User satisfaction ≥4.0/5.0 (post-beta survey, 50+ responses)
- ✅ Uptime ≥99% (during 2-week beta period, May 12-25)

### Nice-to-Have (Optimize if Time)

- 🎯 P95 latency <5s (baseline, optimize to <3s Phase 2)
- 🎯 20 games indexed (stretch, 10 is baseline)
- 🎯 Cache hit rate 30%+ (semantic caching, 40-60% Phase 2 target)
- 🎯 Giochi Uniti initial contact (Phase 2 prep)

---

**Document Metadata**:
- **Version**: 1.0
- **Created**: 2025-01-15
- **Purpose**: Parallel Execution Calendar for Board Game AI Phase 1
- **Team Size**: 2 Backend + 1 Frontend + 0.5 DevOps + 0.5 QA = 4 FTE
- **Duration**: 23 weeks (Jan 15 - Jun 27, 2025)
- **Next Update**: After Sprint 2 (Feb 21) - adjust based on actual velocity
