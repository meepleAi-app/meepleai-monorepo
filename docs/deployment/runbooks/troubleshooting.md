# Troubleshooting Runbook

Guida alla diagnosi e risoluzione problemi per servizio.

## Quick Diagnostics

```bash
# Stato generale
docker compose -f docker-compose.prod.yml ps
docker stats --no-stream
df -h
free -h

# Health check completo
/home/meepleai/scripts/health-check.sh
```

---

## PostgreSQL

### Problema: Connection Refused

**Sintomi**: API restituisce errori di connessione database

```bash
# Verifica container running
docker ps | grep postgres

# Verifica PostgreSQL pronto
docker exec meepleai-postgres pg_isready -U meepleai

# Se non risponde, check logs
docker logs meepleai-postgres --tail=100

# Restart se necessario
docker restart meepleai-postgres
```

**Cause comuni**:
- Container crashato → restart
- Disco pieno → libera spazio
- Troppe connessioni → vedi sotto

### Problema: Too Many Connections

**Sintomi**: "FATAL: too many connections for role"

```bash
# Conta connessioni attive
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT count(*) FROM pg_stat_activity;"

# Vedi dettaglio connessioni
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT pid, usename, application_name, state, query_start
   FROM pg_stat_activity
   WHERE datname = 'meepleai';"

# Kill connessioni idle vecchie
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT pg_terminate_backend(pid)
   FROM pg_stat_activity
   WHERE state = 'idle'
   AND query_start < now() - interval '10 minutes';"

# Se persiste, restart API (rilascia connection pool)
docker restart meepleai-api
```

### Problema: Slow Queries

**Sintomi**: API lenta, timeout

```bash
# Trova query lente attive
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT pid, now() - query_start AS duration, query
   FROM pg_stat_activity
   WHERE state = 'active'
   ORDER BY duration DESC
   LIMIT 5;"

# Kill query specifica se bloccante
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT pg_terminate_backend(PID_NUMBER);"

# Analizza indici mancanti
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT schemaname, tablename, indexrelname, idx_scan
   FROM pg_stat_user_indexes
   ORDER BY idx_scan ASC
   LIMIT 10;"
```

### Problema: Disk Full

```bash
# Verifica spazio
docker exec meepleai-postgres df -h /var/lib/postgresql/data

# Pulisci WAL vecchi (con cautela!)
docker exec meepleai-postgres psql -U meepleai -c "CHECKPOINT;"

# Vacuum per recuperare spazio
docker exec meepleai-postgres psql -U meepleai -c "VACUUM FULL ANALYZE;"
```

---

## Redis

### Problema: Connection Refused

```bash
# Verifica container
docker ps | grep redis

# Test ping
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" ping

# Se fallisce, check logs
docker logs meepleai-redis --tail=50

# Restart
docker restart meepleai-redis
```

### Problema: Memory Full

**Sintomi**: "OOM command not allowed when used memory > 'maxmemory'"

```bash
# Check memoria usata
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" INFO memory

# Flush cache se necessario (ATTENZIONE: perde dati cache!)
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" FLUSHDB

# Oppure solo chiavi specifiche
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" --scan --pattern "cache:*" | \
  xargs docker exec -i meepleai-redis redis-cli -a "$REDIS_PASSWORD" DEL
```

### Problema: Slow Operations

```bash
# Abilita slowlog
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" CONFIG SET slowlog-log-slower-than 10000

# Vedi operazioni lente
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" SLOWLOG GET 10

# Check chiavi grandi
docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" --bigkeys
```

---

## Qdrant

### Problema: Not Responding

```bash
# Health check
curl http://localhost:6333/health

# Se fallisce, check container
docker ps | grep qdrant
docker logs meepleai-qdrant --tail=100

# Restart
docker restart meepleai-qdrant
```

### Problema: Search Slow

