# Scaling Runbook

Guida per scalare l'infrastruttura MeepleAI in base alla crescita.

## Indicatori di Scaling

### Quando Scalare Verticalmente

| Metrica | Soglia Warning | Soglia Critica | Azione |
|---------|----------------|----------------|--------|
| CPU | >70% sustained | >85% sustained | Upgrade VPS |
| RAM | >80% | >90% | Upgrade VPS |
| Disco | >70% | >85% | Expand storage |
| Response time | >500ms avg | >1s avg | Investigate + scale |
| Error rate | >1% | >5% | Investigate + scale |

### Quando Scalare Orizzontalmente

- Singolo VPS upgraded non basta
- Necessità alta disponibilità (HA)
- Separazione geografica richiesta
- Budget disponibile per complessità aggiuntiva

### Quando Migrare a Managed Services

- Tempo manutenzione >15h/mese
- Downtime non accettabile per business
- Team si espande (focus su sviluppo)
- Budget >€200/mese disponibile
- Compliance richiede certificazioni specifiche

---

## Scaling Verticale (VPS Upgrade)

### Opzioni Hetzner Cloud

| Piano | vCPU | RAM | Disco | Prezzo/mese | Use Case |
|-------|------|-----|-------|-------------|----------|
| **CPX21** | 3 | 4GB | 80GB | €8.98 | Development only |
| **CPX31** | 4 | 8GB | 160GB | €15.59 | Beta (attuale) |
| **CPX41** | 8 | 16GB | 240GB | €29.52 | Production small |
| **CPX51** | 16 | 32GB | 360GB | €65.18 | Production medium |
| **CCX13** | 2 | 8GB | 80GB | €12.49 | CPU-intensive |
| **CCX23** | 4 | 16GB | 160GB | €24.99 | Balanced workload |
| **CCX33** | 8 | 32GB | 240GB | €49.99 | High performance |
| **CCX43** | 16 | 64GB | 360GB | €99.99 | Enterprise |

### Procedura Upgrade VPS

**Metodo 1: Resize in-place (Downtime ~5-15 min)**

```
Hetzner Console → Server → Rescale:

1. BACKUP prima! /home/meepleai/scripts/backup-all.sh
2. Stop applicazione
   docker compose -f docker-compose.prod.yml down
3. Hetzner Console → Server → Power Off
4. Click "Rescale" → Seleziona nuovo piano
5. Conferma (volume dati preservato)
6. Attendi completamento (~5-10 min)
7. Power On
8. SSH e verifica
   docker compose -f docker-compose.prod.yml up -d
9. Health check
   /home/meepleai/scripts/health-check.sh
```

**Metodo 2: Migrazione a nuovo VPS (Downtime ~30-60 min)**

```bash
# Usa quando resize non disponibile o vuoi zero-risk

# 1. Crea nuovo VPS con specs desiderate
# 2. Setup base sul nuovo VPS (vedi setup-guide-self-hosted.md)

# 3. Sul VECCHIO server - backup finale
/home/meepleai/scripts/backup-all.sh
docker compose -f docker-compose.prod.yml down

# 4. Copia backup al nuovo server
scp -r /home/meepleai/backups/* meepleai@NEW_IP:/home/meepleai/backups/

# 5. Sul NUOVO server - restore
cd /home/meepleai/app/infra
docker compose -f docker-compose.prod.yml up -d postgres redis qdrant
sleep 60

# Restore PostgreSQL
gunzip -c /home/meepleai/backups/postgres/dump_LATEST.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai

# Restore Redis
docker cp /home/meepleai/backups/redis/dump_LATEST.rdb meepleai-redis:/data/dump.rdb
docker restart meepleai-redis

# Start applicazione
docker compose -f docker-compose.prod.yml up -d

# 6. Aggiorna DNS su Cloudflare con nuovo IP
# 7. Attendi propagazione DNS (1-5 min con TTL basso)
# 8. Verifica tutto funziona
# 9. Elimina vecchio VPS dopo 24-48h di stabilità
```

### Ottimizzazione Prima di Scalare

Prima di spendere per upgrade, verifica ottimizzazioni:

```bash
# 1. Verifica query lente PostgreSQL
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT query, calls, mean_time, total_time
   FROM pg_stat_statements
   ORDER BY total_time DESC LIMIT 10;"

# 2. Verifica indici mancanti
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT schemaname, tablename, attname, null_frac, avg_width, n_distinct
   FROM pg_stats
   WHERE schemaname = 'public';"

# 3. Verifica cache hit ratio Redis
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" INFO stats | grep hit

# 4. Verifica memory usage Qdrant
curl http://localhost:6333/telemetry -H "api-key: $QDRANT_API_KEY" | jq '.result.memory'

# 5. Analizza Docker resource usage
docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}"
```

---

## Scaling Orizzontale

### Architettura Multi-VPS

```
                    ┌─────────────────┐
                    │   Cloudflare    │
                    │   (Load Balancer)│
                    └────────┬────────┘
                             │
              ┌──────────────┼──────────────┐
              │              │              │
       ┌──────▼──────┐ ┌─────▼─────┐ ┌──────▼──────┐
       │   VPS #1    │ │  VPS #2   │ │   VPS #3    │
       │ Application │ │Application│ │  Database   │
       │   + API     │ │  + API    │ │ PostgreSQL  │
       │   + Redis   │ │  (replica)│ │   Qdrant    │
       └─────────────┘ └───────────┘ └─────────────┘
```

### Step 1: Separare Database (Prima Espansione)

**Quando**: CPU database >60% OR RAM >70% OR bisogno backup più robusto

```bash
# Crea VPS dedicato per database
# Hetzner Console → New Server → CPX31 o superiore

# Setup sul nuovo VPS Database
apt update && apt install -y docker.io docker-compose-plugin

# docker-compose.db.yml
version: '3.8'
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: meepleai
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: meepleai
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"  # Apri solo su rete privata Hetzner!
    deploy:
      resources:
        limits:
          memory: 8G

  qdrant:
    image: qdrant/qdrant:latest
    environment:
      QDRANT__SERVICE__API_KEY: ${QDRANT_API_KEY}
    volumes:
      - qdrant_data:/qdrant/storage
    ports:
      - "6333:6333"

volumes:
  postgres_data:
  qdrant_data:
```

```bash
# Configura rete privata Hetzner tra i VPS
# Hetzner Console → Network → Create Network

# Aggiorna connection string su VPS applicazione
# .env: DATABASE_URL=postgresql://meepleai:pass@10.0.0.2:5432/meepleai
```

### Step 2: Load Balancing (Seconda Espansione)

**Quando**: Singola istanza API non gestisce il carico

```yaml
# Cloudflare Load Balancing (opzionale, €5/mese)
# Oppure usa Cloudflare DNS round-robin (gratuito)

# Cloudflare Dashboard → DNS:
# api.meepleai.com → A → VPS1_IP
# api.meepleai.com → A → VPS2_IP
```

**Considerazioni per Multi-Instance**:

```yaml
# Requisiti per stateless API:
session_storage: Redis (condiviso tra istanze)
file_uploads: S3/R2 (non filesystem locale)
cache: Redis (condiviso)
background_jobs: Separato o coordinato

# Session sharing via Redis
# Configura Redis su VPS dedicato o managed
REDIS_URL=redis://:password@10.0.0.3:6379
```

### Step 3: Read Replicas PostgreSQL

**Quando**: Query lettura pesanti, reporting, analytics

```bash
# Setup PostgreSQL streaming replication

# Primary (master)
# postgresql.conf
wal_level = replica
max_wal_senders = 3
wal_keep_size = 64MB

# pg_hba.conf
host replication replicator 10.0.0.0/24 md5

# Replica
# recovery.conf / postgresql.auto.conf
primary_conninfo = 'host=10.0.0.2 port=5432 user=replicator password=...'
```

---

## Migrazione a Managed Services

### Path Consigliato

```
Fase 1 (Self-hosted): €20-25/mese
    ↓ Trigger: >15h/mese manutenzione O business-critical
Fase 2 (Hybrid): €50-80/mese
    - PostgreSQL → Neon (managed)
    - Redis/Qdrant → self-hosted
    ↓ Trigger: Crescita utenti O team espanso
Fase 3 (Full Managed): €100-200/mese
    - PostgreSQL → Neon Pro
    - Redis → Upstash
    - Qdrant → Qdrant Cloud
    - VPS → solo per servizi Python custom
```

