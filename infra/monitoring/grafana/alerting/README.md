# Grafana Alert Rules

Definizioni di alerting **Grafana** (`apiVersion: 1`, con `uid`, `title`, `condition`), non regole
Prometheus. Stavano in `infra/prometheus/alerts/`, dove sembravano regole Prometheus senza esserlo:
`promtool check rules` le rifiuta con `yaml: unmarshal errors`, e il gate di #3821 falliva su di esse
al primo colpo.

| file | issue |
|---|---|
| `domain-event-outbox.yml` | #1535 T6 — backlog, età della più vecchia Pending, spike di Failed |
| `http-retry-alerts.yaml` | #1453 — retry HTTP e circuit breaker |

## ⚠️ Nessuno dei due è caricato

Non sono montati da alcun compose né referenziati da un provisioning Grafana: il provisioning
dell'alerting non esiste in questo repo. Sono quindi **inerti** — descrivono alert che nessuno
valuta, ed erano inerti anche prima di questo spostamento.

Spostarli qui non li attiva: li rende riconoscibili per quello che sono. Perché diventino reali
serve montarli sotto `/etc/grafana/provisioning/alerting/` nel servizio grafana, e a quel punto
vanno verificate le metriche che interrogano — con lo stesso criterio di
`infra/scripts/verify-alert-metric-names.sh`, perché un alert Grafana su un nome inesistente è
cieco esattamente come uno Prometheus.
