# #2632 (SI-1) — GamebookCampaignSession ↔ GameNight link: architecture design

**Issue**: [#2632](https://github.com/meepleAi-app/meepleai-monorepo/issues/2632) (SI-1) · part of [#2619](https://github.com/meepleAi-app/meepleai-monorepo/issues/2619)
**Date**: 2026-07-01
**Method**: `/sc:spec-panel` (Fowler · Newman · Nygard) — architecture focus
**Status**: RATIFIED 2026-07-01 — D-LINK (Option A), D-WHEN, D-SHARED, D-PUBLISHED all ratified. SI-1b implementation follows §7.
**Depends on**: D4 (reuse-Session), ratified in [`2026-07-01-issue-2619-decomposition-design.md`](./2026-07-01-issue-2619-decomposition-design.md).

---

## 1. Why this spec exists

The #2619 decomposition assumed SI-1 ("render the GameNight spine in libro-game screens") was **FE-only, domain already shipped**. Discovery invalidated that premise:

> `GamebookCampaignSession` has **no link** to a GameNight or a Session — neither in the backend aggregate nor in the FE. The libro-game play flow (`apps/web/src/app/(authenticated)/library/[gameId]/play/[campaignId]`) is a standalone, campaign-centric surface that never enters the GameNight machinery.

So the spine cannot be "rendered" until the **relationship** exists. This spec locks how a libro-game campaign relates to the GameNight/Session model (the architectural decision the product owner asked to settle before implementation), so SI-1b implements against a ratified link instead of a guessed one.

---

## 2. Domain map (what is actually shipped)

Three session-shaped aggregates exist. They are **not** interchangeable:

| Aggregate | BC | Role | Key fields |
|---|---|---|---|
| `GameNightEvent` | GameManagement | the social evening | `OrganizerId`, `GameIds[]`, RSVPs, `_sessions: List<GameNightSession>` |
| `GameNightSession` | GameManagement (child of `GameNightEvent`) | a **sitting** within the evening — the #10/#15/#8 lifecycle carrier | `GameNightEventId`, **`SessionId`**, `GameId`, `PlayOrder`, `Status` (Pending/InProgress/Completed/Skipped/Corrupted), `StartedAt`, `CompletedAt` |
| `Session` | SessionTracking | the **rich play tracker** (scores, cards, dice, notes, checkpoints, chat) + live lifecycle | `UserId`, `GameId`, participants, raises `SessionStartedDomainEvent` → GameNight #15 transition |
| `GamebookCampaignSession` | SessionTracking | the **persistent libro-game campaign** (spans many evenings) | `GameRef`, `OwnerUserId`, `Title`, progress via `SessionBookProgress`; **only** `SoftDelete` |

**The spine already wires itself** — `GameNightEvent` → `GameNightSession.SessionId` → `Session`. A `Session` starting raises `SessionStartedDomainEvent`, which `SessionStartedHandler` handles to promote the GameNight `Published → InProgress` (#15). Max-1-live (#10) is enforced *synchronously in the aggregate* by `GameNightEvent.StartCurrentSession()` (`GameNightEvent.cs:437-442`, throws `MaxLiveSessionsExceededException` if any `GameNightSession.Status == InProgress`) — **not** by the domain event. `Session.GameId` names the played game and carries a **hard FK to `shared_games`** (`SessionConfiguration.cs:155-159`, `Restrict`, required).

```
GameNightEvent ──1:N──> GameNightSession ──SessionId──> Session ──GameId──> (a game)
                              │                            │
                        status/#15/#10               raises SessionStarted
```

**The one missing edge** is `Session ⇢ GamebookCampaignSession`. A gamebook campaign is played across many evenings; each evening's play should be a `Session`. Today no such edge exists, and the libro-game flow does not create `Session`s at all.

---

## 3. The gap, precisely

1. **No link field.** `Session` has no `GamebookCampaignId`; `GamebookCampaignSession` has no `SessionId`/`GameNightId`.
2. **No Session is created for libro-game play.** `library/[gameId]/play/[campaignId]` mutates `GamebookCampaignSession` + `SessionBookProgress` directly. It never constructs a `SessionTracking.Session`, so it is wholly outside the GameNight spine.
3. **Private-game campaigns cannot become a `Session` as-is.** `GamebookCampaignSession.GameRef` may be `Kind == Private` (a row in `PrivateGameEntity`, a different table), but `Session.GameId` FK-references `shared_games` only. Creating a `Session` from a private-game campaign would violate the FK. → resolved by the **Shared-only invariant** in §5/§7 (GameNights already only reference shared games, so this is inherent, not a new limit).

Rendering the spine (#1/#15/#8/#10) therefore requires: (a) a persistent link, (b) a way for a libro-game play to *become* a Session under a GameNight, and (c) that the campaign's game is a **shared** game.

---

## 4. Options for the link (D-LINK)

Evaluated against the shipped structure. The campaign is **long-lived (1:N over sittings)**; a sitting is **short-lived (1 evening)**.

### Option A — `Session.GamebookCampaignId` (nullable FK on Session) — RECOMMENDED
A libro-game play sitting **is a `Session`** whose `GamebookCampaignId` points at the persistent campaign it advances. Reuses the existing `GameNightSession.SessionId → Session` machinery verbatim; the spine is reached by `campaign ← Session.GamebookCampaignId ← GameNightSession.SessionId ← GameNightEvent`.

- **Fowler**: the edge sits on the *short-lived* side pointing to the *stable* side (classic child→parent FK). The campaign never has to know which sittings exist; it stays a clean aggregate root.
- **Newman**: no new cross-BC coupling shape — `Session` already lives in SessionTracking alongside `GamebookCampaignSession`; the FK is intra-BC.
- **D4 fit**: exactly "the play evening is a Session; campaign status is *derived* from its Sessions." Migration is a **link column on `Session`**, not the timestamp-trio-on-campaign that D4 forbade. ✅
- **Cost / caveats** (surfaced by adversarial review, all accepted):
  - *Shared-only*: `Session.GameId` FK-restricts to `shared_games`; gamebook-GameNight play is therefore limited to `GameRef.Kind == Shared` campaigns. This is **inherent** — GameNights already only reference shared games (`GameNightEvent.GameIds`, `GameNightSession.GameId` all FK `shared_games`) — not a new limitation. Private-game campaigns stay standalone (Session-less, no spine).
  - *Inert Session fields (accepted tech debt)*: `Session.Create()` seeds a mandatory `"Owner"` participant + `ScoringType=Points` + empty `ScoreData` + turn/invite scaffolding (`Session.cs:238-242`). For a (typically solo) gamebook reading these are semantically empty. We accept them as inert for SI-1b rather than fork a `GamebookSession` type; revisit if gamebook Sessions accrete their own needs. DTOs/endpoints that read Sessions will surface these empty fields for gamebook Sessions.
  - libro-game play must start creating `Session`s in the GameNight path (see §6).

### Option B — link on the campaign (`GamebookCampaignSession.CurrentSessionId` or a Session list)
The campaign points at its live/most-recent Session.
- **Rejected**: a "current pointer" is transient state on a persistent aggregate (needs constant rewrite as sittings open/close); a Session *list* on the campaign duplicates the `GameNightSession.SessionId` edge and inverts the natural child→parent direction. Nygard: more mutable state on the long-lived aggregate = more concurrency surface for no gain.

### Option C — dedicated join entity `CampaignPlaySession(campaignId, sessionId)`
- **Rejected**: it is a plain many-to-one (each Session advances ≤1 campaign); a join table models many-to-many. YAGNI. It also fragments the read path (extra hop) with zero flexibility benefit.

**Decision (D-LINK, proposed): Option A** — `Session.GamebookCampaignId Guid?` nullable.

---

## 5. Consequences

- **Migration**: add nullable `Session.GamebookCampaignId uuid NULL` + a filtered index `WHERE gamebook_campaign_id IS NOT NULL`. Additive, backward-compatible (existing non-gamebook Sessions keep it null). No change to `GamebookCampaignSession` schema (honours D4).
- **Derived campaign status** (D4): a campaign is *in-progress* if it has a live Session (`GamebookCampaignId = c AND StartedAt != null AND FinalizedAt == null`), *resumable* otherwise, *completed* via the manual `Complete` flag (SI-8). No new timestamps on the campaign.
- **Spine read path**: given a campaign, its GameNight-attached sittings = `GameNightSession` rows whose `SessionId` ∈ {Sessions with this `GamebookCampaignId`}. The owning `GameNightEvent` supplies the "Serata" strip (title, organizer, status, session pip).
- **#15 promotion and #10 guard are reused, not free.** A gamebook Session is a `Session`, so once it is attached via `AddSession()` and started via `GameNightEvent.StartCurrentSession()`, #15 (`SessionStartedHandler`) and #10 (the `StartCurrentSession` InProgress check) fire unchanged. But the SI-1b command **must replicate the exact `AddSession()` → `StartCurrentSession()` call sequence** of `StartGameNightSessionCommandHandler` (`:69-70`) — #10 lives inside `StartCurrentSession`, not in the domain event, so calling only one of the two silently bypasses the guard.
- **Attach requires `Published`**: `GameNightEvent.AddSession()` throws unless `Status == Published` (`GameNightEvent.cs:413-415`). Mid-night attachment (GameNight already `InProgress`) is **out of scope for SI-1b** — gamebook attachment happens on a `Published` (pre-start) GameNight. Relaxing `AddSession` to allow mid-night attach is a separate decision, not taken here.

---

## 6. Open sub-decision (D-WHEN): when does a libro-game play get a Session + GameNight?

The FK alone is inert — something must *create* the Session. Gamebooks are frequently played **solo at home**, not only at game nights. Proposed split:

- **Solo / standalone play** stays campaign-only (progress via `SessionBookProgress`); **no** Session, **no** spine. Unchanged behaviour.
- **GameNight play**: when a campaign is played as part of a GameNight, a `Session` (with `GamebookCampaignId` set) is created and attached via a `GameNightSession`. Only then does the spine render.

This keeps SI-1b additive and non-regressive: the standalone flow is untouched; the spine is a *GameNight-context* feature. It does mean SI-1b must introduce the entry point "play this campaign as part of a GameNight" (a command that creates the Session + GameNightSession). That entry point is the real weight of SI-1b and should be called out in its acceptance criteria.

**Split progress source (accepted, bounded).** A dual-mode campaign records reading progress in two places: `SessionBookProgress` (standalone) and, for GameNight-attached play, potentially against the `Session`. For SI-1b we keep **`SessionBookProgress` as the single source of truth for reading position** (the gamebook Session does *not* own paragraph progress — it is only the spine attachment + lifecycle carrier). The Session contributes liveness/lifecycle to the spine; it does not fork the "where am I in the book" state. This avoids the split-source problem the review flagged: `useCampaignProgress` stays authoritative for position; a *new* read path derives spine presence from Sessions. If a future feature needs Session-scoped reading history, that is a separate decision.

**D-WHEN (proposed)**: standalone stays Session-less; GameNight-context play creates the Session; `SessionBookProgress` remains the single progress source. Ratify alongside D-LINK.

---

## 7. SI-1b implementation scope (post-ratification)

1. **BE**: `Session.GamebookCampaignId` field + factory param (nullable) + EF config + migration + filtered index (`WHERE gamebook_campaign_id IS NOT NULL`). No change to `GamebookCampaignSession` schema (honours D4).
2. **BE**: command to attach+start a gamebook Session within a **`Published`** GameNight — **guard `campaign.GameRef.Kind == Shared`** (else reject: private-game campaigns are standalone-only), create `Session` with `GamebookCampaignId = campaign.Id` and `GameId = campaign.GameRef.Id`, then replicate the **`GameNightEvent.AddSession()` → `StartCurrentSession()`** sequence (`StartGameNightSessionCommandHandler:69-70`) so #15 promotion + #10 max-1-live fire through the *existing* guard. Calling only one of the two silently bypasses #10.
3. **BE**: derived campaign-status query (in-progress / resumable / completed) from the campaign's Sessions + the manual `Complete` flag (SI-8). `SessionBookProgress` stays the single reading-position source (§6).
4. **FE**: the "Serata" spine strip in the libro-game play surface, rendered **only when** the campaign's play is GameNight-attached (a new read path derives spine presence from Sessions; `useCampaignProgress` stays authoritative for position); per-session UIs nested under it. (This is the original SI-1 rendering, now unblocked.)
5. Tests: unit (link + Shared-only guard + derived status + #15 fires + #10 blocks 2nd live), integration (Testcontainers: attach on Published → spine read path; private-game rejected), FE (spine renders/absent by attachment).

SI-2 (#2633 max-1-live badge) and SI-3 (#2634 close strip) then layer on this shell as originally sequenced.

---

## 8. Decisions to ratify

- **D-LINK** = Option A (`Session.GamebookCampaignId` nullable FK on the short-lived Session). ✅ survived adversarial review as the correct shape.
- **D-WHEN** = standalone play stays Session-less; GameNight-context play creates the Session; `SessionBookProgress` remains the single reading-position source.
- **D-SHARED** (added post-review) = gamebook-GameNight play is limited to `GameRef.Kind == Shared` campaigns (inherent to the shared-games-only GameNight model). Private-game campaigns are standalone-only.
- **D-PUBLISHED** (added post-review) = gamebook attachment happens on a `Published` (pre-start) GameNight; mid-night attach is out of scope for SI-1b.

Adversarial review (architecture) confirmed the domain map and Option A's shape; the four decisions above absorb its five findings (private-game FK, AddSession Published-only, #10-not-free call sequence, inert Session fields as accepted tech debt, single progress source). On ratification, update #2632's acceptance criteria to the §7 scope and implement SI-1b.
