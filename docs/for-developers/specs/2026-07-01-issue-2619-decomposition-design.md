# #2619 — Decomposition & Reconciliation Design

**Issue**: [#2619](https://github.com/meepleAi-app/meepleai-monorepo/issues/2619) — `design(libro-game): align SP6 UI to GameNight/GameBook domain model — 6 HIGH gaps`
**Date**: 2026-07-01
**Method**: `/sc:spec-panel` critique (Fowler · Cockburn · Wiegers · Nygard · Adzic)
**Inputs**: [gap report SP6](../audits/2026-06-30-claude-design-gap-report-sp6.md) (49 gaps) · [domain model spec 2026-06-04](./2026-06-04-gamenight-session-domain-model.md) · [integration plan 2026-07-01](../audits/2026-07-01-sp6-librogame-integration-plan.md)
**Status**: RATIFIED 2026-07-01 — D1–D3 ratified (demo/domain consensus), D4 resolved = **reuse-Session**. SI-1…SI-8 filed as sub-issues of #2619.

---

## 1. Executive summary

#2619 is an **umbrella of UI↔domain alignment gaps**, not a single implementable ticket. The Claude Design SP6 demo (#1888) surfaced 49 gaps (6 HIGH), all rooted in one theme: the SP6 libro-game mockups embed **per-session** UIs without rendering the owning **GameNight/GameBook** aggregates that are **already shipped on the backend**.

Two findings reshape the work before any code is written:

1. **Most "undefined" semantics are already resolved.** The domain model spec (2026-06-04) authoritatively defines the very things #2619 lists as open (`startedAt` derivation, `in-progress→completed`, the 3 Session timestamps, max-1-live). For those, the residual work is **FE rendering only** — not domain design.
2. **The umbrella spans two different aggregates.** The GameNight/Session spine (Thread A) is shipped domain + missing FE. The gamebook-campaign close (Thread B) touches a *different* aggregate (`GamebookCampaignSession`) that genuinely lacks a lifecycle. Conflating them is the reason the umbrella looks larger and vaguer than it is.

**Net effect**: after reconciliation, #2619 decomposes into **8 sub-issues** across two threads, gated by **4 decisions** (3 already answered by the demo/domain spec → ratify; 1 genuinely open architectural call → D4).

---

## 2. Reconciliation vs the shipped domain (Wiegers)

> "A requirement that names a gap already closed elsewhere isn't a requirement — it's a traceability defect. Re-anchor each gap to its authoritative source before estimating."

| #2619 gap | #2619 framing | Authoritative resolution (already shipped) | Residual work |
|---|---|---|---|
| **#14** "Ora di inizio" derived | "semantics undefined" | Domain spec **Invariante 5** (`startedAt` = "Apri Live mode" timestamp, **never user input**; time/duration fields *removed* from the draft editor) | FE: read-only `▶ Ora di inizio {startedAt} · derivata` chip; delete any time-picker |
| **#11** 3 Session timestamps | "startedAt semantics undefined" | Domain spec **Invariante #11** (`createdAt` always · `startedAt` nullable · `completedAt` nullable) + BE mapping (`Session.Create`/`OpenLiveMode`/`Finalize`) | FE: render the 3 states; no domain change |
| **#8** in-progress→completed | "transition not shown" | Domain spec §status (`in-progress → completed`: manual "Termina serata" **or** last Session saved; no backward transition) | FE: close strip renders the promotion; BE already transitions |
| **#10** max-1-live | "guard missing" | BE guard **shipped** (`GameNightEvent.StartCurrentSession()` → `MaxLiveSessionsExceededException` → HTTP 409) | FE: LIVE badge + blocked modal surfacing the 409 |
| **#1/#15** GameNight unrendered | "the spine" | `GameNightEvent` aggregate shipped (1→N Session, planned→in-progress on first live) | **FE only**: draw the owning "Serata" strip |
| resume (#11/#14) | "new-Session vs draft reactivation undefined" | Demo **inferred** new live Session (fresh `startedAt`, campaign `createdAt` unchanged, GameNight re-promoted); consistent with Invariante 5 | Ratify (D3) → FE wiring |

**Conclusion**: 5 of 6 HIGH gaps are **FE-rendering gaps against a shipped, specced domain**. Only the gamebook-campaign close (Thread B) is net-new.

---

## 3. Aggregate separation (Fowler)

> "Two lifecycles are being drawn on one canvas. Name them, or every estimate will be wrong."

- **Thread A — GameNight / Session spine.** `GameNightEvent` (GameManagement) owns 1→N `Session` (SessionTracking). Fully shipped + specced. The libro-game screens must *render* this: the owning Serata, the planned→in-progress→completed lifecycle, the LIVE badge, the derived `startedAt`. **No domain work.**
- **Thread B — Gamebook campaign.** `GamebookCampaignSession` (SessionTracking) is the *persistent reading campaign* — progress accumulated across many evenings. It has **only** `SoftDelete` today (no `Status`/`StartedAt`/`CompletedAt`, confirmed while shipping quick-win #3). The demo's "session-end 3-way" (done/archive/abandon) implies a lifecycle this aggregate does not have.

**The core open question (D4)**: when a user *plays* a libro-game during an evening, is that play a **`Session` under a GameNight** (reuse Thread A's lifecycle), or does **`GamebookCampaignSession` grow its own close state machine**? This decides whether SI-8 exists at all.

---

## 4. Decisions

| ID | Decision | Status | Proposed resolution | Rationale |
|---|---|---|---|---|
| **D1** | Canonical libro-game identity | **Ratify** | **Eldoria** (Side Room / agent "Arbitro Eldoria") | Demo already unified every screen under Eldoria (gap report Turn 3/5 "unified under Eldoria", "re-anchored to Eldoria"). Kills ~6 ENTITY drift gaps. |
| **D2** | Close-outcome model | **Ratify** | **3-way selector** (Completa / Archivia / Abbandona); *defeat* is a dock-only branch, not a 4th top-level state | Demo reconciled the mockup's 4 parallel states vs .jsx 3-option dialog to a 3-way selector. `Completa`+`Abbandona` terminal; `Archivia` resumable. |
| **D3** | Resume semantics | **Ratify** | "▶ Riprendi" opens a **new live Session** (fresh `startedAt`), campaign `createdAt` unchanged, GameNight re-promoted planned/completed→in-progress | Demo inference is consistent with Invariante 5 (`startedAt` derived from live-open). No draft reactivation. |
| **D4** | Gamebook play ↔ aggregate | **RATIFIED = reuse-Session** | A libro-game play evening **is a `Session`** under a GameNight; `GamebookCampaignSession` gains only a lightweight resumable/completed **derived** status (not a parallel timestamp trio) + a manual `Complete` flag | Fowler: don't duplicate the Session lifecycle. The campaign is the "series"; each evening is a Session. SI-8 is therefore effort **M** (derive campaign status from its Sessions + a manual Complete flag), **no timestamp-trio migration**. |

D4 was the only decision that changed scope materially; ratified **reuse-Session** on 2026-07-01. SI-8 below reflects the ratified variant.

---

## 5. Sub-issue decomposition

> Acceptance criteria in Given/When/Then (Adzic). Effort is FE unless noted. Priority follows the issue's "#1/#15 first" directive.

### Thread A — GameNight/Session spine (FE rendering, domain shipped)

**SI-1 · Render the GameNight spine in libro-game screens** — gaps #1/#15 · `HIGH` · effort **L** · deps none
> Given a libro-game play/setup/storyboard screen for a GameNight with N Sessions,
> When it renders,
> Then it draws the owning "Serata" strip (title, host, `planned/in-progress/completed` badge, session pip `0→1→…`), and the per-session UIs are visibly nested under it.
Umbrella-within-umbrella: SI-2/SI-3/SI-4 hang off this shell.

**SI-2 · max-1-live badge + blocked state** — gap #10 · `HIGH` · effort **M** · deps SI-1
> Given a GameNight already has one live Session,
> When the user tries to open a 2nd live Session,
> Then the UI shows the LIVE badge on the running one and a blocked modal surfacing the BE `409 MaxLiveSessionsExceeded` ("puoi averne solo una"), without a client-side race.

**SI-3 · in-progress→completed close strip** — gap #8 · `HIGH` · effort **M** · deps SI-1, D2
> Given the last live Session of a GameNight is closed via the 3-way selector,
> When "Completa" (or last-save) fires,
> Then the Serata strip moves in-progress→completed (no backward transition; Completa/Abbandona terminal, Archivia resumable), reflecting the shipped BE transition.

**SI-4 · derived startedAt chip + resume wiring** — gaps #11/#14 · `MED` · effort **S–M** · deps SI-1, D3
> Given a live Session,
> When the play UI renders,
> Then it shows a read-only `▶ Ora di inizio {startedAt} · derivata` chip (no time-picker, no duration input — Invariante 5); and "▶ Riprendi" opens a new live Session with a fresh `startedAt`.

### Thread B — Gamebook-campaign specifics

**SI-5 · Lock canonical identity (Eldoria)** — cross-mockup drift · `HIGH` · effort **S** (doc/fixture) · deps D1
> Given every SP6 libro-game mockup/fixture,
> When identity is audited,
> Then all reference the single canonical Eldoria identity (game, Side Room, agent "Arbitro Eldoria"), retiring Tainted Grail/Nanolith/Runa di Ardenel drift. Closes ~6 ENTITY gaps.

**SI-6 · GameBook 1..N book-manager in real UIs** — GameBook 2-PDF vs 1..N · `HIGH` · effort **L** · deps none
> Given onboarding/detail/glossary,
> When a game's books are shown,
> Then they use the 1..N book-manager (demo FIX-2) — not hardcoded "Press Start + Rules" — reflecting the shipped `GameBook` aggregate (community `OwnerUserId=null` + personal). Removes all "Press Start"/"24 pagine" copy.

**SI-7 · Glossary `contexts[]` multi-context** — glossary schema · `MED` · effort **M (BE+FE)** · deps none
> Given a glossary entry appearing in multiple books/paragraphs,
> When it is stored,
> Then `GlossaryEntry.contexts[]` persists all contexts (BE migration from single-context), and the glossary editor renders them. (DELETE verb already shipped in quick-win #2.)

**SI-8 · Gamebook-campaign close (reuse-Session, D4)** — session-end 3-way · `MED` · effort **M** · deps SI-3, D2
> Given a libro-game play evening ends,
> When the user picks Completa / Archivia / Abbandona in the 3-way selector,
> Then the outcome is `Session.Finalize()` + the GameNight transition (Thread A, SI-3), and the campaign status is **derived** from its Sessions plus a manual `Complete` flag (Completa/Abbandona terminal, Archivia resumable).
> No new `Status`/`StartedAt`/`CompletedAt` columns on `GamebookCampaignSession`, **no migration** — the lifecycle lives in the Session/GameNight aggregates per D4.

---

## 6. Sequencing (Cockburn · Nygard)

> Cockburn: "Deliver the spine first — it's the shell every other story mounts into." Nygard: "Land the failure-mode story (max-1-live) early; a blocked-state that only exists in a mockup is a production incident waiting to happen."

1. **SI-5** (identity lock) — smallest, unblocks clean fixtures for everything else. Ship first.
2. **SI-1** (spine) — the shell for SI-2/3/4.
3. **SI-2** (max-1-live) — surfaces the already-shipped 409 guard.
4. **SI-3** (close strip) + **SI-4** (startedAt/resume) — complete the lifecycle rendering.
5. **SI-6** (GameBook 1..N) — parallelizable with Thread A (independent surface).
6. **SI-7** (glossary contexts) — parallelizable BE slice.
7. **SI-8** (campaign close) — **last**; blocked on D4. Do not start until D4 is ratified.

Threads A and B are independent → SI-6/SI-7 can run in parallel with SI-1..SI-4.

---

## 7. Non-goals / out of scope

- No new Claude Design mockups are produced here; SI-1..SI-8 implement against existing mockups + the shipped domain.
- The ~43 MEDIUM/LOW gaps (16 dead-end CTAs, KB-count conflicts, agent-name drift, quota/error-count conflicts) are **absorbed** by the sub-issues above (identity lock + spine + 1..N adoption resolve the bulk) — they are not separately filed unless a residue survives SI-1..SI-8.
- TOKEN gaps: 0 (design system held; no token work).

## 8. Traceability

| Gap cluster (of 49) | Covered by |
|---|---|
| #1/#15 GameNight spine | SI-1 |
| #10 max-1-live | SI-2 |
| #8 completed transition | SI-3 |
| #11/#14 startedAt/resume | SI-4 (domain already resolved) |
| cross-mockup identity (~6 ENTITY) | SI-5 |
| GameBook 1..N (+ "Press Start" copy) | SI-6 |
| glossary contexts[] | SI-7 |
| session-end 3-way close | SI-8 (reuse-Session, D4) |
| 16 CTA + KB/quota/agent-name drift | absorbed by SI-1/SI-5/SI-6 |

---

## 9. Next action

D1–D3 ratified (demo/domain consensus); D4 resolved = **reuse-Session** (2026-07-01). **SI-1 … SI-8** filed as sub-issues of #2619 with the acceptance criteria above. Implement per §6 sequencing: SI-5 → SI-1 → SI-2 → SI-3/SI-4, with SI-6/SI-7 in parallel and SI-8 last.
