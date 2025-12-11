# Environment Variable Guide (infra/env)

This folder contains the `.env` files used by Docker Compose and standalone tooling.  
Every file has an `.example` template - copy it, rename it without the `.example` suffix, and fill in the values as described here. If a command accidentally creates a directory instead of a file (sometimes happens on Windows), delete the folder and recreate the `.env` file before starting Docker so `_FILE` references resolve correctly.

> **Tip**  
> Run `tools/secrets/init-secrets.sh` first: it creates the Docker secret files in `infra/secrets/` so any `_FILE` references already work.

| File | Environment | Service | How to create |
|------|-------------|---------|---------------|
| `api.env.dev.example` | Development | API | `cp api.env.dev.example api.env.dev` |
| `api.env.staging.example` | Staging | API | `cp api.env.staging.example api.env.staging` |
| `api.env.prod.example` | Production | API | `cp api.env.prod.example api.env.prod` |
| `api.env.ci.example` | CI/CD | API | Copy into GitHub secrets |
| `web.env.dev.example` | Development | Web | `cp web.env.dev.example web.env.dev` |
| `web.env.staging.example` | Staging | Web | `cp web.env.staging.example web.env.staging` |
| `web.env.prod.example` | Production | Web | `cp web.env.prod.example web.env.prod` |
| `web.env.ci.example` | CI/CD | Web | Copy into GitHub secrets |
| `n8n.env.dev.example` | Development | n8n | `cp n8n.env.dev.example n8n.env.dev` |
| `n8n.env.staging.example` | Staging | n8n | `cp n8n.env.staging.example n8n.env.staging` |
| `n8n.env.prod.example` | Production | n8n | `cp n8n.env.prod.example n8n.env.prod` |
| `n8n.env.ci.example` | CI/CD | n8n | Copy into GitHub secrets |
| `alertmanager.env.example` | All | Alertmanager | `cp alertmanager.env.example alertmanager.env` |

**Experimental** (in `../experimental/`):
| File | Environment | Service | How to create |
|------|-------------|---------|---------------|
| `infisical.env.example` | POC | Infisical | `cp ../experimental/infisical.env.example ../experimental/infisical.env` |

## Quick setup commands

### Development Environment

Run from the repo root to generate all development `.env` files:

**macOS / Linux**
```bash
cd infra/env
cp api.env.dev.example api.env.dev
cp web.env.dev.example web.env.dev
cp n8n.env.dev.example n8n.env.dev
cp alertmanager.env.example alertmanager.env
```

**Windows PowerShell**
```powershell
cd infra/env
Copy-Item api.env.dev.example api.env.dev
Copy-Item web.env.dev.example web.env.dev
Copy-Item n8n.env.dev.example n8n.env.dev
Copy-Item alertmanager.env.example alertmanager.env
Get-ChildItem . -Filter "*.env" | Select-Object Name, Length
```

### Staging Environment

```bash
cd infra/env
cp api.env.staging.example api.env.staging
cp web.env.staging.example web.env.staging
cp n8n.env.staging.example n8n.env.staging
# Edit files to replace <REPLACE_*> placeholders
vim api.env.staging web.env.staging n8n.env.staging
```

### Production Environment

```bash
cd infra/env
cp api.env.prod.example api.env.prod
cp web.env.prod.example web.env.prod
cp n8n.env.prod.example n8n.env.prod
# ⚠️  CRITICAL: Edit files to replace <REPLACE_*> with production values
# ⚠️  Store in secure secret manager (AWS Secrets Manager, Azure Key Vault)
vim api.env.prod web.env.prod n8n.env.prod
```

**Note**: Confirm each file shows a non-zero `Length` before running `docker compose up`.

Below you’ll find the variables that require manual input and how to obtain each one.

---

## 1. `api.env.dev`
| Key | Description | How to set |
|-----|-------------|-----------|
| `EMBEDDING_PROVIDER` | `ollama` for the local container or `openrouter` for cloud embeddings (via OpenRouter) | Leave `ollama` for local dev |
| `OLLAMA_URL` / `EMBEDDING_MODEL` | Endpoint + model for embeddings | Default `http://ollama:11434` / `mxbai-embed-large` |
| `JWT_ISSUER` | Token issuer used by the API | Usually `http://localhost:8080` |
| `ALLOW_ORIGIN` | Allowed origin for CORS | `http://localhost:3000` when running Next.js locally |
| `SEQ_URL` | Seq ingestion URL | `http://seq:5341` inside Docker; use `http://localhost:8081` when hitting from host |
| `INITIAL_ADMIN_EMAIL` | Email for the bootstrap admin account | Any address you control (default `admin@meepleai.dev`) |
| `N8N_ENCRYPTION_KEY` | Only used when running API outside Docker (within containers it comes from secrets) | Generate with `openssl rand -base64 32` if needed |

> Passwords/API keys (OpenRouter, initial admin password, Postgres) are injected via `/run/secrets/...` – no need to add them to `.env`.

