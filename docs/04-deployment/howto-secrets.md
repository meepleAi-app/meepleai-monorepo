# How-To: Secrets Management

**Data**: 2026-01-19 | **Issue**: #2570

---

## Sintesi

MeepleAI utilizza un **sistema di gestione secrets a 3 livelli** con validazione centralizzata:

| Livello | Comportamento | Esempi |
|---------|---------------|--------|
| **CRITICAL** | Startup bloccato se mancante | database, redis, jwt, admin |
| **IMPORTANT** | Warning, funzionalità ridotte | openrouter, bgg |
| **OPTIONAL** | Info, alcune feature disabilitate | oauth, email, monitoring |

### Architettura

```
┌─────────────────────────────────────────────────────────────────┐
│                    SECRET MANAGEMENT FLOW                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐    │
│  │ setup-       │     │ .secret      │     │ Docker       │    │
│  │ secrets.ps1  │ ──► │ files        │ ──► │ Compose      │    │
│  │ (genera)     │     │ (KEY=VALUE)  │     │ (env_file)   │    │
│  └──────────────┘     └──────────────┘     └──────────────┘    │
│                              │                    │             │
│                              │                    ▼             │
│                              │           ┌──────────────┐       │
│                              │           │ load-secrets │       │
│                              │           │ -env.sh      │       │
│                              │           │ (mapping)    │       │
│                              │           └──────────────┘       │
│                              │                    │             │
│                              ▼                    ▼             │
│                    ┌─────────────────────────────────────┐     │
│                    │         API Container               │     │
│                    │   SecretLoader.cs (validazione)     │     │
│                    └─────────────────────────────────────┘     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Differenze per Ambiente

| Ambiente | Metodo | Location |
|----------|--------|----------|
| **Development** | env_file (KEY=VALUE) | `infra/secrets/*.secret` |
| **Staging** | Docker secrets (.txt) | `infra/secrets/staging/*.txt` |
| **Production** | Docker secrets (.txt) | `infra/secrets/prod/*.txt` |

---

## Quick Start

### 1. Setup Iniziale (Development)

```powershell
# Naviga alla directory secrets
cd infra/secrets

# Esegui setup (auto-genera valori sicuri)
.\setup-secrets.ps1 -SaveGenerated

# Output: Crea tutti i .secret files con password auto-generate
```

### 2. Configurazione Manuale

Dopo il setup, configura manualmente questi secrets:

```powershell
# API Keys (richiesti per funzionalità AI)
notepad openrouter.secret    # Inserisci OPENROUTER_API_KEY

# BoardGameGeek (opzionale)
notepad bgg.secret           # Inserisci BGG_USERNAME e BGG_PASSWORD

# OAuth providers (opzionale)
notepad oauth.secret         # Inserisci client ID/secrets per Google/GitHub/Discord
```

### 3. Generazione .env.development

Per sviluppo locale (fuori Docker):

```powershell
cd infra/secrets
.\generate-env-from-secrets.ps1

# Output: Genera .env.development nella root del progetto
```

### 4. Avvio Servizi

```bash
# Con Docker (usa .secret files)
cd infra
docker compose up -d

# Locale (usa .env.development)
cd apps/api/src/Api
dotnet run
```

---

## Operazioni Comuni

### Aggiornare un Secret

```powershell
# 1. Modifica il file
notepad infra/secrets/redis.secret

# 2. Riavvia i servizi interessati
docker compose restart redis api

# 3. (Opzionale) Rigenera .env.development
cd infra/secrets
.\generate-env-from-secrets.ps1 -Force
```

### Aggiungere un Nuovo Secret

1. Crea template: `infra/secrets/myservice.secret.example`
2. Aggiungi a `SecretDefinitions.cs`:
   ```csharp
   ["myservice"] = new(
       SecretLevel.Important,
       "MYSERVICE_API_KEY"
   ),
   ```
3. Aggiungi a `docker-compose.yml`:
   ```yaml
   api:
     env_file:
       - ./secrets/myservice.secret
   ```
4. Rigenera: `.\setup-secrets.ps1`

### Rotazione Secrets

```powershell
# 1. Genera nuovo valore
$newPassword = [Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 }) -as [byte[]])

# 2. Aggiorna file
(Get-Content redis.secret) -replace 'REDIS_PASSWORD=.*', "REDIS_PASSWORD=$newPassword" | Set-Content redis.secret

# 3. Riavvia tutti i servizi che usano il secret
docker compose restart redis api

# 4. Rigenera .env.development
.\generate-env-from-secrets.ps1 -Force
```

---

## File Reference

### Directory Structure

```
infra/secrets/
├── *.secret.example          # Templates (tracked in git)
├── *.secret                  # Active secrets (gitignored)
├── setup-secrets.ps1         # Setup script
├── generate-env-from-secrets.ps1  # .env generator
├── staging/
│   ├── README.md
│   ├── *.txt.example         # Staging templates
│   └── *.txt                 # Staging secrets (gitignored)
└── prod/
    ├── README.md
    ├── *.txt.example         # Prod templates
    └── *.txt                 # Prod secrets (gitignored)
```

### Secret Files (18 totali)

#### Critical (6) - Startup bloccato se mancanti

| File | Chiavi | Descrizione |
|------|--------|-------------|
| `database.secret` | POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB | Database credentials |
| `redis.secret` | REDIS_PASSWORD | Cache/session store |
| `qdrant.secret` | QDRANT_API_KEY | Vector database |
| `jwt.secret` | JWT_SECRET_KEY, JWT_ISSUER, JWT_AUDIENCE | Auth tokens |
| `admin.secret` | ADMIN_EMAIL, ADMIN_PASSWORD, ADMIN_DISPLAY_NAME | Bootstrap admin |
| `embedding-service.secret` | EMBEDDING_SERVICE_API_KEY | ML embeddings |

#### Important (3) - Warning se mancanti

| File | Chiavi | Descrizione |
|------|--------|-------------|
| `openrouter.secret` | OPENROUTER_API_KEY, OPENROUTER_DEFAULT_MODEL | LLM provider |
| `unstructured-service.secret` | UNSTRUCTURED_API_KEY | PDF extraction |
| `bgg.secret` | BGG_USERNAME, BGG_PASSWORD | BoardGameGeek API |

#### Optional (9) - Info se mancanti

| File | Chiavi | Descrizione |
|------|--------|-------------|
| `oauth.secret` | GOOGLE/GITHUB/DISCORD_OAUTH_CLIENT_ID/SECRET | Social login |
| `email.secret` | SMTP_*, GMAIL_APP_PASSWORD | Email sending |
| `storage.secret` | S3_ACCESS_KEY, S3_SECRET_KEY, etc. | Cloud storage |
| `monitoring.secret` | GRAFANA_ADMIN_PASSWORD, PROMETHEUS_PASSWORD | Observability |
| `traefik.secret` | TRAEFIK_DASHBOARD_USER/PASSWORD | Reverse proxy |
| `n8n.secret` | N8N_ENCRYPTION_KEY, N8N_BASIC_AUTH_PASSWORD | Workflow automation |
| `smoldocling-service.secret` | SMOLDOCLING_API_KEY | Advanced PDF |
| `reranker-service.secret` | RERANKER_API_KEY | Search reranking |

---

## Script Reference

### setup-secrets.ps1

```powershell
# Crea .secret files da .example con valori auto-generati
.\setup-secrets.ps1

# Salva backup dei valori generati
.\setup-secrets.ps1 -SaveGenerated
```

**Auto-genera:**
- JWT_SECRET_KEY (Base64, 64 bytes)
- POSTGRES_PASSWORD (20 chars, complesso)
- REDIS_PASSWORD (20 chars, complesso)
- QDRANT_API_KEY (Base64, 32 bytes)
- ADMIN_PASSWORD (16 chars, complesso)
- Vari API keys per servizi interni

### generate-env-from-secrets.ps1

```powershell
# Genera .env.development dalla root
.\generate-env-from-secrets.ps1

# Forza sovrascrittura
.\generate-env-from-secrets.ps1 -Force

# Output personalizzato
.\generate-env-from-secrets.ps1 -OutputPath "C:\custom\path\.env"
```

### load-secrets-env.sh

Script runtime che:
1. Mappa `POSTGRES_*` → `DB_POSTGRESDB_*` (per n8n)
2. Costruisce `REDIS_URL` da `REDIS_PASSWORD`
3. Mappa `GRAFANA_ADMIN_PASSWORD` → `GF_SECURITY_ADMIN_PASSWORD`
4. Backward-compatibility: `GOOGLE_CLIENT_ID` → `GOOGLE_OAUTH_CLIENT_ID`

---

## Staging/Production Setup

### Staging

```bash
cd infra/secrets/staging

# Crea da template
cp postgres-password.txt.example postgres-password.txt
cp redis-password.txt.example redis-password.txt

# Genera password sicure
openssl rand -base64 24 > postgres-password.txt
openssl rand -base64 24 > redis-password.txt

# Avvia
docker compose -f docker-compose.yml -f compose.staging.yml up -d
```

### Production

```bash
cd infra/secrets/prod

# Crea da template
cp postgres-password.txt.example postgres-password.txt
cp redis-password.txt.example redis-password.txt
cp openrouter-api-key.txt.example openrouter-api-key.txt

# Password MOLTO sicure (32+ caratteri)
openssl rand -base64 32 > postgres-password.txt
openssl rand -base64 32 > redis-password.txt

# API key manuale
echo "sk-or-v1-your-key" > openrouter-api-key.txt

# Avvia
docker compose -f docker-compose.yml -f compose.prod.yml up -d
```

---

## Troubleshooting

### "CRITICAL secrets missing" all'avvio

```bash
# Verifica quali mancano
docker compose logs api | grep -i "secret"

# Rigenera i mancanti
cd infra/secrets
.\setup-secrets.ps1
```

### OAuth login non funziona

1. Verifica naming corretto in `oauth.secret`:
   ```
   GOOGLE_OAUTH_CLIENT_ID=...    # ✅ Corretto
   GOOGLE_CLIENT_ID=...          # ⚠️ Vecchio (backward-compatible)
   ```

2. Riavvia API:
   ```bash
   docker compose restart api
   ```

### .env.development non sincronizzato

```powershell
cd infra/secrets
.\generate-env-from-secrets.ps1 -Force
```

### Redis connection refused

```bash
# Verifica password match
docker compose exec redis redis-cli -a "$(cat infra/secrets/redis.secret | grep REDIS_PASSWORD | cut -d= -f2)" ping
```

---

## Security Best Practices

1. **Mai committare** `.secret` files o `.env.development`
2. **Rotazione** ogni 90 giorni
3. **Backup** in vault sicuro (1Password, HashiCorp Vault)
4. **Password complesse**: 20+ caratteri per production
5. **Principio minimo privilegio**: ogni servizio ha solo i secrets necessari

---

## Appendice: Variable Mapping

| Source (.secret) | Runtime Mapping | Used By |
|------------------|-----------------|---------|
| `POSTGRES_USER` | `DB_POSTGRESDB_USER` | n8n |
| `POSTGRES_PASSWORD` | `DB_POSTGRESDB_PASSWORD` | n8n |
| `POSTGRES_DB` | `DB_POSTGRESDB_DATABASE` | n8n |
| `REDIS_PASSWORD` | `REDIS_URL` (costruito) | API |
| `GRAFANA_ADMIN_PASSWORD` | `GF_SECURITY_ADMIN_PASSWORD` | Grafana |
| `GOOGLE_CLIENT_ID` | `GOOGLE_OAUTH_CLIENT_ID` | API (backward-compat) |
