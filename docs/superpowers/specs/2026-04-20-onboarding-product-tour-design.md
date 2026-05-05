# Onboarding Product Tour — Design Spec

**Date**: 2026-04-20
**Owner**: redesign-v2 Phase 2.5 (Task #32)
**Branch**: `refactor/redesign-v2-m6`
**Source mockup**: `admin-mockups/design_files/onboarding.{html,jsx}`

## Goal

Replace the single-step profile form at `/onboarding` with the Claude Design product-tour (5 steps: Welcome → Games → Agents → FirstSession → Complete). The tour is pure UX — no backend persistence for selections — and only calls `completeOnboarding(false)` at the end to unblock library routing.

## Context & decisions

User decisions from brainstorming (2026-04-20):

- **1.a — Replace**: The product-tour fully replaces the existing profile form. `displayName` and avatar capture are deferred to `/settings/profile` (already exists). New users enter library with default displayName (email prefix, backend-handled).
- **2.a — UX only**: No backend calls for game/agent selections during the tour. Pure client-side state + localStorage for step resume.
- **3. Separate**: The existing `OnboardingWizard` (invitation-accept flow) stays untouched. Related components (`WelcomeChecklist`, `OnboardingReminderBanner`, `DiscoverVisitTracker`) are out of scope.

## Architecture

### Route

`/onboarding` — `(authenticated)` route group. Authenticated users with `user.onboardingCompleted === false` are redirected here by existing `AuthProvider` logic (unchanged).

### Flow

```
Welcome (step 0)
  └─ "Inizia il tour →" → Games (1)
  └─ "Salta, esploro da solo" → Complete (4)

Games (step 1)  — min 3 selected
  └─ "Avanti → (n/3)" disabled until 3+ → Agents (2)
  └─ Skip (topbar) → Complete (4)

Agents (step 2)
  └─ "Avanti →" → FirstSession (3)
  └─ Back → Games (1)

FirstSession (step 3)
  └─ Click ActionCard → completeOnboarding(false) → router.push(action.href)
  └─ "Salta" → Complete (4)
  └─ Back → Agents (2)

Complete (step 4)
  └─ "Vai alla home →" → completeOnboarding(false) → router.push('/library')
```

### File structure

```
apps/web/src/app/(authenticated)/onboarding/
  page.tsx                             # server component: metadata + <OnboardingTourClient />
  OnboardingTourClient.tsx             # client: step state, localStorage, transitions
  __tests__/OnboardingTourClient.test.tsx

apps/web/src/components/onboarding/tour/
  WelcomeStep.tsx
  GameSelectStep.tsx
  AgentToggleStep.tsx
  FirstSessionStep.tsx
  CompleteStep.tsx
  Confetti.tsx
  data.ts                              # GAMES, AGENTS, ACTIONS constants
  index.ts
  __tests__/WelcomeStep.test.tsx
  __tests__/GameSelectStep.test.tsx
  __tests__/AgentToggleStep.test.tsx
  __tests__/FirstSessionStep.test.tsx
  __tests__/CompleteStep.test.tsx

apps/web/e2e/
  onboarding-tour.spec.ts              # happy path + skip path
```

**Removed**:
- `apps/web/src/app/(authenticated)/onboarding/page.tsx` (old profile form) — rewritten from scratch
- `apps/web/src/app/(authenticated)/onboarding/__tests__/page.test.tsx` — replaced by new client test

### Data

**`data.ts`** (module-scoped constants, not exported to backend):

```ts
export const GAMES = [
  { id: 'catan', title: 'Catan', year: 1995, players: '3–4', em: '🌾', gradient: ['25 88% 50%', '15 80% 38%'] },
  // + 7 more (carcassonne, ticket, wingspan, 7wonders, terraforming, azul, splendor)
] as const;

export const AGENTS = [
  { id: 'rules', em: '🎲', name: 'Agente Regole', desc: '…', defaultOn: true },
  // + 3 more
] as const;

export const ACTIONS = [
  { id: 'event', em: '🎉', title: 'Crea la prima serata', desc: 'Pianifica con amici', entity: 'event', href: '/game-nights' },
  { id: 'library', em: '🎲', title: 'Esplora la library', desc: 'Vedi i tuoi giochi', entity: 'game', href: '/library' },
  { id: 'chat', em: '💬', title: 'Chatta con un agente', desc: 'Prova una domanda', entity: 'agent', href: '/chat' },
] as const;

export const MIN_SELECTED = 3;
export const STEP_LABELS = ['Giochi', 'Agenti', 'Sessione'] as const;
```

`ACTIONS.href` mapped to real MeepleAI routes (not `home.html` mockup placeholder).

### State (OnboardingTourClient)

```ts
const [step, setStep] = useState<0 | 1 | 2 | 3 | 4>(0);
const [dir, setDir] = useState<1 | -1>(1);
const [animKey, setAnimKey] = useState(0);
const [selectedGames, setSelectedGames] = useState<string[]>([]);
const [agentStates, setAgentStates] = useState<Record<string, boolean>>(() =>
  Object.fromEntries(AGENTS.map(a => [a.id, a.defaultOn]))
);
const [isCompleting, setIsCompleting] = useState(false);
const [completeError, setCompleteError] = useState<string | null>(null);
```

**localStorage**: `mai-onboarding-step` — persist step on change, restore on mount (clamped to 0-4).

### Primitives reused (M6 already-shipped)

- `StepProgress` (Task #17) — 3 dots for steps 1-3 with entity accent
- `Button` primitive v2 (`btn primary`, `btn ghost`)
- Entity tokens: `e-game`, `e-agent`, `e-session`, `e-event`, `e-agent`

### New primitives (inline, not extracted)

These stay inside `tour/` — too specific to extract to `ui/primitives`:

- `GameCard` (selectable tile with emoji cover gradient + check badge) — uses `aria-pressed`
- `AgentRow` (toggle row) — uses `role="switch"` + `aria-checked`
- `ActionCard` (entity-accented action tile with arrow)
- `Confetti` (40 animated pieces, CSS-only via inline styles)
- `BottomBar` (sticky back/next footer)

### Backend integration

- **Only call**: `api.auth.completeOnboarding(false)` at completion (Step 4 "Vai alla home" or Step 3 action click)
- **No** calls to `/library`, `/agents/preferences`, or `/profile`
- Error handling: on failure, toast + retry button; user stays on current step

### Routing guards

- `AuthProvider` existing redirect: `!authLoading && user && !user.onboardingCompleted && pathname !== '/onboarding'` → `/onboarding` (unchanged)
- Client-side redirect: if `user.onboardingCompleted === true` on mount, `router.replace('/library')`
- Auth loading spinner preserved

## Accessibility

- Skip link `Vai al contenuto` → `#ob-main`
- `OnboardingProgress` has `role="progressbar"`, `aria-valuenow`, `aria-valuemax`, `aria-label`
- `GameCard` has `aria-pressed`, descriptive `aria-label`
- `AgentRow` toggle has `role="switch"`, `aria-checked`, `aria-label` reflecting state
- Counter "n di 3 selezionati" uses `aria-live="polite"`
- All interactive controls keyboard-navigable (Tab order: step → skip → content → bottom bar)
- Focus ring visible via existing `focus-visible` ring tokens
- Reduced motion: `prefers-reduced-motion` disables slide animations + confetti

## Testing

### Unit (Vitest + RTL)

- **`WelcomeStep.test.tsx`**: renders userName when provided, renders without userName, calls `onStart` / `onSkip`
- **`GameSelectStep.test.tsx`**: renders 8 cards, toggles selection, counter updates, counter-ready state at 3+
- **`AgentToggleStep.test.tsx`**: renders 4 agents, 3 default-on, toggle fires, active-count live region updates
- **`FirstSessionStep.test.tsx`**: renders 3 actions, click fires `onChoose` with href
- **`CompleteStep.test.tsx`**: renders brand mark + CTA, calls `onHome`; confetti mocked (aria-hidden check)
- **`OnboardingTourClient.test.tsx`**:
  - initial step = 0
  - Welcome "Inizia" → step 1
  - Games gating: Avanti disabled until 3 selected
  - Games → Agents → FirstSession → bottom bar labels correct
  - Back button navigation
  - Skip from Welcome → Complete (step 4)
  - localStorage resume: step written on change, read on mount
  - Complete "Vai alla home" → calls `api.auth.completeOnboarding(false)` → `router.push('/library')`
  - Complete error: API failure shows error + retry

### E2E (Playwright)

`e2e/onboarding-tour.spec.ts` using `userPage` fixture (not-yet-onboarded user):

- Mobile happy path: Welcome → select 3 games → Agents → FirstSession → Complete → `/library`
- Skip path: Welcome skip → Complete → `/library`
- FirstSession direct CTA: click "Esplora la library" → `/library`

Integration with existing auth fixture assumes MSW scenario can provide a user with `onboardingCompleted: false`.

## Error handling

| Scenario | Behavior |
|----------|----------|
| `completeOnboarding` fails | Show inline error above CTA, "Riprova" button, stay on step |
| Network timeout | Same as above; no auto-retry |
| localStorage quota exceeded | Silently catch — resume becomes no-op |
| User navigates away mid-tour | Selections lost (acceptable per decision 2.a); step resumes from localStorage on return |
| User with `onboardingCompleted: true` visits `/onboarding` | `router.replace('/library')` on mount |

## Bundle impact

New route chunks roughly sized:
- `OnboardingTourClient` + 5 step components + Confetti ≈ 8-12KB gzipped
- No new dependencies (reuses `lucide-react`, entity tokens, existing primitives)

Bundle baseline bump expected; update `bundle-size-baseline.json` in the migration PR (same pattern as M2).

## Out of scope

Deferred to Task #33 or future work:
- `WelcomeChecklist` integration (dashboard-level post-onboarding nudge)
- `OnboardingReminderBanner` tie-in
- Backend persistence of game/agent preferences (future)
- Multi-device resume (backend step tracking)
- Dynamic games list from user's library (mockup uses 8-game preset — that's fine)
- `OnboardingWizard` (invitation flow) — untouched
- Avatar upload UX — stays in `/settings/profile`

## Success criteria

- `/onboarding` renders 5-step tour matching Claude Design mockup visual
- Step gating works (min 3 games before Avanti enables)
- Skip and complete paths both call `completeOnboarding(false)` exactly once
- All tests pass (unit + E2E)
- No regression on existing `OnboardingWizard` or `/settings/profile` flows
- Bundle baseline updated in same PR
- Lighthouse a11y ≥ 95 on `/onboarding`

## Related

- Brief: `docs/superpowers/plans/2026-04-20-m6-migration-notes.md` (Next steps → M6.5 product spec)
- Mockup: `admin-mockups/design_files/onboarding.{html,jsx,css}`
- Previous migration: `docs/superpowers/plans/2026-04-20-m6-gruppo1-migration.md` (Task #22 completed M6 Task 7)
- Task #33 follow-up: Login/Register/Verify-email refactor
