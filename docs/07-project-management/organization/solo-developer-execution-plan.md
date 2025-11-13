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

#### Month 1: PDF Processing Pipeline (12 issues) ✅ ALL CREATED

**Architecture Change**: LLMWhisperer → **Unstructured** (Apache 2.0, RAG-optimized, zero cost)

- [x] ~~[BGAI-001] Setup LLMWhisperer~~ → **#941** Closed (replaced)
- [x] ~~[BGAI-002] LlmWhispererPdfExtractor~~ → **#942** Closed (replaced)
- [x] ~~[BGAI-003] LLMWhisperer config~~ → **#943** Closed (replaced)
- [x] ~~[BGAI-004] LLMWhisperer tests~~ → **#944** Closed (replaced)
- [ ] **[BGAI-001-v2]** Install Unstructured → **#952** ✅
- [ ] **[BGAI-002-v2]** UnstructuredPdfExtractor → **#953** ✅
- [ ] **[BGAI-003-v2]** Unstructured tests → **#954** ✅
- [ ] [BGAI-005] SmolDocling FastAPI service → **#945** ✅
- [ ] [BGAI-006] Docker pdf-processor → **#946** ✅
- [ ] [BGAI-007] SmolDoclingPdfExtractor C# → **#947** ✅
- [ ] [BGAI-008] SmolDocling integration tests → **#948** ✅
- [ ] [BGAI-009] IPdfTextExtractor interface → **#940** ✅
- [ ] [BGAI-010] EnhancedOrchestrator → **#949** ✅
- [ ] [BGAI-011] 3-stage E2E tests → **#950** ✅
- [ ] [BGAI-012] PDF quality validation → **#951** ✅
- [ ] [BGAI-013] Bug fixes edge cases → **#955** ✅
- [ ] [BGAI-014] Code review checklist → **#956** ✅
- [ ] [BGAI-015] Documentation complete → **#957** ✅

**New 3-Stage Pipeline**:
1. **Stage 1**: Unstructured (Apache 2.0, RAG-optimized, 1.29s)
2. **Stage 2**: SmolDocling (VLM 256M, complex layouts)
3. **Stage 3**: Docnet (existing fallback)

**Reference**: `docs/architecture/pdf-extraction-opensource-alternatives.md`

#### Month 2: LLM Integration (12 issues) ✅ ALL CREATED

**Note**: Evaluate Ollama-only vs OpenRouter+Ollama to eliminate/minimize costs

- [ ] [BGAI-016] LLM strategy decision → **#958** ✅
- [ ] [BGAI-017] OllamaClient implementation → **#959** ✅
- [ ] [BGAI-018] OpenRouterClient (if Option B) → **#960** ✅
- [ ] [BGAI-019] Unit tests LLM clients → **#961** ✅
- [ ] [BGAI-020] AdaptiveLlmService routing → **#962** ✅
- [ ] [BGAI-021] Feature flag AI:Provider → **#963** ✅
- [ ] [BGAI-022] Integration tests routing → **#964** ✅
- [ ] [BGAI-023] Replace RagService LLM calls → **#965** ✅
- [ ] [BGAI-024] Backward compatibility tests → **#966** ✅
- [ ] [BGAI-025] Performance testing → **#967** ✅
- [ ] [BGAI-026] Cost tracking → **#968** ✅
- [ ] [BGAI-027] LLM documentation/ADR → **#969** ✅

#### Month 3: Multi-Model Validation (13 issues) ✅ ALL CREATED
- [ ] [BGAI-028] ConfidenceValidationService → **#970** ✅
- [ ] [BGAI-029] CitationValidationService → **#971** ✅
- [ ] [BGAI-030] HallucinationDetectionService → **#972** ✅
- [ ] [BGAI-031] Unit tests 3 validation layers → **#973** ✅
- [ ] [BGAI-032] MultiModelValidationService → **#974** ✅
- [ ] [BGAI-033] Consensus similarity calculation → **#975** ✅
- [ ] [BGAI-034] Unit tests consensus (18 tests) → **#976** ✅
- [ ] [BGAI-035] Wire all 5 validation layers → **#977** ✅
- [ ] [BGAI-036] End-to-end testing validation → **#978** ✅
- [ ] [BGAI-037] Performance optimization → **#979** ✅
- [ ] [BGAI-038] Bug fixes edge cases → **#980** ✅
- [ ] [BGAI-039] Accuracy baseline → **#981** ✅
- [ ] [BGAI-040] Update ADRs validation → **#982** ✅

