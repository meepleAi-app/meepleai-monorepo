# Infrastructure Cost Summary

**Version**: 1.2 | **Updated**: 2026-01-20

## Executive Summary

| Phase | Users | Monthly | Annual | Budget | Status |
|-------|-------|---------|--------|--------|--------|
| **Alpha** | 10 | **€19.30** | €231.60 | €50-200 | ✅ 96% under |
| **Beta** | 100 | **€78.85** | €946.20 | €50-200 | ✅ 61% under |
| **Release 1K** | 1,000 | **€367.65** | €4,411.80 | €200 | ⚠️ Revenue needed |
| **Release 10K** | 10,000 | **€1,714** | €20,568 | €200 | ❌ Funding needed |

**Key**: Alpha/Beta sustainable within budget. Release phases require revenue/funding.

---

## Cost Breakdown by Phase

### Alpha (10 Users) - €19.30/mo

| Component | Service | Specs | Monthly |
|-----------|---------|-------|---------|
| VPS | Hetzner CPX31 | 4 vCPU, 16GB, 160GB NVMe | €15.41 |
| Backup | Snapshots | 7-day retention | €3.08 |
| Domain | Cloudflare | meepleai.com | €0.81 |
| Email/SSL/DNS | Free tier | SendGrid, Let's Encrypt | €0 |
| **TOTAL** | - | - | **€19.30** |

**Performance**: <500ms p95, 5-8 concurrent users, 95-97% uptime, 50 RAG queries/h peak

---

### Beta (100 Users) - €78.85/mo

**Architecture**: 2-node (App + DB)

**Node 1 - Application** (€47.98):

| Item | Service | Specs | Cost |
|------|---------|-------|------|
| VPS | Hetzner CCX33 | 8 vCPU AMD, 32GB, 240GB | €44.90 |
| Backup | Snapshots | 7-day | €3.08 |

**Services**: .NET API, Python AI (embedding, reranker), Redis

**Node 2 - Database** (€25.29):

| Item | Service | Specs | Cost |
|------|---------|-------|------|
| VPS | Hetzner CPX31 | 4 vCPU, 16GB, 160GB | €15.41 |
| Storage | Volume | +340GB → 500GB total | €6.80 |
| Backup | Snapshots | 7-day | €3.08 |

**Services**: PostgreSQL, Qdrant

**Communication** (€5.58):

| Item | Service | Volume | Cost |
|------|---------|--------|------|
| Domains | Cloudflare | 2 domains | €1.63 |
| Email | SendGrid Free + AWS SES | 900/mo + overflow | €0.10 |
| 2FA | Self-hosted TOTP + Twilio SMS | 45 TOTP, 5×8 SMS | €1.85 |

**Total**: €73.27 (infra) + €5.58 (comm) = **€78.85** | **Buffer**: €121.15/mo (61%)

**Performance**: <300ms p95, 40-60 concurrent, 99% uptime, 400 RAG queries/h

---

### Release 1K (1,000 Users) - €367.65/mo

**Architecture**: Multi-node cluster + load balancing

| Component | Service | Specs | Qty | Cost |
|-----------|---------|-------|-----|------|
| Load Balancer | Hetzner CPX11 | 2 vCPU, 4GB | 1 | €7.15 |
| API Cluster | Hetzner CCX43 | 16 vCPU, 64GB | 1-2 | €89.90 |
| Python AI | Hetzner CCX33 | 8 vCPU, 32GB | 1 | €44.90 |
| DB Primary | Hetzner CCX33 | 8 vCPU, 32GB | 1 | €44.90 |
| Storage | SSD Volume | 1TB | 1 | €10.20 |
| Redis Cluster | Hetzner CPX21 | 3 vCPU, 8GB | 3 | €31.35 |
| Backup | Snapshots + S3 | All nodes + offsite | - | €15.00 |
| **Infra Subtotal** | - | - | - | **€243.40** |
| Domains | Cloudflare | 3 (.com/.io/typo) | - | €4.55 |
| Email | AWS SES | 8,400/mo (free tier) | - | €0 |
| 2FA | TOTP + Twilio | 360 TOTP, 40×8 SMS | - | €14.80 |
| **Comm Subtotal** | - | - | - | **€19.35** |
| **TOTAL** | - | - | - | **€367.65** |

