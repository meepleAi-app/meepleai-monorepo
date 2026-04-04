# Secret Files - Template Directory

This directory contains **template files** for the secret management system.

## Usage

1. **Copy template files** to `infra/secrets/` directory:
   ```bash
   cp infra/secrets.example/*.secret.example infra/secrets/
   ```

2. **Rename files** (remove `.example` extension):
   ```bash
   cd infra/secrets
   for file in *.secret.example; do mv "$file" "${file%.example}"; done
   ```

3. **Edit files** with your actual credentials:
   ```bash
   nano database.secret  # or your preferred editor
   ```

## Secret Levels

### 🔴 CRITICAL (Startup blocked if missing)
- `database.secret` - PostgreSQL credentials
- `redis.secret` - Redis password
- `jwt.secret` - JWT signing key
- `admin.secret` - Initial admin user
- `embedding-service.secret` - Embedding service API key
- ~~`qdrant.secret`~~ - **Removed** (Issue #4861): pgvector uses PostgreSQL — no separate key needed

### 🟡 IMPORTANT (Warning logged if missing)
- `openrouter.secret` - OpenRouter API key
- `unstructured-service.secret` - PDF processing API key
- `bgg.secret` - BoardGameGeek credentials

### 🟢 OPTIONAL (Info logged if missing)
- `oauth.secret` - Google/GitHub OAuth credentials
- `email.secret` - SMTP server settings
- `storage.secret` - S3-compatible storage credentials
- `monitoring.secret` - Grafana/Prometheus passwords
- `traefik.secret` - Traefik dashboard credentials
- `smoldocling-service.secret` - Document intelligence API key
- `reranker-service.secret` - Reranker API key

## File Format

Each secret file uses **KEY=VALUE** format:

```ini
# Comments start with #
KEY_NAME=value_here
ANOTHER_KEY=another_value

# Empty lines are ignored
```

## Security

- ✅ **DO**: Keep `infra/secrets/` directory in `.gitignore`
- ✅ **DO**: Use strong passwords and API keys
- ✅ **DO**: Rotate secrets regularly
- ❌ **DON'T**: Commit actual secret files to git
- ❌ **DON'T**: Share secret files via insecure channels

## Docker Compose

The `docker-compose.yml` file is configured to mount `infra/secrets/` as volumes:

```yaml
volumes:
  - ./secrets:/app/infra/secrets:ro  # Read-only mount
```

## Generating Strong Secrets

### JWT Secret Key
```bash
openssl rand -base64 64
```

### PostgreSQL Password
```bash
openssl rand -base64 32
```

### Redis Password
```bash
openssl rand -hex 32
```

## Troubleshooting

**Issue**: Startup fails with "CRITICAL secrets missing"
- **Solution**: Check logs for specific missing secrets, then copy and configure template files

**Issue**: "Secrets directory not found"
- **Solution**: Ensure `infra/secrets/` directory exists and contains `.secret` files

**Issue**: "Parse error in secret file"
- **Solution**: Verify KEY=VALUE format, check for special characters, ensure UTF-8 encoding

## Related Documentation

- [Secrets Management Guide](../../docs/04-deployment/secrets-management.md)
- [Development Setup](../../docs/02-development/README.md)
- [Deployment Guide](../../docs/04-deployment/README.md)
