# n8n Workflows

## Contenuto

- `agent-explain-orchestrator.json`: definisce il webhook `/webhook/agent/explain` con retry, logging e risposta standardizzata.

## Scopo

Qui finiscono le workflow effettivamente importate nell’istanza n8n che gira con MeepleAI. Questi JSON sono quelli attivi in sviluppo/staging e orchestrano le chiamate verso l’API, le notifiche e le logiche di fallback.

## Esempio di utilizzo dell’applicazione

La UI MeepleAI chiama `/webhook/agent/explain` su n8n, che attiva `agent-explain-orchestrator.json`. Il workflow riceve il payload, genera un request ID, chiama `http://api:8080/agent/explain`, applica la logica di retry e restituisce la risposta consolidata al client, semplificando la gestione dei webhook.
