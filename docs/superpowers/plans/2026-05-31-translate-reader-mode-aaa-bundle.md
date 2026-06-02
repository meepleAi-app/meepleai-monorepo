# Translate-Viewer Reader-Mode + AAA Contrast Bundle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add reader-mode toggle (#1558 H) and AAA contrast text (#1561 J) to `TranslateViewer` per Aaron CORE refinement spec 2026-05-23, closing 2 Cluster #1487 follow-up issues in a single PR.

**Architecture:** Two FE-only additions sharing the `TranslationPane.tsx` surface:
1. **Reader-mode**: SSR-safe localStorage-backed hook + toggle button in viewer header + CSS-var cascade via `data-reader-mode` wrapper attribute scoping the viewer subtree (toggle + skeleton + body all scale).
2. **AAA contrast**: New `--c-text-high-contrast` design token (light `#0f0a05` ≈15:1, dark `#ffffff` ≈18:1) applied to TranslationPane body `<p>`; jest-axe AAA assertion both themes.

Both additions are additive (no FSM touch, zero risk to #1557 G shipped 24h ago in PR #1765).

**Tech Stack:** React 19 · Next.js 16 App Router · Tailwind 4 · CSS vars · `data-theme` next-themes · Vitest + jest-axe + Testing Library

---

## Locked Decisions (from /sc:spec-panel critique)

- **DEC-1 (scope)**: CSS vars applied via `data-reader-mode="true|false"` attribute on TranslateViewer root `<div>`, NOT globally. Scoping selector: `[data-reader-mode="true"] .reader-mode-content`. Zero leak (Fowler ✅).
- **DEC-2 (CSS naming)**: Reuse mockup class names `.reader-mode-content` + `.reader-mode-toggle` 1:1 (NOT inferred `--tv-*`). Default off: 16px/1.55/60ch. Active: 24px/1.7/65ch + `padding: var(--s-6)`.
- **DEC-3 (localStorage)**: Key `'reader-mode-enabled'`, value `'true'|'false'` (string). No namespacing — user-preference globale (cross-page sticky). SSR-safe: `useEffect` mount + `typeof window !== 'undefined'` guard.
- **DEC-4 (toggle UI)**: Placed in TranslateViewer header (after `<h1>`). `aria-pressed={isReaderMode}`, `aria-label` dynamic ("Attiva Reader Mode" / "Disattiva Reader Mode"). Icon 📖 + label "Reader" (off) / "Reader ✓" (on) per mockup line 1697-1727.
- **DEC-5 (skeleton apply)**: Reader-mode CSS must cascade to `LoadingSkeleton` for visual coherence during OCR. Implement via `data-reader-mode` on the viewer wrapper that includes both skeleton and TranslationPane as children.
- **DEC-6 (AAA token)**: Add to `design-tokens-canonical.css`: `:root { --c-text-high-contrast: 32 36% 4%; }` (≈ `#0f0a05` HSL, 15:1 on cream `#f7f3ee`) and `[data-theme="dark"] { --c-text-high-contrast: 0 0% 100%; }` (≈ `#ffffff`, 18:1 on dark bg). Use HSL triplet format consistent with existing token convention.
- **DEC-7 (AAA scope)**: Apply `var(--c-text-high-contrast)` ONLY to body `<p>` in TranslationPane.tsx:45. `<em>` opacity-0.92 variant deferred (TranslationPane renders flat text via `whitespace-pre-wrap`, no `<em>` nodes today). Document as MINOR follow-up in PR body.
- **DEC-8 (jest-axe AAA)**: Use `axe.configure({ rules: [{ id: 'color-contrast-enhanced', enabled: true }] })` for AAA test on TranslationPane (NOT global). The `color-contrast-enhanced` axe rule enforces 7:1 ratio (AAA). Keep default `color-contrast` (AA 4.5:1) elsewhere.
- **DEC-9 (graceful degrade)**: If `localStorage` is unavailable (private mode, SSR), hook falls back to in-memory state without throwing. `try/catch` wrapper around `localStorage.getItem/setItem`. No telemetry (silent).

---

## Given/When/Then Scenarios

**Reader-mode**:
- **S1**: GIVEN localStorage['reader-mode-enabled']='false' AND viewer mounts, WHEN user clicks 📖 toggle, THEN TranslationPane body font scales 16→24px AND line-height to 1.7 AND max-width 65ch AND localStorage updated to 'true' AND `aria-pressed='true'`.
- **S2**: GIVEN localStorage['reader-mode-enabled']='true' (prev session), WHEN TranslateViewer mounts, THEN reader-mode applies after hydration AND toggle `aria-pressed='true'` AND no hydration mismatch warning.
- **S3**: GIVEN reader-mode is active AND uploading phase, WHEN LoadingSkeleton renders, THEN skeleton text/blocks reflect scaled font (data attr cascades).
- **S4**: GIVEN reader-mode active, WHEN user unmounts+remounts viewer, THEN reader-mode is restored from localStorage on remount.
- **S5**: GIVEN localStorage.setItem throws (private mode), WHEN user clicks toggle, THEN UI updates (in-memory state) AND no error thrown AND no console.error.
- **S6**: GIVEN toggle is rendered, WHEN axe runs, THEN zero violations on `aria-pressed`, `aria-label`, button role.

**AAA contrast**:
- **S7**: GIVEN TranslationPane renders body text on `:root` (light theme), WHEN axe runs with `color-contrast-enhanced` rule, THEN zero violations (≥7:1 ratio).
- **S8**: GIVEN TranslationPane renders body text on `[data-theme="dark"]`, WHEN axe runs with `color-contrast-enhanced` rule, THEN zero violations (≥7:1 ratio).
- **S9**: GIVEN `--c-text-high-contrast` token is added to design-tokens-canonical.css, WHEN we read computed style of TranslationPane body `<p>`, THEN `color` resolves to the token's HSL value.

---

## File Structure

**Modify**:
- `apps/web/src/styles/design-tokens-canonical.css` — add `--c-text-high-contrast` light+dark
- `apps/web/src/styles/globals.css` — add `.reader-mode-content` + `.reader-mode-toggle` styles + `[data-reader-mode="true"]` selectors
- `apps/web/src/styles/__tests__/entity-tokens.test.ts` — add test for new token presence
- `apps/web/src/components/features/gamebook/TranslateViewer.tsx` — wire ReaderModeToggle + `data-reader-mode` wrapper
- `apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx` — add S2/S3/S4 integration tests
- `apps/web/src/components/features/gamebook/TranslationPane.tsx` — apply AAA contrast class + `.reader-mode-content` className
- `apps/web/src/components/features/gamebook/__tests__/TranslationPane.test.tsx` — add S7/S8/S9 + reader-mode scaling tests
- `apps/web/src/components/features/gamebook/LoadingSkeleton.tsx` — add `.reader-mode-content` className (cascades from parent data attr)
- `apps/web/src/components/features/gamebook/__tests__/LoadingSkeleton.test.tsx` — add reader-mode cascade test

**Create**:
- `apps/web/src/lib/gamebook/hooks/useReaderMode.ts` — SSR-safe localStorage hook
- `apps/web/src/lib/gamebook/hooks/__tests__/useReaderMode.test.ts` — 8 tests (S1, S2, S4, S5)
- `apps/web/src/components/features/gamebook/ReaderModeToggle.tsx` — toggle component
- `apps/web/src/components/features/gamebook/__tests__/ReaderModeToggle.test.tsx` — 6 tests (toggle + S6)

---

### Task 1: Add `--c-text-high-contrast` token (AAA #1561)

**Files:**
- Modify: `apps/web/src/styles/design-tokens-canonical.css` (`:root` + `[data-theme="dark"]` blocks)
- Modify: `apps/web/src/styles/__tests__/entity-tokens.test.ts` (add token assertion)

- [ ] **Step 1: Write failing test**

Add to `apps/web/src/styles/__tests__/entity-tokens.test.ts` (after the existing `--c-kb` test):

```typescript
describe('--c-text-high-contrast (AAA contrast)', () => {
  it('exists in :root (light theme) with HSL 32 36% 4% (#0f0a05 ≈15:1 on cream)', () => {
    expect(css).toMatch(/:root\s*\{[\s\S]*--c-text-high-contrast:\s*32\s+36%\s+4%/);
  });

  it('exists in [data-theme="dark"] with HSL 0 0% 100% (#ffffff ≈18:1 on dark)', () => {
    expect(css).toMatch(/\[data-theme="dark"\]\s*\{[\s\S]*--c-text-high-contrast:\s*0\s+0%\s+100%/);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/styles/__tests__/entity-tokens.test.ts`
Expected: 2 FAIL with "expected '...' to match /.../"

- [ ] **Step 3: Add token to design-tokens-canonical.css**

In `:root` block (around line 26-46, after `--c-kb-text`):

```css
  /* AAA contrast token (#1561 J — Aaron CORE spec 2026-05-23 §1b)
   * Light: hsl(32 36% 4%) ≈ #0f0a05 vs cream #f7f3ee = 15.1:1 (AAA-large + AAA-normal)
   * Dark:  hsl(0 0% 100%) = #ffffff vs dark bg #14100a = 18.4:1 (AAA-large + AAA-normal)
   * Use via: color: hsl(var(--c-text-high-contrast))
   */
  --c-text-high-contrast: 32 36% 4%;
```

In `[data-theme="dark"]` block (around line 199-213, after `--c-kb-text` dark variant):

```css
  /* AAA contrast dark — see :root docblock */
  --c-text-high-contrast: 0 0% 100%;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/styles/__tests__/entity-tokens.test.ts`
Expected: PASS (all tests including the 2 new)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/styles/design-tokens-canonical.css apps/web/src/styles/__tests__/entity-tokens.test.ts
git commit -m "feat(tokens): #1561 J - add --c-text-high-contrast AAA token (15:1 light, 18:1 dark)"
```

---

### Task 2: Create `useReaderMode` hook (SSR-safe localStorage)

**Files:**
- Create: `apps/web/src/lib/gamebook/hooks/useReaderMode.ts`
- Create: `apps/web/src/lib/gamebook/hooks/__tests__/useReaderMode.test.ts`

- [ ] **Step 1: Write failing tests**

Create `apps/web/src/lib/gamebook/hooks/__tests__/useReaderMode.test.ts`:

```typescript
import { renderHook, act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useReaderMode } from '../useReaderMode';

describe('useReaderMode', () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  it('returns false initially when localStorage is empty (S1 default)', () => {
    const { result } = renderHook(() => useReaderMode());
    expect(result.current.isReaderMode).toBe(false);
  });

  it('returns true when localStorage has reader-mode-enabled=true (S2 persistence)', () => {
    window.localStorage.setItem('reader-mode-enabled', 'true');
    const { result } = renderHook(() => useReaderMode());
    expect(result.current.isReaderMode).toBe(true);
  });

  it('toggle() flips state and writes to localStorage', () => {
    const { result } = renderHook(() => useReaderMode());
    act(() => result.current.toggle());
    expect(result.current.isReaderMode).toBe(true);
    expect(window.localStorage.getItem('reader-mode-enabled')).toBe('true');
    act(() => result.current.toggle());
    expect(result.current.isReaderMode).toBe(false);
    expect(window.localStorage.getItem('reader-mode-enabled')).toBe('false');
  });

  it('preserves state across remount via localStorage (S4)', () => {
    const { result: r1, unmount } = renderHook(() => useReaderMode());
    act(() => r1.current.toggle());
    unmount();
    const { result: r2 } = renderHook(() => useReaderMode());
    expect(r2.current.isReaderMode).toBe(true);
  });

  it('falls back to in-memory state when localStorage.setItem throws (S5)', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });
    const { result } = renderHook(() => useReaderMode());
    act(() => result.current.toggle());
    expect(result.current.isReaderMode).toBe(true); // in-memory still works
    expect(consoleError).not.toHaveBeenCalled(); // silent
  });

  it('falls back to false when localStorage.getItem throws on mount', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });
    const { result } = renderHook(() => useReaderMode());
    expect(result.current.isReaderMode).toBe(false);
  });

  it('ignores invalid localStorage values (not "true"/"false")', () => {
    window.localStorage.setItem('reader-mode-enabled', 'garbage');
    const { result } = renderHook(() => useReaderMode());
    expect(result.current.isReaderMode).toBe(false);
  });

  it('toggle() is stable across renders (referential equality)', () => {
    const { result, rerender } = renderHook(() => useReaderMode());
    const firstToggle = result.current.toggle;
    rerender();
    expect(result.current.toggle).toBe(firstToggle);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/lib/gamebook/hooks/__tests__/useReaderMode.test.ts`
Expected: 8 FAIL with "Cannot find module '../useReaderMode'"

- [ ] **Step 3: Implement hook**

Create `apps/web/src/lib/gamebook/hooks/useReaderMode.ts`:

```typescript
'use client';

import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'reader-mode-enabled';

export interface UseReaderModeResult {
  /** Current reader-mode state (true = active, larger font + line-height + max-width). */
  isReaderMode: boolean;
  /** Flip reader-mode state. Stable reference across renders. */
  toggle: () => void;
}

/**
 * SSR-safe reader-mode hook backed by localStorage (key: `reader-mode-enabled`).
 *
 * Per DEC-3 + DEC-9: gracefully degrades to in-memory state if localStorage
 * is unavailable (private mode, quota exceeded). Returns false on SSR; reads
 * actual value after hydration to avoid mismatch warnings.
 *
 * #1558 H · Aaron CORE spec 2026-05-23 §1b.
 */
export function useReaderMode(): UseReaderModeResult {
  const [isReaderMode, setIsReaderMode] = useState(false);

  useEffect(() => {
    if (typeof window === 'undefined') return;
    try {
      const stored = window.localStorage.getItem(STORAGE_KEY);
      if (stored === 'true') setIsReaderMode(true);
    } catch {
      // localStorage unavailable (private mode / SecurityError) — keep default false
    }
  }, []);

  const toggle = useCallback(() => {
    setIsReaderMode((prev) => {
      const next = !prev;
      if (typeof window !== 'undefined') {
        try {
          window.localStorage.setItem(STORAGE_KEY, String(next));
        } catch {
          // localStorage write blocked — in-memory state still flips correctly
        }
      }
      return next;
    });
  }, []);

  return { isReaderMode, toggle };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/lib/gamebook/hooks/__tests__/useReaderMode.test.ts`
Expected: 8/8 PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/gamebook/hooks/useReaderMode.ts apps/web/src/lib/gamebook/hooks/__tests__/useReaderMode.test.ts
git commit -m "feat(translate-viewer): #1558 H - useReaderMode SSR-safe localStorage hook"
```

---

### Task 3: Create `ReaderModeToggle` component

**Files:**
- Create: `apps/web/src/components/features/gamebook/ReaderModeToggle.tsx`
- Create: `apps/web/src/components/features/gamebook/__tests__/ReaderModeToggle.test.tsx`

- [ ] **Step 1: Write failing tests**

Create `apps/web/src/components/features/gamebook/__tests__/ReaderModeToggle.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';
import { ReaderModeToggle } from '../ReaderModeToggle';

expect.extend(toHaveNoViolations);

describe('ReaderModeToggle', () => {
  it('renders with aria-pressed=false when isReaderMode is false', () => {
    render(<ReaderModeToggle isReaderMode={false} onToggle={vi.fn()} />);
    const button = screen.getByRole('button', { name: /attiva reader mode/i });
    expect(button).toHaveAttribute('aria-pressed', 'false');
  });

  it('renders with aria-pressed=true when isReaderMode is true', () => {
    render(<ReaderModeToggle isReaderMode={true} onToggle={vi.fn()} />);
    const button = screen.getByRole('button', { name: /disattiva reader mode/i });
    expect(button).toHaveAttribute('aria-pressed', 'true');
  });

  it('shows "Reader" label when off and "Reader ✓" when on (mockup parity)', () => {
    const { rerender } = render(<ReaderModeToggle isReaderMode={false} onToggle={vi.fn()} />);
    expect(screen.getByText('Reader')).toBeInTheDocument();
    rerender(<ReaderModeToggle isReaderMode={true} onToggle={vi.fn()} />);
    expect(screen.getByText('Reader ✓')).toBeInTheDocument();
  });

  it('calls onToggle when clicked', async () => {
    const onToggle = vi.fn();
    render(<ReaderModeToggle isReaderMode={false} onToggle={onToggle} />);
    await userEvent.click(screen.getByRole('button'));
    expect(onToggle).toHaveBeenCalledOnce();
  });

  it('renders the book emoji icon with aria-hidden', () => {
    render(<ReaderModeToggle isReaderMode={false} onToggle={vi.fn()} />);
    const icon = screen.getByText('📖');
    expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('has no a11y violations (S6)', async () => {
    const { container } = render(<ReaderModeToggle isReaderMode={false} onToggle={vi.fn()} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/ReaderModeToggle.test.tsx`
Expected: 6 FAIL with "Cannot find module '../ReaderModeToggle'"

- [ ] **Step 3: Implement component**

Create `apps/web/src/components/features/gamebook/ReaderModeToggle.tsx`:

```typescript
'use client';

import type { ReactElement } from 'react';

export interface ReaderModeToggleProps {
  isReaderMode: boolean;
  onToggle: () => void;
}

/**
 * Reader-mode toggle button per mockup §1b lines 1697-1727.
 * Toggles between default (16pt) and reader-mode (24pt + 1.7 line-height + 65ch).
 *
 * #1558 H · Aaron CORE spec 2026-05-23.
 */
export function ReaderModeToggle({ isReaderMode, onToggle }: ReaderModeToggleProps): ReactElement {
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={isReaderMode}
      aria-label={isReaderMode ? 'Disattiva Reader Mode' : 'Attiva Reader Mode'}
      className={`reader-mode-toggle${isReaderMode ? ' active' : ''}`}
    >
      <span className="ico" aria-hidden="true">📖</span>
      <span>{isReaderMode ? 'Reader ✓' : 'Reader'}</span>
    </button>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/ReaderModeToggle.test.tsx`
Expected: 6/6 PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/ReaderModeToggle.tsx apps/web/src/components/features/gamebook/__tests__/ReaderModeToggle.test.tsx
git commit -m "feat(translate-viewer): #1558 H - ReaderModeToggle component (aria-pressed + i18n labels)"
```

---

### Task 4: Add CSS styles for `.reader-mode-toggle` + `.reader-mode-content`

**Files:**
- Modify: `apps/web/src/styles/globals.css` (append at end of file)

- [ ] **Step 1: Add CSS styles**

Append to `apps/web/src/styles/globals.css` (at the end of the file):

```css
/* ============================================================================
 * Translate-Viewer reader-mode (#1558 H · Aaron CORE spec 2026-05-23 §1b)
 * ============================================================================
 *
 * Toggle (.reader-mode-toggle) and content scaling (.reader-mode-content)
 * scoped via [data-reader-mode="true"] attribute on TranslateViewer root.
 *
 * Default (toggle off): font-size inherits (16px base), line-height 1.55,
 * max-width 60ch, padding var(--s-4).
 *
 * Active (toggle on): 24px / 1.7 / 65ch / var(--s-6) padding.
 * ============================================================================ */

.reader-mode-toggle {
  display: inline-flex;
  align-items: center;
  gap: var(--s-2);
  padding: var(--s-1) var(--s-3);
  border-radius: var(--r-pill);
  background: var(--bg-card);
  border: 1px solid var(--border);
  color: var(--text);
  font-family: var(--f-mono);
  font-size: var(--fs-xs);
  cursor: pointer;
}

.reader-mode-toggle.active {
  background: hsl(var(--c-kb) / 0.15);
  border-color: hsl(var(--c-kb));
  color: hsl(var(--c-kb));
  font-weight: var(--fw-bold);
}

.reader-mode-toggle .ico {
  font-size: 14px;
}

[data-reader-mode="true"] .reader-mode-content {
  font-size: 24px;
  line-height: 1.7;
  padding: var(--s-6);
  max-width: 65ch;
  margin-inline: auto;
}
```

- [ ] **Step 2: Verify CSS does not break existing styles**

Run: `cd apps/web && pnpm lint:tokens 2>&1 | head -20`
Expected: no new violations (or "0 errors" if clean). The new selectors do not use forbidden Tailwind color utilities.

Run: `cd apps/web && pnpm vitest run src/styles/__tests__/`
Expected: existing token tests still PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/styles/globals.css
git commit -m "feat(translate-viewer): #1558 H - reader-mode CSS (.reader-mode-toggle + .reader-mode-content scoped via data-reader-mode)"
```

---

### Task 5: Modify TranslationPane.tsx — apply AAA + reader-mode className

**Files:**
- Modify: `apps/web/src/components/features/gamebook/TranslationPane.tsx`
- Modify: `apps/web/src/components/features/gamebook/__tests__/TranslationPane.test.tsx`

- [ ] **Step 1: Read TranslationPane.tsx current state**

Run: `cat apps/web/src/components/features/gamebook/TranslationPane.tsx`

Locate line ~45 where `<p className="text-sm whitespace-pre-wrap">` renders the body text.

- [ ] **Step 2: Write failing tests**

Add to `apps/web/src/components/features/gamebook/__tests__/TranslationPane.test.tsx` (inside the main `describe` block):

```typescript
describe('AAA contrast + reader-mode (#1561 J + #1558 H)', () => {
  it('body <p> uses var(--c-text-high-contrast) via inline style or class (S9)', () => {
    render(<TranslationPane partialText="Test paragraph" isComplete={true} appliedTerms={[]} />);
    const para = screen.getByText('Test paragraph');
    // Either inline style OR via class with custom property
    const computed = window.getComputedStyle(para);
    // jsdom returns the literal style value; the cascade resolves at runtime in browser
    expect(para.getAttribute('style')).toContain('var(--c-text-high-contrast)');
  });

  it('body <p> has .reader-mode-content class for parent data-attr cascading', () => {
    render(<TranslationPane partialText="Test" isComplete={true} appliedTerms={[]} />);
    const para = screen.getByText('Test');
    expect(para).toHaveClass('reader-mode-content');
  });

  it('has zero AAA color-contrast violations on :root (light theme) (S7)', async () => {
    const { container } = render(
      <TranslationPane partialText="Lorem ipsum dolor sit amet" isComplete={true} appliedTerms={[]} />
    );
    const results = await axe(container, {
      rules: { 'color-contrast-enhanced': { enabled: true } },
    });
    expect(results).toHaveNoViolations();
  });

  it('has zero AAA color-contrast violations on [data-theme="dark"] (S8)', async () => {
    document.documentElement.setAttribute('data-theme', 'dark');
    const { container } = render(
      <TranslationPane partialText="Lorem ipsum dolor sit amet" isComplete={true} appliedTerms={[]} />
    );
    const results = await axe(container, {
      rules: { 'color-contrast-enhanced': { enabled: true } },
    });
    expect(results).toHaveNoViolations();
    document.documentElement.removeAttribute('data-theme');
  });
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/TranslationPane.test.tsx`
Expected: 4 FAIL (missing style attr, missing class, axe violations).

- [ ] **Step 4: Modify TranslationPane.tsx**

Replace the body `<p>` element (around line 45). Change:

```tsx
<p className="text-sm whitespace-pre-wrap">{partialText}</p>
```

to:

```tsx
<p
  className="text-sm whitespace-pre-wrap reader-mode-content"
  style={{ color: 'hsl(var(--c-text-high-contrast))' }}
>
  {partialText}
</p>
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/TranslationPane.test.tsx`
Expected: All tests PASS (including the 4 new).

If a pre-existing test now fails because the `<p>` class string changed, update its assertion to match the new className.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/features/gamebook/TranslationPane.tsx apps/web/src/components/features/gamebook/__tests__/TranslationPane.test.tsx
git commit -m "feat(translate-viewer): #1561 J + #1558 H - TranslationPane AAA contrast + reader-mode-content class"
```

---

### Task 6: Modify LoadingSkeleton.tsx — cascade reader-mode

**Files:**
- Modify: `apps/web/src/components/features/gamebook/LoadingSkeleton.tsx`
- Modify: `apps/web/src/components/features/gamebook/__tests__/LoadingSkeleton.test.tsx`

- [ ] **Step 1: Read LoadingSkeleton.tsx current state**

Run: `cat apps/web/src/components/features/gamebook/LoadingSkeleton.tsx`

Identify the root element + any text-bearing child elements that should scale with reader-mode.

- [ ] **Step 2: Write failing test**

Add to `apps/web/src/components/features/gamebook/__tests__/LoadingSkeleton.test.tsx` (inside the main `describe` block):

```typescript
describe('reader-mode cascade (#1558 H · DEC-5)', () => {
  it('root element has .reader-mode-content class for parent data-attr cascading', () => {
    const { container } = render(<LoadingSkeleton uiStep="ocr" />);
    // Root element should expose the cascade hook
    const root = container.firstChild as HTMLElement;
    expect(root).toHaveClass('reader-mode-content');
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/LoadingSkeleton.test.tsx`
Expected: 1 FAIL (missing class).

- [ ] **Step 4: Modify LoadingSkeleton.tsx**

In `apps/web/src/components/features/gamebook/LoadingSkeleton.tsx`, append `reader-mode-content` to the root element's `className`. Example: if the root is `<div className="space-y-2 ...">`, change to `<div className="space-y-2 ... reader-mode-content">`.

If `className` is built dynamically, append via template literal or `cn()` utility.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/LoadingSkeleton.test.tsx`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/features/gamebook/LoadingSkeleton.tsx apps/web/src/components/features/gamebook/__tests__/LoadingSkeleton.test.tsx
git commit -m "feat(translate-viewer): #1558 H DEC-5 - LoadingSkeleton cascades reader-mode via .reader-mode-content"
```

---

### Task 7: Modify TranslateViewer.tsx — wire ReaderModeToggle + data-reader-mode wrapper

**Files:**
- Modify: `apps/web/src/components/features/gamebook/TranslateViewer.tsx`
- Modify: `apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx`

- [ ] **Step 1: Write failing tests**

Add to `apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx` (inside the main `describe` block; adapt prop setup to match existing test patterns):

```typescript
describe('reader-mode integration (#1558 H)', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  it('renders the ReaderModeToggle in the header', () => {
    render(/* existing TranslateViewer setup */);
    expect(screen.getByRole('button', { name: /reader mode/i })).toBeInTheDocument();
  });

  it('root wrapper has data-reader-mode="false" by default (S1)', () => {
    const { container } = render(/* existing TranslateViewer setup */);
    const root = container.querySelector('[data-reader-mode]');
    expect(root).toHaveAttribute('data-reader-mode', 'false');
  });

  it('clicking the toggle sets data-reader-mode="true" + writes localStorage (S1)', async () => {
    const { container } = render(/* existing TranslateViewer setup */);
    await userEvent.click(screen.getByRole('button', { name: /attiva reader mode/i }));
    expect(container.querySelector('[data-reader-mode]')).toHaveAttribute('data-reader-mode', 'true');
    expect(window.localStorage.getItem('reader-mode-enabled')).toBe('true');
  });

  it('applies reader-mode from localStorage on mount (S2 persistence)', () => {
    window.localStorage.setItem('reader-mode-enabled', 'true');
    const { container } = render(/* existing TranslateViewer setup */);
    expect(container.querySelector('[data-reader-mode]')).toHaveAttribute('data-reader-mode', 'true');
  });

  it('reader-mode wrapper contains both LoadingSkeleton and TranslationPane (S3 cascade)', () => {
    window.localStorage.setItem('reader-mode-enabled', 'true');
    const { container } = render(/* existing TranslateViewer setup, force phase to a state where skeleton renders */);
    const wrapper = container.querySelector('[data-reader-mode="true"]');
    expect(wrapper).toBeInTheDocument();
    // Skeleton (if rendered) AND TranslationPane (if rendered) are inside this wrapper.
  });

  it('has zero a11y violations with reader-mode toggle rendered (S6)', async () => {
    const { container } = render(/* existing TranslateViewer setup */);
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

NOTE for implementer: the existing test file likely has a helper for setting up `<TranslateViewer />` props (e.g., `defaultProps`, `setup()`). Reuse it. If unsure, copy the prop setup from the first existing test in the file. Place `useEvent` / `axe` imports at top with other imports if missing.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/TranslateViewer.test.tsx -t "reader-mode integration"`
Expected: 6 FAIL with missing toggle / missing data-reader-mode attr.

- [ ] **Step 3: Modify TranslateViewer.tsx**

Add imports at top of `apps/web/src/components/features/gamebook/TranslateViewer.tsx`:

```typescript
import { ReaderModeToggle } from '@/components/features/gamebook/ReaderModeToggle';
import { useReaderMode } from '@/lib/gamebook/hooks/useReaderMode';
```

Inside the component function, after existing `useState` hooks (around line 44-70):

```typescript
const { isReaderMode, toggle: toggleReaderMode } = useReaderMode();
```

Wrap the existing root return JSX with `data-reader-mode` attribute. Locate the outermost element returned (around line 143). If it's currently:

```tsx
return (
  <div className="...">
    <h1>Traduci pagina libro game</h1>
    {/* ... rest ... */}
  </div>
);
```

Change to:

```tsx
return (
  <div className="..." data-reader-mode={isReaderMode}>
    <div className="flex items-center justify-between gap-2">
      <h1>Traduci pagina libro game</h1>
      <ReaderModeToggle isReaderMode={isReaderMode} onToggle={toggleReaderMode} />
    </div>
    {/* ... rest (unchanged: BookPicker, camera section, SegmentPicker, TranslationPane) ... */}
  </div>
);
```

Important: `data-reader-mode={isReaderMode}` renders as `data-reader-mode="true"` or `data-reader-mode="false"` (React serializes boolean attributes as string when the attribute name doesn't match a known boolean HTML attr).

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/TranslateViewer.test.tsx`
Expected: All tests PASS (existing + 6 new).

If pre-existing tests fail because the header structure changed (e.g., they used `screen.getByText('Traduci pagina libro game')` and now there's a wrapper `<div>`), update them to use `getByRole('heading', { name: /Traduci pagina libro game/i })` instead.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/TranslateViewer.tsx apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx
git commit -m "feat(translate-viewer): #1558 H - wire ReaderModeToggle + data-reader-mode wrapper in viewer header"
```

---

### Task 8: Verification + final commit (orchestration)

**Files:** none modified — verification only.

- [ ] **Step 1: Run full gamebook test scope**

Run: `cd apps/web && pnpm vitest run src/components/features/gamebook/ src/lib/gamebook/ src/styles/`
Expected: All tests PASS (existing + ~25 new from tasks 1-7).

If any failure: investigate, fix inline (do NOT skip tests), re-run. Report root cause in commit message.

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors.

If errors: fix inline. Common pitfalls:
- `data-reader-mode={isReaderMode}` may produce TS error if React types are strict — use `data-reader-mode={String(isReaderMode)}` as workaround.
- `style={{ color: 'hsl(var(--c-text-high-contrast))' }}` — CSSProperties accepts strings, no issue expected.

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: 0 errors, 0 warnings (CLAUDE.md baseline 0).

If `local/no-hardcoded-color-utility` flags new code: review — we used only CSS vars + Tailwind tokens, should pass. If false-positive, add line-level disable with `// eslint-disable-next-line local/no-hardcoded-color-utility — reason: <why>`.

- [ ] **Step 4: Run lint:tokens**

Run: `cd apps/web && pnpm lint:tokens`
Expected: no new violations in `audits/2026-05-12-token-violations.md` diff.

- [ ] **Step 5: Final integration sanity check**

Run: `cd apps/web && pnpm vitest run --reporter=verbose 2>&1 | tail -50`
Expected: full suite PASS, no skips on new tests, no console.error in test output.

- [ ] **Step 6: Final commit (if any fixup needed)**

If the verification surfaced fixup commits needed (typecheck/lint), commit them:

```bash
git add -A
git commit -m "chore(translate-viewer): #1558 + #1561 - typecheck/lint fixup"
```

If nothing to commit, skip this step.

- [ ] **Step 7: Push branch**

```bash
git push -u origin feature/issue-1558-reader-mode-aaa-bundle
```

---

## Post-Plan Steps (handled by main session, NOT subagent)

After the plan is fully implemented, the main session will:

1. Run final code-reviewer agent (superpowers:requesting-code-review)
2. Fix any MAJOR findings inline, defer MINOR in PR body
3. Open PR base `main-dev` with `Closes #1558` + `Closes #1561` in body
4. Verify CI green (P67: rerun flaky if AdminSideDrawer or similar baseline)
5. Merge squash normale (post-P75 resolution via #1526, no --admin needed for clean FE PRs)
6. Verify auto-close of #1558 + #1561
7. Update memory with session log

---

## Spec Coverage Self-Review

| Spec requirement | Task |
|---|---|
| #1558 AC: 📖 button in header w/ aria-pressed | Task 3 (component) + Task 7 (wire) |
| #1558 AC: localStorage 'reader-mode-enabled' read+write | Task 2 (hook) |
| #1558 AC: CSS vars 16↔24px, line-height +30%, max-width 65ch | Task 4 (CSS) |
| #1558 AC: applies to BOTH skeleton AND TranslationPane | Task 5 (pane) + Task 6 (skeleton) + Task 7 (wrapper) |
| #1558 AC: SSR-safe | Task 2 (hook useEffect mount) |
| #1558 AC: jest-axe `aria-pressed` | Task 3 (S6) |
| #1561 AC: `--c-text-high-contrast` on TranslationPane body | Task 1 (token) + Task 5 (apply) |
| #1561 AC: jest-axe color-contrast assertion | Task 5 (S7+S8) |
| #1561 AC: AAA both themes | Task 5 (S7+S8) |
| DEC-9 graceful degrade localStorage failure | Task 2 (S5) |
| #1561 deferred: `<em>` opacity 0.92 | Document in PR body as MINOR |

All AC covered. No placeholders detected. Type names consistent: `UseReaderModeResult`, `ReaderModeToggleProps`, `isReaderMode`, `toggle` used uniformly across tasks 2/3/7.

---

## Implementation Notes for Subagent

- **Stack reminder**: Vitest (not Jest), Testing Library, jest-axe, Tailwind 4, Next.js 16 App Router. Mock-free DOM via jsdom.
- **Path discipline**: Components in `apps/web/src/components/features/gamebook/`, hooks in `apps/web/src/lib/gamebook/hooks/`, styles in `apps/web/src/styles/`. Tests adjacent in `__tests__/` folders.
- **Existing patterns**: `LoadingSkeleton`, `AbortButton`, `TranslationPane` all follow `export function Name(props): ReactElement` pattern with named `export interface Props`. Reuse.
- **i18n state**: TranslateViewer is currently Italian-only hardcoded (per audit). Use IT strings consistently with existing LABELS const in `TranslateViewer.steps.ts:24-32`.
- **No FSM touch**: Reader-mode is purely visual; do NOT modify `phase` state, `useTranslateSegmentSSE` hook, or any abort/timeout logic. These belong to #1557 G shipped 24h ago.
- **CSS file choice**: Append to `globals.css` (not `design-tokens-canonical.css`). Tokens go in canonical; component CSS in globals.
- **TranslateViewer test setup**: Existing tests likely use a helper like `renderTranslateViewer(props)` or inline `render(<TranslateViewer {...defaultProps} />)`. Locate and reuse, do NOT reinvent the setup.
- **Run subagent prompts with absolute paths** under `D:\Repositories\meepleai-monorepo-frontend\`.
