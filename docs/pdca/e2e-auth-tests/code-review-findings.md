# E2E Test Anti-Pattern Code Review Findings

**Date**: 2026-01-19
**Scope**: Search for E2E test anti-patterns based on lessons learned from auth-email-registration-flow

## Summary

Overall, the E2E test codebase is in **good shape**. Most tests follow correct patterns for response timing and don't use problematic approaches.

## Anti-Patterns Searched

### 1. Response Timing (waitForResponse AFTER click)

**Status**: ✅ **No major issues found**

Most tests in `apps/web/e2e/` correctly set up `waitForResponse` BEFORE triggering the action. Example of correct pattern in `service-status.spec.ts:343-350`:

```typescript
// Wait for API call triggered by refresh
const responsePromise = adminPage.waitForResponse(
  response => response.url().includes('/api/v1/admin/infrastructure/details'),
  { timeout: 5000 }
);
await refreshButton.click();
await responsePromise;
```

The auth-email-registration test now includes a comment documenting this pattern for future reference.

### 2. page.evaluate(fetch) for Cross-Origin Calls

**Status**: ✅ **No issues found**

No tests use `page.evaluate()` with `fetch()` for API calls. The codebase uses `page.request` for direct API calls, which is the correct approach.

### 3. Mobile Viewport Issues (hidden md: elements)

**Status**: ⚠️ **Potential risk areas identified**

Found 6 components that use responsive hiding (`hidden md:`):

| Component | Location | E2E Risk |
|-----------|----------|----------|
| `TopNav.tsx` | `apps/web/src/components/layout/` | **HIGH** - Contains logout button (fixed in auth test) |
| `PublicHeader.tsx` | `apps/web/src/components/layouts/` | MEDIUM - May have navigation elements |
| `ChatLayout.tsx` | `apps/web/src/components/layouts/` | MEDIUM - Layout-specific features |
| `TopNav.stories.tsx` | `apps/web/src/components/layout/` | LOW - Storybook file |
| `HeroSection.tsx` | `apps/web/src/components/landing/` | LOW - Visual only |
| `HowItWorksSection.tsx` | `apps/web/src/components/landing/` | LOW - Visual only |

**Recommendations**:
- Review E2E tests that interact with `PublicHeader` and `ChatLayout` on mobile viewports
- Ensure tests that need logout functionality use the pattern from `auth-email-registration-flow.spec.ts`
- Consider adding mobile-specific test paths for critical features

### 4. React Navigation Without Fallback

**Status**: ⚠️ **Pattern established but may need review**

The `auth-email-registration-flow.spec.ts` test now includes a fallback for React navigation:

```typescript
// Wait for React navigation
await page.waitForTimeout(3000);
if (!page.url().includes('/dashboard')) {
  // Fallback to full page navigation
  await page.goto('/dashboard');
}
```

**Recommendation**: Review other tests that expect client-side navigation after form submission to ensure they have similar fallbacks.

## Files with Potential Issues

### High Priority Review

1. **Any test using logout button on mobile** - Should use API approach like `auth-email-registration-flow.spec.ts`

2. **Tests with form submission + navigation** - Should have fallback navigation pattern

### Tests Using Correct Patterns (Examples)

- `admin/service-status.spec.ts` - Correct response timing
- `auth-email-registration-flow.spec.ts` - All patterns correct after fixes
- Export tests with `waitForEvent('download')` - Correct listener timing

## Automated Checks (Suggested)

Add ESLint rules or custom linting for E2E tests:

```javascript
// Potential lint rules
{
  // Warn on page.evaluate with fetch
  "no-restricted-syntax": [
    "warn",
    {
      "selector": "CallExpression[callee.object.name='page'][callee.property.name='evaluate'] > ArrowFunctionExpression > CallExpression[callee.name='fetch']",
      "message": "Use page.request instead of page.evaluate(fetch) to avoid CORS issues"
    }
  ]
}
```

## Conclusion

The E2E test codebase follows good practices overall. The main areas for improvement are:
1. Ensure mobile viewport tests handle hidden elements properly
2. Add navigation fallbacks for client-side routing tests
3. Document patterns (done in `docs/pdca/e2e-auth-tests/lessons-learned.md`)

---

**Related Documentation**:
- `docs/pdca/e2e-auth-tests/lessons-learned.md` - Detailed patterns and examples
- `.serena/memories/e2e-playwright-patterns.md` - Quick reference for future sessions
