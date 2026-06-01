# FE PR2 #1559 — Multi-language detection + override modal (TranslateViewer)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the FE side of #1559 multi-language detection + override modal on `TranslateViewer`. Reads `detectedSourceLang` + `langDetectionConfidence` from BE PR1 (#1787 shipped) SSE final chunk; renders a confidence-tiered header badge; auto-opens a `Radix Dialog` modal with radio group for `<0.5` confidence; supports re-translate via cached OCR (`?sourceLangOverride=` query param).

**Architecture:** 4 new files (pure helper, types/labels, presentational badge, Radix-wrapped modal) + 2 modifications (hook additive evolution + TranslateViewer state machine integration). Modal state is local to TranslateViewer (no global store). Type-safe `SourceLangCode` union shared across all files. Analytics via existing `trackEvent` infrastructure. a11y enforced via `jest-axe` + Radix automatic ARIA.

**Tech Stack:** Next.js 16 + React 19 + TypeScript + Tailwind 4 + Vitest + `@radix-ui/react-dialog@1.1.15` + `@testing-library/react` + `userEvent` + `jest-axe@10`.

**DEC traceability:** DEC-FE-1..DEC-FE-13 locked via spec-panel critique 2026-06-01 (see [#1559 comment 4592606051](https://github.com/meepleAi-app/meepleai-monorepo/issues/1559#issuecomment-4592606051)). G/W/T scenarios S1-S8 inline below.

**BE contract (shipped PR #1787):** SSE final chunk additive `DetectedSourceLang: 'EN'|'FR'|'DE'|'ES'|'IT'|null` + `LangDetectionConfidence: 0..1|null`; endpoint query param `?sourceLangOverride=EN|FR|DE|ES|IT` validated pre-SSE (400 on invalid); OCR cache via HybridCache TTL 24h on re-translate.

---

## File Structure

| Path | Responsibility | Type | DEC |
|------|---------------|------|-----|
| `apps/web/src/lib/gamebook/lang-codes.ts` | `SourceLangCode` union type + `LANG_LABELS_IT` dict + `LANG_CODES_ORDER` array | Create | FE-13 |
| `apps/web/src/lib/gamebook/lang-tier.ts` | Pure helper `getLangTier(confidence)` → tier classification | Create | FE-2 |
| `apps/web/src/lib/gamebook/__tests__/lang-tier.test.ts` | Boundary tests for `getLangTier` | Create | FE-2/12 |
| `apps/web/src/lib/gamebook/hooks/useTranslateSegmentSSE.ts` | Hook extension: state additive + `sourceLangOverride` param + URL builder | Modify | FE-1 |
| `apps/web/src/lib/gamebook/hooks/__tests__/useTranslateSegmentSSE.test.tsx` | New test cases for lang fields + sourceLangOverride URL | Modify | FE-1/12 |
| `apps/web/src/components/features/gamebook/LangBadge.tsx` | Presentational pill, 4 tier variants, hardcoded IT, label-only (no %) | Create | FE-3 |
| `apps/web/src/components/features/gamebook/__tests__/LangBadge.test.tsx` | Tier rendering, onTap, aria-label, jest-axe | Create | FE-3/12 |
| `apps/web/src/components/features/gamebook/LangOverrideModal.tsx` | Radix Dialog wrapper, radio group 5 langs, dismissable, onConfirm | Create | FE-4 |
| `apps/web/src/components/features/gamebook/__tests__/LangOverrideModal.test.tsx` | Radio + confirm + dismiss + focus + jest-axe + a11y | Create | FE-4/12 |
| `apps/web/src/components/features/gamebook/TranslateViewer.tsx` | Wire badge + modal state machine + analytics + SegmentPicker block + re-translate | Modify | FE-5/6/7/8/10/11 |
| `apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx` | S1-S8 integration scenarios | Modify | FE-12 |

**Test commands:**
- Unit + integration: `cd apps/web && pnpm test -- --run <test-file-path>` (single file) or `pnpm test -- --run` (all)
- Typecheck: `cd apps/web && pnpm typecheck`
- Lint: `cd apps/web && pnpm lint`
- Coverage: `cd apps/web && pnpm test:coverage`

---

## Task 1: Types & labels scaffold

**Files:**
- Create: `apps/web/src/lib/gamebook/lang-codes.ts`

- [ ] **Step 1: Create the types + labels file**

```ts
/**
 * Language codes supported by the gamebook translate viewer.
 *
 * Aligned with BE PR #1787 allowlist (DEC-3 BE):
 * NTextCat detection filters to these 5 ISO 639-1 UPPERCASE codes.
 * The 5 radio options of LangOverrideModal mirror this set.
 *
 * Target lang is fixed to IT in v1 (Aaron CORE row K/L). Future i18n
 * may add a target picker via separate epic.
 */
export type SourceLangCode = 'EN' | 'FR' | 'DE' | 'ES' | 'IT';

/** Ordered presentation list for radio options (modal). */
export const LANG_CODES_ORDER: readonly SourceLangCode[] = ['EN', 'FR', 'DE', 'ES', 'IT'];

/** Italian human-readable labels for radio + badge display. */
export const LANG_LABELS_IT: Record<SourceLangCode, string> = {
  EN: 'Inglese',
  FR: 'Francese',
  DE: 'Tedesco',
  ES: 'Spagnolo',
  IT: 'Italiano',
};

/** Type guard for runtime parsing of BE-emitted lang strings. */
export function isSourceLangCode(value: unknown): value is SourceLangCode {
  return typeof value === 'string' && (LANG_CODES_ORDER as readonly string[]).includes(value);
}
```

- [ ] **Step 2: Verify typecheck passes**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS with no errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/gamebook/lang-codes.ts
git commit -m "T1 SourceLangCode types + LANG_LABELS_IT dict (#1559)"
```

---

## Task 2: Tier classification helper (TDD)

**Files:**
- Create: `apps/web/src/lib/gamebook/lang-tier.ts`
- Test: `apps/web/src/lib/gamebook/__tests__/lang-tier.test.ts`

- [ ] **Step 1: Write the failing tests**

```ts
// apps/web/src/lib/gamebook/__tests__/lang-tier.test.ts
import { describe, expect, it } from 'vitest';

import { getLangTier, type LangTier } from '../lang-tier';

describe('getLangTier', () => {
  it('returns "none" for null', () => {
    expect(getLangTier(null)).toBe('none' satisfies LangTier);
  });

  it('returns "none" for undefined', () => {
    expect(getLangTier(undefined)).toBe('none' satisfies LangTier);
  });

  it('returns "high" for confidence > 0.8', () => {
    expect(getLangTier(0.81)).toBe('high' satisfies LangTier);
    expect(getLangTier(0.95)).toBe('high' satisfies LangTier);
    expect(getLangTier(1.0)).toBe('high' satisfies LangTier);
  });

  it('returns "medium" for confidence in [0.5, 0.8] inclusive', () => {
    expect(getLangTier(0.5)).toBe('medium' satisfies LangTier);
    expect(getLangTier(0.65)).toBe('medium' satisfies LangTier);
    expect(getLangTier(0.8)).toBe('medium' satisfies LangTier);
  });

  it('returns "low" for confidence < 0.5', () => {
    expect(getLangTier(0.49)).toBe('low' satisfies LangTier);
    expect(getLangTier(0.31)).toBe('low' satisfies LangTier);
    expect(getLangTier(0)).toBe('low' satisfies LangTier);
  });

  it('returns "low" for negative confidence (defensive)', () => {
    expect(getLangTier(-0.1)).toBe('low' satisfies LangTier);
  });

  it('returns "high" for confidence above 1 (defensive)', () => {
    expect(getLangTier(1.5)).toBe('high' satisfies LangTier);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test -- --run src/lib/gamebook/__tests__/lang-tier.test.ts`
Expected: FAIL with "Cannot find module '../lang-tier'"

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/src/lib/gamebook/lang-tier.ts
/**
 * Confidence tier classification per DEC-FE-2 (#1559).
 *
 *   - null/undefined → 'none' (no detection, legacy path or out-of-allowlist null+high)
 *   - confidence > 0.8 → 'high' (informational badge, no friction)
 *   - 0.5 ≤ confidence ≤ 0.8 → 'medium' (tap-to-confirm pill, SegmentPicker blocked)
 *   - confidence < 0.5 → 'low' (auto-open modal, SegmentPicker blocked)
 *
 * Confidence values come from BE NTextCat tanh(relativeGap × 200) normalization
 * (PR #1787 BE deviation note). FE treats the [0,1] range as opaque ordinal.
 */
export type LangTier = 'high' | 'medium' | 'low' | 'none';

export function getLangTier(confidence: number | null | undefined): LangTier {
  if (confidence === null || confidence === undefined) return 'none';
  if (confidence > 0.8) return 'high';
  if (confidence >= 0.5) return 'medium';
  return 'low';
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `cd apps/web && pnpm test -- --run src/lib/gamebook/__tests__/lang-tier.test.ts`
Expected: PASS — 7/7 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/gamebook/lang-tier.ts apps/web/src/lib/gamebook/__tests__/lang-tier.test.ts
git commit -m "T2 getLangTier pure helper + 7 boundary tests (#1559)"
```

---

## Task 3: Hook extension (additive state + sourceLangOverride)

**Files:**
- Modify: `apps/web/src/lib/gamebook/hooks/useTranslateSegmentSSE.ts`
- Test: `apps/web/src/lib/gamebook/hooks/__tests__/useTranslateSegmentSSE.test.tsx` (additive)

- [ ] **Step 1: Write the new failing tests (append to existing test file)**

Append these test blocks AT THE END of `useTranslateSegmentSSE.test.tsx` (before the last `});` closing the outer `describe`):

```tsx
  // ---------------------------------------------------------------------------
  // #1559 — lang detection fields + sourceLangOverride
  // ---------------------------------------------------------------------------

  it('captures detectedSourceLang and langDetectionConfidence from final chunk', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 1, BOOK_ID));
    act(() =>
      lastInstance!.simulateMessage({
        delta: 'Apri la porta.',
        isComplete: true,
        paragraphId: PARA_ID,
        appliedTerms: [],
        detectedSourceLang: 'EN',
        langDetectionConfidence: 0.92,
      })
    );

    expect(result.current.detectedSourceLang).toBe('EN');
    expect(result.current.langDetectionConfidence).toBe(0.92);
    expect(result.current.isComplete).toBe(true);
  });

  it('exposes null lang fields when BE emits them (out-of-allowlist or legacy)', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 1, BOOK_ID));
    act(() =>
      lastInstance!.simulateMessage({
        delta: 'X.',
        isComplete: true,
        paragraphId: PARA_ID,
        appliedTerms: [],
        detectedSourceLang: null,
        langDetectionConfidence: 0.31,
      })
    );

    expect(result.current.detectedSourceLang).toBeNull();
    expect(result.current.langDetectionConfidence).toBe(0.31);
  });

  it('exposes undefined lang fields when BE omits them (backward compat)', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 1, BOOK_ID));
    act(() =>
      lastInstance!.simulateMessage({
        delta: 'Legacy.',
        isComplete: true,
        paragraphId: PARA_ID,
        appliedTerms: [],
      })
    );

    expect(result.current.detectedSourceLang).toBeUndefined();
    expect(result.current.langDetectionConfidence).toBeUndefined();
  });

  it('appends sourceLangOverride to URL when provided', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 4, BOOK_ID, 'FR'));

    expect(lastInstance?.url).toContain('sourceLangOverride=FR');
  });

  it('omits sourceLangOverride from URL when not provided', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 4, BOOK_ID));

    expect(lastInstance?.url).not.toContain('sourceLangOverride');
  });

  it('resets lang fields on new start() call', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 1, BOOK_ID));
    act(() =>
      lastInstance!.simulateMessage({
        delta: 'old',
        isComplete: true,
        detectedSourceLang: 'EN',
        langDetectionConfidence: 0.92,
      })
    );

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 2, BOOK_ID, 'DE'));

    expect(result.current.detectedSourceLang).toBeUndefined();
    expect(result.current.langDetectionConfidence).toBeUndefined();
  });
```

- [ ] **Step 2: Run tests to verify failure**

Run: `cd apps/web && pnpm test -- --run src/lib/gamebook/hooks/__tests__/useTranslateSegmentSSE.test.tsx`
Expected: FAIL — 6 new tests fail (state fields undefined, URL missing sourceLangOverride)

- [ ] **Step 3: Implement the hook extension (replace entire file content)**

```ts
// apps/web/src/lib/gamebook/hooks/useTranslateSegmentSSE.ts
'use client';

import { useCallback, useRef, useState } from 'react';

import type { SourceLangCode } from '@/lib/gamebook/lang-codes';

export interface TranslateState {
  partialText: string;
  isComplete: boolean;
  paragraphId?: string;
  appliedTerms: string[];
  error?: string;
  /**
   * Source language detected by BE NTextCat (PR #1787 DEC-3 BE).
   * `null` if out-of-allowlist or detection failed.
   * `undefined` if BE didn't emit (legacy backward compat).
   * Only populated on final SSE chunk per BE DEC-10.
   */
  detectedSourceLang?: SourceLangCode | null;
  /**
   * Detection confidence in [0,1] (BE tanh-normalized, raw).
   * `null` if detection failed; `undefined` if BE didn't emit.
   * FE classifies via `getLangTier()` (DEC-FE-2).
   */
  langDetectionConfidence?: number | null;
}

const initialState: TranslateState = { partialText: '', isComplete: false, appliedTerms: [] };

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export function useTranslateSegmentSSE() {
  const [state, setState] = useState<TranslateState>(initialState);
  const sourceRef = useRef<EventSource | null>(null);

  const stop = useCallback(() => {
    sourceRef.current?.close();
    sourceRef.current = null;
  }, []);

  const start = useCallback(
    (
      campaignId: string,
      photoId: string,
      paragraphNumber: number,
      gameBookId: string,
      sourceLangOverride?: SourceLangCode
    ) => {
      stop();
      setState(initialState);
      let url =
        `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/photos/translate` +
        `?photoId=${encodeURIComponent(photoId)}` +
        `&paragraphNumber=${paragraphNumber}` +
        `&gameBookId=${encodeURIComponent(gameBookId)}`;
      if (sourceLangOverride) {
        url += `&sourceLangOverride=${encodeURIComponent(sourceLangOverride)}`;
      }
      const es = new EventSource(url, { withCredentials: true });
      sourceRef.current = es;

      es.onmessage = (ev: MessageEvent<string>) => {
        try {
          const chunk = JSON.parse(ev.data) as {
            delta?: string;
            isComplete?: boolean;
            paragraphId?: string;
            appliedTerms?: string[];
            error?: string;
            detectedSourceLang?: SourceLangCode | null;
            langDetectionConfidence?: number | null;
          };
          if (chunk.error) {
            setState(s => ({ ...s, error: chunk.error }));
            es.close();
            return;
          }
          setState(s => ({
            partialText: s.partialText + (chunk.delta ?? ''),
            isComplete: chunk.isComplete ?? false,
            paragraphId: chunk.paragraphId ?? s.paragraphId,
            appliedTerms: chunk.appliedTerms ?? s.appliedTerms,
            detectedSourceLang:
              chunk.detectedSourceLang !== undefined
                ? chunk.detectedSourceLang
                : s.detectedSourceLang,
            langDetectionConfidence:
              chunk.langDetectionConfidence !== undefined
                ? chunk.langDetectionConfidence
                : s.langDetectionConfidence,
          }));
          if (chunk.isComplete) es.close();
        } catch {
          // malformed JSON — ignore
        }
      };

      es.onerror = () => {
        setState(s => ({ ...s, error: s.error ?? 'stream_error' }));
        es.close();
      };
    },
    [stop]
  );

  return { ...state, start, stop };
}
```

- [ ] **Step 4: Run all hook tests to verify pass**

Run: `cd apps/web && pnpm test -- --run src/lib/gamebook/hooks/__tests__/useTranslateSegmentSSE.test.tsx`
Expected: PASS — all tests (existing 8 + new 6 = 14)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/gamebook/hooks/useTranslateSegmentSSE.ts \
        apps/web/src/lib/gamebook/hooks/__tests__/useTranslateSegmentSSE.test.tsx
git commit -m "T3 useTranslateSegmentSSE additive lang fields + sourceLangOverride param (#1559)"
```

---

## Task 4: LangBadge component (TDD)

**Files:**
- Create: `apps/web/src/components/features/gamebook/LangBadge.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/LangBadge.test.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
// apps/web/src/components/features/gamebook/__tests__/LangBadge.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { LangBadge } from '../LangBadge';

describe('LangBadge', () => {
  describe('tier rendering', () => {
    it('renders "Sorgente: EN" for tier=high with detected lang', () => {
      render(<LangBadge lang="EN" confidence={0.92} tier="high" />);
      expect(screen.getByText(/sorgente: EN/i)).toBeInTheDocument();
    });

    it('renders "Conferma: FR?" for tier=medium with detected lang', () => {
      render(<LangBadge lang="FR" confidence={0.65} tier="medium" />);
      expect(screen.getByText(/conferma: FR\?/i)).toBeInTheDocument();
    });

    it('renders "Sorgente richiesta" for tier=low (independent of lang)', () => {
      render(<LangBadge lang={null} confidence={0.31} tier="low" />);
      expect(screen.getByText(/sorgente richiesta/i)).toBeInTheDocument();
    });

    it('renders nothing for tier=none (returns null)', () => {
      const { container } = render(
        <LangBadge lang={null} confidence={null} tier="none" />
      );
      expect(container).toBeEmptyDOMElement();
    });

    it('appends "(override)" suffix when isOverride=true on high tier', () => {
      render(<LangBadge lang="FR" confidence={0.92} tier="high" isOverride />);
      expect(screen.getByText(/sorgente: FR.*override/i)).toBeInTheDocument();
    });
  });

  describe('interaction', () => {
    it('calls onTap when user clicks the badge', async () => {
      const user = userEvent.setup();
      const onTap = vi.fn();
      render(<LangBadge lang="EN" confidence={0.92} tier="high" onTap={onTap} />);

      await user.click(screen.getByRole('button'));
      expect(onTap).toHaveBeenCalledTimes(1);
    });

    it('renders as <span> (non-interactive) when no onTap provided', () => {
      render(<LangBadge lang="EN" confidence={0.92} tier="high" />);
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });
  });

  describe('aria-label includes confidence (for screen readers)', () => {
    it('mentions confidence percentage in aria-label on tier=high', () => {
      render(<LangBadge lang="EN" confidence={0.92} tier="high" onTap={() => {}} />);
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute(
        'aria-label',
        expect.stringMatching(/sorgente.*inglese.*confidenza alta.*92%/i)
      );
    });

    it('mentions confidence percentage in aria-label on tier=medium', () => {
      render(<LangBadge lang="FR" confidence={0.65} tier="medium" onTap={() => {}} />);
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute(
        'aria-label',
        expect.stringMatching(/conferma.*francese.*confidenza media.*65%/i)
      );
    });

    it('aria-label on tier=low signals action required', () => {
      render(<LangBadge lang={null} confidence={0.31} tier="low" onTap={() => {}} />);
      const button = screen.getByRole('button');
      expect(button).toHaveAttribute(
        'aria-label',
        expect.stringMatching(/sorgente richiesta.*conferma manualmente/i)
      );
    });
  });

  describe('a11y (jest-axe)', () => {
    it('high tier as button → zero violations', async () => {
      const { container } = render(
        <LangBadge lang="EN" confidence={0.92} tier="high" onTap={() => {}} />
      );
      expect(await axe(container)).toHaveNoViolations();
    });

    it('medium tier as button → zero violations', async () => {
      const { container } = render(
        <LangBadge lang="FR" confidence={0.65} tier="medium" onTap={() => {}} />
      );
      expect(await axe(container)).toHaveNoViolations();
    });

    it('low tier as button → zero violations', async () => {
      const { container } = render(
        <LangBadge lang={null} confidence={0.31} tier="low" onTap={() => {}} />
      );
      expect(await axe(container)).toHaveNoViolations();
    });
  });
});
```

- [ ] **Step 2: Run tests to verify failure**

Run: `cd apps/web && pnpm test -- --run src/components/features/gamebook/__tests__/LangBadge.test.tsx`
Expected: FAIL — "Cannot find module '../LangBadge'"

- [ ] **Step 3: Implement the component**

```tsx
// apps/web/src/components/features/gamebook/LangBadge.tsx
'use client';

import type { ReactElement } from 'react';

import type { LangTier } from '@/lib/gamebook/lang-tier';
import { LANG_LABELS_IT, type SourceLangCode } from '@/lib/gamebook/lang-codes';

export interface LangBadgeProps {
  lang: SourceLangCode | null;
  confidence: number | null;
  tier: LangTier;
  onTap?: () => void;
  /** Render "(override)" suffix when user has manually overridden detected lang. */
  isOverride?: boolean;
}

const TIER_STYLES: Record<Exclude<LangTier, 'none'>, string> = {
  high: 'bg-emerald-100 text-emerald-900 ring-1 ring-emerald-200',
  medium: 'bg-amber-100 text-amber-900 ring-1 ring-amber-200',
  low: 'bg-rose-100 text-rose-900 ring-1 ring-rose-200',
};

const TIER_CONFIDENCE_LABEL_IT: Record<Exclude<LangTier, 'none'>, string> = {
  high: 'Confidenza alta',
  medium: 'Confidenza media',
  low: 'Confidenza bassa',
};

function buildVisualText(
  tier: Exclude<LangTier, 'none'>,
  lang: SourceLangCode | null,
  isOverride: boolean
): string {
  if (tier === 'low') return 'Sorgente richiesta';
  if (tier === 'medium' && lang) return `Conferma: ${lang}?`;
  if (tier === 'medium' && !lang) return 'Conferma sorgente';
  // tier === 'high'
  return isOverride ? `Sorgente: ${lang} (override)` : `Sorgente: ${lang}`;
}

function buildAriaLabel(
  tier: Exclude<LangTier, 'none'>,
  lang: SourceLangCode | null,
  confidence: number | null
): string {
  const langLabel = lang ? LANG_LABELS_IT[lang] : 'sconosciuta';
  const confLabel = TIER_CONFIDENCE_LABEL_IT[tier];
  const confPct =
    confidence !== null && confidence !== undefined
      ? ` (${Math.round(confidence * 100)}%)`
      : '';

  if (tier === 'low') {
    return `Sorgente richiesta. Conferma manualmente la lingua.`;
  }
  if (tier === 'medium') {
    return `Conferma sorgente: ${langLabel}. ${confLabel}${confPct}. Tap per confermare o cambiare.`;
  }
  return `Sorgente: ${langLabel}. ${confLabel}${confPct}. Tap per cambiare.`;
}

export function LangBadge({
  lang,
  confidence,
  tier,
  onTap,
  isOverride = false,
}: LangBadgeProps): ReactElement | null {
  if (tier === 'none') return null;

  const visualText = buildVisualText(tier, lang, isOverride);
  const baseClass = `inline-flex items-center rounded-full px-3 py-1 text-xs font-medium ${TIER_STYLES[tier]}`;

  if (onTap) {
    return (
      <button
        type="button"
        onClick={onTap}
        className={`${baseClass} cursor-pointer hover:opacity-90 focus-visible:ring-2 focus-visible:ring-offset-1`}
        aria-label={buildAriaLabel(tier, lang, confidence)}
        data-testid="lang-badge"
        data-tier={tier}
      >
        {visualText}
      </button>
    );
  }

  return (
    <span className={baseClass} data-testid="lang-badge" data-tier={tier}>
      {visualText}
    </span>
  );
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `cd apps/web && pnpm test -- --run src/components/features/gamebook/__tests__/LangBadge.test.tsx`
Expected: PASS — 14 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/LangBadge.tsx \
        apps/web/src/components/features/gamebook/__tests__/LangBadge.test.tsx
git commit -m "T4 LangBadge 4-tier presentational component + 14 tests incl. jest-axe (#1559)"
```

---

## Task 5: LangOverrideModal (Radix Dialog wrapper) — TDD

**Files:**
- Create: `apps/web/src/components/features/gamebook/LangOverrideModal.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/LangOverrideModal.test.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
// apps/web/src/components/features/gamebook/__tests__/LangOverrideModal.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { LangOverrideModal } from '../LangOverrideModal';

describe('LangOverrideModal', () => {
  describe('rendering', () => {
    it('renders no dialog when open=false', () => {
      render(
        <LangOverrideModal
          open={false}
          onOpenChange={() => {}}
          dismissable
          onConfirm={() => {}}
        />
      );
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('renders dialog with title + 5 radio options when open=true', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          onConfirm={() => {}}
        />
      );
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText(/seleziona la lingua/i)).toBeInTheDocument();
      expect(screen.getAllByRole('radio')).toHaveLength(5);
      // Each label
      expect(screen.getByLabelText('Inglese')).toBeInTheDocument();
      expect(screen.getByLabelText('Francese')).toBeInTheDocument();
      expect(screen.getByLabelText('Tedesco')).toBeInTheDocument();
      expect(screen.getByLabelText('Spagnolo')).toBeInTheDocument();
      expect(screen.getByLabelText('Italiano')).toBeInTheDocument();
    });

    it('preselects the radio matching preselect prop', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          preselect="FR"
          onConfirm={() => {}}
        />
      );
      expect(screen.getByLabelText('Francese')).toBeChecked();
      expect(screen.getByLabelText('Inglese')).not.toBeChecked();
    });
  });

  describe('confirm', () => {
    it('confirm button disabled when no selection + no preselect', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          onConfirm={() => {}}
        />
      );
      const confirmBtn = screen.getByRole('button', { name: /conferma e ritraduci/i });
      expect(confirmBtn).toBeDisabled();
    });

    it('confirm button enabled when preselect provided', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          preselect="FR"
          onConfirm={() => {}}
        />
      );
      const confirmBtn = screen.getByRole('button', { name: /conferma e ritraduci/i });
      expect(confirmBtn).not.toBeDisabled();
    });

    it('confirm enables after user picks a radio (from no-preselect)', async () => {
      const user = userEvent.setup();
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          onConfirm={() => {}}
        />
      );
      await user.click(screen.getByLabelText('Tedesco'));
      const confirmBtn = screen.getByRole('button', { name: /conferma e ritraduci/i });
      expect(confirmBtn).not.toBeDisabled();
    });

    it('invokes onConfirm with selected lang on submit', async () => {
      const user = userEvent.setup();
      const onConfirm = vi.fn();
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          preselect="EN"
          onConfirm={onConfirm}
        />
      );
      await user.click(screen.getByLabelText('Spagnolo'));
      await user.click(screen.getByRole('button', { name: /conferma e ritraduci/i }));
      expect(onConfirm).toHaveBeenCalledWith('ES');
    });
  });

  describe('dismiss', () => {
    it('renders Chiudi button when dismissable=true', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          onConfirm={() => {}}
        />
      );
      expect(screen.getByRole('button', { name: /chiudi/i })).toBeInTheDocument();
    });

    it('hides Chiudi button when dismissable=false', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable={false}
          onConfirm={() => {}}
        />
      );
      expect(screen.queryByRole('button', { name: /chiudi/i })).not.toBeInTheDocument();
    });

    it('calls onOpenChange(false) when Chiudi clicked', async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      render(
        <LangOverrideModal
          open
          onOpenChange={onOpenChange}
          dismissable
          onConfirm={() => {}}
        />
      );
      await user.click(screen.getByRole('button', { name: /chiudi/i }));
      expect(onOpenChange).toHaveBeenCalledWith(false);
    });

    it('calls onOpenChange(false) on Escape when dismissable=true', async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      render(
        <LangOverrideModal
          open
          onOpenChange={onOpenChange}
          dismissable
          onConfirm={() => {}}
        />
      );
      await user.keyboard('{Escape}');
      expect(onOpenChange).toHaveBeenCalledWith(false);
    });
  });

  describe('a11y', () => {
    it('dialog has aria-modal=true (Radix automatic)', () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          onConfirm={() => {}}
        />
      );
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
    });

    it('axe finds zero violations when open (no preselect)', async () => {
      const { container } = render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          onConfirm={() => {}}
        />
      );
      expect(await axe(container)).toHaveNoViolations();
    });

    it('axe finds zero violations when open (with preselect)', async () => {
      const { container } = render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          preselect="DE"
          onConfirm={() => {}}
        />
      );
      expect(await axe(container)).toHaveNoViolations();
    });

    it('axe finds zero violations when dismissable=false', async () => {
      const { container } = render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable={false}
          onConfirm={() => {}}
        />
      );
      expect(await axe(container)).toHaveNoViolations();
    });
  });

  describe('focus', () => {
    it('moves focus inside dialog on mount (focus trap)', async () => {
      render(
        <LangOverrideModal
          open
          onOpenChange={() => {}}
          dismissable
          onConfirm={() => {}}
        />
      );
      await waitFor(() => {
        // Radix moves focus to the first focusable element (typically close button or first radio)
        const dialog = screen.getByRole('dialog');
        expect(dialog.contains(document.activeElement)).toBe(true);
      });
    });
  });
});
```

- [ ] **Step 2: Run tests to verify failure**

Run: `cd apps/web && pnpm test -- --run src/components/features/gamebook/__tests__/LangOverrideModal.test.tsx`
Expected: FAIL — "Cannot find module '../LangOverrideModal'"

- [ ] **Step 3: Implement the component**

```tsx
// apps/web/src/components/features/gamebook/LangOverrideModal.tsx
'use client';

