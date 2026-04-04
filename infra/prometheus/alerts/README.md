# Prometheus Alert Rules

## Contenuto

- File come `api-performance.yml`, `cache-performance.yml`, `database-health.yml`, `http-retry-alerts.yaml`, `vector-search.yml`.
- Ogni file combina recording rules (metriche pre-calcolate) e alert rules (threshold per severity).

## Scopo

Queste regole forniscono la logica che trasforma i counter esposti da MeepleAI API, pgvector (PostgreSQL), Redis, ecc. in alert e metriche pronte da visualizzare. Modifiche ai file vengono ricaricate automaticamente da Prometheus al riavvio per mantenere la sorveglianza aggiornata.

## Esempio di utilizzo dell’applicazione

Se l’errore API supera `meepleai:api:error_rate:5m > 1`, la regola `HighErrorRate` in `api-performance.yml` attiva un alert di severità critica. Alertmanager invia la notifica e il team può aprire il runbook `docs/05-operations/runbooks/high-error-rate.md`, mentre Grafana mostra il blocco `error-monitoring`.
