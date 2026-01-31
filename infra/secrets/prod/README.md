# Production Secrets Directory

This directory contains secrets for the **production environment**.

## Required Files

Create these files with your production credentials (plain text, single value per file):

| File | Description | Required |
|------|-------------|----------|
| `postgres-password.txt` | PostgreSQL password | **Critical** |
| `redis-password.txt` | Redis password | **Critical** |
| `openrouter-api-key.txt` | OpenRouter API key | **Critical** |
| `grafana-admin-password.txt` | Grafana admin password | Important |
| `n8n-encryption-key.txt` | n8n encryption key | Important |
| `n8n-basic-auth-password.txt` | n8n basic auth | Important |
| `gmail-app-password.txt` | Gmail app password (alerts) | Optional |
| `initial-admin-password.txt` | Initial admin user password | Important |
| `api-cert-password.txt` | SSL certificate password | If using HTTPS |
| `api-cert.pfx` | SSL certificate file | If using HTTPS |

## File Format

Each file should contain **only the secret value** (no KEY= prefix):

```
# postgres-password.txt
MyVerySecureProductionPassword!@#$
```

## Security Requirements

- **Strong Passwords**: Minimum 20 characters, mixed case, numbers, symbols
- **Unique Per Service**: Never reuse passwords across services
- **Rotation**: Rotate all secrets every 90 days
- **Backup**: Store encrypted backup in secure vault
- **Access Control**: Limit who can access this directory

## Usage

These files are referenced by `compose.prod.yml` as Docker secrets:

```yaml
secrets:
  postgres-password:
    file: ./secrets/prod/postgres-password.txt
  api-cert.pfx:
    file: ./secrets/prod/api-cert.pfx
```

## Quick Setup (Secure)

```bash
# Generate cryptographically secure passwords
openssl rand -base64 32 > postgres-password.txt
openssl rand -base64 32 > redis-password.txt
openssl rand -base64 32 > n8n-encryption-key.txt
openssl rand -base64 24 > grafana-admin-password.txt
openssl rand -base64 24 > initial-admin-password.txt

# API keys must be obtained from providers
echo "sk-or-v1-..." > openrouter-api-key.txt
```

## SSL Certificate Setup

For HTTPS in production:

```bash
# Generate self-signed cert (for testing only)
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365 -nodes
openssl pkcs12 -export -out api-cert.pfx -inkey key.pem -in cert.pem

# Save certificate password
echo "your-cert-password" > api-cert-password.txt
```

For production, use proper certificates from Let's Encrypt or your CA.

## Pre-Deployment Checklist

- [ ] All required files created
- [ ] No placeholder values remaining
- [ ] Passwords meet complexity requirements
- [ ] Backup stored in secure vault
- [ ] Access permissions restricted (chmod 600)
- [ ] Files not tracked in git
