# MeepleAI Infrastructure Cost Summary

**Version**: 1.2
**Last Updated**: 2026-01-20
**Source Analysis**: infrastructure-sizing-analysis-2026-01-18.md
**Added**: GitHub Repository & CI/CD Cost Analysis section

---

## Executive Summary

**Budget Alignment Overview**:

| Phase | Monthly Cost | Annual Cost | Budget Target | Status |
|-------|--------------|-------------|---------------|--------|
| **Alpha** (10 users) | **€19.30** | €231.60 | €50-200 | ✅ **96% under budget** |
| **Beta** (100 users) | **€78.85** | €946.20 | €50-200 | ✅ **61% under budget** |
| **Release 1K** (1,000 users) | **€367.65** | €4,411.80 | €200 | ⚠️ **Requires revenue** |
| **Release 10K** (10,000 users) | **€1,714** | €20,568 | €200 | ❌ **Requires funding** |

**Key Finding**: Alpha and Beta phases are fully sustainable within initial budget. Release phases require revenue generation or external funding.

---

## Detailed Cost Breakdown

### ALPHA Phase (10 Users) - €19.30/mese

| Component | Service | Specs | Monthly Cost | Annual Cost |
|-----------|---------|-------|--------------|-------------|
| **VPS** | Hetzner CPX31 | 4 vCPU, 16GB RAM, 160GB NVMe | €15.41 | €184.92 |
| **Backup** | Hetzner Snapshots | 7-day retention | €3.08 | €36.96 |
| **Domain** | Cloudflare Registrar | meepleai.com | €0.81 | €9.77 |
| **Email** | SendGrid Free | 3,000 email/mese | €0 | €0 |
| **2FA** | Self-hosted TOTP | App-based (Google Authenticator) | €0 | €0 |
| **SSL** | Let's Encrypt | Auto-renewed | €0 | €0 |
| **DNS** | Cloudflare | Unlimited queries | €0 | €0 |
| **Monitoring** | Grafana/Prometheus | Self-hosted on VPS | €0 | €0 |
| **TOTAL** | - | - | **€19.30** | **€231.65** |

**Budget Margin**: €200 - €19.30 = **€180.70/mese available** (90% buffer)

**Performance Expectations**:
- API response time (p95): <500ms
- Concurrent users: 5-8
- Uptime: 95-97%
- RAG query capacity: 50 query/ora peak

---

### BETA Phase (100 Users) - €78.85/mese

**Architecture**: 2-node setup (Application server + Database server)

**Node 1 - Application Server**:
| Component | Service | Specs | Monthly Cost |
|-----------|---------|-------|--------------|
| VPS | Hetzner CCX33 | 8 vCPU AMD, 32GB RAM, 240GB NVMe | €44.90 |
| Backup | Snapshots | 7-day retention | €3.08 |

**Services on Node 1**: .NET API, Python AI services (embedding, reranker, unstructured), Redis

---

**Node 2 - Database Server**:
| Component | Service | Specs | Monthly Cost |
|-----------|---------|-------|--------------|
| VPS | Hetzner CPX31 | 4 vCPU, 16GB RAM, 160GB NVMe | €15.41 |
| Storage Upgrade | Additional volume | +340GB → 500GB total | €6.80 |
| Backup | Snapshots | 7-day retention | €3.08 |

**Services on Node 2**: PostgreSQL, Qdrant

---

**Communication Services**:
| Component | Service | Volume | Monthly Cost |
|-----------|---------|--------|--------------|
| Domain | Cloudflare | meepleai.com + meeple-ai.com | €1.63 |
| Email | SendGrid Free | 900 email/mese | €0 |
| Email Spike Buffer | AWS SES | Occasional overflow | €0.10 |
| 2FA - TOTP | Self-hosted | 45 users (app-based) | €0 |
| 2FA - SMS | Twilio | 5 users × 8 SMS/mese = 40 SMS | €1.85 |
| SSL | Let's Encrypt | Auto-renewed | €0 |

---

**BETA Total Breakdown**:
| Category | Subtotal | Percentage of Total |
|----------|----------|---------------------|
| Infrastructure (VPS + storage) | €73.27 | 92.9% |
| Communication (domain + email + 2FA) | €3.58 | 4.5% |
| Backup | €2.00 | 2.6% |
| **TOTAL** | **€78.85** | **100%** |

**Budget Margin**: €200 - €78.85 = **€121.15/mese available** (61% buffer)

**Performance Expectations**:
- API response time (p95): <300ms
- Concurrent users: 40-60
- Uptime: 99%
- RAG query capacity: 400 query/ora peak

---

### RELEASE 1K Phase (1,000 Users) - €367.65/mese

**Architecture**: Multi-node cluster with load balancing

**Infrastructure**:
| Component | Service | Specs | Quantity | Monthly Cost |
|-----------|---------|-------|----------|--------------|
| **Load Balancer** | Hetzner CPX11 | 2 vCPU, 4GB RAM | 1 | €7.15 |
| **API Cluster** | Hetzner CCX43 | 16 vCPU, 64GB RAM | 1-2 | €89.90 |
| **Python AI Services** | Hetzner CCX33 | 8 vCPU, 32GB RAM | 1 | €44.90 |
| **Database Primary** | Hetzner CCX33 | 8 vCPU, 32GB RAM | 1 | €44.90 |
| **Storage Volume** | Additional SSD | 1TB for DB | 1 | €10.20 |
| **Redis Cluster** | Hetzner CPX21 | 3 vCPU, 8GB RAM | 3 | €31.35 |
| **Backup** | Snapshots + S3 | All nodes + offsite | - | €15.00 |
| **SUBTOTAL** | - | - | - | **€243.40** |

**Communication Services**:
| Component | Service | Volume | Monthly Cost |
|-----------|---------|--------|--------------|
| Domain | Cloudflare | 3 domains (.com + .io + typo) | €4.55 |
| Email | AWS SES | 8,400 email/mese (< 62K free tier) | €0 |
| 2FA - TOTP | Self-hosted | 360 users | €0 |
| 2FA - SMS | Twilio | 40 users × 8 SMS = 320 SMS | €14.80 |
| **SUBTOTAL** | - | - | **€19.35** |

**RELEASE 1K Total**: €348.30 (infra) + €19.35 (comm) = **€367.65/mese**

**Budget Status**: ⚠️ **Exceeds €200 budget** - Revenue stream required

**Revenue Requirement**:
- Break-even: €367.65/mese
- With 1,000 users: €0.37/user/mese minimum
- **Pricing Model**: €5-10/user/mese suggested (10-27× revenue vs costs)

---

### RELEASE 10K Phase (10,000 Users) - €1,714/mese

**Architecture**: AWS Cloud-native managed services

**Compute & Application**:
| Component | Service | Specs | Monthly Cost |
|-----------|---------|-------|--------------|
| Load Balancer | AWS ALB | 2 AZ, 100GB traffic | €40 |
| API Cluster | ECS Fargate | 4× 4vCPU, 8GB (auto-scale 2-6) | €300 |
| Python AI Services | ECS Fargate | 2× 8vCPU, 16GB | €250 |
| **SUBTOTAL** | - | - | **€590** |

**Data Storage & Caching**:
| Component | Service | Specs | Monthly Cost |
|-----------|---------|-------|--------------|
| Database | RDS PostgreSQL | db.r6g.xlarge (4vCPU, 32GB) Multi-AZ | €450 |
| Vector DB | EC2 + Qdrant | r6g.2xlarge (8vCPU, 64GB) | €280 |
| Redis | ElastiCache | cache.r6g.large (2vCPU, 13GB) Multi-AZ | €180 |
| Object Storage | S3 | 500GB (PDFs, backups) | €15 |
| **SUBTOTAL** | - | - | **€925** |

**Networking & Monitoring**:
| Component | Service | Specs | Monthly Cost |
|-----------|---------|-------|--------------|
| CDN | CloudFront | 1TB traffic | €90 |
| Monitoring | CloudWatch + X-Ray | Logs + tracing | €50 |
| Secrets | Secrets Manager | 50 secrets | €5 |
| **SUBTOTAL** | - | - | **€145** |

