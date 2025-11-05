# Visual Testing with Playwright UI Mode

Quick guide to visual testing and debugging E2E tests for the nextjs-portal issue.

## 🚀 Quick Start

```bash
# Launch interactive menu
./scripts/visual-test-demo.sh

# Or run directly
pnpm test:e2e:ui                          # Open UI mode browser
pnpm test:e2e:ui comments-enhanced        # Specific test (working example)
pnpm test:e2e:ui demo-user-login          # Specific test (failing example)
```

## 📁 Resources

| File | Purpose |
|------|---------|
| `scripts/visual-test-demo.sh` | Interactive launcher with 7 scenarios |
| `e2e/PLAYWRIGHT-UI-MODE-GUIDE.md` | Complete guide to UI mode features |
| `e2e/visual-debug-demo.spec.ts` | Demo test with pause() for inspection |
| `e2e/README-demo-login-testing.md` | Technical analysis of portal issue |

## 🎯 Common Scenarios

### 1. See Portal Issue in Action

```bash
pnpm test:e2e:ui demo-user-login.spec.ts
```

**What to observe:**
- Test navigates to `/`
- Attempts to click "Get Started" button
- ❌ Fails with "nextjs-portal intercepts pointer events"
- Timeline shows error at click action
- DOM snapshot shows `<nextjs-portal>` element

### 2. See Working Alternative (Mock Auth)

```bash
pnpm test:e2e:ui comments-enhanced.spec.ts
```

**What to observe:**
- ✅ Test completes in ~2 seconds
- No home page navigation
- Directly accesses `/versions` with mock auth
- No portal issues
- All interactions work smoothly

### 3. Debug Interactively

```bash
pnpm playwright test visual-debug-demo.spec.ts --debug
```

**What happens:**
- Test pauses at strategic points
- You can inspect DOM manually
- Step through actions one-by-one
- Try clicking elements yourself
- Compare behavior

### 4. Visual Comparison (Headed vs Headless)

```bash
# Headless (default) - portal blocks clicks
pnpm playwright test demo-user-login.spec.ts

# Headed (visible browser) - might work better
pnpm playwright test demo-user-login.spec.ts --headed
```

## 🔍 Investigation Checklist

When debugging the portal issue in UI mode:

- [ ] **Navigate:** Test loads home page successfully
- [ ] **Portal Exists:** Find `<nextjs-portal>` in DOM
- [ ] **Z-Index:** Check if portal has high z-index
- [ ] **Pointer Events:** Check `pointer-events` CSS property
- [ ] **Click Attempt:** Watch click fail with timeout
- [ ] **Error Message:** See "intercepts pointer events" error
- [ ] **Compare Mock:** Run working test to see difference
- [ ] **Headed Mode:** Try in visible browser

## 📊 Test Categories

### ✅ Working Tests (Mock Auth)

All use `fixtures/auth.ts` for reliable authentication:

```bash
pnpm test:e2e:ui comments-enhanced       # Comments system
pnpm test:e2e:ui admin-analytics         # Admin dashboard
pnpm test:e2e:ui admin-configuration     # Configuration UI
pnpm test:e2e:ui chat-streaming          # Streaming QA
pnpm test:e2e:ui chat-context-switching  # Context switching
pnpm test:e2e:ui setup                   # Setup guide
pnpm test:e2e:ui timeline                # Version timeline
```

**Why they work:**
- Bypass home page login UI
- Mock `/api/v1/auth/me` endpoint
- Direct navigation to authenticated pages
- No portal elements loaded
- Fast, reliable, deterministic

### ❌ Failing Tests (Real Login UI)

```bash
pnpm test:e2e:ui demo-user-login         # Real login flow
pnpm test:e2e:ui chat-edit-delete        # Uses real login
```

**Why they fail:**
- Load home page with login modal
- `<nextjs-portal>` appears in headless Chromium
- Portal intercepts all pointer events
- Clicks timeout after 10 seconds
- Known Next.js/Playwright compatibility issue

## 🎬 Demo Script Options

The interactive script (`scripts/visual-test-demo.sh`) offers:

1. **Mock Auth Test** - See working example
2. **Real Login Test** - See portal issue
3. **Admin Tests** - Admin role functionality
4. **Chat Streaming** - User role functionality
5. **UI Mode Browser** - Choose tests from UI
6. **Debug Mode** - Manual inspection with pause
7. **Headed Mode** - Visible browser testing

