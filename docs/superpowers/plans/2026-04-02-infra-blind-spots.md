# Analisi Blind Spot Infrastrutturali — MeepleAI

**Data**: 2026-04-02
**Fonte**: Spec-panel ultrathink — esperti Nygard, Hightower, Newman, Wiegers, Hohpe
**Branch**: feature/infra-spec-panel-fixes
**Stato**: Analisi + raccomandazioni (non ancora implementati)

---

## Overview

Quattro blind spot identificati dopo le correzioni immediate del spec-panel. Richiedono decisioni architetturali o investimenti infrastrutturali significativi — non sono fix semplici.

| # | Blind Spot | Rischio | Effort |
|---|---|---|---|
| 1 | Disaster Recovery | 🔴 Critico | Alto |
| 2 | Capacity Planning | 🟡 Alto | Medio |
| 3 | Secret Rotation | 🟡 Alto | Medio |
| 4 | Rate Limiting per utente | 🟢 Medio | Basso |

---

## 1. Disaster Recovery (DR)

### Problema attuale

Il progetto gira su **un singolo server Hetzner CAX21** (`204.168.135.69`). Non esiste nessun piano documentato per rispondere a:

- Crash del server fisico o del disco
- Cancellazione accidentale di volumi Docker
- Compromissione del server (attacco, cryptolocker)
- Problemi di provisioning Hetzner (raro ma possibile)
- Errore operativo (`docker compose down -v` invece di `down`)

**Dati a rischio**:
- PostgreSQL: dati utenti, sessioni, ownership, KB, RAG embeddings
- Redis: sessioni attive (tollerabile — rilogin)
- MinIO/pdf_uploads: PDF rulebook caricati dagli utenti
- Grafana/Prometheus: dati metrici storici (non business-critical)

### Analisi RPO/RTO attuale

| Metrica | Valore attuale | Target raccomandato |
|---|---|---|
| RPO (max perdita dati) | Illimitato — nessun backup automatico | ≤ 24h staging / ≤ 1h prod |
| RTO (tempo ripristino) | Giorni (rebuild server + data loss) | ≤ 4h staging / ≤ 1h prod |

### Piano raccomandato

#### Fase 1 — Backup automatico PostgreSQL (priorità immediata)

Aggiungere un service `pgbackup` nei compose staging/prod che esegue `pg_dump` periodicamente e carica su Cloudflare R2 (già configurato per storage):

```yaml
# Aggiungere a docker-compose.yml (profile: backup)
pgbackup:
  image: prodrigestivill/postgres-backup-local:16
  container_name: meepleai-pgbackup
  restart: unless-stopped
  profiles: [backup]
  environment:
    POSTGRES_HOST: postgres
    POSTGRES_DB: ${POSTGRES_DB}
    POSTGRES_USER: ${POSTGRES_USER}
    POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    SCHEDULE: "@daily"           # Ogni giorno a mezzanotte
    BACKUP_KEEP_DAYS: 7          # 7 giorni locali
    BACKUP_KEEP_WEEKS: 4         # 4 settimane
    BACKUP_KEEP_MONTHS: 6        # 6 mesi
    POSTGRES_EXTRA_OPTS: "-Z 6 --schema=public --blobs"
  volumes:
    - pgbackup_data:/backups
  depends_on:
    postgres:
      condition: service_healthy
  networks:
    - meepleai
```

**Alternativa con S3 upload**: Usare `pgbackup` + `rclone` sidecar per sync automatico su R2 dopo ogni dump.

#### Fase 2 — Volume snapshot Hetzner

Hetzner offre volume snapshot via API. Aggiungere uno script/cron che esegue snapshot del volume Docker prima di ogni deploy major:

```bash
# scripts/hetzner-snapshot.sh
hcloud volume snapshot create meepleai-data --description "pre-deploy-$(date +%Y%m%d)"
```

Richede `hcloud` CLI e API token Hetzner nel secret vault.

#### Fase 3 — Runbook DR documentato

Creare `docs/operations/disaster-recovery.md` con procedure step-by-step:

1. **Scenario: server irraggiungibile** → come fare failover su nuovo server Hetzner usando backup R2
2. **Scenario: volume corrotto** → restore da pg_dump + riavvio servizi
3. **Scenario: errore operativo** (`down -v`) → restore da snapshot Hetzner
4. **Test DR**: procedure da eseguire trimestralmente per verificare che i backup funzionino

#### Fase 4 — Multi-AZ (produzione finale)

Quando il prodotto è in produzione stabile, valutare:
- **Primary + Read Replica** PostgreSQL (Hetzner second server)
- **Backup server** in zona diversa (Hetzner NBG vs FSN)
- **DNS failover** via Cloudflare per cambiare target IP in < 5min

### Decision point

Per lo staging attuale, la **priorità immediata è il backup automatico** (Fase 1). Le fasi successive possono attendere il go-live produzione.

---

## 2. Capacity Planning

### Problema attuale

**Hetzner CAX21** (ARM64):
- CPU: 4 vCPU
- RAM: **8 GB**
- Disco: 80 GB SSD

**RAM richiesta da tutti i profili** (`make dev` / `make staging`):