**Communication Services**:
| Component | Service | Volume | Monthly Cost |
|-----------|---------|--------|--------------|
| Domain | Cloudflare | 5 domains + Registry Lock | €26.19 |
| Email | AWS SES | 80,000 email (18K over free) | €1.80 |
| 2FA | Supabase Pro | TOTP + auth management | €26.00 |
| **SUBTOTAL** | - | - | **€53.99** |

**RELEASE 10K Total**: €590 + €925 + €145 + €53.99 = **€1,713.99/mese**

**Revenue Requirement**:
- Break-even: €1,714/mese
- With 10,000 users: €0.17/user/mese minimum
- **Pricing Model**: €5-15/user/mese suggested (29-88× revenue vs costs)

**Gross Margin** (at €10/user/mese):
```
Revenue: 10,000 × €10 = €100,000/mese
Costs: €1,714/mese
Gross Margin: (€100,000 - €1,714) / €100,000 = 98.3% 🚀
```

---

## Cost Optimization Opportunities

### Quick Wins (0-30 days implementation)

**1. Reserved Instances / Annual Commitment** (Beta+):
- **Impact**: -15-20% infrastructure costs
- **Savings**: €73/mese × 0.20 = **€14.60/mese** (Beta)
- **Requirement**: Annual prepayment (€879 upfront)

**2. Enable Cloudflare CDN** (all phases):
- **Impact**: -40% bandwidth, +40% performance
- **Cost**: €0 (free tier sufficient)
- **Implementation**: 15 minutes (enable orange cloud in DNS)

**3. Implement Device Trust for 2FA** (Beta+):
- **Impact**: -81% SMS costs
- **Savings**: €14.80 → €6.94 = **€7.86/mese** (Release 1K)
- **Implementation**: 2-4 hours (backend feature)

**Total Quick Win Savings**: €22.46/mese (Beta), €7.86/mese (Release 1K)

---

### Medium-Term Optimizations (1-3 months)

**4. Lazy Model Loading** (Python services):
- **Impact**: -3GB RAM baseline (enable smaller VPS)
- **Savings**: CCX33 → CPX31 = **€29.49/mese**
- **Trade-off**: +5s latency on first query after restart

**5. Spot Instances for PDF Processing** (Release 1K+):
- **Impact**: -70% GPU costs
- **Savings**: €48/mese → €11/mese = **€37/mese**
- **Requirement**: AWS account, batch processing queue

**6. Database Query Caching** (Redis):
- **Impact**: -60% database load (delay scaling trigger)
- **Savings**: Defer 2nd API node for 3-6 months = **€90/mese avoided**
- **Implementation**: 4-8 hours (cache layer integration)

**Total Medium-Term Savings**: €156.49/mese

---

### Long-Term Strategies (6-12 months)

**7. AWS Graviton3 Migration** (Release 10K):
- **Impact**: -20% compute costs
- **Savings**: €984 → €784 = **€200/mese**
- **Requirement**: ARM-compatible Docker images (minor refactor)

**8. Multi-Region CDN** (Release 10K):
- **Impact**: +25% performance, -15% origin bandwidth
- **Cost**: €0 (Cloudflare free tier supports global CDN)
- **Benefit**: Better global latency (Asia, Americas)

**9. Data Deduplication** (Qdrant):
- **Impact**: -35% vector storage growth
- **Savings**: 432GB → 280GB = Defer storage upgrade 6 months = **€10/mese avoided**
- **Implementation**: SHA-256 hash check before PDF processing

**Total Long-Term Savings**: €210/mese

---

## Scaling Cost Projections

### Alpha → Beta Migration (Month 3-6)

**Trigger**: >50 active users OR CPU sustained >70%

**Infrastructure Changes**:
| Action | Cost Delta | Cumulative |
|--------|------------|------------|
| Add Node 2 (DB separation) | +€22.21 | €40.70 |
| Upgrade Node 1 (CPX31 → CCX33) | +€29.49 | €70.19 |
| Add typo domain | +€0.82 | €71.01 |
| Enable SMS 2FA | +€1.85 | €72.86 |
| Increased backups | +€2.00 | €74.86 |
| Email spike buffer | +€0.10 | **€74.96** |

**Monthly Cost Increase**: €74.96 - €19.30 = **+€55.66** (+288%)

**Migration Cost** (one-time):
- Labor: 4-6h weekend migration
- Downtime: 1-2h
- Additional storage: €6.80/mese ongoing

---

### Beta → Release 1K Migration (Month 9-12)

**Trigger**: >80 concurrent users OR API latency p95 >500ms

**Infrastructure Changes**:
| Action | Cost Delta | Cumulative |
|--------|------------|------------|
| Add Load Balancer (CPX11) | +€7.15 | €86.01 |
| Upgrade API node (CCX33 → CCX43) | +€45.00 | €131.01 |
| Add Python services node (CCX33) | +€44.90 | €175.91 |
| Upgrade DB (CPX31 → CCX33 + 1TB) | +€39.09 | €215.00 |
| Add Redis cluster (3× CPX21) | +€31.35 | €246.35 |
| Add 2 domains (.io + typo) | +€3.73 | €250.08 |
| Increase 2FA SMS users | +€12.95 | €263.03 |
| Enhanced backups | +€9.00 | €272.03 |
| **TOTAL from Beta** | - | **€347.89** |

**Monthly Cost Increase**: €367.65 - €78.85 = **+€288.80** (+366%)

**Migration Timeline**: 1-week phased rollout (blue-green deployment)

---

### Release 1K → Release 10K Migration (Month 18-24)

**Trigger**: >500 concurrent users OR VPS management >20h/settimana

**Infrastructure Changes**:
| Action | Cost Delta |
|--------|------------|
| Migrate to AWS managed services | +€1,315 (AWS) - €348.30 (VPS) = **+€966.70** |
| Add premium domains (.ai + extra protection) | +€8.50 |
| Switch to managed 2FA (Supabase Pro) | +€11.20 |
| Enhanced monitoring (CloudWatch + X-Ray) | +€50 |
| CDN (CloudFront) | +€90 |
| **TOTAL Increase** | **+€1,346.35** |

**Monthly Cost**: €367.65 → €1,714 (**+366% increase**)

**Critical**: Requires **Series A funding** or **€100K/mese revenue** (10K users × €10)

---

## Revenue Requirements Analysis

### Break-Even Pricing

**Alpha Phase** (10 users):
```
Monthly cost: €19.30
Break-even price: €19.30 / 10 = €1.93/user/mese
Recommended pricing: €0 (free alpha) - Focus on feedback
```

---

**Beta Phase** (100 users):
```
Monthly cost: €78.85
Break-even price: €78.85 / 100 = €0.79/user/mese
Recommended pricing: €0-2/user/mese (freemium model)
  - Free tier: 5 PDF/week
  - Paid tier: Unlimited PDFs at €2/mese
Target: 30% conversion = 30 users × €2 = €60/mese
Status: Still operating at -€18.85/mese (acceptable for beta)
```

---

**Release 1K Phase** (1,000 users):
```
Monthly cost: €367.65
Break-even price: €367.65 / 1,000 = €0.37/user/mese
Recommended pricing: €5-10/user/mese (industry standard)

Scenario A: €5/mese (budget tier)
  - Revenue: 1,000 × €5 = €5,000/mese
  - Profit: €5,000 - €367.65 = €4,632.35/mese
  - Margin: 92.6% 🚀

Scenario B: €10/mese (standard tier)
  - Revenue: 1,000 × €10 = €10,000/mese
  - Profit: €10,000 - €367.65 = €9,632.35/mese
  - Margin: 96.3% 🚀
```

**Conclusion**: ✅ Even at €5/user/mese, margins are excellent (90%+)

---

**Release 10K Phase** (10,000 users):
```
Monthly cost: €1,714
Break-even price: €1,714 / 10,000 = €0.17/user/mese

Scenario: €10/mese (competitive SaaS pricing)
  - Revenue: 10,000 × €10 = €100,000/mese
  - Profit: €100,000 - €1,714 = €98,286/mese
  - Margin: 98.3% 🚀
  - **Annual profit**: €1,179,432
```

**Valuation Impact** (at €1.2M ARR):
```
SaaS Multiple: 5-10× ARR (industry standard)
Estimated Valuation: €6M - €12M
```

