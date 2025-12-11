# n8n Template Library

## Contenuto

- JSON come `pdf-processing-pipeline.json`, `backup-automation.json`, `cache-warming.json`, `integration-slack-notifications.json` che offrono blueprint per workflow comuni.
- Ogni template include nodi trigger, credenziali placeholder e commenti che mostrano come collegarsi con MeepleAI API e i servizi esterni.

## Scopo

Le template aiutano a standardizzare workflow che MeepleAI utilizza per alerting, elaborazione PDF e sincronizzazione con servizi esterni. Servono da punto di partenza per nuovi workflow prima di promuoverli nella directory `workflows/`.

## Esempio di utilizzo dell’applicazione

Quando il team vuole aggiungere una nuova pipeline di ingestione documenti, importa `infra/n8n/templates/pdf-processing-pipeline.json`, aggiorna i riferimenti `http://api:8080` e `postgres` nelle impostazioni e attiva il trigger. L’app usa poi questo workflow per orchestrare il download, l’estrazione dei metadati e l’invio di notifiche Slack una volta che un PDF viene caricato.
