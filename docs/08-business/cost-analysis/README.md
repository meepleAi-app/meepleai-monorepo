# Cost Analysis & Budget Planning

**Directory**: `docs/08-business/cost-analysis/`
**Audience**: Founders, CFO, business stakeholders, investors
**Purpose**: Infrastructure cost analysis, budget planning, provider comparison

---

## 📚 Documents in This Section

### Complete Cost Analysis (10K+ Users)

**[COST_ANALYSIS_2025.md](./COST_ANALYSIS_2025.md)** - Original comprehensive analysis
- **Scope:** GitHub private repo + Docker infrastructure (dev, staging, production)
- **Target:** 10,000+ MAU (monthly active users)
- **Infrastructure:** 29 vCPU, 61.5 GB RAM, 15 services
- **Cost Range:** €1,000-2,500/month (full production)
- **Providers:** AWS, Azure, GCP, Hetzner, DigitalOcean

**Key Sections:**
- GitHub pricing (Free/Team/Enterprise plans)
- Complete infrastructure breakdown (15 Docker services)
- Cloud provider comparison (AWS Fargate, Azure Container Apps, GCP GKE)
- AI/LLM costs (OpenRouter, self-hosted Ollama)
- Storage, bandwidth, email, CDN, monitoring
- 3 scenarios: MVP/Alpha, Beta, Production

**CSV Export:** [cost-analysis-data.csv](./cost-analysis-data.csv)

---

### Optimized Cost Analysis (2-5K Users) ⭐ RECOMMENDED

**[COST_ANALYSIS_OPTIMIZED_2025.md](./COST_ANALYSIS_OPTIMIZED_2025.md)** - Right-sized for initial launch
- **Scope:** All infrastructure costs optimized for 2,000-5,000 users
- **Infrastructure:** 10 vCPU, 26 GB RAM, 10-12 services (reduced stack)
- **Cost Range:** €80-240/month (balanced production-ready)
- **Focus:** Budget-friendly alternatives, self-hosted LLM, free tiers

**Key Optimizations:**
- **AI/LLM:** 95% savings via self-hosted Ollama + OpenRouter fallback (€10-20/mo vs €230/mo)
- **Infrastructure:** 70-85% savings via Hetzner vs AWS (€70-240/mo vs €1,000/mo)
- **CDN/Bandwidth:** 100% savings via Cloudflare Free
- **Email:** 95% savings via AWS SES (€2/mo vs €30-60/mo)
- **Domain:** .it/,com €9/yr (NOT .ai €140/yr)

**3 Setup Tiers:**
1. **Budget MVP:** €83/month (€996/year) - Bootstrap, self-hosted everything
2. **Balanced Production:** €240/month (€2,880/year) ⭐ **RECOMMENDED** - Mix of self-hosted + managed
3. **Premium AWS:** €1,015/month (€12,180/year) - Fully managed, auto-scaling

**CSV Export:** [cost-analysis-optimized-2025.csv](./cost-analysis-optimized-2025.csv)

---

## 🔗 Related Documentation

### Infrastructure & Deployment
- [EU Hosting Comparison](../../05-operations/deployment/infrastructure/EU_HOSTING_COMPARISON_2025.md) - Top 10 EU providers analyzed
- [Deployment Guide](../../05-operations/deployment-guide.md) - Complete deployment workflow
- [Multi-Environment Strategy](../../05-operations/deployment/multi-environment-strategy.md) - Dev/staging/prod setup

### Business Planning
- [Business Plan](../board-game-ai-business-plan.md) - Complete business strategy
- [Roadmap](../../07-project-management/roadmap/ROADMAP.md) - Product roadmap

### Architecture
- [System Architecture](../../01-architecture/overview/system-architecture.md) - Technical design
- [Infrastructure Diagrams](../../01-architecture/diagrams/infrastructure-overview.md) - Architecture diagrams

---

## 📊 Quick Comparison

| Metric | Original (10K+ users) | Optimized (2-5K users) | Savings |
|--------|----------------------|------------------------|---------|
| **Infrastructure** | €1,000/mo | €70-240/mo | **-€760-930/mo** |
| **AI/LLM** | €230/mo | €10-20/mo | **-€210-220/mo** |
| **Total Monthly** | €1,390-1,690/mo | €82-289/mo | **-€1,101-1,608/mo** |
| **Total Yearly** | €16,680-20,280/yr | €984-3,468/yr | **-€13,212-19,296/yr** |
| **Savings %** | - | - | **75-85%** |

---

## 🎯 Which Analysis to Use?

**Use COST_ANALYSIS_OPTIMIZED_2025.md if:**
- ✅ Starting with 2,000-5,000 users
- ✅ Budget-conscious (need <€300/month)
- ✅ MVP/early production phase
- ✅ Want maximum cost optimizations
- ✅ Can manage self-hosted infrastructure

**Use COST_ANALYSIS_2025.md if:**
- ✅ Planning for 10,000+ users
- ✅ Need to understand full-scale costs
- ✅ Comparing managed cloud providers
- ✅ Enterprise/investor presentations
- ✅ Budget >€500/month available

---

## 💰 Key Cost Drivers

### Infrastructure (Largest Impact)
- **Cloud Provider Choice:** Hetzner -70% vs AWS
- **Managed vs Self-Hosted:** -€50-150/month
- **Right-Sizing:** Only provision what you need

### AI/LLM (Second Largest)
- **Self-Hosted Ollama:** -95% vs API-only
- **Quantization (4-bit):** Runs on CPU, no GPU needed
- **Caching:** -40% duplicate queries

### Services (Additive)
- **CDN:** Cloudflare Free (€0) vs paid (€30-100/mo)
- **Email:** AWS SES (€2) vs SendGrid (€30-60/mo)
- **Monitoring:** Self-hosted (€0) vs Datadog (€100+/mo)

---

## 📅 Update History

- **2025-11-22:** Created cost-analysis section
  - Added COST_ANALYSIS_2025.md (original 10K+ users)
  - Added COST_ANALYSIS_OPTIMIZED_2025.md (optimized 2-5K users)
  - Added CSV exports for both analyses
- **Next Update:** Quarterly review (Q1 2026) or when pricing changes

---

**Maintained by:** Engineering Team
**Last Verified:** 2025-11-22
**Sources:** 50+ official provider pricing pages, verified November 2025
