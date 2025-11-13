# MeepleAI Strategic Roadmap - Italian Board Game Rules Assistant

**Status**: Approved for Implementation
**Version**: 2.0 (Consolidated with Existing MeepleAI System)
**Date**: 2025-01-15
**Owner**: Product & Engineering Team

---

## ⚠️ SYSTEM INTEGRATION NOTICE

This roadmap describes **Board Game AI features integrated into the existing MeepleAI system** (ASP.NET Core 9.0), NOT a separate new system.

**Technology Stack**: ASP.NET Core 9.0 + Next.js 16 + Qdrant + OpenRouter (feature-flagged with Ollama fallback)

**See**: [Consolidation Strategy](../architecture/board-game-ai-consolidation-strategy.md) for integration details.

---

## Executive Summary

**Mission**: Enhance existing MeepleAI system with Italian-focused board game rules assistance, achieving >95% accuracy through multi-model validation, publisher partnerships, and advanced PDF processing.

**Market Opportunity**:
- Mercato italiano completamente unserved (nessun sistema dedicato identificato)
- Community attiva: La Tana dei Goblin, Bottega Ludica (10K+ utenti potenziali)
- Gap competitivo: Competitor accuracy 45-75% vs target 95%+
- First-mover advantage con publisher partnerships italiani

**Strategic Approach**: Niche Focus → Publisher Partnerships → Quality-First → Platform Play

---

## Market Analysis & Competitive Landscape

### Current State of AI Board Game Assistants (2025)

**Commercial Products** (documento fonte: `docs/kb/Sistemi AI per arbitrare giochi da tavolo stato dell'arte 2025.md`):

| Product | Coverage | Accuracy | Multilingual | Limitations |
|---------|----------|----------|--------------|-------------|
| **RulesBot.ai** | Web SaaS | Undisclosed | Claimed | No independent validation |
| **BoardGameAssistant.ai** | Web SaaS | Claimed 99.2% | Claimed | Independent tests show issues |
| **Ludomentor** (Awaken Realms) | iOS/Android | ~95% (1 error in 20+ queries) | Limited | Publisher-only games, 3.6★ rating |
| **Rule Master** (Copenhagen) | Discord bot | High (manual training) | Limited | Small database, explicit accuracy focus |
| **ChatGPT** (tested by Digital Trends) | General AI | 73.5% (estimated) | Yes | Hallucinations: invented "multiple Gandalfs", "Will of the West dice" |

**Key Findings**:
- ⚠️ "Even one mistake negatively impacts game session" - accuracy threshold significantly higher than typical AI
- ⚠️ No robust multilingual solutions identified (BoardGameAssistant.ai claims but no verification)
- ⚠️ Italian market completely unserved despite active community
- ✅ Technology mature: RAG architecture production-ready
- ✅ Academic research gap: Solo Mills 2013 su rule extraction, zero multilingual research

### Market Opportunity Quantification

**Italian Board Game Market**:
- La Tana dei Goblin: 50K+ registered users, 200K+ monthly visits
- Bottega Ludica: Active forum community, curated game reviews
- Asmodee Italia: Major publisher with 100+ localized titles
- Giochi Uniti: Italian publisher with original titles + localizations

**Target Segments**:
1. **Casual Players** (60%): Home gameplay, occasional rules lookup, mobile-first
2. **Enthusiasts** (30%): Frequent gameplay, complex games, accuracy-sensitive
3. **Competitive Players** (10%): Tournament-level, zero-tolerance for errors, citation verification

**Revenue Potential** (Conservative):
- Phase 2 (Month 12): 1,000 MAU × 5% conversion = 50 premium × €4.99 = €249/month
- Phase 3 (Month 18): 5,000 MAU × 10% conversion = 500 premium × €4.99 + 2 B2B (€1K each) = €4,495/month
- Phase 4 (Month 24): 10,000 MAU × 12% conversion = 1,200 premium × €4.99 + 5 B2B (€1.5K avg) = €15,488/month

### Competitive Differentiation

**MeepleAI Unique Value Propositions**:

1. **Quality-First**: 95%+ accuracy target vs 45-75% competitors
   - Multi-model validation (GPT-4 + Claude consensus)
   - Citation verification (page number + text snippet matching)
   - Explicit uncertainty admission (no hallucinations)

