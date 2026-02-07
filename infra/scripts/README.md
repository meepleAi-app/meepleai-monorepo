# Infrastructure Scripts

## Contenuto

Script di utilità per gestione dell'infrastruttura Docker, secrets management, e orchestrazione startup.

## Scripts

### 🚀 start-dev.ps1 (NEW - Issue #3797)

**Scopo**: Orchestrazione startup ambiente sviluppo con validazione e monitoring

**Caratteristiche**:
- Port conflict detection automatico (usa cleanup-dev-ports.ps1)
- Startup orchestrato: infrastruttura → applicazione
- Health validation con timeout configurabili
- Display automatico URLs servizi
- Supporto profili Docker Compose

**Utilizzo**:
```powershell
# Startup standard (profilo dev)
.\infra\scripts\start-dev.ps1

# Startup con profilo specifico
.\infra\scripts\start-dev.ps1 -Profile full

# Skip port check (se sai che le porte sono libere)
.\infra\scripts\start-dev.ps1 -SkipPortCheck

# Start senza attendere health status
.\infra\scripts\start-dev.ps1 -WaitForHealthy:$false
```

**Output Esempio**:
```
🚀 MeepleAI Development Environment Startup
==================================================

>>> Checking for port conflicts...
✓ No port conflicts detected

🏗️  Starting Infrastructure Services
>>> Starting PostgreSQL, Redis, Qdrant...
✓ Infrastructure services started
✓ All infrastructure services healthy

🚀 Starting Application Services
>>> Starting API backend...
✓ API service healthy

>>> Starting Web frontend...
✓ Web service healthy

🌐 Service URLs
  Web Frontend              http://localhost:3000      Ready
  API Backend               http://localhost:8080      Ready
  API Docs (Scalar)         http://localhost:8080/scalar/v1      Ready

✓ Development environment started successfully!
```

**Sequenza Startup**:
1. Port conflict check & cleanup
2. postgres → redis → qdrant (wait healthy)
3. api (wait healthy)
4. web (wait healthy)
5. Display service URLs

**Chi lo usa**: Tutti gli sviluppatori Windows/WSL2
**Quando**: Ogni volta che avvii l'ambiente dev (sostituisce `docker compose up -d`)
**Requisiti**: PowerShell 5.1+, Docker Desktop
**Related**: Issue #3797, tools/cleanup/cleanup-dev-ports.ps1

---

### load-secrets-env.sh

**Scopo**: Carica secrets da Docker secrets e li espone come variabili d'ambiente.

**Utilizzo**:
```bash
# Source lo script per caricare secrets nell'ambiente corrente
source ./scripts/load-secrets-env.sh

# I secrets vengono letti da /run/secrets/ e esposti come env vars
echo $POSTGRES_PASSWORD
echo $OPENROUTER_API_KEY
```

**Secrets Supportati**:
- `postgres-password` → `POSTGRES_PASSWORD`
- `openrouter-api-key` → `OPENROUTER_API_KEY`
- `n8n-encryption-key` → `N8N_ENCRYPTION_KEY`
- `n8n-basic-auth-password` → `N8N_BASIC_AUTH_PASSWORD`
- `gmail-app-password` → `GMAIL_APP_PASSWORD`
- `grafana-admin-password` → `GRAFANA_ADMIN_PASSWORD`
- `initial-admin-password` → `INITIAL_ADMIN_PASSWORD`

**Come Funziona**:
1. Controlla se `/run/secrets/<secret-name>` esiste
2. Legge il contenuto del file secret
3. Esporta come variabile d'ambiente
4. Rimuove newline e spazi

**Esempio**:
```bash
#!/bin/bash
source ./scripts/load-secrets-env.sh

# Ora puoi usare le variabili
docker compose up -d
```

## Requisiti

- Docker secrets configurati (vedi `../secrets/README.md`)
- Bash shell
- Permessi di lettura su `/run/secrets/`

## Note

- **Sicurezza**: Non loggare mai il valore delle secrets
- **Production**: Sempre usare Docker secrets, mai env vars in chiaro
- **Development**: Opzionale, puoi usare `.env.dev` invece

## Related Scripts

Per altri script di infrastruttura vedi:
- `../secrets/README.md` - Generazione e gestione secrets
- `../../tools/secrets/init-secrets.sh` - Inizializzazione secrets
- `../../tools/cleanup-caches.sh` - Pulizia cache e build artifacts

## Aggiungere Nuovi Script

Quando aggiungi script:
1. Documentali in questo README
2. Aggiungi permessi di esecuzione: `chmod +x script.sh`
3. Includi help text: `./script.sh --help`
4. Gestisci errori appropriatamente
5. Logga operazioni critiche

## Troubleshooting

### Secret Not Found

**Errore**: `Secret file not found: /run/secrets/postgres-password`

**Fix**:
```bash
# Inizializza secrets
cd ../secrets
./init-secrets.sh

# Verifica
ls -la /run/secrets/
```

### Permission Denied

**Errore**: `Permission denied: /run/secrets/postgres-password`

**Fix**:
```bash
# Secrets devono essere leggibili dal processo
# Verifica permessi Docker Swarm o Docker Compose secrets
docker secret ls
```

## Best Practices

1. **Mai hardcodare secrets** negli script
2. **Sempre validare** che secrets esistano prima dell'uso
3. **Usare set -e** per exit on error
4. **Loggare operazioni** ma mai valori sensibili
5. **Documentare parametri** e opzioni

## Esempio di utilizzo dell’applicazione

- Prima di eseguire `docker compose up -d api web n8n`, il team sviluppa sorgente `source ./scripts/load-secrets-env.sh` per caricare `/run/secrets/postgres-password` e `openrouter-api-key` nel proprio ambiente di shell. In questo modo l’app MeepleAI può leggere le stesse variabili che riceverebbe nel container e si evita di inserire valori sensibili nei file `.env`.
