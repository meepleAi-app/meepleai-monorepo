# Check: Multi-User Game Library Workflows - Plan Review

**Review Date**: 2026-02-13
**Reviewer**: PM Agent (SuperClaude Framework)
**Plan Reviewed**: docs/pdca/multi-user-game-library/plan.md

---

## Review Summary

**Overall Assessment**: ⚠️ **Plan requires revision before execution**

| Metric | Status | Details |
|--------|--------|---------|
| **Structural Quality** | ✅ Good | Clear phases, logical progression |
| **Effort Estimation** | ⚠️ Underestimated | +22-44% realistic effort |
| **Risk Analysis** | ⚠️ Incomplete | 4/5 critical risks identified |
| **Dependencies** | ❌ Missing | 2 blocker issues not in plan |
| **Technical Validation** | ❌ Not performed | Assumptions not verified |

**Recommendation**: **Fix 5 critical gaps** before Phase 1 execution

---

## Critical Gaps Identified

### 🔴 GAP #1: Backend Endpoint Missing (BLOCKER)

**Finding**:
- Plan assumes: `GET /api/v1/rag/strategies` (public)
- Reality: `GET /api/v1/admin/rag-pipeline/strategies` (admin-only)
- Impact: **Issue #3 blocked**, users cannot fetch strategies

**Evidence**:
```csharp
// RagPipelineAdminEndpoints.cs:45
strategiesGroup.MapGet("/", async (...) => {
    var (authorized, session, error) = context.RequireAdminSession();
    // ❌ Admin only, not public
```

**Fix**:
- **Created**: Issue #8 (Task #8) - Public RAG Strategies Endpoint
- **Priority**: BLOCKER for Issue #3
- **Effort**: 2-3h backend
- **Action**: Create public endpoint exposing RagStrategy enum

