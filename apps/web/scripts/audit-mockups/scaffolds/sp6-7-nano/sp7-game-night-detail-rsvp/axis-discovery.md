# Axis discovery — sp7-game-night-detail-rsvp

**Mockup**: `admin-mockups/design_files/sp7-game-night-detail-rsvp.html` + `.jsx`
**JSX twin lines**: 1506 total
**Route**: `/game-nights/[id]`
**Hero component**: `GameNightDetailView` (`apps/web/src/app/(authenticated)/game-nights/[id]/_components/GameNightDetailView.tsx`)
**Route entry**: `apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx`

## Axis matrix

| Axis | Values | JSX evidence (line) |
|------|--------|---------------------|
| `viewer` | `'host' | 'invitee'` | `STATES` line 1348 `viewer:'host'`, line 1352 `viewer:'invitee'`; pass-through `<MobileScreen {...s}/>` line 1456 |
| `status` | `'pending' | 'ready' | 'cancelled' | 'inprogress' | 'completed'` | `STATES` line 1348 `status:'pending'`, line 1349 `'ready'`, line 1360 `'cancelled'`, line 1361 `'inprogress'`, line 1363 `'completed'` |
| `tab` | `'detail' | 'voting' | 'chat'` | `STATES` line 1348 `tab:'detail'`, line 1356 `tab:'voting'`, line 1366 `tab:'chat'` |
| `rsvpKey` | `'host-pending' | 'host-ready' | 'invitee-pending' | 'invitee-confirmed'` | `STATES` line 1348 `rsvpKey:'host-pending'`, etc. |
| `viewerRSVP` | optional `'yes'` | `STATES` line 1354 `viewerRSVP:'yes'` (frame 04 post-tap celebration) |
| `banner` | optional `'host-ready' | 'cancelled' | 'inprogress' | 'completed'` | `STATES` line 1350 `banner:'host-ready'`, etc. |
| `voting` | optional `'tied'` | `STATES` line 1358 `voting:'tied'` (frame 06 tie-breaker) |
| `sticky` | optional `'rsvp-pending' | 'rsvp-confirmed'` | `STATES` line 1352 `sticky:'rsvp-pending'` (mobile bottom-sheet CTA) |

## Frame matrix (mockup → story)

Source: `STATES` array `const STATES = [...]` lines 1346-1367 (10 mobile states) + `<DesktopShell id="state-11..."/>` line 1469-1480 (2 desktop frames). Total **12 frames**.

| Frame | Mockup state ID | viewer | status | tab | Story export | Story name |
|-------|------------------|--------|--------|-----|--------------|------------|
| 01 | `state-01-host-view-pending` | host | pending | detail | `Frame01_HostPending` | `01 · Host · 3/8 confermati · voting attivo` |
| 02 | `state-02-host-view-ready` | host | ready | detail | `Frame02_HostReady` | `02 · Host · 8/8 confermati · "Avvia sessione live"` |
| 03 | `state-03-invitee-pending` | invitee | pending | detail | `Frame03_InviteePending` | `03 · Davide invitee · pending · RSVP CTA bottom-sheet` |
| 04 | `state-04-invitee-confirmed` | invitee | pending (post-tap) | detail | `Frame04_InviteeConfirmed` | `04 · Davide post-tap "Ci sarò" · celebration micro` |
| 05 | `state-05-voting-active` | host | pending | voting | `Frame05_VotingActive` | `05 · Tab Voting · 7 voti · Twilight in testa` |
| 06 | `state-06-voting-tied` | host | pending | voting | `Frame06_VotingTied` | `06 · Tab Voting · pareggio 3-3 · host scegli (-1h)` |
| 07 | `state-07-cancelled` | host | cancelled | detail | `Frame07_Cancelled` | `07 · Cancellata · banner danger + crea nuova` |
| 08 | `state-08-in-progress` | host | inprogress | detail | `Frame08_InProgress` | `08 · In corso · session indigo pulsing · "Apri sessione"` |
| 09 | `state-09-completed` | host | completed | detail | `Frame09_Completed` | `09 · Completata · "Registra play record" success` |
| 10 | `state-10-mobile-tab-chat` | host | pending | chat | `Frame10_MobileTabChat` | `10 · Mobile · Tab Chat fullscreen + input sticky` |
| 11 | `state-11-desktop-split-detail` | host | pending | detail | `Frame11_DesktopSplitDetail` | `11 · Desktop · Split-view sidebar 380 + Tab Dettagli` |
| 12 | `state-12-desktop-split-voting` | host | pending | voting | `Frame12_DesktopSplitVoting` | `12 · Desktop · Split-view sidebar 380 + Tab Voting` |

## Canonical pick — P245 multi-route consolidation (CRITICAL)

The mockup covers a **single Next.js route** (`/game-nights/[id]`) with **many internal state variants** driven by `useGameNightDetail` (event status + viewer role + RSVP transitions). Per P245:

1. Hero = `GameNightDetailView` (page-client at `apps/web/src/app/(authenticated)/game-nights/[id]/_components/GameNightDetailView.tsx`) — accepts only `id: string` prop, all variants emerge from MSW handlers.
2. **NO separate stories per RSVP state variant** — cover via argTypes `state` axis values mapped to MSW handlers.
3. Internal branching in `GameNightDetailView` already encodes:
   - Draft → legacy `GameNightPlanningLayout` (host-only flow, out of spec-hardening scope per checkpoint decision 1a — Frame 01 may not match exactly until Published flow is wired).
   - Cancelled → v2 Hero + `GameNightCancelledBanner` (Frame 07 ✓).
   - Published / Completed → v2 Hero + (RsvpActionBar for guests Frames 03, 04) + roster `GameNightRsvpRow` + legacy actions (Frames 01, 02, 08, 09).
4. Tabs (Voting / Chat — Frames 05, 06, 10, 12) are NOT yet rendered by current page-client; frames flagged as documentation-only (post-MVP).

## Phase scope (Phase C-1 vs Phase 4)

- **Desktop frames 11, 12**: render canonical via Storybook.
- **Mobile frames 01-10**: rendered Desktop in Phase C-1; viewport sweep DEFERRED to Phase 4.
- **Voting + Chat tabs (Frames 05, 06, 10, 12)**: documentation-only — real components post-MVP per decision 2a (`GameVoteCard`, `VotingTiedResolver`, `GameNightChatStream`).

## Sub-components needing dedicated stories (Stage 4 task)

Already shipped via DS-17-9 pattern — verify presence:
- `GameNightDetailHero.stories.tsx`
- `GameNightStatusBadge.stories.tsx`
- `GameNightRsvpRow.stories.tsx`
- `GameNightRsvpActionBar.stories.tsx`
- `GameNightCancelledBanner.stories.tsx`
- `PublicRsvpForm.stories.tsx`

If not present, queue cluster-queue follow-up.
