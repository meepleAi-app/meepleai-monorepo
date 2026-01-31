# AI Agent System - GitHub Issues Created

**Date**: 2026-01-30
**Epic**: #3174
**Total Issues**: 21 (1 epic + 2 validation + 18 development)

---

## ✅ Issues Created Summary

### EPIC Issue
- **#3174**: EPIC: AI Agent System - RAG Integration with Chat UI

### EPIC 0: RAG Prerequisites (Week 1) 🔴
- **#3172**: [RAG-001] PDF Processing E2E Validation (P0 blocker)
- **#3173**: [RAG-002] Agent Endpoints Smoke Test (P0 blocker)

### EPIC 1: Agent Typology Management (Week 2-3) 🟠

**Backend (4 issues)**:
- **#3175**: AGT-001: AgentTypology Domain Model & Migration (1d)
- **#3176**: AGT-002: Typology CRUD Commands (Admin) (1.5d)
- **#3177**: AGT-003: Editor Proposal Commands (1d)
- **#3178**: AGT-004: Typology Query Handlers (0.5d)

**Frontend (4 issues)**:
- **#3179**: AGT-005: Admin Typologies List Page (1d)
- **#3180**: AGT-006: Create/Edit Typology Form (1.5d)
- **#3181**: AGT-007: Typology Approval Queue (1d)
- **#3182**: AGT-008: Editor Proposal Form & Test Sandbox (1.5d)

### EPIC 2: Session-Based Agent (Week 3-4) 🟠

**Backend (3 issues)**:
- **#3183**: AGT-009: AgentSession Entity & Migration (1d)
- **#3184**: AGT-010: Session Agent Commands (Launch, Chat SSE) (2d)
- **#3189**: AGT-015: GST Integration - Agent State Sync (1d)

**Frontend (4 issues)**:
- **#3185**: AGT-011: Game Card "Ask Agent" Button (0.5d)
- **#3186**: AGT-012: Agent Configuration Modal (1d)
- **#3187**: AGT-013: Agent Chat Sidebar Component (SSE) (2d)
- **#3188**: AGT-014: Agent State Management (Zustand) (0.5d)

### EPIC 3: Testing & Quality (Week 5) 🔴

**Testing (3 issues)**:
- **#3190**: AGT-016: Frontend Agent Components Tests (1d)
- **#3191**: AGT-017: Agent E2E Test Flows (4 scenarios) (2d)
- **#3192**: AGT-018: RAG Quality Validation (20 questions) (1d)

---

## 📊 Issue Statistics

| Category | Count | Estimate | Priority |
|----------|-------|----------|----------|
| **Validation (EPIC 0)** | 2 | 1.5d | P0 |
| **Backend** | 7 | 9d | P1-P2 |
| **Frontend** | 8 | 8d | P0-P2 |
| **Testing** | 3 | 4d | P0-P2 |
| **TOTAL** | **21** | **22.5d** | - |

---

## 🔗 Dependencies Chain

```
EPIC 0 (Blockers):
#3172 (RAG-001) ──┐
                  ├──> [BLOCKS ALL DEVELOPMENT]
#3173 (RAG-002) ──┘

EPIC 1 (Backend):
#3175 (AGT-001) ──> #3176 (AGT-002) ──> #3179 (AGT-005)
                └─> #3177 (AGT-003) ──> #3182 (AGT-008)
                └─> #3178 (AGT-004) ──> #3179, #3180, #3186

EPIC 1 (Frontend):
#3178 (AGT-004) ──> #3179 (AGT-005) ──> #3180 (AGT-006) ──> #3181 (AGT-007)
#3177 (AGT-003) ──> #3182 (AGT-008)

EPIC 2 (Backend):
GST #3160 ──> #3183 (AGT-009) ──> #3184 (AGT-010) ──> #3189 (AGT-015)
                                                      └──> #3191 (AGT-017)

EPIC 2 (Frontend):
#2867 ──> #3185 (AGT-011) ──> #3186 (AGT-012) ──> #3187 (AGT-013) ──> #3188 (AGT-014)
                                                                      └──> #3190 (AGT-016)

EPIC 3 (Testing):
#3187 + #3184 ──> #3190 (AGT-016) ──┐
                                     ├──> #3191 (AGT-017) ──> #3192 (AGT-018)
                  #3172 (RAG-001) ──┘
```

---

## 🗓️ Weekly Schedule

### Week 1: Validation (CRITICAL)
**Goal**: GO/NO-GO decision
- Mon-Tue: #3172 (RAG-001) - PDF processing validation
- Tue-Wed: #3173 (RAG-002) - Endpoint smoke tests
- Thu: GO/NO-GO decision meeting
- Fri: Fix blockers OR start EPIC 1