2. **Italian-Native**: First robust Italian system
   - Fine-tuned embeddings on Italian board game corpus
   - Terminology handling (preserve game-specific keywords)
   - Cultural adaptation (professional translation patterns)

3. **Publisher Partnerships**: Official content access
   - Giochi Uniti, Asmodee Italia targeted (Phase 2)
   - White-label integration for publisher websites
   - Co-marketing and community endorsement

4. **Open-Source Foundation**: Community trust and transparency
   - Core RAG pipeline open-source (Apache 2.0)
   - Academic dataset release (1000 Italian Q&A pairs, CC-BY)
   - Contributor-friendly development model

---

## Product Vision & Roadmap

### Phase 1: MVP Foundation (Months 1-6, 2-3 FTE)

**Goal**: Validate product-market fit con 100 beta users e accuracy >80% su dataset limitato

**Milestones** (Integrated into Existing ASP.NET Core System):
- **Sprint 1-2**: LLMWhisperer C# client + SmolDocling Python microservice integration
- **Sprint 3-4**: 3-stage PDF pipeline (LLMWhisperer → SmolDocling → Docnet.Core fallback)
- **Sprint 5-6**: Multi-model validation (OpenRouter: GPT-4 + Claude consensus)
- **Sprint 7-8**: 5-metric quality framework + Italian localization
- **Sprint 9-10**: Frontend enhancements (Italian UI, citation viewer, mobile-optimized)
- **Sprint 11-12**: Beta launch (La Tana dei Goblin partnership, 100 user recruitment)

**Success Criteria**:
- ✅ 100 beta users recruited in 2 weeks
- ✅ Accuracy ≥80% on golden dataset (100 Q&A, 10 games)
- ✅ Hallucination rate ≤10% on adversarial queries
- ✅ User satisfaction ≥4.0/5.0 (post-beta survey)
- ✅ System uptime ≥99% during beta period

**Budget**: €10-15K (infrastructure, API costs, contractor fees)

**Deliverables**:
- Working MVP deployed to staging (DigitalOcean/Heroku)
- 10 Italian games indexed (Terraforming Mars, Wingspan, Azul, Scythe, Ticket to Ride, Catan, Pandemic, 7 Wonders, Agricola, Splendor)
- Beta user feedback report and iteration plan
- GitHub repository public (core RAG pipeline open-source)

---

### Phase 2: Production Readiness (Months 7-12, 3-4 FTE)

**Goal**: Launch production system con 1,000 MAU e publisher partnerships

**Focus Areas**:
1. **Production Stack Enhancement** (Leverage Existing Infrastructure):
   - Qdrant optimization (hybrid search, Italian collections)
   - Redis cluster (already planned, HA setup, 3 nodes)
   - PostgreSQL replication (already available, primary + replica)
   - CDN for static assets (Cloudflare)

2. **Publisher Partnerships**:
   - Sign agreements: Giochi Uniti, Asmodee Italia
   - Official content licensing (50-100 games)
   - White-label integration POC (iframe embed)
   - Revenue share model: 70% platform, 30% publisher

3. **Scale & Performance** (Extend Existing Optimizations):
   - Semantic caching (Redis + existing HybridCache, 40-60% hit rate target)
   - Smart model routing via OpenRouter (auto-fallback to cheaper models)
   - Load testing (100 RPS sustained, leverage existing test infrastructure)
   - Circuit breakers (OpenRouter API + Ollama fallback for LLM resilience)

4. **Mobile App**:
   - Progressive Web App (PWA) with offline fallback
   - iOS/Android wrappers (Capacitor)
   - Push notifications for rulebook updates/errata

**Success Criteria**:
- ✅ 1,000+ MAU (10x MVP growth)
- ✅ Accuracy ≥90% (validated monthly)
- ✅ Publisher endorsement: 2+ major brands
- ✅ System uptime ≥99.5% (SLA established)
- ✅ 50+ premium subscriptions (€249/month revenue)

**Budget**: €30-50K (team scale-up, infrastructure, marketing)