**Revenue Required**: €367.65 break-even → €0.37/user/mo minimum
**Pricing**: €5-10/user/mo suggested → 92-96% gross margin

---

### Release 10K (10,000 Users) - €1,714/mo

**Architecture**: AWS managed services

| Category | Services | Monthly |
|----------|----------|---------|
| **Compute** | ALB + ECS Fargate (API 4×4vCPU + AI 2×8vCPU) | €590 |
| **Data** | RDS Multi-AZ + EC2 Qdrant + ElastiCache Redis + S3 | €925 |
| **Network** | CloudFront + CloudWatch + Secrets Manager | €145 |
| **Comm** | Domains (5) + AWS SES + Supabase 2FA | €54 |
| **TOTAL** | - | **€1,714** |

**Revenue Required**: €0.17/user/mo break-even
**Pricing**: €10/user/mo → 98.3% gross margin → €1.18M annual profit
**Valuation**: 5-10× ARR = €6-12M

---

## Cost Per User Scaling

| Phase | Cost/User | Margin @ €10 | Users for Break-Even |
|-------|-----------|--------------|---------------------|
| Alpha | €1.93 | 80.7% | 10 |
| Beta | €0.79 | 92.1% | 93 |
| Release 1K | €0.37 | 96.3% | 430 |
| Release 10K | €0.17 | 98.3% | 2,100 |

**Insight**: 91% cost reduction per user from Alpha → Release 10K (economies of scale)

**Industry Comparison**: MeepleAI €0.17/user vs typical SaaS €2-5/user (90-95% advantage)

---

## Optimization Opportunities

### Quick Wins (0-30 days)

| Optimization | Impact | Savings/mo | Implementation |
|--------------|--------|------------|----------------|
| Reserved instances (Beta+) | -15-20% infra | €14.60 | Annual prepay |
| Cloudflare CDN | -40% bandwidth | €0 | 15min setup |
| Device Trust 2FA | -81% SMS | €7.86 | 2-4h backend |

**Total**: €22.46/mo (Beta), €7.86/mo (Release 1K)

### Medium-Term (1-3 months)

| Optimization | Savings/mo | Trade-off |
|--------------|------------|-----------|
| Lazy model loading | €29.49 | +5s latency on cold start |
| Spot instances (GPU) | €37 | Batch processing required |
| Database query caching | €90 deferred | Defer 2nd API node 3-6mo |

**Total**: €156.49/mo

### Long-Term (6-12 months)

| Optimization | Savings/mo | Requirement |
|--------------|------------|-------------|
| AWS Graviton3 | €200 | ARM Docker images |
| Multi-region CDN | €0 (perf gain) | Cloudflare free tier |
| Data deduplication | €10 deferred | SHA-256 check |

**Total**: €210/mo

---

## Tiered Pricing Model

### Tier Definitions

| Tier | Price | PDF/week | Queries/day | Sessions | Support |
|------|-------|----------|-------------|----------|---------|
| **Free** | €0 | 5 | 50 | 2 | Forum |
| **Normal** | €6 | 25 | 200 | 10 | Email 48h |
| **Premium** | €14 | ♾️ | ♾️ | ♾️ | Email 24h + API |

### Conversion Assumptions

| Tier | Industry | Target | Rationale |
|------|----------|--------|-----------|
| Free | 85-95% | 88% | Most stay free (trial, casual) |
| Normal | 5-12% | 10% | Regular users pay for convenience |
| Premium | 1-3% | 2% | Power users, clubs, creators |

