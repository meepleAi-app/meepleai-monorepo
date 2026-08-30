# Happy Path Testing Program — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce 13 happy-path scenario catalogs (~220 scenarios) covering every route of the app, then execute them in a real browser locally and (on green) on staging, recording results.

**Architecture:** Two phases. **Phase A (produce)** writes Given/When/Then catalogs — parallelizable, one subagent per macro-area, output is markdown docs. **Phase B (execute)** runs each scenario in a real browser via MCP, local→staging with per-area gates, output is `RESULTS.md` + GitHub issues on failures. Phase B is inline (single local environment, not parallelizable).

**Tech Stack:** Docs = Markdown (Gherkin-style scenarios). Execution env = `make dev` (Docker full stack) + `make seed-sp4`. Browser = Playwright MCP / claude-in-chrome. App: Next.js web `:3000`, .NET API `:8080`.

**Spec:** `docs/superpowers/specs/2026-07-10-happy-path-testing-program-design.md`

## Global Constraints

- **Happy path only** — no negative/error/edge scenarios in this program.
- **Scenario format** — Given/When/Then per the template in spec §5; stable ID `<AreaID>-NN`; declare `Osservabile ✅`, `Route`, `Utente`.
- **Two levels** — `Flow` (transactional multi-step) vs `Smoke` (read-only view: loads without unexpected 4xx/5xx or JS errors + skeleton→real content + primary action produces visible effect).
- **Pass criterion** — pass only if ALL observables true, no unexpected Console/Network errors; partial = fail; env-blocked = `blocked-env` (⚠️), distinct from fail.
- **Coverage matrix** — every catalog opens with a table mapping each area route to ≥1 scenario, or marks it `smoke-aggregato` / `skip: non-user-facing` with reason. No silent gaps.
- **Data markers** — every Flow-created entity uses `HP-TEST-<data>` in its title/name for repeatability and cleanup.
- **No fixes** — this program verifies & maps; failures → GitHub issues, fixing is separate work.
- **Env** — local `make dev` (full, AI needed for RAG/toolkit) + `make seed-sp4`; accounts: admin from `admin.secret`, users `marco|sara|luca|giulia|andrea@meepleai.test` (premium, verified); staging seed via `make seed-sp4-staging`.
- **Branch** — `feature/happy-path-testing-program` (parent `main-dev`).
- **Language** — catalogs written in Italian (project convention), scenario keywords stay Given/When/Then.

## Macro-area → route-path map (for globbing during exploration)

| Area | Glob roots |
|------|-----------|
| U1 Accesso | `apps/web/src/app/(auth)/**` · `(public)/{pricing,about,contact,legal}` · `(authenticated)/{onboarding,setup}` |
| U2 Catalogo & Discover | `(authenticated)/{games,discover}` · `(authenticated)/hub`(+`/games`) · `(public)/shared-games/[id]` |
| U3 Library & KB | `(authenticated)/library/**` · `(authenticated)/{upload,knowledge-base/**,kb/[id],private-games/[id]}` |
| U4 Chat RAG & Agenti | `(chat)/chat` · `(authenticated)/library/[gameId]/agent` · `(authenticated)/{agents/**,editor/**,pipeline-builder,hub/agents}` |
| U5 Game Night | `(authenticated)/game-nights/**` · `(public)/join/event/[code]` |
| U6 Sessioni & Scoring | `(authenticated)/{sessions/**,play-records/**,players/**}` · `(public)/join/session/[code]` |
| U7 Toolkit & Gamebook | `(authenticated)/{toolkit/**,toolkits/**,hub/toolkits,gamebook/**}` · `library/[gameId]/{toolbox,toolkit,play/**}` |
| U8 Profilo & Notifiche | `(authenticated)/{profile/**,notifications/**,versions,dashboard,n8n}` |
| A1 Agenti AI | `admin/(dashboard)/agents/**` |
| A2 KB admin | `admin/(dashboard)/knowledge-base/**` · `admin/(dashboard)/rag-quality` |
| A3 Catalogo condiviso | `admin/(dashboard)/shared-games/**` · `admin/(dashboard)/games/**` |
| A4 Config & Sistema | `admin/(dashboard)/{config/**,content/**,ai}` · `admin/database-sync` |
| A5 Monitoraggio & Utenti | `admin/(dashboard)/{monitor/**,users/**,analytics,ui-library/**}` · `admin/page.tsx` |

