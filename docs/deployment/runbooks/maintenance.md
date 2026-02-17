# Maintenance Runbook

Procedure di manutenzione ordinaria per MeepleAI.

## Calendario Manutenzione

| Task | Frequenza | Tempo Stimato | Downtime |
|------|-----------|---------------|----------|
| Verifica backup | Settimanale | 10 min | No |
| Aggiornamento Docker images | Mensile | 30-60 min | 5-10 min |
| Aggiornamento Ubuntu | Mensile | 30 min | 5-10 min |
| Rotazione credenziali | Trimestrale | 1 ora | 5-10 min |
| Pulizia disco | Mensile | 15 min | No |
| Review log/security | Settimanale | 20 min | No |
| Test restore backup | Mensile | 1 ora | No |
| Rinnovo SSL (auto) | Ogni 90 giorni | Auto | No |

---

## Manutenzione Giornaliera (Automatica)

### Cosa Gira Automaticamente

```bash
# Crontab attivo
crontab -l

# Dovrebbe mostrare:
# 0 3 * * * /home/meepleai/scripts/backup-env.sh     # Backup 3:00 AM
# */5 * * * * /home/meepleai/scripts/health-check.sh # Health ogni 5 min
```

### Verifica Rapida (2 min)

```bash
# Health check
/home/meepleai/scripts/health-check.sh

# Ultimo backup OK?
ls -la /home/meepleai/backups/postgres/ | tail -1
tail -5 /home/meepleai/logs/backup.log | grep -i "completed\|error"
```

---

## Manutenzione Settimanale

### Checklist Settimanale (15-20 min)

```bash
# 1. Verifica backup ultimi 7 giorni
ls -la /home/meepleai/backups/postgres/
# Devono esserci 7 file

# 2. Verifica spazio disco
df -h
# / deve essere <70%

# 3. Verifica RAM trend
free -h
docker stats --no-stream

# 4. Review errori nei log
docker compose -f docker-compose.prod.yml logs --since=168h 2>&1 | grep -c -i error
# Se >100, investigare

# 5. Verifica certificato SSL
echo | openssl s_client -connect meepleai.com:443 2>/dev/null | \
  openssl x509 -noout -enddate
# Deve scadere tra >30 giorni

# 6. Verifica security updates disponibili
sudo apt list --upgradable 2>/dev/null | grep -i security
```

### Script Checklist Settimanale

```bash
#!/bin/bash
# /home/meepleai/scripts/weekly-check.sh

echo "===== WEEKLY CHECK $(date) ====="

# Backup count
PG_BACKUPS=$(ls /home/meepleai/backups/postgres/*.gz 2>/dev/null | wc -l)
echo "PostgreSQL backups: $PG_BACKUPS (expected: 7)"

# Disk usage
DISK_USAGE=$(df / | tail -1 | awk '{print $5}' | tr -d '%')
echo "Disk usage: $DISK_USAGE% (warning if >70%)"

# Memory
MEM_USAGE=$(free | grep Mem | awk '{printf "%.0f", $3/$2 * 100}')
echo "Memory usage: $MEM_USAGE%"

# Errors last 7 days
ERROR_COUNT=$(docker compose -f /home/meepleai/app/infra/docker-compose.prod.yml logs --since=168h 2>&1 | grep -c -i error)
echo "Errors (7 days): $ERROR_COUNT"

# SSL expiry
SSL_EXPIRY=$(echo | openssl s_client -connect meepleai.com:443 2>/dev/null | openssl x509 -noout -enddate | cut -d= -f2)
echo "SSL expires: $SSL_EXPIRY"

# Security updates
SEC_UPDATES=$(sudo apt list --upgradable 2>/dev/null | grep -c -i security)
echo "Security updates pending: $SEC_UPDATES"

echo "===== END CHECK ====="
```

---

## Aggiornamento Docker Images (Mensile)

### Procedura Safe Update

