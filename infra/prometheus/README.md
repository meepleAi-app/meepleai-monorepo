# Prometheus Rules Library

## Contenuto

- `alerts/`: YAML con gruppi di recording e alert rules (`audit-outbox.yml`, `wikidata-enrichment.yml`, `egress-guard.yml`, ecc.).
- `*.test.yml`: test unitari `promtool` della logica delle regole. Attenzione: dichiarano loro stessi il nome della metrica in `input_series`, quindi **passano anche se quel nome non esiste** in produzione (#3798).

## Scopo

Prometheus legge `infra/prometheus.yml`, che referenzia **uno per uno** i file di regole in
`rule_files`. Mettere un file in `alerts/` NON basta: perche' le sue regole siano valutate servono
**tre** cose, e la mancanza di una qualsiasi e' silenziosa.

| passo | dove | se manca |
|---|---|---|
| 1. il file esiste | `infra/prometheus/alerts/` | — |
| 2. e' montato nel container | `infra/docker-compose.yml`, volumes di `prometheus` | Prometheus non lo vede |
| 3. e' in `rule_files` | `infra/prometheus.yml` | Prometheus lo ignora |

Nel 2026-08 l'audit di #3798 ha trovato **25 alert** fermi al passo 1: presenti nel repo, mai
montati, mai valutati. Un precedente identico era gia' documentato per `api-single-instance.yml`
(#3373/#3383). Un alert che non puo' scattare e' indistinguibile da un sistema sano.

Dopo qualunque modifica alle regole serve inoltre un **force-recreate** del container su staging:
il deploy ordinario non ricarica le rules, quindi il file corretto nel repo puo' convivere a lungo
con una versione vecchia in esecuzione. Qui definiamo le metriche aggregate e le soglie che generano alert (error rate, latenza, consumo di risorse) sfruttando i dati esposti da MeepleAI API e dagli altri servizi sul `/metrics`.

## Esempio di utilizzo dell’applicazione

MeepleAI API espone metriche su `/metrics`. Prometheus le raccoglie e applica le recording rules di `prometheus-rules.yml` per calcolare error rate e p95. Quando il valore supera la soglia critica, Alertmanager invia notifiche e Grafana mostra il problema usando `infra/dashboards/api-performance.json`.


## Nomi delle metriche: la trappola dell'unita'

L'exporter Prometheus di OpenTelemetry **infila l'unita' dichiarata nel nome**, e rimuove il
suffisso `.total` del nome C# prima di appenderla:

```csharp
name: "meepleai.egress.allowed.total",   unit: "requests"   // Counter
   ->  meepleai_egress_allowed_requests_total
```

Regola completa: punti a underscore, `.total` finale rimosso, `_<unit>` appeso, `_total` finale se
il tipo e' un `Counter`. L'unita' NON viene duplicata se il nome la contiene gia' (`_seconds` con
`unit: "s"` resta `_seconds`).

Le regole vanno scritte sul nome **esposto**, non su quello del codice. Per verificarlo:

```bash
docker exec meepleai-prometheus wget -qO- \
  'http://localhost:9090/api/v1/query?query=<espressione>'
# deve restituire un risultato NON vuoto
```

`promtool test rules` non puo' sostituire questo controllo: i test dichiarano loro stessi il nome
in `input_series`, quindi validano la logica della regola su una metrica che potrebbe non esistere.