| Servizio | Reservation | Limit |
|---|---|---|
| postgres | 1G | 2G |
| redis | 512M | 1G |
| api | 2G | 4G |
| web | 512M | 1G |
| embedding-service | 2G | 4G |
| reranker-service | 1G | 2G |
| unstructured-service | 1G | 2G |
| smoldocling-service | 2G | 4G |
| ollama | 4G | 8G |
| orchestration-service | 2G | 4G |
| prometheus | 1G | 2G |
| grafana | 512M | 1G |
| alertmanager | 256M | 512M |
| seq | 256M | 512M |
| node-exporter | 128M | 256M |
| cadvisor | 256M | 512M |
| **TOTALE reservations** | **~18 GB** | **~38 GB** |

**Il server CAX21 ha 8 GB**: i reservation totali superano di 2.25x la RAM disponibile. In staging con tutti i profili attivi, il kernel OOM killer interverrà.

### Analisi per profilo

| Profilo | RAM reservation | Compatibilità CAX21 |
|---|---|---|
| `make dev-core` | ~4G | ✅ OK |
| `make staging` (core + ai + monitoring) | ~18G | ❌ OOM inevitabile |
| `make staging` (core + monitoring) | ~6G | ✅ OK |
| `make staging` (core + ai) | ~14G | ❌ OOM |

### Raccomandazioni

#### Opzione A — Upgrade server (raccomandato per staging con AI)

Hetzner CAX31: 8 vCPU, **16 GB RAM**, 160 GB — costo ~15€/mese vs ~8€/mese CAX21.

Anche con CAX31, l'intera stack AI + monitoring supera i 18 GB reservation. Il profilo ai + monitoring + core è al limite.

Hetzner CAX41: 16 vCPU, **32 GB RAM** — ~28€/mese. Questo copre tutti i profili.

#### Opzione B — Profili selettivi in staging (scelta pragmatica)

Non avviare tutti i profili simultaneamente in staging. Definire due "mode":

```bash
# staging-full: tutti i servizi AI attivi (demo, test AI features)
make staging-ai  # core + ai + monitoring (senza ollama)

# staging-core: solo backend/frontend/monitoring (CI/CD, smoke test)
make staging-core  # core + monitoring
```

Rimuovere `ollama` dal profilo staging di default (8G RAM da solo). L'embedding usa `multilingual-e5-base` via `embedding-service`, non ollama. Ollama è necessario solo per sviluppo locale di nuovi modelli.

#### Opzione C — Resource limits più aggressivi in staging

Abbassare i resource limits nei compose.staging.yml per alcuni servizi:

```yaml
# compose.staging.yml — override resource limits
smoldocling-service:
  deploy:
    resources:
      limits:
        cpus: '1'
        memory: 2G      # ridotto da 4G
      reservations:
        cpus: '0.5'
        memory: 1G      # ridotto da 2G

ollama:
  deploy:
    resources:
      limits:
        cpus: '2'
        memory: 4G      # ridotto da 8G (meno modelli concorrenti)
```

**Nota**: ridurre limits degrada performance, ma evita OOM.

#### Raccomandazione finale

Per il lancio alpha/beta: **Opzione B** (profili selettivi) + **Opzione C** (limits staging). Questo non richiede spesa aggiuntiva.

Per produzione: **Upgrade a CAX41** o servizio cloud managed (Railway, Fly.io, Render) che scala verticalmente su richiesta.

---

## 3. Secret Rotation

### Problema attuale

I secret sono file `.secret` statici gestiti manualmente:
- Creati una volta con `make secrets-setup`
- Sincronizzati dal server staging con `make secrets-sync`
- Nessuna scadenza, nessuna rotazione automatica
- Non c'è audit di chi ha accesso ai secret

**Conseguenza**: Se un secret viene compromesso (leak git accidentale, accesso non autorizzato al server), non c'è meccanismo per rilevarlo o revocarlo rapidamente.

### Tipi di secret e rischio

| Secret | Rischio compromissione | Rotazione raccomandato |
|---|---|---|
| `jwt.secret` (signing key) | 🔴 Critico — tutti i JWT validi | 90 giorni o immediata se sospetto |
| `database.secret` (password Postgres) | 🔴 Critico — accesso DB diretto | 180 giorni |
| `openrouter.secret` (API key LLM) | 🟡 Alto — costo fatture illimitate | 30 giorni |
| `oauth.secret` (Google/GitHub app) | 🟡 Alto — OAuth flow hijack | 180 giorni |
| `admin.secret` | 🟡 Alto — accesso admin panel | 90 giorni |
| `redis.secret` | 🟢 Medio — solo rete interna | 360 giorni |
| `monitoring.secret` | 🟢 Basso — Grafana access | 360 giorni |

### Soluzioni per livello di complessità

#### Livello 1 — Minimo: checklist manuale (0 effort infrastrutturale)

Creare un calendario di rotazione secret in `docs/operations/secret-rotation-schedule.md`:

```
Q1 2026: openrouter.secret, jwt.secret, admin.secret
Q2 2026: database.secret, oauth.secret
Q3 2026: openrouter.secret, jwt.secret
...
```

