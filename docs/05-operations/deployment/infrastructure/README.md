# Infrastructure Provider Comparison

**Directory**: `docs/05-operations/deployment/infrastructure/`
**Audience**: DevOps, SRE, architects, technical decision makers
**Purpose**: Cloud provider evaluation, infrastructure selection, deployment planning

---

## 📚 Documents in This Section

### EU Hosting Providers Comparison

**[EU_HOSTING_COMPARISON_2025.md](./EU_HOSTING_COMPARISON_2025.md)** - Top 10 EU providers analyzed ⭐
- **Scope:** Comprehensive comparison of European cloud/VPS providers
- **Target:** 2,000-5,000 users deployment
- **Requirements:** 10-12 vCPU, 26-30 GB RAM, Docker support, EU datacenter
- **Providers Analyzed:** 10 (Germany, France, Italy, Finland, Switzerland)

**Top 3 Recommendations:**
1. **🥇 Contabo (Germany):** €40-50/month - Budget champion
2. **🥈 Hetzner (Germany/Finland):** €70-93/month - Balanced excellence ⭐ **RECOMMENDED**
3. **🥉 Aruba Cloud (Italy):** €15-44/month - Italian datacenters + 50% promo

**Other Providers Covered:**
- **OVH** (France) - Large provider, anti-DDoS
- **UpCloud** (Finland/Sweden) - Premium performance
- **Scaleway** (France) - GPU instances, innovation
- **Netcup** (Germany) - Ultra budget
- **Exoscale** (Switzerland) - Maximum privacy
- **IONOS** (Germany/UK) - Enterprise focus
- **Hostinger** (Lithuania) - Entry level

---

## 🏆 Quick Decision Matrix

### By Budget
- **<€50/month** → Contabo (€40-50)
- **€50-100/month** → Hetzner (€70-93) ⭐ **RECOMMENDED**
- **€100+/month** → UpCloud (€100-150)

### By Datacenter Location
- **Germany** → Hetzner or Contabo
- **France** → OVH or Scaleway
- **Italy** → Aruba Cloud
- **Switzerland** → Exoscale (max privacy)
- **Nordics** → UpCloud (Finland/Sweden)

### By Priority
- **Maximum Performance** → UpCloud or Hetzner
- **Best Value** → Hetzner ⭐
- **Lowest Price** → Contabo or Netcup
- **Privacy/Compliance** → Exoscale (Swiss)
- **Italian Data Residency** → Aruba Cloud

---

## 📊 Performance Benchmarks

| Provider | CPU Score | Performance | Price/Performance |
|----------|-----------|-------------|-------------------|
| **Hetzner** | 2,150 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **UpCloud** | 2,100 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Contabo** | 1,900 | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **OVH** | 1,850 | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Aruba** | 1,700 | ⭐⭐⭐ | ⭐⭐⭐⭐ |

*CPU Score: GeekBench 6 multi-core (higher is better)*

---

## 💡 Recommended Setup for MeepleAI

### Hetzner 2× CPX41 (Balanced Production)

**Configuration:**
- 2× CPX41 servers (8 vCPU, 16 GB RAM each)
- **Total:** 16 vCPU, 32 GB RAM, 480 GB NVMe SSD
- **Cost:** €92.82/month (€1,116/year)
- **Bandwidth:** 20 TB/server included

**Alternative (More Budget):**
- 1× CPX41 + 1× CPX31 → €70/month
- Total: 12 vCPU, 24 GB RAM, 400 GB NVMe

**Why Hetzner:**
- ✅ Best price/performance balance
- ✅ 99.95% uptime SLA
- ✅ Performance +13% vs Contabo
- ✅ NVMe SSD (fast I/O)
- ✅ EU datacenters (Germany, Finland)
- ✅ Excellent documentation
- ✅ 20 TB bandwidth included
- ✅ Clear scaling path

**Service Distribution:**

**Server #1 - Application Stack:**
- API (ASP.NET): 3 vCPU, 6 GB
- Web (Next.js): 2 vCPU, 4 GB
- Ollama LLM: 2 vCPU, 4 GB
- Monitoring: 1 vCPU, 2 GB

**Server #2 - Data Stack:**
- PostgreSQL: 4 vCPU, 8 GB
- Qdrant (vectors): 2 vCPU, 4 GB
- Redis: 1 vCPU, 2 GB
- n8n + Seq: 1 vCPU, 2 GB

---

## 🔗 Related Documentation

### Cost Analysis
- [Cost Analysis Optimized (2-5K users)](../../../08-business/cost-analysis/COST_ANALYSIS_OPTIMIZED_2025.md) - Complete cost breakdown
- [Cost Analysis Full Scale (10K+ users)](../../../08-business/cost-analysis/COST_ANALYSIS_2025.md) - Original analysis

### Deployment
- [Deployment Guide](../deployment-guide.md) - Complete deployment workflow
- [Multi-Environment Strategy](../multi-environment-strategy.md) - Dev/staging/prod setup
- [Disaster Recovery](../disaster-recovery.md) - Backup and recovery

### Architecture
- [Infrastructure Overview](../../../01-architecture/diagrams/infrastructure-overview.md) - Architecture diagrams
- [System Architecture](../../../01-architecture/overview/system-architecture.md) - Technical design

---

## 🌍 GDPR & Compliance

All providers listed are **EU-based** and **GDPR compliant**:

| Provider | Country | GDPR | ISO 27001 | CISPE | Notes |
|----------|---------|------|-----------|-------|-------|
| **Hetzner** | 🇩🇪 Germany | ✅ | ✅ | ✅ | Native German compliance |
| **Contabo** | 🇩🇪 Germany | ✅ | ✅ | - | German data protection |
| **Aruba** | 🇮🇹 Italy | ✅ | ✅ | ✅ | Italian compliance, CISPE member |
| **OVH** | 🇫🇷 France | ✅ | ✅ | ✅ | French data protection |
| **UpCloud** | 🇫🇮 Finland | ✅ | ✅ | ✅ | Finnish compliance |
| **Exoscale** | 🇨🇭 Switzerland | ✅ | ✅ | - | Swiss privacy (strictest), no US Cloud Act |

---

## 🚀 Migration Path

### Phase 1: Launch (0-2K users)
- **Provider:** Hetzner CX33 or Contabo VPS M
- **Cost:** €5-15/month
- **Setup:** Single server all-in-one

### Phase 2: Growth (2-5K users) ⭐ CURRENT TARGET
- **Provider:** Hetzner 2× CPX or Contabo 2× VPS
- **Cost:** €40-90/month
- **Setup:** App server + Data server

### Phase 3: Scale (5-20K users)
- **Provider:** Hetzner CPX/CCX or UpCloud
- **Cost:** €150-300/month
- **Setup:** Load balanced (3-4 servers)
- **Add:** Managed DB, load balancer

### Phase 4: Enterprise (20K+ users)
- **Provider:** UpCloud, Hetzner Dedicated, or AWS
- **Cost:** €500-2,000/month
- **Setup:** Kubernetes cluster, multi-region
- **Add:** CDN premium, Managed K8s

---

## 📅 Update History

- **2025-11-22:** Created infrastructure section
  - Added EU_HOSTING_COMPARISON_2025.md (10 providers analyzed)
  - Top 3 recommendations: Contabo, Hetzner, Aruba Cloud
  - Performance benchmarks and decision matrix
- **Next Update:** Quarterly review (Q1 2026) or when new providers/pricing

---

**Maintained by:** DevOps Team
**Last Verified:** 2025-11-22
**Sources:** Official provider websites, VPSBenchmarks.com, independent reviews