#### Month 4: Quality Framework + Frontend (15 issues) ✅ ALL CREATED
- [ ] [BGAI-041] 5-metric framework → **#983** ✅
- [ ] [BGAI-042] Automated evaluation job → **#984** ✅
- [ ] [BGAI-043] Prometheus metrics → **#985** ✅
- [ ] [BGAI-044] Grafana dashboard → **#986** ✅
- [ ] [BGAI-045] Quality framework tests → **#987** ✅
- [ ] [BGAI-046] Install shadcn/ui → **#988** ✅
- [ ] [BGAI-047] Design tokens → Check #989-990
- [ ] [BGAI-048] Base components → **#989** ✅
- [ ] [BGAI-049] i18n setup → **#990** ✅
- [ ] [BGAI-050] BoardGameAI page → **#991** ✅
- [ ] [BGAI-051] Component testing → **#992** ✅
- [ ] [BGAI-052] Responsive testing → **#993** ✅
- [ ] [BGAI-053] Accessibility audit → Check #994-995
- [ ] [BGAI-054] Build optimization → **#994** ✅
- [ ] [BGAI-055] Month 4 E2E testing → **#995** ✅

#### Month 5: Golden Dataset + Q&A Interface (14 issues) ✅ ALL CREATED
- [ ] [BGAI-056] Terraforming Mars (20 Q&A) → **#996** ✅
- [ ] [BGAI-057] Wingspan (15 Q&A) → **#997** ✅
- [ ] [BGAI-058] Azul (15 Q&A) → **#998** ✅
- [ ] [BGAI-059] Quality test implementation → **#999** ✅
- [ ] [BGAI-060] Accuracy baseline test → **#1000** ✅
- [ ] [BGAI-061] QuestionInputForm → **#1001** ✅
- [ ] [BGAI-062] ResponseCard component → **#1002** ✅
- [ ] [BGAI-063] GameSelector dropdown → **#1003** ✅
- [ ] [BGAI-064] Loading/error states → **#1004** ✅
- [ ] [BGAI-065] Jest tests (20 tests) → **#1005** ✅
- [ ] [BGAI-066] Backend API integration → **#1006** ✅
- [ ] [BGAI-067] Streaming SSE support → **#1007** ✅
- [ ] [BGAI-068] Error handling/retry → **#1008** ✅
- [ ] [BGAI-069] Month 5 E2E testing → **#1009** ✅

#### Month 6: Completion + Polish (16 issues) ✅ ALL CREATED
- [ ] [BGAI-070] Scythe, Catan, Pandemic → **#1010** ✅
- [ ] [BGAI-071] 7 Wonders, Agricola, Splendor → **#1011** ✅
- [ ] [BGAI-072] Adversarial dataset (50) → **#1012** ✅
- [ ] [BGAI-073] PDF viewer (react-pdf) → **#1013** ✅
- [ ] [BGAI-074] Citation navigation → **#1014** ✅
- [ ] [BGAI-075] Mobile PDF UX → Check #1015-1016
- [ ] [BGAI-076] PDF viewer tests → **#1015** ✅
- [ ] [BGAI-077] Italian UI strings (200+) → **#1016** ✅
- [ ] [BGAI-078] Game catalog page → **#1017** ✅
- [ ] [BGAI-079] Mobile responsive tests → Check #1018-1019
- [ ] [BGAI-080] E2E (question → citation) → **#1018** ✅
- [ ] [BGAI-081] Accuracy validation (80%) → **#1019** ✅
- [ ] [BGAI-082] Performance testing P95 → **#1020** ✅
- [ ] [BGAI-083] Final bug fixes → **#1021** ✅
- [ ] [BGAI-084] Documentation updates → **#1022** ✅
- [ ] [BGAI-085] Phase 1A checklist → **#1023** ✅