---

## Cost per User Analysis

**Economies of Scale**:

| Phase | Total Cost | Users | Cost per User | Margin at €10/user |
|-------|-----------|-------|---------------|-------------------|
| Alpha | €19.30 | 10 | **€1.93** | 80.7% |
| Beta | €78.85 | 100 | **€0.79** | 92.1% |
| Release 1K | €367.65 | 1,000 | **€0.37** | 96.3% |
| Release 10K | €1,714 | 10,000 | **€0.17** | 98.3% |

**Insight**: Cost per user **decreases by 91%** from Alpha to Release 10K (€1.93 → €0.17)

**Industry Comparison**:
- **MeepleAI**: €0.17/user/mese (Release 10K)
- **Typical SaaS infrastructure**: €2-5/user/mese
- **MeepleAI Advantage**: 90-95% lower infrastructure costs (efficient architecture)

---

## Payment Processing Costs (Future Consideration)

**Not yet included in analysis** - Add when monetization starts:

**Stripe Pricing** (Standard in SaaS):
- Transaction fee: 1.5% + €0.25 per payment
- Monthly subscription: Billed automatically

**Cost Impact** (Release 1K, 1,000 paying users at €10/mese):
```
Monthly revenue: €10,000
Stripe fees: 1,000 × (€10 × 1.5% + €0.25) = €150 + €250 = €400/mese
Net revenue: €10,000 - €400 = €9,600/mese
Infrastructure: -€367.65
Profit: €9,232.35/mese (92.3% margin)
```

**Alternative: Annual Billing** (reduce transaction fees):
```
Annual subscription: €100/anno (€10/mese × 12)
Stripe fee per transaction: €100 × 1.5% + €0.25 = €1.75
Annual fees: 1,000 × €1.75 = €1,750/anno = €145.83/mese
Savings vs monthly: €400 - €145.83 = €254.17/mese (-64%)
```

**Recommendation**: ✅ **Offer annual billing with 15-20% discount** (reduce fees + cash flow boost)

---

## Budget Milestones & Gates

### Gate 1: Alpha Launch (Month 0)
- **Budget**: €19.30/mese
- **Funding**: Personal investment (€200-300 upfront)
- **Decision**: Proceed if initial funding available ✅

---

### Gate 2: Beta Scale (Month 3-6)
- **Budget**: €78.85/mese
- **Funding**: Personal investment or pre-seed (€1,000 runway)
- **Decision**: Proceed if 50+ active users + positive feedback ✅

---

### Gate 3: Revenue Requirement (Month 9-12)
- **Budget**: €367.65/mese
- **Requirement**: €500-1,000/mese revenue OR seed funding
- **Decision Criteria**:
  - ✅ Proceed if: 200+ paying users at €5/mese
  - ⚠️ Seek funding if: <100 paying users (pre-revenue phase)
  - ❌ Pause scaling if: No revenue + no funding (optimize current tier)

---

### Gate 4: Series A Requirement (Month 18-24)
- **Budget**: €1,714/mese
- **Requirement**: €20K/mese revenue OR Series A funding (€500K-1M)
- **Decision Criteria**:
  - ✅ Proceed if: 2,000+ paying users at €10/mese
  - ⚠️ Seek Series A if: Strong growth but not yet profitable
  - ❌ Pivot if: No product-market fit after 18 months

---

## Risk Mitigation Budget

**Recommended Contingency Reserves**:

| Phase | Monthly Cost | +20% Buffer | Recommended Reserve |
|-------|--------------|-------------|---------------------|
| Alpha | €19.30 | €23.16 | €100 (5 months runway) |
| Beta | €78.85 | €94.62 | €500 (6 months runway) |
| Release 1K | €367.65 | €441.18 | €2,500 (6 months runway) |
| Release 10K | €1,714 | €2,056.80 | €15,000 (6-9 months runway) |

**Purpose**: Buffer for unexpected costs (traffic spikes, security incidents, provider price increases)

---

## Cost Tracking & Monitoring

### Monthly Cost Review Checklist

**Review Frequency**: Monthly (first week of month)

**Actions**:
- [ ] Export invoice from Hetzner/AWS
- [ ] Track actual vs budgeted costs
- [ ] Identify cost anomalies (>10% variance)
- [ ] Review resource utilization (CPU, RAM, storage)
- [ ] Forecast next month costs based on growth
- [ ] Update budget projections if user growth differs

**Tools**:
- **Hetzner**: Dashboard → Billing → Invoices
- **AWS**: Cost Explorer → Monthly cost breakdown
- **Spreadsheet**: Track costs over time (Excel/Google Sheets template recommended)

---

### Cost Alerts Setup

**Cloudflare** (no native billing alerts):
- Set calendar reminder: Monthly on 1st to check costs

**AWS** (budget alerts):
```yaml
Budget Configuration:
  Name: "MeepleAI Monthly Budget"
  Amount: €2,000/mese
  Alerts:
    - 50% threshold: Email warning
    - 80% threshold: Email + Slack notification
    - 100% threshold: Critical alert + freeze non-essential resources
```

**Recommendation**: Set up budget tracking spreadsheet from Alpha phase (establish baseline)

---

## Summary & Quick Reference

### Cost Per Phase (All-Inclusive)

| Phase | Infrastructure | Communication | **Total/Month** | **Total/Year** |
|-------|---------------|---------------|----------------|---------------|
| Alpha | €18.49 | €0.81 | **€19.30** | **€231.60** |
| Beta | €73.27 | €5.58 | **€78.85** | **€946.20** |
| Release 1K | €348.30 | €19.35 | **€367.65** | €4,411.80 |
| Release 10K | €1,660 | €53.99 | **€1,713.99** | €20,567.88 |

---

### Recommended Pricing Strategy

| Phase | Suggested Price | Target Conversion | Monthly Revenue | Profit |
|-------|----------------|-------------------|-----------------|--------|
| Alpha | €0 (free) | 100% | €0 | -€19.30 (investment) |
| Beta | €2 (optional) | 30% | €60 | -€18.85 (acceptable) |
| Release 1K | €5-10 | 50-70% | €2,500-7,000 | +€2,132-€6,632 ✅ |
| Release 10K | €10-15 | 60-80% | €60,000-120,000 | +€58,286-€118,286 ✅ |

---

## Tiered Pricing Revenue Model

### Pricing Tier Definitions

**Free Tier** (€0/mese):
- 5 PDF uploads/settimana (20/mese)
- 50 RAG queries/giorno (1,500/mese)
- 2 game sessions attive
- Community support (forum, docs)
- **Target**: User acquisition, product trial

**Normal Tier** (€6/mese):
- 25 PDF uploads/settimana (100/mese)
- 200 RAG queries/giorno (6,000/mese)
- 10 game sessions attive
- Email support (48h response)
- Advanced analytics
- **Target**: Regular users, hobbyists

**Premium Tier** (€14/mese):
- Unlimited PDF uploads
- Unlimited RAG queries
- Unlimited game sessions
- Priority email support (24h response)
- Advanced analytics + export
- API access (future)
- Custom AI model fine-tuning (future)
- **Target**: Power users, game clubs, content creators

---

### Conversion Assumptions

**Industry Benchmarks** (Freemium SaaS):
| Tier | Typical Conversion | MeepleAI Target | Rationale |
|------|-------------------|-----------------|-----------|
| **Free** | 85-95% | **88%** | Most users stay free (trial, casual use) |
| **Normal** | 5-12% | **10%** | Regular users willing to pay for convenience |
| **Premium** | 1-3% | **2%** | Power users, enthusiasts, professionals |

**Conversion Improvement Strategies**:
- Onboarding campaigns: Free → Normal (+2-3% conversion)
- Feature gating: Advanced analytics only in Normal+ (incentive)
- Annual discount: 15-20% off (lock-in for 12 months)
- Referral program: "Invite 3 friends → 1 month Normal free"

---

### Revenue Modeling by Registration Volume

#### Scenario 1: 100 Total Registrations (Beta Start)

