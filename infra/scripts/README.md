# Infrastructure Scripts

## Contenuto

Script di utilità per gestione dell'infrastruttura Docker e secrets management.

## Scripts

### load-secrets-env.sh

**Scopo**: Carica secrets da Docker secrets e li espone come variabili d'ambiente.

**Utilizzo**:
```bash
# Source lo script per caricare secrets nell'ambiente corrente
source ./scripts/load-secrets-env.sh

# I secrets vengono letti da /run/secrets/ e esposti come env vars
echo $POSTGRES_PASSWORD
echo $OPENROUTER_API_KEY
```

**Secrets Supportati**:
- `postgres-password` → `POSTGRES_PASSWORD`
- `openrouter-api-key` → `OPENROUTER_API_KEY`
- `n8n-encryption-key` → `N8N_ENCRYPTION_KEY`
- `n8n-basic-auth-password` → `N8N_BASIC_AUTH_PASSWORD`
- `gmail-app-password` → `GMAIL_APP_PASSWORD`
- `grafana-admin-password` → `GRAFANA_ADMIN_PASSWORD`
- `initial-admin-password` → `INITIAL_ADMIN_PASSWORD`

**Come Funziona**:
1. Controlla se `/run/secrets/<secret-name>` esiste
2. Legge il contenuto del file secret
3. Esporta come variabile d'ambiente
4. Rimuove newline e spazi

**Esempio**:
```bash
#!/bin/bash
source ./scripts/load-secrets-env.sh

# Ora puoi usare le variabili
docker compose up -d
```

## Requisiti

- Docker secrets configurati (vedi `../secrets/README.md`)
- Bash shell
- Permessi di lettura su `/run/secrets/`

## Note

- **Sicurezza**: Non loggare mai il valore delle secrets
- **Production**: Sempre usare Docker secrets, mai env vars in chiaro
- **Development**: Opzionale, puoi usare `.env.dev` invece

## Related Scripts

Per altri script di infrastruttura vedi:
- `../secrets/README.md` - Generazione e gestione secrets
- `../../tools/secrets/init-secrets.sh` - Inizializzazione secrets
- `../../tools/cleanup-caches.sh` - Pulizia cache e build artifacts

## Aggiungere Nuovi Script

Quando aggiungi script:
1. Documentali in questo README
2. Aggiungi permessi di esecuzione: `chmod +x script.sh`
3. Includi help text: `./script.sh --help`
4. Gestisci errori appropriatamente
5. Logga operazioni critiche

## Troubleshooting

### Secret Not Found

**Errore**: `Secret file not found: /run/secrets/postgres-password`

**Fix**:
```bash
# Inizializza secrets
cd ../secrets
./init-secrets.sh

# Verifica
ls -la /run/secrets/
```

### Permission Denied

**Errore**: `Permission denied: /run/secrets/postgres-password`

**Fix**:
```bash
# Secrets devono essere leggibili dal processo
# Verifica permessi Docker Swarm o Docker Compose secrets
docker secret ls
```

## Best Practices

1. **Mai hardcodare secrets** negli script
2. **Sempre validare** che secrets esistano prima dell'uso
3. **Usare set -e** per exit on error
4. **Loggare operazioni** ma mai valori sensibili
5. **Documentare parametri** e opzioni
