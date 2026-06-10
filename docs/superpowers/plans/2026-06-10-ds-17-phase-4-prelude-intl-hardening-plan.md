# DS-17 Phase 4 Prelude — IntlProvider Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix IntlProvider runtime context issue in Storybook so pilot stories renderizzano correctly + capture 12 baseline PNGs (Library 9 + GameDetail 3) + flip snapshot CI step a blocking (post stable trajectory).

**Architecture:** Adaptive sequence — start with cheapest fix (decorator order swap), escalate to investigation only when needed. Hard 2gg budget; user authorization required to extend. Fallback custom mock alias accepted as last resort.

**Tech Stack:** Storybook 10.4.1 + @storybook/nextjs Webpack builder, react-intl 10.x, react-intl IntlProvider context, Playwright snapshot, vitest.

**Spec**: `docs/superpowers/specs/2026-06-10-ds-17-phase-4-prelude-intl-hardening-design.md`.

**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063).

---

## File Structure (gross)

| Path | Action | Responsibility |
|---|---|---|
| `apps/web/.storybook/preview.tsx` | MODIFY (Step A) OR REWRITE (Step B/C) | Decorator order swap OR alternative provider |
| `apps/web/.storybook/main.ts` | MODIFY (Step C/D only) | `webpackFinal` resolve.alias for react-intl module OR useTranslation mock |
| `apps/web/src/test-utils/__mocks__/useTranslation-storybook.ts` | CREATE (Step D only) | Stub `t()` function for Storybook env |
| `apps/web/e2e/storybook/__snapshots__/library-*.png` | CREATE | 9 Library baselines (Frame09-17) |
| `apps/web/e2e/storybook/__snapshots__/game-detail-*.png` | CREATE | 3 GameDetail baselines (Frame07-09) |
| `docs/for-developers/frontend/page-mock-story-pattern.md` | MODIFY | Replace Known limitation section con Fix log |
| `CLAUDE.md` | MODIFY | DS-17 paragraph note baseline captured |
| `.github/workflows/ci.yml` | MODIFY (optional) | Flip Storybook snapshot step to blocking IF stable trajectory ready |

---

## Sub-issue Phase 4 prelude — IntlProvider hardening (single PR)

### Task 1: Pre-flight — create sub-issue + branch

**Files:** none (workspace setup)

- [ ] **Step 1: Verify clean main-dev + on correct branch + anchor budget timestamp (Code-reviewer I5)**

```bash
git checkout main-dev && git pull --ff-only && git status --short
date -u +%Y-%m-%dT%H:%M:%SZ > /tmp/phase-4-prelude-start.txt
echo "Phase 4 prelude started at: $(cat /tmp/phase-4-prelude-start.txt)"
```
Expected: main-dev, working tree clean (eccetto `.claude/scheduled_tasks.lock` ignorable). Timestamp anchored for budget tracking in Tasks 5/7.

- [ ] **Step 2: Create sub-issue**

```bash
gh issue create --title "[DS-17 Phase 4 prelude] IntlProvider hardening — unblock baseline PNG capture" --body "$(cat <<'EOF'
## Goal

Fix IntlProvider runtime context issue in Storybook decorator chain → pilot stories renderizzano correctly → capture 12 baseline PNGs (Library 9 + GameDetail 3) → flip snapshot CI step a blocking (post stable trajectory).

## Context

DS-17 Phase 2.5 (PR #2117 \`da6aff26e\`) shipped retroactive pilot rewrite a argTypes matrix MA baseline PNG capture deferred per IntlProvider runtime context blocker: pilot stories rendono "[React Intl] Could not find required intl object" error wall.

Suspected root cause: dual react-intl module instances (preview bundle vs iframe.bundle.js Webpack chunk splitting).

## Scope

- Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-4-prelude-intl-hardening-design.md
- Plan: docs/superpowers/plans/2026-06-10-ds-17-phase-4-prelude-intl-hardening-plan.md

Adaptive sequence A → B → C → D + verification + docs + CI flip.

## Time budget

Hard 2gg total. Solo user può autorizzare extending. Plan executor STOP + raise se budget exceeded.

## Acceptance criteria

- [ ] Pilot stories renderizzano in Storybook (no error wall)
- [ ] 12 baseline PNGs captured + committed
- [ ] \`pnpm test:storybook:snapshots\` 12/12 PASS
- [ ] Docs page-mock-story-pattern.md updated con Fix log
- [ ] CI step flipped a blocking OR documented why still non-blocking
- [ ] Umbrella body updated post merge

## Refs

- Umbrella: #2063
- Phase 2.5 sub-issue: #2113 (MERGED)
- Spec/plan: docs/superpowers/{specs,plans}/2026-06-10-ds-17-phase-4-prelude-intl-hardening-*.md

🤖 Generated with Claude Code
EOF
)" 2>&1 | tail -3
```

Expected: GitHub issue URL — record `#NNNN` for usage successivo (substitute `#TBD` con questo numero).

- [ ] **Step 3: Create branch**

```bash
git checkout -b feature/issue-TBD-ds-17-phase-4-prelude-intl
git config branch.feature/issue-TBD-ds-17-phase-4-prelude-intl.parent main-dev
```

