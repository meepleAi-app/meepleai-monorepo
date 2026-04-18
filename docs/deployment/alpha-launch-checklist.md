# Alpha Launch Checklist

Checklist operativa per il deploy dell'alpha su staging (meepleai.app) per ~10 tester.

**Server**: Hetzner CAX21 (4 vCPU, 8GB RAM) | **Dominio**: meepleai.app | **Target**: `make staging-core` (no AI locale)

---

## Pre-requisiti

- [ ] Accesso SSH al server: `ssh -i ~/.ssh/meepleai-staging deploy@<IP>`
- [ ] DNS Cloudflare configurato (A record → IP server)
- [ ] Account OpenRouter con crediti: https://openrouter.ai/keys
- [ ] (Opzionale) Bucket R2 Cloudflare per PDF storage

---

## Fase 1: Secrets & Configurazione

### 1.1 Verificare secrets locali

```bash
cd infra
ls secrets/*.secret | wc -l  # Deve essere >= 6 (i critici)
```

**Secrets critici** (senza questi il deploy fallisce):

| File | Verifica |
|------|----------|
| `secrets/database.secret` | `POSTGRES_PASSWORD`, `POSTGRES_USER`, `POSTGRES_DB` |
| `secrets/redis.secret` | `REDIS_PASSWORD` |
| `secrets/jwt.secret` | `JWT_SECRET_KEY` (min 64 chars), `JWT_ISSUER`, `JWT_AUDIENCE` |
| `secrets/admin.secret` | `ADMIN_EMAIL`, `ADMIN_PASSWORD` (min 8 chars, 1 maiuscola, 1 cifra) |
| `secrets/openrouter.secret` | `OPENROUTER_API_KEY=sk-or-v1-...` |
| `secrets/embedding-service.secret` | `EMBEDDING_SERVICE_API_KEY` |

### 1.2 Sincronizzare secrets su staging

```bash
# Se secrets già configurati localmente:
make secrets-sync   # Pull da staging (richiede SSH)

# Oppure push manuale:
scp -i ~/.ssh/meepleai-staging secrets/*.secret deploy@<IP>:/opt/meepleai/secrets/
```

### 1.3 Generare basic auth per servizi interni

```bash
# Sul server:
sudo apt install apache2-utils
INTEGRATION_BASIC_AUTH=$(htpasswd -nb admin <password-scelta>)
echo "export INTEGRATION_BASIC_AUTH='$INTEGRATION_BASIC_AUTH'" >> ~/.bashrc
```

---

## Fase 2: Deploy

### 2.1 Pull codice sul server

```bash
ssh -i ~/.ssh/meepleai-staging deploy@<IP>
cd /opt/meepleai/repo
git pull origin main-dev
```

### 2.2 Restore da snapshot (DB + vettori pre-indicizzati)

Il seed completo (giochi + PDF + chunk + embedding pgvector) è in uno snapshot PostgreSQL
pubblicato su R2. Questo è il modo **raccomandato** per popolare staging — include tutto.

```bash
cd infra

# 1. Avvia solo postgres e redis
docker compose -f docker-compose.yml -f compose.staging.yml up -d postgres redis

# 2. Attendi che postgres sia healthy
docker compose exec postgres pg_isready -U meepleai

# 3. Fetch + verifica + restore snapshot da R2
make dev-from-snapshot
```

Il restore:
- Scarica l'ultimo snapshot da R2 (cache locale in `data/snapshots/`)
- Verifica compatibilità: migration EF, modello embedding, dimensioni
- Applica schema EF Core (`dotnet ef database update`)
- Restore dati: `pg_restore --data-only` + supplement per colonne pgvector
- Imposta `SKIP_CATALOG_SEED=true` per evitare re-seed

**Se lo snapshot non è disponibile o è incompatibile**, fai il seed manuale (vedi Fase 3.3 sotto).

### 2.3 Deploy core (raccomandato per CAX21)

```bash
cd infra
make staging-core   # api + web + monitoring + proxy (postgres/redis già up)
```

