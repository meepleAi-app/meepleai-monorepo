# Board Game AI - Quick Start Guide

**Read This First**: 5-minute overview per iniziare subito

---

## 🎯 Cosa Stiamo Costruendo?

**Board Game AI Features** integrate nel sistema MeepleAI esistente (ASP.NET Core 9.0):

1. **Italian-First Q&A System**: Assistente AI per regolamenti giochi da tavolo in italiano
2. **95%+ Accuracy Target**: Multi-model validation (GPT-4 + Claude consensus) vs 45-75% competitors
3. **No Hallucinations**: Explicit uncertainty ("Non lo so") vs inventare regole
4. **Publisher Partnerships**: Giochi Uniti, Asmodee Italia (official content, white-label)
5. **6-Month MVP**: 100 beta users da La Tana dei Goblin, accuracy ≥80% validation

---

## ⚡ Quick Reference

### Technology Stack (Consolidated)

| Componente | Scelta | Perché |
|------------|--------|--------|
| **Backend** | ASP.NET Core 9.0 | Existing system (riuso infra, DDD, 90%+ coverage) |
| **PDF** | LLMWhisperer + SmolDocling + Docnet | 3-stage per high accuracy (95%+) |
| **LLM** | OpenRouter + Ollama | Feature-flag: API (accuracy) vs free (cost) |
| **Vector DB** | Qdrant | Existing (hybrid search ready) |
| **Frontend** | Next.js 16 + React 19 | Latest stable |

### Timeline (Phase 1)

- **Start**: Jan 27, 2025 (Sprint 1 kickoff)
- **MVP Complete**: Jun 27, 2025 (23 weeks)
- **Sprints**: 10 × 2-week iterations

### Team (4 FTE)

- 2 Backend Engineers (PDF + LLM)
- 1 Frontend Engineer (Italian UI + mobile)
- 0.5 DevOps (SmolDocling deploy, monitoring)
- 0.5 QA (Golden dataset, beta testing)

---

## 📚 Essential Documentation (Read in Order)

### For Everyone (30 min total)
1. **[NEXT STEPS](./board-game-ai-NEXT-STEPS.md)** (10 min) - Action plan, Sprint 1 kickoff
2. **[Prioritization Decision](./org/project-prioritization-2025.md)** (10 min) - Why Board Game AI priority, FASE deferred
3. **[Implementation Notes](./board-game-ai-IMPLEMENTATION-NOTES.md)** (10 min) - Technology corrections (CRITICAL)

### For Engineers (2 hours)
4. **[Consolidation Strategy](./architecture/board-game-ai-consolidation-strategy.md)** (30 min) - Integrate into existing vs new system
5. **[Architecture Overview](./architecture/board-game-ai-architecture-overview.md)** (45 min) - Components, data flow, code examples
6. **[Execution Calendar](./org/board-game-ai-execution-calendar.md)** (30 min) - Sprint-by-sprint plan
7. **[Sprint Overview](./org/board-game-ai-sprint-overview.md)** (15 min) - Visual timeline, issue breakdown

### For Business/Leadership (1 hour)
8. **[Strategic Roadmap](./roadmap/board-game-ai-strategic-roadmap.md)** (30 min) - 4-phase plan, market analysis
9. **[Business Plan](./business/board-game-ai-business-plan.md)** (30 min) - Revenue projections, funding

---

## 🚀 Immediate Actions (This Week)

