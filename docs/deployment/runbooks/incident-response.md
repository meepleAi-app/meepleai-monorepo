# Incident Response Runbook

Procedure per la gestione degli incidenti in produzione.

## Severity Levels

| Level | Descrizione | Response Time | Esempi |
|-------|-------------|---------------|--------|
| **P1 Critical** | Servizio completamente down | < 15 min | VPS unreachable, DB corrotto |
| **P2 High** | Funzionalità core degradata | < 1 ora | API lenta, errori >5% |
| **P3 Medium** | Funzionalità secondaria impattata | < 4 ore | Feature singola broken |
| **P4 Low** | Issue minore, no user impact | < 24 ore | Warning nei log |

---

## Fase 1: Detection & Alert

### Sintomi Comuni

| Sintomo | Possibile Causa | Severity |
|---------|-----------------|----------|
| Site non raggiungibile | Traefik down, DNS, VPS | P1 |
| 502 Bad Gateway | API container down | P1 |
| Timeout API | DB connection, RAM | P2 |
| Errori 500 frequenti | Bug applicativo, DB | P2 |
| Ricerche RAG falliscono | Qdrant down, embedding | P2 |
| Login non funziona | Redis down, JWT | P2 |
| Lentezza generale | CPU/RAM saturata | P3 |

### Verifica Iniziale (2 minuti)

```bash
# 1. Verifica raggiungibilità VPS
ping YOUR_VPS_IP

# 2. SSH nel server
ssh meepleai@YOUR_VPS_IP

# 3. Quick health check
/home/meepleai/scripts/health-check.sh

# 4. Stato container
docker compose -f docker-compose.prod.yml ps

# 5. Risorse sistema
htop  # CPU/RAM
df -h # Disco
```

---

## Fase 2: Triage

### Decision Tree

```
Site non raggiungibile?
├── Sì → Ping VPS
│   ├── Ping OK → SSH funziona?
│   │   ├── Sì → Controlla Traefik: docker logs meepleai-traefik
│   │   └── No → Verifica firewall UFW, SSH config
│   └── Ping FAIL → Verifica Hetzner Console, possibile VPS down
│
└── No (site raggiungibile ma errori)
    ├── 502 Bad Gateway → API container down
    ├── 500 Internal Error → Controlla API logs
    ├── Timeout → Controlla DB connections
    └── 404 → Routing/DNS issue
```

### Identificare il Servizio Problematico

```bash
# Controlla tutti i container
docker compose -f docker-compose.prod.yml ps

# Container non healthy?
docker inspect meepleai-NOME --format='{{.State.Health.Status}}'

# Logs specifici
docker logs meepleai-postgres --tail=100
docker logs meepleai-redis --tail=100
docker logs meepleai-qdrant --tail=100
docker logs meepleai-api --tail=100
docker logs meepleai-traefik --tail=100
```

---

## Fase 3: Mitigation (Azioni Immediate)

### 3.1 Container Down - Restart

```bash
# Restart singolo container
docker compose -f docker-compose.prod.yml restart NOME_SERVIZIO

# Se non parte, check logs
docker compose -f docker-compose.prod.yml logs NOME_SERVIZIO --tail=200

# Force recreate
docker compose -f docker-compose.prod.yml up -d --force-recreate NOME_SERVIZIO
```

### 3.2 Disco Pieno

```bash
# Verifica
df -h

# Pulisci Docker (ATTENZIONE: rimuove immagini unused)
docker system prune -a --volumes

# Pulisci log vecchi
find /home/meepleai/logs -mtime +7 -delete
find /home/meepleai/backups -mtime +14 -delete

# Pulisci journal
sudo journalctl --vacuum-time=7d
```

### 3.3 RAM Esaurita

```bash
# Identifica container problematico
docker stats --no-stream

# Restart container che usa troppa RAM
docker restart meepleai-NOME

# Se persiste, riduci limiti o riavvia VPS
sudo reboot
```

### 3.4 CPU al 100%

```bash
# Identifica processo
top -c

# Se è Docker
docker stats --no-stream

# Restart container problematico
docker restart meepleai-NOME

# Se è sistema, possibile attacco - check connessioni
netstat -tuln | grep ESTABLISHED | wc -l
```