Questo esegue:
```bash
docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml \
  --profile monitoring --profile proxy up -d --build
```

**Importante**: L'API partirà con `SEED_PROFILE=Staging`. Se il DB è già popolato dallo snapshot,
non ri-semina. Se il DB è vuoto, esegue il seed Staging (core + catalogo, senza vettori).

### 2.4 Verificare servizi attivi

```bash
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

Servizi attesi:
- `meepleai-postgres` — healthy
- `meepleai-redis` — healthy
- `meepleai-api` — healthy (dipende da postgres+redis)
- `meepleai-web` — running
- `meepleai-traefik` — running (ports 80, 443)
- `meepleai-grafana` — running (opzionale)
- `meepleai-prometheus` — running (opzionale)

### 2.4 Verificare health

```bash
# Health check API
curl -sf https://meepleai.app/api/v1/health | jq .

# Homepage web
curl -sf -o /dev/null -w "%{http_code}" https://meepleai.app

# Certificato SSL
curl -vI https://meepleai.app 2>&1 | grep "SSL certificate"
```

---

## Fase 3: Setup Iniziale App

### 3.1 Primo login admin

1. Naviga a `https://meepleai.app`
2. Registrati con email/password di `admin.secret` (`ADMIN_EMAIL` / `ADMIN_PASSWORD`)
3. Il primo utente diventa automaticamente admin
4. Verifica accesso a `https://meepleai.app/admin`

### 3.2 Attivare modalità invite-only

```bash
# Via API (dall'admin):
curl -X PUT https://meepleai.app/api/v1/admin/settings/registration-mode \
  -H "Content-Type: application/json" \
  -H "Cookie: <session-cookie>" \
  -d '{"enabled": false}'
```

Oppure dall'admin UI: Admin → Users → Settings → Registration Mode → Disable Public

### 3.3 Seed catalogo giochi

Se hai usato lo snapshot (Fase 2.2), il catalogo è già popolato con i 32 giochi + vettori.
Verifica prima:

```bash
curl -sf https://meepleai.app/api/v1/games?pageSize=5 \
  -H "Cookie: <session-cookie>" | jq '.totalCount'
# Se >= 32: lo snapshot ha funzionato, salta al 3.4
```

**Se lo snapshot NON è disponibile** o vuoi aggiungere altri giochi al catalogo:

```bash
# Opzione A: Bulk import via API (solo metadati BGG, no PDF/vettori)
curl -X POST https://meepleai.app/api/v1/admin/games/bulk-import \
  -H "Content-Type: application/json" \
  -H "Cookie: <session-cookie>" \
  -d @/opt/meepleai/repo/infra/scripts/bgg-top-games-bulk-import.json

# Opzione B: Seed completo con PDF (richiede rulebook PDF locali)
cd /opt/meepleai/repo/infra/scripts
./seed-all-games-staging.sh
```

**Nota**: L'opzione A importa solo i metadati da BGG (titolo, immagine, rating, etc.) —
i giochi appariranno nel catalogo ma senza RAG chat. L'opzione B (o lo snapshot) include
i vettori pre-indicizzati che abilitano la chat AI sulle regole.

### 3.4 Verificare catalogo e RAG

```bash
# Conteggio giochi
curl -sf https://meepleai.app/api/v1/games?pageSize=5 \
  -H "Cookie: <session-cookie>" | jq '.totalCount'

# Verificare che i vettori esistano (giochi con RAG abilitato)
curl -sf https://meepleai.app/api/v1/admin/knowledge-base/documents?pageSize=5 \
  -H "Cookie: <session-cookie>" | jq '.totalCount'
# Deve restituire >= 30 se lo snapshot è stato applicato
```

---

## Fase 4: Invitare Alpha Tester

### 4.1 Preparare lista inviti

Crea un CSV con gli indirizzi email dei 10 tester:

```csv
email,displayName,role
tester1@email.com,Nome Tester 1,User
tester2@email.com,Nome Tester 2,User
```

### 4.2 Inviare inviti