**Deliverables**:
- Production deployment (AWS EKS or equivalent)
- 50-100 games indexed (publisher-licensed content)
- Mobile app launched (iOS + Android)
- Monitoring & alerting (Prometheus, Grafana, PagerDuty)
- Publisher B2B dashboard (analytics, revenue reporting)

---

### Phase 3: Competitive Differentiation (Months 13-18, 4-5 FTE)

**Goal**: Achieve 95%+ accuracy gold standard e 5,000 MAU

**Focus Areas**:
1. **Hybrid Search Enhancement** (Extend Existing AI-14):
   - Qdrant hybrid search (sparse + dense vectors)
   - RRF fusion (70% vector + 30% keyword, existing implementation)
   - Query expansion (synonyms, Italian game terms)

2. **Fine-Tuned Italian Model**:
   - Corpus: 10K Italian rulebooks + community FAQ
   - Method: Contrastive learning on annotated Q&A pairs
   - Validation: +5-10 accuracy points over generic embeddings

3. **Advanced Validation** (OpenRouter Multi-Model):
   - Citation correctness ≥90% (stringent page validation)
   - Multi-model ensemble via OpenRouter (GPT-4 + Claude + Gemini consensus)
   - A/B testing framework (extend existing PromptEvaluationService)
   - Ollama fallback for cost control (free self-hosted option)

4. **Academic Publication**:
   - IEEE CoG 2026 paper submission (target: June 2026)
   - Dataset release: 1000 Italian Q&A pairs (CC-BY license)
   - Open-source evaluation tools (benchmarks, scripts)

**Success Criteria**:
- ✅ Accuracy ≥95% (gold standard achieved)
- ✅ 5,000+ MAU (5x Phase 2 growth)
- ✅ Academic paper accepted (IEEE CoG or FDG)
- ✅ 200+ games indexed
- ✅ 500+ premium subscriptions (€2,495/month revenue)

**Budget**: €60-100K (ML engineering, research, marketing)

**Deliverables**:
- Fine-tuned Italian embedding model (open-source release)
- Hybrid search production deployment
- IEEE CoG paper + dataset on Hugging Face
- 200 games indexed (expanded catalog)
- Advanced admin dashboard (A/B testing, model performance)

---

### Phase 4: Platform Ecosystem (Months 19-24, 5-6 FTE)

**Goal**: Build platform with 10,000 MAU e international expansion

**Focus Areas**:
1. **Open API**:
   - Public REST API (OpenAPI 3.1 specification)
   - Rate limits (freemium: 10/day, premium: unlimited)
   - SDK libraries (Python, JavaScript, .NET)
   - Developer documentation (examples, tutorials)

2. **Third-Party Integrations**:
   - BoardGameArena proof-of-concept (AI bot integration)
   - Tabletop Simulator mod (in-game rule lookup)
   - Discord bot (community servers integration)

3. **Community Features**:
   - User corrections (report wrong answer → admin review)
   - Crowdsourced Q&A (community-contributed FAQ)
   - Leaderboard & gamification (top contributors)

4. **International Expansion**:
   - French (France + Quebec markets)
   - German (Germany board game culture)
   - Spanish (Spain + LatAm markets)
   - Per-language fine-tuning (~30% effort each)

**Success Criteria**:
- ✅ 10,000+ MAU (2x Phase 3 growth)
- ✅ 3+ third-party integrations live
- ✅ 20% non-Italian users (international adoption)
- ✅ 100+ external developers using API
- ✅ 1,200+ premium subs (€5,988/month) + 5 B2B deals (€7,500/month) = €13,488/month revenue

**Budget**: €100-150K (international expansion, API development, integrations)

**Deliverables**:
- Public API with SDK libraries (Python, JS, .NET)
- 3+ third-party integrations (BoardGameArena, Tabletop Simulator, Discord)
- International versions (French, German, Spanish)
- Community contribution platform (user corrections, crowdsourced Q&A)
- 500+ games indexed (international catalog)

---

## Technology Strategy

### Open-Source Philosophy

**Core Principles**:
1. **Transparency Builds Trust**: Open-source core = community validation
2. **Academic Contribution**: Research advances the field (publication + dataset)
3. **Community Collaboration**: Contributors improve quality faster than solo development
4. **Sustainable Hybrid Model**: Open core + proprietary premium features