**Total**: 80 issues ACTIVE for Phase 1A (#940, #945-#1023)
**Closed**: 4 issues (#941-#944 LLMWhisperer, replaced with Unstructured)

### 🔗 Related Work (Non-BGAI Issues)

**DDD Prerequisites**:
- **#937**: [DDD] Complete Remaining 5% - Parent epic including PDF services
  - ⚠️ Includes #940 (BGAI-009) as part of PDF services migration
  - 📌 Coordinate: Complete BGAI PDF work first (Month 1), then finish remaining DDD

- **#925**: [DDD] AI Agents Architecture Decision
  - 📌 General architectural decision (not BGAI-specific yet)
  - 📌 Board Game AI will adopt same architecture for agents in Phase 2+

**Frontend General** (NOT Board Game AI specific):
- #926-#935: Frontend Epic phases (general MeepleAI improvements)
- #927: Install shadcn/ui (generale) - BGAI has specific #988
- #928: Design tokens (generale) - BGAI has specific implementation
- Note: BGAI uses separate frontend issues for Board Game AI-specific UI

**Admin Console** (NOT Board Game AI):
- #874-#922: FASE 1-4 Admin Console features (separate project)
- Not blocking BGAI, can proceed in parallel

### ✅ Issue Creation Complete (2025-01-15)

**All 80 issues created successfully**:
- Month 1: #940, #945-#957 (12 issues)
- Month 2: #958-#969 (12 issues)
- Month 3: #970-#982 (13 issues)
- Month 4: #983-#995 (13 issues)
- Month 5: #996-#1009 (14 issues)
- Month 6: #1010-#1023 (14 issues)

**View All**: https://github.com/DegrassiAaron/meepleai-monorepo/issues?q=is:issue+label:board-game-ai

**Milestones**:
- #13: Month 1 (0/12 complete)
- #14: Month 2 (0/12 complete)
- #15: Month 3 (0/13 complete)
- #16: Month 4 (0/13 complete)
- #17: Month 5 (0/14 complete)
- #18: Month 6 (0/14 complete)

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

**Week 1: Unstructured Integration** (Days 1-3) - ⚡ FASTER (was 5 days)
- Day 1-2: Install Unstructured + FastAPI service (#952 - [BGAI-001-v2])
- Day 3: Implement UnstructuredPdfExtractor C# client (#953 - [BGAI-002-v2])
- **Bonus Days 4-5**: Unit tests (#954) + Start SmolDocling early
- **Deliverable**: Unstructured Stage 1 working (Apache 2.0, zero cost, 1.29s)
- **Architecture**: Unstructured → SmolDocling → Docnet (100% open source)

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

**Week 5: LLM Strategy & Ollama** (Days 21-25)
- Day 21: Evaluate LLM strategy (#958 - BGAI-016) - Ollama-only vs hybrid
- Day 22-23: Implement OllamaClient (#959 - BGAI-017)
- Day 24: OpenRouterClient if needed (#960 - BGAI-018)
- Day 25: Unit tests (#961 - BGAI-019)
- **Deliverable**: Ollama working, strategy decided

**Week 6: Adaptive Routing** (Days 26-30)
- Day 26-27: AdaptiveLlmService (#962 - BGAI-020)
- Day 28: Feature flag AI:Provider (#963 - BGAI-021)
- Day 29-30: Integration tests (#964 - BGAI-022)
- **Deliverable**: Adaptive routing operational

**Week 7: RAG Integration** (Days 31-35)
- Day 31-32: Replace RagService LLM calls (#965 - BGAI-023)
- Day 33-34: Backward compatibility (#966 - BGAI-024)
- Day 35: E2E integration tests
- **Deliverable**: RAG uses adaptive LLM

**Week 8: Testing & Docs** (Days 36-40)
- Day 36-37: Performance testing (#967 - BGAI-025)
- Day 38: Cost tracking (#968 - BGAI-026)
- Day 39-40: Documentation/ADR (#969 - BGAI-027)
- **Deliverable**: Month 2 COMPLETE, baseline established

**Month 2 Output**: ✅ LLM integration complete, OpenRouter + Ollama proven

---

**Month 3 (4 weeks): Multi-Model Validation**

**Week 9: Validation Part 1** (Days 41-45)
- Day 41: ConfidenceValidationService (#970)
- Day 42-43: CitationValidationService (#971)
- Day 44-45: HallucinationDetectionService (#972)
- **Quality Gate**: Unit tests (#973)

**Week 10: Multi-Model Consensus** (Days 46-50)
- Day 46-47: MultiModelValidationService (#974)
- Day 48: Consensus similarity (#975)
- Day 49-50: Unit tests 18 (#976)

**Week 11: Pipeline Integration** (Days 51-55)
- Day 51-52: Wire 5 layers (#977)
- Day 53-54: E2E testing (#978)
- Day 55: Performance (#979)

**Week 12: Buffer** (Days 56-60)
- Day 56-57: Bug fixes (#980)
- Day 58: Accuracy baseline (#981)
- Day 59-60: ADR updates (#982)

**Month 3 Output**: ✅ Multi-model validation complete, ready for quality framework

---

**Month 4 (4 weeks): Quality Framework + Frontend Foundation**

**Week 13: Quality Backend** (Days 61-65)
- Day 61-62: 5-metric framework (#983)
- Day 63-64: Automated evaluation (#984)
- Day 65: Prometheus metrics (#985)

**Week 14: Quality Complete** (Days 66-70)
- Day 66-67: Grafana dashboard (#986)
- Day 68-70: Integration tests (#987)

**Week 15: Frontend Foundation** (Days 71-75)
- Day 71: Install shadcn/ui (#988)
- Day 72-73: Design tokens + Base components (#989)
- Day 74-75: More components work

**Week 16: Frontend Complete** (Days 76-80)
- Day 76-77: i18n setup (#990)
- Day 78-79: BoardGameAI page (#991)
- Day 80: Testing (#992, #993, #994, #995)

**Month 4 Output**: ✅ Quality framework + Frontend foundation both complete

---

**Month 5 (4 weeks): Golden Dataset + Q&A Interface**

**Week 17: Dataset Part 1** (Days 81-85)
- Day 81-82: Terraforming Mars 20 Q&A (#996)
- Day 83-84: Wingspan 15 Q&A (#997)
- Day 85: Azul 15 Q&A (#998)

**Week 18: Quality Testing** (Days 86-90)
- Day 86-87: Quality test implementation (#999)
- Day 88-89: Accuracy test (#1000)
- Day 90: Baseline analysis

**Week 19: Q&A Components** (Days 91-95)
- Day 91-92: QuestionInputForm (#1001)
- Day 93-94: ResponseCard (#1002)
- Day 95: GameSelector (#1003)

**Week 20: Q&A Integration** (Days 96-100)
- Day 96: Loading/error states (#1004)
- Day 97-98: Jest tests 20 (#1005)
- Day 99: API integration (#1006)
- Day 100: Streaming SSE (#1007), Error handling (#1008)
- **Gate**: E2E testing (#1009)

**Month 5 Output**: ✅ 50 Q&A dataset + Q&A UI functional

---

**Month 6 (4 weeks): Dataset Completion + PDF Viewer + Italian UI**

**Week 21: Dataset Expansion** (Days 101-105)
- Day 101-102: Scythe, Catan, Pandemic 30 Q&A (#1010)
- Day 103-104: 7 Wonders, Agricola, Splendor 30 Q&A (#1011)
- Day 105: Adversarial dataset 50 (#1012)

**Week 22: PDF Viewer** (Days 106-110)
- Day 106-107: PDF viewer integration (#1013)
- Day 108: Citation navigation (#1014)
- Day 109-110: Mobile UX + Tests (#1015)

**Week 23: Italian Localization** (Days 111-115)
- Day 111-112: Italian strings 200+ (#1016)
- Day 113-114: Game catalog page (#1017)
- Day 115: Mobile responsive (all issue numbers match)

**Week 24: Final Testing** (Days 116-120)
- Day 116-117: E2E testing (#1018)
- Day 118: Accuracy 80% (#1019)
- Day 119: Performance P95 (#1020)
- Day 120: Bug fixes (#1021), Docs (#1022), Checklist (#1023)
- **Deliverable**: Phase 1A COMPLETE

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
