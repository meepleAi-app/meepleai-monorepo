# onboarding — Axis Discovery

**Source HTML**: `admin-mockups/design_files/onboarding.html`
**JSX twin**: `admin-mockups/design_files/onboarding.jsx`
**Phase B classification**: `design_intent: current` · no `pair_disagreement`
**Mockup canonical**: HTML (per MOCKUPS_INDEX pairing rule)

## Mockup stage layout

The mockup renders an `.ob-frame` (phone-style on desktop, 420px max-width)
holding a single-step view at a time. State is owned by `OnboardingApp` root
(`onboarding.jsx:343-…`) with `step` controlled by `useState(0)` and
persisted to `localStorage('mai-onboarding-step')` (lines 354-359).

The mockup stage is a **single-frame interactive demo** — the viewer steps
through 0 → 4 via inner CTAs. For Storybook matrix purposes we split the
demo into 5 frames (one per step), exposing the `step` axis.

## Axis (canonical)

| Axis | Type | Values | Source | Notes |
|------|------|--------|--------|-------|
| `step` | enum | `0` (welcome) \| `1` (games) \| `2` (agents) \| `3` (session) \| `4` (complete) | `onboarding.jsx:344` (`useState(0)`), `STEP_LABELS` (line 36) | Mockup persists to localStorage |
| `state` | enum | `default` \| `selected-min3` \| `all-selected` \| `completion-confetti` | `MIN_SELECTED=3` gate (jsx:37) + selection state from `gamesSel` Set | Drives selection chip rendering |
| `userName` | string \| null | `'Marco'` \| `null` \| custom | `OnboardingGenericWizard` props (codebase, NOT mockup) | Step 1 title interpolation |

The codebase `OnboardingGenericWizard` does NOT mirror the mockup's 5-step
flow — it ships a 3-step compressed flow (Asse D P3, sub-issue #1899
follow-up). Documented as divergence below.

## Frame matrix (Desktop only Phase C-1)

| Frame | Mockup step | Mockup content (lines) | Codebase wiring |
|-------|-------------|------------------------|-----------------|
| 01 | 0 — Welcome | `.ob-welcome` (jsx:217) | NOT in wizard (mockup-only) |
| 02 | 1 — Games | `.ob-game-step.e-game` (jsx:252-…) | Replaced by `InterestsStep` (9 categories, NOT 8 games) |
| 03 | 2 — Agents | `.ob-agent-step.e-agent` (jsx:281-…) | SKIPPED in wizard (agents activated post-FirstGame) |
| 04 | 3 — Session | `.ob-session-step.e-session` (jsx:307-…) | Replaced by `FirstGameStep` (internal catalog) |
| 05 | 4 — Complete | `.ob-complete-step` (jsx:325-…) | Replaced by `InviteFriendComingSoonStep` (placeholder) |

## Component mapping (route ↔ canonical)

| Route | Real Client component | File |
|-------|-----------------------|------|
| `/welcome` | `WelcomePageContent` (Asse D P3 redirect → `/onboarding`) | `apps/web/src/app/(auth)/welcome/_content.tsx` |
| `/onboarding` | `OnboardingPage` (wraps `OnboardingGenericWizard`) | `apps/web/src/app/(authenticated)/onboarding/page.tsx` |
| `/setup` | `SetupPage` (redirect → `/setup-account`) | `apps/web/src/app/(authenticated)/setup/page.tsx` (likely) |
| `/setup-account` | `SetupAccountPageContent` | `apps/web/src/app/(auth)/setup-account/_content.tsx` |

## Canonical component pick

**Picked**: `apps/web/src/app/(authenticated)/onboarding/OnboardingGenericWizard.tsx`

**Why**:
1. Production component (Asse D P3, sub-issue #1899 follow-up).
2. Uses `WizardModal` primitive (asse-B) — proper drawer/stack semantics.
3. Reuses `InterestsStep` + `FirstGameStep` (existing) — no duplication.
4. The legacy `OnboardingTourClient` 5-step page-flow was DELETED in Asse D
   P3 PR. Mockup is the only remaining reference to the 5-step view.

## Mockup ↔ codebase divergences (significant)

| # | Divergence | Resolution |
|---|------------|------------|
| 1 | **5-step mockup vs 3-step wizard.** Mockup steps Welcome → Games → Agents → Session → Complete; wizard collapses to Interests → FirstGame → InviteFriend(coming-soon). | Frame matrix preserves mockup view for designer review. Renders use wizard. Divergence documented in story header. |
| 2 | **Games step uses 8-card grid; wizard `InterestsStep` uses 9 category chips.** | Wizard refactor intentional (#132 InterestsStep). Categories generalize over games for broader matching. |
| 3 | **Agents step is mockup-only.** | Wizard skips — agents activated per-game via FirstGame onboarding flow. Documented in OnboardingGenericWizard.tsx:35-37. |
| 4 | **Session step shows 3 action cards (Crea serata / Library / Chat).** | Wizard `FirstGameStep` searches internal catalog (NOT BGG, per ADR #1903) and adds 1 game. Distinct goal. |
| 5 | **Complete step has confetti animation.** | Wizard handleComplete → toast.success + router.replace('/library'). Confetti not implemented (acceptable simplification). |
| 6 | **Mockup `localStorage('mai-onboarding-step')` persists step across reloads.** | Wizard state is in-memory only (open prop). `api.auth.completeOnboarding(false)` server-side tracks completion. |

## BGG ToS compliance (P145, ADR #1903)

CRITICAL: User-side BGG access is BLOCKED. The wizard's `FirstGameStep`
already uses `api.games.getAll` (internal catalog) — see OnboardingGenericWizard.tsx:33.
Fixture `MOCK_ONBOARDING_CATALOG_DEFAULT` mirrors this contract: 8 internal
games (Catan, Carcassonne, Wingspan, etc.), NO BGG attribution markers, NO
external IDs.

## JSX evidence (line refs)

- `GAMES` array (8 entries): `onboarding.jsx:9-18`
- `AGENTS` array (4 entries, defaultOn flags): `onboarding.jsx:20-25`
- `ACTIONS` array (3 entries with entityClass): `onboarding.jsx:27-31`
- `STEP_LABELS` + entity accent per step: `onboarding.jsx:33-37`
- `MIN_SELECTED=3` gate: `onboarding.jsx:37`
- `step` useState + localStorage persistence: `onboarding.jsx:344-359`
- `OnboardingGenericWizard` step config (3 entries): `OnboardingGenericWizard.tsx:69-101`
- `interestsCompleted` / `firstGameCompleted` validate gates: `OnboardingGenericWizard.tsx:79, 91`