**User Distribution**:
| Tier | Percentage | Users | Price/User | Monthly Revenue |
|------|------------|-------|------------|-----------------|
| Free | 88% | 88 | €0 | €0 |
| Normal | 10% | 10 | €6 | €60 |
| Premium | 2% | 2 | €14 | €28 |
| **TOTAL** | **100%** | **100** | - | **€88** |

**Financial Analysis**:
| Metric | Value |
|--------|-------|
| Monthly Revenue | €88 |
| Infrastructure Cost | €78.85 (Beta phase) |
| **Net Profit** | **+€9.15** |
| **Profit Margin** | 10.4% |

**Status**: ✅ **Break-even achieved** with just 100 users!

---

#### Scenario 2: 250 Total Registrations (Beta Growth)

**User Distribution**:
| Tier | Users | Monthly Revenue |
|------|-------|-----------------|
| Free | 220 (88%) | €0 |
| Normal | 25 (10%) | €150 |
| Premium | 5 (2%) | €70 |
| **TOTAL** | **250** | **€220** |

**Financial Analysis**:
| Metric | Value |
|--------|-------|
| Monthly Revenue | €220 |
| Infrastructure Cost | €78.85 |
| **Net Profit** | **+€141.15** |
| **Profit Margin** | 64.2% |

**Annual Projection**: €141.15 × 12 = **€1,693.80 profit/anno** (without funding!)

---

#### Scenario 3: 500 Total Registrations (Pre-Release 1K)

**User Distribution**:
| Tier | Users | Monthly Revenue |
|------|-------|-----------------|
| Free | 440 (88%) | €0 |
| Normal | 50 (10%) | €300 |
| Premium | 10 (2%) | €140 |
| **TOTAL** | **500** | **€440** |

**Infrastructure Requirements**:
- Still on Beta architecture (2-node)
- Approaching trigger for Release 1K migration (500 users = scaling threshold)

**Financial Analysis**:
| Metric | Value |
|--------|-------|
| Monthly Revenue | €440 |
| Infrastructure Cost | €78.85 (Beta) |
| **Net Profit** | **+€361.15** |
| **Profit Margin** | 82.1% |

**Decision Point**:
- Revenue €440 > Release 1K cost €367.65 ✅
- **Can afford scaling to Release 1K infrastructure** from revenue alone!

---

#### Scenario 4: 1,000 Total Registrations (Release 1K Target)

**User Distribution**:
| Tier | Users | Monthly Revenue |
|------|-------|-----------------|
| Free | 880 (88%) | €0 |
| Normal | 100 (10%) | €600 |
| Premium | 20 (2%) | €280 |
| **TOTAL** | **1,000** | **€880** |

**Infrastructure**: Release 1K architecture (multi-node cluster)

**Financial Analysis**:
| Metric | Value |
|--------|-------|
| Monthly Revenue | €880 |
| Infrastructure Cost | €367.65 |
| Stripe Fees (1.5% + €0.25) | €120 × (€880 × 0.015 + 0.25) = €43.20 |
| **Net Profit** | **+€469.15** |
| **Profit Margin** | 53.3% |

**Annual Projection**: €469.15 × 12 = **€5,629.80/anno** 🚀

---

#### Scenario 5: 2,500 Total Registrations (Growth Phase)

**User Distribution**:
| Tier | Users | Monthly Revenue |
|------|-------|-----------------|
| Free | 2,200 (88%) | €0 |
| Normal | 250 (10%) | €1,500 |
| Premium | 50 (2%) | €700 |
| **TOTAL** | **2,500** | **€2,200** |

**Infrastructure**: Release 1K (sufficient up to ~3,000 users)

**Financial Analysis**:
| Metric | Value |
|--------|-------|
| Monthly Revenue | €2,200 |
| Infrastructure Cost | €367.65 |
| Stripe Fees | €250 × (€2,200 × 0.015 + 0.25) = €111.50 |
| **Net Profit** | **+€1,720.85** |
| **Profit Margin** | 78.2% |

**Annual Projection**: €1,720.85 × 12 = **€20,650.20/anno**

**Valuation Impact** (at €26,400 ARR):
```
SaaS Multiple: 8× ARR (typical for growth-stage)
Estimated Valuation: €211,200
```

---

#### Scenario 6: 5,000 Total Registrations (Pre-Release 10K)

**User Distribution**:
| Tier | Users | Monthly Revenue |
|------|-------|-----------------|
| Free | 4,400 (88%) | €0 |
| Normal | 500 (10%) | €3,000 |
| Premium | 100 (2%) | €1,400 |
| **TOTAL** | **5,000** | **€4,400** |

**Infrastructure**: Approaching Release 10K threshold (need scaling)

**Financial Analysis**:
| Metric | Value |
|--------|-------|
| Monthly Revenue | €4,400 |
| Infrastructure Cost | €367.65 (Release 1K stretched) |
| Stripe Fees | €600 × (€4,400 × 0.015 + 0.25) = €249 |
| **Net Profit** | **+€3,783.35** |
| **Profit Margin** | 86.0% |

**Decision**: Revenue €4,400 >> Release 10K cost €1,714 ✅ **Can afford cloud migration**

---

#### Scenario 7: 10,000 Total Registrations (Release 10K)

**User Distribution**:
| Tier | Users | Monthly Revenue |
|------|-------|-----------------|
| Free | 8,800 (88%) | €0 |
| Normal | 1,000 (10%) | €6,000 |
| Premium | 200 (2%) | €2,800 |
| **TOTAL** | **10,000** | **€8,800** |

**Infrastructure**: AWS managed services (Release 10K architecture)

**Financial Analysis**:
| Metric | Value |
|--------|-------|
| Monthly Revenue | €8,800 |
| Infrastructure Cost | €1,714 |
| Stripe Fees | €1,200 × (€8,800 × 0.015 + 0.25) | €432 |
| **Net Profit** | **+€6,654** |
| **Profit Margin** | 75.6% |

**Annual Projection**: €6,654 × 12 = **€79,848/anno**

**Valuation Impact** (at €105,600 ARR):
```
SaaS Multiple: 10× ARR (high-growth SaaS)
Estimated Valuation: €1,056,000 (over €1M!)
```

---

### Conversion Sensitivity Analysis

**Impact of Conversion Rate Changes** (1,000 total users):

**Scenario A: Conservative Conversion (7% Normal, 1% Premium)**:
| Tier | Users | Revenue |
|------|-------|---------|
| Free | 920 (92%) | €0 |
| Normal | 70 (7%) | €420 |
| Premium | 10 (1%) | €140 |
| **TOTAL** | **1,000** | **€560** |

**Profit**: €560 - €367.65 - €67 (Stripe) = **+€125.35** (22% margin)
**Status**: ⚠️ Profitable but low margin (vulnerable to cost increases)

---

**Scenario B: Target Conversion (10% Normal, 2% Premium)** ✅ **Baseline**:
| Tier | Users | Revenue |
|------|-------|---------|
| Free | 880 (88%) | €0 |
| Normal | 100 (10%) | €600 |
| Premium | 20 (2%) | €280 |
| **TOTAL** | **1,000** | **€880** |

**Profit**: €880 - €367.65 - €106 (Stripe) = **+€406.35** (46% margin)
**Status**: ✅ **Healthy margin**, sustainable growth

---

**Scenario C: Optimistic Conversion (15% Normal, 5% Premium)**:
| Tier | Users | Revenue |
|------|-------|---------|
| Free | 800 (80%) | €0 |
| Normal | 150 (15%) | €900 |
| Premium | 50 (5%) | €700 |
| **TOTAL** | **1,000** | **€1,600** |

**Profit**: €1,600 - €367.65 - €192 (Stripe) = **+€1,040.35** (65% margin)
**Status**: 🚀 **Excellent margins**, aggressive growth possible

---

### Revenue per Active User (ARPU) Analysis

**Average Revenue Per User** (including free tier):

| Total Users | Normal (10%) | Premium (2%) | Total Revenue | ARPU |
|-------------|--------------|--------------|---------------|------|
| 100 | 10 × €6 = €60 | 2 × €14 = €28 | €88 | **€0.88** |
| 250 | 25 × €6 = €150 | 5 × €14 = €70 | €220 | **€0.88** |
| 500 | 50 × €6 = €300 | 10 × €14 = €140 | €440 | **€0.88** |
| 1,000 | 100 × €6 = €600 | 20 × €14 = €280 | €880 | **€0.88** |
| 5,000 | 500 × €6 = €3,000 | 100 × €14 = €1,400 | €4,400 | **€0.88** |
| 10,000 | 1,000 × €6 = €6,000 | 200 × €14 = €2,800 | €8,800 | **€0.88** |

