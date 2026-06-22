# Asse B — UI Shell + Navigation Pattern Implementation Plan (v2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **🚨 v2 — REWRITE post-discovery 2026-06-05**: plan v1 assumeva scratch ma `cascade-navigation-store` ESISTE (Zustand store con DrawerStackEntry + pushDrawer/popDrawer), Drawer primitive Radix+Vaul ESISTE, sonner GIÀ installato. Plan v2 focused su delta reale. Effort rebaseline M+ ~9gg → **M ~6gg (-33%)**.

**Goal:** Implementare le primitive UI cross-route mancanti (token additions + MainSidebar 8 voci + cascade-store generic extension + WizardModal + StatePreview dev-tool + Notifications SSE consumer) sopra le primitive esistenti.

**Architecture:** Estendere `cascade-navigation-store` per DrawerStack generic semantics (mantenendo entity-centric flow). Replicare AdminSidebar pattern per MainSidebar. `dynamic({ssr:false})` per StatePreview garantito tree-shake. SSE consumer reusing asse A `/notifications/stream`.

**Tech Stack:** Next.js 16 (App Router) · React 19 · Tailwind 4 · shadcn/ui · Zustand · Radix Dialog · Vaul · Vitest · Playwright · sonner (already installed)

**Issue**: [#1897](https://github.com/meepleAi-app/meepleai-monorepo/issues/1897) (parent umbrella [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895))
**Spec consolidato**: [`docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`](../specs/2026-06-04-claude-design-alignment-spec-panel-review.md) (Sezione 4 — Asse B)
**Effort target rebaseline**: M ~5-6 gg dev + 1 gg test = **6 gg totali** (vs v1 9 gg)

---

## Decisioni lockate (DEC-1..DEC-6)

| ID | Decisione | Rationale |
|----|-----------|-----------|
| **DEC-1** | Plan v2 focused gaps reali (-33%) | cascade-store + Drawer + sonner già shipped |
| **DEC-2** | Extend `cascade-navigation-store` come DrawerStack canonical | NO duplicate provider; entity-centric flow accettabile |
| **DEC-3** | Notifications counter via SSE `/notifications/stream` | Reuse #5005 stream, live real-time |
| **DEC-4** | StatePreview via `dynamic({ssr:false})` + ESLint rule | Garanzia tree-shake >99% |
| **DEC-5** | Replicate AdminSidebar pattern per `MainSidebar` | Coerenza codebase, test pattern già stabilito |
| **DEC-6** | Vitest + Playwright minimal (no Storybook) | Storybook scope-OUT a sub-issue futura |

---

## Gap Analysis: codebase v1 vs v2

| Spec area v1 | Stato reale | Plan v2 azione |
|--------------|-------------|----------------|
| `sonner` library (MIN-7) | ✅ ESISTE v2.0.7 in `package.json` | **SKIP** |
| `Drawer` primitive (Radix+Vaul multi-mode) | ✅ ESISTE `components/ui/drawer/drawer.tsx` con desktop-right + mobile-bottom | **REUSE** |
| `cascade-navigation-store` | ✅ ESISTE `lib/stores/cascade-navigation-store.ts` con DrawerStackEntry + pushDrawer/popDrawer/closeCascade | **EXTEND** (T3) |
| `AdminSidebar` pattern (config + filter + list) | ✅ ESISTE `components/layout/AdminSidebar/` + `admin-nav/admin-nav-config.ts` | **REPLICATE** (T2) |
| Main user sidebar | ❌ NON ESISTE | **NEW** (T2) |
| `--c-warning-ink` + overlay tokens | ❌ NON ESISTONO | **NEW** (T1) |
| `WizardModal` | ❌ NON ESISTE | **NEW** (T4) |
| `StatePreviewProvider` dev-tool | ❌ NON ESISTE | **NEW** (T5) |
| `prefers-reduced-motion` su Drawer | ❓ Verifica T3 | **AUDIT** in T3 |
| Notifications badge SSE consumer | ❌ NON ESISTE (endpoint shipped asse A) | **NEW** (T6) |

---

## Work Packages v2

| WP | Scope | Effort | Critical path | Sub-task |
|----|-------|--------|---------------|----------|
| **WP1** | Token additions (MAJ-9 gap #37/#38) | S (~3h) | NO | T1 |
| **WP2** | MainSidebar 8 voci (CRIT-3 + DEC-5) | M (~10h) | YES (blocca C+D) | T2 |
| **WP3** | Cascade store audit/extend per generic DrawerStack (DEC-2) | M (~8h) | YES (blocca C+D) | T3 |
| **WP4** | WizardModal primitive (MAJ-1 sync/async normalize) | M (~6h) | NO | T4 |
| **WP5** | StatePreview dynamic-ssr-false (DEC-4 + CRIT-5) | M (~5h) | NO | T5 |
| **WP6** | Notifications SSE consumer (DEC-3) | M (~6h) | NO | T6 |
| **WP7** | Final test + a11y + acceptance | S (~4h) | YES (chiude) | T7 |

**Mix-model hint (P120)**: 3 haiku (T1 token + T7 close + parts T2) + 4 sonnet (T2 nav judgment + T3 cascade extend + T4 wizard + T5 dynamic gate + T6 SSE).

**Total**: 7 task TDD bite-sized. ~6gg effort realistic.

---

## File Structure

### New files
- `apps/web/src/components/layout/MainSidebar/MainSidebar.tsx`
- `apps/web/src/components/layout/MainSidebar/__tests__/MainSidebar.test.tsx`
- `apps/web/src/components/layout/main-nav/main-nav-config.ts`
- `apps/web/src/components/layout/main-nav/filter-nav-by-permission.ts`
- `apps/web/src/components/layout/main-nav/MainNavList.tsx`
- `apps/web/src/components/layout/main-nav/__tests__/main-nav-config.test.ts`
- `apps/web/src/components/ui/wizard-modal/wizard-modal.tsx`
- `apps/web/src/components/ui/wizard-modal/use-wizard-step.ts`
- `apps/web/src/components/ui/wizard-modal/index.ts`
- `apps/web/src/components/ui/wizard-modal/__tests__/wizard-modal.test.tsx`
- `apps/web/src/components/ui/state-preview/state-preview-provider.tsx`
- `apps/web/src/components/ui/state-preview/state-preview-loader.tsx` (dynamic loader)
- `apps/web/src/components/ui/state-preview/index.ts`
- `apps/web/src/components/ui/state-preview/__tests__/state-preview.test.tsx`
- `apps/web/src/hooks/use-notifications-counter.ts` (SSE consumer)
- `apps/web/src/hooks/__tests__/use-notifications-counter.test.tsx`
- `apps/web/e2e/drawer-stack-flow.spec.ts`

### Modified files
- `apps/web/src/styles/design-tokens-canonical.css` (3 new tokens light+dark)
- `apps/web/src/lib/stores/cascade-navigation-store.ts` (add ESC/backdrop/reduced-motion generic semantics)
- `apps/web/src/lib/stores/__tests__/cascade-navigation-store.test.ts` (add missing cases)
- `apps/web/src/components/ui/drawer/drawer.tsx` (verify prefers-reduced-motion)
- `apps/web/.eslintrc` (rule blocco import diretto state-preview)
- `apps/web/src/app/layout.tsx` (mount MainSidebar + StatePreviewProvider dynamic)

---

## WP1 — Token additions (MAJ-9, gap #37 + #38)

### Task 1: Add 3 nuovi token

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Modify: `apps/web/src/styles/design-tokens-canonical.css`
- Test: `apps/web/src/__tests__/styles/design-tokens.test.tsx`

- [ ] **Step 1: Add tokens** light + dark themes (`--c-warning-ink`, `--c-overlay-scrim`, `--c-overlay-gradient-end`)
- [ ] **Step 2: Verify AA contrast** via axe smoke test su `--c-warning-ink` su background cream (`#f7f3ee`)
- [ ] **Step 3: Run `pnpm lint:tokens`** per regenerate inventory
- [ ] **Step 4: Commit** `feat(design-tokens): #1897 add --c-warning-ink + overlay tokens (gap #37/#38)`

**Self-review**:
- [ ] 3 tokens light + dark
- [ ] AA contrast verified (4.5:1 text + 3:1 large)
- [ ] No regression su ESLint rule `local/no-hardcoded-color-utility`

---

## WP2 — MainSidebar 8 voci (CRIT-3 + DEC-5)

### Task 2: MainSidebar config + filter + list + container

**Mix-model**: sonnet · **Effort**: M (~10h)

**Files**: vedi File Structure

> Replica esatta del pattern AdminSidebar (`AdminSidebar.tsx` + `admin-nav-config.ts` + `filter-nav-by-role.ts` + `AdminNavList.tsx`).

- [ ] **Step 1**: Read existing `AdminSidebar.tsx` + `admin-nav-config.ts` + `filter-nav-by-role.ts` + `AdminNavList.tsx` per understand pattern
- [ ] **Step 2**: Create `main-nav-config.ts` con 8 voci:
  ```typescript
  export const MAIN_NAV_ITEMS: ReadonlyArray<MainNavItem> = [
    { id: 'dashboard', label: 'Dashboard', defaultHref: '/dashboard', icon: 'LayoutDashboard' },
    { id: 'library', label: 'Library', defaultHref: '/library', icon: 'Library', gameRelated: true },
    { id: 'games', label: 'Games', defaultHref: '/games?tab=discover', icon: 'Dice5', gameRelated: true },
    { id: 'gamenights', label: 'Game Nights', defaultHref: '/game-nights', icon: 'CalendarHeart' },
    { id: 'sessions', label: 'Sessions', defaultHref: '/sessions', icon: 'History' },
    { id: 'agents', label: 'Agents', defaultHref: '/agents', icon: 'Bot' },
    { id: 'notifications', label: 'Notifications', defaultHref: '/notifications', icon: 'Bell', showCounter: true },
    { id: 'profile', label: 'Profile', defaultHref: '/profile', icon: 'UserCircle' },
  ];
  ```
- [ ] **Step 3**: Create `filter-nav-by-permission.ts` (default: all 8 voci per authenticated user; placeholder per future role-based)
- [ ] **Step 4**: Create `MainNavList.tsx` (mirror `AdminNavList.tsx` pattern, render via config)
- [ ] **Step 5**: Create `MainSidebar.tsx` container (mirror `AdminSidebar.tsx` pattern, mount `MainNavList`)
- [ ] **Step 6**: Tests:
  - `main-nav-config.test.ts`: 8 voci ordine + invariante #20 (Library + Games game-related, no standalone Discover) + Notifications showCounter
  - `MainSidebar.test.tsx`: render 8 voci + Notifications badge counter integration
- [ ] **Step 7**: Mount in `app/layout.tsx` (verify SSR + hydration consistency)
- [ ] **Step 8**: Commit `feat(shell): #1897 MainSidebar 8 voci pattern replicated from AdminSidebar (CRIT-8 + invariante #20)`

**Self-review**:
- [ ] Pattern AdminSidebar replicato 1:1
- [ ] 8 voci in ordine corretto
- [ ] Notifications voce ha `showCounter: true` (T6 connect SSE)
- [ ] /games defaults to ?tab=discover
- [ ] Test regression-free su AdminSidebar tests

---

## WP3 — Cascade store audit/extend (DEC-2)

### Task 3: Cascade store generic DrawerStack semantics

**Mix-model**: sonnet · **Effort**: M (~8h)

**Files:**
- Modify: `apps/web/src/lib/stores/cascade-navigation-store.ts`
- Modify: `apps/web/src/lib/stores/__tests__/cascade-navigation-store.test.ts`
- Modify: `apps/web/src/components/ui/drawer/drawer.tsx` (verify prefers-reduced-motion)
- Test: drawer-stack ESC + backdrop integration test

- [ ] **Step 1**: Audit existing `cascade-navigation-store.test.ts` per identificare gap test cases (ESC back-step, backdrop closeAll, swap drawer, max-depth, cross-link)
- [ ] **Step 2**: Audit `drawer.tsx` per verificare se `prefers-reduced-motion` è già supportato. Se NO, add `@media (prefers-reduced-motion: reduce) { transition: none }` clause
- [ ] **Step 3**: Extend cascade store con generic semantics se mancanti:
  - ESC key handler globale (listen in provider, call `popDrawer`)
  - Backdrop click → `closeCascade`
  - Max depth guard (3 livelli? evita drawer infiniti)
- [ ] **Step 4**: Add missing test cases:
  - ESC pop drawer (single level + 2 livelli back-step)
  - Backdrop close all
  - Swap drawer content (cross-link entity)
  - Max-depth respect
  - prefers-reduced-motion → animation 0ms
- [ ] **Step 5**: Run tests → PASS
- [ ] **Step 6**: Commit `feat(stores): #1897 cascade-navigation-store generic DrawerStack semantics + ESC/backdrop/prefers-reduced-motion`

**Self-review**:
- [ ] cascade store API estesa MA backward compat (entity-centric flow preserved)
- [ ] prefers-reduced-motion supportato (clause CSS o JS)
- [ ] Test coverage ≥90% cascade store methods
- [ ] Drawer primitive non breaking change

---

## WP4 — WizardModal primitive (MAJ-1)

### Task 4: WizardModal + TypeScript signature

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/web/src/components/ui/wizard-modal/wizard-modal.tsx`
- Create: `apps/web/src/components/ui/wizard-modal/use-wizard-step.ts`
- Create: `apps/web/src/components/ui/wizard-modal/index.ts`
- Test: `apps/web/src/components/ui/wizard-modal/__tests__/wizard-modal.test.tsx`

> MAJ-1 finding: validate flow sync OR async. Normalize via internal helper.

- [ ] **Step 1**: Write failing tests (8+ cases):
  - render first step initially
  - Next advances
  - validate sync returning boolean → handled correctly
  - validate sync returning ValidationResult → handled correctly
  - validate async returning Promise<ValidationResult> → handled correctly
  - Skip on optional step
  - Cancel opens confirmation modal
  - Complete on last step calls onComplete
- [ ] **Step 2**: Implement TypeScript signature:
  ```typescript
  type ValidateResult = boolean | { valid: boolean; errors?: ValidationError[] };
  interface WizardStep {
    title: string;
    content: ReactNode;
    validate?: () => ValidateResult | Promise<ValidateResult>;
    optional?: boolean;
  }
  interface ValidationError {
    field?: string;
    message: string;
  }
  interface WizardModalProps {
    steps: WizardStep[];
    onComplete: (data: unknown) => Promise<void>;
    onCancel: () => void;
  }
  ```
- [ ] **Step 3**: Implement WizardModal component + use Radix Dialog primitive (sopra Drawer Z-index)
- [ ] **Step 4**: Normalize validate flow via helper:
  ```typescript
  async function normalizeValidate(result: ReturnType<NonNullable<WizardStep['validate']>>) {
    const awaited = await Promise.resolve(result);
    if (typeof awaited === 'boolean') return { valid: awaited, errors: [] };
    return awaited;
  }
  ```
- [ ] **Step 5**: ConfirmCancelModal nested per Cancel action
- [ ] **Step 6**: Run tests → PASS
- [ ] **Step 7**: Commit `feat(ui): #1897 WizardModal primitive + sync/async validate normalize (MAJ-1)`

**Self-review**:
- [ ] TypeScript signature accept boolean | ValidationResult | Promise<...>
- [ ] Cancel flow con confirmation modal
- [ ] Skip button only on optional steps
- [ ] Validate errors rendered via role="alert"
- [ ] a11y: dialog role + aria-modal + focus trap

---

## WP5 — StatePreview dynamic-ssr-false (DEC-4 + CRIT-5)

### Task 5: StatePreviewProvider dev-only + tree-shake guarantee

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Create: `apps/web/src/components/ui/state-preview/state-preview-provider.tsx`
- Create: `apps/web/src/components/ui/state-preview/state-preview-loader.tsx`
- Create: `apps/web/src/components/ui/state-preview/index.ts`
- Modify: `apps/web/.eslintrc` (rule blocco import diretto)
- Test: `apps/web/src/components/ui/state-preview/__tests__/state-preview.test.tsx`

- [ ] **Step 1**: Write failing tests:
  - useStatePreview returns "default" when no override
  - setStateFor overrides state in dev
  - production NODE_ENV → setStateFor no-op (override ignored, always "default")
- [ ] **Step 2**: Implement StatePreviewProvider (context + setStateFor) + useStatePreview hook
- [ ] **Step 3**: Implement loader pattern:
  ```typescript
  // state-preview-loader.tsx
  import dynamic from 'next/dynamic';

  export const StatePreviewProvider = dynamic(
    () => import('./state-preview-provider').then(m => m.StatePreviewProvider),
    { ssr: false, loading: () => null }
  );
  ```
- [ ] **Step 4**: ESLint rule:
  ```json
  {
    "rules": {
      "no-restricted-imports": ["error", {
        "patterns": [{
          "group": ["**/state-preview/state-preview-provider"],
          "message": "Use './state-preview-loader' instead (dev-only tree-shake)"
        }]
      }]
    }
  }
  ```
- [ ] **Step 5**: Mount in `app/layout.tsx`:
  ```tsx
  import { StatePreviewProvider } from '@/components/ui/state-preview/state-preview-loader';
  ```
- [ ] **Step 6**: Tree-shake acceptance:
  ```bash
  pnpm build
  ! grep -r 'StatePreviewProvider' .next/static/chunks/
  ```
- [ ] **Step 7**: Run tests → PASS
- [ ] **Step 8**: Commit `feat(ui): #1897 StatePreviewProvider dev-only + dynamic-ssr-false (DEC-4)`

**Self-review**:
- [ ] dynamic({ssr:false}) wrapper
- [ ] ESLint rule prevents direct import
- [ ] Production build → 0 grep matches
- [ ] Tests cover NODE_ENV gate

---

## WP6 — Notifications SSE consumer (DEC-3)

### Task 6: useNotificationsCounter hook via SSE

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/web/src/hooks/use-notifications-counter.ts`
- Create: `apps/web/src/hooks/__tests__/use-notifications-counter.test.tsx`

- [ ] **Step 1**: Write failing tests:
  - Hook returns initial count from GET /notifications/unread-count (fetch-on-mount)
  - SSE event "notification" increments count
  - Hook respects auth (no fetch if anonymous)
  - Reconnect on EventSource error after 5s
  - Cleanup EventSource on unmount
- [ ] **Step 2**: Implement hook:
  ```typescript
  export function useNotificationsCounter() {
    const [count, setCount] = useState(0);
    const [error, setError] = useState<Error | null>(null);

    useEffect(() => {
      // Initial fetch via React Query (cached + revalidate)
      const fetchInitial = async () => {
        const res = await fetch('/api/v1/notifications/unread-count');
        if (!res.ok) return;
        const data = await res.json();
        setCount(data.count);
      };
      fetchInitial();

      // SSE stream
      const eventSource = new EventSource('/api/v1/notifications/stream');
      eventSource.addEventListener('notification', () => {
        setCount(c => c + 1);
      });
      eventSource.onerror = (e) => {
        setError(new Error('SSE disconnected'));
        // Reconnect logic handled by browser automatically with EventSource
      };

      return () => eventSource.close();
    }, []);

    return { count, error };
  }
  ```
- [ ] **Step 3**: Integrate into `MainNavList.tsx` for Notifications voce badge
- [ ] **Step 4**: Test cases:
  - Mock EventSource (vitest-environment-jsdom)
  - Assert count starts at server value
  - Assert SSE event increments count
  - Assert cleanup on unmount
- [ ] **Step 5**: Commit `feat(hooks): #1897 useNotificationsCounter SSE consumer (DEC-3)`

**Self-review**:
- [ ] EventSource cleanup on unmount
- [ ] Initial fetch via /unread-count endpoint (asse A WP4 audit)
- [ ] SSE event handler atomic increment
- [ ] No memory leak (verify via test)

---

## WP7 — Final test + a11y + acceptance

### Task 7: E2E DrawerStack flow + axe AA gate + CLAUDE.md update

**Mix-model**: haiku · **Effort**: S (~4h)

**Files:**
- Create: `apps/web/e2e/drawer-stack-flow.spec.ts`
- Modify: `CLAUDE.md` (asse B implementation summary)
- Modify: spec consolidato changelog

- [ ] **Step 1**: Playwright E2E test:
  ```typescript
  test('DrawerStack flow: open 2 levels + ESC back-step + backdrop closeAll', async ({ page }) => {
    await page.goto('/dashboard');
    // Open first drawer (entity GameNight)
    await page.getByRole('button', { name: /game night/i }).first().click();
    await expect(page.getByRole('dialog')).toBeVisible();

    // Open nested drawer (Player from GameNight)
    await page.getByRole('link', { name: /player/i }).first().click();
    await expect(page.getByText(/player profile/i)).toBeVisible();

    // ESC back-step
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText(/player profile/i)).not.toBeVisible();

    // Backdrop closeAll
    await page.locator('[data-testid="drawer-backdrop"]').click();
    await expect(page.getByRole('dialog')).not.toBeVisible();
  });
  ```
- [ ] **Step 2**: Axe AA scan su pagina con DrawerStack open (3 livelli)
- [ ] **Step 3**: Update CLAUDE.md sezione asse B
- [ ] **Step 4**: Update spec consolidato changelog v2.1 entry
- [ ] **Step 5**: Run full test suite → 0 regression
- [ ] **Step 6**: Final commit `docs(asse-b): #1897 COMPLETE — DrawerStack E2E + a11y + acceptance`

---

## Self-Review Checklist (post-plan v2)

**Spec coverage post-discovery**:
- [x] MAJ-9 token additions → WP1 T1
- [x] CRIT-3 + DEC-5 MainSidebar 8 voci → WP2 T2
- [x] CRIT-2 + DEC-2 cascade store extend → WP3 T3
- [x] MAJ-1 + WizardModal sync/async → WP4 T4
- [x] CRIT-5 + DEC-4 StatePreview dynamic → WP5 T5
- [x] CRIT-4 + DEC-3 Notifications SSE counter → WP6 T6
- [x] E2E + a11y close → WP7 T7

**Placeholder scan**: nessuno. ESLint rule defined inline. EventSource reconnect via browser default.

**Type consistency**:
- `MainNavItem` consistent T2
- `WizardStep`/`ValidateResult`/`ValidationError` consistent T4
- `StateKind` consistent T5

**Critical path**:
- WP1 (tokens) foundation — parallelizable
- WP2 (MainSidebar) blocks asse C + D
- WP3 (cascade extend) blocks asse C + D
- WP4/5/6 parallelizable
- WP7 closes

**Effort verification**:
- WP1: 3h ≈ 0.4gg
- WP2: 10h ≈ 1.3gg
- WP3: 8h ≈ 1gg
- WP4: 6h ≈ 0.8gg
- WP5: 5h ≈ 0.6gg
- WP6: 6h ≈ 0.8gg
- WP7: 4h ≈ 0.5gg
- **Total**: ~42h ≈ 5.5gg ✓ (target 6gg ±)

---

## Execution Handoff

**Plan v2 complete and saved to `docs/superpowers/plans/2026-06-04-asse-b-ui-shell-pattern.md`.**

**Critical path**:
1. WP1 + WP2 + WP3 (foundation) — sequential
2. WP4 + WP5 + WP6 parallel
3. WP7 closes

**Recommended execution**:
1. **Subagent-Driven (recommended)** — Fresh subagent per task con mix-model (3 haiku + 4 sonnet)
2. **Inline Execution** — Praticabile per asse B (~6gg scope), single dev sessione lunga

---

## Changelog

- **2026-06-05 v2**: rewrite post-discovery. cascade-store + Drawer + sonner già shipped → -33% effort. 7 task vs 10 task v1. 6 decisioni lockate (DEC-1..DEC-6).
- **2026-06-04 v1**: initial plan. Assumed scratch UI primitives, sovra-stimato.
