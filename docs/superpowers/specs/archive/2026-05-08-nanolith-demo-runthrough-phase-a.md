---
title: Nanolith Libro Game Demo Runthrough — Fase A (Aaron Solo Pre-Validation)
status: draft
type: spec-design
date: 2026-05-08
authors: [DegrassiAaron]
related-specs:
  - 2026-05-07-libro-game-nanolith-demo-design.md
  - 2026-05-08-libro-game-iter1-cross-branch-audit.md
  - 2026-05-04-libro-game-assistant-vision.md
related-plans:
  - 2026-05-07-libro-game-nanolith-iter1-plan.md
  - 2026-05-07-libro-game-nanolith-iter-1a.md
  - 2026-05-07-libro-game-nanolith-iter-1b.md
related-prs:
  - meepleAi-app/meepleai-monorepo#828
  - meepleAi-app/meepleai-monorepo#837
review-cycle: brainstorming-Q1-Q10 + spec-self-review
---

> **Scope**: spec orchestratore della **Fase A — Aaron solo pre-validation runthrough** del caso d'uso Nanolith libro game. NON sostituisce lo spec design Iter 1 (`2026-05-07-libro-game-nanolith-demo-design.md`) che resta fonte di verità feature. NON copre Fase B (Aaron + amici dogfood serata, spec separato post-Fase-A).
>
> **Decision-ready**: orchestratore di pipeline 4-stadi (locale endpoint → locale browser → staging browser → smartphone Android Chrome) con 5 SMART goals (G1-G5), 14 giorni timeline, scope-protection esplicita e go/no-go binario per abilitare Fase B planning.

# Nanolith Libro Game Demo Runthrough — Fase A

## Indice