**Key Insight**: ARPU remains **constant at €0.88/user** regardless of scale (linear conversion model)

**Industry Comparison**:
- Typical SaaS ARPU: €3-8/user (all users)
- MeepleAI ARPU: €0.88/user (with 88% free tier)
- **Paying Users ARPU**: €880 / 120 paying = **€7.33/user** (competitive!)

---

### Minimum Viable User Base (MVU) by Phase

**Minimum Users Required to Cover Infrastructure Costs**:

#### Beta Phase (€78.85/mese cost):

**Calculation**:
```
Required revenue: €78.85 + Stripe fees
With 10% Normal + 2% Premium conversion:
  X × (0.10 × €6 + 0.02 × €14) = €78.85 + (X × 0.12 × 0.015 + 0.12 × 0.25)
  X × 0.88 = €78.85 + €0.0303X
  0.8497X = €78.85
  X = 92.8 users

Minimum: 93 total registrations
  - Free: 82 users
  - Normal: 9 users (9 × €6 = €54)
  - Premium: 2 users (2 × €14 = €28)
  - Revenue: €82 - Stripe €9.84 = €72.16 ≈ €78.85 ✅
```

**MVU for Beta**: **93 total users** (just 9 Normal + 2 Premium paying)

---

#### Release 1K Phase (€367.65/mese cost):

**Calculation**:
```
Required revenue: €367.65 + Stripe fees
X × 0.88 = €367.65 + stripe
X = 418 users (approximate)

Breakdown:
  - Free: 368 users
  - Normal: 42 users (42 × €6 = €252)
  - Premium: 8 users (8 × €14 = €112)
  - Revenue: €364 - Stripe €43.68 = €320.32 (needs adjustment)

Actual MVU: ~430 total users
  - Normal: 43 (€258)
  - Premium: 9 (€126)
  - Revenue: €384 - Stripe €46 = €338 ≈ €367.65 ✅
```

**MVU for Release 1K**: **430 total users** (43 Normal + 9 Premium)

---

#### Release 10K Phase (€1,714/mese cost):

**Calculation**:
```
X × 0.88 = €1,714 + stripe
X ≈ 1,950 users

Breakdown:
  - Free: 1,716 users
  - Normal: 195 users (195 × €6 = €1,170)
  - Premium: 39 users (39 × €14 = €546)
  - Revenue: €1,716 - Stripe €206 = €1,510 (short)

Actual MVU: ~2,100 total users
  - Normal: 210 (€1,260)
  - Premium: 42 (€588)
  - Revenue: €1,848 - Stripe €222 = €1,626 ≈ €1,714 ✅
```

**MVU for Release 10K**: **2,100 total users** (210 Normal + 42 Premium)

---

### Revenue Growth Trajectory Simulation

**Realistic Growth Path** (based on MVU analysis):

| Month | Total Users | Paying Users | Monthly Revenue | Infrastructure Cost | Net Profit | Phase |
|-------|-------------|--------------|-----------------|---------------------|------------|-------|
| **Month 0-3** (Alpha) | 10 | 0 (free alpha) | €0 | €19.30 | -€19.30 | Alpha |
| **Month 4** | 50 | 6 (5 Normal, 1 Premium) | €44 | €19.30 | +€24.70 | Alpha |
| **Month 6** | 100 | 12 (10 Normal, 2 Premium) | €88 | €78.85 | +€9.15 | **Migrate to Beta** |
| **Month 9** | 250 | 30 (25 Normal, 5 Premium) | €220 | €78.85 | +€141.15 | Beta |
| **Month 12** | 500 | 60 (50 Normal, 10 Premium) | €440 | €78.85 | +€361.15 | Beta |
| **Month 15** | 750 | 90 (75 Normal, 15 Premium) | €660 | €367.65 | +€292.35 | **Migrate to 1K** |
| **Month 18** | 1,500 | 180 (150 Normal, 30 Premium) | €1,320 | €367.65 | +€952.35 | Release 1K |
| **Month 24** | 5,000 | 600 (500 Normal, 100 Premium) | €4,400 | €367.65 | +€4,032.35 | Release 1K |
| **Month 30** | 10,000 | 1,200 (1K Normal, 200 Premium) | €8,800 | €1,714 | +€7,086 | **Migrate to 10K** |

**Cumulative Profit** (30 months):
```
Total Revenue: €88 + €220 + €440 + €660 + €1,320 + €4,400 + €8,800 = €15,928
Total Costs: €19.30×3 + €78.85×9 + €367.65×12 + €1,714×6 = €15,629.55
Net Profit (30 months): €15,928 - €15,629.55 = +€298.45

Status: ✅ Self-funded growth possible (no external funding needed!)
```

---

### Tier Migration Patterns

**Free → Normal Conversion Triggers**:

| Trigger Event | Conversion Probability | Implementation |
|---------------|----------------------|----------------|
| Hit PDF upload limit (5/week) | 15-20% | Upsell prompt: "Unlock 25 PDFs/week for €6/mese" |
| Hit query limit (50/day) | 10-15% | Soft limit: "Upgrade for unlimited queries" |
| Active 30+ days | 8-12% | Email campaign: "You're a power user! Try Normal free for 7 days" |
| Invited by Premium user | 20-25% | Referral discount: "Friend invited you → 50% off first month" |

**Expected Impact**: +3-5% overall conversion (10% → 13-15%)

---

**Normal → Premium Conversion Triggers**:

| Trigger Event | Conversion Probability | Implementation |
|---------------|----------------------|----------------|
| Upload >20 PDFs/mese | 25-30% | Upsell: "Unlock unlimited uploads for €14/mese" |
| Use advanced analytics 10+ times | 15-20% | Feature gate: "Export analytics available in Premium" |
| Active 90+ days | 10-15% | Loyalty upgrade: "You're a valued user! Premium upgrade for €10 (30% off)" |
| Game club/group detected | 40-50% | Targeted offer: "Manage group sessions with Premium" |

**Expected Impact**: +1-2% overall Premium adoption (2% → 3-4%)

---

### Optimized Conversion Scenario

**With Conversion Improvement** (1,000 users):

**Baseline** (10% Normal, 2% Premium):
- Revenue: €880/mese
- Paying users: 120 (12%)

**Optimized** (15% Normal, 4% Premium):
| Tier | Users | Monthly Revenue |
|------|-------|-----------------|
| Free | 810 (81%) | €0 |
| Normal | 150 (15%) | €900 |
| Premium | 40 (4%) | €560 |
| **TOTAL** | **1,000** | **€1,460** |

**Financial Analysis**:
| Metric | Baseline | Optimized | Delta |
|--------|----------|-----------|-------|
| Monthly Revenue | €880 | €1,460 | +€580 (+66%) |
| Infrastructure Cost | €367.65 | €367.65 | €0 |
| Stripe Fees | €106 | €175 | +€69 |
| **Net Profit** | €406.35 | €917.35 | **+€511 (+126%)** |

**ROI of Conversion Optimization**:
- Investment: 40h engineering (onboarding flow, email campaigns, analytics)
- Cost: 40h × €50/h = €2,000 one-time
- Monthly gain: €511
- **Payback period**: €2,000 / €511 = **3.9 months** ✅

---

### Pricing Tier Comparison Matrix

**Feature Availability by Tier**:

| Feature | Free | Normal (€6) | Premium (€14) |
|---------|------|-------------|---------------|
| PDF uploads/settimana | 5 | 25 | ♾️ Unlimited |
| RAG queries/giorno | 50 | 200 | ♾️ Unlimited |
| Game sessions attive | 2 | 10 | ♾️ Unlimited |
| Support | Forum | Email 48h | Email 24h + Priority |
| Analytics dashboard | ❌ | ✅ Basic | ✅ Advanced + Export |
| API access | ❌ | ❌ | ✅ 10,000 calls/mese |
| Custom AI fine-tuning | ❌ | ❌ | ✅ (future) |
| Team collaboration | ❌ | ❌ | ✅ Up to 5 members |
| White-label mode | ❌ | ❌ | ✅ (future) |

