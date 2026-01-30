TODOS.md

Ruolo Editor

- tester dell'app. Invia feedback,segnala bug.
- moderatore contenuti (shared games). Gli utenti possono fare richieste sull'inserimento di giochi nella shared library. Se e' senza pdf, puo' approvarlo. Se richiede pdf, puo' scartarlo o proporlo all'admin.
- creatore profili agent. Puo' creare e testare profili agent.
  L’admin e editor possono usare lo scraper per cercare pdf e caricarli.
  L’editor puo’ richiedere di caricare pdf  
  L’utente potrebbe avere un gioco non presente in BGG o in shared games, deve poterlo creare e aggiungerci i pdf. Potrebbe richiedere a un admin di pubblicarlo nella shared games.

Self-hosted TOTP
Prima di andare in produzione, consiglio una  
 revisione legale per personalizzare i dettagli specifici (es. [Città] nella sezione legge applicabile, email
legal@meepleai.com).

Opzione 1: Script Bash (Raccomandato)  
 cd infra

# Minimal (solo servizi core: postgres, redis, qdrant, api, web)

./start-minimal.sh

# Dev (core + prometheus, grafana)

./start-dev.sh

# Full (tutti i servizi inclusi AI/ML e monitoring)

./start-full.sh
Opzione 2: Docker Compose con Profili
cd infra

# Minimal profile

docker compose --profile minimal up -d

# Dev profile

docker compose --profile dev up -d

# Full profile (TUTTO incluso HyperDX)

docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full up -d
Profili Disponibili
┌─────────┬────────────────────────────────────────────┬─────────────────────────┐
│ Profilo │ Servizi Inclusi │ Uso Consigliato │
├─────────┼────────────────────────────────────────────┼─────────────────────────┤
│ minimal │ PostgreSQL, Redis, Qdrant, API, Web │ Sviluppo rapido │
├─────────┼────────────────────────────────────────────┼─────────────────────────┤
│ dev │ minimal + Prometheus, Grafana │ Sviluppo con monitoring │
├─────────┼────────────────────────────────────────────┼─────────────────────────┤
│ full │ Tutto (AI/ML, n8n, Mailpit, HyperDX, etc.) │ Testing completo │
└─────────┴────────────────────────────────────────────┴─────────────────────────┘
Servizi Disponibili (Full Profile)
✅ Core: API (8080), Web (3000)
✅ Database: PostgreSQL (5432), Redis (6379), Qdrant (6333)
✅ AI/ML: Embedding (8000), Unstructured (8001), SmolDocling (8002), Reranker (8003), Ollama (11434)
✅ Monitoring: Grafana (3001), Prometheus (9090), Alertmanager (9093)
✅ Automation: n8n (5678), Mailpit (8025)
Comandi Utili

# Verificare status

docker compose ps

# Logs

docker compose logs -f
docker compose logs -f api

# Stop

docker compose down

# Stop con cancellazione dati

docker compose down -v
Prerequisiti
Prima di avviare, assicurati di aver generato i secrets:
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated
cd ..

Prossimi Passi (quando avrai i server)

1. Configura GitHub Environments in Settings → Environments
2. Aggiungi Secrets (SSH keys, hosts)
3. Imposta DEPLOY_METHOD variable (ssh | kubernetes | cloudrun)
4. Testa staging con merge su main-staging

● Ho completato il set di runbooks. Ecco il riepilogo finale:
Runbooks Creati
Runbook: README.md
Contenuto: Indice, contatti emergenza, checklist rapide
────────────────────────────────────────
Runbook: incident-response.md
Contenuto: Severity levels P1-P4, decision tree, post-mortem template
────────────────────────────────────────
Runbook: disaster-recovery.md
Contenuto: 4 scenari (VPS down, DB corrotto, ransomware, cancellazione)
────────────────────────────────────────
Runbook: backup-restore.md
Contenuto: Strategie backup, script automatici, procedure restore
────────────────────────────────────────
Runbook: troubleshooting.md
Contenuto: Debug per servizio (PostgreSQL, Redis, Qdrant, API, Traefik)
────────────────────────────────────────
Runbook: maintenance.md
Contenuto: Calendario manutenzione, rotazione credenziali, cleanup
────────────────────────────────────────
Runbook: scaling.md
Contenuto: Scaling verticale/orizzontale, migrazione a managed services
Struttura Finale
docs/04-deployment/runbooks/
├── README.md # Indice e quick reference
├── incident-response.md # Gestione incidenti
├── disaster-recovery.md # Recovery da failure catastrofici
├── backup-restore.md # Backup e restore
├── troubleshooting.md # Debug per servizio
├── maintenance.md # Manutenzione ordinaria
└── scaling.md # Guida scaling
Prossimi Passi Consigliati

1. Acquista dominio: meepleai.com su Porkbun (~€10/anno)
2. Crea account: Hetzner Cloud + Cloudflare (gratuiti)
3. Provisiona VPS: CPX31 (€15.59/mese) seguendo setup-guide-self-hosted.md
4. Configura backup automatici: Crontab come da runbook
5. Testa i runbooks: Simula un incident per validare le procedure
