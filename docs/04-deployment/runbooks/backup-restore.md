# Backup & Restore Runbook

Procedure per backup e restore di tutti i servizi MeepleAI.

## Strategia Backup

| Componente | Frequenza | Retention | Metodo |
|------------|-----------|-----------|--------|
| **PostgreSQL** | Giornaliero 3:00 | 7 giorni | pg_dumpall + gzip |
| **Redis** | Giornaliero 3:00 | 7 giorni | BGSAVE + copy |
| **Qdrant** | Giornaliero 3:00 | 7 giorni | Snapshot API |
| **VPS Snapshot** | Giornaliero | 7 giorni | Hetzner auto |
| **Secrets** | Manuale | Permanente | Password manager |

## Locations

```
/home/meepleai/backups/
├── postgres/
│   ├── dump_20260127_030000.sql.gz
│   ├── dump_20260126_030000.sql.gz
│   └── ...
├── redis/
│   ├── dump_20260127_030000.rdb
│   └── ...
├── qdrant/
│   ├── snapshot_20260127_030000.snapshot
│   └── ...
└── secrets-backup-YYYYMMDD.txt  # Copia sicura credenziali
```

---

## Backup Automatici

### Script Principale

Location: `/home/meepleai/scripts/backup-all.sh`

```bash
#!/bin/bash
# Eseguito da cron alle 3:00 AM

set -euo pipefail

BACKUP_DIR="/home/meepleai/backups"
DATE=$(date +%Y%m%d_%H%M%S)
RETENTION_DAYS=7
LOG="/home/meepleai/logs/backup.log"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG"; }

# PostgreSQL
log "Backing up PostgreSQL..."
docker exec meepleai-postgres pg_dumpall -U meepleai > "$BACKUP_DIR/postgres/dump_$DATE.sql"
gzip -f "$BACKUP_DIR/postgres/dump_$DATE.sql"

# Redis
log "Backing up Redis..."
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" BGSAVE
sleep 5
docker cp meepleai-redis:/data/dump.rdb "$BACKUP_DIR/redis/dump_$DATE.rdb"

# Qdrant
log "Backing up Qdrant..."
curl -s -X POST "http://localhost:6333/snapshots" -H "api-key: $QDRANT_API_KEY"
sleep 10
SNAPSHOT=$(curl -s "http://localhost:6333/snapshots" -H "api-key: $QDRANT_API_KEY" | jq -r '.result[-1].name')
curl -s -o "$BACKUP_DIR/qdrant/snapshot_$DATE.snapshot" \
  "http://localhost:6333/snapshots/$SNAPSHOT" -H "api-key: $QDRANT_API_KEY"

# Cleanup old backups
find "$BACKUP_DIR" -type f -mtime +$RETENTION_DAYS -delete

log "Backup completed"
```

### Cron Configuration

```bash
# Visualizza crontab attuale
crontab -l

# Dovrebbe contenere:
# 0 3 * * * /home/meepleai/scripts/backup-env.sh >> /home/meepleai/logs/backup-cron.log 2>&1
```

### Verifica Backup Funzionanti

```bash
# Controlla ultimo backup
ls -la /home/meepleai/backups/postgres/ | tail -3
ls -la /home/meepleai/backups/redis/ | tail -3
ls -la /home/meepleai/backups/qdrant/ | tail -3

# Controlla log
tail -50 /home/meepleai/logs/backup.log

# Verifica dimensioni (non devono essere 0)
du -sh /home/meepleai/backups/*
```

---

## Backup Manuale

### PostgreSQL

```bash
# Backup completo
docker exec meepleai-postgres pg_dumpall -U meepleai > backup_$(date +%Y%m%d).sql
gzip backup_$(date +%Y%m%d).sql

# Backup singolo database
docker exec meepleai-postgres pg_dump -U meepleai meepleai > backup_db_$(date +%Y%m%d).sql

# Backup singola tabella
docker exec meepleai-postgres pg_dump -U meepleai -t users meepleai > backup_users.sql
```

### Redis

```bash
# Trigger save
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" BGSAVE

# Attendi completamento
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" LASTSAVE

# Copia file
docker cp meepleai-redis:/data/dump.rdb ./redis_backup_$(date +%Y%m%d).rdb
```

### Qdrant

```bash
# Crea snapshot
curl -X POST "http://localhost:6333/snapshots" \
  -H "api-key: $QDRANT_API_KEY" \
  -H "Content-Type: application/json"

# Lista snapshot disponibili
curl "http://localhost:6333/snapshots" -H "api-key: $QDRANT_API_KEY" | jq

# Download snapshot specifico
curl -o qdrant_backup.snapshot \
  "http://localhost:6333/snapshots/SNAPSHOT_NAME" \
  -H "api-key: $QDRANT_API_KEY"

# Snapshot di singola collection
curl -X POST "http://localhost:6333/collections/documents/snapshots" \
  -H "api-key: $QDRANT_API_KEY"
```

