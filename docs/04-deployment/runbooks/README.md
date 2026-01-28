# MeepleAI Operations Runbooks

Runbook operativi per la gestione dell'infrastruttura MeepleAI in produzione.

## Quick Reference

| Situazione | Runbook | Priorità |
|------------|---------|----------|
| **Servizio down** | [incident-response.md](./incident-response.md) | P1 |
| **VPS guasto** | [disaster-recovery.md](./disaster-recovery.md) | P1 |
| **Dati persi** | [backup-restore.md](./backup-restore.md) | P1 |
| **Errori applicazione** | [troubleshooting.md](./troubleshooting.md) | P2 |
| **Aggiornamenti** | [maintenance.md](./maintenance.md) | P3 |
| **Performance lente** | [scaling.md](./scaling.md) | P3 |

## Contatti Emergenza

| Ruolo | Contatto | Quando |
|-------|----------|--------|
| On-call | [Il tuo telefono] | Sempre |
| Hetzner Support | support@hetzner.com | VPS issues |
| Cloudflare | community.cloudflare.com | DNS/CDN issues |

## Infrastruttura

```
Server: meepleai-prod-01
IP: [YOUR_VPS_IP]
Provider: Hetzner Cloud (fsn1/hel1)
OS: Ubuntu 24.04 LTS

Servizi:
├── Traefik (reverse proxy + SSL)
├── PostgreSQL 16 (database)
├── Redis 7 (cache)
├── Qdrant (vector DB)
├── .NET API
├── Embedding Service (Python)
└── Reranker Service (Python)
```

## Accesso

```bash
# SSH
ssh meepleai@YOUR_VPS_IP

# Directory principale
cd /home/meepleai/app/infra

# Docker Compose
docker compose -f docker-compose.prod.yml [comando]
```

## Indice Runbook

### 1. [Incident Response](./incident-response.md)
Gestione incidenti: detection, triage, mitigation, resolution, post-mortem.

### 2. [Disaster Recovery](./disaster-recovery.md)
Procedure di recovery per failure catastrofici: VPS down, data corruption, ransomware.

### 3. [Backup & Restore](./backup-restore.md)
Backup automatici, verifica integrità, procedure di restore per ogni servizio.

### 4. [Troubleshooting](./troubleshooting.md)
Debug per servizio: PostgreSQL, Redis, Qdrant, API, Python services, Traefik.

### 5. [Maintenance](./maintenance.md)
Manutenzione ordinaria: aggiornamenti, pulizia, rotazione credenziali, health check.

### 6. [Scaling](./scaling.md)
Guida al scaling: verticale, orizzontale, migrazione a managed services.

## Metriche Chiave

| Metrica | Target | Critico |
|---------|--------|---------|
| API Response Time (p95) | <500ms | >2000ms |
| Error Rate | <1% | >5% |
| CPU Usage | <70% | >90% |
| RAM Usage | <80% | >95% |
| Disk Usage | <70% | >90% |
| Uptime | >99% | <95% |

## Checklist Giornaliera

```bash
# Health check rapido
/home/meepleai/scripts/health-check.sh

# Verifica risorse
docker stats --no-stream

# Verifica spazio disco
df -h

# Ultimi errori
docker compose -f docker-compose.prod.yml logs --tail=50 | grep -i error
```

## Checklist Settimanale

- [ ] Verificare backup eseguiti correttamente
- [ ] Review log errori
- [ ] Controllare spazio disco trend
- [ ] Verificare certificati SSL (scadenza)
- [ ] Aggiornare immagini Docker se necessario
- [ ] Test restore backup (mensile)