### `api.env.ci.example`
Used by CI/CD jobs. Copy the file content into your pipeline variables (e.g., GitHub Actions `env:` block) and change:
- `POSTGRES_USER`, `POSTGRES_DB` if your CI database uses different names.
- `ConnectionStrings__Postgres` host should point to `postgres` inside the compose network.

---

## 2. `web.env.dev` / `web.env.ci.example`
| Key | Description | How to set |
|-----|-------------|-----------|
| `NEXT_PUBLIC_API_BASE` | URL the Next.js app calls | `http://localhost:8080` (dev server); `http://api:8080` inside Docker |
| `NEXT_PUBLIC_TENANT_ID` | Logical tenant label | `dev`, `ci`, etc. |

These values are safe to commit if you want shared defaults; the file is gitignored so you can customize per developer.

---

## 3. `n8n.env.dev`
| Key | Description | How to set |
|-----|-------------|-----------|
| `DB_POSTGRESDB_HOST/PORT/...` | Database connection for n8n | Keep defaults (`postgres`, `5432`, `meepleai`) |
| `MEEPLEAI_API_URL` | Base URL the workflows call | `http://api:8080` inside Docker |
| `N8N_SERVICE_SESSION` | Service-account session token for authenticated workflows | Generate with `pwsh tools/setup-n8n-service-account.ps1` and paste the output |
| `N8N_BASIC_AUTH_USER` | Username for Basic Auth | e.g. `admin` |

Secrets:
- `DB_POSTGRESDB_PASSWORD` → `infra/secrets/postgres-password.txt`
- `N8N_BASIC_AUTH_PASSWORD` → `infra/secrets/n8n-basic-auth-password.txt`
- `N8N_ENCRYPTION_KEY` → `infra/secrets/n8n-encryption-key.txt`

### `n8n.env.ci.example`
Same keys as above but tailored for CI. Generate a separate `N8N_SERVICE_SESSION` token if you plan to run automated tests against CI n8n.

---

## 4. `alertmanager.env`
| Key | Description | How to set |
|-----|-------------|-----------|
| `GMAIL_APP_PASSWORD` | SMTP password for Gmail | Create a Google **App Password** (Account → Security → App Passwords → “Mail”) and store it in `infra/secrets/gmail-app-password.txt` |
| `SLACK_WEBHOOK_URL` (optional) | Slack channel webhook | Create an Incoming Webhook in Slack (Workspace → Apps → “Incoming Webhooks”) and paste the HTTPS URL |

The rest of the SMTP fields live in `infra/alertmanager.yml`; no need to duplicate them here.

---

## 5. `infisical.env` (Experimental)

**Location**: `../experimental/infisical.env.example`

Used only for the Infisical POC stack (Issue #936). Required values:

| Key | How to set |
|-----|-----------|
| `INFISICAL_ENCRYPTION_KEY` | `openssl rand -hex 16` |
| `INFISICAL_AUTH_SECRET` | `openssl rand -base64 32` |
| `INFISICAL_DB_PASSWORD` | Any strong password (also reused for `postgresql://infisical:<pwd>@...`) |
| `SMTP_*` | SMTP relay you plan to use (can leave defaults for Gmail) |

Copy and replace the placeholders:
```bash
cd ../experimental
cp infisical.env.example infisical.env
# Edit infisical.env with your values
docker compose -f docker-compose.infisical.yml up -d
```

---

## 6. `api.env.ci`, `web.env.ci`, `n8n.env.ci`
These `.example` files are templates for your CI/CD platform. Recommended workflow:

1. Copy the desired `.example` into your secret manager (GitHub Actions → Repository Settings → Secrets and variables).
2. Replace service endpoints with whatever network layout you use in CI (e.g., `postgres`, `api`, `qdrant`).
3. Inject the secret into the job via `env:` or `--env-file`.

---

## Verification Checklist
- [ ] `infra/secrets/*.txt` populated (see secrets README)
- [ ] `api.env.dev`, `web.env.dev`, `n8n.env.dev`, `alertmanager.env` exist
- [ ] All required API keys (OpenRouter) created and stored safely
- [ ] Optional: `../experimental/infisical.env` if you plan to run the Infisical POC

Once the files are in place you can start the stack:
```bash
cd infra
# Development mode (recommended)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d

# Or start specific services only
docker compose up -d postgres qdrant redis ollama api web
```
If anything is missing, Docker will complain about unreadable secrets or env files – refer back to this guide for the exact key/value requirements.

## Esempio di utilizzo dell’applicazione

- Quando MeepleAI API viene avviata, il container carica `infra/env/api.env.dev` (o il file corrispondente all’ambiente) per impostare `POSTGRES_HOST`, `OPENROUTER_API_KEY`, `SEQ_URL` e altri parametri. Allo stesso modo, il front-end Next.js legge `infra/env/web.env.dev` per sapere che deve chiamare `http://localhost:8080`, mentre n8n usa `infra/env/n8n.env.dev` per connettersi all’API `http://api:8080` e all’istanza PostgreSQL.