import * as Dialog from '@radix-ui/react-dialog';
import { useEffect, useId, useState, type ReactElement } from 'react';

import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { LANG_CODES_ORDER, LANG_LABELS_IT, type SourceLangCode } from '@/lib/gamebook/lang-codes';

export interface LangOverrideModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Preselected radio option (if any). */
  preselect?: SourceLangCode;
  /** When false, hide the Chiudi button and ignore Escape (forced-choice mode for auto-low). */
  dismissable: boolean;
  /** Called when user confirms with a selected lang. */
  onConfirm: (lang: SourceLangCode) => void;
}

export function LangOverrideModal({
  open,
  onOpenChange,
  preselect,
  dismissable,
  onConfirm,
}: LangOverrideModalProps): ReactElement {
  const [selected, setSelected] = useState<SourceLangCode | undefined>(preselect);
  const titleId = useId();
  const radioName = useId();

  // Sync selected when preselect or open changes (modal reopen resets state).
  useEffect(() => {
    if (open) {
      setSelected(preselect);
    }
  }, [open, preselect]);

  const handleConfirm = () => {
    if (selected) onConfirm(selected);
  };

  return (
    <Dialog.Root
      open={open}
      onOpenChange={(next) => {
        // Block Escape/outside-click dismiss when not dismissable
        if (!next && !dismissable) return;
        onOpenChange(next);
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay
          className="fixed inset-0 z-50 bg-black/40 data-[state=open]:animate-in data-[state=open]:fade-in"
          data-testid="lang-override-scrim"
        />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-50 w-full max-w-md -translate-x-1/2 -translate-y-1/2 rounded-lg bg-background p-6 shadow-lg"
          aria-labelledby={titleId}
          onEscapeKeyDown={(e) => {
            if (!dismissable) e.preventDefault();
          }}
          onPointerDownOutside={(e) => {
            if (!dismissable) e.preventDefault();
          }}
        >
          <Dialog.Title id={titleId} className="text-lg font-semibold">
            Seleziona la lingua sorgente
          </Dialog.Title>
          <Dialog.Description className="mt-1 text-sm text-muted-foreground">
            Conferma la lingua originale della pagina per migliorare la traduzione.
          </Dialog.Description>

          <RadioGroup
            value={selected}
            onValueChange={(v) => setSelected(v as SourceLangCode)}
            name={radioName}
            className="mt-4 gap-3"
          >
            {LANG_CODES_ORDER.map((code) => (
              <label
                key={code}
                htmlFor={`${radioName}-${code}`}
                className="flex items-center gap-2 cursor-pointer text-sm"
              >
                <RadioGroupItem id={`${radioName}-${code}`} value={code} />
                <span>{LANG_LABELS_IT[code]}</span>
              </label>
            ))}
          </RadioGroup>

          <div className="mt-6 flex justify-end gap-2">
            {dismissable && (
              <button
                type="button"
                onClick={() => onOpenChange(false)}
                className="rounded-md px-4 py-2 text-sm font-medium text-muted-foreground hover:bg-muted"
              >
                Chiudi
              </button>
            )}
            <button
              type="button"
              onClick={handleConfirm}
              disabled={!selected}
              className="rounded-md bg-[var(--c-agent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
              data-testid="lang-override-confirm"
            >
              Conferma e ritraduci
            </button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `cd apps/web && pnpm test -- --run src/components/features/gamebook/__tests__/LangOverrideModal.test.tsx`
Expected: PASS — 15 tests

If any test fails on focus trap (`focus inside dialog on mount`), note: Radix may need `userEvent.setup({ pointerEventsCheck: PointerEventsCheckLevel.Never })` if running in jsdom + Radix Portal nuances. If so, mark the focus test `it.skip` with `// jsdom limitation` and document in PR body.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/LangOverrideModal.tsx \
        apps/web/src/components/features/gamebook/__tests__/LangOverrideModal.test.tsx
git commit -m "T5 LangOverrideModal Radix Dialog + radio + dismiss + 15 tests (#1559)"
```

---

## Task 6: TranslateViewer integration — badge + modal + analytics + block (S1, S2, S3, S7, S8)

**Files:**
- Modify: `apps/web/src/components/features/gamebook/TranslateViewer.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx`

- [ ] **Step 1: Add new test scenarios (append to TranslateViewer.test.tsx)**

First read the existing test file structure (look at top imports + `describe` blocks). Append these new `describe` blocks at the end of the file, BEFORE the outer file-level scope ends:

```tsx
  // ---------------------------------------------------------------------------
  // #1559 — Lang detection + override modal (S1, S2, S3, S7, S8)
  // ---------------------------------------------------------------------------

  describe('#1559 — S1: tier=high happy path', () => {
    it('renders LangBadge "Sorgente: EN" + SegmentPicker enabled + no auto-modal', async () => {
      // Reuse existing pattern from earlier #1557/#1558 scenarios:
      // - Setup MSW handlers for upload + segment
      // - Render TranslateViewer
      // - Fire SSE chunk with detectedSourceLang="EN", langDetectionConfidence=0.92
      // - Assert: LangBadge text "Sorgente: EN", SegmentPicker enabled, modal not opened
      // - Assert: trackEvent called with 'translate.lang_detected', {lang:"EN", tier:"high"}
      //
      // Use existing renderTranslateViewer / MSW setup utilities from this file.
      // Mock trackEvent: vi.spyOn(analytics, 'trackEvent')
    });
  });

  describe('#1559 — S2: tier=medium tap-to-confirm + SegmentPicker block', () => {
    it('renders "Conferma: FR?" + SegmentPicker DISABLED, tap opens modal preselect FR, confirm enables picker', async () => {
      // - Fire SSE with detectedSourceLang="FR", langDetectionConfidence=0.65
      // - Assert: badge text "Conferma: FR?", SegmentPicker disabled
      // - User clicks badge → modal opens with FR preselected
      // - User clicks "Conferma e ritraduci"
      // - Assert: modal closes, badge becomes "Sorgente: FR (override)", SegmentPicker enabled
      // - Assert: trackEvent fires 'translate.lang_modal_opened' with {tier:"medium", mode:"auto-medium-tap"}
    });
  });

  describe('#1559 — S3: tier=low forced auto-modal', () => {
    it('auto-opens modal mode=auto-low with no preselect + SegmentPicker DISABLED', async () => {
      // - Fire SSE with detectedSourceLang=null, langDetectionConfidence=0.31
      // - Assert: modal auto-opens, no preselect, SegmentPicker disabled
      // - Assert: trackEvent fires 'translate.lang_detected' AND 'translate.lang_modal_opened' with mode:"auto-low"
      // - User picks "DE" + confirm
      // - Assert: modal closes, badge "Sorgente: DE (override)", SegmentPicker enabled
      // - Assert: trackEvent fires 'translate.lang_overridden' {fromLang: null, toLang: "DE"}
    });

    it('S5: dismiss auto-low modal keeps SegmentPicker DISABLED + badge danger', async () => {
      // - Same setup as S3 but user presses Escape
      // - Assert: modal closes, confirmedLang stays null, badge "Sorgente richiesta" danger tier
      // - Assert: SegmentPicker remains DISABLED
      // - Assert: trackEvent fires 'translate.lang_modal_dismissed' {tier:"low"}
      // - User taps badge → modal reopens mode='auto-medium-tap' (not 'auto-low' second time)
    });
  });

  describe('#1559 — S7: legacy backward compat (lang fields undefined/null)', () => {
    it('SSE without lang fields → tier=none, no badge, no modal, SegmentPicker enabled', async () => {
      // - Fire SSE without detectedSourceLang/langDetectionConfidence
      // - Assert: no badge rendered, no modal, SegmentPicker enabled
    });
  });

  describe('#1559 — S8: out-of-allowlist null lang + high confidence → tier=none', () => {
    it('SSE {detectedSourceLang: null, langDetectionConfidence: 0.85} → tier=none, no badge, picker enabled', async () => {
      // Note: BE may emit lang=null with raw high confidence when detection lands outside allowlist.
      // FE: getLangTier(0.85) returns 'high' BUT the tier 'high' rendering requires non-null lang.
      // Per DEC-FE-10, when lang=null we override the tier to 'none' before rendering badge.
      // - Fire SSE with detectedSourceLang=null, langDetectionConfidence=0.85
      // - Assert: no badge rendered (tier fallback to 'none')
      // - Assert: SegmentPicker enabled (no block)
    });
  });
```

> Note for implementer: the existing `TranslateViewer.test.tsx` already has render helpers (MSW setup, mock EventSource, photo upload mocks). Reuse those patterns — DO NOT recreate. The pseudo-code comments above describe the assertion shape; flesh them out matching the existing test structure observed when reading the file at start of this task.

- [ ] **Step 2: Run tests to verify failure**

Run: `cd apps/web && pnpm test -- --run src/components/features/gamebook/__tests__/TranslateViewer.test.tsx`
Expected: FAIL — assertions reference `LangBadge` / `LangOverrideModal` not yet wired

- [ ] **Step 3: Modify TranslateViewer to integrate**

Replace the current `TranslateViewer.tsx` with the extended version below (full file, additive on existing):

```tsx
// apps/web/src/components/features/gamebook/TranslateViewer.tsx
'use client';

import { useCallback, useEffect, useMemo, useRef, useState, type ReactElement } from 'react';

import { AbortButton } from '@/components/features/gamebook/AbortButton';
import { BookPicker } from '@/components/features/gamebook/BookPicker';
import { LangBadge } from '@/components/features/gamebook/LangBadge';
import { LangOverrideModal } from '@/components/features/gamebook/LangOverrideModal';
import { LoadingSkeleton } from '@/components/features/gamebook/LoadingSkeleton';
import { ReaderModeToggle } from '@/components/features/gamebook/ReaderModeToggle';
import { SegmentPicker } from '@/components/features/gamebook/SegmentPicker';
import {
  deriveUiStep,
  isAbortableStep,
  LABELS,
} from '@/components/features/gamebook/TranslateViewer.steps';
import { TranslationPane } from '@/components/features/gamebook/TranslationPane';
import { useGameBooks } from '@/hooks/useGameBooks';
import { trackEvent } from '@/lib/analytics/track-event';
import { GameBookRole, hasRole, type GameRef } from '@/lib/api/gamebook';
import type { GamebookPhotoArtifact, GamebookSegment } from '@/lib/api/gamebook-photos';
import { usePhotoUpload } from '@/lib/gamebook/hooks/usePhotoUpload';
import { useReaderMode } from '@/lib/gamebook/hooks/useReaderMode';
import { useSegmentPhoto } from '@/lib/gamebook/hooks/useSegmentPhoto';
import { useTranslateSegmentSSE } from '@/lib/gamebook/hooks/useTranslateSegmentSSE';
import type { SourceLangCode } from '@/lib/gamebook/lang-codes';
import { getLangTier, type LangTier } from '@/lib/gamebook/lang-tier';

export interface TranslateViewerProps {
  campaignId: string;
  gameRef: GameRef;
}

export type Phase =
  | 'idle'
  | 'uploading'
  | 'segmenting'
  | 'segments_ready'
  | 'translating'
  | 'translated';

type ModalMode = 'auto-low' | 'auto-medium-tap' | 'manual';
type ModalState = { open: boolean; mode: ModalMode; preselect?: SourceLangCode };

/**
 * Compute effective tier for badge rendering.
 * DEC-FE-10: when detected lang is null with high confidence (out-of-allowlist),
 * fall back to 'none' (un-actionable detection).
 */
function effectiveTier(lang: SourceLangCode | null | undefined, conf: number | null | undefined): LangTier {
  const rawTier = getLangTier(conf);
  if (rawTier === 'high' && (lang === null || lang === undefined)) return 'none';
  return rawTier;
}

export function TranslateViewer({ campaignId, gameRef }: TranslateViewerProps): ReactElement {
  const [phase, setPhase] = useState<Phase>('idle');
  const [artifact, setArtifact] = useState<GamebookPhotoArtifact | null>(null);
  const [activeSegment, setActiveSegment] = useState<GamebookSegment | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { isReaderMode, toggle: toggleReaderMode } = useReaderMode();

  const { data: books } = useGameBooks(gameRef);
  const narrativeBooks = useMemo(
    () =>
      (books ?? [])
        .filter(b => hasRole(b.roles, GameBookRole.Narrative))
        .map(b => ({ id: b.id, displayName: b.displayName, roles: b.roles })),
    [books]
  );

  const [selectedBookId, setSelectedBookId] = useState<string | undefined>(undefined);
  const effectiveBookId =
    selectedBookId ?? (narrativeBooks.length === 1 ? narrativeBooks[0].id : undefined);

  const upload = usePhotoUpload(campaignId);
  const segment = useSegmentPhoto(campaignId);
  const sse = useTranslateSegmentSSE();

  const [timeoutError, setTimeoutError] = useState<string | null>(null);
  const uiStep = deriveUiStep(phase, sse);

  // #1559 — Lang detection state
  const [confirmedLang, setConfirmedLang] = useState<SourceLangCode | null>(null);
  const [autoLowFiredForArtifact, setAutoLowFiredForArtifact] = useState<string | null>(null);
  const [analyticsDetectedFiredForArtifact, setAnalyticsDetectedFiredForArtifact] = useState<string | null>(null);
  const [modalState, setModalState] = useState<ModalState>({ open: false, mode: 'manual' });

  // Reset lang state on artifact change (DEC-FE-11)
  useEffect(() => {
    setConfirmedLang(null);
    setAutoLowFiredForArtifact(null);
    setAnalyticsDetectedFiredForArtifact(null);
  }, [artifact?.id]);

  const tier = effectiveTier(sse.detectedSourceLang, sse.langDetectionConfidence);
  const effectiveLang: SourceLangCode | null = confirmedLang ?? sse.detectedSourceLang ?? null;
  const isOverride = confirmedLang !== null && confirmedLang !== sse.detectedSourceLang;
  // Tier shown in UI: after confirmedLang, badge transitions to 'high' info-tier (override case)
  const renderedTier: LangTier = confirmedLang ? 'high' : tier;
  const segmentBlockedByLang = (tier === 'medium' || tier === 'low') && !confirmedLang;

  // Emit analytics: lang_detected fires once per artifact when sse.isComplete with lang fields present
  useEffect(() => {
    if (!sse.isComplete || !artifact?.id) return;
    if (analyticsDetectedFiredForArtifact === artifact.id) return;
    // Only emit when BE actually sent the fields (DEC-FE-12 vs legacy)
    if (sse.detectedSourceLang === undefined && sse.langDetectionConfidence === undefined) return;
    trackEvent('translate.lang_detected', {
      photoId: artifact.id,
      lang: sse.detectedSourceLang ?? null,
      confidence: sse.langDetectionConfidence ?? null,
      tier,
    });
    setAnalyticsDetectedFiredForArtifact(artifact.id);
  }, [sse.isComplete, sse.detectedSourceLang, sse.langDetectionConfidence, artifact?.id, tier, analyticsDetectedFiredForArtifact]);

  // Auto-open modal for tier=low first time per artifact (DEC-FE-5)
  useEffect(() => {
    if (!sse.isComplete || !artifact?.id) return;
    if (autoLowFiredForArtifact === artifact.id) return;
    if (tier !== 'low') return;
    if (confirmedLang) return;
    setModalState({ open: true, mode: 'auto-low', preselect: undefined });
    setAutoLowFiredForArtifact(artifact.id);
    trackEvent('translate.lang_modal_opened', {
      photoId: artifact.id,
      tier,
      mode: 'auto-low',
    });
  }, [sse.isComplete, tier, artifact?.id, autoLowFiredForArtifact, confirmedLang]);

  const handleFile = async (file: File) => {
    if (!effectiveBookId) return;
    setTimeoutError(null);
    setPhase('uploading');
    setArtifact(null);
    setActiveSegment(null);
    try {
      const uploaded = await upload.mutateAsync({ file, gameBookId: effectiveBookId });
      setPhase('segmenting');
      const segmented = await segment.mutateAsync({ photoId: uploaded.id });
      setArtifact(segmented);
      setPhase('segments_ready');
    } catch {
      setPhase('idle');
    }
  };

  const handleAbort = useCallback(() => {
    sse.stop();
    setTimeoutError(null);
    setPhase(prev => (prev === 'translating' ? 'segments_ready' : 'idle'));
  }, [sse]);

  const HARD_TIMEOUT_MS = 20_000;
  useEffect(() => {
    if (phase !== 'uploading' && phase !== 'segmenting' && phase !== 'translating') return;
    const timerId = window.setTimeout(() => {
      if (sse.isComplete) return;
      sse.stop();
      setTimeoutError(LABELS.timeoutError);
      setPhase(prev => (prev === 'translating' ? 'segments_ready' : 'idle'));
    }, HARD_TIMEOUT_MS);
    return () => window.clearTimeout(timerId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [phase]);

  const handlePickSegment = (paragraphNumber: number) => {
    if (!artifact || !effectiveBookId) return;
    if (segmentBlockedByLang) return; // defensive — picker should be disabled
    const seg = artifact.segments.find(s => s.paragraphNumber === paragraphNumber);
    if (!seg) return;
    setActiveSegment(seg);
    setPhase('translating');
    sse.start(campaignId, artifact.id, paragraphNumber, effectiveBookId, confirmedLang ?? undefined);
  };

  if (phase === 'translating' && sse.isComplete) {
    setPhase('translated');
  }

  const handleBadgeTap = () => {
    if (!artifact?.id) return;
    const mode: ModalMode = tier === 'medium' ? 'auto-medium-tap' : 'manual';
    setModalState({
      open: true,
      mode,
      preselect: confirmedLang ?? sse.detectedSourceLang ?? undefined,
    });
    trackEvent('translate.lang_modal_opened', {
      photoId: artifact.id,
      tier,
      mode,
    });
  };

  const handleModalOpenChange = (open: boolean) => {
    if (!open && artifact?.id) {
      trackEvent('translate.lang_modal_dismissed', {
        photoId: artifact.id,
        tier,
      });
    }
    setModalState(prev => ({ ...prev, open }));
  };

  const handleModalConfirm = (chosen: SourceLangCode) => {
    if (!artifact?.id) return;
    const previous = confirmedLang ?? sse.detectedSourceLang ?? null;
    setConfirmedLang(chosen);
    setModalState(prev => ({ ...prev, open: false }));
    if (chosen !== previous) {
      trackEvent('translate.lang_overridden', {
        photoId: artifact.id,
        fromLang: previous,
        toLang: chosen,
      });
      // Re-translate if a segment is already active (DEC-FE-7)
      if (activeSegment && effectiveBookId) {
        setPhase('translating');
        sse.start(campaignId, artifact.id, activeSegment.paragraphNumber, effectiveBookId, chosen);
      }
    }
  };

  // Modal dismissable: true everywhere per locked Opt B (dismissable + SegmentPicker block)
  const modalDismissable = true;

  const errorMessage =
    upload.error?.message ?? segment.error?.message ?? sse.error ?? timeoutError ?? undefined;

  const isBusy = phase === 'uploading' || phase === 'segmenting' || phase === 'translating';
  const cameraDisabled = isBusy || !effectiveBookId;
  const segmentPickerDisabled = phase === 'translating' || segmentBlockedByLang;

  // Show badge after segments_ready (post-OCR)
  const showBadge =
    (phase === 'segments_ready' || phase === 'translating' || phase === 'translated') &&
    artifact !== null &&
    renderedTier !== 'none';

  return (
    <div className="grid gap-4 px-4 py-6 sm:px-6" data-reader-mode={String(isReaderMode)}>
      <header className="flex flex-wrap items-center gap-3">
        <div className="flex flex-1 items-center justify-between gap-2">
          <h1 className="text-xl font-semibold text-[var(--c-game)]">Traduci pagina libro game</h1>
          <ReaderModeToggle isReaderMode={isReaderMode} onToggle={toggleReaderMode} />
        </div>
        {showBadge && (
          <LangBadge
            lang={effectiveLang}
            confidence={sse.langDetectionConfidence ?? null}
            tier={renderedTier}
            onTap={handleBadgeTap}
            isOverride={isOverride}
          />
        )}
      </header>

      {narrativeBooks.length > 1 && (
        <section
          className="rounded-lg border border-[var(--c-game)]/20 bg-background p-4 grid gap-2"
          data-testid="translate-viewer-book-picker-section"
        >
          <p className="text-sm text-muted-foreground">Da quale libro proviene questa pagina?</p>
          <BookPicker
            books={narrativeBooks}
            value={effectiveBookId ?? ''}
            onChange={setSelectedBookId}
          />
        </section>
      )}

      {books !== undefined && narrativeBooks.length === 0 && (
        <p
          className="text-sm text-destructive"
          role="alert"
          data-testid="translate-viewer-no-narrative-books"
        >
          Questo gioco non ha libri narrativi disponibili per photo-translate.
        </p>
      )}

      <section className="rounded-lg border border-[var(--c-game)]/20 bg-background p-4 grid gap-3">
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          capture="environment"
          aria-label="Seleziona foto da tradurre"
          onChange={e => {
            const f = e.target.files?.[0];
            if (f) void handleFile(f);
          }}
          className="hidden"
          data-testid="photo-input"
        />
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={cameraDisabled}
          className="rounded-md bg-[var(--c-agent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          data-testid="open-camera-button"
        >
          {phase === 'idle' || phase === 'translated' ? 'Scatta o scegli foto' : 'In corso…'}
        </button>
        {uiStep && <LoadingSkeleton uiStep={uiStep} />}
        {isAbortableStep(uiStep) && <AbortButton onClick={handleAbort} />}
        {errorMessage && (
          <p className="text-sm text-destructive" role="alert" data-testid="translate-viewer-error">
            {errorMessage}
          </p>
        )}
        {segmentBlockedByLang && (
          <p
            className="text-sm text-muted-foreground"
            role="status"
            data-testid="translate-viewer-lang-block-hint"
          >
            Conferma la sorgente per tradurre.
          </p>
        )}
      </section>

      {artifact &&
        (phase === 'segments_ready' || phase === 'translating' || phase === 'translated') && (
          <SegmentPicker
            segments={artifact.segments}
            onPick={handlePickSegment}
            disabled={segmentPickerDisabled}
          />
        )}

      {activeSegment && (phase === 'translating' || phase === 'translated') && (
        <TranslationPane
          partialText={sse.partialText}
          isComplete={sse.isComplete}
          appliedTerms={sse.appliedTerms}
          sourceTextEn={activeSegment.sourceText}
          error={sse.error}
        />
      )}

      <LangOverrideModal
        open={modalState.open}
        onOpenChange={handleModalOpenChange}
        preselect={modalState.preselect}
        dismissable={modalDismissable}
        onConfirm={handleModalConfirm}
      />
    </div>
  );
}
```

- [ ] **Step 4: Flesh out S1, S2, S3, S5, S7, S8 test bodies**

Now go back to the test file and replace each `// - ...` comment block with concrete assertions matching the existing `TranslateViewer.test.tsx` test infra. The existing patterns there use the established MSW handlers + fake EventSource + render helpers. Reuse them.

Key reusable assertion patterns:
```ts
import * as analyticsModule from '@/lib/analytics/track-event';
// ...
const trackSpy = vi.spyOn(analyticsModule, 'trackEvent');
// ...
expect(trackSpy).toHaveBeenCalledWith('translate.lang_detected', expect.objectContaining({
  lang: 'EN',
  tier: 'high',
}));
```

Then:

Run: `cd apps/web && pnpm test -- --run src/components/features/gamebook/__tests__/TranslateViewer.test.tsx`
Expected: PASS — all S1, S2, S3, S5, S7, S8 scenarios (existing tests still pass)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/TranslateViewer.tsx \
        apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx
git commit -m "T6 TranslateViewer wire badge+modal+analytics+SegmentPicker block (#1559)"
```

---

## Task 7: Re-translate flow + S4 + jest-axe full viewer

**Files:**
- Modify: `apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx` (add S4 + S6)

- [ ] **Step 1: Add S4 + S6 test scenarios**

Append to the same test file under the `#1559` describe block:

```tsx
  describe('#1559 — S4: re-translate via sourceLangOverride after manual modal', () => {
    it('user re-opens modal manually + changes lang + confirms → SSE re-fires with override', async () => {
      // 1. Setup: translate completes with detected="EN", confidence=0.92 (S1 setup)
      // 2. Translation displayed in TranslationPane
      // 3. User clicks LangBadge ("Sorgente: EN")
      //    → modal opens mode='manual' with preselect="EN"
      //    → trackEvent fires 'translate.lang_modal_opened' {mode: 'manual', tier: 'high'}
      // 4. User selects "FR" radio + clicks "Conferma e ritraduci"
      // 5. Assert:
      //    - modal closes
      //    - trackEvent fires 'translate.lang_overridden' {fromLang:'EN', toLang:'FR'}
      //    - sse.start called again with sourceLangOverride='FR' (verify via lastInstance.url contains 'sourceLangOverride=FR')
      //    - phase transitions to 'translating'
      //    - badge becomes "Sorgente: FR (override)" once SSE final completes
    });

    it('user opens modal but confirms same lang → no re-translate fired', async () => {
      // 1. Same setup as above
      // 2. User clicks badge, modal opens with preselect="EN"
      // 3. User clicks "Conferma" without changing
      // 4. Assert:
      //    - modal closes
      //    - trackEvent does NOT fire 'translate.lang_overridden'
      //    - sse.start NOT called again
      //    - phase stays 'translated'
    });
  });

  describe('#1559 — S6: jest-axe a11y full viewer state', () => {
    it('axe finds zero violations with modal closed + badge tier=high', async () => {
      // Setup S1 baseline. Assert axe(container).toHaveNoViolations()
    });

    it('axe finds zero violations with modal open (auto-low)', async () => {
      // Setup S3 baseline. Assert axe(container).toHaveNoViolations()
    });

    it('axe finds zero violations with modal open (manual reopen)', async () => {
      // Setup S4 baseline up to "modal open". Assert axe(container).toHaveNoViolations()
    });
  });
```

> Note for implementer: implementation in Task 6 should already satisfy S4 and S6 (re-translate logic + a11y from Radix + jest-axe pass-through). This task verifies that AND fills out the remaining concrete test bodies.

- [ ] **Step 2: Run tests to verify all 8 scenarios pass**

Run: `cd apps/web && pnpm test -- --run src/components/features/gamebook/__tests__/TranslateViewer.test.tsx`
Expected: PASS — all S1-S8 scenarios + existing tests

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx
git commit -m "T7 TranslateViewer S4 re-translate + S6 jest-axe full viewer (#1559)"
```

---

## Task 8: Final verification + plan completion note

**Files:**
- Verify: full test suite, lint, typecheck, coverage
- Modify: `docs/superpowers/plans/2026-06-01-fe-1559-lang-detection-override-modal.md` (append completion notes)

- [x] **Step 1: Run full FE test suite**

Run: `cd apps/web && pnpm test -- --run`
Expected: PASS — no regressions, ~50+ new tests added across T2-T7

- [x] **Step 2: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS — no errors

- [x] **Step 3: Lint**

Run: `cd apps/web && pnpm lint`
Expected: PASS — no errors

If lint complains about `local/no-hardcoded-color-utility` on `bg-emerald-100`/`bg-amber-100`/`bg-rose-100` in `LangBadge.tsx`: this is a tier-specific status badge use case (analogous to entity tokens). Add ONE inline disable comment:

```tsx
// eslint-disable-next-line local/no-hardcoded-color-utility -- tier-specific status badge colors (DEC-FE-3)
const TIER_STYLES: ...
```

OR migrate to entity-style tokens if available (check `tokens.css` and `audits/2026-05-12-token-violations.md`). Prefer migration if tokens exist.

- [x] **Step 4: Coverage check on new files**

Run: `cd apps/web && pnpm test:coverage -- --run src/lib/gamebook src/components/features/gamebook/LangBadge.tsx src/components/features/gamebook/LangOverrideModal.tsx`
Expected: coverage ≥ 90% on `LangBadge.tsx`, `LangOverrideModal.tsx`, `lang-tier.ts`, `lang-codes.ts`, `useTranslateSegmentSSE.ts` extension

- [x] **Step 5: Append completion notes to plan doc**

Append to this file:

```markdown

---

## Execution complete (YYYY-MM-DD)

- All 8 tasks executed, 8 commits per TDD
- Test counts: T2 (7) + T3 (6 new in extended file) + T4 (14) + T5 (15) + T6 (5 new in extended file) + T7 (5 new in extended file) = ~52 new tests
- Coverage on new files: ≥90%
- jest-axe: 0 violations on all in-scope states
- Files created: 4 (lang-codes.ts, lang-tier.ts, LangBadge.tsx, LangOverrideModal.tsx) + 3 test files
- Files modified: 2 (useTranslateSegmentSSE.ts, TranslateViewer.tsx) + 2 test files
- DEC-FE-1..DEC-FE-13 all addressed
- Open follow-ups (deferred): localStorage user preferred lang, target lang picker UI, i18n via i18next, manual "Cambia sorgente" tier-none button
```

- [x] **Step 6: Final commit**

```bash
git add docs/superpowers/plans/2026-06-01-fe-1559-lang-detection-override-modal.md
git commit -m "feat(gamebook): T8 plan completion notes (#1559)"
```

---

## Execution complete (2026-06-01)

- All 8 tasks executed; 7 commits on branch `feature/issue-1559-fe-lang-detection-override-modal`
  - T1 `6711420b2`: `lang-codes.ts` — SourceLangCode union + LANG_LABELS_IT + isSourceLangCode
  - T2 `61535d40d`: `lang-tier.ts` + `lang-tier.test.ts` — 7 boundary tests
  - T3 `e48946ace`: `useTranslateSegmentSSE.ts` additive + 6 new test cases (14 total)
  - T4 `aa2026c26`: `LangBadge.tsx` + 13 tests (tier/interaction/aria/axe)
  - T5 `767394f88`: `LangOverrideModal.tsx` + 15 tests (radio/confirm/dismiss/focus/axe)
  - T6 `ba7f033c2`: `TranslateViewer.tsx` full integration + S1/S2/S3/S5/S7/S8 (6 tests)
  - T7 `5d6a50531`: `TranslateViewer.test.tsx` S4a/S4b/S6a/S6b/S6c (5 tests)
- Test counts: T2 (7) + T3 (6 new) + T4 (13) + T5 (15) + T6 (6 new) + T7 (5 new) = **52 new tests**
- `TranslateViewer.test.tsx` totals: **35/35 passing**
- Typecheck: PASS (tsc --noEmit, 0 errors)
- jest-axe: 0 violations across S6a (tier=high), S6b (tier=low+modal open), S6c (tier=medium blocked), S9/S9b/S9c pre-existing
- DEC-FE-1..DEC-FE-13 all addressed
- Files created: 4 source (lang-codes.ts, lang-tier.ts, LangBadge.tsx, LangOverrideModal.tsx) + 3 test files
- Files modified: 2 source (useTranslateSegmentSSE.ts, TranslateViewer.tsx) + 2 test files (useTranslateSegmentSSE.test.tsx, TranslateViewer.test.tsx)
- Open follow-ups (deferred, tracked in #1559 issue body): localStorage user preferred lang, target lang picker UI, i18n via i18next, manual "Cambia sorgente" tier-none button

---

## Self-Review

**1. Spec coverage:**
- DEC-FE-1 (hook extension) → T3 ✓
- DEC-FE-2 (getLangTier helper) → T2 ✓
- DEC-FE-3 (LangBadge label-only) → T4 ✓
- DEC-FE-4 (LangOverrideModal Radix Dialog) → T5 ✓
- DEC-FE-5 (modal state machine trigger logic) → T6 ✓
- DEC-FE-6 (SegmentPicker disable rule) → T6 (`segmentBlockedByLang`) ✓
- DEC-FE-7 (re-translate via sse.start with override) → T6 (`handleModalConfirm`) + T7 (S4 tests) ✓
- DEC-FE-8 (4 analytics events) → T6 (`trackEvent` calls) + T7 (S4 verifies lang_overridden) ✓
- DEC-FE-9 (hardcoded IT i18n) → T1 (LANG_LABELS_IT) + T4 (badge text) + T5 (modal labels) ✓
- DEC-FE-10 (null lang + high conf → tier none fallback) → T6 (`effectiveTier`) ✓
- DEC-FE-11 (confirmedLang resets on artifact change) → T6 (`useEffect [artifact?.id]`) ✓
- DEC-FE-12 (test stack 90% + jest-axe) → T2 + T4 + T5 + T7 (S6) ✓
- DEC-FE-13 (file targets) → T1-T7 (all files) ✓
- G/W/T S1-S8 → T6 (S1, S2, S3, S5, S7, S8) + T7 (S4, S6) ✓

**2. Placeholder scan:** No TODO/TBD/"implement later" in code blocks. Test scenario blocks in T6 use `//` pseudo-comments BUT instruct implementer to flesh out via existing patterns — acceptable per skill rules (pattern reuse instruction is concrete enough). T8 explicitly states to fill in remaining test bodies.

**3. Type consistency:**
- `SourceLangCode = 'EN' | 'FR' | 'DE' | 'ES' | 'IT'` used consistently across T1, T3, T4, T5, T6, T7
- `LangTier = 'high' | 'medium' | 'low' | 'none'` consistent T2, T4, T6
- `getLangTier(confidence: number | null | undefined)` signature stable
- `trackEvent(name: string, props?: Record<string, unknown>)` per analytics infra confirmed
- All test files reference correct relative paths via `../` for sibling source files

All consistent. Plan ready for execution.

---

## Execution Handoff

Two execution options:

**1. Subagent-Driven (recommended)** — Fresh subagent per task, review between tasks, fast iteration (P120 mix-model: haiku for T1+T2+T3, sonnet for T4+T5+T6+T7+T8)

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch with checkpoints

Recommended: Subagent-Driven mix-model.
