# Project Prioritization Decision - Board Game AI Priority

**Status**: Approved (Option A)
**Decision Date**: 2025-01-15
**Effective Date**: Immediate
**Decision Maker**: Product & Engineering Leadership

---

## Executive Decision: Board Game AI is Strategic Priority

### ✅ APPROVED: Option A - Board Game AI First, FASE Deferred

**Rationale**:
1. **Market Window**: Italian board game market completely unserved, first-mover advantage time-sensitive
2. **Strategic Value**: New revenue stream (€120K ARR potential Phase 3) vs internal tooling
3. **Academic Opportunity**: IEEE CoG 2026 paper deadline (June 2026) requires Phase 2-3 completion
4. **Competitive Risk**: If delayed, competitors (BoardGameAssistant.ai) could launch Italian version
5. **Resource Efficiency**: Consolidated approach (ASP.NET Core) = faster development (3-4 months vs 6)

---

## Impact on Open Issues

### 🔴 DEFERRED: FASE 1-4 Admin Console Project (#890-922)

**Total Issues Deferred**: 33 issues across 4 phases

**New Timeline**: Post-Board Game AI Phase 2 (July 2026+)

**Affected Issues**:
- **FASE 1: Infrastructure Monitoring** (#890-902) - 13 issues
  - Original Deadline: 11/25/2025 → **NEW: Aug 2026**
  - Rationale: Existing Prometheus + Grafana sufficient for Board Game AI Phase 1-2

- **FASE 2: Infrastructure Monitoring** (#890-902 continuation)
  - Original Deadline: 12/09/2025 → **NEW: Sep 2026**

- **FASE 3: Enhanced Management** (#903-914) - 12 issues
  - Original Deadline: 12/23/2025 → **NEW: Oct 2026**
  - Rationale: User management adequate for Phase 1-2 (100-1000 MAU)

- **FASE 4: Advanced Features** (#915-922) - 8 issues
  - Original Deadline: 12/30/2025 → **NEW: Nov 2026**
  - Rationale: Reporting not critical until Phase 3+ scale

**Exceptions (Keep If Critical)**:
- **#891**: InfrastructureMonitoringService - **KEEP IF** needed for Board Game AI multi-model observability
- **#893**: Prometheus client integration - **KEEP IF** LLM validation metrics require new instrumentation

**Communication**:
- [ ] Update issue milestones: FASE-1 → FASE-Deferred (label)
- [ ] Add comment to all FASE issues: "Deferred to post-Board Game AI Phase 2 (strategic priority shift)"
- [ ] Close milestone "FASE 1" (11/25/2025), create new "FASE Deferred" (Aug 2026)

---

### 🟢 ALIGNED: Frontend Foundation Epic (#926)

**Issue #926**: "[FRONTEND-1] Epic: Foundation & Quick Wins (Phase 1)"

**Status**: **KEEP & ALIGN with Board Game AI**

**Scope Adjustment**:
- Original: General design system foundation
- **NEW**: Foundation for Board Game AI Italian UI (Phase 1)
- Deliverables:
  - shadcn/ui installation (#927) - Component library for Q&A interface
  - Design tokens (#928) - Italian-themed colors/typography
  - Dark/light mode (#929) - User preference (optional for Phase 1)

**Timeline**: Sprint 1-2 (Weeks 1-4) parallel with Board Game AI backend work

**Resource**: 1 Frontend Engineer (50% time Sprint 1-2, 100% time Sprint 3-6 for Italian UI)

---

### 🟡 RESCOPED: Frontend Enhancement Epics (#927-935)

**Epics Deferred to Phase 2+**:
- **#930**: Component migration (20-30 components) → **Defer to Phase 2** (Month 7-12)
- **#931**: React 19 optimization → **Defer to Phase 2**
- **#932**: Advanced features → **Defer to Phase 3** (Month 13-18)
- **#933**: App Router migration → **Defer to Phase 3**
- **#934**: Design polish → **Defer to Phase 3**
- **#935**: Performance & Accessibility → **Partial in Phase 1** (WCAG AA for Italian UI), full in Phase 3

**Rationale**: Focus frontend effort on Board Game AI Italian UI (Q&A interface, mobile responsive, citation viewer) instead of general enhancements.

---

### 🟠 CLARIFICATION REQUIRED: DDD & AI Agents (#925, #937, #940)

**Issue #925**: "[DDD] AI Agents Architecture Decision"
- **Status**: CLARIFY SCOPE within 48 hours
- **Questions**:
  - What AI agents? (Board Game AI related? Different feature?)
  - Timeline urgency? (Can defer to Phase 2?)
  - Effort estimate? (2 days vs 2 weeks makes difference)
- **Action**: Engineering Lead provide scope, then prioritize vs defer

**Issue #937**: "[DDD] Complete Remaining 5% - PDF Services + RAG Split"
- **Status**: CLARIFY "5%" within 48 hours
- **Questions**:
  - What's the remaining 5%? (DDD Phase 4 was 98.8% complete per docs)
  - Does it block Board Game AI PDF pipeline? (If yes, prioritize; if no, defer)
- **Action**: Review DDD completion docs, define scope

**Issue #940**: "[DDD] Migrate PdfTextExtractionService to IPdfTextExtractor adapter"
- **Status**: **LIKELY ALIGNED** with Board Game AI
- **Rationale**: Board Game AI needs IPdfTextExtractor interface for 3-stage pipeline (LLMWhisperer, SmolDocling, Docnet)
- **Action**: **KEEP & PRIORITIZE** as Board Game AI dependency (Sprint 1)

---

## Resource Allocation (Board Game AI Phase 1)

### Team Assignment (Months 1-6)

**Backend (2 FTE, 100% allocation)**:
- Engineer 1: PDF processing (LLMWhisperer C# client, SmolDocling integration, orchestrator)
- Engineer 2: Multi-model validation (OpenRouter client, consensus logic, quality framework)

**Frontend (1 FTE, 50% → 100% ramp)**:
- Sprint 1-2 (50%): Foundation (#926 - shadcn/ui, design tokens)
- Sprint 3-6 (100%): Board Game AI Italian UI (Q&A interface, PDF viewer, mobile)

**DevOps (0.5 FTE, as-needed)**:
- SmolDocling Python service deployment (Docker + GPU)
- LLMWhisperer API integration (env vars, secrets)
- Monitoring setup (Prometheus metrics for Board Game AI)

**QA (0.5 FTE, Sprint 5+)**:
- 5-metric testing framework (extend existing PromptEvaluationService)
- Golden dataset annotation (100 Italian Q&A pairs)
- Beta testing coordination (La Tana dei Goblin)

**Total**: 3-4 FTE (vs FASE 1-4 requirement: 4-5 FTE)

---

## Timeline Comparison

### Board Game AI (APPROVED PRIORITY)

```
Jan 2025 ████████████████ Phase 1 Sprint 1-6 (MVP Foundation)
Feb 2025 ████████████████
Mar 2025 ████████████████
Apr 2025 ████████████████
May 2025 ████████████████
Jun 2025 ████████████████ Phase 1 Complete: 100 beta users, 80% accuracy
Jul 2025 ████████████████ Phase 2 Sprint 1-6 (Production Launch)
Aug 2025 ████████████████
Sep 2025 ████████████████
Oct 2025 ████████████████
Nov 2025 ████████████████
Dec 2025 ████████████████ Phase 2 Complete: 1K MAU, 2 publishers, 90% accuracy
```

### FASE 1-4 (DEFERRED TO POST-PHASE 2)

```
Jan 2025 ⏸️⏸️⏸️⏸️⏸️⏸️⏸️⏸️ DEFERRED (Board Game AI priority)
Feb 2025 ⏸️⏸️⏸️⏸️⏸️⏸️⏸️⏸️
Mar 2025 ⏸️⏸️⏸️⏸️⏸️⏸️⏸️⏸️
Apr 2025 ⏸️⏸️⏸️⏸️⏸️⏸️⏸️⏸️
May 2025 ⏸️⏸️⏸️⏸️⏸️⏸️⏸️⏸️
Jun 2025 ⏸️⏸️⏸️⏸️⏸️⏸️⏸️⏸️
Jul 2025 ⏸️⏸️⏸️⏸️⏸️⏸️⏸️⏸️
Aug 2025 ████████████████ FASE 1 Restart (Infrastructure Monitoring)
Sep 2025 ████████████████ FASE 2
Oct 2025 ████████████████ FASE 3 (User Management, Bulk Ops)
Nov 2025 ████████████████ FASE 4 (Reporting, Advanced Alerting)
Dec 2025 ████████████████ FASE Complete
```

**New FASE Timeline**: Aug-Dec 2026 (5 months vs original 2 months, more realistic)

---

## Issue Actions (Immediate)

### Defer FASE Issues (33 issues)

**Command to execute**:
```bash
# Add "deferred" label to all FASE issues
for issue in 890 891 892 893 894 895 896 897 898 899 900 901 902 903 904 905 906 907 908 909 910 911 912 913 914 915 916 917 918 919 920 921 922; do
  gh issue edit $issue --add-label "deferred" --milestone "FASE-Deferred-2026"
done

# Add comment explaining deferral
for issue in 890 891 892 893 894 895 896 897 898 899 900 901 902 903 904 905 906 907 908 909 910 911 912 913 914 915 916 917 918 919 920 921 922; do
  gh issue comment $issue --body "⏸️ **DEFERRED to Aug 2026+**: Strategic priority shift to Board Game AI (Italian market first-mover advantage). FASE features remain important but not time-sensitive. See docs/org/project-prioritization-2025.md for details."
done
```

---

### Align Frontend Foundation (#926)

**Command**:
```bash
gh issue edit 926 --title "[FRONTEND-1] Epic: Foundation for Board Game AI Italian UI (Phase 1)" \
  --add-label "board-game-ai" \
  --milestone "Board-Game-AI-Phase-1"

gh issue comment 926 --body "✅ **ALIGNED with Board Game AI Phase 1**: Rescoped to support Italian UI development. Focus: shadcn/ui (#927), design tokens (#928) for Q&A interface, PDF viewer, mobile responsive layout. Timeline: Sprint 1-2 (Weeks 1-4)."
```

---

### Defer Other Frontend Epics (#927-935)

**Command**:
```bash
for issue in 930 931 932 933 934 935; do
  gh issue edit $issue --add-label "deferred" --milestone "Board-Game-AI-Phase-2+"
  gh issue comment $issue --body "⏸️ **DEFERRED to Phase 2+**: Frontend resources allocated to Board Game AI Italian UI (Phase 1 priority). This epic remains valuable but not time-critical. Revisit after Phase 2 launch (July 2026)."
done
```

---

### Clarify DDD Issues (#925, #937, #940)

**Issue #940**: "[DDD] Migrate PdfTextExtractionService to IPdfTextExtractor adapter"
```bash
gh issue edit 940 --add-label "board-game-ai" --add-label "priority-high" --milestone "Board-Game-AI-Phase-1"
gh issue comment 940 --body "✅ **PRIORITIZED for Board Game AI**: IPdfTextExtractor interface required for 3-stage PDF pipeline (LLMWhisperer → SmolDocling → Docnet.Core). Target: Sprint 1 completion. See docs/architecture/board-game-ai-consolidation-strategy.md"
```

**Issue #925**: "[DDD] AI Agents Architecture Decision"
```bash
gh issue comment 925 --body "❓ **CLARIFICATION NEEDED**: Please provide scope details:\n1. What AI agents are being proposed?\n2. Is this Board Game AI related or separate feature?\n3. Effort estimate (days)?\n4. Timeline urgency (can defer to Phase 2?)?\n\nNeeded within 48 hours to determine if blocks Board Game AI Phase 1 start."
```

**Issue #937**: "[DDD] Complete Remaining 5% - PDF Services + RAG Split"
```bash
gh issue comment 937 --body "❓ **CLARIFICATION NEEDED**: What constitutes the remaining 5%? DDD Phase 4 docs show 98.8% completion (85/86 tests passing). Please specify:\n1. Exact services/components to split\n2. Does this block Board Game AI PDF pipeline integration?\n3. Effort estimate?\n\nIf non-blocking, defer to Phase 2. If blocking, prioritize for Sprint 1."
```

---

## Board Game AI Phase 1 Kick-Off

### New Milestone: "Board-Game-AI-Phase-1" (Jan 2025 - Jun 2025)

**Target**: 100 beta users, 80% accuracy, Italian market validation

**Sprints** (2-week iterations):
- Sprint 1-2 (Jan 15 - Feb 11): PDF processing (LLMWhisperer + SmolDocling)
- Sprint 3-4 (Feb 12 - Mar 11): Multi-model validation (OpenRouter integration)
- Sprint 5-6 (Mar 12 - Apr 8): 5-metric framework + Italian localization
- Sprint 7-8 (Apr 9 - May 6): Frontend (Italian UI, mobile responsive)
- Sprint 9-10 (May 7 - Jun 3): Beta testing + iteration
- Sprint 11-12 (Jun 4 - Jul 1): Phase 1 completion + Phase 2 planning

---

## Resource Allocation Matrix

| Resource | Jan-Jun 2025 (Phase 1) | Jul-Dec 2025 (Phase 2) | Jan-Jun 2026 (Phase 3) | Jul-Dec 2026 (FASE) |
|----------|----------------------|----------------------|----------------------|---------------------|
| **Backend Engineer 1** | 🎯 Board Game AI (PDF) | 🎯 Board Game AI (Scale) | 🎯 Board Game AI (Quality) | 📊 FASE (if still needed) |
| **Backend Engineer 2** | 🎯 Board Game AI (LLM) | 🎯 Board Game AI (Publishers) | 🎯 Board Game AI (Fine-tune) | 📊 FASE |
| **Frontend Engineer** | 🎯 Board Game AI (Italian UI) | 🎯 Board Game AI (Mobile) | 🎯 Board Game AI (i18n) | 📊 FASE |
| **DevOps (0.5 FTE)** | 🎯 Board Game AI (SmolDocling deploy) | 🎯 Board Game AI (K8s) | 🎯 Board Game AI (Multi-region) | 📊 FASE |
| **QA (0.5 FTE)** | 🎯 Board Game AI (Golden dataset) | 🎯 Board Game AI (Quality gates) | 🎯 Board Game AI (Academic) | 📊 FASE |

**Total Capacity**: 3.5-4 FTE fully dedicated to Board Game AI (Jan 2025 - Jun 2026)

---

## Risk Mitigation for Deferred FASE

### Stakeholder Communication

**If FASE had committed stakeholders**:
1. **Immediate notification** (within 24 hours):
   - Email to stakeholders explaining strategic shift
   - New timeline: Aug 2026+ (19 months delay)
   - Alternative: Prioritize subset if critical pain points

2. **Stakeholder meeting** (within 1 week):
   - Present Board Game AI business case (€120K ARR potential)
   - Discuss FASE critical features (which MUST happen sooner?)
   - Negotiate minimal viable FASE (reduce scope 80%?)

### Minimal FASE (If Absolutely Required)

**If cannot defer entirely**, minimal scope for Q1 2025:
- **Keep**: Infrastructure health checks (#891-893) - 3 issues, 2 weeks effort
- **Defer**: User management, reporting, advanced features (#903-922) - 30 issues to Aug 2026

**Trade-off**: Board Game AI Phase 1 timeline extends +2 weeks (Aug 2025 vs Jun 2025 completion)

---

## Success Metrics (Board Game AI Priority)

### Phase 1 (MVP) - Target: Jun 2025

| Metric | Target | Measurement | Status Tracking |
|--------|--------|-------------|----------------|
| **Beta Users** | 100 recruited | La Tana dei Goblin signup | Weekly updates |
| **Accuracy** | ≥80% | Golden dataset (100 Q&A) | Weekly evaluation |
| **Hallucination Rate** | ≤10% | Adversarial queries (50 synthetic) | Weekly testing |
| **User Satisfaction** | ≥4.0/5.0 | Post-beta survey | After 2-week beta |
| **Uptime** | ≥99% | Prometheus monitoring | Continuous |

### Phase 2 (Production) - Target: Dec 2025

| Metric | Target | Measurement |
|--------|--------|-------------|
| **MAU** | 1,000 | Google Analytics |
| **Premium Conversion** | 50 users (5%) | Stripe subscriptions |
| **Publisher Deals** | 2 signed (Giochi Uniti + 1) | Contracts |
| **Accuracy** | ≥90% | Expanded dataset (500 Q&A) |
| **MRR** | €3,249 | Premium €249 + B2B €3K |

### Phase 3 (Scale) - Target: Jun 2026

| Metric | Target | Measurement |
|--------|--------|-------------|
| **MAU** | 5,000 | Google Analytics |
| **Accuracy** | ≥95% | Comprehensive dataset (1000 Q&A) |
| **Academic** | IEEE CoG paper accepted | Conference notification |
| **MRR** | €9,995 | Premium €2,495 + B2B €7,500 |

**FASE Evaluation**: After Phase 3 completion (Jun 2026), re-assess FASE priority vs other features (international expansion, API platform).

---

## Communication Plan

### Internal Team (Immediate - Today)

**Slack/Discord Announcement**:
```
📢 **Strategic Priority Shift: Board Game AI First**

Decision: Board Game AI Italian market opportunity is our #1 priority starting TODAY.

✅ **APPROVED**: Board Game AI Phase 1 (Jan-Jun 2025)
- Target: 100 beta users, Italian market validation, 80% accuracy
- Team: 3-4 FTE fully dedicated
- Timeline: 6 months to MVP

⏸️ **DEFERRED**: FASE 1-4 Admin Console to Aug 2026+
- Rationale: Internal tooling can wait, market opportunity is time-sensitive
- New timeline: Post-Board Game AI Phase 2 (19 months delay)

📋 **Actions**:
- All FASE issues (#890-922) labeled "deferred", milestone updated
- Frontend Foundation (#926) rescoped for Board Game AI Italian UI
- DDD issues (#925, #937, #940) under clarification (48 hours)

🎯 **Next Steps**:
- Sprint 1 planning: Tomorrow (Jan 16)
- LLMWhisperer trial setup: This week
- La Tana dei Goblin outreach: Next week

Questions? Reply in #product-strategy channel.

See: docs/org/project-prioritization-2025.md for full details.
```

---

### External Stakeholders (Within 1 Week)

**If FASE had external commitments**:

**Email Template**:
```
Subject: Important Update: FASE Admin Console Timeline Change

Dear [Stakeholder],

We're writing to inform you of a strategic timeline adjustment for the FASE Admin Console project.

DECISION:
The FASE project (issues #890-922, originally scheduled Nov-Dec 2025) has been deferred to August 2026 or later.

REASON:
We've identified a high-value market opportunity in the Italian board game AI assistance space (€120K+ ARR potential, completely unserved market). To capture first-mover advantage, we're prioritizing Board Game AI development (6-month intensive effort starting January 2025).

NEW FASE TIMELINE:
- Original: Nov 25, 2025 - Dec 30, 2025 (6 weeks)
- Revised: August 2026+ (19 months delay)
- Scope: Will be reassessed in June 2026 based on priorities

IMPACT ON YOU:
[Customize based on stakeholder role]
- Admin users: Continue using existing admin features (sufficient for current scale)
- Reporting needs: Manual exports remain available
- Monitoring: Existing Prometheus + Grafana dashboards functional

ALTERNATIVE:
If FASE features are business-critical for you, we can discuss:
1. Minimal scope subset (2-3 critical features only, Q1 2025)
2. External contractor augmentation (additional cost)
3. Self-service workarounds (we'll provide support)

Let's schedule a call to discuss: [Calendar link]

We appreciate your understanding as we pursue this strategic opportunity.

Best regards,
[Product Lead]
```

---

## Governance & Decision-Making

### Monthly Check-ins (Board Game AI Progress Reviews)

**What**: 30-min status review with leadership
**When**: Last Friday of each month (Jan 31, Feb 28, Mar 31, Apr 30, May 30, Jun 27)
**Attendees**: CEO, CTO, Product Lead, Engineering Lead
**Agenda**:
1. Sprint outcomes (metrics, deliverables)
2. Blockers & risks
3. Budget burn vs projections
4. Phase 1 completion forecast (on track for Jun 2025?)
5. FASE deferral validation (still correct decision?)

### Go/No-Go Gates

**Gate 1 (End of Sprint 2 - Feb 11)**:
- LLMWhisperer + SmolDocling integration working?
- Quality scores validate 95%+ accuracy feasible?
- Team velocity sustainable (not burning out)?
- **Decision**: Continue Phase 1 OR pivot if tech not viable

**Gate 2 (End of Sprint 6 - Jun 3)**:
- 100 beta users recruited?
- 80% accuracy validated?
- User satisfaction ≥4.0/5.0?
- **Decision**: Proceed to Phase 2 OR iterate Phase 1 another 2-4 weeks

**Gate 3 (End of Phase 2 - Dec 2025)**:
- 1,000 MAU achieved?
- 2 publisher partnerships signed?
- 90% accuracy validated?
- **Decision**: Proceed to Phase 3 OR re-assess FASE urgency

---

## FASE Re-Evaluation Criteria (Jun 2026)

**Before un-deferring FASE, assess**:

1. **Is Board Game AI successful?**
   - 5,000+ MAU achieved? (Phase 3 target)
   - €10K+ MRR? (Revenue validates market)
   - 95% accuracy? (Quality goal met)

2. **Are FASE features still needed?**
   - Did we grow to scale where manual admin insufficient? (1000+ concurrent users?)
   - Do we have new stakeholders requiring advanced reporting?
   - Are current workarounds causing operational pain?

3. **What's higher priority than FASE?**
   - International expansion (French, German markets)?
   - API platform (3rd party integrations)?
   - Advanced Board Game AI features (voice input, board state recognition)?

**If FASE still priority**: Execute Aug-Dec 2026 timeline
**If lower priority**: Defer further or descope to essentials only

---

## Appendix: Issue Categorization

### 🔴 Deferred (FASE 1-4): 33 issues
- #890-902: FASE 1-2 Infrastructure Monitoring
- #903-914: FASE 3 Enhanced Management
- #915-922: FASE 4 Advanced Features

### 🟢 Aligned (Board Game AI): 3+ issues
- #926: Frontend Foundation (rescoped)
- #940: IPdfTextExtractor migration (dependency)
- #889: Performance + Accessibility testing (extends to Board Game AI)

### 🟡 Clarification Needed: 3 issues
- #925: AI Agents Architecture (scope unknown)
- #937: DDD Remaining 5% (scope unclear)
- #936: Infisical POC (low priority spike)

### ⏸️ Deferred (Frontend Enhancements): 6 issues
- #930-935: Component migration, React 19 optimization, advanced features

---

**Document Metadata**:
- **Version**: 1.0
- **Decision**: Option A - Board Game AI Priority
- **Approvers**: CEO, CTO, Product Lead
- **Status**: APPROVED & EXECUTED
- **Communication**: Team notified, stakeholders contacted
- **Next Review**: Monthly (last Friday)
