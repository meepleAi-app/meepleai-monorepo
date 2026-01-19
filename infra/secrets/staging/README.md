# Staging Secrets Directory

This directory contains secrets for the **staging environment**.

## Required Files

Create these files with your staging credentials (plain text, single value per file):

| File | Description | Required |
|------|-------------|----------|
| `postgres-password.txt` | PostgreSQL password | Yes |
| `redis-password.txt` | Redis password | Yes |
| `openrouter-api-key.txt` | OpenRouter API key | Yes |
| `grafana-admin-password.txt` | Grafana admin password | Optional |

## File Format

Each file should contain **only the secret value** (no KEY= prefix):

```
# postgres-password.txt
MySecurePassword123!
```

## Usage

These files are referenced by `compose.staging.yml` as Docker secrets:

```yaml
secrets:
  redis-password:
    file: ./secrets/staging/redis-password.txt
```

## Security

- Never commit these files to git (directory is gitignored)
- Use strong, unique passwords for each service
- Rotate secrets every 90 days
- Store backup in secure vault (1Password, HashiCorp Vault, etc.)

## Quick Setup

```bash
# Generate secure password
openssl rand -base64 24 > postgres-password.txt
openssl rand -base64 24 > redis-password.txt
echo "your-openrouter-api-key" > openrouter-api-key.txt
```