### Revenue Projections

| Users | Free | Normal | Premium | Revenue/mo | Infra | Stripe | Profit | Margin |
|-------|------|--------|---------|------------|-------|--------|--------|--------|
| 100 | 88 | 10 (€60) | 2 (€28) | €88 | €78.85 | €11 | -€1.41 | -1.6% |
| 250 | 220 | 25 (€150) | 5 (€70) | €220 | €78.85 | €26 | +€115 | 52.2% |
| 500 | 440 | 50 (€300) | 10 (€140) | €440 | €78.85 | €53 | +€308 | 70.1% |
| **1,000** | **880** | **100 (€600)** | **20 (€280)** | **€880** | **€367.65** | **€106** | **+€407** | **46.2%** |
| 10,000 | 8,800 | 1,000 (€6K) | 200 (€2.8K) | €8,800 | €1,714 | €1,056 | +€6,030 | 68.5% |

**ARPU**: €0.88/user (all tiers) | €7.33/user (paying only)

**Break-Even**: 93 users (Beta), 430 users (Release 1K), 2,100 users (Release 10K)

---

## Scaling Triggers & Migration

### Alpha → Beta (Month 3-6)

**Trigger**: >50 active users OR CPU >70%
**Cost Delta**: +€55.66 (+288%)
**Actions**: Add DB node, upgrade App node, enable SMS 2FA, storage upgrade

### Beta → Release 1K (Month 9-12)

**Trigger**: >80 concurrent OR p95 >500ms
**Cost Delta**: +€288.80 (+366%)
**Actions**: Add load balancer, scale API/DB, Redis cluster, Python node

### Release 1K → 10K (Month 18-24)

**Trigger**: >500 concurrent OR ops >20h/week
**Cost Delta**: +€1,346 (+366%)
**Critical**: Requires Series A OR €100K/mo revenue

---

## Pricing Strategy

### Recommended Tiers (Optimized)

| Tier | Price | Target Conv | Features |
|------|-------|-------------|----------|
| Free | €0 | 85% | 5 PDF/wk, 50 q/day, 2 sessions |
| Normal | **€5** | 12% ↑ | 25 PDF/wk, 200 q/day, analytics |
| Premium | **€12** | 3% ↑ | Unlimited, API, priority |

**Impact** (1,000 users): €960/mo (+9% vs €6/€14 baseline)

### Annual Billing Discount

| Tier | Monthly | Annual | Discount | Effective |
|------|---------|--------|----------|-----------|
| Normal | €6 | €50 | 17% | €4.17/mo |
| Premium | €14 | €115 | 17% | €9.58/mo |

**Stripe Savings**: -92% fees (€1,399 → €115 annually)
**Cash Flow**: €3,650 upfront (50% annual adoption)

---

## GitHub CI/CD Costs

### GitHub Plans

| Plan | Cost/user/mo | Minutes/mo | Storage | Best For |
|------|--------------|------------|---------|----------|
| Free | €0 | 2,000 | 500MB | Solo dev, early |
| Team | €4 | 3,000 | 2GB | 3-10 members |
| Enterprise | €21 | 50,000 | 50GB | Large org |

### Workflow Consumption Estimate

| Phase | Plan | Minutes Used | Overage | Plan Cost | Total GitHub |
|-------|------|--------------|---------|-----------|--------------|
| **Alpha** | Free | 8,500 | 6,500 × €0.006 | €0 | €24 |
| **Beta** | Team (4u) | 15,000 | 12,000 × €0.006 | €16 | €61 |
| **Release 1K** | Team (7u) | 25,000 | 22,000 × €0.006 | €28 | €178 |
| **Release 10K** | Enterprise (12u) | 60,000 | 10,000 × €0.006 | €252 | €312 |

**Workflows**: CI (15min), E2E 4-shard (30min), Security (12min), K6 (40min), Visual (10min)

