# Nanolith Demo Runthrough — Anomalies Live Log

> Live MD compilato durante la pipeline runthrough Fase A. Tag classification per ogni entry:
> - `#BLOCKER` — failure reproducible 3× consecutivi che impedisce stadio
> - `#ANOMALY` — comportamento sub-target ma non bloccante (latenza, UX rough)
> - `#OBSERVATION` — note libere, comportamento atteso ma vale memorizzare

**Spec di riferimento:** `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md`
**Plan di riferimento:** `docs/superpowers/plans/2026-05-08-nanolith-demo-runthrough-phase-a.md`

---

## §0 — T-10d Checkpoint Go/No-Go (data: 2026-05-08)

**Domanda:** PR #828 + #837 mergiati su main-dev?

[x] **YES** — PR #828 merge commit: `1df39e840` (2026-05-08 fast-forward) | PR #837 merge commit: `cbec2383f` (2026-05-08 fast-forward)
       → **Path A primary continues**

[ ] NO  — Mancano: N/A
       → Path B fallback: N/A

**Decisione finale:** **Path A primary** (Iter 1.A + Iter 1.B mergiati su `main-dev` PRIMA di T-10d effettivo).

**Contesto aggiuntivo discovered durante Task 0**:

Durante l'esecuzione di Task 0 (workspace setup) il `git pull --ff-only` su `main-dev` ha rivelato un fast-forward `71eab98e5..ed4f26830` di 144 file changes. Questo include i seguenti merge realizzati da team review fuori dalla nostra sessione brainstorming:

| Hash | PR | Descrizione |
|---|---|---|
| `1df39e840` | #828 | feat(gamebook): Iter 1.A — campaign sessions + chat shell for Nanolith dogfood |
| `cbec2383f` | #837 | feat(gamebook): Iter 1.B — photo translate + glossary + history for Nanolith dogfood |
| `3957b3c0b` | #838 | feat(gamebook): resume picker 4 stati + legacy routing cleanup |
| `ed4f26830` | #839 | docs/nanolith-staging-setup-design (merge commit) |

**Implicazione per pipeline plan**:

- ✅ **Task 1 e Task 2 SKIP** — entrambe PR mergiate fuori sessione, no review/merge ops da fare
- ✅ **Task 4 SKIP** — PR #838 ha shipped R5 ResumeCard 4 stati (mockup G state-01/02/03/04 + GamebookResumeShell orchestrator). `ResumeHero.tsx` (78 LOC) copre 1:1 lo scope state-02 originalmente plannato in Task 4
- ✅ **Task 1.A.9 legacy cleanup** anche shipped via PR #838: `apps/web/src/app/(authenticated)/library/games/[gameId]/translate/page.tsx` deleted (era il DEMO Path 5a workaround)
- 📦 **Bonus**: `2026-05-07-nanolith-staging-setup-design.md` + plan `2026-05-07-nanolith-staging-setup.md` aggiunti via PR #839 — riferimento utile per Task 5 seed staging

**Outcome T-10d**: pipeline pre-T-2d è già stata completata. Possiamo procedere immediatamente con:
- Task 3 (questo file, doc log) ✅
- Task 6-7 (smoke script + Makefile)
- Task 9 (Playwright @demo-runthrough)
- Poi G1-G5 runthrough operational

**Timeline reale collapsata**: 14gg → ~3-4gg.

---

## §1 — Stadio 1 (locale endpoint smoke) — eseguito 2026-05-08

