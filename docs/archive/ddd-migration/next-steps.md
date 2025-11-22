# Board Game AI - Immediate Next Steps & Action Plan

**Status**: Ready for Execution
**Date**: 2025-01-15
**Timeline**: Start NOW → MVP by June 2025

---

## 📋 Summary of Decisions Made

### ✅ Strategic Decisions
1. **Consolidation**: Board Game AI features integrated into existing MeepleAI (ASP.NET Core 9.0)
2. **Priority**: Board Game AI is #1 priority, FASE 1-4 deferred to Aug 2026+
3. **Technology Stack**:
   - Backend: ASP.NET Core 9.0 (existing, enhanced)
   - PDF: Unstructured + SmolDocling + Docnet.Core (3-stage)
   - LLM: OpenRouter (GPT-4 + Claude) + Ollama fallback (free)
   - Vector DB: Qdrant (existing)
   - Frontend: Next.js 16 + React 19 (existing)

### 📚 Documentation Created (13 documents, ~350 pages)
1. ✅ Strategic Roadmap (board-game-ai-strategic-roadmap.md)
2. ✅ Architecture Overview (board-game-ai-architecture-overview.md)
3. ✅ API Specification (board-game-ai-api-specification.md)
4. ✅ Testing Strategy (board-game-ai-testing-strategy.md)
5. ✅ Deployment Guide (board-game-ai-deployment-guide.md)
6. ✅ Business Plan (board-game-ai-business-plan.md)
7. ✅ Executive Summary (board-game-ai-executive-summary.md)
8. ✅ Documentation Index (board-game-ai-documentation-index.md)
9. ✅ ADR-001: Hybrid RAG Architecture
10. ✅ ADR-002: Multilingual Embeddings
11. ✅ ADR-003: PDF Processing Pipeline
12. ✅ Consolidation Strategy (board-game-ai-consolidation-strategy.md)
13. ✅ Implementation Notes (board-game-ai-IMPLEMENTATION-NOTES.md)