> Route ambigue (es. `hub`, `editor`, `n8n`) sono assegnate definitivamente da Task A0 nella mappa globale.

## File Structure

**Phase A (docs, created):**
- `docs/for-developers/testing/happy-path/_coverage-map.md` — global route→area assignment (all 220 routes), authored by A0. Single source that guarantees no orphan route.
- `docs/for-developers/testing/happy-path/_TEMPLATE.md` — shared scenario template + legend, authored by A0.
- `docs/for-developers/testing/happy-path/U1-accesso.md` … `A5-monitoraggio.md` — the 13 catalogs.
- `docs/for-developers/testing/happy-path/RESULTS.md` — execution report skeleton, authored by A0, filled in Phase B.
- `docs/for-developers/testing/happy-path/README.md` — index + how-to-run, authored by A0.

**Phase B produces no source files** — it fills `RESULTS.md` and opens GitHub issues.

---

# PHASE A — Produce the catalogs

## Standard catalog procedure (applies to every Task A1–A13)

Each catalog task follows these steps. The per-task briefing below supplies the concrete route list, expected flows, and exploration targets.

1. **Inventory**: `glob` the area's route roots (see map above) → full list of `page.tsx`. Cross-check against `_coverage-map.md` (A0) so the area's route set matches its assignment.
2. **Explore**: for each route, read the `page.tsx` + its primary components/hooks to learn the real user flow and the observable UI markers (headings, buttons, list items, empty-states). For Flow routes, trace the primary action to the API call (`apps/web/src/lib/api/**`) so the observable is accurate. Use `seed-sp4/data.json` for concrete seed data (game names, user emails).
3. **Coverage matrix**: write the area matrix — every route → scenario ID(s) or `smoke-aggregato`/`skip` + reason.
4. **Scenarios**: write each scenario with the template (`Given/When/Then`, `Osservabile ✅`, `Route`, `Utente`, `[Flow|Smoke]`). Use concrete seed data. Mark Flow-created entities `HP-TEST-<data>`.
5. **Self-verify**: confirm every area route appears in the matrix (no gaps); confirm each scenario has ≥1 observable.
6. **Commit**: `git add` the catalog + `git commit -m "docs(testing): <area> happy-path catalog"`.

**Deliverable per task**: one catalog file, matrix-complete, committed. A reviewer can accept/reject one area's catalog independently.

---

### Task A0: Foundation (coverage map + template + skeletons)

**Files:**
- Create: `docs/for-developers/testing/happy-path/_coverage-map.md`
- Create: `docs/for-developers/testing/happy-path/_TEMPLATE.md`
- Create: `docs/for-developers/testing/happy-path/RESULTS.md`
- Create: `docs/for-developers/testing/happy-path/README.md`

**Interfaces:**
- Produces: `_coverage-map.md` (route→area table consumed by A1–A13 step 1); `_TEMPLATE.md` (scenario template consumed by all); `RESULTS.md` skeleton (consumed by Phase B).

- [ ] **Step 1: Full route inventory.** `glob apps/web/src/app/**/page.tsx` (all 220). For each, assign exactly one macro-area (U1–A5) using the route-path map; resolve ambiguous routes (`hub`, `hub/*`, `editor/*`, `pipeline-builder`, `n8n`, `private-games`, `kb`, `versions`) explicitly. Write `_coverage-map.md` as a table `| route | area | Flow/Smoke (expected) |`. Assert the row count equals the glob count (no route unassigned, none double-assigned).

- [ ] **Step 2: Template.** Write `_TEMPLATE.md` containing the scenario template (spec §5), the pass/blocked-env legend, and the Flow/Smoke definitions. This is what each catalog copies.

- [ ] **Step 3: RESULTS skeleton.** Write `RESULTS.md` with one section per macro-area, each an empty results table `| ID | Tipo | Locale | Staging | Screenshot | Note/Issue |`, plus a top summary line (`totale / ✅ / ❌ / ⚠️ / ⏭️`).

- [ ] **Step 4: README.** Write `README.md`: index of the 13 catalogs, the run procedure (`make dev` → `make seed-sp4` → execute), link to spec + this plan.