**Setup reale (deviazioni dal plan)**:
- API via `dotnet run --no-launch-profile` locale (Docker container API build fallisce con `dotnet publish -c Release` OOM su WSL2; replicato anche staging exit 137).
- Postgres + Redis via Docker.
- DB seed: Strategy A snapshot/restore (`make snapshot` 123MB → `psql -f staging-snapshot-2026-05-08.sql` → `meepleai_db` locale).
- Smoke eseguito inline curl manuale (script `demo-smoke-local.sh` rimosso da workspace dall'utente — restore via commit `54f2fa649`).

**Endpoint outcome**:

| # | Endpoint plannato | Status reale | Note |
|---|---|---|---|
| 1 | `POST /api/v1/auth/login` | ✅ 200 | Login `badsworm@gmail.com / TestNanolith2026!` OK |
| 2 | `GET /api/v1/library/me` | ❌ 404 → ✅ 200 con path corretto `/library` | Plan path errato. Reale: `GET /api/v1/library`. Response `{items: [...]}`. Nanolith gameId `94e99e38-...` |
| 3 | `GET /api/v1/games/{nanolithId}` | ✅ 200 | OK |
| 4 | `POST /api/v1/gamebook/campaigns` | ✅ 201 | Campagna UUID `63a388b0-...`, body `{gameId, title}` |
| 5 | `POST /api/v1/agents/chat-stream` | ❌ 405 | Endpoint inesistente. Reale: chat multi-step `POST /chat/sessions` + `POST /chat/sessions/{id}/messages` |
| 6 | `POST /api/v1/gamebook/campaigns/{id}/photos` | ⏭️ non testato | Multipart upload deferred a G2 Playwright |
| 7 | `POST /.../{photoId}/translate` | ⏭️ non testato | Deferred a G2 |

**Anomalies/Observations**:

- **`#OBSERVATION` Endpoint contract drift plan vs codice (3/7)**: path nominali del plan G1.1 (`/library/me`, `/agents/chat-stream`) non corrispondono al codice shipped. Concern già flagged dal Task 6 implementer subagent. NON application bug — è plan contract drift. Playwright G2 usa data-testid reali, non eredita problema.
- **`#OBSERVATION` Agent name drift**: plan "Nanolith Tutor" vs codice "Arbitro Nanolith" (id `7b2b1b91-...`, type `RulesInterpreter`).
- **`#OBSERVATION` Docker build API OOM cross-environment**: `dotnet publish -c Release` exit 137 SIGKILL sia locale WSL2 sia staging server. Workaround `dotnet run --no-launch-profile` con env override.
- **`#OBSERVATION` Postgres password drift**: `database.secret` `POSTGRES_PASSWORD=postgres` vs container `change_me_strong_password_here`. Override env CLI.

**G1 status**: ✅ **PASS-WITH-OBSERVATIONS** — 4/7 endpoint OK valida step user flow [1]-[4]. Step [5]-[6] deferred a G2/G3. Nessun `#BLOCKER` reproducible.

**Spec/Plan amendments carry-forward**:
- Spec §3 G1.1 Gherkin tabella: aggiornare path corretti (`/library`, chat multi-step)
- Plan Task 6: rifare smoke script con endpoint contract reali

---

## §2 — Stadio 2 (locale browser flow)

(in attesa T-1d, eseguibile dopo G1 pass + Task 9)

**Data esecuzione:** TBD
**Browser:** Chrome ≥ 120 desktop su localhost:3000
**Auto Playwright:** TBD
**Manual flow time:** TBD

**Anomalies/Observations:**
- TBD

**G2 status:** ⏳ pending

---

## §3 — Stadio 3 (staging browser flow) — partial 2026-05-08

**Setup eseguito**:
- Pull staging repo `/opt/meepleai/repo` da `e82ecb430` → `8faeac3d` (60 commit ahead).
- `docker compose -f docker-compose.yml -f compose.staging.yml --profile ai-essential --profile monitoring-essential up -d` lanciato; build api+web da source.
- Cloudflared tunnel running (systemd service `active`).
- Password reset `badsworm@gmail.com` → `TestNanolith2026!` via PBKDF2-SHA256 600k hash + `UPDATE users SET PasswordHash = 'v1.600000...'` direct DB.

**Endpoint outcome (4 testati via CF Access service token)**:

| # | Endpoint | Status | Note |
|---|---|---|---|
| 1 | `POST /auth/login` | ✅ 200 | badsworm authenticated, response include user object |
| 2 | `GET /library` | ✅ 200 | `items[]` con Nanolith gameId `94e99e38-...` |
| 3 | `GET /games/{id}` | ✅ 200 | OK |
| 4 | `POST /gamebook/campaigns` | ❌ 404 | Endpoint Iter 1.A non registrato — container build stale |

**Anomalies/Observations**:

- **`#BLOCKER` Force rebuild OOM staging** (exit 137): `docker compose build --no-cache api web` su staging ha exit 137 SIGKILL stesso pattern locale. Container API running ha codice OLD (non Iter 1.A). Step [4] CTA libro game endpoint `/gamebook/campaigns` 404. Resolution richiede memory tuning Docker su staging server (oltre scope sessione).
- **`#OBSERVATION` PDF Nanolith assenti staging**: `data/rulebook/nanolith_datasource/Nanolith Rules ENG.pdf` (101MB) + `Press Start ENG.pdf` (36MB) non presenti né su repo locale né staging filesystem né MinIO bucket. Seed `make seed-nanolith-demo-staging` patched per skip PDF verify (account + game + agent senza KB).
- **`#OBSERVATION` Admin secret bash sourcing fail**: `infra/secrets/admin.secret` ha `ADMIN_DISPLAY_NAME=System Administrator` non quoted, fallisce `. admin.secret` con "Administrator: command not found". Workaround: env override CLI `INITIAL_ADMIN_EMAIL=... INITIAL_ADMIN_PASSWORD=... bash seed-nanolith-demo.sh`.
- **`#OBSERVATION` Cloudflare Access curl headers**: smoke staging endpoint via public URL richiede `CF-Access-Client-Id` + `CF-Access-Client-Secret` headers (da `cf-access.secret`). Senza CF headers tutti gli endpoint ritornano 302 redirect a CF auth.
- **`#OBSERVATION` Staging deployment fragile**: API + Web + Cloudflared completamente assenti all'inizio sessione (rimossi pre-sessione, possibile post PR #840 cutover Traefik→CF Tunnel). Restart via `docker compose up -d api web` ricreate container ma rebuild cache stale → endpoint Iter 1.A non disponibile.

**G3 status**: 🟡 **PARTIAL** — 3/4 endpoint testati OK validano step [1]-[3] user flow staging (login, collection, game detail). Step [4] CTA libro game blocked da staging build OOM. Step [5]-[7] non testati (dipendono [4]).

**G3 follow-up necessario** (post sessione):
- Fix Docker build OOM staging (memory limit, multi-stage cache, swap, OR build pre-built image from CI artifact)
- Re-run G3 manual Chrome flow contro staging URL public dopo build green
- G4 mobile Android Chrome (gated da G3 manual pass)

**Data esecuzione:** TBD
**URL:** https://meepleai.app
**Auto smoke:** TBD
**Manual flow time:** TBD
**Diff funzionali vs G2:** TBD

**Anomalies/Observations:**
- TBD

**G3 status:** ⏳ pending

---

## §4 — Stadio 4 (smartphone Android Chrome)

(in attesa T-0d, eseguibile dopo G3 pass + device acquisition + page Storybook prep)

**Data esecuzione:** TBD
**Device:** TBD (Android ≥ 11)
**Browser:** Chrome [version]
**Manual flow time:** TBD
**Camera permission flow:** TBD
**Foto upload time:** TBD
**SSE translate first-token:** TBD

**Anomalies/Observations:**
- TBD

**G4 status:** ⏳ pending

---

## §5 — Pipeline closure (T-0d EOD)

(in attesa post-G4)

**Pipeline status:** ⏳ pending
**Branch:** feature/nanolith-demo-runthrough-phase-a
**PR:** TBD (open via gh pr create dopo G5)
**Total time:** TBD

**Lessons learned:**
- TBD