**Open-Source Components** (Apache 2.0 License):
- RAG pipeline (PDF processing, chunking, embeddings, retrieval)
- Multi-model validation framework
- 5-metric testing & evaluation tools
- Italian NLP utilities (terminology glossary, sentence tokenizer)
- API client libraries (Python, JavaScript, .NET SDKs)

**Proprietary Components** (Closed-Source):
- Publisher-licensed rulebook content (encrypted storage)
- Fine-tuned Italian embeddings (trained on licensed corpus)
- Premium API access (rate limits, priority support, SLA)
- White-label integration (B2B publisher customization)
- Advanced admin features (A/B testing, analytics dashboards)

### Technology Stack

**MVP (Phase 1)**:
```yaml
Backend:
  - Language: Python 3.11+
  - Framework: FastAPI
  - PDF: LLMWhisperer (free tier) → SmolDocling (fallback)
  - Embeddings: multilingual-e5-large (Sentence Transformers)
  - Vector DB: ChromaDB (local, embedded)
  - LLM: OpenAI GPT-4 Turbo (primary)
  - Cache: Redis (single instance)

Frontend:
  - Framework: Next.js 14 + React 18
  - Styling: Tailwind CSS
  - State: Zustand (lightweight)
  - PDF Viewer: react-pdf

Infrastructure:
  - Deployment: Docker Compose
  - Hosting: DigitalOcean App Platform
  - Monitoring: Basic logs (stdout)
```

**Production (Phase 2+)**:
```yaml
Backend:
  - Vector DB: Weaviate self-hosted (Kubernetes)
  - LLM: GPT-4 Turbo (primary) + Claude 3.5 Sonnet (validation)
  - Cache: Redis Cluster (3 nodes, HA)
  - Database: PostgreSQL 16 (primary + replica)
  - Search: PostgreSQL Full-Text Search (hybrid with vector)

Frontend:
  - Mobile: Capacitor (iOS + Android wrappers)
  - PWA: Service Workers (offline support)

Infrastructure:
  - Orchestration: Kubernetes (AWS EKS or DigitalOcean)
  - IaC: Terraform (VPC, RDS, EKS, S3)
  - Monitoring: Prometheus + Grafana + PagerDuty
  - Tracing: OpenTelemetry → Jaeger
  - CDN: Cloudflare (static assets, DDoS protection)
  - Secrets: AWS Secrets Manager
```

### AI/ML Strategy

**Embedding Model Evolution**:
- **Phase 1**: multilingual-e5-large (pre-trained, zero-shot)
- **Phase 2**: Fine-tune on 1K Italian Q&A pairs (contrastive learning)
- **Phase 3**: Fine-tune on 10K corpus (Italian rulebooks + community FAQ)
- **Phase 4**: Cross-lingual transfer (Italian → French/German/Spanish)

**LLM Strategy**:
- **Primary**: OpenAI GPT-4 Turbo (highest accuracy, broad knowledge)
- **Validation**: Claude 3.5 Sonnet (diversity, consensus checking)
- **Fallback**: Claude (if GPT-4 unavailable, circuit breaker pattern)
- **Cost Optimization**: GPT-3.5 Turbo for simple queries (smart routing based on complexity)

**Validation Architecture**:
```
Layer 1: Confidence Threshold (≥0.70 required, else explicit uncertainty)
Layer 2: Multi-Model Consensus (GPT-4 + Claude, cosine similarity ≥0.90)
Layer 3: Citation Verification (page number + text snippet match)
Layer 4: Forbidden Keywords (500+ blocklist, detect fabricated rules)
Layer 5: User Feedback (negative reports → admin review → model improvement)
```

---

## Business Model & Monetization

### Freemium Model

**Free Tier** (Acquisition Focus):
- 10 queries/day (24-hour reset)
- All indexed games accessible
- Web-only access (no mobile app)
- Community support (Discord, email)
- Non-intrusive ads (board game related)
- **Target**: 80-90% of users (organic growth, word-of-mouth)