## 📸 Screenshots & Traces

### Automatic on Failure

Configured in `playwright.config.ts`:
```typescript
use: {
  screenshot: 'only-on-failure',
  trace: 'retain-on-failure',
}
```

Saved to: `test-results/`

### View Trace Report

```bash
# Run test (generates trace on failure)
pnpm test:e2e demo-user-login

# Open trace viewer
pnpm test:e2e:report
```

**Trace viewer shows:**
- Timeline of all actions
- Network requests
- Console logs
- DOM snapshots at each step
- Screenshots
- Source code

## 🎓 Learning Path

### Beginner: See the Problem

```bash
# 1. Run working test
pnpm test:e2e:ui comments-enhanced.spec.ts

# 2. Run failing test
pnpm test:e2e:ui demo-user-login.spec.ts

# 3. Compare the difference
```

### Intermediate: Investigate

```bash
# 1. Run debug test
pnpm playwright test visual-debug-demo.spec.ts --debug

# 2. Inspect DOM during pauses
# 3. Check portal properties
# 4. Try manual clicks
```

### Advanced: Analyze

```bash
# 1. Generate trace
pnpm test:e2e demo-user-login.spec.ts

# 2. Open trace viewer
pnpm test:e2e:report

# 3. Analyze timeline, network, console
# 4. Compare headed vs headless behavior
```

## 💡 Key Insights

### Why Mock Auth Works

```typescript
// Mock auth bypasses the problematic flow:
//
// ❌ Real login: / → click Get Started → modal → portal issue
// ✅ Mock auth: /versions → already authenticated → no portal

// Setup mock
await page.route('/api/v1/auth/me', (route) => {
  route.fulfill({ body: JSON.stringify({ user: {...} }) });
});

// Navigate directly
await page.goto('/versions'); // No home page, no portal!
```

### Portal Issue Root Cause

The `<nextjs-portal>` element:
- Is part of Next.js modal/overlay system
- Appears in headless Chromium environment
- Has very high z-index
- Intercepts all pointer events
- Blocks clicks even when invisible
- Doesn't appear (or behaves differently) in headed mode

**Not a bug in our code** - it's a Next.js/Playwright/headless Chromium interaction issue.

## 🎯 Recommendations

### For CI/CD (Automated Testing)

✅ **Use mock auth** for all E2E tests
- Reliable
- Fast (2-5 seconds vs 10-15 seconds)
- No portal issues
- Tests actual functionality (not login UI)

### For Login UI Testing

✅ **Backend integration tests** (`apps/api/tests/`)
- Test `/api/v1/auth/login` endpoint
- Test session creation
- Test password validation
- Already 100% coverage

✅ **Manual testing periodically**
- Real browser testing
- UX validation
- Visual regression checks

✅ **Headed mode for demos**
```bash
pnpm playwright test demo-user-login.spec.ts --headed --project=chromium
```

### For Visual Regression

✅ **Screenshot testing** for authenticated pages
```typescript
// In tests
await expect(page).toHaveScreenshot('feature.png');
```

✅ **Trace viewer** for debugging failures
```bash
pnpm test:e2e:report
```

## 🔗 Related Documentation

- [Playwright UI Mode Docs](https://playwright.dev/docs/test-ui-mode)
- [Debugging Tests](https://playwright.dev/docs/debug)
- [Trace Viewer](https://playwright.dev/docs/trace-viewer)
- [Visual Comparisons](https://playwright.dev/docs/test-snapshots)

---

## Quick Commands Reference

```bash
# Interactive launcher
./scripts/visual-test-demo.sh

# UI mode (choose tests from browser)
pnpm test:e2e:ui

# UI mode (specific test)
pnpm test:e2e:ui <test-file>

# Debug mode (with pause)
pnpm playwright test <test-file> --debug

# Headed mode (visible browser)
pnpm playwright test <test-file> --headed

# View trace report
pnpm test:e2e:report

# Update screenshot baselines
pnpm playwright test --update-snapshots
```

---

**TL;DR**: Use `./scripts/visual-test-demo.sh` to launch an interactive menu with 7 testing scenarios. Option 1 shows a working test (mock auth), option 2 shows the portal issue. 🎯
