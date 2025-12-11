# n8n Automation Assets

## Contenuto

- `templates/`: re-usable workflow blueprints (backup, Slack notifications, cache warming, PDF processing ecc.).
- `workflows/`: orchestrazioni già importate nell’istanza n8n attiva (es. `agent-explain-orchestrator.json`).

## Scopo

Questa cartella raccoglie tutti i JSON definiti per n8n. Le workflow template velocizzano la creazione di nuove automazioni, mentre i workflow presenti nella cartella `workflows/` sono quelli attivamente importati nel container e usati durante i test locali o in staging.

## Esempio di utilizzo dell’applicazione

Durante un deploy di sviluppo, il team esegue `docker compose up -d n8n`, importa `infra/n8n/workflows/agent-explain-orchestrator.json` e assegna i credentials corretti. L’app MeepleAI chiama poi `/webhook/agent/explain` e la workflow definita qui applica request ID, richiama `http://api:8080/agent/explain`, gestisce retry e restituisce la risposta formattata ai client.