### Secrets

```bash
# Backup secrets (salva in password manager!)
cat /home/meepleai/app/infra/secrets/*.secret > secrets_backup_$(date +%Y%m%d).txt

# NON lasciare sul server! Copia in locale e cancella
scp meepleai@YOUR_VPS_IP:~/secrets_backup_*.txt .
ssh meepleai@YOUR_VPS_IP "rm ~/secrets_backup_*.txt"
```

---

## Restore Procedures

### Restore PostgreSQL

#### Restore Completo

```bash
cd /home/meepleai/app/infra

# 1. Stop applicazione (evita scritture durante restore)
docker compose -f docker-compose.prod.yml stop api

# 2. Restore da backup
gunzip -c /home/meepleai/backups/postgres/dump_YYYYMMDD_HHMMSS.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai

# 3. Verifica
docker exec -it meepleai-postgres psql -U meepleai -d meepleai -c "\dt"
docker exec -it meepleai-postgres psql -U meepleai -d meepleai -c "SELECT count(*) FROM users;"

# 4. Riavvia applicazione
docker compose -f docker-compose.prod.yml start api
```

#### Restore Singolo Database

```bash
# Drop e ricrea database
docker exec -it meepleai-postgres psql -U meepleai -c "DROP DATABASE IF EXISTS meepleai;"
docker exec -it meepleai-postgres psql -U meepleai -c "CREATE DATABASE meepleai;"

# Restore
gunzip -c backup.sql.gz | docker exec -i meepleai-postgres psql -U meepleai -d meepleai
```

#### Restore Singola Tabella

```bash
# Estrai tabella da dump completo
gunzip -c dump.sql.gz > dump.sql

# Trova e estrai la tabella (esempio: users)
# Manualmente: apri dump.sql e copia sezione COPY users...

# Oppure usa pg_restore se hai formato custom
docker exec -i meepleai-postgres pg_restore -U meepleai -d meepleai -t users backup.dump
```

### Restore Redis

```bash
cd /home/meepleai/app/infra

# 1. Stop Redis
docker compose -f docker-compose.prod.yml stop redis

# 2. Copia backup nel container
docker cp /home/meepleai/backups/redis/dump_YYYYMMDD.rdb meepleai-redis:/data/dump.rdb

# 3. Fix permessi
docker exec meepleai-redis chown redis:redis /data/dump.rdb

# 4. Restart Redis
docker compose -f docker-compose.prod.yml start redis

# 5. Verifica
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" DBSIZE
```

### Restore Qdrant

#### Da Snapshot Completo

```bash
# 1. Copia snapshot nel container
docker cp /home/meepleai/backups/qdrant/snapshot_YYYYMMDD.snapshot \
  meepleai-qdrant:/qdrant/snapshots/

# 2. Restore via API
curl -X POST "http://localhost:6333/snapshots/recover" \
  -H "api-key: $QDRANT_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"location": "file:///qdrant/snapshots/snapshot_YYYYMMDD.snapshot"}'

# 3. Verifica
curl "http://localhost:6333/collections" -H "api-key: $QDRANT_API_KEY" | jq
```

#### Da Snapshot Collection

```bash
# Restore singola collection
curl -X POST "http://localhost:6333/collections/documents/snapshots/recover" \
  -H "api-key: $QDRANT_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"location": "file:///qdrant/snapshots/documents_snapshot.snapshot"}'
```

### Restore VPS da Hetzner Snapshot

```
Hetzner Console → Servers → meepleai-prod-01:

1. Stop server (Power → Power Off)
2. Vai a "Snapshots" tab
3. Seleziona snapshot desiderato
4. Click "Restore"
5. Conferma (ATTENZIONE: sovrascrive tutti i dati!)
6. Attendi completamento (10-30 min)
7. Power On server
8. Verifica SSH accesso
```

---

## Backup Offsite (Raccomandato)

### Setup Cloudflare R2

```bash
# Installa rclone
curl https://rclone.org/install.sh | sudo bash

# Configura R2
rclone config

# Scegli: n (new remote)
# Name: r2
# Storage: s3
# Provider: Cloudflare
# Access Key ID: [da Cloudflare R2]
# Secret Access Key: [da Cloudflare R2]
# Endpoint: https://ACCOUNT_ID.r2.cloudflarestorage.com

# Test connessione
rclone lsd r2:

# Crea bucket se non esiste
rclone mkdir r2:meepleai-backups
```