### Today (Jan 15)
- [x] ✅ FASE issues deferred (#890-922, 33 issues)
- [x] ✅ Documentation created (16 documents, ~350 pages)
- [ ] Read QUICK-START.md (this doc, 5 min)
- [ ] Slack announcement to team (template in prioritization doc)

### Tomorrow (Jan 16)
- [ ] Setup accounts: LLMWhisperer (https://llmwhisperer.com), OpenRouter (https://openrouter.ai)
- [ ] Add credits: OpenRouter $50 (testing budget)
- [ ] Pull Ollama models: `docker exec ollama ollama pull mistral:7b-instruct-v0.3-q4_K_M`

### This Week (Jan 17-19)
- [ ] Prototype SmolDocling (Python service, 1-2 days)
- [ ] Test LLMWhisperer (3 Italian PDFs: Terraforming Mars, Wingspan, Azul)
- [ ] Test OpenRouter (GPT-4 Turbo + Claude 3.5 Sonnet API calls)

### Next Week (Jan 20-26)
- [ ] **Thu Jan 23**: Sprint 1 Planning Meeting (4 hours, full team)
  - Review documentation
  - Create 60+ GitHub Issues (backend + frontend)
  - Assign Sprint 1 tasks
  - Setup GitHub Project board

### Sprint 1 Kickoff (Jan 27)
- [ ] Daily standups start (9:00 AM)
- [ ] Pair programming (PDF processing complexity)
- [ ] Target: LLMWhisperer + SmolDocling working by Feb 7

---

## 💻 Development Setup (20 Minutes)

### 1. Clone/Update Repo
```bash
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
# OR if already cloned:
git pull origin main
```

### 2. Read Core Docs (MUST READ before coding)
```bash
# Open in your editor
code docs/board-game-ai-IMPLEMENTATION-NOTES.md  # Technology corrections
code docs/architecture/board-game-ai-consolidation-strategy.md  # Integration approach
```

### 3. Environment Setup
```bash
cd infra

# Copy environment template
cp env/.env.example env/.env.dev

# Edit with your API keys (add these lines)
echo "LLMWHISPERER_API_KEY=your_key_here" >> env/.env.dev
echo "OPENROUTER_API_KEY=your_key_here" >> env/.env.dev

# Start services
docker compose up -d

# Verify health
curl http://localhost:8080/health  # API
curl http://localhost:6333/health  # Qdrant
curl http://localhost:11434/api/tags  # Ollama models
```

### 4. Run Existing Tests (Verify Baseline)
```bash
# Backend tests
cd apps/api
dotnet test  # Should pass (90%+ coverage)

# Frontend tests
cd apps/web
pnpm test  # Should pass (90%+ coverage)
```

### 5. Review Existing Code (1 Hour)
```bash
# Key files to understand:
apps/api/src/Api/BoundedContexts/DocumentProcessing/  # PDF processing (existing)
apps/api/src/Api/BoundedContexts/KnowledgeBase/      # RAG pipeline (existing)
apps/api/src/Api/Services/RagService.cs              # Extend with multi-model validation
apps/web/src/pages/                                  # Frontend pages (add board-game-ai/)
```

**You're Ready!** Attend Sprint 1 Planning (Jan 23) with setup complete.

---

## 🎓 Learning Resources (Optional, 2-3 Hours)

### Board Game AI Domain
- **Research Doc**: `docs/kb/Sistemi AI per arbitrare giochi da tavolo stato dell'arte 2025.md` (1 hour)
  - Competitor analysis (45-75% accuracy documented)
  - Hallucination problem (ChatGPT invented "multiple Gandalfs")
  - Italian market unserved (zero dedicated systems found)

### LLMWhisperer (30 min)
- Docs: https://llmwhisperer.com/docs
- Try demo: https://llmwhisperer.com/playground (upload sample PDF)
- Key feature: Layout preservation for LLM consumption

### OpenRouter (30 min)
- Docs: https://openrouter.ai/docs
- Models: https://openrouter.ai/models (compare GPT-4 Turbo vs Claude 3.5)
- Pricing: https://openrouter.ai/pricing (understand cost per query)

### SmolDocling (30 min)
- GitHub: https://github.com/DS4SD/docling
- Examples: https://github.com/DS4SD/docling/tree/main/examples
- Vision-language model: Processes PDFs as images (no OCR needed)

---

## 🆘 Help & Support

### Questions During Development

**Technical Questions**:
- Slack: #engineering channel
- Email: engineering@meepleai.dev (CTO responds within 24h)

**Board Game AI Specific**:
- Slack: #board-game-ai channel (create after team announcement)
- Docs: docs/board-game-ai-documentation-index.md (full navigation)

**Issue/Bug Reports**:
- GitHub Issues: Tag with `board-game-ai` label
- Urgent: Slack @engineering-lead (for blockers)

### Common Issues & Solutions

**"LLMWhisperer API returns 429"**:
- Cause: Free tier rate limit (100 pages/day exceeded)
- Solution: Use cached results OR wait 24 hours OR upgrade to paid tier ($29/mo)

**"SmolDocling slow on my machine"**:
- Cause: No GPU (falls back to CPU, 6x slower)
- Solution: Accept slow for MVP (one-time per rulebook) OR use DevOps GPU machine

**"OpenRouter returns 401 Unauthorized"**:
- Cause: Invalid API key or insufficient credits
- Solution: Check OPENROUTER_API_KEY env var, verify $50 credits added

**"Ollama model not found"**:
- Cause: Model not pulled
- Solution: `docker exec ollama ollama pull mistral:7b-instruct-v0.3-q4_K_M`

---

## ✅ Quick Checklist (Before Sprint 1)

**Accounts & Access**:
- [ ] LLMWhisperer account + API key
- [ ] OpenRouter account + $50 credits + API key
- [ ] GitHub access (meepleai-monorepo repo, write permissions)
- [ ] Slack/Discord (team channels)

**Environment**:
- [ ] Docker Compose running (all services healthy)
- [ ] .env.dev configured (LLMWHISPERER_API_KEY, OPENROUTER_API_KEY)
- [ ] Ollama models pulled (mistral, llama3, nomic-embed-text)
- [ ] Tests passing (dotnet test, pnpm test both green)

**Knowledge**:
- [ ] Read Implementation Notes (technology corrections)
- [ ] Read Consolidation Strategy (integration approach)
- [ ] Review Execution Calendar (your sprint assignments)

**Ready for Sprint 1**: ✅ All checkboxes above complete

---

## 📊 Metrics to Track (Your KPIs)

### Developer Metrics (Personal)
- **Velocity**: Story points completed per sprint (target: 5-8 per 2 weeks)
- **Quality**: Test coverage (maintain 90%+)
- **Cycle Time**: Issue opened → merged (target: <5 days)

### Team Metrics (Collective)
- **Sprint Burndown**: Remaining story points (should trend to 0 by Friday)
- **Build Health**: CI/CD passing (green checkmarks)
- **Code Review**: PR review time (target: <24 hours)

### Product Metrics (Phase 1 Exit)
- **Accuracy**: ≥80% (measured weekly on golden dataset)
- **Beta Users**: 100 recruited (target: 2 weeks recruitment)
- **Satisfaction**: ≥4.0/5.0 (post-beta survey)

**Dashboard**: Grafana at http://localhost:3001 (admin/admin) - Board Game AI panel added Sprint 7

---

## 🎉 Milestone Celebrations

**Sprint 2 Complete** (Feb 21): 🍕 Team lunch (PDF pipeline working end-to-end)

**Sprint 6 Complete** (Apr 18): 🍾 Demo day (show Italian UI to advisors)
- **GATE 1 DECISION**: Tech viable for 95%+ accuracy? (Critical decision point)

**Beta Launch** (May 5): 🚀 Launch party (100 beta users recruited)

**Phase 1 Complete** (Jun 27): 🏆 Offsite (team dinner, Phase 1 retrospective)
- **GATE 2 DECISION**: Proceed to Phase 2? (Publisher outreach, production launch)

---

**Good Luck! Andiamo! 🚀**

---

**Document**: Quick Start Guide
**Audience**: All Team Members (New & Existing)
**Purpose**: 5-Minute Onboarding for Board Game AI Phase 1
**Status**: Ready for Distribution
