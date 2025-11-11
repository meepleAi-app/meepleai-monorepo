# MeepleAI Documentation Index

**Project**: Italian Board Game Rules AI Assistant
**Version**: 1.0
**Last Updated**: 2025-01-15

---

## Quick Start

**New Team Members** → Read in this order:
1. [Strategic Roadmap](./roadmap/board-game-ai-strategic-roadmap.md) - Vision, market, 4-phase plan
2. [System Architecture](./architecture/board-game-ai-architecture-overview.md) - Technical design, components, data flow
3. [API Specification](./api/board-game-ai-api-specification.md) - REST API endpoints, examples
4. [Testing Strategy](./testing/board-game-ai-testing-strategy.md) - Quality gates, test pyramid
5. [Deployment Guide](./deployment/board-game-ai-deployment-guide.md) - Docker Compose → Kubernetes

**Developers** → Focus on:
- API Specification + Architecture Decision Records (ADRs)
- Testing Strategy (unit + integration patterns)
- Deployment Guide (local setup instructions)

**Business Stakeholders** → Focus on:
- Strategic Roadmap (market analysis, business model)
- Business Plan (revenue projections, funding needs)

---

## Documentation Structure

### 📊 Strategic Documentation

**[Strategic Roadmap](./roadmap/board-game-ai-strategic-roadmap.md)** (50 pages)
- Executive summary and market opportunity
- 4-phase implementation roadmap (MVP → Production → Scale → Platform)
- Technology strategy and stack evolution
- Business model and revenue projections
- Risk management and success metrics
- **Audience**: Founders, investors, strategic partners

**[Business Plan](./business/board-game-ai-business-plan.md)** (35 pages)
- Market analysis and competitive landscape
- Business model and unit economics (LTV/CAC, margins)
- Go-to-market strategy per phase
- Financial projections (18-month forecast)
- Funding requirements and sources
- Exit strategy and valuation scenarios
- **Audience**: Investors, board of directors, CFO

---

### 🏗️ Technical Documentation

**[System Architecture Overview](./architecture/board-game-ai-architecture-overview.md)** (60 pages)
- Architecture principles (Quality-First, Defense-in-Depth)
- High-level and component architecture
- Data flow diagrams (Q&A, indexing pipelines)
- Technology stack per phase
- Security architecture (auth, encryption, secrets)
- Deployment architecture (Docker → Kubernetes)
- Scalability considerations
- **Audience**: Engineers, architects, technical leadership

**[Architecture Decision Records (ADRs)](./architecture/)** (15+ documents, 5-10 pages each)
- ADR-001: Hybrid RAG Architecture
- ADR-002: Multilingual Embedding Strategy
- ADR-003: PDF Processing Pipeline
- ADR-004: Vector DB Selection
- ADR-005: LLM Strategy
- ADR-006: Caching Strategy
- **Audience**: Engineering team, technical decision-makers

---

### 🔌 API & Integration

**[API Specification v1.0](./api/board-game-ai-api-specification.md)** (40 pages)
- REST API endpoints (Question Answering, Games, Rulebooks, Users, Admin)
- Authentication (API key, session cookies)
- Rate limiting (tier-based)
- Error handling (standard error format, HTTP codes)
- Data models (TypeScript interfaces)
- Examples (cURL, Python, JavaScript)
- SDK libraries (Phase 2+)
- **Audience**: API consumers, frontend developers, third-party integrators

---

### 🧪 Quality & Testing

**[Testing Strategy](./testing/board-game-ai-testing-strategy.md)** (30 pages)
- Testing philosophy ("One Mistake Ruins Session")
- Test pyramid (70% unit, 20% integration, 5% quality, 5% E2E)
- 5-metric framework (Accuracy, Hallucination, Confidence, Citation, Latency)
- Golden dataset (1000 Q&A pairs, 10 games)
- Synthetic adversarial dataset (100+ hallucination triggers)
- Quality gates (PR → pre-production)
- Testing tools and infrastructure
- **Audience**: QA engineers, developers, quality assurance team

---

### 🚀 Deployment & Operations

**[Deployment Guide](./deployment/board-game-ai-deployment-guide.md)** (35 pages)
- Phase 1: Docker Compose (local + staging)
- Phase 2: Kubernetes (production, AWS EKS)
- Infrastructure as Code (Terraform manifests)
- Monitoring & observability (Prometheus, Grafana, PagerDuty)
- CI/CD pipeline (GitHub Actions)
- Rollback strategies (manual + automated canary)
- Security best practices
- Cost optimization
- **Audience**: DevOps engineers, SRE, infrastructure team

---

## Key Concepts & Glossary

### Technical Terms

**RAG (Retrieval Augmented Generation)**: Architecture combining document retrieval with LLM generation for accurate, cited responses. Pipeline: Query → Retrieve relevant chunks → Generate answer with context → Validate.

**Vector Database**: Specialized database for high-dimensional similarity search (e.g., Weaviate, Pinecone). Stores embeddings (numerical representations of text) and retrieves semantically similar documents.

**Embedding**: Numerical vector (e.g., 1024 dimensions) representing text semantics. Similar texts have similar embeddings (measured via cosine similarity).

**Hallucination**: AI-generated content that is factually incorrect or fabricated. Example: ChatGPT inventing "multiple Gandalfs" in War of the Ring (non-existent in actual game).

**Confidence Score**: Numerical measure (0.0-1.0) of AI's certainty, based on retrieval quality and model probability. Threshold: ≥0.70 required for valid answer.

**Multi-Model Validation**: Using multiple LLMs (e.g., GPT-4 + Claude) to validate answers via consensus (cosine similarity ≥0.90 between responses).

