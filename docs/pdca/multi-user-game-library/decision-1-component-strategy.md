# Decision #1: Component Reuse Strategy

**Date**: 2026-02-13
**Epic**: Multi-User Game Library Workflows
**Context**: Issue #4 (User Wizard) requires optional PDF flow

---

## Decision

**CHOSEN**: **Option A - Refactor Shared Components** ✅

**Decided By**: User
**Recommended By**: PM Agent
**Rationale**: Maintainability, DRY principles, lower effort

---

## Decision Context

### Problem Statement
Admin wizard components (`ChatSetupStep`, `GameCreationStep`) assume `pdfId: string` (required).

User wizard needs "optional PDF" flow:
- User can create game WITHOUT PDF
- User can skip agent config if no PDF
- Conditional step navigation

### Options Evaluated

#### Option A: Refactor Shared ✅ SELECTED
```yaml
Approach:
  - Make admin components flexible
  - Add optional props (pdfId?: string | null)
  - Conditional rendering based on props
  - Single codebase for both wizards

Effort: +4h refactoring
Total: 24h (Issue #4)

Pros:
  ✅ DRY (Don't Repeat Yourself)
  ✅ Single source of truth
  ✅ Easier long-term maintenance
  ✅ Lower total effort (24h vs 26h)
  ✅ Changes benefit both wizards

Cons:
  ⚠️ Risk: Potential regression in admin wizard

Risk Mitigation:
  - Comprehensive tests before refactor
  - Run existing admin wizard E2E tests
  - Gradual rollout with feature flag
  - Code review before merge
```

#### Option B: Duplicate User (Not Selected)
```yaml
Approach:
  - Create UserChatSetupStep.tsx (separate)
  - Create UserGameCreationStep.tsx (separate)
  - Keep admin wizard unchanged

Effort: +6h duplication
Total: 26h (Issue #4)

Pros:
  ✅ Zero risk to admin wizard
  ✅ User-specific optimizations

Cons:
  ❌ Code duplication (~200 lines duplicated)
  ❌ 2x maintenance burden
  ❌ Higher effort (+2h)
  ❌ Technical debt
  ❌ Fixes need to be applied twice
```

---

## Implementation Plan (Option A)

### Phase A: Preparation (2h)
1. **Analyze Admin Wizard Components** (30 min)
   - Read ChatSetupStep.tsx fully
   - Read GameCreationStep.tsx fully
   - Identify all pdfId dependencies
   - Map conditional logic requirements

2. **Create Test Baseline** (1h)
   - Run existing admin wizard E2E test
   - Document expected behavior
   - Create test cases for optional PDF flow
   - Establish regression test suite

3. **Design Refactor** (30 min)
   - Define new prop interfaces
   - Plan conditional rendering logic
   - Document breaking changes (if any)
   - Review with user if needed

### Phase B: Refactoring (2h)
1. **ChatSetupStep Refactor** (1h)
   ```tsx
   // Before
   interface ChatSetupStepProps {
     pdfId: string;  // Required
   }

   // After
   interface ChatSetupStepProps {
     pdfId?: string | null;  // Optional
   }

   // Add conditional logic
   if (!pdfId) {
     return <NoPdfMessage />;
   }
   ```

2. **GameCreationStep Refactor** (1h)
   ```tsx
   // Add new prop
   interface GameCreationStepProps {
     skipPdfOption?: boolean;  // Show "Skip PDF" button
   }

   // Conditional navigation
   const handleNext = () => {
     if (skipPdfOption) {
       // Show skip button
     }
     onComplete(gameId, gameName);
   };
   ```

### Phase C: Integration (User Wizard - 18h)
Use refactored components in new UserWizardClient.tsx

### Phase D: Validation (2h)
1. **Admin Wizard Regression Tests** (1h)
   - Run full admin wizard E2E
   - Verify no behavior changes
   - Test all 5 steps still work

2. **User Wizard Tests** (1h)
   - Test "skip PDF" path
   - Test "skip agent" path
   - Test full flow

---

## Success Criteria

### Functional ✅
- [ ] Admin wizard continues to work (no regression)
- [ ] User wizard supports optional PDF/agent
- [ ] Conditional navigation works correctly
- [ ] All 3 paths work: full, skip-pdf, skip-agent

### Technical ✅
- [ ] No code duplication
- [ ] Props interfaces backward compatible
- [ ] TypeScript strict mode passing
- [ ] Test coverage ≥85%

### Quality ✅
- [ ] E2E tests pass (admin + user wizards)
- [ ] No breaking changes to public APIs
- [ ] Documentation updated

---

## Risk Assessment

### Identified Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Admin wizard breaks | Low | High | Comprehensive E2E tests |
| Conditional logic bugs | Medium | Medium | State machine diagram |
| TypeScript errors | Low | Low | Incremental compilation |
| Performance regression | Very Low | Low | No DB changes |

### Mitigation Strategies
1. **Test First**: Establish baseline before refactor
2. **Incremental**: Refactor one component at a time
3. **Validation**: Test admin wizard after each change
4. **Rollback Plan**: Git revert if critical issues found

---

## Timeline Impact

### Original Issue #4 Estimate: 20h
### With Refactoring (Option A): 24h

**Breakdown**:
```
Preparation: 2h
Refactoring: 2h
User Wizard: 18h
Validation: 2h
Total: 24h
```

**vs Option B**: 26h (+2h saved)

---

## Implementation Order

### Step-by-Step Execution
```
Day 1 (8h):
├─ Analyze admin components (30min)
├─ Create test baseline (1h)
├─ Refactor ChatSetupStep (1h)
├─ Refactor GameCreationStep (1h)
├─ Test admin wizard (30min)
└─ Start User Wizard structure (3h)

Day 2 (8h):
├─ User Wizard Step 1 (3h)
├─ User Wizard Step 2 (3h)
└─ User Wizard Step 3 (2h)

Day 3 (8h):
├─ Integration (3h)
├─ Tests (3h)
├─ Validation (2h)
└─ PR + Code Review

Total: 24h (~3 days)
```

---

## Next Actions

### Immediate (Before Starting)
- [x] Decision documented ✅
- [ ] Create Issue #4 branch
- [ ] Read admin wizard components (ChatSetupStep, GameCreationStep)
- [ ] Create refactor plan document

### Phase B (Refactoring)
- [ ] Refactor ChatSetupStep (pdfId optional)
- [ ] Refactor GameCreationStep (skip button)
- [ ] Test admin wizard (regression check)

### Phase C (User Wizard)
- [ ] Create UserWizardClient state machine
- [ ] Implement Step 1, 2, 3
- [ ] Integration tests

---

## References

- Epic: Task #1
- Issue: Task #4 (User Wizard)
- Related: Issue #9 (Tier limits - integrated)
- Admin Wizard: `/apps/web/src/app/(authenticated)/admin/wizard/`

---

**Decision Recorded**: ✅
**Ready to Start**: Issue #4 with Refactor Strategy

**Awaiting user confirmation to proceed with Phase 2.**
