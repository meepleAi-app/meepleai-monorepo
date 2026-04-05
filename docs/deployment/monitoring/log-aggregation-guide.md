# Log Aggregation Guide

Stack di log aggregation per MeepleAI: Fluent-bit → Loki → Grafana.

## Accesso Rapido

**Grafana (staging)**: https://meepleai.app/grafana
- Credenziali: vedi `infra/secrets/monitoring.secret`
- Dashboard diretta: https://meepleai.app/grafana/d/meepleai-log-explorer

## Avvio Stack

```bash
cd infra

# Avvia log aggregation (staging)
make logging

# Ferma log aggregation
make logging-down

# URL Grafana
make logs-ui
```

## Architettura

```
[Docker Containers]
    → [Fluent-bit :24224] (raccoglie da /var/lib/docker/containers)
    → [Loki :3100]        (storage, retention 30gg)
    → [Grafana :3000]     (UI, su /grafana via Traefik)
```

## Config Files

| File | Scopo |
|------|-------|
| `infra/compose.logging.yml` | Servizi Loki + Fluent-bit |
| `infra/monitoring/loki/loki-config.yml` | Config Loki (retention, storage) |
| `infra/monitoring/fluent-bit/fluent-bit.conf` | Input/Output Fluent-bit |
| `infra/monitoring/fluent-bit/parsers.conf` | Parser .NET (Serilog) e Python JSON |
| `infra/grafana-datasources.yml` | Datasource Prometheus + Loki |
| `infra/dashboards/log-explorer.json` | Dashboard log explorer |

## Query LogQL Utili

```logql
# Tutti i log di un servizio
{container_name="meepleai-api"}

# Solo errori da tutti i servizi MeepleAI
{container_name=~"meepleai-.*"} | json | level = "error"

# Log API degli ultimi 10 minuti con parola chiave
{container_name="meepleai-api"} |= "migration" | json

# Error rate per servizio (ultimi 5 min)
sum by (container_name) (count_over_time({container_name=~"meepleai-.*"} | json | level="error" [5m]))
```

## Deployment su Staging

Il profilo `logging` va aggiunto al comando di deploy staging:

```bash
# Deploy completo con logging
docker compose \
  -f docker-compose.yml \
  -f compose.staging.yml \
  -f compose.traefik.yml \
  -f compose.logging.yml \
  --profile ai --profile monitoring --profile logging --profile proxy \
  up -d
```

## Troubleshooting

| Problema | Verifica |
|----------|----------|
| Datasource Loki "Data source connected but no labels received" | Fluent-bit è avviato? `docker logs meepleai-fluent-bit` |
| Nessun log in Explore | Seleziona time range → Last 1h. Verifica label `container_name` |
| Loki non si avvia | Verifica permessi su volume: `docker inspect meepleai-loki` |
| Grafana non mostra dashboard | Attendi 10s per provisioning automatico |

## Retention

- **Loki**: 30 giorni (720h) — configurato in `loki-config.yml`
- **Compactor**: ogni 10 minuti, ottimizza storage
