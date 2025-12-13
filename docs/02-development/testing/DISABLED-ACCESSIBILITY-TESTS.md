# Disabled Accessibility Tests Tracking

**Status**: Temporary Disabled (CI Only)  
**Priority**: P1 (Re-enable Target: 2 weeks)  
**Created**: 2025-12-10  
**Last Updated**: 2025-12-13T10:59:23.970Z

## Summary

4 Playwright E2E accessibility tests are **skipped in CI** due to timing/stability issues with modal focus management and network idle states.

## Disabled Tests

### 1. Auth Modal Accessibility (`accessibility.spec.ts:158`)

```typescript
test.skip(isCI, 'Landing page auth modal should have no violations when open')
```

**Reason**: Modal timing issues with production server  
**Issue**: #1868  
**Local**: ✅ Passes  
**CI**: ❌ Flaky (race condition with modal mount)

**Root Cause**:
- Next.js modal hydration timing
- Focus trap initialization race condition
- `networkidle` unreliable in CI environment

---

### 2. Keyboard Navigation (`accessibility.spec.ts:180`)

```typescript
test.skip(isCI, 'should be able to navigate landing page with keyboard')
```

**Reason**: networkidle timing issues with production server  
**Issue**: #1868  
**Local**: ✅ Passes  
**CI**: ❌ Flaky (Next.js dev tools interference)

**Root Cause**:
- Next.js dev overlay captures initial Tab focus
- `networkidle` too aggressive for hydration complete
- Need to wait for React hydration marker

---

### 3. Button Activation (`accessibility.spec.ts:206`)

```typescript
test.skip(isCI, 'should be able to activate buttons with keyboard')
```

**Reason**: Modal/dialog timing issues with production server  
**Issue**: #1868  
**Local**: ✅ Passes  
**CI**: ❌ Flaky (Enter key timing)

**Root Cause**:
- React onClick handler not registered before Enter press
- Modal open animation conflicts with keyboard event
- Need `waitForFunction` for button interactivity

---

### 4. Focus Indicators (`accessibility.spec.ts:253`)

```typescript
test.skip(isCI, 'buttons should have visible focus indicators')
```

**Reason**: Visual focus detection unreliable in headless browser  
**Issue**: #1868  
**Local**: ⚠️ Manual verification needed  
**CI**: ❌ Flaky (outline detection in headless)

**Root Cause**:
- CSS `:focus-visible` not rendered consistently in headless
- Playwright screenshot comparison needed
- Current check uses computed styles (unreliable)

---

## Impact Analysis

**Coverage Loss**: ~4 accessibility tests (out of 12 total)  
**Risk**: ⚠️ **Medium** - Modal/keyboard navigation not tested in CI

**Mitigations**:
- ✅ Manual testing pre-release
- ✅ Storybook visual testing for focus states
- ✅ Local E2E runs pass (developer workflow)
- ✅ Axe-core violations checked on 8 other pages

---

## Action Plan (P1 - 2 weeks)

### Week 1: Stabilize Modal Tests

**Goal**: Re-enable test #1 (Auth Modal)

1. **Replace `networkidle`** → `waitForLoadState('domcontentloaded')` + custom marker
2. **Add modal ready check**:
   ```typescript
   await page.waitForFunction(() => {
     const modal = document.querySelector('[role="dialog"]');
     return modal && modal.getAttribute('data-state') === 'open';
   });
   ```
3. **Increase timeouts** for CI (30s → 60s for modal operations)

### Week 2: Fix Keyboard Navigation

**Goal**: Re-enable tests #2, #3, #4

1. **Skip Next.js dev tools**:
   ```typescript
   // Filter out Next.js portals from Tab navigation
   await page.evaluate(() => {
     document.querySelectorAll('nextjs-portal').forEach(el => 
       el.setAttribute('inert', 'true')
     );
   });
   ```

2. **Wait for hydration**:
   ```typescript
   // React 19 hydration marker
   await page.waitForFunction(() => 
     window.__NEXT_DATA__?.props?.pageProps !== undefined
   );
   ```

3. **Focus indicator check** (Playwright screenshot):
   ```typescript
   await expect(page.locator('button:focus')).toHaveScreenshot('button-focus.png', {
     stylePath: 'e2e/styles/focus-only.css' // Hide non-focus content
   });
   ```

---

## Testing Strategy

### Local Development

**Run all tests** (including skipped):
```bash
cd apps/web
pnpm test:e2e --grep="accessibility"
```

**Force run skipped tests**:
```bash
CI=false pnpm test:e2e accessibility.spec.ts
```

### CI Pipeline

**Current behavior**: 4 tests automatically skipped when `process.env.CI === 'true'`

**After fix**: Remove `test.skip(isCI, ...)` conditionals

---

## Success Criteria (Re-enable Checklist)

- [ ] Modal tests pass 10/10 runs in CI
- [ ] Keyboard tests pass 10/10 runs in CI  
- [ ] No `test.skip(isCI, ...)` in `accessibility.spec.ts`
- [ ] CI pipeline passes with all 12 accessibility tests enabled
- [ ] Documentation updated (remove TODO comments)

---

## Monitoring

**Pre-commit**: ✅ TypeScript + ESLint (no accessibility checks)  
**CI**: ⚠️ 8/12 accessibility tests enabled  
**Manual**: ✅ Full accessibility audit via `pnpm audit:a11y`  
**Storybook**: ✅ Chromatic visual regression (focus states)

---

## Related Issues

- **#1868**: CI flakiness with modal timing
- **#841**: Removed `force: true` from button clicks
- **#1256**: Coverage target 90% (accessibility tests count toward E2E coverage)

---

## References

- **Playwright Docs**: https://playwright.dev/docs/test-assertions#visible
- **React Hydration**: https://react.dev/reference/react-dom/client/hydrateRoot
- **Axe-core Rules**: https://github.com/dequelabs/axe-core/blob/develop/doc/rule-descriptions.md

---

**Status**: 📋 **Tracked** - Re-enable target: 2025-12-24 (before Beta launch)

