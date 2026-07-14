# Libro-game standalone roster — domain decision (Issue #2759 / gap E4)

**Date**: 2026-07-14
**Issue**: [#2759](https://github.com/meepleAi-app/meepleai-monorepo/issues/2759) (design, area/backend) — deferred from #2750 verification pass (gap E4)
**Status**: **DECIDED** (design-complete). Implementation tracked separately (see Follow-up).
**Related**: #2750 (parent), #2619 (SP6 umbrella), [GameNight/Session domain model](./2026-06-04-gamenight-session-domain-model.md) (D4 reuse-Session, #10 max-1-live).

---

## Problem

The standalone "Inizia sessione" flow for a gamebook campaign **drops the player roster**. `CampaignSetupDrawer.tsx` Step 2 shows hardcoded preset chips + a disabled "Aggiungi giocatore" button; `createCampaign` sends only `{gameId, title}`; the backend creates a **player-less** `GamebookCampaignSession` and routes to `/library/[gameId]/play/[campaignId]`. The roster has nowhere to persist on the standalone path — **by design** (D4 reuse-Session: `GamebookCampaignSession` is deliberately player-less; the play-evening lifecycle lives on `Session`/`GameNight`).

## Decision

**Option A + no-live-mode** (user-confirmed 2026-07-14):

1. **A — standalone play spins up a `Session`** to carry the roster (User-linked participants + free guests), reusing the existing `Session` aggregate and the `AttachGamebookCampaignToGameNightCommandHandler` pattern (minus the GameNight steps). `Session` already carries `_participants` + a nullable `GamebookCampaignId` backlink.
2. **No live-mode on the standalone Session** — the created Session persists the roster but does **not** open live mode (`StartedAt` stays null, `IsLive=false`). Rationale: invariant **#10 max-1-live** is enforced only on the `GameNightEvent` aggregate; a live standalone Session with no GameNightEvent would bypass #10 and allow two concurrent live sittings for the same campaign. Keeping the standalone Session non-live means **#10 continues to govern GameNight play only** — no new liveness guard needed.

**Rejected — Option B** (add a participant collection to `GamebookCampaignSession`): domain mismatch, duplicates `Session.participants`, contradicts D4.

## Why A is feasible (evidence)

All the plumbing already exists — this is a wire-up, not new domain modeling:

- `Session.GamebookCampaignId` is a nullable FK (`Session.cs:65`); `Session._participants` supports User-linked **and** guest (`Participant.UserId` nullable).
- `CreateSessionCommand` already accepts `Participants`, `GamebookCampaignId`, `GuestNames`, `SkipGameNightEnvelope` — `CreateSessionCommandHandler` seeds owner + explicit participants + guest names in one atomic `SaveChanges`.
- `AttachGamebookCampaignToGameNightCommandHandler` is a near-complete reference (seed participants → `CreateSessionCommand` with `GamebookCampaignId` + `SkipGameNightEnvelope=true` inside a transaction). Option A = this handler **minus** the GameNight aggregate steps (no `EnsureCanStartSession`/`AddSession`/`StartCurrentSession`/`OpenSessionLiveMode`).
- Both `CreateGamebookCampaign` and `CreateSessionCommand` live in **SessionTracking** → in-context dispatch, no cross-bounded-context hop (simpler than the attach path).

## Implementation plan (for the follow-up issue)

**Backend**
1. Extend `CreateGamebookCampaignRequest` (`GamebookCampaignEndpoints.cs:365`) + `CreateGamebookCampaignCommand` with roster fields: `participants` (User-linked) + `guestNames`.
2. `CreateGamebookCampaignHandler`: after creating the campaign, **dispatch `CreateSessionCommand`** with `GamebookCampaignId = campaign.Id`, the roster participants + guest names, `SkipGameNightEnvelope = true`, and **without** opening live mode. Wrap campaign-create + Session-create in an **explicit transaction** (mirror `AttachGamebookCampaignToGameNightCommandHandler` lines 96-151) so a Session failure rolls back the campaign (today's handler does a bare `AddAsync`+`SaveChangesAsync`).
3. **KB-readiness gate**: `CreateSessionCommandHandler` throws `kb_not_ready` (422) if the game's KB is not Ready. Recommendation: **bypass the KB-readiness gate for standalone gamebook Session creation** — gamebook play does not depend on RAG KB readiness. Confirm during implementation (add a flag to `CreateSessionCommand` or a dedicated path).
4. **Update the doc-comment invariant** at `Session.cs:59-64` ("standalone play stays Session-less") — no longer true under this decision.

**Frontend**
5. `CampaignSetupDrawer.tsx` Step 2: replace the hardcoded preset chips + disabled "Aggiungi giocatore" with the **already-shipped `PlayerSetup` picker** (`game-night/PlayerSetup.tsx`, User-linked + guest tabs). If `PlayerSetup` is coupled to game-night-specific stores/hooks, wrap it for the standalone path rather than modifying the source.
6. Extend `CreateCampaignInput` (`gamebook-campaigns.ts`) with `participants`/`guestNames` and send the real roster.

**Guest identity model (MVP)**: seed the owner as a User-linked participant + everyone else as free guest names (the picker's guest tab). User-lookup for added players can come later.

## Non-interference checks (verified in discovery)
- `GetGamebookCampaignSpineQueryHandler` filters to **GameNight-attached** sittings, so a standalone (night-less) Session will **not** surface a phantom spine strip — the standalone play page stays spine-less as today.
- Per-book reading progress lives in `SessionBookProgress`, not on `Session` — the roster (Session) and progress (SessionBookProgress) stay separate carriers.

## Explicitly NOT to do
- Do not add a `participants` field to `CreateGamebookCampaignCommand`'s aggregate side / `GamebookCampaignSession` (Option B — domain mismatch).
- Do not ship an editable-but-non-persisted roster whose Step-3 copy implies persistence (misleading UX).
- Do not open live mode on the standalone Session (would bypass #10).

## Follow-up
This issue (#2759) is **design-complete** with this document. A separate **implementation issue** covers the BE + FE wire-up above.