---

### Revenue Projections Summary Table

**Complete Revenue Model** (all scenarios):

| Total Users | Free Users | Normal Users | Premium Users | Monthly Revenue | Infrastructure | Stripe | **Net Profit** | **Margin** |
|-------------|------------|--------------|---------------|-----------------|----------------|--------|---------------|-----------|
| 100 | 88 | 10 | 2 | €88 | €78.85 | €10.56 | -€1.41 | -1.6% |
| 250 | 220 | 25 | 5 | €220 | €78.85 | €26.40 | +€114.75 | 52.2% |
| 500 | 440 | 50 | 10 | €440 | €78.85 | €52.80 | +€308.35 | 70.1% |
| **1,000** | **880** | **100** | **20** | **€880** | **€367.65** | **€105.60** | **+€406.75** | **46.2%** |
| 2,500 | 2,200 | 250 | 50 | €2,200 | €367.65 | €264 | +€1,568.35 | 71.3% |
| 5,000 | 4,400 | 500 | 100 | €4,400 | €367.65 | €528 | +€3,504.35 | 79.6% |
| **10,000** | **8,800** | **1,000** | **200** | **€8,800** | **€1,714** | **€1,056** | **+€6,030** | **68.5%** |

**Key Insights**:
1. **Break-even**: ~95 total users (Beta phase)
2. **Healthy margins**: >250 users (50%+ margin)
3. **Scaling trigger**: 500 users = revenue covers next tier infrastructure
4. **Optimal scale**: 1,000-5,000 users = 70-80% margins (sweet spot)

---

### Pricing Elasticity Simulation

**Impact of Price Changes** (1,000 users, 10% Normal conversion baseline):

**Normal Tier Price Sensitivity**:
| Normal Price | Conversion Rate | Normal Users | Normal Revenue | Total Revenue | Net Profit |
|--------------|----------------|--------------|----------------|---------------|------------|
| €4 | 13% (+30%) | 130 | €520 | €800 | +€326.75 (-20%) |
| €5 | 11.5% (+15%) | 115 | €575 | €855 | +€381.75 (-6%) |
| **€6** | **10%** (baseline) | **100** | **€600** | **€880** | **€406.75** |
| €7 | 8.5% (-15%) | 85 | €595 | €875 | +€401.75 (-1%) |
| €8 | 7% (-30%) | 70 | €560 | €840 | +€366.75 (-10%) |

**Optimal Price Point**: **€6/mese** (maximizes profit at current conversion)

---

**Premium Tier Price Sensitivity**:
| Premium Price | Conversion Rate | Premium Users | Premium Revenue | Total Revenue | Net Profit |
|---------------|----------------|---------------|-----------------|---------------|------------|
| €10 | 3% (+50%) | 30 | €300 | €900 | +€426.75 (+5%) |
| €12 | 2.5% (+25%) | 25 | €300 | €900 | +€426.75 (+5%) |
| **€14** | **2%** (baseline) | **20** | **€280** | **€880** | **€406.75** |
| €16 | 1.5% (-25%) | 15 | €240 | €840 | +€366.75 (-10%) |
| €20 | 1% (-50%) | 10 | €200 | €800 | +€326.75 (-20%) |

**Optimal Price Point**: **€10-12/mese** (higher conversion + similar revenue)

---

### Recommended Tiered Pricing Strategy

**Optimized Pricing** (based on elasticity analysis):

| Tier | Price | Features | Target Conversion |
|------|-------|----------|------------------|
| **Free** | €0 | 5 PDF/week, 50 queries/day | 85% (reduced from 88%) |
| **Normal** | **€5/mese** | 25 PDF/week, 200 queries/day | 12% (increased from 10%) |
| **Premium** | **€12/mese** | Unlimited, API access, priority support | 3% (increased from 2%) |

**Revenue Impact** (1,000 users):
```
Free: 850 users × €0 = €0
Normal: 120 users × €5 = €600
Premium: 30 users × €12 = €360
Total Revenue: €960/mese

vs Baseline (€880): +€80/mese (+9% revenue increase)
vs Infrastructure (€367.65): +€592.35 profit (+46% margin improvement)
```

**Recommendation**: ✅ **Lower Normal to €5, increase Premium adoption with better features**

---

### Annual Billing Discount Model

**Encourage Annual Subscriptions** (reduce Stripe fees + cash flow boost):

**Pricing**:
| Tier | Monthly Billing | Annual Billing | Discount | Annual Price |
|------|----------------|----------------|----------|--------------|
| Normal | €6/mese | €60/anno | 17% off | €50/anno (€4.17/mese equivalent) |
| Premium | €14/mese | €140/anno | 17% off | €115/anno (€9.58/mese equivalent) |

**Revenue Impact** (1,000 users, 50% choose annual):

**Monthly Billing** (50% of paying users):
- Normal: 50 × €6 = €300/mese
- Premium: 10 × €14 = €140/mese
- Subtotal: €440/mese

**Annual Billing** (50% of paying users):
- Normal: 50 × €50 = €2,500 upfront / 12 = €208.33/mese equivalent
- Premium: 10 × €115 = €1,150 upfront / 12 = €95.83/mese equivalent
- Subtotal: €304.16/mese equivalent

**Total Monthly Equivalent**: €744.16/mese

**Stripe Savings**:
- Monthly billing fees: €440 × 12 × (1.5% + €0.25) = €79.20 + €1,320 = €1,399.20/anno
- Annual billing fees: €3,650 × (1.5% + €0.25) = €54.75 + €60 = €114.75/anno
- **Savings**: €1,399.20 - €114.75 = **€1,284.45/anno** (-92% transaction fees!)

**Cash Flow Benefit**: €3,650 upfront (instead of €5,280 over 12 months)

**Recommendation**: ✅ **Heavily promote annual billing** (massive fee savings)

---

### Tier Upgrade Path Optimization

**Conversion Funnel**:

```
1,000 Total Registrations
    │
    ├─► 880 (88%) stay Free
    │       │
    │       └─► Conversion campaigns (email, in-app prompts)
    │           ├─► 30 upgrade to Normal (Month 3-6)
    │           └─► 10 upgrade to Premium (Month 6-12)
    │
    ├─► 100 (10%) start with Normal
    │       │
    │       └─► Upsell to Premium
    │           └─► 15 upgrade to Premium (Month 2-4)
    │
    └─► 20 (2%) start with Premium
            └─► Retention focus (keep 95%+)
```

**Total Paying After Optimization**: 120 + 40 (upgrades) = **160 users** (16% vs 12% baseline)

**Revenue**: 125 Normal × €6 + 35 Premium × €14 = €750 + €490 = **€1,240/mese**

**vs Baseline**: €1,240 - €880 = **+€360/mese** (+41% revenue increase)

---

## Staging Environment Cost Analysis

### Principle: Staging = Production Replica

A staging environment replicates production infrastructure for pre-release testing. The cost impact depends on which production phase you're replicating and your optimization strategy.

### Staging Cost by Production Phase

| Production Phase | Prod Cost/month | Full Staging | Reduced Staging (50%) | **Total with Staging** |
|------------------|-----------------|--------------|----------------------|------------------------|
| **Alpha** (10 users) | €19.30 | €19.30 | €12-14 | €31-38 (+60-100%) |
| **Beta** (100 users) | €78.85 | €78.85 | €40 | €119-158 (+50-100%) |
| **Release 1K** | €367.65 | €367.65 | €183.83 | €551-735 (+50-100%) |
| **Release 10K** | €1,714 | €1,714 | €857 | €2,571-3,428 (+50-100%) |

### Staging Optimization Strategies

#### Strategy 1: Reduced Staging (Recommended for Alpha/Beta)

Staging uses 50-70% of production resources with reduced redundancy:

```yaml
# Staging configuration - reduced resources
staging_resources:
  postgres:
    production: 2GB
    staging: 1GB           # -€7.50/month savings
  qdrant:
    production: 4GB
    staging: 512MB         # -€5/month savings
  redis:
    production: 768MB
    staging: 256MB         # -€2/month savings

# Total savings: 30-40% vs full replica
# Alpha staging cost: ~€12-14/month (vs €19.30 full)
# Beta staging cost: ~€40/month (vs €78.85 full)
```