```bash
cd /home/meepleai/app/infra

# 1. Backup PRIMA di aggiornare
/home/meepleai/scripts/backup-all.sh

# 2. Pull nuove immagini
docker compose -f docker-compose.prod.yml pull

# 3. Verifica quali immagini sono cambiate
docker images --format "{{.Repository}}:{{.Tag}} {{.CreatedAt}}" | head -20

# 4. Aggiorna UN servizio alla volta (meno rischio)

# Database (con cautela!)
docker compose -f docker-compose.prod.yml up -d --no-deps postgres
sleep 30
docker exec meepleai-postgres pg_isready
# Se OK, continua

# Redis
docker compose -f docker-compose.prod.yml up -d --no-deps redis
sleep 10
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" ping

# Qdrant
docker compose -f docker-compose.prod.yml up -d --no-deps qdrant
sleep 30
curl http://localhost:6333/health

# Applicazione
docker compose -f docker-compose.prod.yml up -d --no-deps embedding-service reranker-service
sleep 60
docker compose -f docker-compose.prod.yml up -d --no-deps api

# Traefik
docker compose -f docker-compose.prod.yml up -d --no-deps traefik

# 5. Verifica tutto funziona
/home/meepleai/scripts/health-check.sh
curl https://api.meepleai.com/health

# 6. Pulisci immagini vecchie
docker image prune -f
```

### Rollback se Problemi

```bash
# Se qualcosa non funziona dopo update:

# 1. Stop servizio problematico
docker compose -f docker-compose.prod.yml stop NOME_SERVIZIO

# 2. Torna a immagine precedente (se salvata)
docker compose -f docker-compose.prod.yml up -d --no-deps NOME_SERVIZIO

# 3. Se non hai immagine precedente, restore da backup
# Vedi backup-restore.md
```

---

## Aggiornamento Ubuntu (Mensile)

### Procedura

```bash
# 1. Backup prima
/home/meepleai/scripts/backup-all.sh

# 2. Check updates disponibili
sudo apt update
sudo apt list --upgradable

# 3. Applica security updates (safe)
sudo apt upgrade -y

# 4. Se richiede reboot
sudo reboot

# 5. Dopo reboot, verifica servizi
docker compose -f docker-compose.prod.yml ps
/home/meepleai/scripts/health-check.sh
```

### Upgrade Major Version (es. 24.04 → 26.04)

**ATTENZIONE**: Richiede pianificazione, possibile downtime esteso.

```bash
# 1. Backup completo + snapshot Hetzner
# 2. Test su VPS clone prima
# 3. Pianifica finestra manutenzione
# 4. do-release-upgrade (seguire wizard)
# 5. Verifica Docker ancora funziona
# 6. Verifica applicazione
```

---

## Rotazione Credenziali (Trimestrale)

### Credenziali da Ruotare

| Credenziale | Location | Impatto Rotazione |
|-------------|----------|-------------------|
| POSTGRES_PASSWORD | database.secret | Restart API |
| REDIS_PASSWORD | redis.secret | Restart API |
| QDRANT_API_KEY | qdrant.secret | Restart API + Qdrant |
| JWT_SECRET_KEY | jwt.secret | Invalida tutti i token! |
| ADMIN_PASSWORD | admin.secret | Solo admin login |

### Procedura Rotazione Database Password

```bash
cd /home/meepleai/app/infra

# 1. Genera nuova password
NEW_PASS=$(openssl rand -base64 32 | tr -d '/+=')
echo "New password: $NEW_PASS"

# 2. Aggiorna in PostgreSQL
docker exec meepleai-postgres psql -U meepleai -c \
  "ALTER USER meepleai PASSWORD '$NEW_PASS';"

# 3. Aggiorna secrets/database.secret
nano secrets/database.secret
# Cambia POSTGRES_PASSWORD=...

# 4. Aggiorna .env
nano .env
# Aggiorna riga POSTGRES_PASSWORD

# 5. Restart API per usare nuova password
docker compose -f docker-compose.prod.yml restart api

# 6. Verifica
curl https://api.meepleai.com/health
```

### Procedura Rotazione JWT Secret

**ATTENZIONE**: Invalida TUTTI i token attivi. Tutti gli utenti dovranno ri-loggarsi.

