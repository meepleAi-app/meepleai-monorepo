# 🚀 Roadmap Q1 2026 - Next Actions

**Status**: ✅ Roadmap Complete - Ready for Execution
**Created**: 2026-01-13

---

## 📋 Immediate Actions (This Week)

### 1. Create GitHub Sub-Issues (14 issues)
**Priority**: HIGH | **Owner**: Tech Lead

Eseguire i seguenti comandi per creare le sub-issues su GitHub:

#### Sprint 2 Sub-Issues (4 issues)
```bash
# Agent Mode Selector Component
gh issue create \
  --title "[Sub-Issue 2398.1] Agent Mode Selector Component" \
  --label "frontend,component,Sprint 2" \
  --body "**Parent**: #2398

**Description**: React component for agent mode selection (Rules Clarifier, Strategy Advisor, Setup Assistant)

**Tasks**:
- [ ] Create \`AgentModeSelector.tsx\` in \`apps/web/src/components/agent\`
- [ ] Implement UI with radio/tabs for 3 modes
- [ ] Add tooltip descriptions
- [ ] State management with Zustand
- [ ] Storybook stories
- [ ] Tests with Vitest (>90% coverage)
- [ ] Accessibility validation (Lighthouse >95)

**Acceptance Criteria**:
- ✅ 3 modes render correctly
- ✅ Selection updates global state
- ✅ Storybook documented
- ✅ Tests pass

**Dependencies**: None (can start immediately)
**Merge Checkpoint**: Component tests + Storybook validation"

# Mode Configuration Panel
gh issue create \
  --title "[Sub-Issue 2398.2] Mode Configuration Panel UI" \
  --label "frontend,component,Sprint 2" \
  --body "**Parent**: #2398

**Description**: Dynamic configuration panel for agent mode parameters

**Tasks**:
- [ ] Create \`AgentConfigPanel.tsx\`
- [ ] Dynamic form based on selected mode
- [ ] React Hook Form + Zod validation
- [ ] Confidence threshold slider
- [ ] Advanced options toggle
- [ ] Storybook stories
- [ ] Form validation tests

**Acceptance Criteria**:
- ✅ Form adapts to mode dynamically
- ✅ Zod validation prevents invalid values
- ✅ Config saved to local storage
- ✅ Test coverage >85%

**Dependencies**: Sub-Issue 2398.1 (state management)
**Merge Checkpoint**: Form validation tests pass"

# Document Picker Component
gh issue create \
  --title "[Sub-Issue 2399.1] Document Picker Component" \
  --label "frontend,component,Sprint 2" \
  --body "**Parent**: #2399

**Description**: Document selection component for Knowledge Base

**Tasks**:
- [ ] Create \`DocumentPicker.tsx\`
- [ ] List with checkboxes
- [ ] Search/filter functionality
- [ ] Pagination for large lists
- [ ] Hover preview
- [ ] Storybook stories (empty, 1 doc, 100+ docs)
- [ ] Multi-select tests

**Acceptance Criteria**:
- ✅ Multi-select with checkboxes
- ✅ Real-time search filter
- ✅ Pagination handles >100 documents
- ✅ Preview shows metadata
- ✅ Test coverage >90%

**Dependencies**: Backend API (can use mock)
**Merge Checkpoint**: Component tests + API mock integration"

# Selected Documents Display
gh issue create \
  --title "[Sub-Issue 2399.2] Selected Documents Display" \
  --label "frontend,component,Sprint 2" \
  --body "**Parent**: #2399

**Description**: Display selected documents with removal and reordering

**Tasks**:
- [ ] Create \`SelectedDocuments.tsx\`
- [ ] Badge list with removal
- [ ] Drag-and-drop ordering
- [ ] Max documents limit with alert
- [ ] Selection statistics
- [ ] Storybook stories
- [ ] Drag-and-drop tests

**Acceptance Criteria**:
- ✅ Removable badges
- ✅ Drag-and-drop reorder
- ✅ Alert on limit reached
- ✅ Test coverage >85%

**Dependencies**: Sub-Issue 2399.1 (selection state)
**Merge Checkpoint**: Interaction tests pass"
```