### 3.5 Database Connection Issues

```bash
# Test connessione PostgreSQL
docker exec meepleai-postgres pg_isready -U meepleai

# Se fallisce, restart
docker restart meepleai-postgres

# Verifica connessioni attive
docker exec meepleai-postgres psql -U meepleai -c "SELECT count(*) FROM pg_stat_activity;"

# Troppe connessioni? Kill idle
docker exec meepleai-postgres psql -U meepleai -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE state = 'idle' AND query_start < now() - interval '10 minutes';"
```

### 3.6 SSL Certificate Issues

```bash
# Verifica certificato
echo | openssl s_client -connect meepleai.com:443 2>/dev/null | openssl x509 -noout -dates

# Force renewal Traefik
docker restart meepleai-traefik

# Se persiste, elimina e rigenera
docker exec meepleai-traefik rm /letsencrypt/acme.json
docker restart meepleai-traefik
```

---

## Fase 4: Resolution

### Verifica Post-Fix

```bash
# 1. Health check completo
/home/meepleai/scripts/health-check.sh

# 2. Test endpoint critici
curl -w "\nTime: %{time_total}s\n" https://api.meepleai.com/health
curl -w "\nTime: %{time_total}s\n" https://meepleai.com

# 3. Verifica log puliti
docker compose -f docker-compose.prod.yml logs --tail=50 | grep -i error

# 4. Test funzionale (se possibile)
# - Login
# - Upload PDF
# - Query RAG
```

### Comunicazione

Per incidenti P1/P2, considera:
- [ ] Notifica stakeholder
- [ ] Status page update (se presente)
- [ ] Tweet/comunicazione utenti (se necessario)

---

## Fase 5: Post-Mortem

### Template Post-Mortem

```markdown
# Post-Mortem: [Titolo Incidente]

**Data**: YYYY-MM-DD HH:MM
**Durata**: X minuti/ore
**Severity**: P1/P2/P3
**Impatto**: [Descrizione impatto utenti]

## Timeline

| Ora | Evento |
|-----|--------|
| HH:MM | Primo alert/segnalazione |
| HH:MM | Inizio investigazione |
| HH:MM | Root cause identificata |
| HH:MM | Fix applicato |
| HH:MM | Servizio ripristinato |
| HH:MM | Verifica completata |

## Root Cause

[Descrizione dettagliata della causa]

## Resolution

[Cosa è stato fatto per risolvere]

## Action Items

- [ ] [Azione preventiva 1]
- [ ] [Azione preventiva 2]
- [ ] [Miglioramento monitoring]

## Lessons Learned

- [Cosa abbiamo imparato]
- [Cosa faremo diversamente]
```

---

## Quick Commands Reference

```bash
# ===== DIAGNOSTICA =====
docker compose -f docker-compose.prod.yml ps          # Stato container
docker compose -f docker-compose.prod.yml logs -f     # Logs live
docker stats --no-stream                              # Risorse container
htop                                                  # Risorse sistema
df -h                                                 # Spazio disco
netstat -tuln                                         # Porte aperte

# ===== RESTART =====
docker compose -f docker-compose.prod.yml restart     # Tutti
docker restart meepleai-NOME                          # Singolo

# ===== EMERGENZA =====
docker compose -f docker-compose.prod.yml down        # Stop tutto
docker compose -f docker-compose.prod.yml up -d       # Riavvia tutto
sudo reboot                                           # Reboot VPS

# ===== LOGS =====
docker logs meepleai-api --tail=200 --since=1h       # Ultima ora
docker logs meepleai-api 2>&1 | grep -i error        # Solo errori
```

---

## Escalation Path

1. **Self-resolve** (primi 15-30 min)
2. **Hetzner Support** (se VPS issue): support@hetzner.com
3. **Cloudflare Support** (se DNS/CDN): community.cloudflare.com
4. **Restore da backup** (se data corruption): [backup-restore.md](./backup-restore.md)
5. **Disaster Recovery** (se failure totale): [disaster-recovery.md](./disaster-recovery.md)
