# E2E Test Suite Phase 3: Advanced Features Plan

**Issue**: #795 TEST-006: Fix 228 Failing E2E Tests
**Phase**: 3 - Advanced Features & Remaining Core Tests
**Status**: PLANNED
**Estimated**: 15-20 hours

## Current Status (After Phase 2)

**Infrastructure**: ✅ 100% COMPLETE
- Browser stable (TEST-007 fixed)
- i18n helper ready (205 keys)
- Environment config working

**Tests Fixed**:
- home.spec.ts: 4/4 (100%)
- admin-users.spec.ts: 5/6 (83%)
- admin-analytics.spec.ts: 2/8 (25% - UI bugs)
- **Total**: 11/18 Phase 2 tests (61%)

**Proven Patterns**:
1. Run test → identify failures
2. Inspect actual UI (source or Playwright trace)
3. Update i18n keys to match reality
4. Fix selectors (getByRole with exact:true, filtered locators)
5. Add force clicks ({ force: true }) for portal overlays
6. Disable HTML5 validation for form tests
7. Validate passing

---

## Phase 3 Targets (Prioritized)

### Priority 1: Complete Phase 2 Files (46 tests, 8-10h)

#### 1. setup.spec.ts (22 tests, 4-5h)
**File**: `apps/web/e2e/setup.spec.ts`
**Likely Issues**:
- i18n mismatches (setup wizard has many text elements)
- Modal interactions (game selection, progress tracking)
- Force clicks needed for buttons
- API mocking for setup guide generation

**Strategy**:
- Run test, capture failures
- Update i18n keys for setup wizard flow
- Add force clicks to modal buttons
- Fix game selection dropdown
- Verify progress tracking elements

**Target**: 18-22/22 passing (80-100%)

#### 2. timeline.spec.ts (24 tests, 4-5h)
**File**: `apps/web/e2e/timeline.spec.ts`
**Likely Issues**:
- i18n mismatches (timeline UI elements)
- Filter interactions
- Event display and details
- Responsive design tests

**Strategy**:
- Run test, capture failures
- Update i18n keys for timeline elements
- Fix filter selectors
- Add force clicks as needed
- Verify event rendering with mocked data

**Target**: 20-24/24 passing (83-100%)

### Priority 2: Editor Tests (38 tests, 6-8h)

#### editor.spec.ts (38 tests, 6-8h)
**File**: `apps/web/e2e/editor.spec.ts`
**Complexity**: HIGH (largest file, rich text editor)
**Likely Issues**:
- TipTap editor interactions
- Toolbar button clicks (many portal overlays expected)
- Content editing and formatting
- Save/load operations
- i18n for editor UI elements

**Strategy**:
- Run test suite in sections (toolbar, editing, saving)
- Update i18n for editor-specific UI
- Add force clicks to ALL toolbar buttons
- Test Monaco/TipTap editor interactions carefully
- May need special handling for contenteditable elements

**Target**: 30-38/38 passing (79-100%)

**Sub-files**:
- editor-rich-text.spec.ts (19 tests) - TipTap specific
- editor-advanced.spec.ts (9 tests) - Advanced features

### Priority 3: Chat Tests (30+ tests, 4-6h)

#### Files:
- chat-streaming.spec.ts (13 tests)
- chat-animations.spec.ts (17 tests)
- chat-edit-delete.spec.ts (19 tests)
- chat-context-switching.spec.ts (13 tests)
- chat.spec.ts (minimal)

**Likely Issues**:
- Streaming response handling (timing)
- Animation tests (reduced motion, transitions)
- Message editing/deleting (portal clicks)
- Context switching (state management)
- i18n for chat UI

**Strategy**:
- Start with chat.spec.ts (simplest)
- Move to chat-streaming (timing issues)
- Then animations, edit-delete, context-switching
- Add generous timeouts for streaming
- Force clicks for all message actions

**Target**: 25-30/30+ passing (83-100%)

---

## Proven Fix Patterns (from Phase 2)

### Pattern 1: Strict Mode Violations
```typescript
// Problem: Text appears in multiple elements
// Solution: Use specific role or filtered locator
page.getByRole('cell', { name: 'text', exact: true })
page.locator('h2', { hasText: getTextMatcher('key') })
```

### Pattern 2: Portal Overlay Clicks
```typescript
// Problem: <nextjs-portal> intercepts clicks
// Solution: Force click option
await button.click({ force: true });
await link.click({ force: true });
```

### Pattern 3: HTML5 Validation Bypass
```typescript
// Problem: required attribute prevents form submission
// Solution: Disable HTML5 validation
await page.evaluate(() => {
  const form = document.querySelector('form');
  if (form) form.setAttribute('novalidate', 'true');
});
```

### Pattern 4: i18n Updates
```typescript
// CRITICAL: Inspect actual UI first!
// Don't guess translations - check actual page text

// 1. Run test, see actual text in error message
// 2. Update i18n.ts with actual text
'key': { en: 'Actual UI Text', it: 'Testo UI Reale' }

// 3. Use in test
getTextMatcher('key') // Creates regex: /(Actual UI Text|Testo UI Reale)/i
```