Script di supporto per rotazione sicura:

```bash
# scripts/rotate-secret.sh <secret-name> <new-value>
# 1. Aggiorna file .secret
# 2. Restart servizi impattati
# 3. Verifica health
# 4. Log rotazione in audit trail
```

#### Livello 2 — Consigliato: Infisical OSS (già sperimentato nel repo)

La directory `infra/experimental/` contiene già un `docker-compose.infisical.yml`. Infisical è un secret manager open-source con:
- Rotazione automatica programmabile
- Audit log di ogni accesso
- Client SDK per .NET e Next.js
- Sync automatico agli ambienti

Passare da file `.secret` a Infisical richiederebbe:
1. Deploy Infisical self-hosted (o Infisical Cloud free tier)
2. Migrazione secret esistenti su Infisical
3. Modifica `load-secrets-env.sh` per leggere da Infisical invece di file
4. Configurare policy di rotazione

Effort stimato: 2-3 giorni. Alta priorità pre-produzione.

#### Livello 3 — Alternativa: HashiCorp Vault

Più potente di Infisical ma più complesso da operare. Eccessivo per la scala attuale.

### Priorità

Avviare con **Livello 1** (checklist) immediato. Pianificare **Livello 2** (Infisical) per il trimestre pre-produzione.

---

## 4. Rate Limiting per Utente Autenticato

### Problema attuale

Il rate limiting Traefik attuale è **per IP**:

```yaml
# middlewares.yml
rate-limit-api:
  rateLimit:
    average: 300
    period: 1m
    burst: 100
    sourceCriterion:
      ipStrategy:
        depth: 1   # ← X-Forwarded-For, quindi IP del client
```

**Problemi**:

1. **Utenti dietro NAT/proxy condiviso** (università, aziende): 50 utenti condividono un IP → rate limit colpisce tutti quando uno abusa
2. **Un utente autenticato** può creare molti account diversi con VPN e bypassare il limite
3. **Nessun limite per risorsa costosa**: le API RAG/embedding costano molto di più computazionalmente di una GET games, ma hanno lo stesso rate limit

### Raccomandazioni

#### A — Rate limiting per JWT claim (breve termine)

Il rate limiting per utente autenticato si implementa a livello applicativo (.NET API), non Traefik. Aggiungere middleware ASP.NET con `AspNetCoreRateLimit` o la libreria built-in .NET 8+:

```csharp
// Program.cs
builder.Services.AddRateLimiter(options => {
    options.AddPolicy("authenticated-user", context => {
        var userId = context.User?.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString();
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1)
        });
    });

    // Rate limit stricter per endpoint RAG (costosi)
    options.AddPolicy("rag-expensive", context => {
        var userId = context.User?.FindFirst("sub")?.Value;
        return RateLimitPartition.GetFixedWindowLimiter($"rag:{userId}", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 20,     // solo 20 query RAG/min
            Window = TimeSpan.FromMinutes(1)
        });
    });
});
```

Applicare `[EnableRateLimiting("rag-expensive")]` agli endpoint `/api/v1/kb/chat` e `/api/v1/kb/search`.

#### B — Redis-based rate limiting (distribuito)

Usare Redis per rate limiting distribuito che persiste tra restart dell'API. Libreria `redis-rate-limiter` o implementazione custom con `INCR` + `EXPIRE`.

Vantaggio: se si scala orizzontalmente l'API, il rate limit è condiviso tra istanze.

#### C — Traefik per IP + applicativo per utente (difesa in profondità)

Mantenere il rate limit Traefik per IP come protezione contro attacchi senza autenticazione (scraping, bot), e aggiungere il rate limit applicativo per utenti autenticati. Due livelli complementari.

### Priorità

Per l'alpha: non bloccante. Gli utenti sono invitati e il numero è basso.

Per produzione: implementare **Opzione A** (applicativo per utente) prima del lancio pubblico, specialmente per endpoint RAG che chiamano LLM con costo pay-per-token.

---

## Piano di Esecuzione

| Blind Spot | Azione immediata | Sprint |
|---|---|---|
| DR | Aggiungere pgbackup service + cron R2 | P1 — prima del prossimo deploy prod |
| Capacity | Adottare profili selettivi staging + upgrade CAX31 | P1 — entro fine aprile |
| Secret Rotation | Creare checklist rotazione + pianificare Infisical | P2 — pre-produzione |
| Rate Limiting | Implementare rate limit per-user nel .NET API | P2 — prima del lancio pubblico |

---

## Note Architetturali

**Single point of failure consapevole**: La scelta di un singolo server Hetzner è una decisione pragmatica per lo stage attuale (alpha/beta). Il costo di un'architettura multi-nodo (load balancer, replica DB, HA Redis) non è giustificato prima del product-market fit. La priorità è il backup automatico per proteggere i dati, non la disponibilità 99.99%.

**Evoluzione prevista**: Quando il prodotto supera i 1000 utenti attivi, rivalutare il passaggio a un cloud managed (Railway, Fly.io, Render, o AWS/GCP managed services) che gestisce scaling, backup e HA automaticamente.
