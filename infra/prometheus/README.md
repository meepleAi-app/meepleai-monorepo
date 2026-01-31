# Prometheus Rules Library

## Contenuto

- `alerts/`: YAML con gruppi di recording e alert rules (`api-performance.yml`, `vector-search.yml`, `quality-metrics.yml`, ecc.).

## Scopo

Prometheus legge `infra/prometheus.yml`, che include i file in `alerts/`. Qui definiamo le metriche aggregate e le soglie che generano alert (error rate, latenza, consumo di risorse) sfruttando i dati esposti da MeepleAI API e dagli altri servizi sul `/metrics`.

## Esempio di utilizzo dell’applicazione

MeepleAI API espone metriche su `/metrics`. Prometheus le raccoglie e applica regole come `api-performance.yml` per calcolare error rate e p95. Quando il valore supera la soglia critica, Alertmanager invia notifiche e Grafana mostra il problema usando `infra/dashboards/api-performance.json`.
