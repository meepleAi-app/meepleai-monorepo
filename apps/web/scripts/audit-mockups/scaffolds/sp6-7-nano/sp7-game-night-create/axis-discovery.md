# Axis discovery — sp7-game-night-create

**Mockup**: `admin-mockups/design_files/sp7-game-night-create.html` + `.jsx`
**JSX twin lines**: 1642 total
**Route**: `/game-nights/new`
**Hero component**: `GameNightCreateWizard` (`apps/web/src/components/features/game-night-create/GameNightCreateWizard.tsx`)
**Page-client**: `apps/web/src/app/(authenticated)/game-nights/new/_content.tsx` (`NewGameNightContent`)
**Route entry**: `apps/web/src/app/(authenticated)/game-nights/new/page.tsx`

## Axis matrix

| Axis | Values | JSX evidence (line) |
|------|--------|---------------------|
| `step` | `1 | 2 | 3 | 4` | `STATES` array line 1476-1483 each entry has `step: N`; `<StepIndicator current={step}>` line 1162; `<Step1Quando>`/`<Step2Dove>`/`<Step3Chi>`/`<Step4Cosa>` mounted by step line 1164-1167 |
| `variant` | `null | 'warning' | 'empty' | 'typing' | 'filled' | 'decide-group'` | `STATES` line 1477 `variant: 'warning'`, line 1479 `'empty'`, line 1480 `'typing'`, line 1481 `'filled'`, line 1483 `'decide-group'`; pass-through `<Step1Quando withWarning={variant === 'warning'} />` line 1164; `<Step3Chi variant={variant}>` line 1166; `<Step4Cosa decideGroup={variant === 'decide-group'}>` line 1167 |

## Frame matrix (mockup → story)

Source: `STATES` array `const STATES = [...]` lines 1475-1484 (8 mobile states) + state-09 mobile step-flow overview line 1438-1470 + state-10 desktop split-form line 1362-1405. Total **10 frames**.

| Frame | Mockup state ID | step | variant | Story export | Story name |
|-------|------------------|------|---------|--------------|------------|
| 01 | `state-01-step1-date` | 1 | `null` | `Frame01_Step1Quando` | `01 · Step 1 — Quando? (default sab 17 mag · 21:00 · 3h)` |
| 02 | `state-02-step1-warning` | 1 | `'warning'` | `Frame02_Step1Warning` | `02 · Step 1 — Conflitto rilevato (warning amber)` |
| 03 | `state-03-step2-location` | 2 | `null` | `Frame03_Step2Location` | `03 · Step 2 — Dove? (Casa host + 4 opzioni + mappa)` |
| 04 | `state-04-step3-empty` | 3 | `'empty'` | `Frame04_Step3Empty` | `04 · Step 3 — Chi? (empty + suggested regulars)` |
| 05 | `state-05-step3-typing` | 3 | `'typing'` | `Frame05_Step3Typing` | `05 · Step 3 — Autocomplete dropdown attivo ("fede")` |
| 06 | `state-06-step3-filled` | 3 | `'filled'` | `Frame06_Step3Filled` | `06 · Step 3 — 6 invitati (5 reg + 1 email NEW)` |
| 07 | `state-07-step4-games` | 4 | `null` | `Frame07_Step4Games` | `07 · Step 4 — Cosa? (3 candidati selected)` |
| 08 | `state-08-step4-decide-group` | 4 | `'decide-group'` | `Frame08_Step4DecideGroup` | `08 · Step 4 — Lascia decidere al gruppo` |
| 09 | `state-09-mobile-step-flow` | overview | mobile | `Frame09_MobileStepFlow` | `09 · Mobile · Step-flow overview (1→4 affiancati)` |
| 10 | `state-10-desktop-split` | 1 | desktop | `Frame10_DesktopSplit` | `10 · Desktop · Step 1 + RSVP live preview (8-col / 4-col)` |

## Canonical pick — P245 multi-route consolidation

The mockup is multi-step but single-route (`/game-nights/new`). The 4 Step components (`Step1Quando` / `Step2Dove` / `Step3Chi` / `Step4Cosa`) all live inside the `GameNightCreateWizard` orchestrator's internal switch. We pick the **orchestrator** as story hero — argTypes axis `state.step` drives which sub-pane renders.

Sub-components (`GameNightDateTimePicker`, `GameNightLocationToggle`, `PlayerInviteAutocomplete`, `GameCandidatesPicker`, `RSVPCardLivePreview`) are real exported components in `components/features/game-night-create/index.ts` — they should get **dedicated stories alongside their source files** for designer review, not separate exports here.

## Phase scope (Phase C-1 vs Phase 4)

- **Desktop frames 01-08+10**: cover via argTypes `state` variants in this story.
- **Mobile frame 09 (step-flow overview)**: documentation-only frame; full mobile viewport sweep DEFERRED to **Phase 4** (per parent spec DEC-Pilot-3 Desktop primary).
- Mobile fidelity for individual states (01-08) renders fine via Storybook viewport addon when wired post-Phase 4.

## Sub-components needing dedicated stories (Stage 4 task)

Already shipped via DS-17-9 pattern — verify presence:
- `GameNightDateTimePicker.stories.tsx` (Step 1 calendar)
- `GameNightLocationToggle.stories.tsx` (Step 2 4-option radio)
- `PlayerInviteAutocomplete.stories.tsx` (Step 3 search + chips)
- `GameCandidatesPicker.stories.tsx` (Step 4 grid + compatibility)
- `RSVPCardLivePreview.stories.tsx` (live preview sticky right)

If not present, create scaffolds in the cluster-queue follow-up.