#### Sprint 3 Sub-Issues (3 issues)
```bash
# QuickQuestion Generator UI
gh issue create \
  --title "[Sub-Issue 2401.1] QuickQuestion Generator UI Component" \
  --label "frontend,component,Sprint 3,ai" \
  --body "**Parent**: #2401

**Description**: UI for AI-powered QuickQuestion generation

**Tasks**:
- [ ] Create \`QuickQuestionGenerator.tsx\`
- [ ] Prompt textarea input
- [ ] Generate button with loading state
- [ ] AI progress indicator
- [ ] Error handling UI
- [ ] Storybook stories (idle, loading, success, error)
- [ ] Async state tests

**Acceptance Criteria**:
- ✅ Prompt validation (min/max length)
- ✅ Loading spinner on generation
- ✅ Clear error messages
- ✅ Transition to display component
- ✅ Test coverage >90%

**Dependencies**: Backend AI API (can use mock)
**Merge Checkpoint**: Async state tests + API mock"

# AI Loading States
gh issue create \
  --title "[Sub-Issue 2401.2] AI Generation Loading States" \
  --label "frontend,component,Sprint 3" \
  --body "**Parent**: #2401

**Description**: Visual loading states for AI operations

**Tasks**:
- [ ] Create \`AILoadingIndicator.tsx\`
- [ ] Skeleton loader for QuickQuestion
- [ ] Animated progress bar
- [ ] Estimated time remaining
- [ ] Cancellation button
- [ ] Storybook stories (various times)
- [ ] Timeout handling tests

**Acceptance Criteria**:
- ✅ Skeleton matches layout
- ✅ Smooth progress animation
- ✅ Dynamic time estimation
- ✅ Cancellation interrupts API
- ✅ Test coverage >85%

**Dependencies**: Sub-Issue 2401.1 (generator UI)
**Merge Checkpoint**: Loading interaction tests"

# Question Display + Editing
gh issue create \
  --title "[Sub-Issue 2401.3] Generated Question Display + Editing" \
  --label "frontend,component,Sprint 3" \
  --body "**Parent**: #2401

**Description**: Display and edit AI-generated QuickQuestion

**Tasks**:
- [ ] Create \`QuickQuestionEditor.tsx\`
- [ ] AI preview display
- [ ] Inline editing with auto-save
- [ ] Regenerate button with confirmation
- [ ] Save/discard actions
- [ ] Storybook stories (preview, editing, dirty state)
- [ ] Dirty state tests

**Acceptance Criteria**:
- ✅ Preview shows generated question
- ✅ Debounced auto-save
- ✅ Regenerate confirmation if modified
- ✅ Unsaved changes warning
- ✅ Test coverage >90%

**Dependencies**: Sub-Issue 2401.1 (AI response structure)
**Merge Checkpoint**: Editing workflow E2E test"
```

#### Sprint 4 Sub-Issues (3 issues)
```bash
# State Editor Component Library
gh issue create \
  --title "[Sub-Issue 2406.1] State Editor Component Library" \
  --label "frontend,component,Sprint 4,state-management" \
  --body "**Parent**: #2406

**Description**: Base components for game state editing

**Tasks**:
- [ ] Create \`StateEditor.tsx\` container
- [ ] \`PlayerStateEditor.tsx\` (count, names, scores)
- [ ] \`ResourceEditor.tsx\` (tokens, cards, resources)
- [ ] \`BoardStateEditor.tsx\` (grid, piece placement)
- [ ] Validation rules per component
- [ ] Storybook stories
- [ ] Validation logic tests

**Acceptance Criteria**:
- ✅ 3 editor components functional
- ✅ Validation prevents invalid states
- ✅ Storybook documents all use cases
- ✅ Test coverage >90%

**Dependencies**: Backend schema (can use mock types)
**Merge Checkpoint**: Component library validation"

# Player Mode UI Controls
gh issue create \
  --title "[Sub-Issue 2406.2] Player Mode UI Controls" \
  --label "frontend,component,Sprint 4,ai" \
  --body "**Parent**: #2406

**Description**: UI controls for Player Mode AI suggestions

**Tasks**:
- [ ] Create \`PlayerModeControls.tsx\`
- [ ] Suggest Move button with tooltip
- [ ] AI suggestion display panel
- [ ] Confidence meter
- [ ] Apply Move / Ignore actions
- [ ] Storybook stories (no suggestion, low/high confidence)
- [ ] Suggestion workflow tests

**Acceptance Criteria**:
- ✅ Button triggers AI request
- ✅ Clear confidence visualization
- ✅ Apply Move updates state
- ✅ Test coverage >85%

**Dependencies**: Backend AI API (can use mock)
**Merge Checkpoint**: AI suggestion E2E test"

# Ledger Timeline
gh issue create \
  --title "[Sub-Issue 2406.3] Ledger Mode History Timeline" \
  --label "frontend,component,Sprint 4,state-management" \
  --body "**Parent**: #2406

**Description**: Visual timeline for Ledger Mode history

**Tasks**:
- [ ] Create \`LedgerTimeline.tsx\`
- [ ] Vertical timeline with actions
- [ ] Diff visualization per action
- [ ] Rollback button with confirmation
- [ ] Export history (JSON/CSV)
- [ ] Storybook stories (empty, 5 actions, 50+ actions)
- [ ] Rollback logic tests

**Acceptance Criteria**:
- ✅ Timeline shows action history
- ✅ Diff highlights state changes
- ✅ Rollback restores previous state
- ✅ Export generates downloadable file
- ✅ Test coverage >90%

**Dependencies**: Backend history + rollback API
**Merge Checkpoint**: Rollback E2E test"
```

