# Docker Secrets Directory

This directory contains sensitive secrets for local development using Docker Secrets.

## ⚠️ Security

**IMPORTANT**:
- All `.txt` files in this directory are **git-ignored**
- Never commit actual secret values to version control
- Use `tools/secrets/init-secrets.sh` to initialize secrets from templates

## Secrets Structure

| Secret File | Purpose | Used By |
|-------------|---------|---------|
| `postgres-password.txt` | PostgreSQL database password | postgres, api, n8n |
| `openrouter-api-key.txt` | OpenRouter API key for LLM | api |
| `n8n-encryption-key.txt` | n8n workflow encryption | n8n |
| `n8n-basic-auth-password.txt` | n8n UI authentication | n8n |
| `gmail-app-password.txt` | Gmail App Password for alerts | alertmanager |
| `grafana-admin-password.txt` | Grafana admin UI password | grafana |
| `initial-admin-password.txt` | API bootstrap admin password | api |

## Initialization

```bash
# Initialize all secrets from templates
cd tools/secrets
./init-secrets.sh

# Or manually create secret files:
cd infra/secrets
echo "your-postgres-password" > postgres-password.txt
echo "your-openrouter-key" > openrouter-api-key.txt
# ... etc
```

## Usage in Docker Compose

Secrets are mounted at `/run/secrets/<secret-name>` in containers.

Example:
```yaml
services:
  postgres:
    secrets:
      - postgres-password
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/postgres-password

secrets:
  postgres-password:
    file: ./secrets/postgres-password.txt
```

## Rotation

See `docs/guide/secrets-management.md` for rotation procedures.

Quick rotation:
```bash
cd tools/secrets
./rotate-secret.sh postgres-password
```

## Troubleshooting

**Secret file not found**:
```bash
ls -la infra/secrets/*.txt
# If empty, run: tools/secrets/init-secrets.sh
```

**Permission denied**:
```bash
chmod 600 infra/secrets/*.txt
```

## Esempio di utilizzo dell’applicazione

- Quando MeepleAI API si avvia nel container Docker, monta `postgres-password` e `openrouter-api-key` come secrets Docker e legge `/run/secrets/postgres-password` per connettersi al database e `/run/secrets/openrouter-api-key` per chiamare il provider LLM. n8n e Alertmanager usano lo stesso meccanismo per `n8n-basic-auth-password.txt`, `n8n-encryption-key.txt` e `gmail-app-password.txt`, quindi nessuna password sensibile viene mai scritta nei file di configurazione `.env` committati.

## References

- [Docker Secrets Documentation](https://docs.docker.com/engine/swarm/secrets/)
- [MeepleAI Secrets Management Guide](../../docs/guide/secrets-management.md)
- Issue #708: Secrets management strategy
