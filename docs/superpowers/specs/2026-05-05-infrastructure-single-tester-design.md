# Infrastructure for Single-Tester Alpha — Design Spec

> **Created**: 2026-05-05
> **Author**: DegrassiAaron + spec panel (Wiegers, Hightower, Nygard, Newman, Fowler, Hohpe, Crispin, Adzic)
> **Status**: Approved → Implementation
> **Related**: `docs/operations/operations-manual.md`, `docs/operations/disaster-recovery-runbook.md`, `docs/operations/ci-cd-pipeline.md`

## 1. Context

MeepleAI è in fase **alpha personale a tempo indeterminato**: l'unico tester è il proprietario del progetto. L'infrastruttura attuale (21 servizi Docker, 24 secret files, 3 ambienti, 3 pipeline CI gated, monitoring stack completo, Traefik+Let's Encrypt) è dimensionata per "mid-stage production" e introduce **complessità accidentale** sproporzionata al caso d'uso reale.

Vincoli rigidi:
- **Hardware**: Hetzner CAX21 ARM64 (4 vCPU, **8 GB RAM**, **75 GB disk**) — no upgrade nel medio termine.
- **Workflow primario**: hybrid `make integration` (frontend + backend locali, postgres+redis+AI services remoti via SSH tunnel).
- **Budget operativo**: ~EUR 14/mese (CAX21) + costi LLM (DeepSeek/OpenRouter, attualmente non monitorati).

Questo design risponde alla domanda: *"qual è il minimo di infrastruttura necessario perché il proprietario possa testare end-to-end l'app, in modo deterministico e a basso attrito, senza investire in scalabilità o multi-tenancy che non gli servono?"*

## 2. Decisioni Socratiche

| ID | Domanda | Decisione | Rationale |
|----|---------|-----------|-----------|
| Q1 | Quale set "felice di rilasciare"? | **9 scenari**: A1-A5 (Login, BGG add, PDF upload, Chat citations, Logout/re-login) + C1, C2, C3, C5 (deploy, migration, backup verify, RAG <10s) | Copre full funnel utente + invarianti operazionali |
| Q2 | Orizzonte single→multi user? | **Alpha personale indefinita** | Massima semplificazione, taglio aggressivo |
| Q3 | Edge architecture? | **Cloudflare Tunnel** (rimuovi Traefik+LE) | Elimina cert renewal + port collision + label drift + socket exposure |
| Q4 | Strategia orchestration-service? | **Profilo `tutor-agents`, default OFF** in staging | Recupera ~2GB RAM 95% del tempo, on-demand quando serve testare Tutor/Arbitro |
| Q5 | DR confidence threshold? | **A** (quarterly walkthrough) **+ C** (monthly auto-restore + smoke read-back) | B (full live drill) overkill per single user; mantiene confidence senza costi VPS aggiuntivi |

## 3. User Goals SMART

### G1 — Smoke set affidabile end-to-end

**Specific**: I 9 scenari smoke (A1-A5 + C1+C2+C3+C5) passano sia su `dev` (locale o tramite `make work`) sia su `staging` (`meepleai.app`).

**Measurable**: 9/9 pass, esecuzione sequenziale ≤15 min totali, RAG query ≤10s end-to-end.

**Achievable**: Smoke automation parziale già esiste (`smoke_test()` nel deploy workflow); estendere copertura.

**Relevant**: Definisce direttamente "felice di rilasciare" (Crispin); ogni regressione qui è bloccante.

**Time-bound**: Operativo a ogni deploy + a ogni inizio sessione di test (manuale).

#### Scenari Gherkin

```gherkin
Feature: Smoke Set "Felice di rilasciare"

Background:
  Given staging.meepleai.app è raggiungibile
  And i secret files sono presenti in /opt/meepleai/repo/infra/secrets/

Scenario: A1 — Login → Library mostra giochi
  Given un utente registrato esiste su staging
  When eseguo POST /api/v1/auth/login con credenziali valide
  Then ricevo 200 con session cookie SameSite=Lax Secure
  And GET /api/v1/library/me ritorna 200 con almeno 1 game entry
  And la pagina /library renderizza senza errori console

Scenario: A2 — Search BGG → Add to library
  Given sono autenticato come user
  When eseguo GET /api/v1/bgg/search?query=Catan
  Then ricevo 200 con almeno 1 risultato BGG
  When seleziono il primo risultato e POST /api/v1/library/me/games
  Then ricevo 201 con SharedGameId valorizzato
  And il game appare in /library entro 2s

Scenario: A3 — Upload PDF → KB indicizzato
  Given sono autenticato e il game "Catan" è nella mia library
  When carico catan-rules.pdf via POST /api/v1/games/{id}/pdfs
  Then ricevo 202 Accepted con upload_id
  And entro 5 minuti GET /api/v1/games/{id}/kb-status ritorna {status: "indexed"}
  And la dashboard admin /admin/knowledge-base mostra il game come "Indicizzato"

Scenario: A4 — Open game → Chat con citations
  Given il game "Catan" ha KB indicizzato
  When apro /chat e seleziono il game
  And invio "Quanti dadi servono per il primo turno?"
  Then entro 10s ricevo una risposta non vuota via SSE
  And la risposta contiene almeno 1 [citation:N] tag valido
  And ogni citation ha snippet + page_number + source_doc_id

Scenario: A5 — Logout → Re-login persistence
  Given sono autenticato con session attiva
  When eseguo POST /api/v1/auth/logout
  Then ricevo 200 e il cookie viene cleared
  When eseguo POST /api/v1/auth/login con stesse credenziali
  Then la library/RAG history precedente è ancora visibile

Scenario: C1 — Deploy senza rotture
  Given un commit valido su main-staging
  When workflow_run "Deploy to Staging" parte
  Then build → push GHCR → SSH pull → compose up termina senza errori
  And meepleai-api è Healthy entro 60s post-deploy
  And meepleai-web ritorna HTTP 200 su / entro 60s

Scenario: C2 — Migration applicata correttamente
  Given una nuova migration EF Core esiste in main-staging
  When il job migrate-db esegue
  Then dotnet ef migrations script --idempotent genera SQL > 10 righe
  And psql -v ON_ERROR_STOP=1 -f migrations.sql exit code 0
  And SELECT FROM __EFMigrationsHistory mostra la nuova migration in TOP 1

Scenario: C3 — Backup crea file valido + verify pass
  Given è passata mezzanotte UTC
  When il cron 03:00 esegue scripts/backup.sh
  Then /backups/meepleai/<TIMESTAMP>/postgres.sql.gz esiste e gzip -t passa
  And il file è > 1KB
  And il cron 03:30 esegue scripts/backup-verify.sh
  And exit code è 0 e tutti i check ✅ PASS

Scenario: C5 — RAG query end-to-end <10s
  Given il game "Catan" è indicizzato e DeepSeek API è raggiungibile
  When invio una query RAG via /chat
  Then time-to-first-token (TTFT) è <3s
  And time-to-completion è <10s
  And il counter Prometheus rag_query_duration_seconds_bucket{le="10"} incrementa
```

### G2 — Edge architecture su Cloudflare Tunnel

**Specific**: `meepleai.app` servito tramite `cloudflared` daemon su VPS, Traefik+Let's Encrypt rimossi dall'edge, porte 80/443 chiuse a internet pubblico, accesso staging limitato a CF Access (Google SSO, solo email proprietario).

**Measurable**: `nmap -p 80,443 204.168.135.69` ritorna `filtered/closed`, smoke set G1 continua a passare 9/9, downtime durante migrazione <30 min.

**Achievable**: setup `cloudflared` ~2h + reconfig service routing ~2h + test ~1h.

**Relevant**: Elimina 4 categorie failure mode (cert renewal, port collision, Traefik label drift, Docker socket exposure); semplifica secret management (~3 secrets in meno).

**Time-bound**: Completato entro **2026-06-30** (orizzonte 8 settimane).

#### Scenari Gherkin

```gherkin
Feature: Cloudflare Tunnel Edge

Scenario: Migration cutover completata
  Given Traefik è attualmente l'edge proxy su meepleai.app
  When eseguo il runbook docs/operations/cf-tunnel-migration.md
  Then cloudflared daemon è running su VPS (systemd service)
  And il DNS record meepleai.app punta a CF tunnel (CNAME tunnel-id.cfargotunnel.com)
  And Traefik container è stoppato e rimosso da compose.staging.yml
  And Let's Encrypt certs sono rimossi da volume traefik_certs
  And smoke set G1 (9/9) passa contro nuovo endpoint

Scenario: Porte 80/443 chiuse a internet pubblico
  Given migrazione completata
  When eseguo nmap -p 80,443,22 204.168.135.69 da host esterno
  Then porta 22 (SSH) appare "open" (mantenuta)
  And porte 80, 443 appaiono "filtered" o "closed"
  And ufw status mostra DENY su 80/tcp e 443/tcp

Scenario: CF Access limita meepleai.app a single user
  Given CF Access policy "owner-only" è configurata
  When un utente non autorizzato tenta di accedere a https://meepleai.app
  Then CF mostra Google SSO challenge
  And solo l'email del proprietario è accettata
  And l'utente non autorizzato vede "Access Denied" page
  When io accedo con la mia email
  Then accesso è consentito e la sessione MeepleAI parte normalmente

Scenario: Rollback path documentato e testato
  Given migrazione completata da almeno 24h
  When il proprietario decide di rollback
  Then il runbook docs/operations/cf-tunnel-rollback.md è eseguibile in <30 min
  And dopo rollback, Traefik è di nuovo edge e smoke set passa
```

### G3 — Resource footprint per single-tester

**Specific**: Profilo Docker Compose `single-tester` definito in `compose.staging.yml`; servizi disabilitati di default: `orchestration-service`, `n8n`, `seq`, `loki`, `fluent-bit`, `cadvisor`. `make staging-minimal` lancia solo postgres+redis+api+web+embedding+reranker+prometheus+grafana+alertmanager+node-exporter.

**Measurable**: `free -m` su VPS mostra ≥5GB available (oggi ~2GB), `docker stats` mostra <3GB total reservation, smoke set G1 passa 9/9.

**Achievable**: 80% del lavoro è solo aggiungere `profiles: [tutor-agents]` o `[full-monitoring]` ai servizi opzionali in compose files.

**Relevant**: Sblocca CI runner co-locato (no più OOM), previene disk pressure ricorrente.

**Time-bound**: Rilasciato in 1 PR entro **2026-05-19** (2 settimane).

#### Scenari Gherkin

```gherkin
Feature: Profilo single-tester

Scenario: Default deploy usa profilo single-tester
  Given un commit pulito su main-staging
  When deploy-staging.yml esegue
  Then docker compose up usa --profile minimal (no tutor-agents, no full-monitoring, no automation)
  And docker ps mostra esattamente 10 container: postgres, redis, api, web, embedding, reranker, prometheus, grafana, alertmanager, node-exporter
  And docker ps NON mostra: meepleai-orchestrator, meepleai-n8n, meepleai-seq, meepleai-loki, meepleai-cadvisor

Scenario: RAM available rispetta target
  Given staging gira con profilo single-tester
  When eseguo ssh deploy@204.168.135.69 'free -m'
  Then "available" è ≥5000 (5GB)
  And "used" è ≤3000 (3GB)

Scenario: Tutor agents on-demand activation
  Given staging gira con profilo single-tester (orchestration spento)
  When eseguo make staging-with-tutor sul server
  Then orchestration-service container parte e diventa healthy entro 60s
  And POST /api/v1/tutor/query ritorna 200 (era 503)
  When eseguo make staging-minimal di nuovo
  Then orchestration container è stoppato in <30s e RAM torna ≥5GB

Scenario: Smoke set continua a passare con profilo ridotto
  Given staging gira con profilo single-tester
  When eseguo lo smoke set G1 (A1-A5, C1-C5)
  Then 9/9 scenari passano (Tutor non è nello smoke set)
```

### G4 — Dev loop hybrid (`make work`)

**Specific**: Singolo comando `make work` che apre SSH tunnel a staging, avvia API+Web locali in modalità Integration, healthcheck loop, auto-recovery se tunnel cade, teardown pulito su `Ctrl+C`.

**Measurable**: Da `make work` a `localhost:3000` ready in ≤90s wall-clock; tunnel auto-restart entro 5s se SSH socket muore; `make work-stop` rimuove tutti i processi (no orphan dotnet/next).

**Achievable**: gli scripts `integration-tunnel.sh` + `integration-start.sh` + `integration-check.sh` esistono già — serve wrapper Makefile + retry loop + trap cleanup.

**Relevant**: Q4=C scelto, hybrid è il workflow daily; oggi richiede 3 comandi e error-prone.

**Time-bound**: Rilasciato entro **2026-05-12** (1 settimana).

#### Scenari Gherkin

```gherkin
Feature: make work - Hybrid Dev Loop

Scenario: Cold start completo in <90s
  Given staging è up e SSH key è in ~/.ssh/meepleai-staging
  And nessun tunnel attivo (make tunnel-status = "No active tunnels")
  When eseguo make work
  Then in <30s i tunnel SSH sono aperti (postgres:25432, redis:26379, AI services)
  And in <60s API è up su http://localhost:8080/health
  And in <90s Web è up su http://localhost:3000 e renderizza la home

Scenario: Auto-recovery quando tunnel cade
  Given make work è running
  When killo manualmente il processo SSH tunnel (kill <PID>)
  Then entro 5s il watchdog rilancia integration-tunnel.sh start
  And API continua a rispondere (riconnessione transparent)
  And nessun errore appare nella console make work

Scenario: Teardown pulito
  Given make work è running con FE+BE locali e tunnel aperti
  When premo Ctrl+C
  Then trap handler chiama make work-stop
  And dotnet/next processes sono killed
  And SSH tunnel è chiuso (ssh -O exit)
  And lsof -i:8080,3000 ritorna vuoto
  And exit code è 0

Scenario: Pre-flight check identifica problemi prima di start
  Given SSH key non ha permessi 600
  When eseguo make work
  Then il pre-flight check fallisce con messaggio "SSH key permissions: chmod 600 required"
  And exit code è 1, no tunnel viene aperto
```

### G5 — DR & Backup Validation continua

**Specific**: (a) Cron mensile (1° del mese, 04:00 UTC) esegue `backup-restore-test.sh` esteso con 3 smoke read-back queries (`SELECT count FROM games`, `users`, `sessions`); (b) Cron trimestrale (1° gen/apr/lug/ott, 09:00 UTC) invia notifica via webhook per "DR runbook walkthrough due"; (c) walkthrough loggato in `docs/operations/dr-walkthrough-log.md`.

**Measurable**: Report mensile in `/var/log/meepleai-restore-test.log` con `✅ PASS` / `❌ FAIL`; notifica trimestrale ricevuta; entry walkthrough loggato con data + esito + drift trovato.

**Achievable**: Estendere `backup-restore-test.sh` esistente (~30 righe di smoke read-back); aggiungere 2 entry crontab; creare 1 file log + 1 doc walkthrough log.

**Relevant**: Q5 = A+C scelto; previene "backup non testati" risk senza investimento in fresh-VPS drill.

**Time-bound**: Cron mensile attivo entro **2026-05-19**, prima walkthrough trimestrale loggata entro **2026-08-01**.

#### Scenari Gherkin

```gherkin
Feature: DR Validation Continua

Scenario: Monthly auto-restore test passa
  Given è il 1° del mese, ore 04:00 UTC
  And esiste backup giornaliero <YYYYMMDD>-030000 in /backups/meepleai/
  When cron esegue scripts/backup-restore-test.sh --with-smoke-readback
  Then crea container Postgres temp da backup
  And esegue 3 query smoke (SELECT count FROM games, users, sessions)
  And ogni query restituisce row count >0
  And cleanup del container temp avviene
  And log finale "✅ Restore + smoke read-back PASSED" in /var/log/meepleai-restore-test.log

Scenario: Monthly auto-restore test fallisce e notifica
  Given backup è corrotto (gzip integrity fail)
  When cron esegue backup-restore-test
  Then exit code != 0
  And webhook notification è inviata con payload {"status":"failure","test":"monthly-restore","date":"<DATE>"}
  And entry "❌ FAIL" è in /var/log/meepleai-restore-test.log

Scenario: Quarterly walkthrough reminder ricevuto
  Given è il 1° gennaio (o aprile/luglio/ottobre), ore 09:00 UTC
  When cron quarterly-dr-reminder esegue
  Then webhook notification con payload {"status":"action_required","task":"DR runbook walkthrough due","runbook":"docs/operations/disaster-recovery-runbook.md"}
  And se non viene loggato un walkthrough entro 7 giorni, secondo reminder è inviato

Scenario: Walkthrough loggato manualmente
  Given ho ricevuto il reminder trimestrale
  When eseguo manualmente il walkthrough del runbook (no nuova VPS, solo step review)
  And aggiungo entry al file docs/operations/dr-walkthrough-log.md con data + esito + drift trovato
  Then il next reminder partirà 3 mesi dopo dalla data loggata
```

## 4. Scope

### In-scope (questo design)

- Profilo Docker Compose `single-tester` con servizi opzionali gated
- Comando `make work` unificato per hybrid dev loop (Q4 scenario C)
- Cloudflare Tunnel migration (cf-tunnel-migration.md + rollback runbook)
- Backup-restore-test mensile esteso con smoke read-back
- Quarterly DR walkthrough reminder + log file
- Smoke set automation script (`scripts/smoke-set.sh`) per esecuzione locale dei 9 scenari G1
- Quick wins già approvati nel panel, mappati ai goals:
  - **Pre-deploy port cleanup** (`fuser -k 8080/tcp`) → parte di **G1 scenario C1** (deploy senza rotture)
  - **Daily disk prune cron** (`docker system prune -af --filter "until=72h"`) → parte di **G5** (validation continua, supporta C3 backup)
  - **Profilo `single-tester`** → core di **G3**
  - Nota: auto-migrations al deploy è **già implementato** in `deploy-staging.yml` job `migrate-db` (linee ~423-534, `dotnet ef migrations script --idempotent` + `psql -v ON_ERROR_STOP=1`); G1 scenario C2 ne valida solo l'invariante.

### Out-of-scope (esplicitamente esclusi)

- Hardware upgrade (Q5 = no)
- Spostamento CI runner a GitHub-hosted (rimandato, dipende da CF Tunnel migration)
- LLM cost monitoring/alerting (separato design, non bloccante)
- Multi-user beta migration path (Q2 = alpha indefinita, non rilevante ora)
- Annual full DR drill su nuova VPS (Q5 = solo A+C)
- Exploratory testing harness formale (separato design, deferred)
- Riduzione Grafana dashboards a 2 (Q3 implicit, ma non critico per resource saving)

## 5. Non-funzionali

| Aspetto | Target |
|---------|--------|
| Reversibility | Ogni change ha rollback documentato in <30 min |
| Downtime accettabile | <30 min per change pianificato, <2h per emergency |
| Effort totale dev | ~12h distribuite su 8 settimane |
| Costo aggiuntivo runtime | EUR 0 (CF Tunnel free tier, cron locale) |
| Backwards compat | Profilo `full` rimane disponibile per re-attivare tutti i servizi |

## 6. Rischi e Mitigazioni

| Rischio | Probabilità | Impatto | Mitigazione |
|---------|-------------|---------|-------------|
| CF Tunnel cutover rompe smoke set | Media | Alto | Rollback runbook testato prima del cutover; mantieni Traefik 7 giorni post-cutover come fallback |
| `make work` watchdog loop infinito | Bassa | Medio | Max retry counter (5) + exit con error message |
| Profilo `single-tester` rompe RAG (orchestration spento) | Bassa | Basso | Tutor non è nello smoke set; `make staging-with-tutor` per testare on-demand |
| Monthly auto-restore consuma 2GB temp disk | Media | Basso | Cleanup garantito da trap EXIT; `df -h` check pre-execution |
| Quarterly reminder webhook non recapitato | Bassa | Basso | Fallback a entry in `/var/log` + grep mensile manuale |

## 7. Open Questions (non bloccanti)

- **OQ1**: Quale webhook usare per notifiche? Slack via `BACKUP_WEBHOOK_URL` esistente o canale separato?
- **OQ2**: Cloudflare Access policy "owner-only" — quale identity provider? Google SSO è il più pratico; magic link email è alternativa.
- **OQ3**: Se cf-tunnel migration introduce latency >50ms aggiuntiva sul TTFT, accept o rollback?

## 8. Approvazione

- Spec approvata da DegrassiAaron in conversazione del 2026-05-05.
- Implementazione segue: `docs/superpowers/plans/2026-05-05-infrastructure-single-tester.md`.