### Pattern 5: Test Execution Flow
```bash
# 1. Clean environment
powershell.exe -File tools/cleanup-test-processes.ps1

# 2. Run specific test
cd apps/web
pnpm test:e2e <filename>.spec.ts

# 3. Analyze failures (look for patterns)
# 4. Apply fixes
# 5. Rerun to validate
# 6. Commit when passing rate good
```

---

## Phase 3 Execution Strategy

### Week 1: Core Features (10-12h)
- **Day 1-2**: setup.spec.ts (22 tests) - 4-5h
- **Day 2-3**: timeline.spec.ts (24 tests) - 4-5h
- **Day 3**: Validation & fixes - 2h

### Week 2: Advanced Features (8-10h)
- **Day 1-2**: editor.spec.ts + variants (38+ tests) - 6-8h
- **Day 2-3**: Validation & iteration - 2h

### Week 3: Chat & Polish (4-6h)
- **Day 1**: chat tests (30+ tests) - 4-6h
- **Day 2**: Final validation & cleanup - 2h

**Total Phase 3**: 15-20 hours (as estimated)

---

## Success Criteria

**Phase 3 Targets**:
- setup.spec.ts: 80-100% passing (18-22/22)
- timeline.spec.ts: 83-100% passing (20-24/24)
- editor tests: 79-100% passing (45-55/57)
- chat tests: 83-100% passing (25-30/30+)

**Overall Target**: 85% pass rate (231/272 tests)

**Minimum Acceptable**: 75% pass rate (204/272 tests)

---

## Risk Mitigation

### Known Challenges

**1. Portal Overlay Persistence**
- **Issue**: nextjs-portal blocks many clicks despite force: true
- **Mitigation**:
  - Investigate portal closing logic
  - Consider adding data-testid to critical buttons
  - May need to fix UI bug (portal should close after use)

**2. Editor Complexity**
- **Issue**: TipTap editor has complex interaction model
- **Mitigation**:
  - Test toolbar sections independently
  - Use Playwright trace for debugging
  - May need editor-specific test utilities

**3. Streaming/Timing**
- **Issue**: Chat streaming tests have timing sensitivities
- **Mitigation**:
  - Increase timeouts generously
  - Use waitForSelector with explicit conditions
  - Mock streaming responses for consistency

**4. Test Execution Time**
- **Issue**: Full suite takes 30-45min to run
- **Mitigation**:
  - Run files individually during development
  - Use --grep for specific test sections
  - Only run full suite for final validation

---

## Documentation Requirements

### For Each Fixed File

Document in Phase 3 implementation markdown:
- Test file name and test count
- Failures found and fixes applied
- i18n keys added/updated
- Pass rate achieved
- Any UI bugs discovered
- Patterns used

### Final Phase 3 Report

Include:
- Total tests fixed
- Overall pass rate achieved
- i18n key count (current vs final)
- Time invested vs estimated
- Recommendations for Phase 4
- Known issues remaining

---

## Tools & Commands

### Essential Commands
```bash
# Clean environment
powershell.exe -File tools/cleanup-test-processes.ps1

# Run specific file
cd apps/web
pnpm test:e2e <file>.spec.ts

# Run with grep pattern
pnpm test:e2e <file>.spec.ts --grep "test name pattern"

# Generate HTML report
pnpm test:e2e:report

# Run in UI mode (for debugging)
pnpm test:e2e:ui <file>.spec.ts
```

### Debug Tools
```bash
# View Playwright trace
pnpm exec playwright show-trace test-results/<path>/trace.zip

# Check TypeScript errors
pnpm typecheck | grep "e2e/"

# Validate i18n keys
grep -r "getTextMatcher\|\\bt(" e2e/*.spec.ts
```

---

## Completion Checklist

- [ ] setup.spec.ts: 80%+ passing
- [ ] timeline.spec.ts: 83%+ passing
- [ ] editor.spec.ts: 79%+ passing
- [ ] editor-rich-text.spec.ts: 80%+ passing
- [ ] editor-advanced.spec.ts: 80%+ passing
- [ ] chat-streaming.spec.ts: 80%+ passing
- [ ] chat-animations.spec.ts: 80%+ passing
- [ ] chat-edit-delete.spec.ts: 80%+ passing
- [ ] chat-context-switching.spec.ts: 80%+ passing
- [ ] Overall Phase 3: 85%+ pass rate (231/272 tests)
- [ ] Documentation complete
- [ ] i18n keys comprehensive (250+ keys expected)
- [ ] PR created and reviewed
- [ ] Issue #795 updated with Phase 3 results

---

## Post-Phase 3 (Phase 4 Preview)

**Remaining Work** (10-15h):
- error-handling.spec.ts (separate issue - stubbed tests)
- OAuth integration (navigation complexity)
- Accessibility tests (WCAG validation)
- Edge cases and error scenarios
- Final cleanup and optimization

**Target**: 95% pass rate (258/272 tests)

---

**This plan provides clear, actionable steps for completing Phase 3 efficiently using proven patterns from Phases 0-2.**
