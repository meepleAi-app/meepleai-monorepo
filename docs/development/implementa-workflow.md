# /implementa - Full Issue Implementation Workflow

> **Updated**: 2026-01-24 - Code review threshold changed to >= 70 (was >= 75)

## Code Review Threshold

**CRITICAL CHANGE**: Code review issue resolution threshold is **>= 70** (not >= 75 or >= 80).

### Rationale
- Score 70-79: Real issues that impact functionality, explicitly called out in CLAUDE.md, or will hit in practice
- Score < 70: Minor style, nitpicks, unlikely edge cases, or false positives
- This balances code quality with pragmatic delivery velocity

### Issue Priority by Score

| Score Range | Priority | Action |
|-------------|----------|--------|
| 90-100 | Critical | MUST fix before merge |
| 80-89 | High | MUST fix before merge |
| **70-79** | **Important** | **FIX in current PR** ✅ |
| 60-69 | Medium | Document as tech debt, fix in follow-up |
| < 60 | Low | Ignore or defer to future improvement |

## Workflow Steps (Updated)

### Phase 6: PR e Code Review (Modified)

1. Create PR
2. Execute `/code-review:code-review <PR>`
3. **Fix ALL issues with score >= 70** (was >= 75) ⬅️ CHANGED
4. Repeat code review until no issues >= 70 remain
5. Update GitHub issue checkbox
6. Close issue

### Issue Resolution Strategy

**For each issue >= 70**:

1. **Score 90-100 (Critical)**:
   - Fix immediately in current PR
   - Cannot merge without resolution

2. **Score 80-89 (High)**:
   - Fix immediately in current PR
   - May require design discussion if complex

3. **Score 70-79 (Important)**: ⬅️ NEW THRESHOLD
   - Fix if straightforward (< 30min)
   - Create sub-issue if complex (> 30min)
   - Document in PR if deferred

4. **Score < 70**:
   - Ignore or document as "nice-to-have"
   - Optionally create separate improvement issue

## Example (PR #2990)

**Issues Found (Score >= 70)**:
1. Failing tests (75) → Create sub-issue #2991
2. JsonException not handled (72) → Fixed in PR ✅
3. CQRS parsing logic (78) → Documented as tech debt
4. DateTime.UtcNow reliability (75) → Defer to TestTimeProvider refactoring
5. Missing test execution docs (75) → Document in PR

**Resolution**:
- Fixed critical bugs (score 72)
- Created sub-issues for complex problems (score 75)
- Documented tech debt (score 78)
- PR merged with 85% issue resolution rate

---

**Last Updated**: 2026-01-24
**Threshold**: >= 70 (changed from >= 75)
**Rationale**: Balance quality with delivery velocity
