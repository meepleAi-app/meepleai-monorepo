# n8n setup (placeholder)
- Avvia `n8n` (porta 5678).
- Crea tre Workflow HTTP Request Trigger: /agent/qa, /agent/explain, /agent/setup
- A valle: node HTTP Request -> chiama `meepleagentai-api:8080` con stesso payload.
- Aggiungi logging e retry/backoff (3 tentativi, esponi `request_id` in risposta).