#### Strategy 2: On-Demand Staging (Maximum Savings)

Start staging only when needed, stop after validation:

```bash
# Start staging 4-8 hours before deployment
docker compose --profile staging up -d

# Run UAT tests and validation
./scripts/run-uat-tests.sh

# Stop after validation complete
docker compose --profile staging down
```

**Cost Formula:**
```
On-Demand Cost = Full Staging Cost × (Active Hours / 730)

Example (Beta, 50 hours/month active):
€78.85 × (50/730) = €5.40/month
```

**Typical Usage Patterns:**

| Usage Pattern | Hours/month | Beta Staging Cost |
|---------------|-------------|-------------------|
| Weekly deploys (8h each) | 32 | €3.46 |
| Bi-weekly releases (8h each) | 16 | €1.73 |
| Major releases only | 8-16 | €0.86-1.73 |
| Always-on | 730 | €78.85 |

#### Strategy 3: Synthetic Data Staging (No Storage Duplication)

Use seed data instead of production data copy:

| Component | Production | Staging | Savings |
|-----------|------------|---------|---------|
| Storage (1K users) | 40GB | 2GB (seed) | €6.80/month |
| Database size | 1GB | 50MB | Minimal I/O costs |
| Vector DB | 390MB | 10MB | Reduced memory needs |

**Trade-off:** Cannot test with production-scale data patterns.

### Total Infrastructure Costs (Dev + Staging + Prod)

#### Scenario: Beta Phase Target

| Environment | Cost/month | Configuration |
|-------------|-----------|---------------|
| **Development** | €0 | Local Docker Compose |
| **Staging (reduced)** | €40 | 50% resources |
| **Production** | €78.85 | Full Beta infrastructure |
| **TOTAL** | **€118.85** | Within €200 budget ✅ |

**Budget Analysis:**
- Budget target: €200/month
- Total with staging: €118.85
- **Buffer remaining: €81.15 (41% margin)**

#### Scenario: Release 1K Phase

| Environment | Cost/month | Configuration |
|-------------|-----------|---------------|
| **Development** | €0 | Local Docker Compose |
| **Staging (reduced)** | €183.83 | 50% resources |
| **Production** | €367.65 | Full Release 1K infrastructure |
| **TOTAL** | **€551.48** | Requires revenue stream |

**Revenue Requirement:**
- Monthly cost: €551.48
- At €0.88 ARPU: Need 627 users minimum
- At target 1,000 users: €880 revenue → €328.52 profit ✅

### When Staging is Worth the Cost

| Phase | Staging Recommended | Rationale |
|-------|---------------------|-----------|
| **Alpha** (10 users) | ❌ No | Test on prod with rollback capability |
| **Beta** (100 users) | ⚠️ Optional | Consider on-demand for major releases |
| **Release 1K** | ✅ Yes (reduced) | Downtime risk outweighs cost |
| **Release 10K** | ✅ Yes (full) | Business-critical, must mirror prod |

### Recommended Staging Strategy by Phase

#### Alpha Phase (€200 budget)
- **Skip dedicated staging**
- Use feature flags + canary releases on production
- Local docker-compose for pre-deployment testing
- **Cost impact: €0 additional**

#### Beta Phase (€200 budget)
- **On-demand staging** for major releases
- Spin up 4-8 hours before deploy, shut down after
- **Cost impact: €3-10/month** (depending on release frequency)

#### Release 1K (Revenue-backed)
- **Reduced staging** (50% resources)
- Always-on for continuous UAT and QA
- **Cost impact: €183.83/month**
- **ROI:** Prevents downtime that costs more in lost users

#### Release 10K (Enterprise)
- **Full staging mirror**
- Include load testing capabilities
- **Cost impact: €857-1,714/month**
- **ROI:** Required for SLA compliance and enterprise clients

### Staging Cost Formula

```
Staging_Cost = Prod_Cost × Reduction_Factor × (Active_Hours / 730)

Where:
- Reduction_Factor: 0.5-1.0 (resource reduction percentage)
- Active_Hours: 730 (always-on) or less (on-demand)

Examples:

1. Beta, always-on, 50% reduced:
   €78.85 × 0.5 × 1 = €39.43/month

2. Beta, on-demand (50h/month), full resources:
   €78.85 × 1.0 × (50/730) = €5.40/month

3. Release 1K, always-on, 50% reduced:
   €367.65 × 0.5 × 1 = €183.83/month
```

### Implementation: Docker Compose Staging Profile

Create staging profile in `docker-compose.yml`:

```yaml
# Staging profile with reduced resources
services:
  postgres:
    profiles: ["dev", "staging", "prod"]
    deploy:
      resources:
        limits:
          memory: ${POSTGRES_MEMORY:-2G}  # Override for staging

  # Staging-specific overrides
  postgres-staging:
    profiles: ["staging"]
    extends:
      service: postgres
    deploy:
      resources:
        limits:
          memory: 1G  # 50% of production
```

**Usage:**
```bash
# Start staging environment
docker compose --profile staging up -d

# Start production environment
docker compose --profile prod up -d
```

### Cost Monitoring for Staging

Track staging costs separately:

```yaml
# Hetzner/AWS tagging for cost allocation
tags:
  environment: staging
  project: meepleai
  cost-center: infrastructure

# Monthly review checklist
staging_cost_review:
  - [ ] Check staging uptime hours
  - [ ] Compare actual vs budgeted staging cost
  - [ ] Evaluate on-demand vs always-on ROI
  - [ ] Adjust resources based on usage patterns
```

---

## GitHub Repository & CI/CD Cost Analysis

### GitHub Plans Comparison

**Repository Hosting Costs** (Private Repositories):

| Plan | Monthly Cost | Actions Minutes | Packages Storage | Best For |
|------|-------------|-----------------|------------------|----------|
| **Free** | €0 | 2,000/month | 500MB | Solo developers, early projects |
| **Team** | €4/user/month | 3,000/month | 2GB | Small teams (3-10 members) |
| **Enterprise** | €21/user/month | 50,000/month | 50GB | Large organizations, compliance needs |

**Key Features**:
- **Private Repositories**: Unlimited on all plans
- **Public Repository Actions**: Always free (unlimited minutes)
- **Private Repository Actions**: Counted against plan quota

---

### MeepleAI CI/CD Workflow Analysis

**Current Workflows** (analyzed from `.github/workflows/`):

| Workflow | Trigger | Estimated Duration | Monthly Runs (estimate) |
|----------|---------|-------------------|------------------------|
| **CI** (ci.yml) | Push/PR to main, main-dev, frontend-dev | 15-20 min | 200-400 |
| **E2E Tests** (e2e-tests.yml) | Push/PR with code changes | 25-35 min | 100-200 |
| **Security Scan** (security.yml) | Push/PR + Weekly schedule | 10-15 min | 60-100 |
| **K6 Performance** (k6-performance.yml) | Nightly + Manual | 30-45 min | 30-60 |
| **Security Penetration** (security-penetration-tests.yml) | Weekly + Security PRs | 8-10 min | 10-20 |
| **Branch Policy** (branch-policy.yml) | PR events | 1-2 min | 200-400 |
| **Dependabot Automerge** (dependabot-automerge.yml) | Dependabot PRs | 2-5 min | 20-40 |

