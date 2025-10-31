# E2E Fix Phase 2 - Remaining Work

## Status
**Completed**: 2/9 spec files refactored with auth fixture
**Remaining**: 7 spec files need auth fixture integration

## Completed Files ✅
1. **admin-analytics.spec.ts** - 7/7 tests updated
   - All tests use `{ adminPage: page }`
   - beforeEach removed (handled by fixture)

2. **admin-configuration.spec.ts** - 4/5 tests updated
   - Admin tests use `{ adminPage: page }`
   - non-admin test keeps `{ page }` (correct)

## Remaining Files (7)

### Real Login Pattern (6 files)
Files using actual login flow with demo data:

1. **timeline.spec.ts**
   - Pattern: `page.fill('input[type="email"]', 'admin@meepleai.dev')`
   - Estimated tests: ~5
   - Effort: 30min

2. **setup.spec.ts**
   - Pattern: Same login flow
   - Estimated tests: ~3
   - Effort: 20min

3. **comments-enhanced.spec.ts**
   - Pattern: Same login flow
   - Estimated tests: ~8
   - Effort: 40min

4. **chat-streaming.spec.ts**
   - Pattern: Same login flow
   - Estimated tests: ~10
   - Effort: 45min

5. **chat-context-switching.spec.ts**
   - Pattern: Same login flow
   - Estimated tests: ~6
   - Effort: 30min

6. **chat-animations.spec.ts**
   - Pattern: Same login flow
   - Estimated tests: ~8
   - Effort: 40min

### Mock API Pattern (Skip)
These files use `mockAuthenticatedAdmin()` and don't need fixture:
- admin-users.spec.ts (custom mock)
- admin.spec.ts (custom mock)
- authenticated.spec.ts (custom mock)

## Refactoring Pattern

### Before (timeline.spec.ts example)
```ts
import { test, expect } from '@playwright/test';

test.describe('Timeline', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:3000/login');
    await page.fill('input[type="email"]', 'admin@meepleai.dev');
    await page.fill('input[type="password"]', 'Demo123!');
    await page.click('button[type="submit"]');
    await page.waitForURL(/.*\/(chat|home|$)/);
  });

  test('shows timeline', async ({ page }) => {
    await page.goto('/admin/timeline');
    // test logic...
  });
});
```

### After
```ts
import { test, expect } from './fixtures/auth';

test.describe('Timeline', () => {
  test('shows timeline', async ({ adminPage: page }) => {
    await page.goto('/admin/timeline');
    // test logic...
  });
});
```

## Effort Estimate
| File | Tests | Effort | Impact |
|------|-------|--------|--------|
| timeline.spec.ts | ~5 | 30min | Login timeout fixes |
| setup.spec.ts | ~3 | 20min | Login timeout fixes |
| comments-enhanced.spec.ts | ~8 | 40min | Login timeout fixes |
| chat-streaming.spec.ts | ~10 | 45min | Login timeout fixes |
| chat-context-switching.spec.ts | ~6 | 30min | Login timeout fixes |
| chat-animations.spec.ts | ~8 | 40min | Login timeout fixes |
| **Total** | **~40** | **~3.5h** | **~40 additional tests fixed** |

## Implementation Checklist

For each file:
- [ ] Change import from `@playwright/test` to `./fixtures/auth`
- [ ] Remove `test.beforeEach()` login block
- [ ] Update test signatures: `({ page })` → `({ adminPage: page })`
- [ ] Update URLs: `http://localhost:3000/path` → `/path`
- [ ] Verify no custom auth logic is lost

## Expected Outcome
After completing all 6 files:
- **Current**: 37/243 pass (15%)
- **With Phase 1-4**: ~190/243 pass (78%) - from config + 2 specs
- **With all 6 files**: ~230/243 pass (95%) - production-ready

## Next Steps
1. Implement remaining 6 files (3.5h)
2. Run full E2E suite validation
3. Document final pass rate
4. Commit with summary
5. Update CI/CD if needed

## Notes
- admin-users/admin/authenticated use mock API (no change needed)
- Auth fixture provides waitForLoadState (prevents race conditions)
- Each file refactor takes ~30-45min avg
- Total remaining effort: **3.5 hours**