### 2. Setup GitHub Project Board
**Priority**: HIGH | **Owner**: Tech Lead

1. Crea nuovo GitHub Project: "MeepleAI Q1 2026 Roadmap"
2. Configura colonne:
   - **Backlog**: Issue non ancora iniziate
   - **main-dev**: Backend development in corso
   - **frontend-dev**: Frontend development in corso
   - **Integration Testing**: E2E testing
   - **Ready for Main**: Checkpoint passed
   - **Done**: Merged in `main`

3. Aggiungi tutte le issue aperte al board
4. Link parent-child relationships (use GitHub tasklists)

### 3. Branch Protection Rules
**Priority**: HIGH | **Owner**: DevOps

Configurare branch protection su GitHub:

```yaml
# main branch
- Require pull request reviews: 2 approvers
- Require status checks to pass: true
- Required checks:
  - backend-ci (tests + build)
  - frontend-ci (tests + build + e2e)
  - security-scan (semgrep + detect-secrets)
- Require conversation resolution: true
- Do not allow bypassing the above settings

# main-dev branch
- Require pull request reviews: 1 approver
- Require status checks to pass: true
- Required checks:
  - backend-ci
- Allow force pushes: false

# frontend-dev branch
- Require pull request reviews: 1 approver
- Require status checks to pass: true
- Required checks:
  - frontend-ci
- Allow force pushes: false
```

### 4. CI/CD Configuration
**Priority**: MEDIUM | **Owner**: DevOps

Verificare configurazione GitHub Actions:

1. **Backend CI** (`.github/workflows/backend-ci.yml`):
   - Trigger on push to `main-dev` and PRs
   - Run unit tests + integration tests
   - Code coverage report (Codecov)

2. **Frontend CI** (`.github/workflows/frontend-ci.yml`):
   - Trigger on push to `frontend-dev` and PRs
   - Run Vitest + E2E Playwright
   - Storybook build validation

3. **Integration CI** (new):
   - Trigger on merge to integration branches
   - Full E2E test suite
   - Performance benchmarks

---

## 📅 Week 1 Actions (Sprint 2 Kickoff)

### Monday: Sprint Planning
**Priority**: HIGH | **Owner**: Tech Lead + Team

1. **Team Meeting** (1 hour):
   - Present roadmap strategy (parallel development)
   - Explain branch flow (main-dev + frontend-dev → main)
   - Review Sprint 2 goals and deliverables
   - Assign issues to developers

2. **Issue Assignment**:
   - **Backend Dev**: #2397 (LLM Provider Selection)
   - **Frontend Dev 1**: #2398.1 + #2398.2 (Agent Mode UI)
   - **Frontend Dev 2**: #2399.1 + #2399.2 (KB Document Selection)

3. **Setup Development Environment**:
   - Clone repositories
   - Checkout correct branches
   - Verify local build + tests pass

### Tuesday-Thursday: Development
**Priority**: HIGH | **Owner**: Individual Developers

1. **Backend Developer** (`main-dev`):
   - Implement LLM provider abstraction
   - OpenRouter integration
   - Ollama integration
   - Unit tests (>90% coverage)
   - Create API contract for frontend

2. **Frontend Developers** (`frontend-dev`):
   - Component development
   - Storybook stories
   - Unit tests (>85% coverage)
   - Use API mocks for backend dependencies

3. **Daily Standup** (15 min):
   - Progress update
   - Blockers discussion
   - Checkpoint status review

### Friday: Sprint 2 Checkpoint Review
**Priority**: HIGH | **Owner**: Tech Lead

1. **Code Review**:
   - Review PRs to `main-dev`
   - Review PRs to `frontend-dev`
   - Approve if checkpoint criteria met