**RRF (Reciprocal Rank Fusion)**: Algorithm combining ranked lists from multiple sources. Formula: `score = Σ(1 / (k + rank))` where k=60. Used to merge vector + keyword search results.

---

### Business Terms

**MAU (Monthly Active Users)**: Unique users active in a calendar month (measured via analytics).

**MRR (Monthly Recurring Revenue)**: Predictable monthly revenue from subscriptions. ARR = MRR × 12.

**LTV (Lifetime Value)**: Total revenue from a customer over their lifetime. Formula: `LTV = ARPU × Avg Lifetime (months)`.

**CAC (Customer Acquisition Cost)**: Cost to acquire one customer. Formula: `CAC = Marketing Spend / New Customers`.

**Churn Rate**: % customers canceling per month. Retention = 1 - Churn. Target: <10% monthly churn (>90% retention).

**NPS (Net Promoter Score)**: Metric measuring satisfaction. Scale: -100 to +100. Calculation: % Promoters (9-10 rating) - % Detractors (0-6 rating).

---

## Document Maintenance

### Review Schedule

**Quarterly Reviews** (every 3 months):
- Strategic Roadmap: Adjust timelines, validate assumptions, update metrics
- Architecture Overview: Assess tech debt, plan refactoring, evaluate new tools
- API Specification: Review breaking changes, plan v2.0 if needed
- Business Plan: Update revenue projections, refine go-to-market

**Annual Reviews** (every 12 months):
- Comprehensive documentation audit (outdated sections, broken links)
- Lessons learned (what worked, what didn't, pivot decisions)
- Competitive landscape update (new entrants, industry changes)

### Contribution Guidelines

**Updating Documentation**:
1. Create feature branch: `git checkout -b docs/update-roadmap-q2`
2. Make changes (Markdown format, follow existing structure)
3. Add to changelog section in document (version, date, summary)
4. Create PR with `[DOCS]` prefix: `[DOCS] Update Q2 2025 roadmap milestones`
5. Request review from doc owner (see document metadata)
6. Merge after approval

**Creating New Documents**:
- Follow template structure (Table of Contents, sections, metadata footer)
- Add to this index (`docs/board-game-ai-documentation-index.md`)
- Link from related documents (cross-references)
- Tag with appropriate audience (developers, business, investors)

---

## Related Documentation

### External Resources

**Research Foundation**:
- Source document: [docs/kb/Sistemi AI per arbitrare giochi da tavolo stato dell'arte 2025.md](../kb/Sistemi%20AI%20per%20arbitrare%20giochi%20da%20tavolo%20%20stato%20dell'arte%202025.md)
- 152 lines comprehensive landscape analysis (commercial products, academic research, tech stacks, open-source projects, implementation case studies)

**Academic Papers**:
- Mills 2013: "Learning Board Game Rules from an Instruction Manual" (University of Washington)
- IEEE CoG 2024: "Grammar-based Game Description Generation using Large Language Models" (arXiv:2407.17404)
- LREC 2016: Question-answering for Minecraft (Dumont, Tian, Inui)

**Community Resources**:
- La Tana dei Goblin: https://www.gdt.it/forum (Italian board game community)
- BoardGameGeek: https://boardgamegeek.com (global board game database)
- BGG Forums: Rules section (community Q&A, precedent for crowdsourced rules)

**Technology Documentation**:
- Weaviate Docs: https://weaviate.io/developers/weaviate (vector DB)
- LangChain: https://python.langchain.com (RAG framework)
- Sentence Transformers: https://www.sbert.net (embedding models)
- FastAPI: https://fastapi.tiangolo.com (Python API framework)

---

## FAQ

**Q: Why Italian-first instead of English?**
A: Italian market completely unserved (zero direct competitors), active community (La Tana dei Goblin 50K+ users), first-mover advantage, lower competition. English market saturated with ChatGPT, BoardGameAssistant.ai, etc.

**Q: Why open-source core if business model is paid?**
A: Open core builds trust (community validation), attracts contributors (faster development), enables academic research (IEEE CoG publication), differentiates from proprietary competitors. Proprietary = licensed publisher content + premium features.

**Q: Why 95% accuracy target instead of 99%+?**
A: Realistic based on competitor analysis (45-75% current state). 95% achievable via multi-model validation + fine-tuning + publisher content. 99%+ requires human-in-the-loop validation (too expensive for MVP).

**Q: How to handle rulebook copyright/licensing?**
A: Phase 1: Use publicly available rulebooks (fair use for indexing). Phase 2+: Publisher partnerships with explicit licensing agreements. Content encrypted at rest, access controlled per user tier.

**Q: What if OpenAI/Anthropic change pricing or APIs?**
A: Semantic caching reduces dependency (40-60% hit rate = fewer API calls). Fallback: Self-hosted Ollama + Mistral (open-source LLM). API abstraction layer enables switching providers without code changes.

**Q: Why not build mobile app first?**
A: Web-first faster to iterate (no app store approval delays). Progressive Web App (PWA) provides mobile experience in Phase 1. Native iOS/Android wrappers (Capacitor) in Phase 2 once product-market fit validated.

---

## Contact & Support

**Documentation Issues**:
- GitHub Issues: https://github.com/meepleai/meepleai-monorepo/issues (tag: `documentation`)
- Slack: `#docs-feedback` channel

**Technical Questions**:
- Email: engineering@meepleai.dev
- Slack: `#engineering` channel

**Business Inquiries**:
- Email: business@meepleai.dev
- Publisher partnerships: partnerships@meepleai.dev

---

**Index Metadata**:
- **Version**: 1.0
- **Maintainer**: Documentation Team
- **Last Updated**: 2025-01-15
- **Next Update**: 2025-02-15 (monthly)
