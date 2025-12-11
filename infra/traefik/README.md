# Traefik Reverse Proxy Configuration

## Contenuto

- `traefik.yml`: configurazione statica (entrypoint, logging, providers, metrics).
- `dynamic/`: middleware, router e TLS dinamici.
- `certs/`: certificati autofirmati per HTTPS locale.
- `logs/`: file di log per accessi ed eventi.

## Scopo

Questo set di file viene montato dal servizio `traefik` nel `docker-compose.yml` e permette di orchestrare il proxy che espone `api`, `web`, `n8n`. La configurazione statica stabilisce entry point e providers, mentre le directory `dynamic`, `certs` e `logs` permettono di gestire router/middleware live, TLS locale e troubleshooting.

## Esempio di utilizzo dell’applicazione

Avviando `docker compose up -d traefik` con `traefik.enable=true` impostato sui servizi, Traefik legge `infra/traefik/dynamic/middlewares.yml` e reindirizza `https://meepleai.localhost` verso `web` e `api`. Usa `infra/traefik/certs/localhost.crt` e `.key` per TLS e scrive gli accessi in `infra/traefik/logs/access.log` così il team può correlare i problemi dell’app col proxy.