```bash
# Verifica collection stats
curl "http://localhost:6333/collections/documents" \
  -H "api-key: $QDRANT_API_KEY" | jq

# Verifica RAM usage
docker stats meepleai-qdrant --no-stream

# Se RAM quasi piena, considera:
# 1. Aumentare limite RAM container
# 2. Abilitare on-disk storage per vectors
```

### Problema: API Key Invalid

```bash
# Verifica API key configurata
echo $QDRANT_API_KEY

# Test con API key
curl -I "http://localhost:6333/collections" -H "api-key: $QDRANT_API_KEY"

# Se 401, verifica env nel container
docker exec meepleai-qdrant env | grep QDRANT
```

---

## .NET API

### Problema: 502 Bad Gateway

**Sintomi**: Traefik restituisce 502

```bash
# Verifica API container running
docker ps | grep api

# Health check diretto
curl http://localhost:8080/health

# Se non risponde, check logs
docker logs meepleai-api --tail=200

# Restart
docker restart meepleai-api
```

**Cause comuni**:
- Container crashato
- Out of memory
- Dependency (DB/Redis) non raggiungibile
- Porta non esposta

### Problema: High Memory Usage

```bash
# Check memoria
docker stats meepleai-api --no-stream

# Se >90% limite, potrebbe essere memory leak
# Restart temporaneo
docker restart meepleai-api

# Se persiste, potrebbe servire profiling applicazione
```

### Problema: Slow Response Times

```bash
# Check health endpoint (dovrebbe essere <100ms)
time curl http://localhost:8080/health

# Se lento, verifica dipendenze
time docker exec meepleai-postgres pg_isready
time docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" ping

# Check CPU
docker stats meepleai-api --no-stream

# Verifica log per errori
docker logs meepleai-api --tail=100 | grep -i "error\|exception\|slow"
```

### Problema: Errori 500

```bash
# Cerca eccezioni nei log
docker logs meepleai-api --tail=500 | grep -i exception

# Log dettagliati (se configurato)
docker logs meepleai-api --tail=500 | grep -E "ERROR|FATAL"

# Verifica connessione servizi
docker exec meepleai-api curl -s http://embedding-service:8000/health
docker exec meepleai-api curl -s http://reranker-service:8001/health
```

---

## Embedding/Reranker Services (Python)

### Problema: Service Not Responding

```bash
# Check container
docker ps | grep -E "embedding|reranker"

# Health check
curl http://localhost:8000/health  # embedding
curl http://localhost:8001/health  # reranker

# Logs
docker logs meepleai-embedding --tail=100
docker logs meepleai-reranker --tail=100

# Restart
docker restart meepleai-embedding meepleai-reranker
```

### Problema: Model Loading Slow/Failed

```bash
# Check logs per errori download modello
docker logs meepleai-embedding 2>&1 | grep -i "model\|download\|error"

# Verifica spazio disco (modelli ~1-2GB ciascuno)
df -h

# Verifica RAM (modelli richiedono RAM)
free -h
docker stats meepleai-embedding --no-stream
```

### Problema: Inference Lenta

```bash
# Check CPU usage (inference CPU-intensive)
docker stats meepleai-embedding --no-stream

# Se CPU 100%, potrebbe essere:
# 1. Troppe richieste parallele
# 2. Modello troppo grande per risorse
# 3. Serve batching delle richieste
```

---

## Traefik (Reverse Proxy)

### Problema: SSL Certificate Error

```bash
# Verifica certificato
echo | openssl s_client -connect meepleai.com:443 2>/dev/null | \
  openssl x509 -noout -dates

# Check Traefik logs per errori ACME
docker logs meepleai-traefik 2>&1 | grep -i "acme\|certificate\|error"

# Force renewal
docker restart meepleai-traefik

# Se persiste, elimina e rigenera
docker exec meepleai-traefik rm -f /letsencrypt/acme.json
docker restart meepleai-traefik
# Attendi 2-5 minuti per nuovo certificato
```

### Problema: 404 Not Found