**Premium Tier** (€4.99/month or €49.99/year):
- Unlimited queries
- Mobile app (iOS + Android)
- Offline mode (download rulebooks)
- Priority support (email, 48h response SLA)
- Ad-free experience
- Advanced features (game state tracking, custom variants)
- **Target**: 10-15% conversion (quality differentiation drives upgrade)

**Publisher B2B** (Custom Pricing: €500-2,000/month):
- White-label integration (iframe embed, branded UI)
- Official content prioritized (publisher games indexed first)
- Analytics dashboard (query volume, popular games, user engagement)
- Revenue share: 70% platform, 30% publisher
- Co-marketing (joint announcements, social media, conferences)
- **Target**: 5-10 publishers by Phase 4

**Educational/Non-Profit** (Free or Discounted):
- Academic research use (cite dataset, publication acknowledgment)
- Board game cafes/clubs (promote community, partnership model)
- Schools/libraries (educational purpose, grant-funded)

### Revenue Projections

| Phase | Timeline | MAU | Premium | B2B Deals | Monthly Revenue | Monthly Costs | Net |
|-------|----------|-----|---------|-----------|----------------|---------------|-----|
| **Phase 1 (MVP)** | Month 6 | 100 | 0 | 0 | €0 | €500 | -€500 |
| **Phase 2 (Launch)** | Month 12 | 1,000 | 50 (5%) | 0 | €249 | €500 | -€251 |
| **Phase 3 (Growth)** | Month 18 | 5,000 | 500 (10%) | 2 (€1K each) | €4,495 | €10,000 | -€5,505 |
| **Phase 4 (Scale)** | Month 24 | 10,000 | 1,200 (12%) | 5 (€1.5K avg) | €15,488 | €16,000 | -€512 |
| **Sustainability** | Month 30-36 | 15,000 | 2,250 (15%) | 8-10 | €25,000+ | €22,000 | **+€3,000** |

**Breakeven Target**: Month 30-36 (2.5-3 years from start)

### Funding Strategy

**Bootstrap Phase 1** (€5-10K):
- Personal funds + volunteers
- Open-source contributors (community-driven development)
- GitHub Sponsors (sustainable FOSS model)

**Grant Phase 2** (€100-500K):
- EU Horizon Europe (Innovation Actions, AI research)
- Italian MISE grants (digitalization, innovation)
- Board game industry grants (Asmodee Foundation, Hasbro)

**Angel/Seed Phase 3** (€200-500K):
- 18-24 months runway (team scale-up, international expansion)
- Board game VC (Tabletop Ventures, similar)
- Strategic angels (publisher executives, game designers)

**Revenue Phase 4** (Self-Funded):
- Premium + B2B revenue sustainable (€25K+/month)
- Potential Series A (€2-5M) if scale international (50K+ MAU)

---

## Risk Management

### Critical Risks & Mitigation Strategies

**1. Accuracy Risk** [CRITICAL - SEVERITY: 9/10]

**Risk**: "One mistake ruins game session" - even 90% accuracy insufficient for competitive play

**Impact**: User trust destroyed, viral negative feedback, difficult recovery

**Mitigation**:
- Multi-model validation (GPT-4 + Claude consensus, cosine similarity ≥0.90)
- 5-metric testing framework (Accuracy ≥80%, Hallucination ≤10%, Confidence ≥0.70, Citation ≥80%, Latency ≤3000ms)
- Explicit uncertainty admission (confidence <0.70 → "Non ho informazioni sufficienti")
- User feedback loop (negative reports → admin review → regression test)
- Quality gates (pre-production: all 5 metrics must pass thresholds)

**Monitoring**: Weekly accuracy evaluation on golden dataset, alert if <90%

---

**2. Hallucination Risk** [CRITICAL - SEVERITY: 9/10]

**Risk**: AI invents rules (e.g., ChatGPT "multiple Gandalfs", "Will of the West dice")

**Impact**: Incorrect gameplay, competition disputes, reputational damage

**Mitigation**:
- Five-layer defense:
  1. Confidence threshold (≥0.70 required)
  2. Multi-model consensus (GPT-4 + Claude agreement)
  3. Citation verification (page number + text snippet match)
  4. Forbidden keywords (500+ blocklist from known hallucination cases)
  5. User feedback (negative reports → investigation → blocklist update)
