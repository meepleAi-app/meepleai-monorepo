# Plan: Multi-User Game Library Workflows

**Epic ID**: Task #1
**Created**: 2026-02-13
**PM Agent**: SuperClaude Framework

---

## Hypothesis

Implementare 3 flussi differenziati per aggiungere giochi alla libreria permette di:
1. **Admin** → Gestire catalogo globale con workflow completo (già esistente)
2. **User** → Aggiungere giochi personali senza complessità admin
3. **Editor** → Proporre giochi per approvazione con quality gates

**Perché questo approccio?**
- Admin wizard esistente è troppo complesso per utenti normali
- Editor necessita workflow approvazione (non publish diretto)
- Separazione ruoli migliora UX e sicurezza

---

## Architecture Overview

### Existing Components (Reuse)
```
/admin/wizard (✅ Completo - 5 step)
├─ PdfUploadStep
├─ GameCreationStep
├─ ChatSetupStep
├─ QAStep
└─ PublishStep

Components Riusabili:
├─ AgentChatSheet
├─ AgentConfigSheet
├─ PdfUploadForm
└─ ImageUpload
```

### New Components (To Build)

#### Phase 1: Quick Wins
```
/library/private
├─ _components/AddGameButton.tsx (Issue #2)

/components/agent/config
├─ TypologySelector.tsx (Issue #3)
└─ StrategySelector.tsx (Issue #3)
```

#### Phase 2: User Wizard
```
/library/private/add
├─ page.tsx
├─ client.tsx
└─ /components/library/user-wizard
    ├─ UserWizardClient.tsx (Issue #4)
    ├─ Step1CreateGame.tsx
    ├─ Step2UploadPdf.tsx (optional)
    └─ Step3ConfigAgent.tsx (optional)

Backend:
POST /api/v1/games/:gameId/agent (Issue #5)
```

#### Phase 3: Editor Workflow
```
/library/propose
├─ page.tsx
├─ client.tsx
└─ /components/library/editor-wizard
    ├─ EditorProposalClient.tsx (Issue #6)
    └─ RequestApprovalStep.tsx (nuovo step 5)

/library/proposals (enhance)
└─ Tracking UI improvements (Issue #7)
```

---

## Expected Outcomes (Quantitative)

### Development Effort
- **Phase 1**: 5 hours (2h + 3h)
- **Phase 2**: 2 giorni (1.5d + 4h)
- **Phase 3**: 1.5 giorni (1d + 3h)
- **Total**: ~4.5 giorni

### Quality Metrics
- Test Coverage: Frontend ≥85%, Backend ≥90%
- E2E Tests: 3 flussi completi
- Code Review: All PRs reviewed before merge

### Success Metrics
```yaml
User Flow Success:
  - User can create game without PDF: 100% functional
  - User can add PDF optionally: 100% functional
  - User can configure agent if PDF exists: 100% functional

Editor Flow Success:
  - Editor can submit proposal: 100% functional
  - Approval workflow functional: 100% functional
  - Status tracking accurate: 100% functional

Technical Quality:
  - No TypeScript errors: strict mode compliance
  - No console warnings in production
  - Performance: Wizard load time <2s
  - Accessibility: WCAG 2.1 AA compliant
```

---

## Risks & Mitigation

### Risk 1: Backend API Missing
**Risk**: GET /api/v1/rag/strategies might not exist
**Impact**: Issue #3 (Selectors) blocked
**Probability**: Medium
**Mitigation**:
- Verify endpoint existence before Phase 1 start
- Create backend endpoint if missing (additional effort: 2h)
- Alternative: Hardcode strategy list initially (technical debt)

### Risk 2: Admin Wizard Reuse Complexity
**Risk**: Admin wizard components tightly coupled, hard to reuse
**Impact**: Phase 2/3 effort increase
**Probability**: Low (components already well-structured)
**Mitigation**:
- Review admin wizard code before Phase 2 start
- Extract shared logic if needed
- Create wrapper components for reuse

### Risk 3: Agent Creation Processing Time
**Risk**: RAG processing might take too long, blocking user flow
**Impact**: UX degradation in wizard
**Probability**: Medium
**Mitigation**:
- Async processing with status polling (already implemented in ChatSetupStep)
- Progress indicator for user feedback
- Timeout handling with retry mechanism