2. **Integration Testing**:
   - Create integration branch: `integration/sprint-2`
   - Merge `main-dev` + `frontend-dev`
   - Run full E2E test suite
   - Performance benchmarks

3. **Merge to Main** (if all green):
   - Merge `integration/sprint-2` → `main`
   - Tag release: `v0.1.0-sprint2`
   - Deploy to staging environment

4. **Sprint Retrospective** (30 min):
   - What went well?
   - What can improve?
   - Action items for Sprint 3

---

## 📊 Progress Tracking

### Weekly Reports
**Format**: Update `docs/roadmap/weekly-progress.md`

```markdown
# Week 1 Progress (2026-01-13 to 2026-01-19)

## Completed
- ✅ Roadmap documentation created
- ✅ 14 sub-issues created on GitHub
- ✅ GitHub Project Board setup
- ✅ Branch protection rules configured

## In Progress
- 🔄 Sprint 2 Backend: LLM Provider Selection (70% complete)
- 🔄 Sprint 2 Frontend: Agent Mode UI (50% complete)

## Blockers
- None

## Next Week
- Complete Sprint 2 development
- Integration testing
- Merge to main (Checkpoint 1)
```

### Checkpoint Validation Checklist
Before merging to `main`, verify:

- [ ] ✅ **Backend Tests**: Unit tests >90%, integration tests pass
- [ ] ✅ **Frontend Tests**: Component tests >85%, E2E tests pass
- [ ] ✅ **Storybook**: All stories render correctly
- [ ] ✅ **Security Scan**: Zero critical vulnerabilities
- [ ] ✅ **Performance**: API p95 <200ms, frontend TTI <3s
- [ ] ✅ **Code Review**: 2 approvals for main, 1 for dev branches
- [ ] ✅ **Documentation**: README updated, CHANGELOG.md updated

---

## 🎯 Success Metrics Dashboard

### Sprint 2 Targets (Week 2)
- [ ] Backend: LLM Provider abstraction complete
- [ ] Frontend: Agent Mode UI + KB Selection UI complete
- [ ] Tests: >90% backend, >85% frontend coverage
- [ ] Integration: E2E tests pass
- [ ] Merge: Successfully merged to `main`

### Sprint 3 Targets (Week 4)
- [ ] Backend: GameStateTemplate + Rulebook AI complete
- [ ] Frontend: QuickQuestion UI complete
- [ ] Tests: Maintain coverage targets
- [ ] Integration: AI generation E2E tests pass
- [ ] Merge: Successfully merged to `main`

### Sprint 4 Targets (Week 6)
- [ ] Backend: GameSessionState + Player/Ledger modes complete
- [ ] Frontend: State Editor UI complete
- [ ] Tests: Maintain coverage targets
- [ ] Integration: State management E2E tests pass
- [ ] Merge: Successfully merged to `main`

---

## 📞 Communication Channels

### Daily Standup (15 min, 9:00 AM)
- Format: What I did yesterday, what I'll do today, blockers
- Platform: Slack #standup channel or video call

### Weekly Sprint Review (Friday, 4:00 PM)
- Format: Demo completed features, review metrics, plan next sprint
- Platform: Video call + screen sharing

### Ad-hoc Questions
- **Technical**: Slack #dev-questions
- **Roadmap**: Slack #roadmap-q1-2026
- **Urgent**: Direct message Tech Lead

---

## 🚨 Escalation Protocol

If checkpoint fails (tests red, security issues, performance degradation):

1. **Immediate**: Stop merge, keep in dev branch
2. **Triage** (Tech Lead + Developer): Root cause analysis (1 hour max)
3. **Fix**: Address issues in dev branch, re-run checkpoint
4. **Re-validate**: Full checkpoint checklist again
5. **Merge**: Only after all green

---

## 📚 Documentation References

- **Full Roadmap**: [Q1-2026-ROADMAP-EPIC.md](./Q1-2026-ROADMAP-EPIC.md)
- **Executive Summary**: [ROADMAP-EXECUTIVE-SUMMARY.md](./ROADMAP-EXECUTIVE-SUMMARY.md)
- **Sub-Issues Template**: [SUB-ISSUES-TEMPLATE.md](./SUB-ISSUES-TEMPLATE.md)
- **Development Guide**: [../../CLAUDE.md](../../CLAUDE.md)

---

**Questions?** Contact PM Agent or Tech Lead

**Last Updated**: 2026-01-13
