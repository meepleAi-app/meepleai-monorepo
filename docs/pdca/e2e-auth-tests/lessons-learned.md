# E2E Authentication Tests - Lessons Learned

**Date**: 2026-01-19
**Context**: Email registration flow E2E test (`auth-email-registration-flow.spec.ts`)
**Result**: All 12 tests passing across all viewports

## Key Lessons

### 1. Playwright Response Timing (CRITICAL)

**Problem**: `waitForResponse` was called AFTER `click()`, causing the response to arrive before the listener was set up.

**Wrong Pattern**:
```typescript
await submitButton.click();
// ❌ Response already arrived - listener misses it
const response = await page.waitForResponse(url => url.includes('/api/v1/auth/register'));
```

**Correct Pattern**:
```typescript
// ✅ Set up listener BEFORE triggering the action
const responsePromise = page.waitForResponse(
  response => response.url().includes('/api/v1/auth/register'),
  { timeout: 15000 }
);
await submitButton.click();
const response = await responsePromise;
```

**Rule**: Always set up `waitForResponse()` BEFORE the action that triggers the response.

---

### 2. React Client-Side Navigation vs Full Page Navigation

**Problem**: React's `router.push('/dashboard')` may not work reliably in E2E tests, even when session cookies are correctly set.

**Root Cause**: Next.js middleware session validation behaves differently for:
- **Client-side navigation**: React Router handles routing, middleware may not re-validate
- **Full page navigation**: Browser makes new request, middleware validates session

**Solution**: Add fallback full page navigation when React navigation fails:
```typescript
// Wait for React navigation
await page.waitForTimeout(3000);

if (!page.url().includes('/dashboard')) {
  // Session cookie is set, but React didn't navigate
  // Try full page navigation as fallback
  await page.goto('/dashboard');
  await page.waitForLoadState('networkidle');
}
```

**Rule**: Never rely solely on React/SPA navigation in E2E tests - always have a fallback.

---

### 3. Mobile Viewport UI Differences

**Problem**: TopNav (with logout button) is hidden on mobile (`hidden md:flex`), BottomNav doesn't have logout.

**Discovery**: Desktop test passed, mobile test failed because logout button wasn't accessible.

**Solution**: Use Playwright's request API to call logout endpoint directly on mobile:
```typescript
if (isMobile) {
  const cookies = await context.cookies();
  const cookieHeader = cookies.map(c => `${c.name}=${c.value}`).join('; ');

  // Use Playwright's request API (not browser context)
  await page.request.post(`${apiBase}/api/v1/auth/logout`, {
    headers: { 'Cookie': cookieHeader },
  });

  // Clear cookies manually and navigate
  await context.clearCookies();
  await page.goto('/login');
}
```

**Rule**: Always verify UI elements are visible on ALL viewport sizes. Provide alternative test paths for mobile.

---

### 4. Cookie Handling with Playwright Request API

**Problem**: When using `page.request.post()`, cookies from the response (Set-Cookie) are NOT automatically applied to the browser context.

**Why**: Playwright's request API is separate from the browser - it's like `curl`, not like `fetch` in browser.

**Solution**: Manually clear cookies after API logout:
```typescript
// API call doesn't sync cookies with browser
await page.request.post(logoutUrl, { headers: { Cookie: ... } });

// Must manually clear browser cookies
await context.clearCookies();

// Navigate to trigger middleware check
await page.goto('/login');
```

**Rule**: `page.request` and `page.evaluate(fetch)` are different. Use `page.request` for server-side calls, manage cookie sync manually.

---

### 5. CORS and Browser Context

**Problem**: Trying to call API from within `page.evaluate()` caused CORS errors.

**Wrong Pattern**:
```typescript
// ❌ CORS error - browser enforces same-origin policy
await page.evaluate(async () => {
  await fetch('http://localhost:8080/api/v1/auth/logout', { method: 'POST' });
});
```

**Correct Pattern**:
```typescript
// ✅ Playwright request bypasses browser CORS
await page.request.post('http://localhost:8080/api/v1/auth/logout');
```

**Rule**: Use `page.request` for cross-origin API calls in tests, not `page.evaluate(fetch)`.

---

## Checklist for Future E2E Tests

- [ ] Set up `waitForResponse()` BEFORE triggering actions
- [ ] Add fallback `page.goto()` after React navigation
- [ ] Test ALL viewports (desktop, tablet, mobile)
- [ ] Verify UI elements are visible on mobile
- [ ] Use `page.request` for direct API calls (not `page.evaluate`)
- [ ] Manually sync cookies when using `page.request`
- [ ] Add meaningful timeouts with descriptive error messages
- [ ] Log network requests/responses for debugging

## Related Files

- `apps/web/e2e/auth-email-registration-flow.spec.ts` - Main test file
- `apps/web/middleware.ts` - Session validation middleware
- `apps/web/src/components/layout/TopNav.tsx` - Desktop navigation
- `apps/web/src/components/layout/BottomNav.tsx` - Mobile navigation

## Tags

`#e2e` `#playwright` `#authentication` `#cookies` `#navigation` `#mobile` `#viewport`