### Migrazione PostgreSQL → Neon

```bash
# 1. Crea database Neon
# neon.tech → New Project → Copy connection string

# 2. Export da self-hosted
docker exec meepleai-postgres pg_dump -U meepleai -Fc meepleai > backup.dump

# 3. Import su Neon
pg_restore -h ep-xxx.eu-central-1.aws.neon.tech -U meepleai -d meepleai backup.dump

# 4. Verifica dati
psql $NEON_CONNECTION_STRING -c "SELECT count(*) FROM users;"

# 5. Aggiorna connection string in .env
DATABASE_URL=postgresql://user:pass@ep-xxx.eu-central-1.aws.neon.tech/meepleai?sslmode=require

# 6. Deploy e verifica
docker compose -f docker-compose.prod.yml restart api

# 7. Dopo 48h stabilità, rimuovi PostgreSQL locale
docker compose -f docker-compose.prod.yml stop postgres
docker volume rm infra_postgres_data
```

### Migrazione Redis → Upstash

```bash
# 1. Crea database Upstash
# upstash.com → Create Database → EU region

# 2. Export chiavi importanti (se necessario)
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" --rdb /data/dump.rdb
docker cp meepleai-redis:/data/dump.rdb ./redis_export.rdb

# 3. Per applicazioni stateless, salta import
# Redis cache si ricostruisce automaticamente

# 4. Aggiorna connection string
REDIS_URL=rediss://default:xxx@eu1-xxx.upstash.io:6379

# 5. Deploy e verifica
docker compose -f docker-compose.prod.yml restart api

# 6. Rimuovi Redis locale dopo stabilità
```

### Migrazione Qdrant → Qdrant Cloud

```bash
# 1. Crea cluster Qdrant Cloud
# cloud.qdrant.io → Create Cluster → EU region

# 2. Export collections
curl -X POST "http://localhost:6333/collections/documents/snapshots" \
  -H "api-key: $QDRANT_API_KEY"

# Scarica snapshot
SNAPSHOT=$(curl -s "http://localhost:6333/collections/documents/snapshots" \
  -H "api-key: $QDRANT_API_KEY" | jq -r '.result[-1].name')
curl -o documents.snapshot \
  "http://localhost:6333/collections/documents/snapshots/$SNAPSHOT" \
  -H "api-key: $QDRANT_API_KEY"

# 3. Upload su Qdrant Cloud
# Via dashboard o API con nuovo endpoint

# 4. Aggiorna configuration
QDRANT_URL=https://xxx-xxx.eu-central.aws.cloud.qdrant.io:6333
QDRANT_API_KEY=new_cloud_api_key

# 5. Deploy e verifica embedding/search funzionano
```

---

## Costi Scaling

### Confronto Costi per Fase

| Fase | Self-Hosted | Hybrid | Full Managed |
|------|-------------|--------|--------------|
| **PostgreSQL** | €0 | €19 (Neon Launch) | €69 (Neon Scale) |
| **Redis** | €0 | €0 | €10 (Upstash Pro) |
| **Qdrant** | €0 | €0 | €25 (Qdrant Cloud) |
| **VPS** | €15-30 | €15-30 | €8-15 |
| **Cloudflare** | €0 | €0-20 | €0-20 |
| **Totale** | €15-30 | €35-70 | €110-140 |

### ROI Scaling

```
Break-even per managed services:

Costo manutenzione self-hosted: ~€50/h (tuo tempo)
Ore manutenzione/mese: 5-10h = €250-500 valore

Se managed costa €80/mese extra ma risparmia 6h:
ROI = (6h × €50) - €80 = €220/mese positivo

Considera anche:
- Stress ridotto
- Focus su sviluppo
- SLA garantiti
- Backup automatici
```

---

## Monitoraggio per Scaling Decisions

### Dashboard Metriche Chiave