(Replace `TBD` con sub-issue # from Step 2.)

---

### Task 2: Diagnostic phase — control test + console.log

**Files:**
- Modify (temporary): `apps/web/.storybook/preview.tsx`

- [ ] **Step 1: Control test — verify if existing stories render OK (Playwright-based)**

```bash
cd apps/web
pnpm build-storybook 2>&1 | tail -3
```
Expected: build succeeds.

**Code-reviewer C2**: Storybook iframe.html serves a JS shell that hydrates client-side. `curl` cannot see React-rendered error wall. Use Playwright with `waitForLoadState('networkidle')` for ground-truth check.

Create `apps/web/e2e/diagnostic.spec.ts` (reused by Tasks 3/4/5/7 verification):

```ts
import { test, expect } from '@playwright/test';

const STORIES = [
  { name: 'AuthModal (existing control)', slug: 'components-auth-authmodal--default' },
  { name: 'Library Frame09 (pilot)', slug: 'pages-sp4-library-mockup-matrix--frame-09-all-grid-rail' },
  { name: 'Library Frame13 (pilot empty)', slug: 'pages-sp4-library-mockup-matrix--frame-13-empty-first-run' },
  { name: 'GameDetail Frame07 (pilot)', slug: 'pages-sp4-gamedetail-mockup-matrix--frame-07-desktop-own-info' },
];

for (const { name, slug } of STORIES) {
  test(`Diagnostic: ${name} renders without intl error`, async ({ page }) => {
    await page.goto(`/iframe.html?id=${slug}&viewMode=story`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);
    const errorVisible = await page.locator('text=Could not find required').count();
    if (errorVisible > 0) {
      const text = await page.locator('body').innerText();
      throw new Error(`${name}: intl error wall detected\n${text.slice(0, 500)}`);
    }
    // Sanity check: story-root should have rendered children
    const rootChildren = await page.locator('#storybook-root > *').count();
    expect(rootChildren).toBeGreaterThan(0);
  });
}
```

Run diagnostic:

```bash
pnpm exec playwright test --config playwright.storybook.config.ts e2e/diagnostic.spec.ts 2>&1 | tail -20
```

Expected outcomes (decision gate):
- **AuthModal PASS + Library/GameDetail PASS** → no intl error anywhere; revisit assumption; recapture baseline directly (skip Steps A-D, jump to Task 6)
- **AuthModal PASS + Library/GameDetail FAIL** → issue is NEW-stories specific; investigate decorator order interaction with new stories (proceed Task 3)
- **AuthModal FAIL + Library/GameDetail FAIL** → issue is iframe-wide; investigate global IntlProvider integration (proceed Task 3 con expectation Step A unlikely to work)
- **AuthModal FAIL + Library/GameDetail PASS** → unexpected, investigate; do not proceed without understanding

Document outcome in `.diagnostic-notes.md` later.

Expected outcomes:
- **If AuthModal OK + Library FAIL** → issue is NEW-stories specific; investigate decorator order interaction with new stories
- **If both FAIL** → issue is iframe-wide; investigate global IntlProvider integration
- **If both OK** → revisit assumption; recapture baseline directly (skip Steps A-D)

Document outcome in commit message later. Record observation.

- [ ] **Step 2: Add console.log instrumentation to AllProviders**

Use Edit tool on `apps/web/.storybook/preview.tsx`. Find `const AllProviders = ({ children }: { children: React.ReactNode }) => {` and add console.log inside:

```tsx
const AllProviders = ({ children }: { children: React.ReactNode }) => {
  console.log('[storybook AllProviders] decorator running, FLAT_IT_MESSAGES keys:', Object.keys(FLAT_IT_MESSAGES).length);
  return (
    <ReactIntlProvider
      // ... existing code
```

- [ ] **Step 3: Rebuild + reopen pilot story in browser, inspect console**

```bash
pnpm build-storybook 2>&1 | tail -3
pnpm exec http-server storybook-static -p 6007 -s &
```

Open browser to `http://127.0.0.1:6007/iframe.html?id=pages-sp4-library-mockup-matrix--frame-09-all-grid-rail&viewMode=story`. Open browser DevTools console.

Look for:
- `[storybook AllProviders] decorator running` log → decorator runs at render time
- `[React Intl] Could not find required intl object` error → useIntl context lookup fails

If `[storybook AllProviders]` log NOT seen → decorator is NOT running. If log seen → IntlProvider context exists but useIntl context lookup fails (likely dual module instance).

```bash
kill %1  # Stop http-server
```

- [ ] **Step 4: Remove console.log instrumentation (cleanup)**

Use Edit tool to remove the `console.log` line from `AllProviders`.

- [ ] **Step 5: Record diagnostic findings in commit-pending notes**

Create `.diagnostic-notes.md` in repo root (temporary, will be incorporated into PR body):

```markdown
# IntlProvider diagnostic findings

## Control test
- AuthModal renders: [YES/NO]
- Library Frame09 renders: [YES/NO]

## Console.log
- [storybook AllProviders] decorator runs: [YES/NO]
- useIntl context lookup fails: [YES/NO]

## Hypothesis
[Based on observations, which step (A/B/C/D) is most likely to succeed]
```

Fill in the [YES/NO] based on Step 1 + Step 3 outcomes.

**Note**: this file is NOT committed; used as reference for next steps.

---

### Task 3: Step A — Swap decorator order

**Files:**
- Modify: `apps/web/.storybook/preview.tsx`

**Decision gate**: proceed con Task 3 if Task 2 diagnostic shows decorator IS running but context lookup fails. If decorator NOT running, skip to Task 5 (Step C).

- [ ] **Step 1: Swap decorator order in preview.tsx**

Use Edit tool on `apps/web/.storybook/preview.tsx`. Find:

```tsx
  decorators: [
    withThemeByClassName({
      themes: {
        light: 'light',
        dark: 'dark',
      },
      defaultTheme: 'light',
    }),
    Story => (
      <AllProviders>
        <Story />
      </AllProviders>
    ),
  ],
```

Replace with (AllProviders FIRST, withThemeByClassName SECOND):

```tsx
  decorators: [
    Story => (
      <AllProviders>
        <Story />
      </AllProviders>
    ),
    withThemeByClassName({
      themes: {
        light: 'light',
        dark: 'dark',
      },
      defaultTheme: 'light',
    }),
  ],
```

- [ ] **Step 2: Rebuild + diagnostic test (Code-reviewer C2: uses e2e/diagnostic.spec.ts)**

```bash
rm -rf storybook-static
pnpm build-storybook 2>&1 | tail -3
pnpm exec playwright test --config playwright.storybook.config.ts e2e/diagnostic.spec.ts 2>&1 | tail -20
```

Expected decision gate:
- **All 4 stories PASS** → Step A worked. Proceed to Task 6.
- **AuthModal regression (was PASS in Task 2, now FAIL)** → revert Step 1 swap. Document. Proceed to Task 4.
- **Library/GameDetail FAIL** → Step A insufficient. Proceed to Task 4 (Step B).

- [ ] **Step 3: If Step A worked, commit**

```bash
git add apps/web/.storybook/preview.tsx
git commit -m "fix(storybook): #TBD swap decorator order to wire IntlProvider (DS-17 Phase 4 prelude)"
```

If Step A did NOT work, revert + proceed to Task 4:

```bash
git checkout -- apps/web/.storybook/preview.tsx
```

---

### Task 4: Step B — Alternative provider OR dynamic import

**Decision gate**: proceed con Task 4 only if Task 3 Step A failed.

**Files:**
- Modify: `apps/web/.storybook/preview.tsx`

- [ ] **Step 1: Try alternative — use `@/components/providers/IntlProvider` production wrapper**

Use Edit tool on `apps/web/.storybook/preview.tsx`. Find:

```tsx
import { IntlProvider as ReactIntlProvider } from 'react-intl';
```

Replace with:

```tsx
import { IntlProvider } from '@/components/providers/IntlProvider';
```

Find `const AllProviders = ...` block and replace `<ReactIntlProvider ... messages={FLAT_IT_MESSAGES} ...>` con `<IntlProvider>` (no props — production wrapper handles locale + messages internally):

```tsx
const AllProviders = ({ children }: { children: React.ReactNode }) => {
  return (
    <IntlProvider>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider attribute="class" defaultTheme="light" enableSystem={false}>
          <TooltipProvider>
            <MockAuthProvider>{children}</MockAuthProvider>
          </TooltipProvider>
        </ThemeProvider>
      </QueryClientProvider>
    </IntlProvider>
  );
};
```

**Code-reviewer I1**: explicit Edit blocks for cleanup (verify current preview.tsx state first):

- Edit A: delete line `import itMessages from '../src/locales/it.json';` (replace entire line with empty string)
- Edit B: delete `flattenMessages` function block + `const FLAT_IT_MESSAGES = ...` declaration (full removal)
- Edit C: replace `import { IntlProvider as ReactIntlProvider } from 'react-intl';` with new import added at Step 1 (`import { IntlProvider } from '@/components/providers/IntlProvider';`)

Verify post-cleanup: `pnpm lint` reports zero unused-import warnings on preview.tsx.

- [ ] **Step 2: Rebuild + diagnostic test (reuse e2e/diagnostic.spec.ts)**

```bash
rm -rf storybook-static
pnpm build-storybook 2>&1 | tail -3
pnpm exec playwright test --config playwright.storybook.config.ts e2e/diagnostic.spec.ts 2>&1 | tail -20
```

Decision gate:
- **All 4 stories PASS** → Step B worked. Proceed to Task 6.
- **AuthModal regression** → revert. Proceed to Task 5.
- **Library/GameDetail FAIL** → Step B insufficient. Proceed to Task 5 (Step C).

- [ ] **Step 3: If Step B worked, commit**

```bash
git add apps/web/.storybook/preview.tsx
git commit -m "fix(storybook): #TBD use production IntlProvider wrapper (DS-17 Phase 4 prelude Step B)"
```

If Step B failed, revert + proceed to Task 5:

```bash
git checkout -- apps/web/.storybook/preview.tsx
```

---

### Task 5: Step C — Webpack investigation + resolve.alias

**Decision gate**: proceed only if Task 4 Step B failed. Budget allocated: 3-6h. If exceeded 1.5gg total → escalate to Task 7 (Step D fallback).

**Files:**
- Modify: `apps/web/.storybook/main.ts`

- [ ] **Step 1: Add webpack-bundle-analyzer to debug config**

Use Edit tool on `apps/web/.storybook/main.ts`. Add to `webpackFinal` (create if missing):

```ts
const config: StorybookConfig = {
  // ... existing config

  webpackFinal: async (config) => {
    // DEBUG: bundle-analyzer to find dual react-intl instances
    if (process.env.STORYBOOK_DEBUG_BUNDLE) {
      const { BundleAnalyzerPlugin } = require('webpack-bundle-analyzer');
      config.plugins.push(
        new BundleAnalyzerPlugin({
          analyzerMode: 'static',
          reportFilename: 'bundle-report.html',
          openAnalyzer: false,
        })
      );
    }
    return config;
  },
};
```

- [ ] **Step 2: Install webpack-bundle-analyzer devDep**

```bash
pnpm add -D webpack-bundle-analyzer
```

- [ ] **Step 3: Run debug build**

```bash
STORYBOOK_DEBUG_BUNDLE=1 pnpm build-storybook 2>&1 | tail -5
```

Open `storybook-static/bundle-report.html` in browser. Search for "react-intl". Verify if appears in multiple chunks (preview bundle vs iframe bundle).

- [ ] **Step 4: Apply resolve.alias fix**

Use Edit tool on `apps/web/.storybook/main.ts`. Update `webpackFinal`:

```ts
const path = require('path');

webpackFinal: async (config) => {
  // Force single react-intl module instance across all chunks (Phase 4 prelude fix)
  config.resolve.alias = {
    ...config.resolve.alias,
    'react-intl': path.resolve(__dirname, '../node_modules/react-intl'),
  };

  // Debug bundle (opt-in via env var)
  if (process.env.STORYBOOK_DEBUG_BUNDLE) {
    const { BundleAnalyzerPlugin } = require('webpack-bundle-analyzer');
    config.plugins.push(
      new BundleAnalyzerPlugin({
        analyzerMode: 'static',
        reportFilename: 'bundle-report.html',
        openAnalyzer: false,
      })
    );
  }

  return config;
},
```

**Code-reviewer I2**: verified `.storybook/main.ts` does NOT currently import `path`. Add as second import line:

```ts
import path from 'path';
```

- [ ] **Step 5: Rebuild + diagnostic test + budget check (Code-reviewer C2+I5)**

```bash
rm -rf storybook-static
pnpm build-storybook 2>&1 | tail -3
pnpm exec playwright test --config playwright.storybook.config.ts e2e/diagnostic.spec.ts 2>&1 | tail -20
```

**Budget gate (Code-reviewer I5)**:

```bash
START=$(cat /tmp/phase-4-prelude-start.txt)
NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)
ELAPSED_HOURS=$(python3 -c "from datetime import datetime; s=datetime.fromisoformat('$START'.replace('Z','')); n=datetime.fromisoformat('$NOW'.replace('Z','')); print(int((n-s).total_seconds()/3600))")
echo "Elapsed: $ELAPSED_HOURS hours (budget 1.5gg = 12 work-hours)"
[ "$ELAPSED_HOURS" -ge 12 ] && echo "ESCALATE: budget exceeded, proceed to Task 7" || echo "PROCEED: budget remaining"
```

Decision gate:
- **All 4 PASS** → Step C worked. Proceed to Task 6.
- **Library/GameDetail FAIL OR budget ≥12h** → Proceed to Task 7 (Step D fallback).
- **AuthModal regression** → revert. Proceed to Task 7.

- [ ] **Step 6: If Step C worked, commit (Code-reviewer I7+I8)**

```bash
# Discover modified lockfiles (monorepo may have root + per-app)
MODIFIED_LOCKS=$(git status --porcelain | grep "pnpm-lock.yaml" | awk '{print $2}')
echo "Modified lockfiles: $MODIFIED_LOCKS"

git add apps/web/.storybook/main.ts apps/web/package.json $MODIFIED_LOCKS
git commit -m "fix(storybook): #TBD resolve.alias force single react-intl module (DS-17 Phase 4 prelude C)"
```

**Code-reviewer I8 — Optional webpack-bundle-analyzer cleanup**:

```bash
# Decision: keep for future debugging vs remove for lean lockfile
# Recommend: REMOVE post-success (alias fix doesn't need analyzer at runtime)
pnpm remove -D webpack-bundle-analyzer
git add apps/web/package.json $MODIFIED_LOCKS
# Also remove BundleAnalyzerPlugin import + plugin push block from main.ts
git commit -m "chore: #TBD remove webpack-bundle-analyzer after Step C investigation done"
```

If Step C failed, revert + proceed to Task 7:

```bash
git checkout -- apps/web/.storybook/main.ts apps/web/package.json
pnpm install  # restore lockfile to match package.json
```

---

### Task 6: Verification phase + baseline capture (if Step A/B/C worked)

**Decision gate**: proceed con Task 6 if Step A, B, or C succeeded. Skip if all failed → Task 7 fallback.

**Files:**
- Create: `apps/web/e2e/storybook/__snapshots__/library-*.png` (9 PNGs)
- Create: `apps/web/e2e/storybook/__snapshots__/game-detail-*.png` (3 PNGs)

- [ ] **Step 1: Full verification matrix**

```bash
pnpm typecheck 2>&1 | tail -3
pnpm build-storybook 2>&1 | tail -3
grep -c "ReactIntlProvider\|IntlProvider" storybook-static/main.*.js 2>&1 | head -3
```

Expected:
- typecheck clean
- build-storybook succeeds
- IntlProvider count > 0 in main bundle

- [ ] **Step 2: Run full diagnostic suite (Code-reviewer recommendation #5: automated, not visual)**

```bash
rm -rf storybook-static && pnpm build-storybook 2>&1 | tail -3
pnpm exec playwright test --config playwright.storybook.config.ts e2e/diagnostic.spec.ts 2>&1 | tail -20
```

Expected: all 4 diagnostic stories PASS. NO "intl object missing" error wall.

- [ ] **Step 3: Capture baselines**

```bash
rm -rf test-results playwright-report apps/web/e2e/storybook/__snapshots__
pnpm test:storybook:snapshots:update 2>&1 | tail -10
```

Expected: 12 passed, 12 PNGs in `apps/web/e2e/storybook/__snapshots__/`.

If FAIL: investigate per-test errors via test-results dir. Common: story slug mismatch, page rendering issue, network mock issue.

- [ ] **Step 4: Verify snapshots committed**

```bash
ls -la apps/web/e2e/storybook/__snapshots__/ | head -20
```

Expected: 12 PNG files (9 library-*, 3 game-detail-*).

- [ ] **Step 5: Smoke test gate**

Verify gate works by modifying fixture + re-running test:

```bash
# Modify Library fixture title to force visible diff
sed -i.bak "s/title: 'Wingspan'/title: 'SMOKE TEST'/" apps/web/src/__tests__/fixtures/mockup-pilots/library.ts
pnpm test:storybook:snapshots 2>&1 | tail -5
# Expect Library Frame09 fail with diff > 5% (Wingspan title in grid card)
```

Then revert:

```bash
mv apps/web/src/__tests__/fixtures/mockup-pilots/library.ts.bak apps/web/src/__tests__/fixtures/mockup-pilots/library.ts
pnpm test:storybook:snapshots 2>&1 | tail -5
# Expect 12/12 pass
```

If smoke test fails to detect change → snapshot tolerance too loose, investigate.

If sed fails on Windows Git Bash → use node script alternative:

```bash
node -e "
const fs = require('fs');
const f = 'apps/web/src/__tests__/fixtures/mockup-pilots/library.ts';
const c = fs.readFileSync(f, 'utf-8');
fs.writeFileSync(f + '.bak', c);
fs.writeFileSync(f, c.replace(\"title: 'Wingspan'\", \"title: 'SMOKE TEST'\"));
"
```

- [ ] **Step 6: Commit baselines**

```bash
git add apps/web/e2e/storybook/__snapshots__/
git commit -m "test(snapshot): #TBD 12 baseline PNGs Library + GameDetail (DS-17 Phase 4 prelude)"
```

---

### Task 7: Step D — Custom mock alias fallback (if A+B+C failed)

**Decision gate**: proceed only if Tasks 3+4+5 all failed OR 1.5gg total elapsed without fix.

**Files:**
- Create: `apps/web/src/test-utils/__mocks__/useTranslation-storybook.ts`
- Modify: `apps/web/.storybook/main.ts`
- Modify: `apps/web/.storybook/preview.tsx` (revert any provider attempts)

- [ ] **Step 1: Create mock useTranslation for Storybook**

Write `apps/web/src/test-utils/__mocks__/useTranslation-storybook.ts`:

```ts
/**
 * Storybook-specific useTranslation mock (DS-17 Phase 4 prelude Step D fallback).
 *
 * Stub t() function returns key as-is (no actual translation lookup). Used
 * via Storybook builder webpack `resolve.alias` to bypass IntlProvider context
 * dependency when production provider integration cannot be wired.
 *
 * Trade-off: stories non testano i18n behavior reale; translation keys diventano
 * fixed strings nelle rendered UI. Snapshot pixel-faithful works for layout +
 * styling verification.
 *
 * Refs: spec docs/superpowers/specs/2026-06-10-ds-17-phase-4-prelude-intl-hardening-design.md, umbrella #2063.
 */

import type { MessageDescriptor } from 'react-intl';

export type TranslationFunction = {
  (id: string): string;
  (id: string, defaultMessage: string): string;
  (id: string, values: Record<string, string | number | boolean | Date | null | undefined>): string;
};

export interface UseTranslationReturn {
  t: TranslationFunction;
  formatMessage: (
    descriptor: MessageDescriptor,
    values?: Record<string, string | number | boolean | Date | null | undefined>
  ) => string;
  locale: string;
  formatNumber: (value: number, options?: Intl.NumberFormatOptions) => string;
  formatDate: (value: Date | number | string, options?: Intl.DateTimeFormatOptions) => string;
  formatTime: (value: Date | number | string, options?: Intl.DateTimeFormatOptions) => string;
  formatRelativeTime: (
    value: number,
    unit?: Intl.RelativeTimeFormatUnit,
    options?: Intl.RelativeTimeFormatOptions
  ) => string;
}

export function useTranslation(): UseTranslationReturn {
  const t: TranslationFunction = ((id: string, valuesOrDefault?: unknown) => {
    if (typeof valuesOrDefault === 'string') return valuesOrDefault; // defaultMessage fallback
    return id; // return key as-is
  }) as TranslationFunction;

  return {
    t,
    formatMessage: (descriptor) => String(descriptor.defaultMessage || descriptor.id || ''),
    locale: 'it',
    // Code-reviewer C1: full production surface — Library/GameDetail use date/number formatting
    formatNumber: (value, options) => new Intl.NumberFormat('it', options).format(value),
    formatDate: (value, options) => new Intl.DateTimeFormat('it', options).format(new Date(value)),
    formatTime: (value, options) =>
      new Intl.DateTimeFormat('it', { hour: 'numeric', minute: 'numeric', ...options }).format(new Date(value)),
    formatRelativeTime: (value, unit = 'second', options) =>
      new Intl.RelativeTimeFormat('it', options).format(value, unit),
  };
}

export default useTranslation;
```

- [ ] **Step 2: Wire alias in .storybook/main.ts webpackFinal**

Use Edit tool on `apps/web/.storybook/main.ts`. Update `webpackFinal`:

```ts
import path from 'path';

webpackFinal: async (config) => {
  // Step D fallback: bypass IntlProvider via custom useTranslation mock
  config.resolve.alias = {
    ...config.resolve.alias,
    '@/hooks/useTranslation': path.resolve(__dirname, '../src/test-utils/__mocks__/useTranslation-storybook.ts'),
  };
  return config;
},
```

- [ ] **Step 3: Revert preview.tsx to baseline state (no provider integration attempts)**

If Tasks 3 or 4 changes are still in working tree, revert:

```bash
git checkout -- apps/web/.storybook/preview.tsx
```

Verify preview.tsx is back to baseline (with IntlProvider wire from Phase 2.5 but without Step A/B mods).

- [ ] **Step 4: Rebuild + multi-frame diagnostic (Code-reviewer C2+I3)**

```bash
rm -rf storybook-static test-results playwright-report
pnpm build-storybook 2>&1 | tail -3
pnpm exec playwright test --config playwright.storybook.config.ts e2e/diagnostic.spec.ts 2>&1 | tail -20
```

Expected: All 4 diagnostic stories PASS (mock bypasses IntlProvider context entirely; AuthModal continues to work via production wrapper bypass).

If any FAIL: stop, raise to user — investigation insufficient even with mock fallback. Possible: production code uses `useIntl()` directly (not via `useTranslation`), needs second mock alias.

- [ ] **Step 5: Capture baselines**

```bash
rm -rf apps/web/e2e/storybook/__snapshots__
pnpm test:storybook:snapshots:update 2>&1 | tail -10
```

Expected: 12 passed.

- [ ] **Step 6: Commit Step D fix + baselines**

```bash
git add apps/web/src/test-utils/__mocks__/useTranslation-storybook.ts apps/web/.storybook/main.ts apps/web/e2e/storybook/__snapshots__/
git commit -m "fix(storybook): #TBD Step D fallback custom useTranslation mock (DS-17 Phase 4 prelude)"
```

---

### Task 8: Update docs — page-mock-story-pattern.md

**Files:**
- Modify: `docs/for-developers/frontend/page-mock-story-pattern.md`

- [ ] **Step 1: Replace Known limitation section con Fix log**

Use Edit tool on `docs/for-developers/frontend/page-mock-story-pattern.md`. Find:

```markdown
## ⚠️ Known limitation Phase 2.5 — IntlProvider runtime context
```

Replace entire `## ⚠️ Known limitation Phase 2.5 — IntlProvider runtime context` section (down to next `##` heading) with:

```markdown
## Fix log — IntlProvider hardening (Phase 4 prelude #TBD)

Phase 2.5 ship con baseline DEFERRED per IntlProvider runtime context blocker. Phase 4 prelude
investigation (sub-issue #TBD) ha applied the following fix:

### Diagnostic findings

- Control test: AuthModal renders [YES/NO] · Library Frame09 renders [YES/NO]
- Console.log [storybook AllProviders] decorator runs: [YES/NO]
- React DevTools inspection: [findings]

### Step path taken

- [x] Step A: swap decorator order (`AllProviders` before `withThemeByClassName`)
- [ ] Step B: alternative provider (`@/components/providers/IntlProvider` production wrapper)
- [ ] Step C: webpack `resolve.alias` force single react-intl module
- [ ] Step D: custom `useTranslation` mock alias (Storybook env only)

(Mark with `[x]` the step that succeeded; remove `[ ]` for steps NOT taken.)

### Root cause

[Fill with identified root cause OR note "investigation deferred — fallback applied"]

### Fix applied

[Concise description of the change merged]
```

Substitute `[YES/NO]`, `[findings]`, `[x]` checkboxes, and root cause description with actual findings from Task 2 + Task 3/4/5/7.

- [ ] **Step 2: Update Pilot table baseline status**

Find:

```markdown
| Library | `src/app/(authenticated)/library/_content.stories.tsx` | 9 Desktop (Frame09-17) | current | DEFERRED (Phase 4 — IntlProvider blocker) |
| GameDetail | `src/app/(authenticated)/games/[id]/_components/GameDetailView.stories.tsx` | 3 Desktop (Frame07-09) | current | DEFERRED (Phase 4 — IntlProvider blocker) |
```

Replace `DEFERRED (Phase 4 — IntlProvider blocker)` con `CAPTURED #TBD`.

- [ ] **Step 3: Update Snapshot gate trajectory table**

Find Phase 2.5 row:

```markdown
| ✅ **Phase 2.5 hardening** | shipped (baselines DEFERRED) | 5% area | Desktop 1440x900 | continue-on-error | 0 (IntlProvider blocker, see below) |
```

Replace with:

```markdown
| ✅ **Phase 2.5 hardening** | shipped + baselines captured Phase 4 prelude | 5% area | Desktop 1440x900 | continue-on-error | 12 (Library 9 + GameDetail 3) |
```

- [ ] **Step 4: Commit docs**

```bash
git add docs/for-developers/frontend/page-mock-story-pattern.md
git commit -m "docs(frontend): #TBD page-mock-story-pattern Fix log Phase 4 prelude (DS-17)"
```

---

### Task 9: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update DS-17 paragraph baseline note**

Use Edit tool on `CLAUDE.md`. Find existing DS-17 paragraph (search "DS-17 Phase 2.5+ (#2113)") and replace:

```markdown
**Baseline PNG capture deferred a Phase 4**: pilot stories rendono "intl object missing" error wall nonostante `ReactIntlProvider` wire'd in preview.tsx — suspected dual react-intl module instances vs Webpack chunk splitting.
```

with:

```markdown
**Baseline 12 PNGs captured** (Library 9 + GameDetail 3) post Phase 4 prelude #TBD merge. CI gate `continue-on-error: true` (`--blocking` flip post 14gg stable trajectory).
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: #TBD CLAUDE.md baseline captured note (DS-17 Phase 4 prelude)"
```

---

### Task 10: CI workflow update (DEFERRED to future PR — Code-reviewer I4)

**Files:** (none modified in this PR)

**Decision gate (Code-reviewer I4)**: Task 10 is DEFERRED in this PR per Phase 4 prelude spec. The 14gg stable trajectory is a FUTURE observation window; no prior PR runs exist yet (this PR is the FIRST to ship working baselines). Skip Task 10 unconditionally; mention in PR body 'CI gate flip deferred to follow-up PR pending 14gg observation' and proceed to Task 11.

This task remains documented as a template for the FUTURE follow-up PR that will flip CI step to blocking after observation period.

**Future PR template** (do NOT execute now):

- [ ] **Step 1: After 14gg stable**

If ≥14 PR runs observed with passing snapshot gate post-Phase 4 prelude merge, proceed to Step 2.

- [ ] **Step 2: Flip CI step to blocking (if stable)**

Use Edit tool on `.github/workflows/ci.yml`. Find:

```yaml
      - name: Storybook snapshot scaffolding (DS-17-8-v2 non-blocking)
        run: pnpm test:storybook:snapshots
        continue-on-error: true
```

Replace with:

```yaml
      - name: Storybook snapshot gate (DS-17 Phase 4)
        run: pnpm test:storybook:snapshots
        # Flipped to blocking 2026-06-10 post 14gg stable trajectory (DS-17 Phase 4 prelude #TBD).
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: #TBD Storybook snapshot gate blocking (DS-17 Phase 4 prelude)"
```

---

### Task 11: Final verification + PR + admin-squash merge + cleanup

- [ ] **Step 1: Full verification**

```bash
pnpm typecheck 2>&1 | tail -3
pnpm lint 2>&1 | tail -3
pnpm build-storybook 2>&1 | tail -3
pnpm vitest run scripts/mockup-annotations/__tests__/ 2>&1 | tail -5
pnpm test:storybook:snapshots 2>&1 | tail -5
pnpm lint:fidelity 2>&1 | tail -5
```

Expected: all clean. Snapshot 12/12 PASS. Fidelity 3 PASS.

- [ ] **Step 2: Cleanup diagnostic notes**

Verify `.diagnostic-notes.md` (created in Task 2 Step 5) is NOT in git status. If present, delete:

```bash
rm -f .diagnostic-notes.md
```

- [ ] **Step 3: Push branch**

```bash
git push -u origin feature/issue-TBD-ds-17-phase-4-prelude-intl
```

- [ ] **Step 4: Create PR with explicit body template (Code-reviewer I6)**

```bash
gh pr create --base main-dev --head feature/issue-TBD-ds-17-phase-4-prelude-intl \
  --title "fix(storybook): #TBD DS-17 Phase 4 prelude IntlProvider hardening + 12 baseline PNGs" \
  --body "$(cat <<'EOF'
## Goal

Fix IntlProvider runtime context blocker in Storybook → 12 baseline PNGs captured (Library 9 + GameDetail 3) → unblock Phase 3 inizio.

## Diagnostic findings (Task 2)

- Control test AuthModal renders: [YES/NO]
- Control test Library Frame09 renders: [YES/NO]
- Control test Library Frame13 renders: [YES/NO]
- Control test GameDetail Frame07 renders: [YES/NO]
- Hypothesis: [from diagnostic-notes]

## Step path taken

- [x or empty] Step A: swap decorator order
- [x or empty] Step B: production IntlProvider wrapper
- [x or empty] Step C: webpack resolve.alias single react-intl module
- [x or empty] Step D: custom useTranslation mock alias fallback

## Root cause

[Identified root cause OR fallback rationale]

## Baselines

12 PNG captured + committed (Library Frame09-17 + GameDetail Frame07-09). Smoke test gate verified.

## CI

Storybook snapshot gate `continue-on-error: true` unchanged — flip to blocking deferred pending 14gg stable trajectory observation (future PR per Code-reviewer I4).

Closes #TBD

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Pre-PR validation**: substitute placeholders [YES/NO], [x or empty], [from diagnostic-notes], [Identified root cause OR fallback rationale] with actual values from Tasks 2/3/4/5/7 outputs before invoking command.

- [ ] **Step 5: Admin-squash merge**

```bash
gh pr merge <PR#> --squash --admin --delete-branch \
  --subject "fix(storybook): #TBD DS-17 Phase 4 prelude IntlProvider hardening (#PR)" \
  --body "<truncated body>"
```

- [ ] **Step 6: Cleanup + sync main-dev**

```bash
git stash push -m "transient" -- .claude/scheduled_tasks.lock 2>&1 || true
git checkout main-dev
git pull --ff-only
git branch -D feature/issue-TBD-ds-17-phase-4-prelude-intl 2>&1 || true
git stash drop 0 2>&1 || true
git log --oneline -3
```

Expected: main-dev fast-forwarded to Phase 4 prelude squash merge.

---

### Task 12: Umbrella body update

**Files:**
- Modify: GitHub issue #2063 body via `gh issue edit`

- [ ] **Step 1: Fetch current umbrella body**

```bash
gh issue view 2063 --json body --jq .body > /tmp/issue-2063-body.md
grep -n "Phase 2.5\|DS-17 Phase 4 prelude" /tmp/issue-2063-body.md
```

- [ ] **Step 2: Update Phase 2.5 note + add Phase 4 prelude row**

Use Edit tool on `/tmp/issue-2063-body.md`:

- Old string: `**Baseline PNG capture deferred a Phase 4** per IntlProvider runtime context issue (suspected dual react-intl module instances vs Webpack chunk splitting).`
- New string: `**Baseline 12 PNGs captured** post DS-17 Phase 4 prelude #TBD (PR #PR \`SHA\`).`

Then find `### Phase 4 — Hardening (~1 settimana)` section and add new row at top:

```markdown
- [x] **DS-17 Phase 4 prelude — IntlProvider hardening** (#TBD): PR #PR merged `SHA` — fixed IntlProvider runtime context via Step [A/B/C/D]; 12 baseline PNGs captured (Library 9 + GameDetail 3); CI gate [blocking/non-blocking].
```

(Substitute `#TBD`, `#PR`, `SHA`, and `[A/B/C/D]` + `[blocking/non-blocking]` based on actual outcome.)

- [ ] **Step 3: Apply update**

```bash
gh issue edit 2063 --body-file /tmp/issue-2063-body.md
```

Expected: GitHub issue URL output.

- [ ] **Step 4: Verify**

```bash
gh issue view 2063 --json body --jq .body | grep -A2 "Phase 4 prelude"
```

Expected: new Phase 4 prelude row visible.

---

## Self-review checklist

(Ran inline after writing — fixes applied where issues found.)

**1. Spec coverage:**
- §3.1 Diagnostic → Task 2 ✓
- §3.2 Step A swap order → Task 3 ✓
- §3.2 Step B alternative provider → Task 4 ✓
- §3.2 Step C webpack investigation → Task 5 ✓
- §3.2 Step D fallback mock → Task 7 ✓
- §3.3 Verification matrix → Task 6 ✓
- §4 Time budget + escalation → embedded in each Task decision gates ✓
- §5 Docs update — page-mock-story-pattern.md → Task 8 ✓
- §5 Docs update — CLAUDE.md → Task 9 ✓
- §5 CI workflow update → Task 10 (optional) ✓
- §5 Umbrella body update → Task 12 ✓

**2. Placeholder scan:**
- `#TBD` for sub-issue # + `#PR` for PR # + `SHA` for merge commit + `[A/B/C/D]` for step taken: ALL intentional (executor fills in based on actual outcomes during Tasks 1, 11, 12).
- "TBD" elsewhere: 0 hits ✓
- `[YES/NO]` placeholders in Task 8 Step 1: intentional (executor fills from Task 2 diagnostic findings)

**3. Type consistency:**
- `ReactIntlProvider` (Step A baseline) → `IntlProvider` (Step B production wrapper) → `react-intl alias` (Step C) → `useTranslation mock alias` (Step D) — each step's references consistent ✓
- 12 baseline PNGs counted correctly (Library 9 Frame09-17 + GameDetail 3 Frame07-09) ✓
- Snapshot slug pattern `pages-sp4-X-mockup-matrix--frame-NN-*` consistent ✓

**4. Spec gaps:** none identified.

**5. Decision gates explicit:**
- Each Task 3/4/5/7 has decision gate to escalate
- Task 6 has "if Step A/B/C worked" gate
- Task 7 has "if all previous failed" gate
- Task 10 has "if 14gg stable" gate

All issues fixed inline. No re-review needed.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-10-ds-17-phase-4-prelude-intl-hardening-plan.md`. Two execution options:

**1. Subagent-Driven (recommended)** — dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — execute tasks in this session using executing-plans, batch execution with checkpoints.

User has previously requested "scrivi piano, review piano, implementa piano" pattern suggesting inline execution after agent-based review.

Proceeding with: **agent code-review of plan first** → **inline execution with checkpoints**.
