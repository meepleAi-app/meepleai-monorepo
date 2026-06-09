# Manual Test Session — main-dev (2026-06-09)

**Tester**: badsworm@gmail.com
**Branch**: `main-dev` @ `6e152796e`
**Topologia**: stack locale completo (Docker Compose `compose.dev.yml --profile ai`) — snapshot rigenerato via `make seed-index` per allineamento schema giugno.
**Obiettivo**: validare feature shipped tra 2026-06-01 e 2026-06-09 + riprova Asse A/B/C/D (#1896-#1899) shipped 2026-06-05.

## Setup

| Componente | URL | Stato |
|---|---|---|
| Web (Next.js) | http://localhost:3000 | ✅ HTTP 200 |
| API (.NET) | http://localhost:8080 | ✅ Healthy (20/20 checks) |
| API docs | http://localhost:8080/scalar/v1 | ✅ HTTP 200 |
| Postgres | localhost:5432 (db `meepleai_staging`, user `meepleai`) | ✅ Healthy |
| Mailpit (email dev) | http://localhost:8025 | ✅ HTTP 200 |
| Embedding | localhost:8000 (768-dim multilingual-e5-base) | ✅ Healthy |
| Reranker | localhost:8003 | ✅ Healthy |
| Orchestration | localhost:8004 | ✅ Healthy |
| Unstructured (PDF) | localhost:8001 | ✅ Healthy |
| Smoldocling (PDF alt) | localhost:8002 | ❌ Restart-loop (torch bug, non blocker) |
| Ollama | localhost:11434 | ✅ Healthy |
| MinIO/S3 R2 storage | external (`meepleai-uploads`) | ✅ Healthy |
| Grafana | http://localhost:3001 | ✅ Healthy |
| Prometheus | http://localhost:9090 | ✅ Healthy |
| Catalog FTS | 159 games / 65 categories / 158 mechanics | ✅ |

## Account test (seedati da `CoreSeedLayer`)

| Ruolo | Email | Password source |
|---|---|---|
| Admin | `admin@meepleai.app` | `ADMIN_PASSWORD` in `infra/secrets/admin.secret` |
| User test | `test@meepleai.com` | `SEED_TEST_PASSWORD` in `infra/secrets/admin.secret` |
| SuperAdmin (project owner) | `badsworm@gmail.com` | `SEED_BADSWORM_PASSWORD` in `infra/secrets/admin.secret` |

Per i test multi-utente: usa **badsworm@gmail.com come host** e **test@meepleai.com come guest**. Se serve un terzo utente, registra `host2@test.local` via `/register` con publicRegistration attivo.

## PR mergiati 2026-06-01 → 2026-06-09 in scope

- #2049 (2026-06-09) — fix(hub): #2043 Bug 3 game detail route → canonical /shared-games
- #2047 (2026-06-09) — fix(hub): #2043 Bug 2 /hub redirect → /hub/games
- #2046 (2026-06-09) — fix(hub): #2043 hub catalog visibility
- #2044 (2026-06-09) — claude-md baseline cleanup (Asse A PdfDocument 7-state fix)
- #2039 (2026-06-09) — reindex pending state
- #2033 (2026-06-09) — search vector tsvector
- #2030 (2026-06-08) — admin shared-game gaps G1/G2/G5
- #1961 (2026-06-07) — cherry-pick CS8604 to main-dev
- #1893 (2026-06-04) — claude design demo
- #1870 (2026-06-03) — issue 1853/1854 recover followup (fix)
- #1868 (2026-06-03) — issue 1836 config flags reskin
- #1867 (2026-06-03) — issue 1853/1854 C1 followup
- #1866 (2026-06-03) — issue 1852 handler update call (hotfix)
- #1863 (2026-06-03) — issue 1852 cover propagation
- HEAD merge (2026-06-09 post-pull) — BGG enrichment queue + ErrorCodeClassifier + library designers

## Test plan

Format: `[PASS|FAIL|SKIP] descrizione — note/evidenza`

### 1️⃣ Smoke golden path

- [ ] Login con account host
- [ ] Dashboard mostra 4 priority sections (Prossimi, Recenti, Suggested, Friends Activity) — Asse C #1898
- [ ] MainSidebar mostra 8 voci (Library, Games, Dashboard, GameNights, Sessions, Friends, Notifications, Settings) — Asse B #1897
- [ ] DesktopShell visibile solo su `lg+`
- [ ] Naviga `/games` → tab Discover di default (DEC-1 Asse D P2)
- [ ] Game detail page render correttamente

### 2️⃣ Hub routing fixes (#2046-#2049)

- [ ] `/hub` → redirect a `/hub/games` (#2047)
- [ ] `/hub/games/[id]` → render dettaglio canonical, NO 404 (#2049)
- [ ] `/hub/games` → catalog visibile (#2046)
- [ ] Anchor link da MainSidebar a `/games?tab=discover` → render Discover tab

### 3️⃣ Polymorphic score editor (Asse D P1 #1899)

Crea una Session in GameNight Published; per ognuno dei 4 tipi:

- [ ] `Points` (host): editor numerico, autosave debounced 500ms
- [ ] `Points` (guest non-host): UI fallback su `ScoreBoard` legacy (backward-compat)
- [ ] `BinaryWin` (host): toggle win/lose per player
- [ ] `Objectives` (host): selezione da `MVP_OBJECTIVES_CATALOGUE` placeholder
- [ ] `Ranking` (host): drag&drop con @dnd-kit/sortable, riordino persistente
- [ ] IDOR: guest tenta `PATCH /api/v1/sessions/{id}/scores` → 403 Forbidden

### 4️⃣ Live session invariants (Asse A #1896)

- [ ] Crea GameNight, lancia 1° Session → `Session.StartedAt != null`
- [ ] Tenta lanciare 2° Session contemporanea → atteso `409 MaxLiveSessionsExceededException`
- [ ] Completa Session → `FinalizedAt != null`, status → completed
- [ ] Side effect: GameNight in dashboard "Recenti"
- [ ] Verifica X-Warning-Code header su comandi degradati (Asse A WP2)

### 5️⃣ Onboarding wizard 3-step (Asse D P3)

Crea account fresco:

- [ ] `/onboarding` mostra `WizardModal` 3 step (non 5 legacy)
- [ ] Step 1: InterestsStep (gate `interestsCompleted`)
- [ ] Step 2: FirstGameStep → catalogo interno via `api.games.getAll()` (NON BGG — vincolo #1903)
- [ ] Step 3: InviteFriendComingSoonStep → solo skip
- [ ] Completion redirect a `/dashboard`

### 6️⃣ Cover propagation + admin shared-game (#1863, #1870, #2030)

- [ ] AddGameDrawer (`/library`) con BGG cover → cover salvata
- [ ] Admin shared-game gap analysis G1/G2/G5 (#2030) — solo admin
- [ ] Catalogo `/games?tab=discover` mostra cover

### 7️⃣ Chat KB / RAG (#2033, #2039)

⚠️ Richiede embedding/reranker/orchestration up

- [ ] Upload PDF in admin → indexing
- [ ] Reindex pending state visibile in UI (#2039)
- [ ] Chat thread con search vector tsvector (#2033) — query Italian/English
- [ ] Streaming SSE → token incrementale visibile
- [ ] ChatInfoPanel sidebar expansion
- [ ] Verifica risposta cita fonti corrette

### 8️⃣ Admin config flags + registration mode (#1868)

- [ ] Admin → `/admin/config` → General
- [ ] Toggle `publicRegistrationEnabled = false`
- [ ] `/register` (logout) → mostra `RequestAccessForm` invece di register standard
- [ ] Toggle back `true` → register standard restored
- [ ] Config flags UI reskinned (#1868)

## Bug / issue trovati

| # | Severity | Step | Descrizione | Repro | Screenshot/Log |
|---|---|---|---|---|---|
| 1 | Low/UX | 1.1 | SuperAdmin atterra su `/admin/overview` invece di `/library` (atteso da design #893: "admins/superadmins land on the user app by default"). Probabilmente perché URL iniziale era admin (bookmark) o middleware `from=` settato. Da chiarire: dovrebbe il backend/middleware forzare landing su `/library` per superadmin ignorando `from=` quando from punta a route admin? | Login con badsworm@gmail.com da URL non specificato | n/a |
| 2 | Medium/UX | 1.4 | Header "legacy" `🔍 Hub · /discover` mostrato anche su `/games?tab=discover`. Causa: `DiscoverHero.tsx:40` ha badge route hardcoded. Refactor Asse D P2 (#1899) ha estratto `DiscoverHub` riusabile ma non ha parametrizzato il `Hero` per riflettere la route effettiva. Fix: aggiungere prop `routeBadge` (o `pathnameOverride`) a `DiscoverHero` e propagarla da `DiscoverHub` come fa già per `effectivePathname`. | Login → navigate `/games` → osservare il badge in cima | `apps/web/src/components/features/discover/DiscoverHero.tsx:40` |
| 3 | Medium/UX | 1.x | Logo `MeepleLogo` SVG legacy (meeple body + ai-spark) usato su pagine `(public)` + `(auth)` + `PublicFooter`. Brand identity incoerente vs `AppTopBar` ("M" gradient canonical). Soluzione: creare `BrandMark` riusabile + sostituire 3 usage + refactor `AppTopBar`. Issue **#2057** aperta con scope completo e acceptance criteria. | Home `/`, login, register, footer | `meeple-logo.tsx`, `UnifiedHeader.tsx:67`, `AuthLayout.tsx:74`, `PublicFooter.tsx:78` |

## Note operative

- Email vanno a Mailpit, non delivery reale (memory `dev-email-uses-mailpit.md`)
- Se vedi 500 con `InvalidOperationException` → stop e investiga (rule #2568)
- Per check exception API: `make logs s=api` o `pwsh -c "docker logs meepleai-api --tail=100"`
- Per check web SSR: `pwsh -c "docker logs meepleai-web --tail=50"`

## Conclusione

- **Avvio sessione**: ⏳
- **Fine sessione**: ⏳
- **Esito complessivo**: ⏳
- **Issue da aprire**: ⏳
