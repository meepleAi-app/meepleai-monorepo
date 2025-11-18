# Docker Secrets Directory

This directory contains sensitive secrets for local development using Docker Secrets. If a secret script accidentally creates a directory instead of a file (common on Windows when the script is interrupted), delete the folder and recreate the `.txt` file with the instructions below before restarting Docker.

## ⚠️ Security

**IMPORTANT**:
- All `.txt` files in this directory are **git-ignored**
- Never commit actual secret values to version control
- Use `tools/secrets/init-secrets.sh` to initialize secrets from templates

## Secrets Structure

| Secret File | Purpose | Used By | How to Generate |
|-------------|---------|---------|-----------------|
| `postgres-password.txt` | PostgreSQL database password | meepleai-postgres, meepleai-api, meepleai-n8n | `openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24` *(or see PowerShell example below)* |
| `openrouter-api-key.txt` | OpenRouter API key for LLM usage | meepleai-api | Create at https://openrouter.ai/account/api-keys and paste the value |
| `n8n-encryption-key.txt` | Encrypts n8n credentials/secrets | meepleai-n8n | `openssl rand -base64 32` |
| `n8n-basic-auth-password.txt` | Protects n8n UI with Basic Auth | meepleai-n8n | `openssl rand -base64 18 | tr -dc 'A-Za-z0-9' | head -c 18` |
| `gmail-app-password.txt` | Google App Password for SMTP alerts | meepleai-alertmanager | Google Account -> Security -> App Passwords (Other -> "MeepleAI Alertmanager") |
| `grafana-admin-password.txt` | Grafana admin login password | meepleai-grafana | `openssl rand -base64 18 | tr -dc 'A-Za-z0-9' | head -c 18` |
| `initial-admin-password.txt` | Bootstrap admin password for MeepleAI API | meepleai-api | `openssl rand -base64 18 | tr -dc 'A-Za-z0-9' | head -c 18` |
| `n8n-service-session.txt` *(optional)* | Service token for n8n workflows | meepleai-n8n workflows | `pwsh tools/setup-n8n-service-account.ps1` generates both token and `.env` file |


## Secret Setup Checklist

1. **Run the template initializer (one time)**
   ```bash
   cd tools/secrets
   ./init-secrets.sh
   ```
   This creates `*.txt` files from the `.example` templates.

2. **Fill each file with the generated secret**
   ```bash
   # Example: PostgreSQL
   openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24 > infra/secrets/postgres-password.txt

   # Example: n8n encryption key
   openssl rand -base64 32 > infra/secrets/n8n-encryption-key.txt

   # Example: OpenRouter API key (paste the value you copy from the dashboard)
   echo "sk-live-xxxxxxxxxxxxxxxx" > infra/secrets/openrouter-api-key.txt
   ```

   **Windows / PowerShell quick reference**
   ```powershell
   $secrets = "D:/Repositories/meepleai-monorepo/infra/secrets"
   Set-Content -NoNewline -Path "$secrets/postgres-password.txt" -Value ([System.Web.Security.Membership]::GeneratePassword(24,4))
   Set-Content -NoNewline -Path "$secrets/n8n-encryption-key.txt" -Value (-join ((1..32) | ForEach-Object { [char](Get-Random -Minimum 48 -Maximum 123) }))
   Set-Content -NoNewline -Path "$secrets/n8n-basic-auth-password.txt" -Value "admin123"          # replace with a stronger password
   Set-Content -NoNewline -Path "$secrets/grafana-admin-password.txt" -Value "admin"              # replace with a stronger password
   Set-Content -NoNewline -Path "$secrets/initial-admin-password.txt" -Value "Admin123!ChangeMe"
   Set-Content -NoNewline -Path "$secrets/gmail-app-password.txt" -Value "<Google App Password>"
   Set-Content -NoNewline -Path "$secrets/openrouter-api-key.txt" -Value "<OpenRouter key>"
   ```
   After writing the files run `Get-ChildItem $secrets -File` to ensure each entry is a file and not a directory.

3. **Regenerate secrets later with `rotate-secret.sh`**
   ```bash
   cd tools/secrets
   ./rotate-secret.sh postgres-password
   ```
   The script backs up the old value, prompts for a new one (or auto-generates), and reminds you which services to restart.

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

## References

- [Docker Secrets Documentation](https://docs.docker.com/engine/swarm/secrets/)
- [MeepleAI Secrets Management Guide](../../docs/guide/secrets-management.md)
- Issue #708: Secrets management strategy
