# 🗺️ MeepleAI Roadmap

**Ultimo Aggiornamento**: 2026-01-30
**Priorità Massima**: 🤖 AI Agent System - Chat UI
**Status**: EPIC 0-1 Backend ✅ | EPIC 2 Frontend 50% | EPIC 3 Testing ⏳

---

## 📋 Indice

1. [Overview & Status](#-overview--status)
2. [🎯 Priorità Massima: AI Agent System](#-priorit%C3%A0-massima-sistema-ai-agent-5-settimane)
3. [🎮 Priorità Alta: Game Session Toolkit](#-priorit%C3%A0-alta-game-session-toolkit)
4. [📊 Testing & Quality](#-testing--quality-parallelo)
5. [🎨 Design System](#-design-system--component-library)
6. [📚 Personal Library](#-personal-library--user-features)
7. [🏗️ Infrastructure](#%EF%B8%8F-infrastructure--devops)
8. [🔧 Bug Fixes](#-bug-fixes--refactoring)
9. [📅 Timeline & Milestone](#-timeline-ottimizzato-10-settimane)
10. [🚨 Blockers](#-blockers--dipendenze)

---

## 📊 Overview & Status

**Issue Totali**: 56 aperte (10 completate oggi)
**Epic Attive**: 7
**Milestone Target**: Agent Chat MVP (Week 5)

**Progress Epic**:
- ✅ Epic #3174 EPIC 0-1: Backend Completo (7/7 issues)
- 🟡 Epic #3174 EPIC 2: Frontend 50% (2/4 issues)
- ⏳ Epic #3174 EPIC 3: Testing Pending (0/3 issues)
- 🟡 Epic #3167 GST: Backend 100%, Frontend Pending (3/7 done)

**Next Critical Actions**:
1. 🔴 AGT-013: Agent Chat Sidebar (SSE Streaming) ⭐⭐⭐
2. 🟡 AGT-014: Agent State Management (Zustand)
3. 🟡 GST-004/005: Toolkit Frontend Integration

---

## 🎯 Priorità Massima: Sistema AI Agent (5 settimane)

### Epic #3174: AI Agent System - RAG Integration with Chat UI

**Timeline**: Week 1-5 (Backend Week 1-3 ✅ | Frontend Week 3-4 🟡 | Testing Week 5 ⏳)
**Progress**: 10/18 issues completate (56%)

#### ✅ EPIC 0-1: Foundation & Typology (Week 1-3) - COMPLETATO

**EPIC 0 - RAG Validation** (2/2 ✅):
- [x] #3172 RAG-001: PDF Processing E2E Validation
- [x] #3173 RAG-002: Agent Endpoints Smoke Test

**EPIC 1 Backend** (4/4 ✅):
- [x] #3175 AGT-001: AgentTypology Domain Model & Migration
- [x] #3176 AGT-002: Typology CRUD Commands (Admin)
- [x] #3177 AGT-003: Editor Proposal Commands
- [x] #3178 AGT-004: Typology Query Handlers

**EPIC 1 Frontend** (4/4 ✅):
- [x] #3179 AGT-005: Admin Typologies List Page
- [x] #3180 AGT-006: Create/Edit Typology Form
- [x] #3181 AGT-007: Typology Approval Queue
- [x] #3182 AGT-008: Editor Proposal Form & Test Sandbox

#### 🟡 EPIC 2: Session-Based Agent & Chat UI (Week 3-4) - 50% Complete

**Backend** (3/3 ✅):
- [x] #3183 AGT-009: AgentSession Entity & Migration
- [x] #3184 AGT-010: Session Commands (Launch, Chat, UpdateState)
- [x] #3189 AGT-015: GST Integration - State Sync

**Frontend** (2/4 🟡):
- [x] #3185 AGT-011: Game Card 'Ask Agent' Button ⭐
- [x] #3186 AGT-012: Agent Configuration Modal ⭐
- [ ] #3187 AGT-013: Agent Chat Sidebar (SSE Streaming) ⭐⭐⭐ **CRITICAL**
- [ ] #3188 AGT-014: Agent State Management (Zustand Store)

**Blocker**: GST SSE (#3162) prerequisito per AGT-013

#### ⏳ EPIC 3: Testing & QA (Week 5) - Pending

- [ ] #3190 AGT-016: Frontend Components Tests
- [ ] #3191 AGT-017: E2E Test Flows (4 Scenarios)
- [ ] #3192 AGT-018: RAG Quality Validation (20 Questions)

**Success Metrics**: >90% accuracy | <5s latency | <3% hallucination | >85% coverage

---

## 🎮 Priorità Alta: Game Session Toolkit

### Epic #3167: Game Session Toolkit - Collaborative Scorekeeper

**Progress**: 3/7 issues (43% - Backend Complete ✅)
**Dipendenze**: Bloccante per AGT-013/AGT-014 (Agent Chat UI)

**Backend** (3/3 ✅):
- [x] #3160 GST-001: Bounded Context & Database Schema
- [x] #3161 GST-002: CQRS Commands & Queries
- [x] #3162 GST-003: Real-Time SSE Infrastructure

**Frontend** (0/4 ⏳):
- [ ] #3163 GST-004: Generic Toolkit Routes & Integration
- [ ] #3164 GST-005: Game-Specific Toolkit Integration
- [ ] #3165 GST-006: Session History & UserLibrary Integration
- [ ] #3166 GST-007: MVP Testing & Quality Assurance

---

## 📊 Testing & Quality (Parallelo)

### Epic #3005: Test Coverage Improvement
**Target**: Backend 85%→90% | Frontend 69%→85%

- [ ] #3025 Backend Coverage (+5% - edge cases, error handling)
- [ ] #3026 Frontend Coverage (+16% - components, hooks, utilities)
- [ ] #3082 E2E Test Flows (50 scenarios)
- [ ] #2852 Visual Regression (Chromatic setup)

---

## 🎨 Design System & Component Library

### Epic #2965: Dual-Theme Design System (Glass Light + Dark)

- [ ] #2924 Storybook Setup & Foundation
- [ ] #2925 Extract Reusable Components
- [ ] #2926 Design System Documentation

**Duplicati da chiudere**: #2930, #2931

---

## 📚 Personal Library & User Features

- [ ] #2866 Library Page (Search & Filters)
- [ ] #2867 Game Cards (Grid/List Views)
- [ ] #3120 Private Games & Catalog Proposal System

---

## 🏗️ Infrastructure & DevOps

### Epic #2967: Zero-Cost CI/CD (Oracle Cloud)
**Week 1-2 Setup**:
- [ ] #2968 VM Provisioning
- [ ] #2969 Runner Installation
- [ ] #2970 Workflow Migration
- [ ] #2972 Performance Monitoring
- [ ] #2973 Cost Validation

**Optional**: #2974 Prometheus/Grafana | #2975 Troubleshooting Docs | #2976 Maintenance Automation

**Storage & Performance**:
- [ ] #2703 S3-compatible object storage
- [ ] #2927 Lighthouse CI
- [ ] #2928 k6 Load Testing

**Duplicato da chiudere**: #2971

---

## 🔧 Bug Fixes & Refactoring

- [ ] #3095 fix(dashboard): Incorrect /giochi route
- [ ] #3096 refactor(tests): data-testid pattern (i18n)
- [ ] #2929 Accessibility Audit (WCAG 2.1 AA)

---

## 📋 Gap Analysis & CQRS

### Epic #3066: CQRS Compliance
- [ ] #3073 [P2] Feature Flags Backend
- [ ] #3074 [P3] AI Token Usage Tracking
- [ ] #3075 [P1] Session Quota UI
- [ ] #3080 [P3] AI Usage Dashboard

---

## 📅 Timeline Ottimizzato (10 settimane)

### ✅ Week 1-3: Foundation (COMPLETATO)
**Achievement**: 10 issues done | EPIC 0-1 Backend ✅ | EPIC 1 Frontend ✅ | EPIC 2 Backend ✅

### 🟡 Week 3-4: Core Agent Development (IN CORSO)
**Focus**: Chat UI + State Management
- Week 3: Agent Typology Frontend ✅ + Agent Session Backend ✅
- Week 4: **Chat Sidebar & Agent UI** (#3187-#3188) ⭐⭐⭐ **CRITICAL**

### ⏳ Week 5: Testing & Quality
**Focus**: E2E Tests + RAG Quality Validation (#3190-#3192)

### ⏳ Week 6-8: Parallel Development
**Focus**: Component Library + Infrastructure
- Week 6: Storybook (#2924) + Library Pages (#2866, #2867)
- Week 7: Design System (#2925, #2926) + Oracle Cloud (#2968-#2970)
- Week 8: Coverage Improvement (#3025, #3026) + CI/CD (#2972, #2973)

### ⏳ Week 9-10: Polish & Launch
**Focus**: Bug Fixes + Accessibility
- Week 9: E2E (#3082) + Bug Fixes (#3095, #3096)
- Week 10: Accessibility (#2929) + Final QA

---

## 🎯 Milestone Chiave

### M1: Agent Chat MVP (Week 5) ⭐⭐⭐
**Deliverable**: RAG pipeline ✅ | 2 typologies ✅ | Chat UI + SSE | Admin UI ✅ | >90% coverage
**Valore**: Giocatori chattano con AI per regole, setup, strategie

### M2: Game Session Toolkit (Week 4)
**Deliverable**: Backend CQRS ✅ | SSE infra ✅ | Frontend toolkit | Session history
**Valore**: Collaborative scorekeeper + agent state sync foundation

### M3: Design System (Week 8)
**Deliverable**: Storybook 20+ components | Glass Light+Dark themes | Design tokens docs
**Valore**: UI development velocity + consistency

### M4: Zero-Cost CI/CD (Week 8)
**Deliverable**: Oracle Cloud runner | Workflows migrati | $0 cost | Monitoring
**Valore**: GitHub Actions costs eliminated, faster builds

---

## 📊 Metriche di Successo

**Agent System**: >90% accuracy | <5s latency | <3% hallucination | >90% BE + >85% FE coverage

**Testing**: Backend 85%→90% | Frontend 69%→85% | E2E 0→50 flows

**Infrastructure**: CI/CD $120/mo→$0 | Build time -20% | Uptime >99.5%

---

## 🚨 Blockers & Dipendenze

### Critical Path (Agent System)
```
✅ EPIC 0-1 Backend (RAG + Typology) - DONE
  ↓
✅ EPIC 1 Frontend (Typology UI) - DONE
  ↓
✅ EPIC 2 Backend (Session + GST Sync) - DONE
  ↓
🔴 EPIC 2 Frontend (Chat UI) - BLOCKED by GST SSE (#3162)
  ↓
⏳ EPIC 3 Testing (E2E + QA) - Week 5
```

**Current Blocker**: GST SSE Infrastructure (#3162) prerequisito per AGT-013 Chat Sidebar

**Dipendenze**:
- GameCard (#2867) → AGT-011 ✅
- game_sessions table (GST-001 #3160) → AGT-009 ✅

**Duplicati da chiudere**: #2930, #2931, #2971

---

## 💡 Note Strategiche

**Perché Agent System è Priorità #1?**
1. High User Value: differenziatore competitivo
2. 90% Infrastructure Ready: Qdrant, embedding, reranker operativi
3. Clear Scope: 18 issues, 5 settimane
4. Reusable Framework: base per Strategy Helper, Tournament Assistant

**Parallelizzazione**:
- Week 2-3: Agent Backend + GST Backend (paralleli)
- Week 6-10: Component Library + Infrastructure + Testing (paralleli)

**Risk Mitigation**:
- RAG Validation First ✅
- E2E Tests Early (parallel setup)
- Incremental Delivery (typology → session → chat)

---

## 📖 Risorse & Documentazione

**Agent System**: `docs/prd/ai-agent-system-mvp.md` | `ai-agent-epic-breakdown.md` | `ai-agent-summary.md` | `ai-agent-visual-roadmap.md`

**Workflows**: Git Parallel Development `docs/workflows/git-parallel-development.md`

**Project Docs**: Architecture `docs/01-architecture/` | Development `docs/02-development/` | Testing `docs/05-testing/`

---

## 🎯 Next Actions (Priority Order)

1. **🔴 CRITICAL**: AGT-013 Agent Chat Sidebar (SSE Streaming) ⭐⭐⭐
2. **🟡 HIGH**: AGT-014 Agent State Management (Zustand Store)
3. **🟡 HIGH**: GST Frontend (#3163-#3164) - Toolkit UI Development

---

## 🎉 Recent Progress (2026-01-30)

**10 Issues Completate** 🚀:
- EPIC 0: RAG Prerequisites 2/2 ✅
- EPIC 1 Backend: Typology 4/4 ✅
- EPIC 1 Frontend: Typology UI 4/4 ✅
- EPIC 2 Backend: Session 3/3 ✅ (AGT-009, AGT-010, AGT-015)
- EPIC 2 Frontend: 2/4 (AGT-011 Button, AGT-012 Modal)

**Current Focus**: Chat Sidebar UI (blocked by GST SSE #3162)