**Via Admin UI** (raccomandato):
1. Admin → Users → Invitations
2. "Send Invitation" per singolo invito, oppure "Bulk Import" per CSV
3. I tester riceveranno email con link di attivazione

**Via API**:
```bash
# Singolo invito
curl -X POST https://meepleai.app/api/v1/admin/users/invite \
  -H "Content-Type: application/json" \
  -H "Cookie: <session-cookie>" \
  -d '{"email": "tester@email.com", "displayName": "Tester", "role": "User"}'

# Bulk
curl -X POST https://meepleai.app/api/v1/admin/users/bulk/invite \
  -H "Content-Type: multipart/form-data" \
  -H "Cookie: <session-cookie>" \
  -F "file=@invites.csv"
```

### 4.3 Verificare inviti

```bash
# Admin UI: Admin → Users → Invitations → filtro "Pending"
# Oppure via API:
curl -sf https://meepleai.app/api/v1/admin/users/invitations?status=Pending \
  -H "Cookie: <session-cookie>" | jq '.totalCount'
```

**Nota**: Se SMTP non è configurato (`email.secret`), gli inviti vengono creati ma l'email non parte. In quel caso, copia il link di attivazione dall'admin UI e invialo manualmente.

---

## Fase 5: Verifiche Post-Deploy

### 5.1 Golden Path Test (manuale)

Esegui come tester (non admin):

1. [ ] Apri link invito → attiva account → login
2. [ ] Browse catalogo giochi → cerca un gioco
3. [ ] Aggiungi gioco alla libreria
4. [ ] (Se PDF disponibile) Apri chat → fai domanda sulle regole
5. [ ] Verifica risposta AI pertinente
6. [ ] Aggiungi gioco alla wishlist
7. [ ] Logout → login di nuovo

### 5.2 Health check continuo

```bash
# Sul server, verifica logs API:
docker logs meepleai-api --tail=50 --follow

# Grafana (se monitoring attivo):
# https://meepleai.app/grafana (login con credenziali monitoring.secret)
```

### 5.3 Backup verificato

```bash
cd /opt/meepleai/repo/infra
make backup              # Backup manuale DB
make backup-verify       # Verifica integrità
```

---

## Troubleshooting Rapido

| Problema | Comando |
|----------|---------|
| API non parte | `docker logs meepleai-api --tail=100` |
| DB connection error | `docker exec meepleai-postgres pg_isready` |
| Redis error | `docker exec meepleai-redis redis-cli ping` |
| SSL non funziona | `docker logs meepleai-traefik --tail=50` |
| Web 502/503 | `docker restart meepleai-web` |
| Riavvio completo | `make staging-down && make staging-core` |
| Spazio disco | `df -h && docker system prune -f` |

---

## Configurazione Opzionale (Post-Launch)

### Email SMTP (per inviti automatici)

Configura `secrets/email.secret`:
```
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=noreply@meepleai.app
SMTP_PASSWORD=<app-password>
SMTP_FROM_EMAIL=noreply@meepleai.app
```

### OAuth Login (Google/Discord)

Configura `secrets/oauth.secret` e imposta callback URLs:
- Google: `https://meepleai.app/api/v1/auth/oauth/google/callback`
- Discord: `https://meepleai.app/api/v1/auth/oauth/discord/callback`

Poi attiva via admin:
```bash
curl -X PUT https://meepleai.app/api/v1/admin/settings \
  -H "Content-Type: application/json" \
  -d '{"key": "oauth_login", "value": "true"}'
```

### S3/R2 Storage (per PDF)

Configura `secrets/storage.secret` con credenziali Cloudflare R2.
Senza S3, i PDF vengono salvati sul volume Docker locale.

### AI Services (embedding locale)

Se vuoi embedding locale (senza OpenRouter per embeddings):
```bash
make staging   # Invece di staging-core — aggiunge embedding + reranker
```

**Attenzione**: Richiede CAX31+ (16GB RAM) o swap configurato.

---

*Ultimo aggiornamento: 2026-04-18*