### Total Costs (Infra + GitHub)

| Phase | Infrastructure | GitHub CI | **Total** | Budget Status |
|-------|---------------|-----------|-----------|---------------|
| Alpha | €19.30 | €24 | **€43.30** | ✅ €156.70 buffer |
| Beta | €78.85 | €61 | **€139.85** | ✅ €60.15 buffer |
| Release 1K | €367.65 | €178 | **€545.65** | ⚠️ Revenue-backed |
| Release 10K | €1,714 | €312 | **€2,026** | ❌ Series A |

**Optimization**: Self-hosted runners @ €15/mo break-even at >5,000min overage

---

## Staging Environment Costs

| Prod Phase | Prod Cost | Full Staging | Reduced (50%) | Total |
|------------|-----------|--------------|---------------|-------|
| Alpha | €19.30 | €19.30 | €12-14 | €31-38 (+60-100%) |
| Beta | €78.85 | €78.85 | €40 | €119-158 (+50-100%) |
| Release 1K | €367.65 | €367.65 | €184 | €551-735 (+50-100%) |

**Strategies**:
1. **On-Demand**: Start only when needed → €5.40/mo (50h usage)
2. **Reduced**: 50% resources, always-on → €40/mo (Beta)
3. **Synthetic Data**: Seed data instead of prod copy → -€6.80/mo storage

**Recommendation by Phase**:
- Alpha: ❌ Skip (test on prod with rollbacks)
- Beta: ⚠️ On-demand (€3-10/mo)
- Release 1K: ✅ Reduced always-on (€184/mo)
- Release 10K: ✅ Full mirror (€857-1,714/mo)

---

## Revenue Modeling

### Minimum Viable Users (MVU)

| Phase | Cost/mo | MVU Total | MVU Normal | MVU Premium | Revenue |
|-------|---------|-----------|------------|-------------|---------|
| Beta | €78.85 | 93 | 9 | 2 | €82 |
| Release 1K | €367.65 | 430 | 43 | 9 | €384 |
| Release 10K | €1,714 | 2,100 | 210 | 42 | €1,848 |

### Growth Trajectory (30 months)

| Month | Users | Paying | Revenue/mo | Cost | Profit | Phase |
|-------|-------|--------|------------|------|--------|-------|
| 0-3 | 10 | 0 | €0 | €19.30 | -€19.30 | Alpha free |
| 6 | 100 | 12 | €88 | €78.85 | +€9 | **→ Beta** |
| 12 | 500 | 60 | €440 | €78.85 | +€361 | Beta |
| 15 | 750 | 90 | €660 | €367.65 | +€292 | **→ Release 1K** |
| 24 | 5,000 | 600 | €4,400 | €367.65 | +€4,032 | Release 1K |
| 30 | 10,000 | 1,200 | €8,800 | €1,714 | +€7,086 | **→ Release 10K** |

**Cumulative Profit (30mo)**: +€298 → ✅ Self-funded growth possible

---

## Conversion Optimization

### Tier Migration Triggers

**Free → Normal**:

| Trigger | Probability | Implementation |
|---------|-------------|----------------|
| Hit PDF limit (5/wk) | 15-20% | Upsell prompt |
| Hit query limit (50/day) | 10-15% | Soft limit banner |
| Active 30+ days | 8-12% | Email campaign |
| Premium referral | 20-25% | 50% off first month |

**Impact**: +3-5% overall conversion (10% → 13-15%)

**Normal → Premium**:

| Trigger | Probability | Implementation |
|---------|-------------|----------------|
| >20 PDFs/mo | 25-30% | Unlimited upsell |
| Analytics 10+ uses | 15-20% | Feature gate export |
| Active 90+ days | 10-15% | Loyalty upgrade |
| Game club detected | 40-50% | Group management |

**Impact**: +1-2% Premium (2% → 3-4%)