**Status**: ✅ Issue created, dependency added (Task #4 blocked by Task #8)

---

### 🔴 GAP #2: Component Coupling Not Validated (CRITICAL)

**Finding**:
- Plan assumes: "Reuse GameCreationStep (adapt for optional PDF)"
- Reality: `ChatSetupStep` requires `pdfId: string` (non-nullable)
- Impact: **User wizard cannot reuse** admin components without refactoring

**Evidence**:
```tsx
// ChatSetupStep.tsx:17
interface ChatSetupStepProps {
  pdfId: string;  // ❌ Required, blocks "no PDF" flow
}
```

**Fix**:
- **Decision needed**: Refactor shared (4h) vs Duplicate user (6h)
- **Recommendation**: Refactor shared components
- **Changes**: Make `pdfId?: string | null`, conditional rendering
- **Effort adjustment**: Issue #4 effort +4h

**Status**: ⚠️ Requires architectural decision before Issue #4 start

---

### 🟡 GAP #3: Conditional Logic Not Specified (HIGH)

**Finding**:
- Plan mentions "optional steps" but doesn't specify state machine
- Missing: How to skip steps? Back navigation? Cancel handling?

**Missing Documentation**:
```typescript
// State transitions not defined
Step 1 (Game) → Step 2 (PDF)?
  - Always goes to PDF step

Step 2 (PDF) → Step 3 (Agent)?
  - IF pdfId exists → Agent config
  - IF skipped → Complete (no agent)

Step 3 (Agent) → Complete?
  - Always completes

Back Navigation:
  - From Step 3 → Step 2 (OK)
  - From Step 2 → Step 1 (OK, but keep gameId?)
  - From Step 1 → Cancel wizard?
```

**Fix**:
- **Add to Issue #4**: State machine diagram
- **Document**: All 3 paths (full, skip-pdf, skip-agent)
- **Edge cases**: Cancel, back navigation, resume after error
- **Effort adjustment**: Issue #4 effort +3h

**Status**: ⚠️ Requires detailed specification in Issue #4

---

### 🟡 GAP #4: Effort Underestimation (HIGH)

**Finding**: Multiple issues underestimate complexity

| Issue | Original | Realistic | Increase |
|-------|----------|-----------|----------|
| Issue #4 (User Wizard) | 1.5d | 2.5d | +67% |
| Issue #5 (Backend API) | 4h | 6h | +50% |
| **Phase 1** | 5h | 5h | 0% (fixed by Issue #8) |
| **Phase 2** | 16h | 31h | +94% |
| **Phase 3** | 11h | 12h | +9% |
| **TOTAL** | 4.5d | 5.5-6.5d | +22-44% |

**Reasons**:
1. Conditional logic complexity (+4h)
2. State persistence (+2h)
3. Edge case testing (+3h)
4. Integration tests not counted (+2h)
5. Tier limits validation (+5h)

**Fix**:
- **Revised timeline**: 5.5-6.5 days (not 4.5)
- **Buffer**: Add 20-30% to complex issues
- **Separate**: Core effort vs edge cases/testing

**Status**: ⚠️ Effort estimates updated in check.md

---

### 🟡 GAP #5: Security & Tier Limits Missing (HIGH)

**Finding**:
- Plan doesn't validate tier limits (PDF size, strategy cost)
- Risk: Free users upload large PDFs, select costly strategies
- Backend rejects → poor UX, wasted processing

**Missing Validation**:
```yaml
Free Tier Limits (assumed, not verified):
  - Max PDF size: 10MB
  - Max PDFs/month: 5
  - Allowed strategies: Fast, Balanced only

Pro Tier:
  - Max PDF size: 50MB
  - Allowed strategies: All except Custom

Enterprise:
  - All features unlocked
```

**Fix**:
- **Created**: Issue #9 (Task #9) - Tier Limits Validation
- **Priority**: HIGH (Phase 2)
- **Effort**: 4-5h
- **Integration**: Add to Issue #4 (User Wizard)

**Status**: ✅ Issue created

---

## Revised Roadmap

### **Phase 0: Prerequisites** (NEW - 4-5h)
```yaml
Issue #8: Public RAG Strategies Endpoint
  - Backend: Create GET /api/v1/rag/strategies
  - Expose RagStrategy enum publicly
  - Effort: 2-3h
  - Blocks: Issue #3

Validation Tasks:
  - ✅ Verify RagStrategy enum exists (Found: 12 strategies)
  - ✅ Verify admin endpoint exists (Found: /admin/rag-pipeline/strategies)
  - ❌ Public endpoint missing (Issue #8 created)
  - ⚠️ Component coupling check (Decision needed: Refactor vs Duplicate)
```

### **Phase 1: Quick Wins** (5h) - No Change
```yaml
Issue #2: Add Game Button (2h)
Issue #3: Selectors (3h) - NOW depends on Issue #8
```

### **Phase 2: User Wizard** (31h vs 16h original)
```yaml
Issue #4: User Wizard (20h vs 12h)
  - Core wizard: 12h
  - Conditional logic: 4h
  - State persistence: 2h
  - Edge cases: 2h

Issue #5: Backend Agent API (6h vs 4h)
  - CQRS implementation: 4h
  - Integration tests: 2h

Issue #9: Tier Limits (5h) - NEW
  - Client validation: 3h
  - UI feedback: 2h
```

### **Phase 3: Editor Workflow** (12h vs 11h)
```yaml
Issue #6: Editor Wizard (8h vs 8h)
Issue #7: Tracking Enhancement (4h vs 3h)
  - Filters + badges: 3h
  - Edge cases (rejection flow): 1h
```

### **Phase 4: Edge Case Testing** (NEW - 0.5-1d)
```yaml
Comprehensive E2E:
  - User wizard: all 3 paths
  - Editor wizard: approval/rejection flows
  - Tier limits: Free vs Pro vs Enterprise
  - Performance: Wizard load time validation
  - Accessibility: WCAG 2.1 AA compliance
```

---

## Architectural Decisions Required

### **Decision #1: Component Reuse Strategy**

**Question**: Refactor admin components vs Duplicate user-specific?

**Option A: Refactor Shared** (Recommended)
```yaml
Pros: DRY, single source of truth, easier maintenance
Cons: Risk regression in admin wizard
Effort: 4h refactoring + 2h testing
Changes:
  - ChatSetupStep: pdfId?: string | null
  - GameCreationStep: skipPdf?: boolean prop
  - Conditional rendering based on props
```

**Option B: Duplicate User Components**
```yaml
Pros: No risk to admin, user-specific optimizations
Cons: Code duplication, 2x maintenance
Effort: 6h duplication + 3h testing
New Files:
  - UserChatSetupStep.tsx
  - UserGameCreationStep.tsx
```

**Required Before**: Issue #4 implementation start
**Recommendation**: Option A (Refactor) - maintains code quality

---

### **Decision #2: Tier Limits Source of Truth**

**Question**: Where are tier limits defined? How to fetch?

**Investigation Needed**:
```bash
# Search tier limit configuration
grep -r "tier.*limit\|maxPdfSize" apps/api/src/Api/
```

**Options**:
1. **Database-driven**: Fetch from tier config table
2. **Hardcoded**: Constants in backend/frontend
3. **Feature flags**: Dynamic configuration

**Required Before**: Issue #9 implementation
**Action**: Verify tier limit system architecture

---

## Success Criteria Clarifications

### **Functional (Epic DoD)**
- [ ] User può aggiungere gioco a libreria privata (con/senza PDF)
  - **Add**: "with tier limits enforced"
- [ ] Editor può proporre gioco per catalogo condiviso
  - **Add**: "with rejection/re-submission flow"
- [ ] Agent config UI permette scelta typology + strategia RAG
  - **Add**: "filtered by user tier"
- [ ] Admin wizard remains functional (no regression)
  - **Add**: "verified with E2E regression tests"

### **Quality (Revised)**
- [ ] Test coverage ≥85% **branch coverage** (frontend)
- [ ] Test coverage ≥90% **branch coverage** (backend)
- [ ] E2E tests per tutti e 3 flussi **+ edge cases**
- [ ] No TypeScript errors (strict mode)
- [ ] No accessibility violations (WCAG 2.1 AA)
- **Add**: [ ] Performance: Wizard load time <2s (P95)
- **Add**: [ ] Security: Tier limits enforced (client + server)

### **Documentation (Enhanced)**
- [ ] Epic and issues documented in docs/pdca/
- [ ] Architecture decisions documented (ADR if needed)
  - **Add**: "Decision: Component reuse strategy (refactor vs duplicate)"
- [ ] User guides updated in docs/
  - **Add**: "How to add game to private library (with screenshots)"
- [ ] API changes documented in docs/03-api/
  - **Add**: "GET /api/v1/rag/strategies specification"

---

## Risk Re-Assessment

### Original Risk Analysis
| Risk | Original Priority | Revised Priority | Reason |
|------|-------------------|------------------|---------|
| Backend API Missing | Medium | 🔴 **CRITICAL** | Confirmed missing, blocks Phase 1 |
| Admin Wizard Reuse | Low | 🔴 **CRITICAL** | Coupling validated, requires refactoring |
| Agent Processing Time | Medium | 🟡 **MEDIUM** | Mitigation exists (ChatSetupStep polling) |
| ShareRequest Workflow | Low | 🟢 **LOW** | System exists, verified functional |

### New Risks Identified
| Risk | Priority | Impact | Mitigation |
|------|----------|--------|------------|
| **Tier Limits Not Enforced** | 🔴 HIGH | User frustration, backend overload | Issue #9 created |
| **Conditional Logic Bugs** | 🟡 MEDIUM | UX confusion, broken flows | Detailed state machine spec required |
| **Timeline Overrun** | 🟡 MEDIUM | Stakeholder expectations | Revised estimates 5.5-6.5d |

---

## Recommendations Summary

### 🔴 CRITICAL (Must Fix Before Execution)
1. **Create Issue #8** (Public RAG Strategies Endpoint) ✅ Done
2. **Decide**: Component refactor vs duplicate (Decision #1)
3. **Update**: Issue #4 effort 1.5d → 2.5d

### 🟡 HIGH (Strongly Recommended)
4. **Create Issue #9** (Tier Limits Validation) ✅ Done
5. **Specify**: Conditional logic state machine in Issue #4
6. **Add**: Edge case test scenarios to all issues

### 🟢 RECOMMENDED (Nice to Have)
7. **Add**: Phase 4 dedicated edge case testing (0.5-1d)
8. **Document**: Architectural decisions (refactor strategy)
9. **Create**: Issue #3.1 "Post-creation PDF upload" (future)

---

## Next Steps

### Immediate (Before Starting Phase 1)
1. **Verify Tier Limits**: Search codebase for tier configuration
2. **Decide Architecture**: Refactor shared vs Duplicate (Decision #1)
3. **Update Issue #4**: Add conditional logic specification
4. **Complete Issue #8**: Public strategies endpoint (blocker)

### Before Phase 2
5. **Review**: Admin wizard components (validate refactor safety)
6. **Document**: State machine diagram for user wizard
7. **Test Plan**: Edge case scenarios for all wizards

### Before Phase 3
8. **Verify**: ShareRequest approval workflow supports editor use case
9. **Review**: Existing proposals page for enhancement opportunities

---

## Conclusion

**Original Plan Quality**: 7/10
- ✅ Good structure and phase separation
- ✅ Component reuse strategy identified
- ✅ Risk analysis performed
- ❌ Effort underestimated 20-40%
- ❌ Critical dependencies not validated
- ❌ Security/tier limits not considered

**Revised Plan Quality**: 9/10 (after fixes)
- All critical gaps addressed
- Effort estimates realistic
- Dependencies mapped
- Security considerations included

**Ready for Execution**: ⚠️ **After fixes applied**
- Complete Issue #8 (blocker)
- Make architectural decision (refactor vs duplicate)
- Update Issue #4 with conditional logic spec
- Then: **READY for Phase 1**

---

## Action Items for User

### Before Starting Implementation
- [ ] Review this check.md document
- [ ] Decide: Component refactor (Option A) or duplicate (Option B)?
- [ ] Approve revised effort estimates (5.5-6.5d instead of 4.5d)
- [ ] Prioritize: Start with Issue #8 (prerequisite) or defer and hardcode strategies?

### Optional Enhancements
- [ ] Add Issue #3.1 (Post-creation PDF upload) to backlog
- [ ] Add Phase 4 (Edge case testing) as separate milestone
- [ ] Review tier limits configuration before Phase 2

**PM Agent recommends**: Start with Issue #8 → Decision #1 → Issue #2 → Issue #3 → Phase 2
