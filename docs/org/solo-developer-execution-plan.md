# Board Game AI - Solo Developer Execution Plan (Git Worktree Strategy)

**Team Size**: 1 Developer (Full-Stack)
**Strategy**: Git worktree per sviluppo parallelo (backend + frontend + microservices)
**Timeline**: Revised for solo execution (più realistico)
**Duration**: 9-12 mesi (vs 6 mesi con 4 FTE team)
**Last Updated**: 2025-01-15
**Status**: 📋 Issues to be created (~75-80 issues for Phase 1A)

---

## 🎯 GitHub Issues Tracking

### Issue Creation Status

**Current State**:
- ✅ Existing: #937 (DDD 5% complete), #940 (PDF migration)
- 🔴 Missing: ~75-80 issues need creation for Board Game AI features

**Issue Prefix**: `[BGAI-XXX]` for Board Game AI features

### Required Labels
```bash
# Create labels for Board Game AI tracking
gh label create "board-game-ai" --description "Board Game AI features" --color "0E8A16"
gh label create "month-1" --description "Month 1: PDF Processing" --color "D4C5F9"
gh label create "month-2" --description "Month 2: LLM Integration" --color "BFD4F2"
gh label create "month-3" --description "Month 3: Multi-Model Validation" --color "C5DEF5"
gh label create "month-4" --description "Month 4: Quality Framework" --color "F9D0C4"
gh label create "month-5" --description "Month 5: Golden Dataset" --color "FEF2C0"
gh label create "month-6" --description "Month 6: Italian UI" --color "BFDADC"
```

### Missing Issues Breakdown by Month