### Optimized Scenario (1,000 users)

| Metric | Baseline (10%/2%) | Optimized (15%/4%) | Delta |
|--------|-------------------|-------------------|-------|
| Paying Users | 120 (12%) | 190 (19%) | +70 (+58%) |
| Revenue/mo | €880 | €1,460 | +€580 (+66%) |
| Profit/mo | €407 | €917 | +€511 (+126%) |

**ROI**: 40h eng (€2K) → €511/mo gain → **3.9mo payback**

---

## Pricing Elasticity

### Normal Tier Sensitivity (1,000 users)

| Price | Conv Rate | Users | Revenue | Profit | vs €6 |
|-------|-----------|-------|---------|--------|-------|
| €4 | 13% (+30%) | 130 | €520 | €327 | -20% |
| €5 | 11.5% (+15%) | 115 | €575 | €382 | -6% |
| **€6** | **10%** | **100** | **€600** | **€407** | **baseline** |
| €7 | 8.5% (-15%) | 85 | €595 | €402 | -1% |
| €8 | 7% (-30%) | 70 | €560 | €367 | -10% |

**Optimal**: €6/mo (current baseline)

### Premium Tier Sensitivity

| Price | Conv | Revenue | Profit | vs €14 |
|-------|------|---------|--------|--------|
| €10 | 3% (+50%) | €300 | €427 | +5% |
| €12 | 2.5% (+25%) | €300 | €427 | +5% |
| **€14** | **2%** | **€280** | **€407** | **baseline** |
| €16 | 1.5% (-25%) | €240 | €367 | -10% |

**Optimal**: €10-12/mo (higher conversion, similar revenue)

---

## Budget Gates

### Gate 1: Alpha Launch (Month 0)
- **Budget**: €19.30/mo
- **Funding**: €200-300 upfront
- **Decision**: ✅ Proceed if funding available

### Gate 2: Beta Scale (Month 3-6)
- **Budget**: €78.85/mo
- **Funding**: €1,000 runway
- **Decision**: ✅ Proceed if 50+ users + positive feedback

### Gate 3: Revenue Requirement (Month 9-12)
- **Budget**: €367.65/mo
- **Requirement**: €500-1,000/mo revenue OR seed funding
- **Criteria**: ✅ 200+ paying @ €5 | ⚠️ Seek funding if <100 | ❌ Pause if no revenue/funding

### Gate 4: Series A (Month 18-24)
- **Budget**: €1,714/mo
- **Requirement**: €20K/mo revenue OR €500K-1M Series A
- **Criteria**: ✅ 2,000+ @ €10 | ⚠️ Seek Series A if strong growth | ❌ Pivot if no PMF

---

## Contingency Reserves

| Phase | Monthly Cost | +20% Buffer | Recommended Reserve | Runway |
|-------|--------------|-------------|---------------------|--------|
| Alpha | €19.30 | €23.16 | €100 | 5 months |
| Beta | €78.85 | €94.62 | €500 | 6 months |
| Release 1K | €367.65 | €441.18 | €2,500 | 6 months |
| Release 10K | €1,714 | €2,057 | €15,000 | 6-9 months |

**Purpose**: Traffic spikes, security incidents, provider price increases

---

## Cost Monitoring

### Monthly Review Checklist
- [ ] Export invoices (Hetzner/AWS)
- [ ] Track actual vs budget
- [ ] Identify anomalies (>10% variance)
- [ ] Review utilization (CPU, RAM, storage)
- [ ] Forecast next month
- [ ] Update projections

### Budget Alerts (AWS)
- 50% threshold: Email warning
- 80% threshold: Email + Slack
- 100% threshold: Critical + freeze resources

---

## Next Steps

1. [Domain Setup Guide](./domain-setup-guide.md)
2. [Email & TOTP Services](./email-totp-services.md)
3. [Infrastructure Sizing](./infrastructure-sizing.md)