- [ ] **Step 5: Commit.** `git add docs/for-developers/testing/happy-path/ && git commit -m "docs(testing): happy-path foundation — coverage map, template, skeletons"`

---

### Task A1: Catalog U1 — Accesso & Onboarding

**Files:** Create `docs/for-developers/testing/happy-path/U1-accesso.md`

**Routes to cover** (verify via glob step 1): `(public)` pricing/about/contact/legal · `(auth)` register/login/logout/reset-password/verify-email/setup-account/welcome/verification-pending/accept-invite · `(authenticated)/onboarding` · `/setup`.

**Expected scenarios** (~18–22): Flow — login (marco), logout, password-reset request→confirm, invite-only register (request-access popup when public reg off), accept-invite token flow, onboarding 3-step wizard (interests→first-game→invite-friend placeholder), setup-account. Smoke — pricing/about/contact/legal static pages, welcome, verification-pending.

**Exploration targets:** `apps/web/src/app/(auth)/**`, `RequestAccessForm`, onboarding `OnboardingGenericWizard` + `InterestsStep`/`FirstGameStep`, proxy `PROTECTED_ROUTES` (`apps/web/src/lib/proxy.ts`) for redirect observables, `RegistrationMode` config. Note the email-verification gate (spec §10): register scenario declares how the local verification token is obtained (SMTP fake/log) as a precondition; if unavailable mark that step `blocked-env`.

- [ ] Follow the Standard catalog procedure (steps 1–6) with the briefing above.

---

### Task A2: Catalog U2 — Catalogo & Discover

**Files:** Create `docs/for-developers/testing/happy-path/U2-catalogo.md`

**Routes to cover:** `(authenticated)/games` (hub tabs discover/catalogo/trending/community) · `(authenticated)/discover` · `(authenticated)/hub` (+`/games/[id]`) · `(public)/shared-games/[id]`.

