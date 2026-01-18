# Sistema Secret Management - Configurazione Finale

**Data**: 2026-01-17
**Contesto**: Security fix post Issue #2565
**Commit**: 92db3532d

---

## ✅ Sistema Consolidato (Issue #2570 - COMPLETE)

### Flusso Unificato: Tutti i Servizi Usano .secret via env_file

**Tutti i servizi** (postgres, n8n, api, grafana, alertmanager):
```yaml
# docker-compose.yml
postgres:
  env_file:
    - ./secrets/database.secret

n8n:
  env_file:
    - ./secrets/database.secret
    - ./secrets/n8n.secret

api:
  env_file:
    - ./secrets/database.secret
    - ./secrets/redis.secret
    - ./secrets/jwt.secret
    - ./secrets/openrouter.secret
    - ./secrets/admin.secret
    - ./secrets/oauth.secret

grafana:
  env_file:
    - ./secrets/monitoring.secret

alertmanager:
  env_file:
    - ./secrets/email.secret
```

**Risultato**: ✅ ZERO .txt files, ZERO Docker secrets, ZERO duplicazione

### Flusso 2: Sviluppo Locale (.NET API)

**Program.cs carica** `.env.development` dalla root:
```csharp
// Program.cs line 53
var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".env.development");
Env.Load(envFilePath);
```

**Contenuto** `.env.development`:
- POSTGRES_*, REDIS_*, OAuth credentials
- ⚠️ Sincronizzato MANUALMENTE con .secret files
- ✅ Gitignored (non committato)

---

## Script Gestione Secret

### ✅ Script ATTIVI (da usare)

**1. `infra/secrets/setup-secrets.ps1`**
```powershell
# FUNZIONE: Inizializzazione prima volta
# INPUT: *.secret.example files
# OUTPUT: *.secret files con valori generati
# USO: .\setup-secrets.ps1 [-SaveGenerated]
# QUANDO: Prima configurazione progetto
```

**2. `infra/secrets/generate-docker-secrets.ps1`**
```powershell
# FUNZIONE: Estrae variabili singole da .secret → .txt
# INPUT: *.secret files (multi-variable)
# OUTPUT: *.txt files (single-value per Docker secrets)
# USO: Eseguire dopo modifica .secret files
# QUANDO: Dopo update di .secret files che sono usati come Docker secrets
# ESEMPIO: oauth.secret:GOOGLE_CLIENT_ID → google-oauth-client-id.txt
```

### ❌ Script RIMOSSI (erano ridondanti)

**~~`scripts/generate-env-from-secrets.sh`~~** (REMOVED in commit 92db3532d)
```bash
# PROBLEMA: Generava apps/api/src/Api/.env che NON veniva caricato
# Program.cs carica .env.development dalla root, non apps/api/src/Api/.env
# RISOLUZIONE: Rimosso lo script e il file .env generato
```

---

## File e Sicurezza

### ✅ Gitignored (SICURI - non committati)

```gitignore
# Secret files
infra/secrets/*.secret
infra/secrets/*.txt

# Environment files
.env
**/.env
.env.development

# Development files con password (da commit 92db3532d)
apps/api/src/Api/apply-migrations.ps1
apps/api/src/Api/appsettings.Development.json
```

### ✅ Committati (SICURI - no secrets)

- `*.secret.example` - Template con placeholder
- `.env.*.example` - Template con placeholder
- `docker-compose.yml` - Configurazione (reference a secrets, no values)
- `docs/` - Documentazione (REDACTED dal commit 92db3532d)

### ⚠️ Esposti in Git History (ROTATI)

**Commit 588bc9c8e, f77b1f91d** esposero password in documentazione:
- POSTGRES_PASSWORD: ~~`DevPassword123`~~ → `l08vsDeSXLh2PwU0TKK2` ✅ ROTATA
- REDIS_PASSWORD: ~~`B+(;Km]IC3:{ysAwOJFJ`~~ → `WPq67zNtixLw8CMyECcs` ✅ ROTATA
- QDRANT_API_KEY: ~~`Dz2tJYjT...`~~ → `JmIGuDmWA1v...` ✅ ROTATA
- ADMIN_PASSWORD: ~~`Hh.Q5V*)>Qg[Xz>A`~~ → `pVKOMQNK0tFNgGlX` ✅ ROTATA

**Mitigazione**:
- ✅ Documentazione redatta (commit 92db3532d)
- ✅ Password rotate
- ✅ File con password non più tracciati
- ⚠️ Git history conserva vecchie password (ACCEPTABLE per dev environment)

---

## Workflow Corretto per Aggiornare Secret

### Scenario 1: Aggiornare Database/Redis/Qdrant Credentials

```bash
# 1. Modifica .secret file
nano infra/secrets/database.secret

# 2. Ricrea container Docker
cd infra
docker compose down postgres
docker volume rm infra_pgdata
docker compose up -d postgres

# 3. Applica migrations (se database)
cd ../apps/api/src/Api
pwsh apply-migrations.ps1

# 4. Aggiorna .env.development manualmente
nano .env.development  # Sincronizza POSTGRES_PASSWORD
```

