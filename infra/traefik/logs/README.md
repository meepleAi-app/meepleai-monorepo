# Traefik Logs

## Contenuto

- `access.log`: registrazione dettagliata delle richieste HTTP proxate.
- `traefik.log`: log del proxy (startup, errori, eventi di routing).
- `.gitkeep`: mantiene la cartella sotto controllo di versione.

## Scopo

Il volume montato dal container Traefik scrive qui i log. Servono per diagnosticare timeout, errori TLS e successo delle richieste.

## Esempio di utilizzo dell’applicazione

Se l’API MeepleAI restituisce 502, il team guarda `infra/traefik/logs/access.log` per identificare l’URL chiamato e `traefik.log` per capire se è fallito il routing o la TLS handshake. Queste informazioni aiutano a distinguere tra problemi dell’applicazione e problemi del proxy.
