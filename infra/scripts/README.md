# Infrastructure Scripts

Script di utilita per gestione infrastruttura Docker, secrets, database e orchestrazione startup.

## Scripts

### Startup & Development

#### start-dev.ps1

Orchestrazione startup ambiente sviluppo con validazione e monitoring.

```powershell
.\infra\scripts\start-dev.ps1                     # Startup standard (profilo dev)
.\infra\scripts\start-dev.ps1 -Profile full        # Profilo specifico
.\infra\scripts\start-dev.ps1 -SkipPortCheck       # Skip port check
.\infra\scripts\start-dev.ps1 -WaitForHealthy:$false  # Senza attendere health
```

Sequenza: port check → postgres/redis (wait healthy) → api (wait healthy) → web → display URLs.

#### integration-tunnel.sh

SSH tunnels verso staging server per ambiente integration. Supporta start/stop/status.

```bash
bash infra/scripts/integration-tunnel.sh start    # Apri tunnel (default)
bash infra/scripts/integration-tunnel.sh stop     # Chiudi tunnel
bash infra/scripts/integration-tunnel.sh status   # Verifica stato
```

Forwarda 11 servizi (PostgreSQL, Redis, Embedding, Reranker, Unstructured, SmolDocling, Ollama, n8n, Grafana, Prometheus, Orchestrator). Usa control socket SSH per gestione persistente.

Integrato nel Makefile: `make tunnel`, `make tunnel-stop`, `make tunnel-status`.

### Secrets Management

#### load-secrets-env.sh

Entrypoint per container Docker (n8n, Grafana, Prometheus, Alertmanager). Mappa variabili da `.secret` files a formati specifici dei servizi (es. `POSTGRES_*` → `DB_POSTGRESDB_*` per n8n, `REDIS_PASSWORD` → `REDIS_URL`).

Usato in `docker-compose.yml` come entrypoint — **non eliminare**.

#### sync-secrets.ps1

Sync bidirezionale secrets tra locale e staging server via SSH/SCP.

```powershell
pwsh infra/scripts/sync-secrets.ps1 -Pull          # Scarica da server
pwsh infra/scripts/sync-secrets.ps1                 # Push verso server (default)
pwsh infra/scripts/sync-secrets.ps1 -Status         # Mostra diff
pwsh infra/scripts/sync-secrets.ps1 -Only openrouter -DryRun  # Singolo secret, dry run
```

Funzionalita: manifest YAML per policy, backup automatico pre-push, restart servizi impattati, warning per servizi stateful.

### Database

#### db-dump.ps1

Export database PostgreSQL con esclusione tabelle sensibili.

```powershell
pwsh infra/scripts/db-dump.ps1                              # Default: exclude sensitive
pwsh infra/scripts/db-dump.ps1 -OutputFile backup.sql       # File specifico
pwsh infra/scripts/db-dump.ps1 -ExcludeSensitive:$false     # Dump completo
```

#### db-restore.ps1

Restore database da dump SQL.

```powershell
pwsh infra/scripts/db-restore.ps1 -InputFile backup.sql                    # Restore
pwsh infra/scripts/db-restore.ps1 -InputFile backup.sql -DropExisting      # Drop + recreate
pwsh infra/scripts/db-restore.ps1 -InputFile backup.sql -TargetDb mydb     # Target specifico
```

#### seed-dump.ps1 / seed-pull.ps1 / seed-restore.ps1

Gestione seed database con checksum SHA256 e safety guards.

```powershell
pwsh infra/scripts/seed-dump.ps1 -Profile dev               # Dump seed locale
pwsh infra/scripts/seed-pull.ps1 -Environment staging -Host deploy@server  # Pull da remoto
pwsh infra/scripts/seed-restore.ps1 -DumpFile seed.dump      # Restore con checksum verify
```

Safety: checksum verification, guard contro restore staging→prod, confirmation prompt.

### Operations

#### reset-staging-beta0.sh

Reset completo staging: backup DB → stop containers → wipe data volumes (preserva ML models) → restart → verify health.

```bash
# Eseguire sul server staging
sudo bash infra/scripts/reset-staging-beta0.sh
```

#### mvp-smoke-test.sh

Health check rapido di tutti i servizi.

```bash
bash infra/scripts/mvp-smoke-test.sh                  # Default: localhost
bash infra/scripts/mvp-smoke-test.sh https://meepleai.app  # URL specifico
```

### AI Services

#### ollama-init.sh

Pull modelli Ollama richiesti (qwen2.5:1.5b + mxbai-embed-large).

```bash
docker exec meepleai-ollama bash /scripts/ollama-init.sh
```

#### reembed-vectors.sh

Trigger batch job re-embedding via admin API (es. dopo cambio modello embedding).

```bash
bash infra/scripts/reembed-vectors.sh http://localhost:8080 $ADMIN_TOKEN
```