**Deliverable**: Infrastructure validated ✅

---

### Week 2: EPIC 1 Sprint 1 (Backend + Admin UI)

**Backend Track** (Dev 1):
- Mon-Tue: #3175 (AGT-001) - Domain model
- Wed-Thu: #3176 (AGT-002) - CRUD commands
- Fri: #3178 (AGT-004) - Query handlers

**Backend Track** (Dev 2 - parallel):
- Wed-Thu: #3177 (AGT-003) - Editor commands (after AGT-001 ready)

**Frontend Track** (Dev 3):
- Mon-Tue: #3179 (AGT-005) - List page (after AGT-004 ready Wed)
- Wed-Thu: #3180 (AGT-006) - Create/Edit form
- Fri: #3181 (AGT-007) - Approval queue

**Frontend Track** (Dev 4 - parallel):
- Thu-Fri: #3182 (AGT-008) - Editor proposals UI (after AGT-003/004 ready)

**Deliverable**: Typology management complete ✅

---

### Week 3: EPIC 2 Sprint 1 (Session Backend)

**Backend Track** (Dev 1):
- Mon-Tue: #3183 (AGT-009) - AgentSession entity (after GST #3160)
- Wed-Fri: #3184 (AGT-010) - Session commands + SSE

**Backend Track** (Dev 2):
- Thu-Fri: #3189 (AGT-015) - GST integration (after GST #3162)

**Deliverable**: Session agent backend complete ✅

---

### Week 4: EPIC 2 Sprint 2 (Frontend Chat UI)

**Frontend Track** (Dev 3 + 4):
- Mon: #3185 (AGT-011) - Game card button
- Tue: #3186 (AGT-012) - Config modal
- Wed-Fri: #3187 (AGT-013) - Chat sidebar (2 devs pair)
- Fri: #3188 (AGT-014) - State management

**Deliverable**: Chat UI complete ✅

---

### Week 5: EPIC 3 (Testing)

**QA Track**:
- Mon: #3190 (AGT-016) - Component tests
- Tue-Thu: #3191 (AGT-017) - E2E tests (4 scenarios)
- Fri: #3192 (AGT-018) - Quality validation

**Deliverable**: MVP tested and validated ✅

---

## 🎯 Quick Commands

### View all agent issues
\`\`\`bash
gh issue list --label "epic:agent-system" --state open
\`\`\`

### View by epic
\`\`\`bash
# Validation
gh issue list --label "agent-rag" --state open

# Typology management
gh issue list --label "agent-typology" --state open

# Session agent
gh issue list --label "agent-session" --state open

# Testing
gh issue list --label "agent-testing" --state open
\`\`\`

### Start working on issue
\`\`\`bash
# Backend
git checkout backend-dev && git pull
git checkout -b feature/agt-001-typology-domain

# Frontend
git checkout frontend-dev && git pull
git checkout -b feature/agt-005-admin-typologies-ui
\`\`\`

### Close issue with commit
\`\`\`bash
git commit -m "feat(agent): implement typology domain model (#3175)

- Created AgentTypology and PromptTemplate entities
- Added migration with FK constraints
- Seeded 3 default typologies (Rules, Setup, Ledger)
- Unit tests >90% coverage

Closes #3175"
\`\`\`

---

## 📋 Labels Used

- \`epic:agent-system\` - All agent-related issues
- \`agent-rag\` - RAG prerequisites and validation
- \`agent-typology\` - Typology management features
- \`agent-session\` - Session-based agent state
- \`agent-testing\` - Testing and quality assurance
- \`p0-critical\` - Blockers and critical path
- \`p1-high\` - Core features
- \`p2-medium\` - Important but not blocking
- \`backend\` - Backend development
- \`frontend\` - Frontend development
- \`testing\` - Test-related work
- \`game-session-toolkit\` - Integration with GST epic

---

## ✅ Next Actions

### This Week (Week 1)
1. **Execute #3172**: Upload PDF, validate processing
2. **Execute #3173**: Test all agent endpoints
3. **GO/NO-GO**: Friday decision meeting
4. **If GO**: Assign issues to team for Week 2 kickoff

### Week 2 (If validation passes)
1. **Backend team**: Start #3175, #3177 (parallel)
2. **Frontend team**: Start #3179, #3182 (parallel after dependencies ready)
3. **Daily standups**: Track progress, resolve blockers

---

**Status**: All issues created and labeled ✅
**Next Step**: Execute EPIC 0 validation (Week 1)
**Success Criteria**: 90% accuracy RAG, <5s latency, healthy services