### Script Backup Offsite

```bash
#!/bin/bash
# /home/meepleai/scripts/backup-offsite.sh

# Sync backup locali a R2
rclone sync /home/meepleai/backups r2:meepleai-backups/$(hostname)/ \
  --transfers 4 \
  --checkers 8 \
  --log-file /home/meepleai/logs/rclone.log

echo "Offsite backup completed: $(date)"
```

### Cron per Offsite

```bash
# Aggiungi a crontab (dopo backup locale)
0 4 * * * /home/meepleai/scripts/backup-offsite.sh >> /home/meepleai/logs/backup-offsite.log 2>&1
```

---

## Verifica Integrità Backup

### Test Settimanale

```bash
#!/bin/bash
# /home/meepleai/scripts/verify-backups.sh

echo "=== Backup Verification $(date) ==="

# Verifica file esistono e non vuoti
for dir in postgres redis qdrant; do
  LATEST=$(ls -t /home/meepleai/backups/$dir/ | head -1)
  SIZE=$(stat -f%z "/home/meepleai/backups/$dir/$LATEST" 2>/dev/null || stat -c%s "/home/meepleai/backups/$dir/$LATEST")

  if [ "$SIZE" -gt 0 ]; then
    echo "✅ $dir: $LATEST ($SIZE bytes)"
  else
    echo "❌ $dir: EMPTY OR MISSING!"
  fi
done

# Verifica integrità PostgreSQL dump
LATEST_PG=$(ls -t /home/meepleai/backups/postgres/*.gz | head -1)
if gunzip -t "$LATEST_PG" 2>/dev/null; then
  echo "✅ PostgreSQL backup integrity OK"
else
  echo "❌ PostgreSQL backup CORRUPTED!"
fi

# Conta giorni di backup disponibili
PG_COUNT=$(ls /home/meepleai/backups/postgres/*.gz 2>/dev/null | wc -l)
echo "📊 PostgreSQL backups available: $PG_COUNT days"
```

### Test Mensile: Restore Completo

```bash
# 1. Crea database test
docker exec meepleai-postgres psql -U meepleai -c "CREATE DATABASE test_restore;"

# 2. Restore in database test
gunzip -c /home/meepleai/backups/postgres/dump_LATEST.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai -d test_restore

# 3. Verifica dati
docker exec meepleai-postgres psql -U meepleai -d test_restore -c "SELECT count(*) FROM users;"

# 4. Cleanup
docker exec meepleai-postgres psql -U meepleai -c "DROP DATABASE test_restore;"

echo "✅ Restore test completed successfully"
```

---

## Troubleshooting Backup

### Backup PostgreSQL Fallisce

```bash
# Verifica spazio disco
df -h

# Verifica PostgreSQL running
docker exec meepleai-postgres pg_isready

# Verifica connessione
docker exec meepleai-postgres psql -U meepleai -c "SELECT 1;"

# Check container logs
docker logs meepleai-postgres --tail=50
```

### Backup Qdrant Fallisce

```bash
# Verifica Qdrant health
curl http://localhost:6333/health

# Verifica API key
curl http://localhost:6333/collections -H "api-key: $QDRANT_API_KEY"

# Check spazio per snapshots
docker exec meepleai-qdrant df -h /qdrant/snapshots
```

### Restore Lento

```bash
# Per PostgreSQL, disabilita constraint durante restore
docker exec -i meepleai-postgres psql -U meepleai -c "SET session_replication_role = 'replica';"
# ... restore ...
docker exec -i meepleai-postgres psql -U meepleai -c "SET session_replication_role = 'origin';"

# Aumenta work_mem temporaneamente
docker exec -i meepleai-postgres psql -U meepleai -c "SET work_mem = '256MB';"
```

---

## Quick Reference

```bash
# ===== BACKUP MANUALE =====
# PostgreSQL
docker exec meepleai-postgres pg_dumpall -U meepleai | gzip > pg_backup.sql.gz

# Redis
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" BGSAVE
docker cp meepleai-redis:/data/dump.rdb ./redis_backup.rdb

# Qdrant
curl -X POST "http://localhost:6333/snapshots" -H "api-key: $QDRANT_API_KEY"

# ===== RESTORE =====
# PostgreSQL
gunzip -c pg_backup.sql.gz | docker exec -i meepleai-postgres psql -U meepleai

# Redis
docker cp redis_backup.rdb meepleai-redis:/data/dump.rdb
docker restart meepleai-redis

# ===== VERIFICA =====
ls -la /home/meepleai/backups/*/
tail -20 /home/meepleai/logs/backup.log
```
