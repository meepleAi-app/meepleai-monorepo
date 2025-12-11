# Traefik Certificates

## Contenuto

- `localhost.crt`, `localhost.key`, `localhost.pfx`: certificati autofirmati usati per TLS locale da Traefik.

## Scopo

Permettono a Traefik di servire HTTPS in sviluppo senza CA esterne. Sono montati nel container e referenziati dal file `infra/traefik/dynamic/tls-localhost.yml`.

## Esempio di utilizzo dell’applicazione

Quando MeepleAI Web chiama `https://api.meepleai.localhost`, Traefik utilizza questi certificati per terminare TLS e inoltrare la richiesta al container `api`. Questo consente di testare CORS/HTTPS senza errori di certificato nei browser durante lo sviluppo.