### Risk 4: ShareRequest Approval Workflow
**Risk**: Existing ShareRequest flow might not support new use case
**Impact**: Phase 3 blocked
**Probability**: Low
**Mitigation**:
- Review existing ShareRequest implementation before Phase 3
- Extend if needed (backend command/query)
- Ensure admin approval UI already exists

---

## Technical Dependencies

### Frontend
- Next.js 16 App Router (✅ configured)
- TypeScript strict mode (✅ enforced)
- Tailwind 4 + shadcn/ui (✅ available)
- Vitest + Playwright (✅ test infrastructure)

### Backend
- .NET 9 + CQRS pattern (✅ implemented)
- PostgreSQL + EF Core (✅ configured)
- Agent typologies table (✅ exists)
- ShareRequest system (✅ exists)

### API Verification Needed
- [ ] GET /api/v1/rag/strategies (verify existence)
- [ ] POST /api/v1/games/:gameId/agent (create in Issue #5)

---

## Success Criteria (Epic DoD)

### Functional
- [ ] User può aggiungere gioco a libreria privata (con/senza PDF)
- [ ] Editor può proporre gioco per catalogo condiviso
- [ ] Agent config UI permette scelta typology + strategia RAG
- [ ] Admin wizard remains functional (no regression)

### Quality
- [ ] Test coverage ≥85% (frontend), ≥90% (backend)
- [ ] E2E tests per tutti e 3 flussi (User, Editor, Admin)
- [ ] No TypeScript errors (strict mode)
- [ ] No accessibility violations (WCAG 2.1 AA)

### Documentation
- [ ] Epic and issues documented in docs/pdca/
- [ ] Architecture decisions documented (ADR if needed)
- [ ] User guides updated in docs/
- [ ] API changes documented in docs/03-api/

### Deployment
- [ ] All PRs merged to parent branch
- [ ] Backend migrations applied
- [ ] Feature flags configured (if needed)
- [ ] Monitoring/analytics configured

---

## Execution Plan (3 Phases)

### Phase 1: Quick Wins (Day 1-2)
**Goal**: Unlock agent configuration and add entry point

**Issue #2**: Add Game Button (2h)
- Create AddGameButton component
- Integrate in /library/private
- Link to /library/private/add
- Tests

**Issue #3**: Agent Selectors (3h)
- Create TypologySelector
- Create StrategySelector
- Fetch from API
- Tests

**Phase 1 DoD**:
- [ ] Button visible and functional
- [ ] Selectors fetch and display options
- [ ] Tests passing (≥85% coverage)

---

### Phase 2: User Wizard (Day 3-5)
**Goal**: Complete user flow → libreria privata

**Issue #4**: User Wizard (1.5d)
- State machine 3-step
- Step 1: Create game (PDF optional)
- Step 2: Upload PDF (optional)
- Step 3: Config agent (optional)
- Integration tests
- E2E test

**Issue #5**: Backend API (4h)
- POST /api/v1/games/:gameId/agent
- CQRS command + handler
- Validation
- Unit + integration tests

**Phase 2 DoD**:
- [ ] User wizard functional end-to-end
- [ ] Backend API working
- [ ] E2E test: user-wizard-flow.e2e.test.ts passing
- [ ] Test coverage targets met

---

### Phase 3: Editor Workflow (Day 6-7)
**Goal**: Complete editor proposal flow

**Issue #6**: Editor Wizard (1d)
- Clone admin wizard structure
- Replace PublishStep with RequestApprovalStep
- Create ShareRequest on submit
- E2E test

**Issue #7**: Tracking Enhancement (3h)
- Filters (status, date, search)
- Status badges
- Admin feedback display
- Actions (view, re-submit, delete)

**Phase 3 DoD**:
- [ ] Editor wizard functional
- [ ] ShareRequest created correctly
- [ ] Tracking UI shows proposals
- [ ] E2E test: editor-proposal-flow.e2e.test.ts passing
- [ ] Epic DoD complete

---

## Next Steps

1. **User Review**: Review epic and 6 issues
2. **API Verification**: Check if GET /api/v1/rag/strategies exists
3. **Start Phase 1**: Begin with Issue #2 (Add Game Button)
4. **Parallel**: Issue #3 (Selectors) can start in parallel
5. **Phase Progression**: Complete Phase 1 → Phase 2 → Phase 3

---

## References

- Epic Task: #1
- Admin Wizard: `/apps/web/src/app/(authenticated)/admin/wizard/`
- CLAUDE.md: Project conventions and patterns
- Design System: `docs/design-system/cards.md` (MeepleCard)