### 🎯 Issue Management
- **Deferred**: 33 FASE issues (#890-922) to Aug 2026+
- **Script Created**: `tools/defer-fase-issues.sh` (run to add comments)
- **Prioritization Doc**: docs/org/project-prioritization-2025.md

---

## 🚀 Immediate Actions (This Week)

### Today (Jan 15, 2025)

**1. Execute Issue Deferral** (15 minutes):
```bash
# Run script to add deferral comments to FASE issues
bash tools/defer-fase-issues.sh

# Verify comments added
gh issue view 890  # Check for deferral comment
```

**2. Team Communication** (30 minutes):
- [ ] Post Slack/Discord announcement (see project-prioritization-2025.md template)
- [ ] Schedule all-hands meeting (30 min, tomorrow or Friday)
- [ ] Share documentation index: docs/board-game-ai-documentation-index.md

---

### This Week (Jan 15-19, 2025)

**3. Technical Setup** (Backend Engineer, 1 day):
- [ ] LLMWhisperer trial account: https://llmwhisperer.com/signup
- [ ] Test API with 3 sample Italian rulebooks (Terraforming Mars, Wingspan, Azul PDFs)
- [ ] Validate quality scores (target: ≥0.80 on modern rulebooks)
- [ ] Document API limits (100 pages/day free tier sufficient for MVP?)

**4. SmolDocling Prototype** (Backend Engineer, 2 days):
- [ ] Create `apps/pdf-processor/` Python FastAPI service
- [ ] Install docling package: `pip install docling`
- [ ] Implement `/api/v1/convert` endpoint
- [ ] Test on scanned PDF (fallback scenario)
- [ ] Docker Compose integration (test locally)

**5. OpenRouter Setup** (Backend Engineer, 1 day):
- [ ] OpenRouter account: https://openrouter.ai/signup
- [ ] API key setup (add to .env.dev: OPENROUTER_API_KEY)
- [ ] Test models: `openai/gpt-4-turbo`, `anthropic/claude-3.5-sonnet`
- [ ] Verify cost tracking (OpenRouter dashboard shows usage)
- [ ] Document model IDs and pricing

**6. Ollama Fallback** (DevOps, 0.5 day):
- [ ] Verify meepleai-ollama running: `docker compose ps meepleai-ollama`
- [ ] Pull models: `docker exec ollama ollama pull mistral:7b-instruct-v0.3-q4_K_M`
- [ ] Pull models: `docker exec ollama ollama pull llama3.1:8b-instruct-q4_K_M`
- [ ] Pull embedding: `docker exec ollama ollama pull nomic-embed-text`
- [ ] Test generation: `curl http://localhost:11434/api/generate`

**7. Community Outreach** (Product Lead, 2 days):
- [ ] Draft La Tana dei Goblin partnership proposal
- [ ] Contact forum admins (private message or email)
- [ ] Prepare beta program description (Google Form template)
- [ ] Prepare 10-game shortlist for MVP (Italian popular games)

---

### Next Week (Jan 20-26, 2025)

**8. Sprint 1 Planning** (Full Team, 4 hours):
- [ ] Review Board Game AI documentation (all team members read roadmap + architecture)
- [ ] Sprint 1 scope definition (LLMWhisperer integration + SmolDocling service)
- [ ] GitHub Issues creation (break down into tasks)
- [ ] Assign tasks to engineers
- [ ] Setup Sprint board (GitHub Projects)

**9. Development Environment** (All Engineers, 1 day):
- [ ] Clone/update repo: `git pull origin main`
- [ ] Review CLAUDE.md + board-game-ai-IMPLEMENTATION-NOTES.md
- [ ] Setup local env: `cd infra && docker compose up -d`
- [ ] Verify services healthy: `curl http://localhost:5080/health`
- [ ] Run existing tests: `cd apps/api && dotnet test`

**10. Kickoff Sprint 1** (Jan 27, 2025):
- [ ] Daily standups (15 min, 9:00 AM)
- [ ] Pair programming sessions (PDF processing complexity)
- [ ] Progress tracking (TodoWrite, GitHub Projects)
- [ ] Target: LLMWhisperer C# client working by Feb 4

---

## 📅 Phase 1 Timeline (Jan 15 - Jun 27, 2025)

### Sprint Schedule (2-week iterations)

| Sprint | Dates | Focus | Deliverables |
|--------|-------|-------|--------------|
| **Sprint 1** | Jan 27 - Feb 7 | PDF Pipeline Setup | LlmWhispererPdfExtractor (C#), SmolDocling service (Python) |
| **Sprint 2** | Feb 10 - Feb 21 | PDF Integration | EnhancedPdfProcessingOrchestrator, 3-stage fallback, tests |
| **Sprint 3** | Feb 24 - Mar 7 | OpenRouter Integration | OpenRouterClient, AdaptiveLlmService, Ollama fallback |
| **Sprint 4** | Mar 10 - Mar 21 | Multi-Model Validation | MultiModelValidationService, consensus logic, tests |
| **Sprint 5** | Mar 24 - Apr 4 | Quality Framework | 5-metric testing, golden dataset (100 Q&A), Italian i18n |
| **Sprint 6** | Apr 7 - Apr 18 | Frontend Italian UI | Q&A interface, PDF viewer, mobile responsive |
| **Sprint 7** | Apr 21 - May 2 | Beta Preparation | La Tana partnership, beta signup, onboarding docs |
| **Sprint 8** | May 5 - May 16 | Beta Testing | 100 user recruitment, feedback collection, iterations |
| **Sprint 9** | May 19 - May 30 | Refinement | Address beta feedback, accuracy improvements |
| **Sprint 10** | Jun 2 - Jun 13 | Phase 1 Polish | Performance optimization, bug fixes, documentation |
| **Sprint 11** | Jun 16 - Jun 27 | Phase 1 Wrap-Up | Final testing, Phase 2 planning, retrospective |

**Phase 1 Completion Target**: **June 27, 2025** (23 weeks from start)

---

## 🎯 Success Criteria (Phase 1 MVP)

### Must-Have (Go/No-Go Gate - Jun 27, 2025)

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| **Beta Users** | 100 recruited | 0 | 🔴 Not started |
| **Accuracy** | ≥80% on 100 Q&A golden dataset | Baseline TBD | 🔴 Not started |
| **Hallucination** | ≤10% on 50 adversarial queries | Baseline TBD | 🔴 Not started |
| **User Satisfaction** | ≥4.0/5.0 post-beta survey | N/A | 🔴 Not started |
| **Uptime** | ≥99% during 2-week beta period | N/A | 🔴 Not started |
| **Test Coverage** | ≥90% (existing standard maintained) | 90%+ (existing) | 🟢 Baseline OK |

### Nice-to-Have (Optimize if Time Permits)

- P95 Latency <5s (target for Phase 1, optimize to <3s in Phase 2)
- 20 games indexed (stretch goal vs 10 baseline)
- Mobile PWA (full native wrappers defer to Phase 2)
- Cache hit rate 30%+ (semantic caching, target 40-60% Phase 2)

---

## 🛠️ Technical Prerequisites (Setup Checklist)

### Accounts & API Keys (Setup This Week)

- [ ] **LLMWhisperer**: Signup at https://llmwhisperer.com, get API key
- [ ] **OpenRouter**: Signup at https://openrouter.ai, get API key, add credits ($50 for testing)
- [ ] **meepleai-ollama**: Verify running in Docker Compose (`docker compose ps meepleai-ollama`)
- [ ] **La Tana dei Goblin**: Create account, contact admins for partnership

### Environment Variables (Update .env.dev)

```bash
# Add to infra/env/.env.dev

# PDF Processing
# Note: Using Unstructured (self-hosted), no API key needed
SMOLDOCLING_SERVICE_URL=http://pdf-processor:8002

# LLM APIs
OPENROUTER_API_KEY=sk-or-v1-...  # From https://openrouter.ai/keys

# Feature Flags (ConfigurationService)
FEATURES_ENHANCED_PDF_PROCESSING=true
FEATURES_MULTI_MODEL_VALIDATION=true
FEATURES_ITALIAN_LOCALIZATION=true
FEATURES_BOARD_GAME_AI=true

# AI Provider Selection
AI_PROVIDER=OpenRouter  # "OpenRouter" or "Ollama"
AI_OPENROUTER_ENABLED=true
AI_OLLAMA_ENABLED=true  # Fallback always enabled
```

### Docker Compose (Extend Existing)

**Add to `infra/docker-compose.yml`**:
```yaml
services:
  pdf-processor:
    build: ../apps/pdf-processor
    container_name: meepleai-pdf-processor
    ports:
      - "8002:8002"
    environment:
      - LOG_LEVEL=INFO
      - PYTHONUNBUFFERED=1
    # GPU support (optional, fallback to CPU if unavailable)
    # deploy:
    #   resources:
    #     reservations:
    #       devices:
    #         - driver: nvidia
    #           count: 1
    #           capabilities: [gpu]
    networks:
      - meepleai-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8002/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

---

## 📊 Resource Allocation (Phase 1)

### Team Assignments (Jan 15 - Jun 27, 2025)

**Backend Engineer 1** (100% allocation):
- Sprint 1-2: LLMWhisperer C# integration
- Sprint 3-4: OpenRouter client implementation
- Sprint 5-6: Quality framework (5-metric testing)
- Sprint 7-10: Performance optimization, beta support

**Backend Engineer 2** (100% allocation):
- Sprint 1-2: SmolDocling Python service
- Sprint 3-4: Multi-model validation service
- Sprint 5-6: Italian localization, terminology handling
- Sprint 7-10: Backend refinement, bug fixes

**Frontend Engineer** (50% → 100% ramp):
- Sprint 1-2 (50%): Foundation (#926 - shadcn/ui setup)
- Sprint 3-6 (100%): Board Game AI Italian UI (Q&A interface, PDF viewer)
- Sprint 7-10 (100%): Mobile responsive, beta UX iterations

**DevOps** (25% allocation, as-needed):
- Sprint 1: SmolDocling Docker deployment
- Sprint 2: GPU configuration (if available)
- Sprint 5: Monitoring setup (Board Game AI metrics)
- Sprint 7: Beta environment deployment

**QA** (25% → 50% ramp):
- Sprint 5-6 (25%): Golden dataset annotation (100 Italian Q&A)
- Sprint 7-8 (50%): Beta testing coordination
- Sprint 9-10 (50%): Quality validation, regression testing

**Total**: 3.5 FTE average (2 backend + 0.75 frontend + 0.375 DevOps + 0.375 QA)

---

## 🎬 Sprint 1 Kickoff Checklist (Jan 27, 2025)

### Pre-Sprint (This Week)

**Monday Jan 13** (TODAY):
- [x] Create prioritization document ✅
- [x] Create consolidation strategy ✅
- [x] Update Board Game AI docs ✅
- [ ] Team announcement (Slack/Discord)

**Tuesday Jan 14**:
- [ ] LLMWhisperer account setup + API testing
- [ ] OpenRouter account + credit top-up ($50)
- [ ] Ollama model pull (mistral, llama3, nomic-embed-text)

**Wednesday Jan 15**:
- [ ] SmolDocling local prototype (Python, test 1 PDF)
- [ ] Docker Compose update (add pdf-processor service)
- [ ] Test full stack: `docker compose up -d`

**Thursday Jan 16**:
- [ ] Sprint 1 planning meeting (4 hours)
- [ ] GitHub Issues creation (Sprint 1 tasks)
- [ ] Sprint board setup (GitHub Projects)

**Friday Jan 17**:
- [ ] Development environment verification (all engineers)
- [ ] Pair programming setup (PDF processing team)
- [ ] Sprint 1 goals review

---

### Sprint 1 Goals (Jan 27 - Feb 7, 2025)

**Primary Deliverables**:
1. ✅ LlmWhispererPdfExtractor working (C# HttpClient integration)
2. ✅ SmolDocling Python service deployed (Docker container)
3. ✅ EnhancedPdfProcessingOrchestrator (3-stage fallback logic)
4. ✅ Tests: 15 unit + 8 integration (90%+ coverage)
5. ✅ Documentation: Code comments, README for pdf-processor

**Success Criteria**:
- [ ] Process 5 Italian rulebooks successfully (end-to-end)
- [ ] Quality scores tracked per stage (Prometheus metrics)
- [ ] All tests passing (CI/CD green)
- [ ] Demo-able to stakeholders (show 3-stage fallback working)

**Sprint 1 Retrospective** (Feb 7):
- What worked well?
- What blockers encountered?
- Velocity assessment (on track for 6-month timeline?)
- Adjust Sprint 2 scope if needed

---

## 📞 Stakeholder Communication

### Internal Team (Already Done via Docs)

**Documentation Shared**:
- [x] Strategic Roadmap
- [x] Consolidation Strategy
- [x] Implementation Notes
- [x] Prioritization Decision

**Next**: Team meeting to Q&A (schedule within 48 hours)

---

### External Stakeholders (If FASE Had Commitments)

**Template Email** (customize per stakeholder):
```
Subject: Important: FASE Admin Console Timeline Change

Dear [Name],

Strategic Priority Shift:
We're deferring the FASE Admin Console project (originally Nov-Dec 2025) to August 2026 or later to pursue a high-value Italian board game AI market opportunity.

New FASE Timeline: Aug 2026+
Board Game AI Timeline: Jan-Jun 2025 (MVP), Jul-Dec 2025 (Production)

Impact on You:
[Customize based on stakeholder needs]

Alternative Solutions:
If FASE features are critical, let's discuss:
1. Minimal scope subset (2-3 features only, Q1 2025)
2. Workarounds using existing tools
3. External contractor (additional cost)

Let's schedule a call: [Calendar link]

Regards,
[Product Lead]
```

**Send within 1 week** if FASE had external stakeholders.

---

## 🔗 Key Documents Quick Reference

### Start Here (New Team Members)
1. **[Implementation Notes](./board-game-ai-IMPLEMENTATION-NOTES.md)** - Technology corrections (MUST READ)
2. **[Consolidation Strategy](./architecture/board-game-ai-consolidation-strategy.md)** - Decision rationale
3. **[Strategic Roadmap](./roadmap/board-game-ai-strategic-roadmap.md)** - 4-phase plan, timeline

### Implementation (Engineers)
4. **[Architecture Overview](./architecture/board-game-ai-architecture-overview.md)** - Components, data flow
5. **[ADR-001](./architecture/adr-001-hybrid-rag-architecture.md)** - Multi-model validation (C# code)
6. **[ADR-003](./architecture/adr-003-pdf-processing-pipeline.md)** - PDF pipeline (LLMWhisperer + SmolDocling)
7. **[API Specification](./api/board-game-ai-api-specification.md)** - Endpoints (extend existing /api/v1/*)

### Testing (QA Team)
8. **[Testing Strategy](./testing/board-game-ai-testing-strategy.md)** - Test pyramid, 5-metric framework

### Operations (DevOps)
9. **[Deployment Guide](./deployment/board-game-ai-deployment-guide.md)** - Docker Compose extensions

### Business (Product/Leadership)
10. **[Business Plan](./business/board-game-ai-business-plan.md)** - Revenue projections, funding
11. **[Prioritization Decision](./org/project-prioritization-2025.md)** - Why Board Game AI priority

---

## 🎓 Learning Resources (For Team Ramp-Up)

### Board Game AI Domain (1-2 hours reading)
- **Source Research**: `docs/kb/Sistemi AI per arbitrare giochi da tavolo stato dell'arte 2025.md` (comprehensive landscape analysis)
- **Key Takeaways**: Competitor accuracy 45-75%, hallucination problem (ChatGPT invented rules), Italian market unserved

### RAG Architecture (2-3 hours)
- **Sam Miller (2024)**: "RAG Board Game Guru" (Google Cloud Vertex AI + Gemini)
- **Andor FAQ LLM**: https://andor-faq-llm.streamlit.app (working demo, study UX)
- **LangChain Docs**: https://python.langchain.com/docs/tutorials/rag (concepts, patterns)

### PDF Processing (1 hour)
- **LLMWhisperer Docs**: https://llmwhisperer.com/docs
- **SmolDocling GitHub**: https://github.com/DS4SD/docling (examples, API reference)

### OpenRouter (30 minutes)
- **OpenRouter Docs**: https://openrouter.ai/docs (API, models, pricing)
- **Model Comparison**: https://openrouter.ai/models (GPT-4 Turbo vs Claude 3.5)

---

## 🚨 Risks & Mitigation (Watch These)

### Risk 1: LLMWhisperer Free Tier Insufficient

**Risk**: 100 pages/day limit = 4 rulebooks/day (10 games MVP = 3 days minimum)
**Probability**: Medium (if need to re-process due to errors)
**Impact**: Low (can upgrade to $29/mo paid tier if needed)
**Mitigation**:
- Start with highest-quality PDFs (modern, native text layer)
- Test quality scores early (Sprint 1)
- Budget for paid tier in Phase 2 ($29/mo = negligible)

---

### Risk 2: SmolDocling GPU Requirement

**Risk**: SmolDocling slow on CPU (60s/page vs 10s/page with GPU)
**Probability**: High (most dev machines lack NVIDIA GPU)
**Impact**: Medium (processing time 6x slower, but one-time per rulebook)
**Mitigation**:
- Accept slow CPU processing for MVP (10 games × 24 pages × 60s = 4 hours one-time)
- Phase 2: Add GPU Droplet ($50/mo DigitalOcean)

---

### Risk 3: OpenRouter Cost Escalation

**Risk**: Multi-model validation = 2x API costs per query
**Probability**: High (will happen at scale)
**Impact**: High (€1,500/mo at 1K MAU, €3K/mo at 5K MAU)
**Mitigation**:
- Semantic caching (40-60% hit rate → 40-60% cost reduction)
- Ollama fallback for free users (premium users get OpenRouter quality)
- Feature flag: Disable consensus for low-value queries
- Publisher B2B revenue (€3K-7.5K/mo) offsets API costs

---

### Risk 4: Beta User Recruitment Slow

**Risk**: Fail to recruit 100 beta users in 2 weeks (La Tana partnership weak)
**Probability**: Low-Medium (community active, but cold outreach)
**Impact**: Medium (delays Phase 1 validation)
**Mitigation**:
- Multi-community approach (La Tana + Bottega Ludica + GiocaOggi)
- Incentive: Free lifetime premium for first 100 beta users
- Influencer outreach (Italian board game YouTubers, podcasters)

---

## ✅ Definition of Done (Phase 1 MVP)

### Technical DoD

- [ ] All Sprint 1-10 deliverables complete
- [ ] 3-stage PDF pipeline working (LLMWhisperer → SmolDocling → Docnet)
- [ ] Multi-model validation (OpenRouter: GPT-4 + Claude consensus at 0.90)
- [ ] Ollama fallback configured and tested (free operation mode)
- [ ] 5-metric framework (Accuracy ≥80%, Hallucination ≤10%, Confidence ≥0.70, Citation ≥80%, Latency ≤5s)
- [ ] Italian localization (UI strings, terminology glossary 500+ terms)
- [ ] Tests passing (90%+ coverage, unit + integration + quality tests)
- [ ] CI/CD green (all checks passing)

### Product DoD

- [ ] 10 Italian games indexed (popular titles from La Tana dei Goblin rankings)
- [ ] 100 beta users recruited and activated (≥1 query per user)
- [ ] Golden dataset created (100 Q&A pairs manually annotated)
- [ ] Beta feedback collected (survey: satisfaction, accuracy perception, feature requests)
- [ ] User satisfaction ≥4.0/5.0 validated
- [ ] Phase 2 plan approved (publisher outreach, production deployment)

### Business DoD

- [ ] La Tana dei Goblin partnership confirmed (beta announcement published)
- [ ] Giochi Uniti initial contact made (Phase 2 partnership prep)
- [ ] Funding strategy confirmed (bootstrap sufficient for Phase 1? Grant applications submitted?)
- [ ] Phase 1 retrospective complete (lessons learned, pivot decisions documented)

---

## 🎉 Celebration Milestones

**Sprint 2 Complete** (Feb 21):
- 🍕 Team lunch (celebrate PDF pipeline working)

**Sprint 6 Complete** (Apr 18):
- 🍾 Demo day (show Italian UI to leadership/advisors)

**Beta Launch** (May 5):
- 🚀 Launch party (100 beta users recruited)

**Phase 1 Complete** (Jun 27):
- 🏆 Offsite celebration (team dinner, Phase 1 → Phase 2 transition)
- 📊 Metrics review (validate 80% accuracy, publish results)
- 🎓 Retrospective (what worked, what to improve)

---

## 📧 Contact & Questions

**Product Questions**: Product Lead, #product-strategy channel

**Technical Questions**: CTO, Engineering Lead, #engineering channel

**Resource Conflicts**: CEO, Project Manager, #leadership channel

**FASE Deferral Concerns**: Product Lead (respond within 24 hours)

---

**Document Metadata**:
- **Version**: 1.0
- **Purpose**: Action Plan for Board Game AI Phase 1 Immediate Start
- **Audience**: Full Team (Engineering, Product, Leadership)
- **Status**: APPROVED - Execute Immediately
- **Next Update**: Weekly (every Monday, sprint progress)