- [1. Executive Summary + Architettura](#1-executive-summary--architettura)
- [2. SMART Goals G1-G5](#2-smart-goals-g1-g5)
- [3. Scenari Gherkin](#3-scenari-gherkin)
- [4. Timeline + Checkpoint + Scope-protection](#4-timeline--checkpoint--scope-protection)
- [5. Decision Log + Out of Scope + Open Questions](#5-decision-log--out-of-scope--open-questions)
- [6. Riferimenti](#6-riferimenti)

---

## 1. Executive Summary + Architettura

### 1.1 Vision

> **Aaron (badsworm@gmail.com) esegue un runthrough tecnico-funzionale solo del caso d'uso Nanolith libro game su 4 ambienti (locale endpoint → locale browser → staging browser → smartphone Android Chrome) entro 14 giorni dall'approvazione spec, raccogliendo evidence lightweight (1 screenshot mobile + note MD) e classificando ogni anomalia come `#BLOCKER`/`#ANOMALY`/`#OBSERVATION`. La demo è considerata passata se `count(#BLOCKER) == 0` AND tutti e 4 gli stadi pipeline pass entro target.**

### 1.2 Caso d'uso

```
[1] Aaron logga (badsworm@gmail.com / TestNanolith2026!)
[2] Vede Nanolith nella sua collezione
[3] Seleziona il gioco
[4] Da game page → CTA "avvia partita libro game" (NanolithCampaignCTA)
[5] Agente "Nanolith Tutor" aiuta nel setup (chat shell, Q&A su Press Start KB)
[6] Carica foto pagina dello Storybook → agente esegue OCR + segmentation + traduzione SSE
```

### 1.3 Out of Scope (Fase A)

Spostati a Fase B / Iter 2 / follow-up:

- Aaron + amici dogfood serata (= Fase B, spec separato)
- N4 resume cross-day (Fase B / Iter 2)
- Glossary editor inline (Iter 2)
- History drawer (Iter 2)
- Encounter Book UX cheatsheet (Iter 2)
- iOS Safari testing (Fase B con device amici)
- Multi-amici on-device (Fase B)
- R1 Polly Circuit Breaker LLM (follow-up post Iter 1)
- R2 Polly Bulkhead (follow-up post Iter 1)
- R3 Prometheus 9 metriche `translation_*`/`ocr_*`/`glossary_*`/`llm_*` (follow-up)
- R4 EXIF GPS stripping client-side (follow-up; Aaron solo, foto temporanee)
- R6 Quota credits UI (Aaron è Admin role, no quota visibile)
- R7 Phase 1.A.9 legacy cleanup (`/library/games/[id]/translate` DEMO removal — non blocca pipeline)
- R5 state-03 multi-campaign list + state-04 stale warning (deferred Fase B per dipendenza N4 cross-day backend logic; Fase A include solo state-02 single fresh resume)
- Telemetria backend logging arricchito
- Performance benchmark > target SMART (annotato come `#ANOMALY`)
- Production-ready cost cap LLM (cost runaway non bloccante per 1-sessione runthrough)

### 1.4 Architettura pipeline 4-stadi

```
┌─────────────────────────────────────────────────────────────────────────┐
│ STADIO 1 — Locale endpoint smoke (auto-pass)                            │
│   Cmd: make demo-smoke-local                                            │
│   Test: curl assertion 7 endpoints /api/v1/* return 2xx                 │
│   Exit: ALL 7 endpoints OK + DB schema migrato + KB indicizzato         │
└────────────────────────────┬────────────────────────────────────────────┘
                             │ pass → next
┌────────────────────────────▼────────────────────────────────────────────┐
│ STADIO 2 — Locale browser flow (Playwright headless + Aaron-on-Chrome)  │
│   Auto: pnpm test:e2e --grep @demo-runthrough (Playwright headless)     │
│   Manual: Aaron repeats flow on Chrome localhost:3000                   │
│   Exit: caso d'uso [1]-[6] completa <10min + 0 #BLOCKER                 │
└────────────────────────────┬────────────────────────────────────────────┘
                             │ pass → next
┌────────────────────────────▼────────────────────────────────────────────┐
│ STADIO 3 — Staging browser flow (auto-smoke + manual)                   │
│   Auto: make demo-smoke-staging (curl + Playwright contro meepleai.app) │
│   Manual: Aaron repeats flow on Chrome desktop staging URL              │
│   Exit: stesso flow + zero diff vs locale + 0 #BLOCKER                  │
└────────────────────────────┬────────────────────────────────────────────┘
                             │ pass → next
┌────────────────────────────▼────────────────────────────────────────────┐
│ STADIO 4 — Smartphone Android Chrome (manual only)                      │
│   Device: Android ≥ 11, Chrome ≥ 120 (secondary device)                 │
│   Test: Aaron repeats flow on smartphone (camera live + responsive)     │
│   Exit: flow [1]-[6] completes <15min + camera permission OK            │
│         + 1 screenshot finale traduzione + 0 #BLOCKER                   │
└────────────────────────────┬────────────────────────────────────────────┘
                             │ pass = DEMO READY
                             ▼
                  G5 demo-readiness gate
                  evidence-bundle persisted
                  go/no-go fase B unblocked
```

### 1.5 Pre-requisiti (verifica pre-T-13d)

| Pre-req | Coverage | Verifica |
|---|---|---|
| Account `badsworm@gmail.com` Admin | `make seed-nanolith-demo[-staging]` | Login locale + staging |
| Gioco "Nanolith" Published | `make seed-nanolith-demo[-staging]` | `/library` mostra Nanolith |
| 2 PDFs indicizzati Ready | `make seed-nanolith-demo[-staging]` | KB queryable |
| Agente "Nanolith Tutor" | `make seed-nanolith-demo[-staging]` | Chat agente accessibile |
| PR #828 + #837 mergiati | review + merge entro T-10d | `git log main-dev` |
| R5 ResumeCard state-02 (single fresh) | implementation entro T-7d | mockup G state-02 wired (state-03/04 deferred Fase B per dipendenza N4 cross-day) |
| `data/rulebook/nanolith_datasource/*.pdf` | repo data | `ls` check |
| SSH key `~/.ssh/meepleai-staging` (per stadio 3) | manual setup | `make tunnel` works |
| Android device + Chrome ≥ 120 | secondary device acquisition | physical |
| Pagina fisica Storybook Nanolith | manual prep | physical |

> **NB role mismatch**: lo script `seed-nanolith-demo.sh` crea `badsworm@gmail.com` come **Admin** (NOT SuperAdmin). La nota nello script dichiara: «SuperAdmin role is seed-immutable. badsworm is created as Admin which is sufficient for the Iter 1 demo (full admin endpoints accessible)». Per Fase A questa è accettabile — Aaron riceve permission Admin ample per invocare ogni endpoint del caso d'uso. Se SuperAdmin role richiesto in futuro, configurare `INITIAL_ADMIN_EMAIL` in `infra/secrets/admin.secret` PRIMA della prima migration + re-bootstrap.

### 1.6 Strategia merge timeline (Q6 D — ibrida)

```
T-13d  spec approved → writing-plans skill → kickoff
T-12d → T-10d  review + merge PR #828 + #837 (primary path A: main-dev)
T-10d  🚦 go/no-go: PRs merged?
       ├─ YES → continue main-dev path
       └─ NO  → fallback: create demo/nanolith-runthrough-phase-a
                 cherry-pick squash di PR #828 + #837
                 deploy staging via override
T-9d → T-7d  R5 Resume card UX implementation (mockup G state-02/03/04)
T-6d   make seed-nanolith-demo-staging re-run + verify
T-3d   🚦 fallback escalation: se ancora NO main-dev → confirm demo-branch live
T-2d   STADIO 1 (locale endpoint smoke) green
T-1d   STADIO 2 + STADIO 3 (locale + staging browser) green
T-0d   STADIO 4 (smartphone) green + acceptance evidence (target 2026-05-22)
```

---

## 2. SMART Goals G1-G5

### 🎯 G1 — Stadio 1: Locale endpoint smoke

| SMART | Valore |
|---|---|
| **S — Specific** | Validare che 7 endpoint `/api/v1/*` necessari al caso d'uso [1]-[6] sono raggiungibili e returnano 2xx contro stack locale completo (`make dev`), con schema DB migrato e KB Nanolith Ready |
| **M — Measurable** | `make demo-smoke-local` exit code 0 → 7/7 endpoint OK + 0 errori CI smoke. Endpoint set: `auth/login`, `library/me`, `games/{id}`, `gamebook/campaigns` (POST), `agents/chat-stream` (POST + SSE), `gamebook/campaigns/{id}/photos` (POST), `gamebook/campaigns/{id}/photos/{pid}/translate` (POST + SSE) |
| **A — Achievable** | Esistente: `make seed-nanolith-demo` + endpoint da Iter 1.A/1.B mergiati. Nuovo: `make demo-smoke-local` target Makefile + `infra/scripts/demo-smoke-local.sh` (~50 LOC bash + curl + jq) |
| **R — Relevant** | Pre-condition tecnica: senza endpoint OK, manual UX testing è inutile. Cattura regression backend pre-Stadio 2 |
| **T — Time-bound** | **T-2d** (2026-05-20) green |

**Dipendenze**: pre-req §1.5 OK + PR #828 + #837 merged (o demo-branch baked).

---

### 🎯 G2 — Stadio 2: Locale browser flow (Chrome)

| SMART | Valore |
|---|---|
| **S — Specific** | Aaron esegue caso d'uso [1]-[6] su Chrome desktop localhost:3000 con stack locale, completando flow login → collection → game → CTA libro game → setup chat → photo translate. Auto-smoke parallelo via Playwright headless `@demo-runthrough` |
| **M — Measurable** | Manual: flow completes in **< 10 min** wall-clock + **0 `#BLOCKER`** in `runthrough-anomalies.md` + traduzione finale visualmente leggibile + 6/6 step utente completati. Auto Playwright: `pnpm test:e2e --grep @demo-runthrough` exit code 0 |
| **A — Achievable** | Esistente: 16 v2 components + chat shell (PR #828) + photo translate (PR #837) + `seed-nanolith-demo` data. Nuovo: 1 spec Playwright `apps/web/e2e/demo-runthrough/nanolith-flow.spec.ts` (~100 LOC, tag `@demo-runthrough`) |
| **R — Relevant** | Validation che FE è correttamente wired al BE (auto smoke) + Aaron acquisisce familiarità col flow pre-staging (manual) |
| **T — Time-bound** | **T-1d** (2026-05-21) green |

**Dipendenze**: G1 pass + browser Chrome ≥ 120 desktop.

---

### 🎯 G3 — Stadio 3: Staging browser flow

| SMART | Valore |
|---|---|
| **S — Specific** | Aaron esegue stesso caso d'uso [1]-[6] su Chrome desktop contro `https://meepleai.app` (deploy staging) con account Aaron staging-seeded. Auto-smoke `make demo-smoke-staging` + Playwright headless contro staging URL |
| **M — Measurable** | Manual: flow completes **< 12 min** (tolleranza network latency vs locale) + **0 `#BLOCKER`** + **0 diff funzionali vs G2** (stesso outcome ogni step). Auto: smoke staging exit 0 |
| **A — Achievable** | Esistente: deploy staging pipeline + `make seed-nanolith-demo-staging` + tunnel SSH + Playwright config staging-target. Nuovo: 1 target Makefile `demo-smoke-staging` (riuso script `demo-smoke-local.sh` con env override) |
| **R — Relevant** | Validation deploy: rileva discrepanze locale-vs-staging (env vars, secrets, network policy CF Access, image pulls) prima di puntare su mobile (più costoso debug) |
| **T — Time-bound** | **T-1d** (2026-05-21) green |

**Dipendenze**: G2 pass + PR #828/#837 deployed in staging (T-10d) + SSH tunnel + secrets sync.

---

### 🎯 G4 — Stadio 4: Smartphone Android Chrome

| SMART | Valore |
|---|---|
| **S — Specific** | Aaron esegue caso d'uso [1]-[6] su smartphone Android (≥ Android 11) con Chrome (≥ 120) contro staging URL, includendo: camera permission grant, scatto foto live di 1 pagina Storybook fisico, upload, segmentation, translate SSE rendered IT. Fallback file picker da galleria se camera denial |
| **M — Measurable** | Flow completes **< 15 min** + **0 `#BLOCKER`** + camera permission grant prompt visualizzato + foto upload completes < 30s + traduzione SSE first-token < 8s + traduzione finale leggibile in viewport mobile (no horizontal scroll testo) + 1 screenshot mobile della traduzione finale (PNG ≤ 2MB) |
| **A — Achievable** | Esistente: layout responsive v2 + `CameraViewfinder` component + `<input type=file capture=environment>` shipped Iter 1.B. Nuovo: 1 protocollo manual test in spec (no code) |
| **R — Relevant** | Validation mobile UX critical per fase B dogfood (Aaron usa mobile al tavolo). Cattura issue specifici mobile (camera permission policy Chrome Android, upload progress UX, layout viewport) |
| **T — Time-bound** | **T-0d** (2026-05-22) green |

**Dipendenze**: G3 pass + Android device disponibile + 1 pagina fisica Storybook Nanolith reperibile.

---

### 🎯 G5 — Demo-readiness gate (aggregator)

| SMART | Valore |
|---|---|
| **S — Specific** | Sintesi binaria "demo Fase A passed" basata su G1∧G2∧G3∧G4 + zero blocker + evidence bundle persisted, abilitando go/no-go pianificazione Fase B (dogfood Aaron+amici) |
| **M — Measurable** | **TUTTI**: G1 pass ∧ G2 pass ∧ G3 pass ∧ G4 pass ∧ `count("#BLOCKER" tag in runthrough-anomalies.md) == 0` ∧ evidence-bundle persisted (`docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results.md` con: 1 screenshot mobile + 5-10 righe note + lista anomalies con tag) |
| **A — Achievable** | Documentation work only, no code. Aaron compila `runthrough-anomalies.md` durante pipeline (live), aggrega in `*-results.md` post-G4 |
| **R — Relevant** | Trigger formale per planning Fase B + record artifact per release notes Iter 1 + decision input per follow-up R1-R10 prioritization |
| **T — Time-bound** | **T-0d EOD** (2026-05-22) — 1 ora post-G4 per evidence bundling |

**Dipendenze**: G1-G4 all pass + Aaron disponibile 1h post-G4 per write-up.

---

### 2.6 Mappatura SMART goals → caso d'uso utente [1]-[6]

| Step caso d'uso | G1 (locale endpoint) | G2 (locale browser) | G3 (staging browser) | G4 (smartphone) | G5 |
|---|:-:|:-:|:-:|:-:|:-:|
| [1] Login Aaron | ✓ smoke | ✓ manual | ✓ manual | ✓ manual | ⊕ |
| [2] Vede Nanolith collection | ✓ smoke | ✓ manual | ✓ manual | ✓ manual | ⊕ |
| [3] Seleziona gioco | — | ✓ manual | ✓ manual | ✓ manual | ⊕ |
| [4] CTA libro game | ✓ smoke | ✓ manual | ✓ manual | ✓ manual | ⊕ |
| [5] Setup chat agente | ✓ smoke (chat-stream) | ✓ manual + N1 | ✓ manual + N1 | ✓ manual + N1 | ⊕ |
| [6] Photo translate | ✓ smoke (upload + translate SSE) | ✓ manual + Playwright | ✓ manual | ✓ manual + camera live | ⊕ |

⊕ = aggregato in G5

---

## 3. Scenari Gherkin

Convenzione tag (estensione spec design 2026-05-07):

- `@happy` happy path | `@edge` edge case | `@error` failure mode
- `@dogfood` setup reale Aaron (badsworm@gmail.com + Nanolith fisico)
- `@stadio-1` … `@stadio-4` per scoping pipeline | `@phase-a` per scope (vs Fase B futura)

### 3.1 G1 — Locale endpoint smoke

#### G1.1 — Happy path: 7 endpoint OK @happy @stadio-1 @phase-a

```gherkin
Given lo stack locale è up via `make dev`
And `make seed-nanolith-demo` è stato eseguito senza errori
And il KB Nanolith è in stato "Ready" (entrambi PDF Press Start + Rules indicizzati)
When eseguo `make demo-smoke-local`
Then lo script restituisce exit code 0 entro 60 sec
And per ogni endpoint del set:
  | endpoint                                                          | metodo | expected |
  | /api/v1/auth/login                                                | POST   | 200      |
  | /api/v1/library/me                                                | GET    | 200      |
  | /api/v1/games/{nanolithId}                                        | GET    | 200      |
  | /api/v1/gamebook/campaigns                                        | POST   | 201      |
  | /api/v1/agents/chat-stream                                        | POST   | 200 SSE  |
  | /api/v1/gamebook/campaigns/{id}/photos                            | POST   | 201      |
  | /api/v1/gamebook/campaigns/{id}/photos/{pid}/translate            | POST   | 200 SSE  |
  la response code è quello atteso
And gli SSE endpoint emettono almeno 1 evento `data:` < 8s
And lo script logga tabella riassuntiva 7/7 OK
```

#### G1.2 — Endpoint photo POST fallisce 5xx @error @stadio-1

```gherkin
Given lo stack locale è up
But il servizio embedding-service è down (container stopped)
When eseguo `make demo-smoke-local`
Then lo script restituisce exit code != 0
And il log indica il primo endpoint failing con status code + body
And classifico l'errore come #BLOCKER in `runthrough-anomalies.md`
And applico fix (restart container) prima di re-run
```

#### G1.3 — Schema DB non migrato @error @stadio-1

```gherkin
Given lo stack locale è appena startato
But le migration EF non sono state applicate
When eseguo `make demo-smoke-local`
Then `/api/v1/gamebook/campaigns` POST restituisce 500 con body contenente "GamebookCampaignSession table missing"
And lo script fail con exit code 2
And classifico come #BLOCKER e applico `dotnet ef database update` prima di re-run
```

### 3.2 G2 — Locale browser flow

#### G2.1 — Caso d'uso [1]-[6] happy path Chrome locale @happy @stadio-2 @phase-a @dogfood

```gherkin
Given G1 è passato (7/7 endpoint OK)
And ho aperto Chrome ≥ 120 desktop su localhost:3000
And `runthrough-anomalies.md` è creato con header timestamp inizio
When effettuo login con badsworm@gmail.com / TestNanolith2026!
Then sono redirected a `/library` entro 3s
When la pagina `/library` è caricata
Then vedo la card "Nanolith" tra i giochi della collezione
When clicco la card "Nanolith"
Then sono navigato a `/library/games/{nanolithId}` entro 2s
When clicco il CTA "Avvia partita libro game" (NanolithCampaignCTA)
Then vedo la chat shell con agente "Nanolith Tutor" (path /library/games/{id}/play/{campaignId})
When chiedo all'agente "come si imposta la partita per 4 giocatori?"
Then ricevo risposta SSE con citation Press Start.pdf entro 8s
And la risposta è actionable (definizione 0.1 dello spec design 2026-05-07)
When clicco il CTA "Carica foto pagina"
Then vedo `CameraViewfinder` o file picker
When carico una foto pagina A4 dello Storybook Nanolith
Then vedo `PageThumb` + segmentation in 3 paragrafi entro 6s
And ogni segment ha `ConfidenceBadge`
When seleziono il primo paragrafo e clicco "Traduci"
Then ricevo traduzione IT in streaming SSE entro 8s first-token
And la traduzione contiene almeno 1 citation `(Storybook, p. N)`
And l'intero flow [1]-[6] è completato in **< 10 min**
And `count("#BLOCKER")` in runthrough-anomalies.md == 0
```

#### G2.2 — Auto Playwright @demo-runthrough headless @happy @stadio-2

```gherkin
Given G1 è passato
And il file `apps/web/e2e/demo-runthrough/nanolith-flow.spec.ts` esiste
When eseguo `pnpm test:e2e --grep @demo-runthrough` con `PLAYWRIGHT_AUTH_BYPASS=true`
Then il test exit code 0
And il log Playwright mostra 6/6 step completati
And il test seedAuthSession + seedCookieConsent + mockAuthEndpoints triple-helper viene applicato
```

#### G2.3 — SSE translate timeout > 30s @error @stadio-2

```gherkin
Given mi trovo allo step [6] del caso d'uso (photo translate)
And il backend LLM provider è in rate-limit
When clicco "Traduci" sul primo paragrafo
And la connessione SSE non emette eventi `data:` per > 30s
Then la UI mostra messaggio "Traduzione in corso, attendi..." per 30s
And dopo 30s mostra "Errore: timeout. Retry?"
When clicco "Retry"
And il retry fallisce con stesso sintomo
Then classifico come #BLOCKER (reproducible 2× consecutivi)
And ferma stadio 2, applica fix backend prima di re-run
But se il primo retry succede, classifico come #ANOMALY (latenza > target)
```

#### G2.4 — Login credenziali errate @edge @stadio-2

```gherkin
Given mi trovo a `/login` su Chrome locale
When inserisco password sbagliata "WrongPwd123!"
Then ricevo errore "Credenziali non valide" entro 2s
And NON sono redirected (rimango su /login)
And classifico come #OBSERVATION (NOT blocker — comportamento atteso)
And riprovo con password corretta TestNanolith2026!
And il flow continua dallo step [2]
```

### 3.3 G3 — Staging browser flow

#### G3.1 — Caso d'uso [1]-[6] staging happy path @happy @stadio-3 @phase-a @dogfood

```gherkin
Given G2 è passato (locale browser flow OK)
And `make seed-nanolith-demo-staging` è stato eseguito (richiede SSH tunnel)
And ho aperto Chrome ≥ 120 desktop su https://meepleai.app
When ripeto identica sequenza di azioni di G2.1
Then ogni step produce stesso outcome funzionale di G2.1
And il flow [1]-[6] è completato in **< 12 min** (tolleranza +20% network latency)
And la traduzione finale è semanticamente equivalente a quella vista in G2.1
And `count("#BLOCKER")` rimane 0
And eventuali differenze (es. tempi più lenti) sono classificate #OBSERVATION
```

#### G3.2 — Diff funzionale staging vs locale @error @stadio-3

```gherkin
Given G2 è passato
And sto eseguendo G3.1 contro staging
When su staging il CTA "Avvia partita libro game" non è visibile su `/library/games/{nanolithId}`
But su locale è visibile
Then registro #BLOCKER "diff staging vs locale: NanolithCampaignCTA assente staging"
And investigo:
  - PR #828 mergiato su main-dev?
  - deploy staging include il commit del CTA?
  - feature flag NEXT_PUBLIC_* differisce?
And NON proseguo a G4 finché diff è risolto
```

#### G3.3 — Cloudflare Access tunnel error @error @stadio-3

```gherkin
Given sto eseguendo `make demo-smoke-staging`
But la SSH tunnel `make tunnel` non è attiva
When lo script tenta `curl https://meepleai.app/api/v1/auth/login`
Then la connessione fallisce con "CF Access blocked"
And lo script fail con exit code != 0 + log "tunnel not active, run `make tunnel` first"
And classifico come #BLOCKER → fix tunnel → re-run
```

### 3.4 G4 — Smartphone Android Chrome

#### G4.1 — Caso d'uso [1]-[6] mobile Android happy @happy @stadio-4 @phase-a @dogfood

```gherkin
Given G3 è passato (staging browser flow OK)
And ho un device Android ≥ 11 con Chrome ≥ 120 connesso a internet
And ho 1 pagina fisica Storybook Nanolith pronta per fotografare
And `runthrough-anomalies.md` ha sezione `## Stadio 4 (smartphone)` aperta
When apro Chrome Android e navigo https://meepleai.app
And login con badsworm@gmail.com / TestNanolith2026!
Then `/library` carica < 5s con layout responsive (no horizontal scroll)
When tocco la card "Nanolith"
Then `/library/games/{id}` carica con CTA "Avvia partita libro game" visibile in viewport mobile
When tocco il CTA libro game
Then la chat shell carica con tastiera mobile inputtable
When chiedo "come si imposta la partita per 4 giocatori?"
Then ricevo risposta SSE leggibile in viewport mobile (font size > 14px, no clip)
When tocco "Carica foto pagina"
Then Chrome richiede camera permission nativa via prompt
When concedo permission
Then `CameraViewfinder` apre live camera back-facing
When fotografo la pagina Storybook
Then la foto è uploaded e segmentation parte
And vedo `PageThumb` rendered correttamente in mobile
When seleziono il primo paragrafo e tocco "Traduci"
Then ricevo SSE first-token < 10s (tolleranza mobile network)
And la traduzione IT è renderizzata leggibile (no horizontal scroll testo)
And catturo screenshot Android della traduzione finale (PNG ≤ 2MB)
And il flow completes in **< 15 min** total
And `count("#BLOCKER")` rimane 0
```

#### G4.2 — Camera permission denied + fallback file picker @edge @stadio-4

```gherkin
Given mi trovo allo step [6] del caso d'uso su mobile
When Chrome Android prompt camera permission
And nego il permission ("Block")
Then `CameraViewfinder` mostra messaggio "Camera negata. Carica da galleria"
And vedo CTA fallback "Scegli da galleria"
When tocco "Scegli da galleria"
Then si apre file picker Android nativo
When seleziono una foto pre-esistente (storybook page screenshot)
Then la foto è uploaded come in G4.1 e proseguo flow
And classifico come #OBSERVATION (fallback funziona) NOT #BLOCKER
```

#### G4.3 — Mobile viewport translate render @happy @stadio-4

```gherkin
Given ho ricevuto traduzione SSE su mobile
When la traduzione finale è renderizzata in viewport
Then nessun testo va in overflow horizontal (no scroll necessario per leggere)
And `font-size` ≥ 14px nel computed style del paragrafo
And `line-height` ≥ 1.5 (leggibilità)
And il citation `(Storybook, p. N)` è cliccabile (tap target ≥ 44px touch friendly)
And catturo screenshot in formato JPEG portrait Android (1080×2400 o simile)
And rinomino screenshot `runthrough-mobile-final-translation.png` salvato in `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results/`
```

### 3.5 G5 — Demo-readiness gate

#### G5.1 — All G pass + 0 blocker → demo passed @happy @phase-a

```gherkin
Given G1 stadio 1 ha exit 0
And G2 stadio 2 manual + Playwright auto pass
And G3 stadio 3 staging ha 0 diff funzionali vs locale
And G4 stadio 4 smartphone è completato
When apro `runthrough-anomalies.md` e conto i tag
Then `count("#BLOCKER")` == 0
And esiste 1 screenshot mobile in `*-results/`
And esiste file `2026-05-08-nanolith-demo-runthrough-results.md` con:
  - timestamp inizio + fine pipeline
  - sintesi 5-10 righe
  - lista #ANOMALY con descrizione (se any)
  - lista #OBSERVATION con descrizione (se any)
  - go/no-go decision: "PASSED — Fase B unblocked"
And committo evidence bundle in branch chore/demo-runthrough-evidence
And apro PR per archiviazione results in main-dev
```

#### G5.2 — Almeno 1 #BLOCKER → demo failed @error @phase-a

```gherkin
Given uno o più stadi G1-G4 hanno prodotto #BLOCKER
When eseguo aggregazione finale runthrough-anomalies.md
Then `count("#BLOCKER") >= 1`
And il file results.md contiene:
  - go/no-go decision: "FAILED"
  - root cause analysis per ogni blocker
  - action items con owner e ETA fix
And NON apro PR Fase B planning
And triggera follow-up issue GitHub per ogni blocker
And re-run intera pipeline da G1 dopo fix tutti #BLOCKER
```

#### G5.3 — Anomalies non-blocking annotated @edge @phase-a

```gherkin
Given alcuni stadi hanno generato #ANOMALY (es. latenza > target ma flow completes)
When raccolgo evidence in results.md
Then la sezione "## Anomalies (non-blocking)" elenca ogni anomaly:
  - timestamp + stadio + sintomo + workaround applicato
And la sezione "## Recommendations for Phase B" propone fix prioritizzati
And demo PASSED nonostante anomalies (non-blocking by definition Q9 D)
```

---

## 4. Timeline + Checkpoint + Scope-protection

### 4.1 Timeline dettagliata (target T-0d = 2026-05-22)

| Day | Milestone | Owner | Verifica |
|---|---|---|---|
| **T-13d** (2026-05-09) | Spec approved → invocazione `superpowers:writing-plans` skill → kickoff plan | Aaron | spec MD committed in main-dev + plan MD scaffolded |
| **T-12d → T-10d** (2026-05-10/12) | Review + merge PR #828 (Iter 1.A) + PR #837 (Iter 1.B) on main-dev | reviewer + Aaron | `git log main-dev` mostra entrambi merge commits |
| **🚦 T-10d checkpoint** (2026-05-12 EOD) | **Go/no-go A primary vs B fallback** | Aaron | decision logged in `runthrough-anomalies.md` §0 |
| → **YES merged** | continua main-dev path | — | — |
| → **NO merged** | crea `demo/nanolith-runthrough-phase-a` (cherry-pick squash di entrambi PR), deploy staging via override | Aaron | branch baked + staging URL serves new code |
| **T-9d → T-7d** (2026-05-13/15) | R5 Resume card UX implementation (mockup G state-02/03/04) | Aaron | 4 sub-component wired + Playwright spec @resume-states |
| **T-6d** (2026-05-16) | `make seed-nanolith-demo-staging` re-run + verifica `Nanolith Tutor` agent + 2 KB Ready | Aaron | seed-results.csv exit 0 |
| **🚦 T-3d checkpoint** (2026-05-19) | **Fallback escalation**: se ancora NO main-dev path → confirm demo-branch live in staging | Aaron | URL staging serve commit cherry-pick |
| **T-2d** (2026-05-20) | **G1 stadio 1**: `make demo-smoke-local` exit 0 (7/7 endpoint OK) | Aaron | log smoke green |
| **T-1d** (2026-05-21 morning) | **G2 stadio 2**: locale browser + auto Playwright @demo-runthrough | Aaron | 0 #BLOCKER + Playwright green |
| **T-1d** (2026-05-21 afternoon) | **G3 stadio 3**: staging browser + `make demo-smoke-staging` | Aaron | 0 #BLOCKER + 0 diff vs locale |
| **T-0d** (2026-05-22 morning) | **G4 stadio 4**: smartphone Android Chrome flow [1]-[6] + screenshot | Aaron | 0 #BLOCKER + evidence PNG salvato |
| **T-0d** (2026-05-22 EOD) | **G5 demo-readiness gate**: aggregator + results.md + PR archiviazione | Aaron | results.md committed + PR opened |

### 4.2 Scope-protection (anti-creep)

> **Q10 C scelta** = 14gg target. Rischio scope creep esplicito accettato. Mitigation rules:

| Rule | Enforcement |
|---|---|
| **R-AC1** Nessun nuovo feature outside §1.3 può essere aggiunto a Fase A senza pull request modifica spec + re-approval | Spec self-review post-modifica |
| **R-AC2** R1, R2, R3, R4, R6, R7 (gap items audit) sono **deferred a follow-up post-Fase A**, NON parte di pipeline | Decision Log §5.1 |
| **R-AC3** N4 (resume cross-day) e Encounter Book UX sono Fase B / Iter 2, NON parte di Gherkin Fase A | Gherkin scenari hanno tag `@phase-a` esplicito |
| **R-AC4** iOS Safari testing è **deferred a Fase B** (Q7 B scelta) | G4 quantifica solo Android; nessuna spec iOS |
| **R-AC5** Telemetria + dashboard + logging arricchito sono Fase B | G5 evidence è lightweight (Q8 D) |
| **R-AC6** Se durante T-9d → T-7d (R5 implementation) emerge bug fuori spec, va fixato solo se **#BLOCKER** per stadi G2-G4. Bugfix non-blocking → Fase B follow-up | runthrough-anomalies.md tag |
| **R-AC7** Refactoring opportunistico durante review PR #828/#837 NON è permesso. Se reviewer trova issue strutturale → file follow-up issue, NON modifica PR | Code review process |

### 4.3 Risk register

| ID | Risk | Likelihood | Impact | Mitigation | Owner |
|---|---|---|---|---|---|
| **RR1** | PR #828 review tarda > T-10d | Media | Alto (fallback B necessario) | T-10d checkpoint formal go/no-go | Aaron |
| **RR2** | PR #837 review tarda > T-10d | Media | Alto | come RR1 | Aaron |
| **RR3** | Diff staging vs locale (G3.2) per CF Access / secrets / env | Media | Medio (1-2gg fix) | `make secrets-sync` pre-T-3d + smoke staging T-2d | Aaron |
| **RR4** | Camera permission Chrome Android edge (G4.2) | Bassa | Basso (fallback file picker copre) | G4.2 scenario testato | Aaron |
| **RR5** | R5 Resume card implementation > 2gg | Bassa | Medio (T-7d slip → T-5d) | scope-cap 4 sub-component, mockup G already designed | Aaron |
| **RR6** | SSE timeout staging > locale per network/CF Access (G3) | Media | Medio (#ANOMALY non #BLOCKER se 1× retry succeeds) | G2.3 retry logic | Aaron |
| **RR7** | Smartphone Android device disponibilità T-0d | Bassa | Alto (G4 unblocked) | acquisition device entro T-3d (alternativa: prestito amico/dispositivo lab) | Aaron |
| **RR8** | Storybook fisico pagina disponibilità per G4 (foto live) | Bassa | Medio (G4.1 happy bloccato, fallback G4.2 file picker da scan PDF) | preparare 1 pagina fisica + 1 screenshot fallback entro T-3d | Aaron |

---

## 5. Decision Log + Out of Scope + Open Questions

### 5.1 Decision Log (Q1-Q10)

| ID | Q | Decisione | Razionale | Trade-off accettato |
|---|---|---|---|---|
| **D1** | Q1 | Spec separato Demo Runthrough Phase A (vs revisione spec design 2026-05-07) | Spec design già `reviewed`, tocchi invalidano expert-panel | 2 doc da mantenere; orchestrator pattern |
| **D2** | Q2 | Pipeline staged: Aaron solo prima, Aaron+amici dopo | Validation ≠ dogfood, separare "code works" da "code is delightful" | Fase B planning richiede separato spec |
| **D3** | Q3 | Spec stretto solo Fase A, Fase B = nuovo spec | No placeholder TBD anti-pattern | Risk: discontinuità Fase B pianificazione |
| **D4** | Q4 | MVD + UX completeness (N2 in-game + R5 ResumeCard state-02 only) | Caso d'uso utente è 6 step linear; UX completa anticipa Fase B. R5 state-03/04 deferred per dipendenza N4 cross-day backend non implementato in Iter 1 | +2 giorni effort vs MVD only; ridotto da +3 giorni iniziali (state-03/04 deferred) |
| **D5** | Q5 | Sequential auto-pass smoke + manual deep | Smartphone non automatabile facilmente; smoke ripetibile per release future | +1gg effort scrittura script smoke |
| **D6** | Q6 | Ibrida A primary + B fallback | Demo timeline disaccoppiata da merge timeline; no fake demo | +complessità: T-10d checkpoint formale |
| **D7** | Q7 | Android Chrome only Fase A | Cross-platform iOS deferred Fase B con device amici | Gap iOS unknown finché Fase B |
| **D8** | Q8 | Lightweight evidence (1 PNG + MD note) | Fase A è validation, evidence ricca per Fase B | Audit trail leggero post-pipeline |
| **D9** | Q9 | Document-then-decide con tag `#BLOCKER`/`#ANOMALY`/`#OBSERVATION` | Q2 binario olistico + Q9 soggettivo mid-pipeline coesistono | Aaron è solo decision-maker, soggettività controllata da tag classification |
| **D10** | Q10 | 14gg target 2026-05-22 | Buffer abbondante, scope-cap originale Iter 1 | Risk scope creep mitigato da R-AC1-R-AC7 |

### 5.2 Open Questions Fase B (carry-forward)

- **Q-PB-1** Quale subset di N4 (resume cross-day) è MVP per Fase B? Solo "auto-save" o anche "resume card UX completo R5 state-02/03/04"?
- **Q-PB-2** iOS Safari coverage Fase B: tutti gli amici hanno Android? Se mix iOS/Android, quale device Aaron porta?
- **Q-PB-3** Telemetria post-sessione Fase B: come si raccoglie il Google Sheet `nanolith-dogfood-eval.gsheet`? Manualmente Aaron + amici? Automatizzato?
- **Q-PB-4** Encounter Book Fase B: stesso flusso N3 photo come Storybook, o UX cheatsheet card?
- **Q-PB-5** Glossary editor Fase B: pill modal inline (mockup H Tracciato A) o standalone screen (Tracciato B)?
- **Q-PB-6** Cost cap LLM Fase B: target € budget per sessione 4h dogfood?
- **Q-PB-7** Recovery flow se SSE timeout ripetuto durante dogfood: Aaron continua su PDF cartaceo (graceful degradation) o stop sessione?

---

## 6. Riferimenti

- **Spec design Iter 1**: `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md`
- **Audit cross-branch**: `docs/superpowers/specs/2026-05-08-libro-game-iter1-cross-branch-audit.md`
- **Vision doc**: `docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md`
- **Plan Tracciato A**: `docs/superpowers/plans/2026-05-07-libro-game-nanolith-iter1-plan.md`
- **Plan Tracciato B 1a**: `docs/superpowers/plans/2026-05-07-libro-game-nanolith-iter-1a.md`
- **Plan Tracciato B 1b**: `docs/superpowers/plans/2026-05-07-libro-game-nanolith-iter-1b.md`
- **PR #828**: `feat(gamebook): Iter 1.A — campaign sessions + chat shell`
- **PR #837**: `feat(gamebook): Iter 1.B — photo translate + glossary + history`
- **Seed automation**: `infra/scripts/seed-nanolith-demo.sh` + `make seed-nanolith-demo[-staging]`
- **Brief SP5 KB upload**: `admin-mockups/briefs/SP5-admin-tools.md`
