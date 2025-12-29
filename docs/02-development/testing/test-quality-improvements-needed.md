# Test Quality Improvements Needed

Generated: 2025-12-29

## Overview
Systematic test quality issues identified during GitHub Actions troubleshooting.

## 🔴 Critical: E2E Tests Using Mocks

**Issue**: 55 E2E test files use `page.route()` to mock API responses instead of testing against real backend.

**Impact**:
- E2E tests don't validate actual API integration
- False confidence in production readiness
- Backend changes not caught by E2E suite

**Affected Files**: 55 files in `apps/web/e2e/`

**Pattern**:
```typescript
// ❌ WRONG: Mocking in E2E
await page.route(new RegExp(`${apiBase}/api/v1/admin/requests.*`), async route => {
  await route.fulfill({ body: JSON.stringify({ requests: mockData }) });
});

// ✅ RIGHT: Use real API
// No mocking - let requests go through to actual backend
```

**Action Required**:
1. Remove `page.route()` mocking from all E2E tests
2. Ensure backend services are running in E2E environment
3. Use real data seeding instead of mock responses
4. Keep mocks only in unit tests

**Files to Fix**: Run `grep -r "page.route(" apps/web/e2e --include="*.spec.ts"`

---

## 🟡 Important: Text-Based Element Identification

**Issue**: 123 test files use `getByText()` with hardcoded strings instead of semantic queries.

**Impact**:
- Tests break on translation changes
- Fragile to UI text updates
- Not testing accessibility properly

**Pattern**:
```typescript
// ❌ WRONG: Text-based identification
screen.getByText('Submit')
screen.getByText('Delete Item')

// ✅ RIGHT: Role-based identification
screen.getByRole('button', { name: /submit/i })
screen.getByLabelText(/item name/i)
```

**Action Required**:
1. Replace `getByText` with semantic queries (`getByRole`, `getByLabelText`)
2. Use regex patterns `/text/i` for case-insensitive matching
3. Prefer accessibility-focused queries

**Priority Files**: Tests with UI interaction (buttons, forms, navigation)

---

## 🟢 Recommended: Async Query Consistency

**Recently Fixed**: Race conditions in `MentionInput` tests

**Best Practice**:
```typescript
// ❌ Synchronous query for async elements
const button = screen.getByRole('button');

// ✅ Async query with proper await
const button = await screen.findByRole('button');

// ✅ Or wrap in waitFor
await waitFor(() => {
  const button = screen.getByRole('button');
  expect(button).toBeInTheDocument();
});
```

**Action Required**:
- Review all `getByRole/getAllByRole` calls
- Use `findByRole/findAllByRole` for elements that render asynchronously
- Add `waitFor()` for queries inside assertions

---

## 📊 Statistics

| Category | Count | Status |
|----------|-------|--------|
| Total test files | 235 | - |
| Files using `getByText` | 123 | 🟡 Needs improvement |
| E2E files with mocks | 55 | 🔴 Critical issue |
| Unit tests with proper roles | ~112 | ✅ Good |

---

## 🎯 Implementation Plan

### Phase 1: Critical (E2E Mocks)
**Timeline**: 2-3 weeks
**Scope**: Remove all `page.route()` mocking from 55 E2E files
**Approach**: File-by-file conversion to real API calls

### Phase 2: Important (Text Identification)
**Timeline**: 3-4 weeks
**Scope**: Replace `getByText` with semantic queries in 123 files
**Approach**: Systematic replacement with linter rules

### Phase 3: Polish (Async Consistency)
**Timeline**: 1 week
**Scope**: Review and fix async query patterns
**Approach**: Automated search and replace with validation

---

## 🔍 Detection Patterns

### Find E2E Mocks
```bash
cd apps/web
grep -r "page.route(" e2e --include="*.spec.ts" -l
```

### Find Text-Based Queries
```bash
cd apps/web
grep -r "getByText(['\"]" src --include="*.test.tsx" -l
```

### Find Hardcoded Button Text
```bash
cd apps/web
grep -r "getByText(['\"].*\(Submit\|Save\|Delete\|Cancel\)" src --include="*.test.tsx"
```

---

## ✅ Recent Fixes (2025-12-29)

**MentionInput Tests**:
- Fixed 8 race conditions with `findAllByRole`
- Fixed 3 tests with `waitFor()` timing
- Both `MentionInput.test.tsx` and `MentionInput.input.test.tsx`

**Reports Tests**:
- Fixed 8 async queries with `findByRole`
- Fixed tab navigation timing issues

**Vitest Config**:
- Increased CI timeouts: 30s → 60s (test), 10s → 20s (hooks)