#### Month 1: PDF Processing Pipeline (15 issues)
- [ ] [BGAI-001] Setup LLMWhisperer account and API key configuration
- [ ] [BGAI-002] Implement LlmWhispererPdfExtractor (C# HttpClient wrapper)
- [ ] [BGAI-003] Add LLMWhisperer configuration (appsettings + feature flag)
- [ ] [BGAI-004] Unit tests for LlmWhispererPdfExtractor (12 tests)
- [ ] [BGAI-005] Create FastAPI service for SmolDocling (Python)
- [ ] [BGAI-006] Docker configuration for pdf-processor service
- [ ] [BGAI-007] Implement SmolDoclingPdfExtractor (C# client)
- [ ] [BGAI-008] Integration tests for SmolDocling pipeline
- [ ] [BGAI-009] Migrate to IPdfTextExtractor interface (existing #940)
- [ ] [BGAI-010] Implement EnhancedPdfProcessingOrchestrator (3-stage fallback)
- [ ] [BGAI-011] Integration tests for 3-stage PDF pipeline (end-to-end)
- [ ] [BGAI-012] PDF quality validation (≥0.80 quality score)
- [ ] [BGAI-013] Bug fixes and edge cases for PDF pipeline
- [ ] [BGAI-014] Code review checklist for PDF processing
- [ ] [BGAI-015] Documentation (README, API docs, code comments)

#### Month 2: LLM Integration (12 issues)
- [ ] [BGAI-016] Setup OpenRouter account and API key
- [ ] [BGAI-017] Implement OpenRouterClient (HttpClient wrapper)
- [ ] [BGAI-018] Dynamic model selection configuration (GPT-4, Claude)
- [ ] [BGAI-019] Unit tests for OpenRouterClient (12 tests)
- [ ] [BGAI-020] Implement OllamaClient (local LLM fallback)
- [ ] [BGAI-021] AdaptiveLlmService (routing logic: API vs local)
- [ ] [BGAI-022] Feature flag AI:Provider (OpenRouter/Ollama toggle)
- [ ] [BGAI-023] Integration tests for adaptive LLM routing
- [ ] [BGAI-024] Replace existing LLM calls in RagService
- [ ] [BGAI-025] Backward compatibility testing for RAG
- [ ] [BGAI-026] Performance testing (latency baseline <3s P95)
- [ ] [BGAI-027] Cost tracking (OpenRouter vs Ollama comparison)

#### Month 3: Multi-Model Validation (13 issues)
- [ ] [BGAI-028] ConfidenceValidationService (threshold ≥0.70)
- [ ] [BGAI-029] CitationValidationService (verify source references)
- [ ] [BGAI-030] HallucinationDetectionService (forbidden keywords)
- [ ] [BGAI-031] Unit tests for 3 validation layers
- [ ] [BGAI-032] MultiModelValidationService (GPT-4 + Claude consensus)
- [ ] [BGAI-033] Consensus similarity calculation (cosine similarity ≥0.90)
- [ ] [BGAI-034] Unit tests for consensus validation (18 tests)
- [ ] [BGAI-035] Wire all 5 validation layers in RAG pipeline
- [ ] [BGAI-036] End-to-end testing (question → validated response)
- [ ] [BGAI-037] Performance optimization (parallel validation)
- [ ] [BGAI-038] Bug fixes for validation edge cases
- [ ] [BGAI-039] Accuracy baseline measurement (target 80%+)
- [ ] [BGAI-040] Update ADRs with actual validation implementation

#### Month 4: Quality Framework + Frontend (15 issues)
- [ ] [BGAI-041] Extend PromptEvaluationService (5-metric framework)
- [ ] [BGAI-042] Automated evaluation job (weekly cron)
- [ ] [BGAI-043] Prometheus metrics (Board Game AI specific)
- [ ] [BGAI-044] Grafana dashboard (5-metric gauges)
- [ ] [BGAI-045] Quality framework integration tests
- [ ] [BGAI-046] Install shadcn/ui in Next.js app
- [ ] [BGAI-047] Design tokens (Italian color palette)
- [ ] [BGAI-048] Base components (Button, Card, Input, Form)
- [ ] [BGAI-049] i18n setup (React Intl, it.json)
- [ ] [BGAI-050] BoardGameAI page structure (/board-game-ai)
- [ ] [BGAI-051] Frontend component testing (Jest 90%+)
- [ ] [BGAI-052] Responsive design testing (320px-1920px)
- [ ] [BGAI-053] Accessibility audit (WCAG 2.1 AA)
- [ ] [BGAI-054] Frontend build optimization
- [ ] [BGAI-055] Month 4 integration testing

#### Month 5: Golden Dataset + Q&A Interface (14 issues)
- [ ] [BGAI-056] Annotation: Terraforming Mars (20 Q&A pairs)
- [ ] [BGAI-057] Annotation: Wingspan (15 Q&A pairs)
- [ ] [BGAI-058] Annotation: Azul (15 Q&A pairs)
- [ ] [BGAI-059] Quality test implementation (accuracy validation)
- [ ] [BGAI-060] Run first accuracy test (baseline measurement)
- [ ] [BGAI-061] QuestionInputForm component (frontend)
- [ ] [BGAI-062] ResponseCard component (answer, confidence, citations)
- [ ] [BGAI-063] GameSelector dropdown component
- [ ] [BGAI-064] Loading and error states (UI/UX)
- [ ] [BGAI-065] Jest tests for Q&A components (20 tests)
- [ ] [BGAI-066] Backend API integration (/api/v1/board-game-ai/ask)
- [ ] [BGAI-067] Streaming SSE support for real-time responses
- [ ] [BGAI-068] Error handling and retry logic
- [ ] [BGAI-069] Month 5 E2E testing

#### Month 6: Completion + Polish (16 issues)
- [ ] [BGAI-070] Annotation: Scythe, Catan, Pandemic (30 Q&A)
- [ ] [BGAI-071] Annotation: 7 Wonders, Agricola, Splendor (30 Q&A)
- [ ] [BGAI-072] Adversarial dataset (50 synthetic queries)
- [ ] [BGAI-073] PDF viewer integration (react-pdf)
- [ ] [BGAI-074] Citation click → jump to page functionality
- [ ] [BGAI-075] Mobile PDF viewer UX optimization
- [ ] [BGAI-076] PDF viewer tests (Jest + Playwright)
- [ ] [BGAI-077] Complete Italian UI strings (200+ translations)
- [ ] [BGAI-078] Game catalog page (/board-game-ai/games)
- [ ] [BGAI-079] Mobile responsive testing (all breakpoints)
- [ ] [BGAI-080] End-to-end testing (question → PDF citation)
- [ ] [BGAI-081] Accuracy validation (80% target on 100 Q&A)
- [ ] [BGAI-082] Performance testing (P95 latency <3s)
- [ ] [BGAI-083] Final bug fixes and polish
- [ ] [BGAI-084] Documentation updates (user guide, README)
- [ ] [BGAI-085] Phase 1A completion checklist

**Total**: 85 issues needed for Phase 1A (Months 1-6)

### Issue Creation Script

```bash
# Run this script to create all Board Game AI issues
# Location: tools/create-bgai-issues.sh

#!/bin/bash

# Month 1 issues
gh issue create --title "[BGAI-001] Setup LLMWhisperer account and API key configuration" \
  --body "Configure LLMWhisperer API for Stage 1 PDF extraction" \
  --label "board-game-ai,month-1,backend" \
  --milestone "Month 1: PDF Processing"

# ... (repeat for all 85 issues)
```

---

## Adjusted Timeline for Solo Developer

### Original Estimate (4 FTE Team): 23 weeks (6 months)
- 2 Backend + 1 Frontend + 0.5 DevOps + 0.5 QA = 4 FTE
- Effort: 230 backend + 115 frontend + 57 DevOps/QA = ~400 developer-days
- Calendar: 6 months

### Revised for Solo (1 FTE): 80-100 weeks (9-12 months)
- 1 Full-Stack Developer
- Effort: Same 400 developer-days, but sequential + context switching overhead
- **Context Switching Tax**: +20% (switching between backend/frontend/DevOps)
- **Total**: 400 × 1.2 = 480 developer-days ≈ **96 weeks** (assuming 5 days/week)
- **Realistic Calendar**: 12 months (con alcune settimane parallele via git worktree)

---

## Git Worktree Strategy (Parallel Development)

### Setup: 3 Worktrees (Backend, Frontend, Microservices)

```bash
# Main worktree (coordination, docs)
cd D:\Repositories\meepleai-monorepo

# Create worktree for backend work
git worktree add D:\Repositories\meepleai-backend feature/bgai-backend

# Create worktree for frontend work
git worktree add D:\Repositories\meepleai-frontend feature/bgai-frontend

# Create worktree for Python microservice (SmolDocling)
git worktree add D:\Repositories\meepleai-pdf-processor feature/bgai-pdf-processor

# List worktrees
git worktree list
```

**Directory Structure**:
```
D:\Repositories\
├── meepleai-monorepo/           # Main (docs, coordination, reviews)
├── meepleai-backend/            # Backend work (C# ASP.NET Core)
├── meepleai-frontend/           # Frontend work (Next.js, React)
└── meepleai-pdf-processor/      # Python microservice (SmolDocling)
```

---

## Workflow Pattern (Parallel Context Switching)

### Daily Pattern (8-Hour Workday)

**Morning (4 hours): Backend Focus**
```bash
# Switch to backend worktree
cd D:\Repositories\meepleai-backend

# Work on current backend task
# - LLMWhisperer integration
# - OpenRouter client
# - Multi-model validation
# etc.

# Commit progress (small, frequent commits)
git add .
git commit -m "feat(bgai): LLMWhisperer API integration (WIP)"
```

**Afternoon (3 hours): Frontend Focus**
```bash
# Switch to frontend worktree (different terminal/IDE window)
cd D:\Repositories\meepleai-frontend

# Work on current frontend task
# - shadcn/ui setup
# - Q&A interface
# - Italian i18n
# etc.

# Commit progress
git add .
git commit -m "feat(bgai): Q&A interface component (WIP)"
```

**End of Day (1 hour): Integration & Testing**
```bash
# Back to main worktree
cd D:\Repositories\meepleai-monorepo

# Merge both branches (if features complete)
git checkout main
git merge feature/bgai-backend
git merge feature/bgai-frontend

# Run full test suite (backend + frontend)
cd apps/api && dotnet test
cd apps/web && pnpm test

# Push to remote
git push origin main
```

---

## Optimized Sprint Plan (Solo Developer)

### Phase 1A: MVP Core (Months 1-6, Solo)

**Month 1 (4 weeks): PDF Processing Pipeline**

**Week 1: LLMWhisperer Integration** (Days 1-5)
- Day 1: Setup LLMWhisperer account (#941 - [BGAI-001])
- Day 2-3: Implement LlmWhispererPdfExtractor (#942 - [BGAI-002])
- Day 4: Add configuration (#943 - [BGAI-003])
- Day 5: Unit tests (#944 - [BGAI-004])
- **Deliverable**: LLMWhisperer Stage 1 working

**Week 2: SmolDocling Python Service** (Days 6-10)
- Day 6-7: FastAPI service implementation (#945 - [BGAI-005])
- Day 8: Docker configuration (#946 - [BGAI-006])
- Day 9: SmolDoclingPdfExtractor C# client ([BGAI-007] - to create)
- Day 10: Integration tests ([BGAI-008] - to create)
- **Deliverable**: SmolDocling Stage 2 working

**Week 3: PDF Orchestration** (Days 11-15)
- Day 11-12: IPdfTextExtractor migration ([BGAI-009] = #940)
- Day 13-14: EnhancedPdfProcessingOrchestrator ([BGAI-010])
- Day 15: Integration tests ([BGAI-011])
- **Quality Gate**: PDF quality validation ([BGAI-012])
- **Deliverable**: 3-stage pipeline end-to-end

**Week 4: Buffer & Documentation** (Days 16-20)
- Day 16-17: Bug fixes, edge cases ([BGAI-013])
- Day 18: Code review checklist ([BGAI-014])
- Day 19-20: Documentation ([BGAI-015])
- **Deliverable**: PDF processing COMPLETE, ready for RAG integration

**Month 1 Output**: ✅ PDF pipeline working (quality ≥95% on modern PDFs)

---

**Month 2 (4 weeks): LLM Integration (OpenRouter + Ollama)**

**Week 5: OpenRouter Client** (Days 21-25)
- Day 21: Setup OpenRouter account ([BGAI-016])
- Day 22-23: Implement OpenRouterClient ([BGAI-017])
- Day 24: Dynamic model configuration ([BGAI-018])
- Day 25: Unit tests (12 tests) ([BGAI-019])
- **Deliverable**: OpenRouter GPT-4 + Claude callable

**Week 6: Ollama Fallback** (Days 26-30)
- Day 26-27: OllamaClient implementation ([BGAI-020])
- Day 28-29: AdaptiveLlmService routing logic ([BGAI-021])
- Day 30: Feature flag AI:Provider ([BGAI-022])
- **Quality Gate**: Integration tests ([BGAI-023])
- **Deliverable**: Ollama free fallback working

**Week 7: RagService Integration** (Days 31-35)
- Day 31-32: Replace existing LLM calls ([BGAI-024])
- Day 33-34: Backward compatibility testing ([BGAI-025])
- Day 35: Integration tests RAG end-to-end
- **Deliverable**: Adaptive LLM in production RAG pipeline

**Week 8: Buffer** (Days 36-40)
- Day 36-37: Performance testing ([BGAI-026])
- Day 38: Cost tracking comparison ([BGAI-027])
- Day 39-40: Documentation updates
- **Deliverable**: Month 2 COMPLETE with performance baseline

**Month 2 Output**: ✅ LLM integration complete, OpenRouter + Ollama proven

---

**Month 3 (4 weeks): Multi-Model Validation**

**Week 9: Validation Services Part 1** (Days 41-45)
- Day 41: ConfidenceValidationService ([BGAI-028])
- Day 42-43: CitationValidationService ([BGAI-029])
- Day 44-45: HallucinationDetectionService ([BGAI-030])
- **Quality Gate**: Unit tests ([BGAI-031])
- **Deliverable**: 3 of 5 validation layers working

**Week 10: Multi-Model Consensus** (Days 46-50)
- Day 46-47: MultiModelValidationService ([BGAI-032])
- Day 48: Consensus similarity calculation ([BGAI-033])
- Day 49-50: Unit tests (18 tests) ([BGAI-034])
- **Deliverable**: GPT-4 + Claude consensus at 0.90

**Week 11: Full Pipeline Integration** (Days 51-55)
- Day 51-52: Wire all 5 validation layers ([BGAI-035])
- Day 53-54: End-to-end testing ([BGAI-036])
- Day 55: Performance optimization ([BGAI-037])
- **Deliverable**: Full validation pipeline working

**Week 12: Buffer** (Days 56-60)
- Day 56-57: Bug fixes edge cases ([BGAI-038])
- Day 58: Accuracy baseline measurement ([BGAI-039])
- Day 59-60: Update ADRs ([BGAI-040])
- **Deliverable**: Month 3 COMPLETE with accuracy baseline

**Month 3 Output**: ✅ Multi-model validation complete, ready for quality framework

---

**Month 4 (4 weeks): Quality Framework + Frontend Foundation**

**Week 13: Quality Metrics Backend** (Days 61-65)
- Day 61-62: Extend PromptEvaluationService ([BGAI-041])
- Day 63-64: Automated evaluation job ([BGAI-042])
- Day 65: Prometheus metrics ([BGAI-043])
- **Deliverable**: 5-metric framework foundation

**Week 14: Quality Metrics Completion** (Days 66-70)
- Day 66-67: Grafana dashboard ([BGAI-044])
- Day 68-70: Integration tests ([BGAI-045])
- **Deliverable**: 5-metric framework operational

**Week 15: Frontend Foundation** (Days 71-75)
- Day 71: Install shadcn/ui ([BGAI-046])
- Day 72-73: Design tokens ([BGAI-047])
- Day 74-75: Base components ([BGAI-048])
- **Deliverable**: shadcn/ui installed and configured

**Week 16: Frontend Completion** (Days 76-80)
- Day 76-77: i18n setup ([BGAI-049])
- Day 78-79: BoardGameAI page structure ([BGAI-050])
- Day 80: Component testing ([BGAI-051])
- **Quality Gate**: Responsive design ([BGAI-052]), Accessibility ([BGAI-053])
- **Final**: Build optimization ([BGAI-054]), Integration testing ([BGAI-055])
- **Deliverable**: Frontend foundation complete

**Month 4 Output**: ✅ Quality framework + Frontend foundation both complete

---

**Month 5 (4 weeks): Golden Dataset + Q&A Interface**

**Week 17: Golden Dataset Part 1** (Days 81-85)
- Day 81-82: Terraforming Mars annotation ([BGAI-056])
- Day 83-84: Wingspan annotation ([BGAI-057])
- Day 85: Azul annotation ([BGAI-058])
- **Deliverable**: 50 Q&A pairs annotated

**Week 18: Quality Testing** (Days 86-90)
- Day 86-87: Quality test implementation ([BGAI-059])
- Day 88-89: Run first accuracy test ([BGAI-060])
- Day 90: Baseline analysis and reporting
- **Deliverable**: Accuracy baseline established

**Week 19: Q&A Interface Components** (Days 91-95)
- Day 91-92: QuestionInputForm component ([BGAI-061])
- Day 93-94: ResponseCard component ([BGAI-062])
- Day 95: GameSelector dropdown ([BGAI-063])
- **Deliverable**: Core Q&A components built

**Week 20: Q&A Interface Integration** (Days 96-100)
- Day 96: Loading and error states ([BGAI-064])
- Day 97-98: Jest tests (20 tests) ([BGAI-065])
- Day 99: Backend API integration ([BGAI-066])
- Day 100: Streaming SSE support ([BGAI-067]), Error handling ([BGAI-068])
- **Quality Gate**: E2E testing ([BGAI-069])
- **Deliverable**: Q&A interface fully functional

**Month 5 Output**: ✅ 50 Q&A dataset + Q&A UI functional

---

**Month 6 (4 weeks): Dataset Completion + PDF Viewer + Italian UI**

**Week 21: Dataset Expansion** (Days 101-105)
- Day 101-102: Scythe, Catan, Pandemic ([BGAI-070])
- Day 103-104: 7 Wonders, Agricola, Splendor ([BGAI-071])
- Day 105: Adversarial dataset ([BGAI-072])
- **Deliverable**: 100 Q&A complete + 50 adversarial

**Week 22: PDF Viewer** (Days 106-110)
- Day 106-107: PDF viewer integration ([BGAI-073])
- Day 108: Citation navigation ([BGAI-074])
- Day 109: Mobile PDF UX ([BGAI-075])
- Day 110: PDF viewer tests ([BGAI-076])
- **Deliverable**: PDF viewer with citation navigation

**Week 23: Italian Localization** (Days 111-115)
- Day 111-112: Italian UI strings 200+ ([BGAI-077])
- Day 113-114: Game catalog page ([BGAI-078])
- Day 115: Mobile responsive testing ([BGAI-079])
- **Deliverable**: Full Italian UI

**Week 24: Final Integration & Testing** (Days 116-120)
- Day 116-117: End-to-end testing ([BGAI-080])
- Day 118: Accuracy validation 80% target ([BGAI-081])
- Day 119: Performance testing P95 <3s ([BGAI-082])
- Day 120: Final bug fixes and polish ([BGAI-083])
- **Final Deliverables**: Documentation ([BGAI-084]), Completion checklist ([BGAI-085])
- **Deliverable**: Phase 1A COMPLETE (core features working)

**Month 6 Output**: ✅ 100 Q&A dataset + Full Italian UI + E2E tested

**GATE 1 DECISION** (End Month 6): Tech viable for 95%+ accuracy? Continue to Phase 1B (Beta) or pivot?

---

### Phase 1B: Beta Testing (Months 7-9, Solo)

**Month 7 (4 weeks): Beta Preparation**

**Week 25-26: Backend Polish**
- Semantic caching (Redis FAISS) - 3d
- Smart model routing (cost optimization) - 2d
- Rate limiting per tier - 1d
- Analytics endpoints - 2d
- Bug fixes - 2d

**Week 27-28: Beta Environment**
- Deploy to staging (DigitalOcean/Heroku) - 2d
- Monitor setup (Prometheus, Grafana) - 2d
- Index 10 Italian games (rulebook processing) - 2d
- Beta signup page + onboarding - 2d
- La Tana dei Goblin outreach - 2d

**Month 7 Output**: ✅ Beta environment ready, monitoring active

---

**Month 8 (4 weeks): Beta Launch & Support**

**Week 29: Beta Recruitment**
- La Tana announcement (forum post, Discord) - 1d
- Multi-community outreach (Bottega Ludica, GiocaOggi) - 2d
- Social media campaign (Twitter/X, Facebook groups) - 1d
- Monitor signups (target: 100 in 2 weeks) - 1d

**Week 30: Beta Active Support**
- User support (Discord #beta-support, email) - 3d
- Bug triage & quick fixes (P0 bugs) - 2d

**Week 31-32: Feedback Collection**
- User interviews (5-10 users, 30 min each) - 3d
- Survey distribution + analysis - 2d
- Accuracy spot-checking (50 user queries manual validation) - 2d
- Top 5 feature requests prioritization - 1d

**Month 8 Output**: ✅ 100 beta users, feedback collected, satisfaction measured

---

**Month 9 (4 weeks): Iteration & Polish**

**Week 33-34: User Feedback Implementation**
- Top 3 backend features (e.g., query history, comparison) - 4d
- Top 3 frontend UX improvements (e.g., bookmarks, share) - 3d
- Accessibility audit (WCAG 2.1 AA) - 2d
- Testing - 1d

**Week 35-36: Phase 1 Finalization**
- Regression testing (all 100 Q&A) - 2d
- Performance optimization (P95 <5s) - 2d
- Documentation update (CLAUDE.md, user guide) - 2d
- Phase 1 retrospective (self-review) - 1d
- Phase 2 planning - 3d

**Month 9 Output**: ✅ Phase 1 COMPLETE (MVP validated, ready for Phase 2 publishers)

**GATE 2** (End Month 9): Proceed to Phase 2? (100 beta users? 80% accuracy? 4.0/5.0 satisfaction?)

---

## Git Worktree Workflow (Maximize Parallel Work)

### Pattern 1: Independent Features (True Parallel)

**Scenario**: Backend API development + Frontend UI development (no integration yet)

**Workflow**:
```bash
# Morning: Backend work (4 hours)
cd D:\Repositories\meepleai-backend
code .  # Open in VS Code instance 1

# Work on LLMWhisperer integration
# ... coding, testing ...

# Commit progress
git add .
git commit -m "feat(bgai): LLMWhisperer extractor - API client complete"

# Afternoon: Frontend work (3 hours)
cd D:\Repositories\meepleai-frontend
code .  # Open in VS Code instance 2 (separate window)

# Work on shadcn/ui setup (completely independent)
# ... coding, testing ...

# Commit progress
git add .
git commit -m "feat(bgai): shadcn/ui installed + base components"

# End of day: Merge both (if ready)
cd D:\Repositories\meepleai-monorepo
git checkout main
git merge feature/bgai-backend  # Fast-forward merge
git merge feature/bgai-frontend  # Fast-forward merge
git push origin main
```

**Time Saved**: ~20% (context switching minimized, no mental model reload)

---

### Pattern 2: Waiting for Build/Test (Async Parallel)

**Scenario**: Backend tests running (5 min) → switch to frontend work

**Workflow**:
```bash
# Backend worktree
cd D:\Repositories\meepleai-backend

# Start long-running test
dotnet test &  # Background process

# Immediately switch to frontend (while tests run)
cd D:\Repositories\meepleai-frontend

# 5 minutes frontend work (design tokens, small UI fix)
# ... quick productive work ...

# Check backend tests (likely finished)
cd D:\Repositories\meepleai-backend
# Tests passed? ✅ Commit
# Tests failed? Fix and repeat
```

**Time Saved**: ~10-15% (utilize waiting time productively)

---

### Pattern 3: Microservice Development (3-Way Parallel)

**Scenario**: SmolDocling Python service + C# client + Frontend PDF viewer

**Workflow**:
```bash
# Terminal 1: Python microservice
cd D:\Repositories\meepleai-pdf-processor
code apps/pdf-processor/  # VS Code Python

# Implement FastAPI endpoint
# ...

# Terminal 2: C# client (waiting for Python service ready)
cd D:\Repositories\meepleai-backend
code apps/api/  # VS Code C#

# Implement SmolDoclingPdfExtractor (stub first, real impl when service ready)
# ...

# Terminal 3: Frontend (independent, can work on PDF viewer UI while backend develops)
cd D:\Repositories\meepleai-frontend
code apps/web/  # VS Code TypeScript

# Design PDF viewer component (mockup, no real backend yet)
# ...
```

**Integration Point**: Week 2 end - all 3 pieces merge and integrate

---

## Revised Sprint Breakdown (Solo Execution)

### Revised Phases (More Realistic for Solo)

**Phase 1A: Core Features** (6 months)
- Month 1: PDF Processing (LLMWhisperer + SmolDocling + Docnet)
- Month 2: LLM Integration (OpenRouter + Ollama)
- Month 3: Multi-Model Validation (5 layers)
- Month 4: Quality Framework (5-metric) + Frontend Foundation
- Month 5: Golden Dataset (100 Q&A) + Q&A Interface
- Month 6: Italian UI + PDF Viewer + Integration Testing

**Phase 1B: Beta Testing** (3 months)
- Month 7: Beta Prep (deployment, monitoring, 10 games indexed)
- Month 8: Beta Launch (100 users, support, feedback)
- Month 9: Iteration (user requests, polish, Phase 1 wrap-up)

**Total Phase 1**: **9 months** (vs 6 months with 4 FTE team)

---

## Prioritization for Solo Developer (Focus Areas)

### High Priority (Must Do for MVP)

**Backend (60% of time)**:
1. ✅ PDF processing (LLMWhisperer + SmolDocling + Docnet) - Month 1
2. ✅ Multi-model validation (OpenRouter: GPT-4 + Claude) - Month 2-3
3. ✅ 5-metric quality framework - Month 4
4. ✅ Golden dataset 100 Q&A - Month 5-6

**Frontend (30% of time)**:
1. ✅ Q&A interface (question → response → citations) - Month 5
2. ✅ Italian i18n (UI strings, terminology) - Month 6
3. ✅ Mobile responsive (320px-1920px) - Month 6
4. ✅ PDF viewer (citation navigation) - Month 6

**DevOps (10% of time)**:
1. ✅ SmolDocling Docker deployment - Month 1
2. ✅ Monitoring (Prometheus, Grafana) - Month 7
3. ✅ Beta deployment (staging) - Month 7

---

### Medium Priority (Nice-to-Have, Phase 2 if Time Tight)

- Semantic caching (40-60% hit rate) → Defer to Month 7 or Phase 2
- Smart model routing (cost optimization) → Defer to Phase 2
- Adversarial dataset (50 synthetic) → Defer to Phase 2 (use 100 Q&A golden for MVP)
- Advanced frontend (animations, dark mode) → Defer to Phase 2

---

### Low Priority (Explicitly Defer to Phase 2+)

- Fine-tuned Italian embeddings → Phase 3 (validated need first)
- Hybrid search (Qdrant sparse vectors) → Phase 3 (existing dense vectors sufficient for 80% accuracy)
- White-label B2B features → Phase 2 (after 1-2 publishers signed)
- API rate limiting advanced (per-publisher quotas) → Phase 2

---

## Time Optimization Techniques (Solo Developer)

### 1. Batch Similar Work (Reduce Context Switching)

**Instead of**: Backend task → Frontend task → Backend task (daily switching)

**Do**: Backend week → Frontend week → Integration week (weekly batching)

**Example (Month 1)**:
- Week 1-2: Pure backend (PDF processing) - Stay in backend worktree entire 2 weeks
- Week 3: Pure frontend (Foundation) - Stay in frontend worktree
- Week 4: Integration + testing - Switch between worktrees as needed

**Time Saved**: ~15-20% (context switching tax reduced)

---

### 2. Use Existing Code Aggressively (DRY)

**Reuse from Existing MeepleAI**:
- ✅ PdfTextExtractionService (DDD Phase 4) → Just add LLMWhisperer + SmolDocling stages
- ✅ RagService → Extend with multi-model validation
- ✅ PromptEvaluationService (ADMIN-01 Phase 4) → Add 5-metric framework
- ✅ HybridCacheService → Add semantic layer for LLM responses
- ✅ ConfigurationService → Use for dynamic AI provider selection

**Time Saved**: ~30-40% (avoid reinventing, focus on deltas)

---

### 3. Defer Non-Critical Testing (Pragmatic Coverage)

**MVP Testing Strategy (Solo Developer)**:
- Unit tests: 80% coverage (vs 90% team standard) - Save 10-15% time
- Integration tests: Critical paths only (PDF pipeline, RAG end-to-end) - Save 20% time
- E2E tests: Happy path only (defer edge cases to Phase 2) - Save 30% time
- Manual testing: Self-QA for UX (defer formal usability testing to beta)

**Trade-off**: Lower test coverage, but faster MVP (catch bugs in beta feedback)

**Phase 2**: Increase coverage back to 90% (after MVP validated)

---

### 4. Automate Repetitive Tasks

**Scripts to Create** (Save 5-10 hours over 9 months):
- `scripts/annotate-qa.sh` - Template generator for golden dataset annotation
- `scripts/test-pdf-pipeline.sh` - Automated testing on 5 sample rulebooks
- `scripts/deploy-staging.sh` - One-command staging deployment
- `scripts/run-quality-tests.sh` - Weekly 5-metric evaluation automation

---

### 5. Use AI Coding Assistants (Claude Code!)

**Tasks to Delegate to Claude Code**:
- Boilerplate generation (C# interfaces, DTO classes)
- Test scaffolding (xUnit test class templates)
- Documentation (code comments, README updates)
- Refactoring (DRY violations, code smells)

**Time Saved**: ~10-15% (mundane tasks automated)

**Example**:
```
Prompt: "Create xUnit tests for LlmWhispererPdfExtractor with 10 test cases covering happy path, API errors, timeout, invalid PDF, quality threshold scenarios"

Claude generates: Full test class with mocks, assertions, arrange-act-assert pattern
```

---

## Realistic Timeline (Solo Developer)

### Conservative Estimate (12 months)

**Phase 1 MVP**: 9 months (Jan 2025 - Sep 2025)
- Months 1-6: Core features (PDF, LLM, validation, quality, UI)
- Months 7-9: Beta testing + iteration

**Buffer**: 3 months (Oct-Dec 2025) for unknowns, learning curve, distractions

**Phase 1 Complete**: **December 2025** (12 months from start)

---

### Aggressive Estimate (9 months)

**Phase 1 MVP**: 9 months (Jan 2025 - Sep 2025)
- Work 100% focused (no distractions, no other projects)
- Use Claude Code aggressively (20% time savings)
- Defer low-priority features (semantic cache, adversarial dataset)
- Accept 80% test coverage (vs 90%)

**Phase 1 Complete**: **September 2025** (9 months from start)

**Risk**: Burnout (sustained 9-month sprint), quality compromises

---

### Recommended (10 months with Sustainable Pace)

**Phase 1A Core**: 6 months (Jan - Jun 2025)
**Phase 1B Beta**: 3 months (Jul - Sep 2025)
**Buffer**: 1 month (Oct 2025) for unexpected delays

**Phase 1 Complete**: **October 2025** (10 months)
**Phase 2 Start**: November 2025 (Publisher outreach, production scale)

**Breakeven**: April-June 2027 (30 months from start vs 24-30 with team)

---

## Git Worktree Best Practices (Solo Developer)

### Daily Routine

**Morning Routine** (30 min):
1. Check all worktrees status: `git worktree list`
2. Pull latest from main: `cd <each worktree> && git pull origin main`
3. Plan today's work (backend or frontend focus?)
4. Open relevant VS Code instance(s)

**End of Day** (30 min):
1. Commit work in each active worktree
2. Merge to main (if features complete)
3. Push to remote (backup)
4. Update GitHub Project board (move cards)

---

### Weekly Routine

**Friday Wrap-Up** (2 hours):
1. Sprint review (self-assessment): What completed? What blocked?
2. Merge all worktree branches to main (integration testing)
3. Run full test suite (backend + frontend)
4. Deploy to staging (if significant changes)
5. Plan next week (prioritize backend vs frontend)

**Sunday Planning** (1 hour, optional):
1. Review Board Game AI docs (roadmap, sprint overview)
2. Identify next week's top 3 tasks
3. Prepare environment (dependencies, setup)

---

### Monthly Routine

**Last Friday of Month** (3 hours):
1. Month retrospective (what worked, what didn't)
2. Metrics review (velocity, accuracy baseline, costs)
3. Adjust timeline if needed (on track for 10-month target?)
4. Update docs (progress notes, lessons learned)

---

## Scope Reductions for Solo Execution

### Features Cut from Phase 1 (Move to Phase 2)

**Backend Features Deferred**:
- ❌ Hybrid search (Qdrant sparse vectors) → Phase 3
- ❌ Fine-tuned Italian embeddings → Phase 3 (use base multilingual)
- ❌ A/B testing framework → Phase 2 (manual experiments sufficient MVP)
- ❌ Advanced analytics (per-game breakdown) → Phase 2

**Frontend Features Deferred**:
- ❌ Dark mode → Phase 2 (Italian UI light mode only)
- ❌ PWA offline mode → Phase 2 (web-only for MVP)
- ❌ Animations & transitions → Phase 2 (functional > beautiful)
- ❌ Advanced filtering (game catalog) → Phase 2 (basic search sufficient)

**Games Scope Reduced**:
- ❌ 20 games → **10 games** for MVP (still validates market)
- ❌ 1000 Q&A dataset → **100 Q&A** (sufficient for 80% accuracy validation)

**Impact**: Reduces scope ~30%, timeline 12 months → **10 months** realistic

---

## Motivation & Sustainability (Solo Marathon)

### Avoid Burnout (Critical for Solo)

**Weekly Work Schedule** (Sustainable):
- Monday-Friday: 8 hours/day (40 hours/week)
- Saturday: 4 hours (optional, catch-up or learning)
- Sunday: OFF (mandatory rest, no coding)

**Vacation/Breaks**:
- 1 week off every 3 months (Month 3, 6, 9) - Prevents burnout
- Adjust timeline: 10 months + 3 weeks = **10.75 months realistic**

---

### Progress Tracking (Gamification)

**Weekly Wins** (Friday Slack post to self):
```
Week X Wins:
✅ Completed #BGAI-5 (SmolDocling client working!)
✅ 20 Q&A annotated (Terraforming Mars complete)
🎯 Next week: Multi-model consensus implementation

Metrics:
- Accuracy baseline: 72% (improving!)
- Test coverage: 85% (on track)
- Sprint velocity: 7 story points (sustainable pace)
```

**Monthly Milestones** (Celebrate Solo Achievements):
- Month 1: 🍕 Pizza night (PDF pipeline complete)
- Month 3: 🎮 Buy new board game (multi-model validation working)
- Month 6: 🍾 Nice dinner (core features done, beta ready)
- Month 9: 🏆 Weekend trip (Phase 1 COMPLETE!)

---

## Solo Developer Advantages

**Flexibility**:
- ✅ No meetings (save 5-10 hours/week vs 4-person team)
- ✅ No code review delays (self-review, faster iteration)
- ✅ No merge conflicts (single developer, clean git history)
- ✅ Deep focus (4-hour deep work blocks possible)

**Ownership**:
- ✅ Full stack control (understand entire system)
- ✅ Architectural decisions (no committee, faster choices)
- ✅ Quality bar (your standards, no compromises)

**Learning**:
- ✅ Master full stack (C# backend, React frontend, Python microservices, DevOps)
- ✅ Domain expertise (board games, Italian market, RAG architecture, LLM validation)
- ✅ Business skills (product, marketing, publisher partnerships)

---

## Final Timeline Recommendation (Solo)

### Realistic Path (10 Months + Buffer)

```
2025
JAN  FEB  MAR  APR  MAY  JUN  JUL  AUG  SEP  OCT  NOV  DEC
│────│────│────│────│────│────│────│────│────│────│────│────│
│ Setup                                                      │
│    │ PDF Pipeline                                          │
│    │    │ LLM Integration                                  │
│    │    │    │ Multi-Model Validation                      │
│    │    │    │    │ Quality Framework                      │
│    │    │    │    │    │ Dataset + UI                      │
│    │    │    │    │    │    │ Beta Prep                    │
│    │    │    │    │    │    │    │ Beta Launch             │
│    │    │    │    │    │    │    │    │ Iteration          │
│    │    │    │    │    │    │    │    │    │ Buffer        │
│    │    │    │    │    │    │    │    │    │    │ Phase 2 │
└────┴────┴────┴────┴────┴────┴────┴────┴────┴────┴────┴────┘
     PHASE 1A (Core)         PHASE 1B (Beta)      P2 Start

GATE 1 (Month 6): Tech viable? ──────────────────▲
GATE 2 (Month 9): MVP validated? ─────────────────────────────▲
```

**Completion Target**: **October 2025** (10 months from Jan 15 start)

**Phase 2 Start**: November 2025 (Publisher outreach, production launch)

**Breakeven**: June-August 2027 (~30 months total vs 24-30 with team)

---

## Success Criteria (Adjusted for Solo)

### Must-Have (Non-Negotiable)

- ✅ Core features working (PDF, multi-model, quality framework)
- ✅ 80% accuracy on 100 Q&A (vs 95%+ Phase 3 target)
- ✅ 100 beta users (community validation)
- ✅ User satisfaction ≥4.0/5.0 (product-market fit)

### Can Compromise (Solo Constraints)

- ⚠️ Test coverage: 80% acceptable (vs 90% team standard)
- ⚠️ Latency: P95 <5s acceptable MVP (vs <3s Phase 2 target)
- ⚠️ Cache hit rate: 20-30% OK (vs 40-60% Phase 2 target)
- ⚠️ Games: 10 indexed acceptable (vs 20 stretch goal)

**Rationale**: Solo developer focuses on **core quality** (accuracy, no hallucinations) over **peripheral optimizations** (performance, scale)

---

## Next Steps (Immediate - Solo)

### This Week (Jan 15-19, 2025)

**Tuesday Jan 14** (TODAY):
- [x] Read QUICK-START.md ✅
- [ ] Read Implementation Notes (technology corrections)
- [ ] Read Consolidation Strategy (integration approach)

**Wednesday Jan 15**:
- [ ] Setup LLMWhisperer account + test 1 PDF
- [ ] Setup OpenRouter account + $50 credits
- [ ] Pull Ollama models (mistral, llama3, nomic-embed-text)

**Thursday Jan 16**:
- [ ] Prototype SmolDocling Python service (local test)
- [ ] Create git worktrees (backend, frontend, pdf-processor)

**Friday Jan 17**:
- [ ] Self-planning session (3 hours):
  - Month 1 detailed task breakdown
  - GitHub Issues creation (prioritize first 10 issues)
  - Environment verification (Docker Compose all services healthy)

---

### Week 2 (Jan 20-26, 2025)

**Monday Jan 20**:
- [ ] Start Month 1 Week 1: LLMWhisperer integration
- [ ] Open backend worktree in VS Code
- [ ] Implement LlmWhispererPdfExtractor (Day 1-3)

**Thursday Jan 23**:
- [ ] LLMWhisperer integration complete
- [ ] Tests passing (10 unit tests)
- [ ] Commit & push

**Friday Jan 24**:
- [ ] Self-review (code quality, documentation)
- [ ] Week 1 retrospective (what worked? adjust Week 2 plan)
- [ ] Prepare for Week 2 (SmolDocling service)

---

### Month 1 Target (End Jan 2025)

- ✅ PDF processing pipeline complete (3-stage fallback working)
- ✅ Process 10 Italian rulebooks successfully
- ✅ Quality scores validated (≥0.80 achievable)
- ✅ Tests passing (80%+ coverage)
- ✅ Documentation updated (code comments, README)

**Self-Assessment**: On track for 10-month timeline? Adjust Month 2 if needed.

---

## Tools & Productivity (Solo Developer Stack)

### Development Tools
- **VS Code**: 3 instances (backend, frontend, pdf-processor worktrees)
- **GitHub Projects**: Solo board (Backlog, This Week, In Progress, Done)
- **Claude Code**: AI pair programmer (boilerplate, tests, refactoring)
- **Postman**: API testing (LLMWhisperer, OpenRouter, own APIs)

### Productivity Tools
- **Todoist** or **Notion**: Daily task tracking (outside git)
- **RescueTime**: Track time allocation (backend vs frontend balance)
- **Pomodoro Timer**: 25-min focus blocks (reduce burnout)

### Communication (Solo)
- **Slack/Discord**: Update #board-game-ai channel (even solo, good for stakeholders)
- **Monthly Blog Post**: Progress updates (builds community interest, pre-marketing)
- **GitHub Discussions**: Document decisions, questions, learnings

---

## FAQs (Solo Developer)

**Q: 10 months is too long, can I do it in 6?**
A: Possible if: (1) Work 60 hours/week (unsustainable), (2) Cut scope 50% (5 games, 50 Q&A, no frontend polish), (3) Accept 70% accuracy (MVP only). Not recommended - quality suffers, burnout likely.

**Q: Should I hire freelancers to help?**
A: Consider for: (1) Golden dataset annotation (€500-1000 for 100 Q&A), (2) Italian translation (professional, €300-500), (3) Beta user support (Month 8, part-time). Don't hire for: Core development (ramp-up time costs more than solo execution).

**Q: Can I use Claude Code for entire features?**
A: Yes for boilerplate (60-80% of code), no for architecture decisions (10-20% requires human judgment). Example: Claude generates LlmWhispererPdfExtractor class (80%), you review + adjust error handling + add business logic (20%).

**Q: What if I get stuck on a hard problem?**
A: (1) Ask Claude Code (80% of blockers solvable), (2) Search GitHub Issues (similar problems), (3) Community forums (Stack Overflow, Reddit), (4) Paid consult (hire expert for 2-4 hours, €200-400, for critical blockers only).

---

## Success Mindset (Solo Execution)

**Think Marathon, Not Sprint**:
- 10 months is long - pace yourself
- Celebrate small wins weekly
- Take breaks (1 week every 3 months)
- Ship iteratively (working increments, not perfect)

**Focus on MVP** (Minimum Viable Product):
- 80% accuracy > 95% accuracy (validate market first)
- 10 games > 20 games (sufficient for beta)
- Functional UI > Beautiful UI (polish in Phase 2)
- Real user feedback > Perfect code (iterate based on users)

**You Can Do This** 💪:
- Existing system is 90% there (PDF, RAG, frontend all working)
- Adding deltas (LLMWhisperer, multi-model, Italian UI) is incremental
- Community (La Tana dei Goblin) wants this (demand validated)
- First-mover advantage (no Italian competitors)

**Andiamo! 🚀**

---

**Document Metadata**:
- **Version**: 1.0 (Solo Developer Edition)
- **Audience**: Solo Full-Stack Developer
- **Purpose**: Realistic Execution Plan for 1-Person Team
- **Timeline**: 10 months (Jan 2025 - Oct 2025)
- **Status**: Ready for Execution