```bash
#!/bin/bash
# /home/meepleai/scripts/scaling-metrics.sh

echo "===== SCALING METRICS $(date) ====="

# CPU trend (ultimi 5 min)
echo "CPU Usage (5 min avg):"
top -bn1 | grep "Cpu(s)" | awk '{print 100 - $8"%"}'

# Memory trend
echo "Memory Usage:"
free -h | grep Mem | awk '{print $3"/"$2" ("int($3/$2*100)"%)"}'

# Disk trend
echo "Disk Usage:"
df -h / | tail -1 | awk '{print $3"/"$2" ("$5")"}'

# PostgreSQL connections
echo "PostgreSQL Connections:"
docker exec meepleai-postgres psql -U meepleai -tAc \
  "SELECT count(*) FROM pg_stat_activity;"

# Response times (se hai logging)
echo "API Response Time (if available):"
docker logs meepleai-api --since=5m 2>&1 | grep -oP 'took \K[0-9]+ms' | \
  awk '{sum+=$1; count++} END {if(count>0) print sum/count"ms avg"}'

# Container stats
echo "Container Resources:"
docker stats --no-stream --format "{{.Name}}: CPU={{.CPUPerc}} MEM={{.MemPerc}}"

echo "===== END METRICS ====="
```

### Alert Thresholds

```bash
# Aggiungi a crontab per alerting
# */5 * * * * /home/meepleai/scripts/scaling-alert.sh

#!/bin/bash
# /home/meepleai/scripts/scaling-alert.sh

ALERT_EMAIL="admin@meepleai.com"

# CPU check
CPU=$(top -bn1 | grep "Cpu(s)" | awk '{print 100 - $8}' | cut -d. -f1)
if [ "$CPU" -gt 80 ]; then
  echo "SCALING ALERT: CPU at $CPU% - Consider VPS upgrade" | \
    mail -s "MeepleAI Scaling Alert: High CPU" $ALERT_EMAIL
fi

# Memory check
MEM=$(free | grep Mem | awk '{printf "%.0f", $3/$2 * 100}')
if [ "$MEM" -gt 85 ]; then
  echo "SCALING ALERT: Memory at $MEM% - Consider VPS upgrade" | \
    mail -s "MeepleAI Scaling Alert: High Memory" $ALERT_EMAIL
fi

# Disk check
DISK=$(df / | tail -1 | awk '{print $5}' | tr -d '%')
if [ "$DISK" -gt 80 ]; then
  echo "SCALING ALERT: Disk at $DISK% - Expand storage or cleanup" | \
    mail -s "MeepleAI Scaling Alert: High Disk" $ALERT_EMAIL
fi
```

---

## Decision Matrix

### Scala Verticalmente Se:

- [ ] CPU >70% per >1 ora sustained
- [ ] RAM >80% sustained
- [ ] Response time >500ms avg
- [ ] Budget limitato (<€50/mese)
- [ ] Team piccolo (1-2 persone)
- [ ] Utenti <1000

### Scala Orizzontalmente Se:

- [ ] Singolo VPS max spec ancora insufficiente
- [ ] Requisiti HA (99.9%+ uptime)
- [ ] Distribuzione geografica necessaria
- [ ] Team >3 persone
- [ ] Utenti >1000

### Migra a Managed Se:

- [ ] Tempo manutenzione >15h/mese
- [ ] Business genera revenue significativo
- [ ] Compliance richiede certificazioni
- [ ] Team vuole focus 100% su sviluppo
- [ ] Budget >€150/mese disponibile
- [ ] Crescita rapida prevista

---

## Quick Reference

```bash
# ===== CHECK SCALING NEED =====
/home/meepleai/scripts/scaling-metrics.sh

# ===== VERTICAL SCALE =====
# Hetzner Console → Rescale (after backup!)

# ===== OPTIMIZE FIRST =====
# Check slow queries
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT * FROM pg_stat_statements ORDER BY total_time DESC LIMIT 5;"

# Check Docker resources
docker stats --no-stream

# ===== MIGRATION PREP =====
# Full backup before any migration
/home/meepleai/scripts/backup-all.sh

# Export for migration
docker exec meepleai-postgres pg_dump -U meepleai -Fc meepleai > migration_backup.dump
```