### Scenario 2: Aggiornare OAuth/API Keys

```bash
# 1. Modifica .secret file
nano infra/secrets/oauth.secret

# 2. Genera .txt files per Docker secrets
cd infra/secrets
pwsh generate-docker-secrets.ps1

# 3. Ricrea services che usano questi secrets
cd ..
docker compose restart api n8n

# 4. Aggiorna .env.development manualmente
nano .env.development  # Sincronizza OAuth credentials
```

### Scenario 3: Prima Configurazione

```bash
# 1. Copia examples e genera valori
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# 2. Genera .txt files per Docker
pwsh generate-docker-secrets.ps1

# 3. Aggiorna .env.development
# Copia manualmente valori da .secret files a .env.development

# 4. Avvia servizi
cd ..
docker compose up -d
```

---

## Mapping: .secret → .txt (Docker Secrets)

Script `generate-docker-secrets.ps1` crea:

| Source Secret | Variable | Target .txt File |
|---------------|----------|------------------|
| database.secret | POSTGRES_PASSWORD | postgres-password.txt |
| redis.secret | REDIS_PASSWORD | redis-password.txt |
| jwt.secret | JWT_SECRET_KEY | jwt-secret.txt |
| openrouter.secret | OPENROUTER_API_KEY | openrouter-api-key.txt |
| admin.secret | ADMIN_PASSWORD | initial-admin-password.txt |
| oauth.secret | GOOGLE_CLIENT_ID | google-oauth-client-id.txt |
| oauth.secret | GOOGLE_CLIENT_SECRET | google-oauth-client-secret.txt |
| oauth.secret | GITHUB_CLIENT_ID | github-oauth-client-id.txt |
| oauth.secret | GITHUB_CLIENT_SECRET | github-oauth-client-secret.txt |
| oauth.secret | DISCORD_CLIENT_ID | discord-oauth-client-id.txt |
| oauth.secret | DISCORD_CLIENT_SECRET | discord-oauth-client-secret.txt |
| bgg.secret | BGG_PASSWORD | bgg-api-token.txt |
| email.secret | SMTP_PASSWORD | gmail-app-password.txt |
| monitoring.secret | GRAFANA_ADMIN_PASSWORD | grafana-admin-password.txt |

---

## Sincronizzazione .env.development

⚠️ **MANUALE** - Nessuno script automatico

**Variabili da sincronizzare** con .secret files:
```
database.secret → POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB
redis.secret → REDIS_PASSWORD
oauth.secret → GOOGLE_*, GITHUB_*, DISCORD_*
admin.secret → INITIAL_ADMIN_PASSWORD
openrouter.secret → OPENROUTER_API_KEY
bgg.secret → BGG_API_TOKEN (if set)
```

**Quando sincronizzare**:
- Dopo `setup-secrets.ps1` (prima configurazione)
- Dopo rotazione password
- Dopo update di .secret files

---

## Audit Secrets (Verifica Integrità)

### Check 1: Tutti i CRITICAL secrets esistono?

```bash
cd infra/secrets
for secret in database redis qdrant jwt admin embedding-service; do
  [ -f "$secret.secret" ] && echo "✅ $secret.secret" || echo "❌ MISSING: $secret.secret"
done
```

### Check 2: Docker .txt files sincronizzati?

```bash
cd infra/secrets
pwsh generate-docker-secrets.ps1
# Verifica output: dovrebbe dire "Created: N files"
```

### Check 3: .env.development sincronizzato?

```bash
# Confronta manualmente
diff <(grep POSTGRES_PASSWORD infra/secrets/database.secret) \
     <(grep POSTGRES_PASSWORD .env.development)
# Nessun output = sincronizzati
```

---

## Best Practices

### ✅ DO

- Usa `.secret` files come source of truth
- Esegui `generate-docker-secrets.ps1` dopo update di `.secret`
- Sincronizza manualmente `.env.development`
- Rotate password periodicamente (ogni 90 giorni)
- Verifica `.gitignore` include tutti i file con secret

### ❌ DON'T

- Non committare `.secret` files
- Non committare `.env` files
- Non committare password in documentazione
- Non usare password di development in production
- Non condividere `.secret` files via email/chat
- Non usare `generate-env-from-secrets.sh` (removed)

---

## Future: Consolidamento Completo (Issue #2566)

**Obiettivo**: Eliminare .txt files, usare solo .secret

**Plan**:
1. Modificare docker-compose.yml per usare `env_file: ./secrets/*.secret` per tutti i servizi
2. Aggiornare `load-secrets-env.sh` per mappare variabili
3. Rimuovere `generate-docker-secrets.ps1` (non più necessario)
4. Singola fonte di verità: .secret files

---

**Stato Attuale**: ✅ Sicuro e funzionale
**Next**: Issue #2566 per consolidamento completo
