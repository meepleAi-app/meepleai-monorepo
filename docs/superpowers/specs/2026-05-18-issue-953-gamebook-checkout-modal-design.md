# Gamebook Quota/Credits Checkout Modal — Design Spec

**Issue**: [#953 — V2 SP6 Iter 1B: Implement quota/credits widget](https://github.com/meepleAi-app/meepleai-monorepo/issues/953)
**Branch**: `feature/issue-953-quota-credits-checkout-modal`
**Parent**: `main-dev`
**Wave**: SP6 Iter 1B (companion to #950 SP7, which lives on a separate branch)
**Mockup**: `admin-mockups/design_files/sp6-libro-game-quota-credits.jsx` (7 states, mobile 375 + desktop 1440)
**Brief reference**: lines 605-712 (pack catalog), 681-705 (states), 709-710 (visual-only constraints)

---

## 1. Background & scope drift

### Initial issue interpretation
The issue title says "quota/credits widget" — Phase 0 audit category **C — da creare ex novo (no existing UI)**. Acceptance criteria propose an embedded card under `apps/web/src/components/v2/gamebook/QuotaCreditsCard.tsx`.

### Reality on disk
1. `QuotaWidget.tsx` **already exists** under `apps/web/src/components/features/gamebook/` (shipped by issue #788 SP6 Phase B Task 2), wired into `GamebookIndexView.tsx` with three variants `default`/`soft`/`hard` derived from `deriveGamebookIndexState` FSM.
2. The mockup `sp6-libro-game-quota-credits.jsx` referenced by #953 is **not** a passive widget. It is a multi-step modal checkout flow (4 steps + an independent soft warning at 47/50).
3. The `apps/web/src/components/v2/gamebook/` path in the AC is **stale**: the DS de-versioning effort (PR #1112, umbrella #1023 closed 2026-05-18) moved feature compositions to `apps/web/src/components/features/<feature>/`. Path correction needed.
4. The mockup states explicitly (Brief §709): "Step 3 form payment è VISUAL ONLY (non integra real Stripe Elements). Test card 4242 placeholder."

### Resolved scope (this spec)
This issue is **the checkout modal**, not the display widget. Specifically:

- A 4-step modal (`Step 1` quota reached → `Step 2` pack picker → `Step 3` checkout form visual-only → `Step 4` success) opened from the existing `QuotaWidget` upgrade CTA.
- An independent **soft warning** component at 90%+ quota usage, rendered as a toast on mobile and a centered modal on desktop, dismissable for the current browser session.
- **100% frontend.** No backend endpoints, no real Stripe integration, no DB persistence. The "Paga" button uses `setTimeout` to simulate latency and a deterministic dev-flag for success/failed sub-state.
- A `success` callback fires when the user completes Step 4, allowing the parent (`GamebookIndexView`) to optimistically invalidate quota queries. Replacing the simulated transaction with a real Stripe call later is a contained swap.

### Why frontend-only
The mockup is explicitly labeled VISUAL ONLY. No `GET /api/v1/budget/packs` endpoint exists; no `POST /api/v1/budget/checkout-intent` exists; pack catalog is not yet defined in any backend domain model. Building these for a flow whose UX is gated as "demo" would be premature. Reversibility is high — when real Stripe wiring lands, the swap touches only Step 3's submit handler and Step 4's confirmation rendering.

---

## 2. Acceptance criteria (SMART)

| ID | Requirement | Validation |
|----|-------------|------------|
| **AC-1** | `QuotaWidget` `onUpgradeClick` prop is wired in `GamebookIndexView` to open `CheckoutModal`. `variant=hard` opens at Step 1; `variant=soft` opens at Step 2 (pack picker, skipping the "quota reached" announcement). | RTL test asserting both entry points; manual visual verification in Storybook/Chromatic |
| **AC-2** | `CheckoutModal` renders 4 steps with `StepIndicator` (existing component reused). Navigation: Step 1 → Step 2 (via "Acquista" CTA), Step 2 → Step 3 (via "Continua"), Step 3 → Step 4 (via "Paga" success outcome). Step 3 → Step 2 is reachable via the "← Torna ai pacchetti" link. No Step 4 → previous transition exists (terminal success state). ESC key, close X, and Step 4 "Torna al gioco" all close the modal. | RTL spec covering all transitions + ESC keydown handler |
| **AC-3** | Step 3 "Paga" button simulates a 2-second loading state, then transitions to either Step 4 (success) or shows a red banner (`role="alert"`) with re-tryable form (failed). Outcome is deterministic in tests via `__testPaymentResult` prop. | Two RTL specs (one per outcome); 2s delay confirmed via fake timers |
| **AC-4** | `SoftWarningCredits` component renders as toast (`role="status"`) on `variant="toast-mobile"` and as modal (`role="dialog"`) on `variant="modal-desktop"`. `onUpgrade` opens `CheckoutModal` at Step 2. `onDismiss` persists dismissal to `localStorage` under key `gamebook.soft-warning.dismissed-at` (ISO timestamp); subsequent mounts within the same browser session skip rendering. | RTL spec covering both variants + localStorage roundtrip + auto-dismiss on `< 90%` usage |
| **AC-5** | `useSoftWarningDismissal` hook auto-shows the warning when `used/total >= 0.9 && < 1.0` AND no dismissal exists for the current session. Session boundary = browser tab lifecycle (sessionStorage, not localStorage). | Hook unit test covering all 4 quadrants of (threshold met, dismissed) |
| **AC-6** | Pack catalog lives at `apps/web/src/lib/gamebook/checkout-packs.ts` exporting a readonly `CHECKOUT_PACKS` constant with three packs (Starter/Mid/Pro) matching the mockup figures exactly (€5/100cr, €20/500cr, €35/1000cr). EUR currency display uses `Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })`. | Unit test asserting pack shape + currency format snapshot |
| **AC-7** | All components ship with i18n-injectable label props (no hardcoded Italian strings inside component bodies). Labels are typed (TS interface) and resolved by the parent. This mirrors the `QuotaWidget` pattern (`QuotaWidgetLabels`). | TS type assertion; grep that no Italian string literal appears in component files except inside `*.test.tsx` mock data |
| **AC-8** | a11y: modal traps focus, ESC closes, `aria-modal="true"`, `aria-labelledby` references the active step's H2. Step indicator is `role="list"` with active step having `aria-current="step"`. Success step `<h2>` is `aria-live="polite"`. Confetti is `aria-hidden="true"`. `prefers-reduced-motion` disables confetti animation + spinner. | `@axe-core/playwright` zero-violations on all 7 mockup states; manual keyboard navigation pass |
| **AC-9** | Playwright `@ci` E2E spec exercises the happy path: Soft warning visible → click Upgrade → Step 2 → Continua → Step 3 → Paga (success forced via `data-test-payment="success"` URL search param read by `CheckoutModal` only when `NODE_ENV !== 'production'`) → Step 4 → Torna al gioco closes modal. Quota state seeded at 47/50 via the existing `visual-test-fixture.ts` pattern (back-end endpoint does not exist; quota data comes from fixture per Gate B). Spec runs in `apps/web/e2e/gamebook-checkout-modal.spec.ts`. | `pnpm test:e2e -- gamebook-checkout-modal` green |
| **AC-10** | Chromatic visual snapshots cover all 7 mockup states (state-01..state-07) in light + dark + mobile (375) + desktop (1440) variants. | Chromatic build green on PR |
| **AC-11** | `v2-migration-matrix.md` row for SP6 quota-credits is added (or updated if pre-stubbed) with `Status = done`, `PR = #N`. Update happens in the same PR. | Manual matrix scan; PR review |
| **AC-12** | Bundle impact: net JS added to the `/gamebook` route < 12 KB gzipped (Step 1-4 + SoftWarning + StepIndicator reuse). Measured via Next.js bundle analyzer comparison before/after. | `pnpm build` output diff in PR description |

**Note on AC-10**: this matches the FREEZE-era token-class discipline from `CLAUDE.md` — all colors must use semantic tokens (`bg-entity-event`, `text-entity-toolkit`, `bg-card`, `text-muted-foreground`) and the `local/no-hardcoded-color-utility` ESLint rule must stay green.

---

## 3. Architecture & component contracts

### 3.1 File tree

```
apps/web/src/components/features/gamebook/
├── QuotaWidget.tsx                       [MODIFY] add onUpgradeClick wiring assertion
├── StepIndicator.tsx                     [REUSE]  already exists, used by photo-upload
├── CheckoutModal.tsx                     [NEW]    4-step orchestrator
├── SoftWarningCredits.tsx                [NEW]    toast | modal variant
├── checkout/
│   ├── Step1QuotaReached.tsx             [NEW]    pure presentational
│   ├── Step2PackPicker.tsx               [NEW]    pure presentational + onSelect
│   ├── Step3CheckoutForm.tsx             [NEW]    form visual-only, 3 sub-states (filled/loading/failed)
│   └── Step4Success.tsx                  [NEW]    confetti + reduced-motion guard
└── __tests__/
    ├── CheckoutModal.test.tsx            [NEW]
    ├── SoftWarningCredits.test.tsx       [NEW]
    └── checkout/
        ├── Step1QuotaReached.test.tsx    [NEW]
        ├── Step2PackPicker.test.tsx      [NEW]
        ├── Step3CheckoutForm.test.tsx    [NEW]
        └── Step4Success.test.tsx         [NEW]

apps/web/src/lib/gamebook/
├── checkout-packs.ts                     [NEW]    PACKS constant + types
└── hooks/
    └── useSoftWarningDismissal.ts        [NEW]    sessionStorage-backed dismissal

apps/web/src/app/(authenticated)/gamebook/_components/
└── GamebookIndexView.tsx                 [MODIFY] wire CheckoutModal + SoftWarningCredits + useSoftWarningDismissal hook

apps/web/e2e/
└── gamebook-checkout-modal.spec.ts       [NEW]    @ci happy path
```

### 3.2 State machine — modal

```
        ┌──────────────┐
        │   closed     │←── ESC | Close X | Step 4 "Torna al gioco"
        └──────┬───────┘
               │ open({ initialStep: 1 | 2 })
               ↓
   variant=hard ────→ Step 1 ──"Acquista"──→ Step 2
                                                │
                              variant=soft ─────┘
                                                │ "Continua"
                                                ↓
                                             Step 3
                                                │ "Paga"
                                                ↓
                                       [2s setTimeout]
                                                │
                                  ┌─────────────┴─────────────┐
                                  │                           │
                                  ↓ success                   ↓ failed
                               Step 4              Step 3 + red banner
                                                   "Torna ai pacchetti" → Step 2
                                                   (or retry "Paga")
```

The modal owns this state machine via `useState<{ step: 1 | 2 | 3 | 4; paymentState: 'idle' | 'loading' | 'failed' }>`. No external state library used.

### 3.3 `CheckoutModal` contract

```typescript
export interface CheckoutLabels {
  readonly modalTitle: (step: 1 | 2 | 3 | 4) => string;
  readonly stepLabels: readonly [string, string, string, string]; // "Quota", "Pacchetto", "Pagamento", "Fatto"
  readonly close: string;          // "Chiudi"
  readonly step1: {
    readonly heading: string;       // "Quota raggiunta"
    readonly subheading: string;    // "Hai tradotto 50 paragrafi questo mese..."
    readonly quotaLabel: string;    // "Quota mensile"
    readonly resetIn: string;       // "Reset tra 12 giorni"
    readonly primaryCta: string;    // "💎 Acquista 100 crediti (€5)"
    readonly secondaryCta: string;  // "⏸️ Continua senza traduzione"
    readonly explainLink: string;   // "Cosa sono i crediti? →"
  };
  readonly step2: {
    readonly heading: string;       // "Scegli il tuo pacchetto"
    readonly subheading: string;    // "Più paragrafi = miglior prezzo. I crediti non scadono."
    readonly disclaimer: string;
    readonly totalLabel: string;    // "Totale"
    readonly continueCta: string;   // "Continua →"
    readonly packBadges: { popular: string; save: string };
  };
  readonly step3: {
    readonly heading: string;       // "Pagamento"
    readonly summary: (packName: string, credits: number, eur: string) => string;
    readonly fieldLabels: { card: string; expiry: string; cvc: string; name: string; country: string };
    readonly trustChips: readonly [string, string, string]; // SSL / Stripe / Refund
    readonly payCta: (eur: string) => string;
    readonly loadingCta: string;    // "Elaborazione…"
    readonly backLink: string;      // "← Torna ai pacchetti"
    readonly failedBanner: { title: string; detail: string };
  };
  readonly step4: {
    readonly title: string;         // "Crediti aggiunti!"
    readonly subtitle: (credits: number) => string;
    readonly recapLabels: { previous: string; purchased: string; balance: string; freeQuotaTitle: string; resetIn: string; rate: string };
    readonly backToGameCta: string; // "🎯 Torna al gioco →"
    readonly receiptLink: (email: string) => string;
  };
}

export interface CheckoutModalProps {
  readonly open: boolean;
  readonly initialStep: 1 | 2;
  readonly quota: {
    readonly used: number;       // e.g. 50
    readonly total: number;      // e.g. 50
    readonly resetDate: string;  // "1 giugno"
    /**
     * Existing credit balance shown in Step 4 recap as "Crediti precedenti".
     * MVP: always 0 (no balance persistence). Future PR introducing real
     * credit accounting will source this from `UserBudgetStatus.creditBalance`.
     */
    readonly previousCredits: number;
  };
  readonly userEmail: string;
  readonly labels: CheckoutLabels;
  readonly onClose: () => void;
  readonly onPurchaseSuccess: (packId: 'starter' | 'mid' | 'pro', creditsAdded: number) => void;
  /** TEST-ONLY deterministic override. Production code never sets this. */
  readonly __testPaymentResult?: 'success' | 'failed';
}
```

The handler `onPurchaseSuccess` is the integration seam for the future Stripe wiring — the parent uses it today to optimistically invalidate quota queries.

### 3.4 `SoftWarningCredits` contract

```typescript
export interface SoftWarningCreditsLabels {
  readonly title: string;        // "Quasi alla fine della quota"
  readonly subtitle: (used: number, total: number, remaining: number) => string;
  readonly upgradeCta: string;   // "Acquista crediti ora" (desktop) | "Acquista crediti" (mobile)
  readonly dismissCta: string;   // "Ok, continua" (desktop) | "Ok" (mobile)
  readonly close: string;        // for X button on desktop modal
}

export interface SoftWarningCreditsProps {
  readonly used: number;
  readonly total: number;
  readonly variant: 'toast-mobile' | 'modal-desktop';
  readonly labels: SoftWarningCreditsLabels;
  readonly onUpgrade: () => void;
  readonly onDismiss: () => void;
}
```

### 3.5 `useSoftWarningDismissal` hook

```typescript
export function useSoftWarningDismissal(used: number, total: number): {
  readonly shouldShow: boolean;
  readonly dismiss: () => void;
};
```

Logic:
- `shouldShow = true` ⇔ `0.9 <= used/total < 1.0` AND `sessionStorage.getItem('gamebook.soft-warning.dismissed-at') === null`.
- `dismiss()` sets the sessionStorage key to `new Date().toISOString()`.
- Returns memoized values; safe to call from a server component? No — the hook reads `sessionStorage`, so callers must mark themselves `'use client'`.
- Edge case: if `total <= 0`, returns `shouldShow: false` defensively.

### 3.6 `checkout-packs.ts` data shape

```typescript
export interface CheckoutPack {
  readonly id: 'starter' | 'mid' | 'pro';
  readonly name: string;          // for display, but i18n labels override
  readonly priceEur: number;
  readonly credits: number;
  readonly perParagraphEur: number;
  readonly badge: 'popular' | 'save' | null;
}

export const CHECKOUT_PACKS: readonly CheckoutPack[] = [
  { id: 'starter', name: 'Starter', priceEur: 5,  credits: 100,  perParagraphEur: 0.05,  badge: 'popular' },
  { id: 'mid',     name: 'Mid',     priceEur: 20, credits: 500,  perParagraphEur: 0.04,  badge: null      },
  { id: 'pro',     name: 'Pro',     priceEur: 35, credits: 1000, perParagraphEur: 0.035, badge: 'save'    },
] as const;

export function formatEur(value: number): string {
  return new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR', minimumFractionDigits: value % 1 === 0 ? 0 : 2 }).format(value);
}
```

The `name` field is kept for development clarity and Chromatic visual snapshots. Production renders use `labels.step2.packNames[pack.id]` injected by the parent (Wave D.3 pattern).

---

## 4. Styling discipline

Compliance with the **token-canonicalization** freeze (CLAUDE.md, DS-15 completed 2026-05-12):

- ❌ Forbidden: `bg-white`, `text-slate-*`, `text-gray-*`, any hardcoded neutral palette utility. Caught by ESLint rule `local/no-hardcoded-color-utility` (error level since DS-15).
- ✅ Required semantic tokens: `bg-background`, `bg-card`, `bg-muted`, `text-foreground`, `text-muted-foreground`, `border-border`, `border-border-strong`.
- ✅ Entity utilities for the toolkit/event accent palette: `bg-entity-toolkit`, `text-entity-toolkit`, `ring-entity-toolkit/30`, `bg-entity-event/10`, `border-entity-event/35`.
- ✅ The mockup's `entityHsl(...)` helper translates to Tailwind utilities directly:
  - `entityHsl('toolkit')` → `bg-entity-toolkit` / `text-entity-toolkit`
  - `entityHsl('event')` → `bg-entity-event` / `text-entity-event`
  - `entityHsl('event', 0.06)` → `bg-entity-event/10` (closest token-aligned alpha)
  - `entityHsl('agent', 0.3)` → `border-entity-agent/30`
- The exemption rule applies: `text-white` is allowed on a CTA that already declares a `bg-entity-*` background (mockup `.e-bg` pattern).

CSS animations:
- Confetti (Step 4) and spinner (Step 3 loading) live in CSS scoped to each component using Tailwind's `@keyframes` plugin pattern, or — if simpler — a single `globals.css` block guarded by `data-slot="qc-*"` selectors.
- All animations wrap in `@media (prefers-reduced-motion: reduce)` → no animation, opacity 0 for confetti, no spinner rotation.

---

## 5. Testing strategy

### 5.1 Unit (Vitest)

- `checkout-packs.test.ts`
  - `CHECKOUT_PACKS` has exactly 3 entries
  - Each entry's `perParagraphEur === Math.round((priceEur / credits) * 1000) / 1000` (within tolerance)
  - `formatEur(5)` returns `"5 €"` (it-IT)
  - `formatEur(5.5)` returns `"5,50 €"` (it-IT)
- `useSoftWarningDismissal.test.ts` (4 quadrants)
  - 95% usage, no dismiss → `shouldShow: true`
  - 95% usage, dismissed → `shouldShow: false`
  - 50% usage, any dismiss state → `shouldShow: false`
  - 100% usage (hard) → `shouldShow: false`

### 5.2 Component (Vitest + RTL)

- `Step1QuotaReached.test.tsx` — renders labels, fires `onContinue` on CTA
- `Step2PackPicker.test.tsx` — renders 3 pack cards, default selection = starter, `onSelect` fires with pack id, "Continua" disabled when none selected
- `Step3CheckoutForm.test.tsx` — renders in `filled` | `loading` | `failed` sub-states; `aria-busy` on loading button
- `Step4Success.test.tsx` — renders recap, confetti present with `aria-hidden`, fires `onClose` on "Torna al gioco"
- `CheckoutModal.test.tsx`
  - Opens at Step 1 when `initialStep=1`
  - Opens at Step 2 when `initialStep=2`
  - Forward navigation Step 1 → 2 → 3 → 4
  - Back link in Step 3 returns to Step 2
  - ESC keydown closes
  - `__testPaymentResult='success'` after 2s (fake timers) → Step 4
  - `__testPaymentResult='failed'` → red banner + form re-visible
  - `onPurchaseSuccess` fires once at Step 4 entry
  - Focus is trapped (focus does not escape to body on Tab from last element)
- `SoftWarningCredits.test.tsx`
  - `variant='toast-mobile'` renders `role="status"` + bottom position
  - `variant='modal-desktop'` renders `role="dialog"` + center position
  - Both fire `onUpgrade` callback
  - Dismiss persists to sessionStorage; re-mount with same session does not render (handled at hook level, but verified here via integration with parent that uses the hook)

### 5.3 E2E (Playwright @ci)

- `apps/web/e2e/gamebook-checkout-modal.spec.ts`
  1. Auth as test user → seed quota state at 47/50 (via test fixture or admin endpoint if available; otherwise mock at MSW boundary)
  2. Visit `/gamebook`
  3. Assert `SoftWarningCredits` visible
  4. Click "Acquista crediti" → modal opens at Step 2
  5. Pack picker: "Starter" pre-selected → click "Continua" → Step 3
  6. Assert form visible (visual-only fields) → click "Paga €5"
  7. Wait 2s → Step 4 visible, recap shows +100 credits
  8. Click "Torna al gioco" → modal closes, page focus returns to upgrade CTA

Test annotated `@ci` so it runs on every PR. No `@dogfood` variant for this feature (no real payment to validate).

### 5.4 Visual regression (Chromatic)

- 7 mockup states × 2 themes (light/dark) × 2 viewports (375 / 1440) = 28 snapshots
- Stories live in `apps/web/src/stories/gamebook-checkout.stories.tsx`

### 5.5 a11y (axe-core via Playwright)

- Each of the 7 states must have zero `serious` or `critical` violations
- Integrated into the existing `Frontend - A11y E2E` workflow

---

## 6. Non-functional requirements

| NFR | Target | Validation |
|-----|--------|------------|
| Bundle delta on `/gamebook` route | < 12 KB gzipped | `next build` analyzer diff |
| First contentful paint impact | ≤ 50ms delta when modal closed | Lighthouse audit pre/post |
| Modal time-to-interactive on open | < 100ms | Playwright `evaluate` perf trace |
| Keyboard navigation | 100% of interactive elements reachable via Tab | Manual + Playwright keyboard spec |
| Color contrast | ≥ 4.5:1 (AA) on all text | axe-core |
| Cross-browser | Chrome 120+, Firefox 121+, Safari 17+ | Playwright matrix |

---

## 7. Out of scope (carved-out for future PRs)

- Real Stripe Elements integration (`<Elements>` provider, `PaymentElement`, `confirmPayment`)
- Pack catalog endpoint `GET /api/v1/budget/packs` — would belong to a new `BoundedContexts/Billing/` or extend `Administration/Application/Queries/Budget/`
- Checkout intent endpoint `POST /api/v1/budget/checkout-intent`
- Route `/gamebook/checkout/success` (mockup mentions it; not built, the Step 4 success card serves as the success surface today)
- Persisted purchase history view
- Multiple currencies (only EUR supported today; the it-IT locale is hardcoded)
- Soft warning persistence across sessions or devices (today: sessionStorage only)
- Admin pack pricing override
- Promo codes
- Failed payment retry policy beyond "fill the form again"
- Translation history reset upon successful purchase (would be a backend concern)

---

## 8. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Soft warning fires unexpectedly on edge boundary (e.g., `used=45`, `total=50` → 90% exact) | Low | Low | Use `>= 0.9` strictly; unit test covers boundary |
| Focus trap breaks on Safari due to inert-attribute differences | Low | Medium | Confirmed: shadcn/ui `Dialog` primitive lives at `apps/web/src/components/ui/overlays/dialog.tsx` (backed by `@radix-ui/react-dialog`). Use it directly — focus trap, ESC, and ARIA wiring come for free. |
| Bundle bloat from unused CSS animations | Low | Low | Scope animation CSS to `data-slot` selectors; tree-shakeable |
| Failed payment sub-state confuses users (no real card to retry with) | Medium | Low | Banner copy says "form placeholder, in produzione useremo Stripe"; documented in spec |
| Step 4 confetti regresses on iOS 17 Safari (`prefers-reduced-motion` not honored on older WebKit) | Low | Low | Explicit `@media` guard; manual smoke on iOS 17 in PR review |
| User mass-dismisses soft warning then hits 100% with no warning | Low | Low | Hard limit (100%) is already handled by `QuotaWidget` variant=hard, which blocks further translation regardless of dismiss state |
| Pack price change requires re-deploy (no remote config) | Medium | Low | Documented as out-of-scope; future PR adds the GET endpoint |
| ESLint `no-hardcoded-color-utility` slips on mockup-style inline HSL strings | Medium | Medium | Spec mandates entity utilities; lint runs in pre-commit + CI |

---

## 9. Implementation order (preview — full plan in next step)

1. Branch + spec commit (done)
2. `checkout-packs.ts` (pure data, no deps)
3. `useSoftWarningDismissal` hook + test (depends on nothing)
4. Step 1, Step 2, Step 4 components in parallel (pure presentational)
5. Step 3 component + sub-state machine (slightly more logic)
6. `CheckoutModal` orchestrator + test (depends on Step 1-4)
7. `SoftWarningCredits` component + test
8. `GamebookIndexView` wiring (depends on CheckoutModal + SoftWarning + hook)
9. Playwright E2E spec
10. Chromatic stories
11. Bundle measurement + report in PR
12. Matrix update + PR + close issue

Subagent dispatch opportunity at steps 4 (3 parallel agents for Step1/Step2/Step4) and steps 9+10 (E2E + stories independent).

---

## 10. Self-review checklist (filled at write-time)

- [x] **Placeholder scan**: no TBD, TODO, or vague phrases. All open questions resolved during brainstorming.
- [x] **Internal consistency**: AC-1..12 match component contracts in §3 and tests in §5.
- [x] **Scope check**: focused enough for one implementation plan (~8h estimate). No decomposition needed.
- [x] **Ambiguity check**: pack pricing, EUR locale, sessionStorage vs localStorage all explicit. Soft warning trigger threshold pinned (`>= 0.9 && < 1.0`).
- [x] **DDD / CQRS**: not applicable — pure frontend feature, no backend changes, no MediatR endpoints touched.
- [x] **CLAUDE.md compliance**: feature branch on parent `main-dev`; paths under `components/features/` not `components/v2/`; token discipline DS-15 honored; testing target ≥ 85% covered.

---

## 11. References

- Issue: https://github.com/meepleAi-app/meepleai-monorepo/issues/953
- Mockup: `admin-mockups/design_files/sp6-libro-game-quota-credits.jsx` (Brief lines 605-712)
- Related issue (display widget, already shipped): #788
- Related issue (companion SP6 Iter 1B): #952 glossary editor, #954 resume-state flow
- Related issue (sister wave migration, parallel work): #950 SP7 game-night-create (separate branch)
- Spec: `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md` §G4.6 quota+checkout flow
- DS migration: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` (path discipline)
- Token canonicalization: `docs/for-developers/specs/2026-05-12-token-canonicalization.md` (DS-15)
- Migration matrix: `docs/for-developers/frontend/v2-migration-matrix.md`

---

**Status**: ✅ Approved by user 2026-05-18 — proceed to plan via `superpowers:writing-plans`.