- Red team testing (100+ adversarial queries designed to trigger hallucination)
- Post-incident protocol (add to blocklist, retrain, publish transparency report)

**Monitoring**: Hallucination rate <3% target, alert if >5% in 24 hours

---

**3. Publisher Partnership Risk** [HIGH - SEVERITY: 7/10]

**Risk**: Publishers unwilling to license content, fear of competition with official apps

**Impact**: Limited official content access, credibility gap vs competitors

**Mitigation**:
- Value proposition: Reduce customer support queries (publishers' pain point)
- Revenue share model (30% publisher, 70% platform)
- White-label integration (publisher-branded, embedded on their websites)
- Non-exclusive licensing (multiple publishers, diversify risk)
- Early outreach (Phase 1 completion → approach Giochi Uniti immediately)
- Fallback: Community-curated content (La Tana dei Goblin, Bottega Ludica)

**Monitoring**: Target 2 publishers by Phase 2, escalate outreach if no response after 2 months

---

**4. Cost Escalation Risk** [MEDIUM - SEVERITY: 6/10]

**Risk**: LLM API costs scale with users (GPT-4 expensive at scale)

**Impact**: Burn rate exceeds projections, need additional funding

**Mitigation**:
- Semantic caching (Redis FAISS, 0.95+ similarity → 40-60% cache hit rate)
- Smart model routing (GPT-3.5 simple queries, GPT-4 complex → 30% cost reduction)
- Query limits (freemium 10/day, prevent abuse)
- Publisher B2B revenue (offset consumer API costs)
- Self-hosted alternatives (Ollama + Mistral for development, evaluate for production if costs exceed €5K/month)

**Monitoring**: API costs tracked weekly, alert if >€2K/month Phase 2, >€5K/month Phase 3

---

**5. Competition Risk** [MEDIUM - SEVERITY: 5/10]

**Risk**: Incumbent (BoardGameAssistant.ai, Ludomentor) launches Italian version

**Impact**: First-mover advantage lost, market share competition

**Mitigation**:
- Speed to market (Phase 1 in 6 months, launch before competitors react)
- Quality differentiation (95%+ accuracy, explicit uncertainty, citation verification)
- Community trust (open-source core, academic publication, transparency)
- Publisher partnerships (exclusive content access via licensing)
- Network effects (user corrections, crowdsourced Q&A improve quality over time)

**Monitoring**: Track competitor announcements (Google Alerts, industry news), pivot if needed

---

## Success Metrics & KPIs

### Technical KPIs

| Metric | Phase 1 Target | Phase 2 Target | Phase 3 Target | Phase 4 Target | Measurement |
|--------|----------------|----------------|----------------|----------------|-------------|
| **Accuracy** | ≥80% | ≥90% | ≥95% | ≥95% | Weekly evaluation on golden dataset |
| **Hallucination Rate** | ≤10% | ≤5% | ≤3% | ≤3% | Forbidden keywords + user reports |
| **P95 Latency** | <5s | <3s | <3s | <3s | Prometheus histogram |
| **Uptime** | ≥99% | ≥99.5% | ≥99.5% | ≥99.9% | Healthcheck monitoring |
| **Confidence R²** | ≥0.6 | ≥0.7 | ≥0.7 | ≥0.7 | Monthly regression analysis |

### Business KPIs

| Metric | Phase 1 | Phase 2 | Phase 3 | Phase 4 | Measurement |
|--------|---------|---------|---------|---------|-------------|
| **MAU** | 100 | 1,000 | 5,000 | 10,000 | Google Analytics |
| **Conversion Rate** | 0% (beta) | 5% | 10% | 12-15% | Stripe subscriptions |
| **Retention (MoM)** | N/A (beta) | ≥60% | ≥70% | ≥70% | Cohort analysis |
| **Publisher Partnerships** | 0 | 2 | 5 | 8-10 | Signed contracts |
| **NPS** | ≥4.0/5.0 | ≥40 | ≥50 | ≥50 | Quarterly survey |

### Community KPIs

| Metric | Phase 1 | Phase 2 | Phase 3 | Phase 4 | Measurement |
|--------|---------|---------|---------|---------|-------------|
| **Discord Members** | 100 | 500 | 1,500 | 3,000 | Discord analytics |
| **Open-Source Contributors** | 3 | 5 | 10 | 20 | GitHub Insights |
| **Academic Citations** | 0 | 0 | 5+ (post-pub) | 10+ | Google Scholar |
| **Community Q&A** | 0 | 0 | 100 | 500 | User-contributed FAQ |

---

## Governance & Organization

### Team Structure

**Phase 1 (MVP)** - 2-3 FTE:
- 1 Backend Engineer (Python, FastAPI, RAG pipeline)
- 1 Frontend Engineer (Next.js, React, mobile responsive)
- 1 DevOps Engineer (Docker, CI/CD, monitoring)
- Community volunteers (beta testing, Q&A annotation)

**Phase 2 (Production)** - 3-4 FTE:
- +1 Backend Engineer (scale, performance optimization)
- +1 QA Engineer (testing automation, quality assurance)
- +1 Community Manager (user support, publisher relations)
- Part-time: Designer, Marketing, Legal (contracts)

**Phase 3 (Growth)** - 4-5 FTE:
- +1 ML Engineer (fine-tuning, model optimization)
- +1 Data Scientist (analytics, A/B testing)
- Part-time: Academic researcher (IEEE CoG paper, dataset)

**Phase 4 (Scale)** - 5-6 FTE:
- +1 Product Manager (roadmap, prioritization)
- +1 Full-time Marketing (international expansion)
- +1 DevRel (API developer support, integrations)

### Decision-Making Process

**Product Decisions**:
- Weekly product reviews (team + key stakeholders)
- User feedback prioritization (NPS, support tickets, community forum)
- Data-driven (A/B testing for significant changes)

**Technical Decisions**:
- Architecture Decision Records (ADRs) for major choices
- Code review process (2 approvals required)
- Quarterly tech debt sprints (20% capacity)

**Business Decisions**:
- Monthly financial reviews (burn rate, revenue, projections)
- Quarterly strategic planning (OKRs, roadmap adjustments)
- Board meetings (if investors involved, quarterly cadence)

### Open-Source Governance

**Community Participation**:
- RFC process for major features (Request for Comments)
- Public roadmap (GitHub Projects, quarterly updates)
- Monthly contributor calls (community sync, Q&A)

**Code of Conduct**:
- Contributor Covenant 2.1 (industry standard)
- Inclusive language, respectful communication
- Clear escalation path (maintainers → core team)

**Contribution Guidelines**:
- CONTRIBUTING.md (setup, testing, PR process)
- Issue templates (bug report, feature request)
- PR checklist (tests, docs, changelog)

---

## Next Steps & Immediate Actions

### Week 1: Foundation Setup

**Team Assembly**:
- [ ] Recruit Backend Engineer (Python/FastAPI expertise, RAG experience preferred)
- [ ] Recruit DevOps Engineer (Docker/Kubernetes, CI/CD automation)
- [ ] Setup communication channels (Slack/Discord workspace, email)
- [ ] Setup project management (Linear/Jira, GitHub Projects)

**Technical Setup**:
- [ ] GitHub organization creation (MeepleAI-org or similar)
- [ ] Repository initialization (monorepo structure: backend, frontend, docs)
- [ ] CI/CD pipeline (GitHub Actions: lint, typecheck, test)
- [ ] Docker Compose baseline (PostgreSQL, Weaviate, Redis containers)

**Documentation**:
- [ ] Review this roadmap document with team (kickoff meeting)
- [ ] Align on Phase 1 scope and sprint breakdown
- [ ] Assign Sprint 1 tasks (infrastructure setup)

---

### Week 2-3: MVP Prototyping

**Infrastructure**:
- [ ] Complete Docker Compose stack (all services healthy)
- [ ] Basic FastAPI skeleton + healthcheck endpoint
- [ ] Next.js frontend scaffold (Tailwind CSS, basic routing)

**PDF Processing**:
- [ ] LLMWhisperer trial account setup (validate free tier limits)
- [ ] Test on 5 sample Italian rulebooks (Terraforming Mars, Wingspan, Azul, Scythe, Catan)
- [ ] Evaluate PDF processing quality (multi-column, tables, formatting)

**LLM Access**:
- [ ] OpenAI API key setup (GPT-4 Turbo access verification)
- [ ] Claude API access (if using for validation in MVP)
- [ ] Cost estimation for Phase 1 (100 beta users, avg 50 queries/user)

---

### Week 4: Community Outreach

**Game Selection**:
- [ ] Finalize 10 Italian games for MVP (popularity + mechanics diversity)
- [ ] Source rulebooks (download official PDFs, verify licensing)
- [ ] Draft golden dataset schema (Q&A format, annotation guidelines)

**Community Partnership**:
- [ ] Contact La Tana dei Goblin admins (beta program proposal)
- [ ] Contact Bottega Ludica (parallel partnership, diversify risk)
- [ ] Prepare beta signup form (Google Forms, waitlist management)
- [ ] Draft beta program docs (onboarding, expectations, feedback channels)

---

### Sprint 1 Kickoff (Week 5)

**Goals**:
- Docker Compose stack running locally
- Basic API healthcheck endpoint functional
- Frontend Hello World page deployed
- CI/CD pipeline passing (lint, typecheck)

**Team Assignments**:
- Backend Engineer: FastAPI setup, Docker Compose services
- DevOps Engineer: CI/CD pipeline, GitHub Actions workflows
- Frontend Engineer: Next.js scaffold, Tailwind configuration

**Success Criteria**:
- [ ] `docker compose up` → all services healthy
- [ ] `curl localhost:8000/health` → 200 OK
- [ ] `npm run dev` → Next.js app accessible localhost:3000
- [ ] GitHub Actions: All checks passing (green checkmarks)

---

## Appendix A: Reference Documents

**Market Research**:
- `docs/kb/Sistemi AI per arbitrare giochi da tavolo stato dell'arte 2025.md` (152 lines, comprehensive landscape analysis)

**Technical Specifications** (to be created in parallel):
- `docs/architecture/adr-001-hybrid-rag-architecture.md`
- `docs/architecture/adr-002-multilingual-embedding-strategy.md`
- `docs/architecture/adr-003-pdf-processing-pipeline.md`
- `docs/api/openapi-specification.yaml`
- `docs/testing/testing-strategy.md`

**Competitive Analysis Sources**:
- RulesBot.ai: https://rulesbot.ai
- BoardGameAssistant.ai: https://boardgameassistant.ai
- Rule Master (Discord): La Tana dei Goblin community mentions
- Academic: Mills 2013 thesis, IEEE CoG proceedings 2024-2025

**Community Resources**:
- La Tana dei Goblin: https://www.gdt.it/forum
- Bottega Ludica: https://www.bottegaludica.it
- Giochi Uniti: https://www.giochiuniti.it
- Asmodee Italia: https://www.asmodee.it

---

## Appendix B: Glossary

**RAG (Retrieval Augmented Generation)**: AI architecture combining document retrieval with LLM generation for accurate, cited responses

**Vector Database**: Specialized database for similarity search on high-dimensional embeddings (Weaviate, ChromaDB, Pinecone)

**Embedding**: Numerical vector representation of text, enabling semantic similarity search

**RRF (Reciprocal Rank Fusion)**: Algorithm for combining multiple ranked lists (e.g., vector + keyword search results)

**Hallucination**: AI-generated content that is factually incorrect or fabricated (e.g., inventing non-existent rules)

**Confidence Score**: Numerical measure (0.0-1.0) of AI's certainty in its answer, based on retrieval quality and model probability

**Citation**: Reference to source document (page number + text snippet) supporting AI's answer

**MAU (Monthly Active Users)**: Number of unique users active in a calendar month

**NPS (Net Promoter Score)**: Metric measuring user satisfaction and likelihood to recommend (scale: -100 to +100)

**SLA (Service Level Agreement)**: Contractual uptime guarantee (e.g., 99.5% = ~3.6 hours downtime/month)

---

**Document Metadata**:
- **Version**: 1.0
- **Last Updated**: 2025-01-15
- **Next Review**: 2025-04-15 (quarterly)
- **Approvers**: Product Lead, Engineering Lead, CEO
- **Status**: APPROVED for Phase 1 Implementation