```bash
# Verifica routing rules
docker logs meepleai-traefik 2>&1 | grep -i "rule\|router"

# Verifica labels sui container
docker inspect meepleai-api --format='{{json .Config.Labels}}' | jq

# Verifica Traefik vede i container
curl -s http://localhost:8080/api/http/routers | jq  # Se dashboard abilitata
```

### Problema: Gateway Timeout

```bash
# Backend non risponde in tempo
# Aumenta timeout in Traefik config o verifica backend

# Check backend health
curl http://localhost:8080/health  # API direttamente

# Verifica network connectivity
docker network inspect infra_meepleai-network
```

---

## Sistema (VPS)

### Problema: Disco Pieno

```bash
# Trova cosa usa spazio
du -sh /* 2>/dev/null | sort -hr | head -10
du -sh /var/lib/docker/* | sort -hr | head -5

# Pulisci Docker
docker system prune -a --volumes  # ATTENZIONE: rimuove tutto unused

# Pulisci log
sudo journalctl --vacuum-time=7d
find /home/meepleai/logs -mtime +7 -delete

# Pulisci backup vecchi
find /home/meepleai/backups -mtime +14 -delete
```

### Problema: RAM Esaurita

```bash
# Vedi chi usa RAM
free -h
docker stats --no-stream | sort -k 4 -hr

# Kill container più affamato (se non critico)
docker restart meepleai-NOME

# Se persiste, considera upgrade VPS o ottimizzazione limiti
```

### Problema: CPU al 100%

```bash
# Trova processo
top -c

# Se Docker
docker stats --no-stream | sort -k 3 -hr

# Possibile attacco? Check connessioni
netstat -tuln | grep ESTABLISHED | wc -l
ss -tuln | grep ESTABLISHED

# Se troppe connessioni da stesso IP
netstat -ntu | awk '{print $5}' | cut -d: -f1 | sort | uniq -c | sort -rn | head
```

### Problema: SSH Lento/Timeout

```bash
# Da Hetzner Console (non via SSH):
# 1. Verifica server running
# 2. Check firewall

# Se puoi accedere:
# Verifica UFW
sudo ufw status

# Verifica SSH daemon
sudo systemctl status sshd

# Check fail2ban (potresti essere bannato)
sudo fail2ban-client status sshd
```

---

## Network Issues

### Problema: DNS Non Risolve

```bash
# Test da VPS
dig meepleai.com
dig api.meepleai.com

# Test da esterno
nslookup meepleai.com 8.8.8.8

# Se DNS OK ma sito non raggiungibile:
# Verifica Cloudflare proxy status
# Verifica IP corretto in DNS records
```

### Problema: Container Non Comunicano

```bash
# Verifica network
docker network ls
docker network inspect infra_meepleai-network

# Test connectivity tra container
docker exec meepleai-api ping -c 3 postgres
docker exec meepleai-api ping -c 3 redis

# Verifica DNS interno Docker
docker exec meepleai-api nslookup postgres
```

---

## Quick Fix Commands

```bash
# ===== RESTART TUTTO =====
cd /home/meepleai/app/infra
docker compose -f docker-compose.prod.yml restart

# ===== RESTART SINGOLO SERVIZIO =====
docker restart meepleai-NOME

# ===== LOGS LIVE =====
docker compose -f docker-compose.prod.yml logs -f --tail=100

# ===== LOGS ERRORI =====
docker compose -f docker-compose.prod.yml logs 2>&1 | grep -i error

# ===== REBUILD E RESTART =====
docker compose -f docker-compose.prod.yml up -d --force-recreate NOME

# ===== NUCLEAR OPTION (ultimo resort) =====
docker compose -f docker-compose.prod.yml down
docker compose -f docker-compose.prod.yml up -d
```

---

## Quando Escalare

Contatta supporto se:
- [ ] VPS non risponde dopo 30 minuti di troubleshooting
- [ ] Sospetta compromissione sicurezza
- [ ] Hardware failure evidente
- [ ] Problema persiste dopo restart completo
- [ ] Perdita dati confermata

**Hetzner Support**: support@hetzner.com
**Cloudflare**: community.cloudflare.com