**Workflow Complexity**:
- **CI Job**: Path filtering (frontend/backend/infra), parallel jobs, services (Postgres, Redis, Qdrant)
- **E2E Tests**: 4-shard parallelization, backend services, Playwright browsers
- **Security**: CodeQL analysis (C#, JavaScript), dependency scanning, Semgrep secrets detection

---

### CI Minutes Consumption Estimate

**Alpha/Beta Phase** (1-3 developers, moderate activity):

| Workflow | Minutes/Run | Monthly Runs | Total Minutes |
|----------|-------------|--------------|---------------|
| CI (frontend) | 12 | 150 | 1,800 |
| CI (backend) | 18 | 100 | 1,800 |
| E2E Tests (4 shards) | 30 | 80 | 2,400 |
| Security Scan | 12 | 50 | 600 |
| K6 Performance | 40 | 30 | 1,200 |
| Security Penetration | 10 | 10 | 100 |
| Misc (branch, deps) | 3 | 200 | 600 |
| **TOTAL** | - | ~620 | **~8,500 min/month** |

**OS Multipliers** (GitHub billing):
- Linux: 1× (standard)
- Windows: 2× (not used in current workflows)
- macOS: 10× (not used in current workflows)

**Effective Minutes** (all Linux): **~8,500 min/month**

---

### Cost Projection by Phase

#### Alpha Phase (1-2 developers)

| Item | Quantity | Unit Cost | Monthly Cost |
|------|----------|-----------|--------------|
| **GitHub Plan** | Free | €0 | €0 |
| **Actions Minutes** | 2,000 included | €0 | €0 |
| **Overage** | ~4,000 min × €0.006 | €0.006/min | **€24** |
| **TOTAL** | - | - | **€24/month** |

**Alternative - GitHub Team Plan**:
| Item | Quantity | Unit Cost | Monthly Cost |
|------|----------|-----------|--------------|
| **GitHub Team** | 2 users | €4/user | €8 |
| **Actions Minutes** | 3,000 included | €0 | €0 |
| **Overage** | ~5,500 min × €0.006 | €0.006/min | €33 |
| **TOTAL** | - | - | **€41/month** |

**Recommendation (Alpha)**: ✅ **Stay on Free plan** - €24/month overage is less than Team plan cost

---

#### Beta Phase (3-5 developers, increased activity)

**Estimated Activity**:
- PRs per month: 100-150
- CI runs: 400-600
- Total minutes: 15,000-20,000

| Item | Quantity | Unit Cost | Monthly Cost |
|------|----------|-----------|--------------|
| **GitHub Team** | 4 users | €4/user | €16 |
| **Actions Minutes** | 3,000 included | €0 | €0 |
| **Overage** | ~15,000 min × €0.006 | €0.006/min | €90 |
| **TOTAL** | - | - | **€106/month** |

**With Optimization** (path filtering, caching already in place):
- Skip unchanged paths: -30% runs
- Optimized minutes: ~10,500
- Overage cost: €45
- **Optimized Total**: **€61/month**

---

#### Release 1K Phase (5-8 developers)

| Item | Quantity | Unit Cost | Monthly Cost |
|------|----------|-----------|--------------|
| **GitHub Team** | 7 users | €4/user | €28 |
| **Actions Minutes** | 3,000 included | €0 | €0 |
| **Overage** | ~25,000 min × €0.006 | €0.006/min | €150 |
| **TOTAL** | - | - | **€178/month** |

---

#### Release 10K Phase (10-15 developers)

| Item | Quantity | Unit Cost | Monthly Cost |
|------|----------|-----------|--------------|
| **GitHub Enterprise** | 12 users | €21/user | €252 |
| **Actions Minutes** | 50,000 included | €0 | €0 |
| **Overage** | ~10,000 min × €0.006 | €0.006/min | €60 |
| **TOTAL** | - | - | **€312/month** |

**Alternative - Team Plan**:
| Item | Quantity | Unit Cost | Monthly Cost |
|------|----------|-----------|--------------|
| **GitHub Team** | 12 users | €4/user | €48 |
| **Actions Minutes** | 3,000 included | €0 | €0 |
| **Overage** | ~57,000 min × €0.006 | €0.006/min | €342 |
| **TOTAL** | - | - | **€390/month** |

**Recommendation (Release 10K)**: ✅ **Enterprise plan** saves €78/month + compliance features

---

### CI Optimization Strategies

#### Quick Wins (Already Implemented)

1. **Path Filtering** (`dorny/paths-filter`):
   - Skip frontend jobs when only backend changes
   - Skip backend jobs when only frontend changes
   - **Savings**: ~30% workflow runs skipped

2. **Concurrency Groups**:
   - Cancel in-progress runs for same PR/branch
   - **Savings**: ~15% duplicate runs avoided

3. **Dependency Caching**:
   - pnpm, NuGet, Playwright browsers cached
   - **Savings**: 3-5 minutes per run

#### Medium-Term Optimizations

4. **E2E Sharding Optimization**:
   - Currently: 4 shards (all run in parallel)
   - Optimization: Dynamic sharding based on test changes
   - **Potential savings**: 20-30% E2E minutes

5. **Conditional Security Scans**:
   - Run CodeQL only on code changes (not docs/infra)
   - **Potential savings**: 10-15% security workflow minutes

6. **Self-Hosted Runners** (Release 1K+):
   - Hetzner dedicated runners: €15-30/month
   - Unlimited minutes on self-hosted
   - **Break-even**: >5,000 minutes/month overage

---

### Self-Hosted Runners Analysis

**When Self-Hosted Makes Sense**:

| Scenario | GitHub-Hosted | Self-Hosted (Hetzner) | Savings |
|----------|---------------|----------------------|---------|
| 5,000 min/month overage | €30 | €15 (CPX11) | €15/month |
| 15,000 min/month overage | €90 | €30 (CPX21) | €60/month |
| 30,000 min/month overage | €180 | €45 (CPX31) | €135/month |

**Self-Hosted Runner Setup** (Hetzner CPX21):
- VPS: €7.15/month (3 vCPU, 8GB RAM)
- Docker + GitHub Actions Runner
- Maintenance: 1-2h/month

**Note**: GitHub announced (Dec 2025) plans to charge €0.002/min for self-hosted runners starting March 2026, but this was **postponed for re-evaluation** after community feedback.

---

### GitHub Actions Pricing Changes (2026)

**Effective January 1, 2026**:
- Linux minutes: €0.006/min (previously €0.008, **-25% reduction**)
- Windows minutes: €0.010/min (reduced from €0.016, **-37.5%**)
- macOS minutes: €0.080/min (unchanged)

**Impact on MeepleAI**: ✅ **Costs will decrease** ~25% for Linux-only workflows

**Self-Hosted Runner Charge** (Postponed):
- Originally planned: €0.002/min from March 2026
- Status: **Postponed indefinitely** after community feedback
- Recommendation: Monitor GitHub announcements

---

### Total GitHub Costs by Phase

| Phase | Plan Cost | Actions Overage | **GitHub Total** | Infrastructure | **Grand Total** |
|-------|-----------|-----------------|------------------|----------------|-----------------|
| **Alpha** | €0 (Free) | €24 | **€24** | €19.30 | **€43.30** |
| **Beta** | €16 (Team) | €45 | **€61** | €78.85 | **€139.85** |
| **Release 1K** | €28 (Team) | €150 | **€178** | €367.65 | **€545.65** |
| **Release 10K** | €252 (Enterprise) | €60 | **€312** | €1,714 | **€2,026** |

**Budget Impact Analysis**:

| Phase | Budget Target | Infra Only | With GitHub CI | Status |
|-------|---------------|------------|----------------|--------|
| Alpha | €50-200 | €19.30 | €43.30 | ✅ Within budget |
| Beta | €50-200 | €78.85 | €139.85 | ✅ Within budget |
| Release 1K | Revenue-backed | €367.65 | €545.65 | ⚠️ Requires revenue |
| Release 10K | Series A | €1,714 | €2,026 | ❌ Requires funding |

---

### Recommendations

1. **Alpha Phase**:
   - Stay on **GitHub Free** plan
   - Accept €24/month Actions overage
   - Total: €43.30/month (within €200 budget)

2. **Beta Phase**:
   - Upgrade to **GitHub Team** when team reaches 3+ members
   - Optimize workflows (already well-optimized)
   - Consider self-hosted runner at €15/month if overage exceeds €50/month
   - Total: €61-139.85/month

3. **Release Phases**:
   - Evaluate **GitHub Enterprise** for compliance and advanced security
   - Implement **self-hosted runners** cluster for cost control
   - Total: €178-312/month depending on scale

---

**Next Steps**:
1. Review [Domain Setup Guide](./domain-setup-guide.md) for domain acquisition
2. Consult [Email & TOTP Services Guide](./email-totp-services.md) for communication setup
3. Follow [Infrastructure Sizing Guide](./infrastructure-sizing.md) for VPS provisioning