```bash
# 1. Genera nuovo secret
NEW_JWT=$(openssl rand -base64 64 | tr -d '\n')

# 2. Aggiorna jwt.secret
nano secrets/jwt.secret
# JWT_SECRET_KEY=nuovo_valore

# 3. Aggiorna .env
nano .env

# 4. Restart API
docker compose -f docker-compose.prod.yml restart api

# 5. Comunica agli utenti (se necessario)
```

---

## Pulizia Disco (Mensile)

```bash
#!/bin/bash
# /home/meepleai/scripts/cleanup.sh

echo "===== DISK CLEANUP $(date) ====="

# Prima di
df -h /

# Docker cleanup
echo "Cleaning Docker..."
docker system prune -f
docker volume prune -f

# Log cleanup (>30 giorni)
echo "Cleaning old logs..."
find /home/meepleai/logs -name "*.log" -mtime +30 -delete
sudo journalctl --vacuum-time=30d

# Backup cleanup (>14 giorni, extra safety oltre retention script)
echo "Cleaning old backups..."
find /home/meepleai/backups -type f -mtime +14 -delete

# Apt cache
echo "Cleaning apt cache..."
sudo apt autoremove -y
sudo apt clean

# Dopo
df -h /

echo "===== CLEANUP COMPLETE ====="
```

---

## Monitoring e Alerting

### Setup Alerting Base (Email)

Se usi Grafana Cloud (free tier):

1. Crea account su https://grafana.com
2. Configura Prometheus remote write
3. Crea alert rules per:
   - CPU > 90% per 5 min
   - RAM > 95% per 5 min
   - Disk > 85%
   - Container down

### Alerting Semplice con Script

```bash
#!/bin/bash
# /home/meepleai/scripts/alert-check.sh
# Esegui ogni 5 minuti via cron

ALERT_EMAIL="admin@meepleai.com"

# Check disk
DISK=$(df / | tail -1 | awk '{print $5}' | tr -d '%')
if [ "$DISK" -gt 85 ]; then
  echo "ALERT: Disk usage at $DISK%" | mail -s "MeepleAI Disk Alert" $ALERT_EMAIL
fi

# Check memory
MEM=$(free | grep Mem | awk '{printf "%.0f", $3/$2 * 100}')
if [ "$MEM" -gt 95 ]; then
  echo "ALERT: Memory usage at $MEM%" | mail -s "MeepleAI Memory Alert" $ALERT_EMAIL
fi

# Check services
if ! curl -sf http://localhost:8080/health > /dev/null; then
  echo "ALERT: API health check failed" | mail -s "MeepleAI API Down" $ALERT_EMAIL
fi
```

---

## Maintenance Windows

### Pianificare Downtime

Per manutenzione che richiede downtime:

1. **Comunica in anticipo** (se hai utenti)
2. **Scegli orario basso traffico** (es. 3-5 AM)
3. **Backup prima**
4. **Testa procedura** (se possibile)
5. **Documenta** cosa hai fatto

### Maintenance Mode (Opzionale)

```bash
# Abilita maintenance page in Traefik
# Crea file maintenance.html

# Oppure semplicemente:
docker compose -f docker-compose.prod.yml stop api
# Traefik mostrerà 502 - non ideale ma funziona

# Dopo manutenzione:
docker compose -f docker-compose.prod.yml start api
```

---

## Checklist Manutenzione Completa

### Settimanale (15 min)
- [ ] Verifica backup (7 file presenti)
- [ ] Check spazio disco (<70%)
- [ ] Review errori log
- [ ] Verifica SSL expiry (>30 giorni)

### Mensile (1-2 ore)
- [ ] Aggiornamento Docker images
- [ ] Aggiornamento Ubuntu security
- [ ] Pulizia disco
- [ ] Test restore backup
- [ ] Review performance metrics

### Trimestrale (2-3 ore)
- [ ] Rotazione credenziali
- [ ] Review sicurezza completo
- [ ] Test disaster recovery
- [ ] Aggiornamento documentazione
- [ ] Review costi infrastruttura
