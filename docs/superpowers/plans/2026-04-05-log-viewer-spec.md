# Specifica: Log Viewer Senza Accesso Diretto

**Data**: 2026-04-05
**Prodotta da**: `/sc:spec-panel --ultrathink`
**Status**: Draft — pronta per implementazione

## Obiettivo

Permettere a sviluppatori e operatori di visualizzare i log di tutti i servizi MeepleAI tramite browser autenticato, senza richiedere accesso SSH al server o comandi Docker locali (`make logs`, `docker exec`, etc.).

## Contesto Tecnico

Stack: Docker Compose su Hetzner. Servizi: API (.NET), Web (Next.js), embedding-service, reranker-service, smoldocling-service, unstructured-service, PostgreSQL, Redis.

**Gap attuale**: Config Loki + Fluent-bit esistono in `infra/monitoring/` ma non sono integrati nel compose stack. `compose.logging.yml` referenziato nel README ma non creato.

## Requisiti Funzionali (P0 = MVP, P1 = evolution)

| ID | Requisito | Priorità |
|----|-----------|----------|
| LOG-01 | Visualizzare log di qualsiasi container MeepleAI via browser autenticato | P0 |
| LOG-02 | Filtrare per servizio, livello (ERROR/WARN/INFO), range temporale | P0 |
| LOG-03 | Retention 30 giorni | P0 |
| LOG-04 | Latenza max log→visibilità: 30 secondi | P1 |
| LOG-05 | Accesso protetto da autenticazione (no anonymous) | P0 |
| LOG-06 | Log persistenti a riavvio dei servizi applicativi | P0 |
| LOG-07 | Ricerca full-text nei log | P1 |
| LOG-08 | Dashboard pre-configurata con filtri comuni per servizio | P1 |

## Architettura Target

```
[Docker Containers]
    → [Fluent-bit] (raccoglie via docker socket/driver)
    → [Loki :3100] (storage + query engine)
    → [Grafana :3001] (UI)
    → [Traefik] (reverse proxy + TLS)
    → [Browser dev/admin su grafana.meepleai.app]
```

## Deliverables

### 1. `infra/compose.logging.yml` (NUOVO — blocco fondamentale)
Servizi da aggiungere al profilo `logging`:
- `loki`: usa `infra/monitoring/loki/loki-config.yml`
- `fluent-bit`: usa `infra/monitoring/fluent-bit/fluent-bit.conf` + `parsers.conf`
- Fluent-bit: mount `/var/lib/docker/containers:ro` + `/var/run/docker.sock:ro`
- Entrambi sulla rete `meepleai`

### 2. `infra/grafana-datasources.yml` (MODIFICA)
Aggiungere datasource Loki:
```yaml
- name: Loki
  type: loki
  url: http://loki:3100
  access: proxy
```

### 3. `infra/monitoring/grafana/dashboards/log-explorer.json` (NUOVO)
Dashboard con:
- Variable `$service` per filtraggio rapido
- Panel: log stream in tempo reale
- Panel: error rate per servizio nel tempo
- Panel: warning/error count ultimi 24h

### 4. Traefik routing Grafana (VERIFICA/MODIFICA in `compose.traefik.yml`)
- Host: `grafana.meepleai.app`
- TLS: Let's Encrypt
- Grafana anonymous access: OFF

### 5. `infra/secrets/grafana.secret` (NUOVO se non esiste)
```
GF_SECURITY_ADMIN_PASSWORD=<password>
GF_SECURITY_ADMIN_USER=admin
GF_AUTH_ANONYMOUS_ENABLED=false
```

### 6. `infra/Makefile` (MODIFICA)
```makefile
logging:      ## Start log aggregation stack (loki + fluent-bit)
logging-down: ## Stop log aggregation stack
logs-ui:      ## Open Grafana in browser
```

## Scenari di Accettazione (BDD)

```gherkin
Feature: Log Viewer Senza Accesso Diretto

  Scenario: Developer vede log API senza SSH
    Given lo stack MeepleAI è running su staging
    And Grafana è accessibile su https://grafana.meepleai.app
    When il developer apre il browser con credenziali valide
    And naviga su Explore → Loki → query: {container_name="meepleai-api"} |= "ERROR"
    Then vede gli errori recenti con timestamp
    And non ha bisogno di aprire un terminale

  Scenario: Filtraggio multi-servizio
    When inserisce: {container_name=~"meepleai-.*"} | json | level = "error"
    Then vede log da TUTTI i servizi filtrati per level=error

  Scenario: Log storici (30 giorni)
    When imposta range "Last 7 days" e cerca {container_name="meepleai-api"} |= "migration"
    Then vede log di tutte le migration della settimana precedente

  Scenario: Accesso non autorizzato bloccato
    Given utente senza credenziali
    When accede a https://grafana.meepleai.app
    Then viene reindirizzato al login Grafana, nessun log visibile

  Scenario: Resilienza al riavvio API
    Given API ha prodotto log negli ultimi 5 minuti
    When `docker compose restart api` viene eseguito
    Then log precedenti al restart ancora visibili in Grafana
    And nuovi log appaiono entro 30 secondi
```

## Failure Modes e Mitigazioni

| Failure | Comportamento atteso | Mitigazione |
|---------|---------------------|-------------|
| Loki down | UI non disponibile, raccolta continua | Fluent-bit buffer 6MB, retry automatico |
| Fluent-bit down | Log non raccolti (gap temporale) | Alert Prometheus su metric assente |
| Storage pieno | Loki blocca ingestione | Compactor ogni 10min, retention 30gg |
| Grafana down | Solo UI compromessa | Restart senza perdita dati Loki |

## Definizione di Done

- [ ] `make logging` avvia Loki + Fluent-bit senza errori
- [ ] Grafana mostra datasource Loki come "Connected"
- [ ] Query `{container_name="meepleai-api"}` restituisce log reali in Explore
- [ ] Dashboard log-explorer caricata e funzionante
- [ ] Grafana accessibile su URL pubblico staging con TLS
- [ ] Accesso senza credenziali → redirect login
- [ ] Log persistono dopo restart del container API
- [ ] Tutti e 5 gli scenari BDD verificati manualmente
- [ ] `docs/deployment/monitoring/log-aggregation-guide.md` aggiornato

## Note Implementative

- **Fluent-bit docker input**: richiede mount privilegiato `/var/lib/docker/containers:ro`. Alternativa più sicura: logging driver `fluentd` su ogni servizio Docker (elimina necessità del mount).
- **Loki non va esposto pubblicamente** — solo Grafana è public-facing.
- **Grafana port 3001** già in uso nello stack — usare lo stesso container, aggiungere solo Traefik label.
- Issue di riferimento: `#3367` (log aggregation)
