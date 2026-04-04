# SSH console

SSH al server e lancia:

    ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69

Poi sul server:

    cd /opt/meepleai

# Carica secrets

    export $(grep -vhsE '^#|^$' secrets/*.secret | grep '=' | sed 's/"//g' | xargs -d '\n' 2>/dev/null) 2>/dev/null

# Restart API e Web (le immagini sono già buildate)

    docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml \     up -d --no-deps api web

# Verifica health

    sleep 10 && curl -sf http://localhost:8080/health | head -5

Se dà errore su secret mancanti tipo oauth.secret, email.secret etc nella cartella secrets/, creali vuoti:

    touch secrets/{oauth,email,n8n,storage,smoldocling,reranker-service,unstructured-service,bgg,slack}.secret

E poi rilancia il docker compose up sopra.
