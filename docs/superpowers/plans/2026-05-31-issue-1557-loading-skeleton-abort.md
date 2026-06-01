# Plan — #1557 Loading 4-step skeleton + abort CTA

> **Source**: issue [#1557](https://github.com/meepleAi-app/meepleai-monorepo/issues/1557), audit [`translate-gap-report.md §2 row G`](../../../admin-mockups/design_handoff/translate-gap-report.md), Aaron CORE spec [`§1b lines 103-141`](../specs/2026-05-23-mockup-refinement-aaron-core-design.md).
>
> **Branch**: `feature/issue-1557-loading-skeleton-abort-cta` (parent: `main-dev`)
>
> **Effort**: L ~3-4h, FE-only, P1.

## Decisions locked (spec-panel critique sintetizzato)

### DEC-1 — Phase enum strategy (Fowler SRP)

**MANTENERE** `Phase` enum esistente (`idle|uploading|segmenting|segments_ready|translating|translated`) — non rompere FSM SSE/upload/segment. **AGGIUNGERE** `UiStep` type 4-valori per la rendering label:

```ts
type UiStep = 'uploading' | 'ocr' | 'translating' | 'glossary-check';

function deriveUiStep(phase: Phase, sse: { appliedTerms: string[]; isComplete: boolean }): UiStep | null {
  if (phase === 'uploading') return 'uploading';
  if (phase === 'segmenting') return 'ocr';
  if (phase === 'translating') {
    return sse.appliedTerms.length > 0 ? 'glossary-check' : 'translating';
  }
  return null;
}
```

**Rationale**: La spec parla di "4 step nominati" (UI concept), il codice FSM è una proiezione tecnica orthogonal. Non touch ai contratti dei hook.

### DEC-2 — Glossary-check signal (Crispin testability)

Usa `sse.appliedTerms.length > 0` come trigger del 4° step. Se appliedTerms vuoto → skip diretto `translating → translated` (acceptable: no glossary check happened).

**Edge case coperto in S5**: rendering switch da "translating" a "glossary-check" quando arriva il primo applied term, anche se `partialText` continua a streamare.

### DEC-3 — Abort rollback (Cockburn primary actor goal)

| Phase corrente | Click abort → | Effetto |
|---|---|---|
| `uploading` | `idle` | Foto descartata, camera disponibile |
| `segmenting` | `idle` | Foto + OCR descartati, camera disponibile |
| `translating` | `segments_ready` | Artifact preserved, può scegliere altro paragrafo |

Tutti i casi → `sse.stop()` chiamato + `setError(null)` + clearTimeout. **Abort NON visibile in `uploading`** (step 1 troppo veloce per essere abortable utilmente, Aaron CORE §1b: "visibile dal step 2 in poi").

### DEC-4 — Hard timeout 20s (Nygard failure mode)

```ts
useEffect(() => {
  if (phase !== 'uploading' && phase !== 'segmenting' && phase !== 'translating') return;
  const TIMEOUT_MS = 20_000;
  const timerId = window.setTimeout(() => {
    sse.stop();
    setError('Traduzione interrotta: superato il limite di 20 secondi. Riprova.');
    // Rollback come DEC-3
    setPhase(prev => prev === 'translating' ? 'segments_ready' : 'idle');
  }, TIMEOUT_MS);
  return () => window.clearTimeout(timerId);
}, [phase]);
```

**Soft target 17s** = solo commento JSDoc sopra `TIMEOUT_MS` (no UI per ora).

### DEC-5 — Skeleton + reduced-motion fallback (Tailwind built-in)

```tsx
<div
  role="status"
  aria-busy="true"
  aria-live="polite"
  className="space-y-2 max-w-[65ch]"
  data-testid={`translate-skeleton-${uiStep}`}
>
  <p className="text-sm text-muted-foreground" data-testid="translate-step-label">{LABELS[uiStep]}</p>
  <div className="h-4 w-full rounded bg-muted/50 motion-safe:animate-pulse" />
  <div className="h-4 w-[92%] rounded bg-muted/50 motion-safe:animate-pulse" />
  <div className="h-4 w-[78%] rounded bg-muted/50 motion-safe:animate-pulse" />
</div>
```

- `motion-safe:animate-pulse` = Tailwind built-in, zero-config; user con `prefers-reduced-motion: reduce` vede solo background statico
- `role="status" + aria-busy + aria-live="polite"` = SR announce phase change senza interruzione
- `max-w-65ch` = allineamento Aaron CORE J (AAA contrast width)

**Shimmer-sweep gradient**: deferred (Tailwind pulse soddisfa "animated + reduced-motion fallback" AC; mockup nice-to-have).

### DEC-6 — Abort button responsive (Fowler single component)

```tsx
{(uiStep === 'ocr' || uiStep === 'translating' || uiStep === 'glossary-check') && (
  <button
    type="button"
    onClick={handleAbort}
    data-testid="translate-abort-button"
    aria-label={LABELS.abortAriaLabel}
    className="
      fixed bottom-4 right-4 z-50 rounded-full shadow-lg px-4 py-3
      bg-background border border-border text-foreground
      lg:static lg:inline-flex lg:rounded-md lg:shadow-none lg:px-3 lg:py-1.5
      hover:bg-muted focus-visible:ring-2 focus-visible:ring-[var(--c-game)]
    "
  >
    {LABELS.abort}
  </button>
)}
```

Una sola istanza nel DOM. Test assertion via `data-testid`, position-test via class snapshot acceptable.

### DEC-7 — i18n (Wiegers no-scope-creep)

Hardcoded IT, estratti in const `LABELS`:

```ts
const LABELS: Record<UiStep | 'abort' | 'abortAriaLabel' | 'timeoutError', string> = {
  uploading: 'Caricamento foto…',
  ocr: 'Sto leggendo il libro…',
  translating: 'Sto traducendo…',
  'glossary-check': 'Cerco parole nel glossario…',
  abort: 'Annulla',
  abortAriaLabel: 'Annulla la traduzione in corso',
  timeoutError: 'Traduzione interrotta: superato il limite di 20 secondi. Riprova.',
};
```

Coerente con `'Traduci pagina libro game'` hardcoded esistente. Migration i18n out-of-scope (#1557 non lo cita).

## Given/When/Then scenarios

### S1 — Uploading shows skeleton + label
GIVEN `phase === 'uploading'` WHEN render THEN `[data-testid="translate-skeleton-uploading"]` visible AND `[data-testid="translate-step-label"]` text === `'Caricamento foto…'` AND container has `aria-busy="true"`.

### S2 — OCR step shows
GIVEN `phase === 'segmenting'` WHEN render THEN `[data-testid="translate-skeleton-ocr"]` visible AND label === `'Sto leggendo il libro…'`.

### S3 — Translating step shows
GIVEN `phase === 'translating'` AND `sse.appliedTerms === []` WHEN render THEN `[data-testid="translate-skeleton-translating"]` visible AND label === `'Sto traducendo…'`.

### S4 — Glossary-check step on appliedTerms signal
GIVEN `phase === 'translating'` AND `sse.appliedTerms === ['sentinel', 'gold']` AND `!sse.isComplete` WHEN render THEN `[data-testid="translate-skeleton-glossary-check"]` visible AND label === `'Cerco parole nel glossario…'`.

### S5 — Abort hidden during uploading
GIVEN `phase === 'uploading'` WHEN render THEN `[data-testid="translate-abort-button"]` NOT in document.

### S6 — Abort visible from step 2+
GIVEN `phase === 'segmenting'` WHEN render THEN `[data-testid="translate-abort-button"]` visible.

### S7 — Abort during translating reverts to segments_ready
GIVEN `phase === 'translating'` AND artifact present WHEN user clicks abort THEN `sse.stop()` called AND phase becomes `segments_ready`.

### S8 — Hard timeout 20s fires
GIVEN `phase === 'translating'` AND `vi.useFakeTimers()` AND 20_001ms advance THEN `sse.stop()` called AND error contains `'20 secondi'` AND phase becomes `segments_ready`.

### S9 — jest-axe scan
GIVEN skeleton rendered in any of the 4 uiSteps WHEN axe runs THEN `violations.length === 0`.

## Tasks (TDD, mix-model P120)

| # | Task | Model | Effort | Deps |
|---|---|---|---|---|
| T1 | Add `UiStep` type, `LABELS` const, `deriveUiStep()` helper in new module `TranslateViewer.steps.ts` + unit tests for deriveUiStep (covers DEC-1 + DEC-2 mapping logic) | haiku | 20m | — |
| T2 | Build `LoadingSkeleton` component (3-line skeleton + label + aria-busy/live) in new file `LoadingSkeleton.tsx` + unit test (snapshot + a11y attrs per uiStep) | haiku | 20m | T1 |
| T3 | Build `AbortButton` component (responsive FAB/inline) in new file `AbortButton.tsx` + unit test (data-testid, aria-label) | haiku | 15m | — |
| T4 | Wire `LoadingSkeleton` + `AbortButton` into `TranslateViewer.tsx`: replace text labels at lines 147-149, add `handleAbort` callback (FSM rollback per DEC-3), integrate `deriveUiStep` | sonnet | 30m | T1, T2, T3 |
| T5 | Implement 20s hard timeout `useEffect` per DEC-4 in `TranslateViewer.tsx` (setTimeout + clearTimeout on phase change/unmount, JSDoc soft target 17s) | haiku | 15m | T4 |
| T6 | Update `TranslateViewer.test.tsx`: add tests for S1, S2, S3, S5, S6 (rendering-level, mock-driven phase via `act` or test helper exposing internal setter via re-render — NB: existing tests use mock-only, so need helper or refactor to accept controlled phase prop OR test via DOM interaction triggering phase change) | sonnet | 30m | T4, T5 |
| T7 | Add advanced tests for S4 (appliedTerms→glossary), S7 (abort flow), S8 (timeout fake timers) via integration-level setup driving real state transitions through mocked hooks side-effects | sonnet | 30m | T6 |
| T8 | Add S9 (jest-axe scan) in `TranslateViewer.test.tsx` covering 4 uiStep states (mirror `EncounterCheatsheetView.test.tsx:3` pattern) | sonnet | 15m | T7 |

**Total**: ~3h25m. Mix-model breakdown: 4 haiku (mechanical schemas/components/timer), 4 sonnet (integration/judgment/a11y).

## P74 risks identified

- **R1**: Existing test pattern uses `vi.mock` only → no internal state controllability. T6/T7 may need a controlled-prop refactor on `TranslateViewer` OR test-helper. Decision punted to subagent: prefer NOT mutating the public API; use real DOM interactions (file upload → triggers `handleFile` → real phase change) to drive transitions.
- **R2**: `_content.tsx` at `apps/web/src/app/(authenticated)/library/[gameId]/play/[campaignId]/translate/_content.tsx` consumes `TranslateViewer` — verify no breakage. Component public API unchanged in this plan.
- **R3**: `useTranslateSegmentSSE.stop()` signature already exists — verified line 6 of hook. ✅
- **R4**: Shimmer-sweep gradient deferred (Tailwind pulse soddisfa AC; Aaron CORE nice-to-have). Document in PR body as MINOR follow-up if reviewer flags.

## Verification gates

- `pnpm typecheck` (project)
- `pnpm test src/components/features/gamebook/__tests__/TranslateViewer.test.tsx` (focused)
- `pnpm lint:tokens` (DS-15 compliance: NO hardcoded `bg-white`/`text-gray-*`)
- jest-axe 0 violations in all 4 uiStep states

## PR body

```
Closes #1557.

## Summary
- Implement loading 4-step skeleton (uploading/ocr/translating/glossary-check) replacing text labels
- Add abort CTA (FAB mobile, inline desktop) visible from step 2+, rolls back FSM
- Hard timeout 20s with error message + state rollback
- Reduced-motion fallback via Tailwind motion-safe:animate-pulse
- jest-axe scan covers 4 uiStep states

## Tests
- 9 new scenarios (S1-S9) in TranslateViewer.test.tsx
- deriveUiStep unit tests in TranslateViewer.steps.test.ts
- LoadingSkeleton + AbortButton unit tests

## Deferred (acceptable per AC)
- Shimmer-sweep gradient (Tailwind pulse satisfies "animated + reduced-motion fallback" AC; mockup nice-to-have)
- Soft 17s target UI feedback (JSDoc only per DEC-4)
- Foto retake CTA on abort during uploading/segmenting (Aaron CORE §1b mentions but #1557 AC doesn't require)
```
