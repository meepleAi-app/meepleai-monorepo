# Enterprise Admin Dashboard - Epics & Issues

> **Created**: 2026-02-05
> **Total Epics**: 4
> **Total Issues**: ~40 (9 per Epic 1 created)
> **Timeline**: 10-12 settimane

---

## Epic Overview

| Epic | Title | Duration | Priority | Dependencies | GitHub |
|------|-------|----------|----------|--------------|--------|
| 1 | Core Dashboard & Infrastructure | 3 weeks | P0 - Critical | None | [#3685](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3685) |
| 2 | User & Content Management | 2 weeks | P1 - High | Epic 1 | [#3686](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3686) |
| 3 | AI Platform | 4 weeks | P1 - High | Epic 1 | [#3687](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3687) |
| 4 | Business & Simulations | 3 weeks | P2 - Medium | Epic 1 + Epic 2 | [#3688](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3688) |

---

## Epic 1: Core Dashboard & Infrastructure

**GitHub**: [#3685](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3685)
**Duration**: 3 settimane
**Status**: 📝 Planning

### Sub-Issues

| # | Issue | Title | Duration | Depends On | Status |
|---|-------|-------|----------|------------|--------|
| 1.1 | [#3689](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3689) | Layout Base & Navigation | 3-4 giorni | - | ⏳ Ready |
| 1.2 | [#3690](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3690) | Admin Role System & Security | 3-4 giorni | #3689 | ⏳ Blocked |
| 1.3 | [#3691](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3691) | Audit Log System | 4-5 giorni | #3689, #3690 | ⏳ Blocked |
| 1.4 | 📝 | Overview Tab - Extended KPIs | 2-3 giorni | #3689 | 📝 To Create |
| 1.5 | [#3692](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3692) | Token Management System | 3-4 giorni | #3689 | ⏳ Blocked |
| 1.6 | 📝 | Resources - DB/Cache/Vectors | 3-4 giorni | #3689 | 📝 To Create |
| 1.7 | 📝 | Operations - Service Control | 3-4 giorni | #3689, #3690 | 📝 To Create |
| 1.8 | [#3693](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3693) | Batch Job System | 4-5 giorni | #3689, #3691 | ⏳ Blocked |
| 1.9 | 📝 | Testing & Integration | 3-4 giorni | All above | 📝 To Create |

### Deliverables

- ✅ Vertical sidebar navigation (7 sezioni)
- ✅ Horizontal tab system
- ✅ Admin role system (SuperAdmin, Admin, Editor)
- ✅ Confirmation modals (Level 1 & 2)
- ✅ Audit log completo
- ✅ Overview tab con 8 KPI
- ✅ Token management dashboard
- ✅ Resource monitoring (DB, Cache, Vectors)
- ✅ Operations panel (service control)
- ✅ Batch job queue system

---

## Epic 2: User & Content Management

**GitHub**: [#3686](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3686)
**Duration**: 2 settimane
**Status**: 📝 Planning
**Depends On**: Epic 1 complete

### Planned Sub-Issues (10 tasks)

| Task | Description | Duration |
|------|-------------|----------|
| 2.1 | User Management Table & Search | 2-3 giorni |
| 2.2 | User Tier Management | 2-3 giorni |
| 2.3 | Token Usage Per User View | 2-3 giorni |
| 2.4 | Block/Unblock Users | 1-2 giorni |
| 2.5 | Impersonate Mode | 3-4 giorni |
| 2.6 | FeatureFlag System | 3-4 giorni |
| 2.7 | UserLimit Configuration | 2-3 giorni |
| 2.8 | Shared Library Bulk Operations | 2-3 giorni |
| 2.9 | Frontend Integration | 2-3 giorni |
| 2.10 | Testing | 2-3 giorni |

---

## Epic 3: AI Platform

**GitHub**: [#3687](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3687)
**Duration**: 4 settimane
**Status**: 📝 Planning
**Depends On**: Epic 1 complete

### Planned Sub-Issues (12 tasks)

| Task | Description | Duration |
|------|-------------|----------|
| 3.1 | AgentDefinition Data Model | 3-4 giorni |
| 3.2 | Agent Builder UI | 3-4 giorni |
| 3.3 | Agent Playground | 4-5 giorni |
| 3.4 | Strategy Editor | 4-5 giorni |
| 3.5 | Visual Pipeline Builder | 5-6 giorni |
| 3.6 | Agent Catalog & Stats | 3-4 giorni |
| 3.7 | Chat Analytics Backend | 3-4 giorni |
| 3.8 | Chat Analytics Frontend | 2-3 giorni |
| 3.9 | PDF Analytics | 3-4 giorni |
| 3.10 | Model Performance Tracking | 2-3 giorni |
| 3.11 | A/B Testing Framework | 3-4 giorni |
| 3.12 | Testing | 4-5 giorni |

---

## Epic 4: Business & Simulations

**GitHub**: [#3688](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3688)
**Duration**: 3 settimane
**Status**: 📝 Planning
**Depends On**: Epic 1 + Epic 2 (partial)

### Planned Sub-Issues (10 tasks)

| Task | Description | Duration |
|------|-------------|----------|
| 4.1 | App Usage Stats Backend | 3-4 giorni |
| 4.2 | App Usage Dashboard | 2-3 giorni |
| 4.3 | LedgerEntry Data Model | 2-3 giorni |
| 4.4 | Automatic Ledger Tracking | 3-4 giorni |
| 4.5 | Manual Ledger CRUD | 2-3 giorni |
| 4.6 | Ledger Dashboard & Visualization | 3-4 giorni |
| 4.7 | Export Ledger (PDF/CSV/Excel) | 2-3 giorni |
| 4.8 | Agent Cost Calculator | 3-4 giorni |
| 4.9 | Resource Forecasting Simulator | 4-5 giorni |
| 4.10 | Testing | 3-4 giorni |

---

## Timeline & Parallelization

### Sequential Timeline (Single Team)
```
Settimana 1-3:   Epic 1 (Core Dashboard)
Settimana 4-5:   Epic 2 (Users)
Settimana 6-9:   Epic 3 (AI Platform)
Settimana 10-12: Epic 4 (Business)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Total: 12 settimane
```

### Parallel Timeline (2-3 Teams)
```
Settimana 1-3:   Epic 1 (tutti focus)
                 ▼
Settimana 4-7:   Epic 2 || Epic 3 (parallel)
                 ▼
Settimana 8-10:  Epic 4 (mentre Epic 3 finisce)
                 ▼
Settimana 11:    Integration & Testing finale
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Total: 10-11 settimane ottimizzate
```

### Critical Path

```
#3689 Layout (W1)
  │
  ├──► #3690 Security (W2) ──┐
  │                           ├──► #3691 Audit (W2) ──► #3693 Batch Jobs (W3)
  ├──► #3692 Tokens (W2) ────┘
  │
  └──► Epic 2 + Epic 3 can start (W4+)
```

---

## Implementation Checklist

### Week 1: Foundation
- [ ] Start #3689 (Layout Base)
- [ ] Design review mockups
- [ ] Setup project structure

### Week 2: Core Systems
- [ ] Complete #3689
- [ ] Start #3690 (Security) + #3692 (Tokens) in parallel
- [ ] Start #3691 (Audit Log)

### Week 3: Resources & Operations
- [ ] Complete #3690, #3691, #3692
- [ ] Start #3693 (Batch Jobs)
- [ ] Overview KPIs, Resources tabs
- [ ] Operations panel

### Week 4-5: User Management (Epic 2)
- [ ] User table & tier management
- [ ] Impersonate mode
- [ ] Feature flags
- [ ] User limits

### Week 6-9: AI Platform (Epic 3)
- [ ] AI Lab (Agent Builder, Playground, Strategy Editor)
- [ ] Agent Catalog
- [ ] Chat & PDF Analytics
- [ ] A/B Testing

### Week 10-12: Business & Simulations (Epic 4)
- [ ] Usage stats
- [ ] Financial ledger
- [ ] Cost calculator
- [ ] Resource forecasting

---

## MVP Milestones

### MVP 1: Core Dashboard (Week 3)
- Overview con 8 KPI
- Resources management
- Operations panel
- Audit log

### MVP 2: Full Management (Week 7)
- + User management
- + Feature flags
- + Impersonate

### MVP 3: AI Platform (Week 9)
- + AI Lab
- + Agent analytics
- + Chat/PDF analytics

### MVP 4: Complete Enterprise (Week 12)
- + Business intelligence
- + Financial ledger
- + Simulations

---

## Resources

- [Specification](./SPECIFICATION.md) - Complete technical spec
- [Mockup Complete](./mockups/admin-dashboard-complete.html) - Interactive mockup
- [Mockup Overview](./mockups/01-overview.html) - Overview section
- [Mockup Resources](./mockups/02-resources.html) - Resources section

---

## Labels Used

- `kind/feature` - Feature implementation
- `area/admin` - Admin dashboard area
- `area/ai` - AI-related features
- `area/db` - Database changes
- `area/security` - Security features
- `backend` / `frontend` - Stack separation
- `priority: high` / `medium` - Priority levels
- `complexity: large` / `medium` - Effort estimation