**Expected scenarios** (~12–16): Flow — browse Discover (default tab), switch tabs, open a shared game detail from catalog, view public shared-game page. Smoke — trending/community ComingSoon tabs, hub landing, invalid-tab fallback to Discover (invariante #20).

**Exploration targets:** `DiscoverHub` component, `/games/page.tsx` multi-tab orchestrator (`parseTab`), `MeepleCard` grid, seed games from `seed-sp4/data.json:games[]` (Azul, Catan, …). Cover uses deterministic placeholder (`cover-utils.ts`), not BGG assets (spec §10).

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A3: Catalog U3 — Library & Knowledge Base

**Files:** Create `docs/for-developers/testing/happy-path/U3-library-kb.md`

**Routes to cover:** `library` (+ wishlist, private, private/add, private/[id], [gameId], [gameId]/kb) · `upload` · `knowledge-base` (+ global, [id], [id]/pdf) · `kb/[id]` · `private-games/[id]`.

**Expected scenarios** (~18–22): Flow — add game to library (owned), add to wishlist, upload a PDF (seed rulebook) and see it indexed, open a game's KB, view a PDF page, add a private game. Smoke — library tabs, wishlist list, knowledge-base list/global, private-games detail.

**Exploration targets:** `library/page.tsx` (hybrid hub #1585), upload flow → `POST /ingest/pdf`, KB list → `GET /admin/pdfs`/`/kb`, seed KB docs (azul-regole-ita.pdf, catan-regole.pdf). Upload indexing can take minutes — observable = doc reaches "indexed/Ready" state.

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A4: Catalog U4 — Chat RAG & Agenti

**Files:** Create `docs/for-developers/testing/happy-path/U4-chat-rag.md`

**Routes to cover:** `(chat)/chat` · `library/[gameId]/agent` · `agents` (+ [id]) · `editor/agent-proposals` (+ create/[id]/edit/[id]/test) · `pipeline-builder` · `hub/agents`.

**Expected scenarios** (~16–20): Flow — ask a rules question and get a streamed cited answer (spec §5 example U4-03), click a citation → opens PDF at page, chat thread on `/chat`, open an agent detail, create/edit an agent proposal in editor, test a proposal. Smoke — agents list, hub/agents, pipeline-builder canvas loads.

**Exploration targets:** `library/[gameId]/agent` chat panel, SSE streaming hook, citation chips (`[Game, p.N]`), seed agents `data.json:agents[]`. LLM output non-deterministic → observables structural (answer present, ≥1 citation, streaming happened), not literal text (spec §10). Needs `make dev` full (AI stack).

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A5: Catalog U5 — Game Night

**Files:** Create `docs/for-developers/testing/happy-path/U5-game-night.md`

**Routes to cover:** `game-nights` (+ new, [id], [id]/live, [id]/summary) · `(public)/join/event/[code]`.

**Expected scenarios** (~12–16): Flow — create a game night (`HP-TEST-<data>`), invite users (tag→"Invia inviti"→pending per domain model), publish (raises invites), join via event code (second user), start it (→ in-progress via first session), view summary. Smoke — game-nights list, event detail, public join-by-code page.

**Exploration targets:** `game-nights/**`, domain model spec `2026-06-04-gamenight-session-domain-model.md` (5-phase RSVP, `GameNightEvent` aggregate, publish→email), seed events `data.json:events[]`. Multi-user scenarios use marco + sara. Invariante max-1-live (#10).

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A6: Catalog U6 — Sessioni & Scoring

**Files:** Create `docs/for-developers/testing/happy-path/U6-sessioni-scoring.md`

**Routes to cover:** `sessions` (+ new, join, [id], [id]/live, [id]/notes, [id]/scoreboard, [id]/join) · `play-records` (+ new, [id], [id]/edit, stats) · `players` (+ [id], [id]/{achievements,games,sessions,stats}) · `(public)/join/session/[code]`.

**Expected scenarios** (~20–24): Flow — create a session, join via code, go live, edit polymorphic scores (Points/BinaryWin/Objectives/Ranking) as host, view scoreboard, add session notes, create+complete a play-record, edit a play-record. Smoke — sessions list, players list/detail sub-pages, play-records stats.

**Exploration targets:** session live shell (epic #2354, G1 layout), `PolymorphicScoreEditor` + `ScoreTabContent` (host swap #2430), `useLiveSessionStore`, `useUpdateSessionScores`, seed sessions `data.json:sessions[]` + play-records. Scoring host-only (IDOR guard); non-host sees read-only renderer.

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A7: Catalog U7 — Toolkit & Gamebook

**Files:** Create `docs/for-developers/testing/happy-path/U7-toolkit-gamebook.md`

**Routes to cover:** `toolkit` (+ play, history, stats, templates, [sessionId]) · `toolkits` (+ [id]) · `hub/toolkits` · `gamebook` (+ upload) · `library/[gameId]/{toolbox,toolkit,toolkit/[sessionId],play,play/[campaignId],play/[campaignId]/encounter,play/[campaignId]/translate}`.

**Expected scenarios** (~18–22): Flow — open a toolkit and play a session, view toolkit history/stats, use a template, upload a gamebook, play a gamebook campaign (encounter), translate a campaign page. Smoke — toolkits list, hub/toolkits, toolkit templates, library toolbox.

**Exploration targets:** toolkit play flow, gamebook campaign play/encounter/translate (`library/[gameId]/play/[campaignId]/**`), seed toolkits `data.json:toolkits[]`. Toolkit AI generation needs full stack.

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A8: Catalog U8 — Profilo & Notifiche

**Files:** Create `docs/for-developers/testing/happy-path/U8-profilo-notifiche.md`

**Routes to cover:** `profile` (+ achievements) · `notifications` (+ preferences) · `versions` · `dashboard` · `n8n`.

**Expected scenarios** (~10–14): Flow — view/edit profile, view achievements, mark notification read, change notification preferences. Smoke — dashboard (4 priority sections: Prossimi/Recenti/Suggeriti/FriendsActivity per asse-C), versions page, n8n integration page.

**Exploration targets:** `profile/page.tsx`, `DashboardClient` (asse-C priority sections), `useNotificationsCounter` SSE, notifications preferences form.

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A9: Catalog A1 — Agenti AI (admin)

**Files:** Create `docs/for-developers/testing/happy-path/A1-agenti.md`

**Routes to cover** (glob `admin/(dashboard)/agents/**`): builder, config, playground, ab-testing (+ new/[id]/results), definitions (+ create/[id]/[id]/edit/playground), models, pipeline, sandbox, strategy, templates, inspector, analytics, usage, chat-history, chat-limits, debug, debug-chat, infrastructure, `agents/page.tsx`.

**Expected scenarios** (~14–18, mostly Smoke + a few Flow): Flow — create an agent definition, run it in playground, configure chat-limits. Smoke — all dashboards (analytics, usage, inspector, models, strategy, infrastructure, ab-testing results) load with data/empty-state; builder + sandbox canvases render; debug-chat responds.

**Exploration targets:** admin agents pages, admin auth (admin.secret account). Many are read-only dashboards → Smoke. Verify no unexpected 4xx/5xx/Console errors.

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A10: Catalog A2 — Knowledge Base admin

**Files:** Create `docs/for-developers/testing/happy-path/A2-kb-admin.md`

**Routes to cover** (glob `admin/(dashboard)/knowledge-base/**` + `rag-quality`): documents, embedding, queue, rag-pipeline, feedback, games, processing, mechanic-extractor (dashboard, analyses), rag-quality.

**Expected scenarios** (~12–16): Flow — upload+embed a KB doc from admin, view processing/queue progress, review a mechanic-extractor analysis, submit RAG feedback. Smoke — documents list, embedding status, rag-pipeline, rag-quality dashboard, kb games list.

**Exploration targets:** admin KB tooling, mechanic-extractor (ME-M1.x work), embedding/queue status pages, `POST /ingest/pdf`. Overlaps U3 upload but from admin surface.

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A11: Catalog A3 — Catalogo condiviso (admin)

**Files:** Create `docs/for-developers/testing/happy-path/A3-catalogo-condiviso.md`

**Routes to cover** (glob `admin/(dashboard)/shared-games/**` + `games/**`): shared-games (list, all, new, import, wizard, seeding, [id], [id]/kb, [id]/rag-setup, [id]/knowledge-base) · games (new, [gameId]/phases, [gameId]/agent/test, [gameId]/processing).

**Expected scenarios** (~14–18): Flow — create a shared game (`HP-TEST-<data>`), quick-publish, import a game, run the wizard, seed a game, configure a game's RAG-setup, define phases, test a game's agent. Smoke — shared-games list/all, seeding dashboard, game processing status.

**Exploration targets:** `admin/shared-games/**`, seeding page, wizard, `POST /admin/shared-games` + quick-publish (see seed step 20). This is the admin flow the seed automates — Flow scenarios exercise the manual path.

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A12: Catalog A4 — Config & Sistema (admin)

**Files:** Create `docs/for-developers/testing/happy-path/A4-config-sistema.md`

**Routes to cover** (glob): config (+ tiers, n8n), content (+ email-templates), ai, `admin/database-sync`.

**Expected scenarios** (~10–14): Flow — toggle Registration Mode (invite-only ↔ public, ties to U1), edit a tier, edit an email template, configure n8n, trigger a database-sync op. Smoke — config landing, AI config, content landing, database-sync status.

**Exploration targets:** `admin/config/**` (RegistrationMode toggle — the invite-only control from CLAUDE.md), tiers, email-templates, n8n config, `admin/database-sync`. Registration toggle scenario must restore original state after (non-destructive).

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A13: Catalog A5 — Monitoraggio & Utenti (admin)

**Files:** Create `docs/for-developers/testing/happy-path/A5-monitoraggio.md`

**Routes to cover** (glob): monitor (grafana, mau, logs, services, service-calls), users/activity, analytics, ui-library (+ compositions, compositions/[id], [id]), `admin/page.tsx`.

**Expected scenarios** (~12–16, mostly Smoke): Flow — view user activity, open a UI-library component detail. Smoke — admin landing, all monitor dashboards (grafana embed, MAU, logs, services, service-calls), analytics, ui-library grid + compositions.

**Exploration targets:** `admin/monitor/**` (many are read-only/embed dashboards → Smoke; grafana may need the monitoring stack from `make dev` full, else `blocked-env`), users/activity, ui-library.

- [ ] Follow the Standard catalog procedure with the briefing above.

---

### Task A-FINAL: Corpus review gate

- [ ] **Step 1:** Verify all 13 catalogs exist and each has a coverage matrix. Cross-check `_coverage-map.md`: every one of the 220 routes appears in exactly one catalog's matrix (scripted check: collect all matrix routes, diff against the map).
- [ ] **Step 2:** Update `RESULTS.md` summary counts to reflect the total scenario count produced.
- [ ] **Step 3:** Commit `docs(testing): complete happy-path catalog corpus (13 areas)`.
- [ ] **Step 4:** **STOP — big-bang review gate.** Present the corpus to the user for review before Phase B (per spec §9 big-bang rhythm). Do not start execution until approved.

---

# PHASE B — Execute (local → staging)

> Runs only after the corpus is approved. Inline, sequential, single environment. Order follows spec §6: U1 → U2 → U3 → U4 → U5 → U6 → U7 → U8 → A3 → A2 → A1 → A4 → A5.

### Task B0: Environment preflight

- [ ] **Step 1:** Start stack — `cd infra && make dev`. Wait until web `:3000` and API `:8080/scalar/v1` respond (poll, don't fixed-sleep).
- [ ] **Step 2:** Seed — `make seed-sp4`. Confirm completion (admin + 5 users + games + indexed PDFs). If KO, diagnose before proceeding (spec §10 env risk).
- [ ] **Step 3:** Load browser MCP tools (single ToolSearch batch: tabs_context, navigate, computer, read_page, tabs_create, read_console_messages, read_network_requests). Open a tab, confirm login as `marco@meepleai.test` works (real login, no bypass). Screenshot the dashboard as the smoke baseline.
- [ ] **Step 4:** Record env readiness in `RESULTS.md` (versions, seed summary, timestamp passed via context — no `Date.now()` in scripts).

### Tasks B1–B13: Execute each area (local → gate → staging)

For each macro-area in spec §6 order, one task with this procedure:

- [ ] **Step 1 (local):** For each scenario in the area's catalog: navigate, perform the `When` steps in the real browser, verify every `Osservabile ✅`. Capture a screenshot per scenario. Record `✅/❌/⚠️` in `RESULTS.md` (Locale column). Use `HP-TEST-<data>` markers for Flow-created data.
- [ ] **Step 2 (failures):** For each `❌`, open a GitHub issue (scenario ID, repro steps, expected vs observed, screenshot, env=local) per project workflow. Do not fix.
- [ ] **Step 3 (gate):** If all area scenarios are `✅` (⚠️ blocked-env does not block), proceed to staging. Otherwise mark the area "blocked on staging" in `RESULTS.md` and continue to the next local area (staging pass deferred until issues resolved separately).
- [ ] **Step 4 (staging):** Repeat the scenarios against `https://meepleai.app` (seed via `make seed-sp4-staging` beforehand; honor the staging email/tier gate, spec §4). Use only `HP-TEST-<data>` data; no destructive ops. Record the Staging column + any local/staging delta.
- [ ] **Step 5:** Commit the `RESULTS.md` update for the area.

### Task B-FINAL: Report & cleanup

- [ ] **Step 1:** Finalize `RESULTS.md` summary (totals, pass rate per area, open issues list, local/staging deltas).
- [ ] **Step 2:** Staging cleanup — `make seed-sp4-reset-staging` (explicit confirm) + note any manually-created `HP-TEST` data for removal (spec §7.1).
- [ ] **Step 3:** Commit `docs(testing): happy-path execution results`.
- [ ] **Step 4:** Open/PR to `main-dev`; summarize pass rate + filed issues to the user.

---

## Self-Review (author checklist)

**Spec coverage:** spec §2 perimeter → A0 map + A1–A13 (all 13 areas). §3 method → Global Constraints + Standard procedure. §4 prereqs → B0. §5 format + pass criterion → Global Constraints + template A0. §6 order → Phase B intro + B1–B13. §7 gate + §7.1 state mgmt → B1–B13 steps 3–4 + HP-TEST markers + B-FINAL cleanup. §8 report → RESULTS.md (A0 skeleton, B fills). §9 structure + big-bang → file structure + A-FINAL gate. §10 risks → addressed (email-verify in A1, dev-full in A4/A7/A13, staging pollution in B4/B-FINAL, env preflight B0). §11 done → B-FINAL. **No gaps.**

**Placeholder scan:** no TBD/TODO; each catalog task carries concrete routes + expected flows + exploration targets; Standard procedure holds the shared steps (referenced, not "TODO"). OK.

**Consistency:** area IDs U1–U8/A1–A5 consistent across map, file structure, tasks. `HP-TEST-<data>` marker consistent. `_coverage-map.md` / `_TEMPLATE.md` / `RESULTS.md` names consistent between A0 (produces) and consumers. OK.
