# Traefik Dynamic Configuration

## Contenuto

- `middlewares.yml`: middleware condivisi (Basic Auth, redirect HTTP→HTTPS, rate limiting).
- `tls-localhost.yml`: binding TLS per `localhost`.
- `tls.yml`: template TLS per ambienti production con certificate resolver esterni.

## Scopo

Traefik monta questo contenuto in `/etc/traefik/dynamic` (provider file). La cartella è osservata in tempo reale, quindi ogni volta che si aggiornano router o middleware il proxy ricarica le impostazioni senza restart.

## Esempio di utilizzo dell’applicazione

Quando il container `web` espone `traefik.http.routers.web.rule=Host(\`meepleai.localhost\`)`, Traefik applica i middleware definiti in `middlewares.yml` (es. Basic Auth per Grafana) e usa `tls-localhost.yml` per servire HTTPS con i certificati locali. L’app rimane così protetta da HTTPS anche in sviluppo.
