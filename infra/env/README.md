# Environment Variable Guide (infra/env)

This folder contains the `.env` files used by Docker Compose and standalone tooling.  
Every file has an `.example` template - copy it, rename it without the `.example` suffix, and fill in the values as described here. If a command accidentally creates a directory instead of a file (sometimes happens on Windows), delete the folder and recreate the `.env` file before starting Docker so `_FILE` references resolve correctly.

> **Tip**  
> Run `tools/secrets/init-secrets.sh` first: it creates the Docker secret files in `infra/secrets/` so any `_FILE` references already work.

| File | Purpose | How to create |
|------|---------|---------------|
| `api.env.dev` | Local ASP.NET API settings | `cp infra/env/api.env.dev.example infra/env/api.env.dev` |
| `api.env.ci.example` | Template for CI pipelines (GitHub Actions) | Copy into GitHub repo secrets / CI variables |
| `web.env.dev` | Next.js dev server overrides | `cp infra/env/web.env.dev.example infra/env/web.env.dev` |
| `web.env.ci.example` | Web CI defaults | Inject into CI environment if needed |
| `n8n.env.dev` | n8n workflow automation (dev) | `cp infra/env/n8n.env.dev.example infra/env/n8n.env.dev` |
| `n8n.env.ci.example` | n8n CI template | Use for pipeline environments |
| `alertmanager.env` | Alertmanager notification settings | `cp infra/env/alertmanager.env.example infra/env/alertmanager.env` |
| `mcp.env.dev` | API keys for MCP helper services | `cp infra/env/mcp.env.dev.example infra/env/mcp.env.dev` |
| `infisical.env` | Self-hosted Infisical POC | `cp infra/env/infisical.env.example infra/env/infisical.env` |

## Quick setup commands

Run one of the following snippets from the repo root to generate all non-CI `.env` files in one shot.

**macOS / Linux**
```bash
cp infra/env/api.env.dev.example infra/env/api.env.dev
cp infra/env/web.env.dev.example infra/env/web.env.dev
cp infra/env/n8n.env.dev.example infra/env/n8n.env.dev
cp infra/env/alertmanager.env.example infra/env/alertmanager.env
cp infra/env/mcp.env.dev.example infra/env/mcp.env.dev
cp infra/env/infisical.env.example infra/env/infisical.env
```

**Windows PowerShell**
```powershell
$envDir = "D:/Repositories/meepleai-monorepo/infra/env"
Copy-Item "$envDir/api.env.dev.example" "$envDir/api.env.dev"
Copy-Item "$envDir/web.env.dev.example" "$envDir/web.env.dev"
Copy-Item "$envDir/n8n.env.dev.example" "$envDir/n8n.env.dev"
Copy-Item "$envDir/alertmanager.env.example" "$envDir/alertmanager.env"
Copy-Item "$envDir/mcp.env.dev.example" "$envDir/mcp.env.dev"
Copy-Item "$envDir/infisical.env.example" "$envDir/infisical.env"
Get-ChildItem $envDir -Filter "*.env" | Select-Object Name, Length
```

Confirm each file shows a non-zero `Length` so Docker sees real files instead of directories before running `docker compose up`.

Below you’ll find the variables that require manual input and how to obtain each one.

---

## 1. `api.env.dev`
| Key | Description | How to set |
|-----|-------------|-----------|
| `EMBEDDING_PROVIDER` | `ollama` for the local container or `openai` for cloud embeddings | Leave `ollama` for local dev |
| `OLLAMA_URL` / `EMBEDDING_MODEL` | Endpoint + model for embeddings | Default `http://meepleai-ollama:11434` / `nomic-embed-text` |
| `JWT_ISSUER` | Token issuer used by the API | Usually `http://localhost:5080` |
| `ALLOW_ORIGIN` | Allowed origin for CORS | `http://localhost:3000` when running Next.js locally |
| `SEQ_URL` | Seq ingestion URL | `http://meepleai-seq:5341` inside Docker; use `http://localhost:5341` when hitting the container from host |
| `INITIAL_ADMIN_EMAIL` | Email for the bootstrap admin account | Any address you control (default `admin@meepleai.dev`) |
| `N8N_ENCRYPTION_KEY` | Only used when running API outside Docker (within containers it comes from secrets) | Generate with `openssl rand -base64 32` if needed |

> Passwords/API keys (OpenRouter, initial admin password, Postgres) are injected via `/run/secrets/...` – no need to add them to `.env`.

### `api.env.ci.example`
Used by CI/CD jobs. Copy the file content into your pipeline variables (e.g., GitHub Actions `env:` block) and change:
- `POSTGRES_USER`, `POSTGRES_DB` if your CI database uses different names.
- `ConnectionStrings__Postgres` host should point to `meepleai-postgres` inside the compose network.

---

## 2. `web.env.dev` / `web.env.ci.example`
| Key | Description | How to set |
|-----|-------------|-----------|
| `NEXT_PUBLIC_API_BASE` | URL the Next.js app calls | `http://localhost:5080` (dev server); `http://meepleai-api:8080` inside Docker |
| `NEXT_PUBLIC_TENANT_ID` | Logical tenant label | `dev`, `ci`, etc. |

These values are safe to commit if you want shared defaults; the file is gitignored so you can customize per developer.

---

## 3. `n8n.env.dev`
| Key | Description | How to set |
|-----|-------------|-----------|
| `DB_POSTGRESDB_HOST/PORT/...` | Database connection for n8n | Keep defaults (`meepleai-postgres`, `5432`, `meepleai`) |
| `MEEPLEAI_API_URL` | Base URL the workflows call | `http://meepleai-api:8080` inside Docker |
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

## 5. `mcp.env.dev`
Each MCP service has its own API key:

| Section | Variable | How to get it |
|---------|----------|---------------|
| GitHub MCP | `GITHUB_TOKEN` | Personal Access Token with `repo`, `workflow`, `admin:org` scopes ([GitHub → Settings → Developer settings → Personal access tokens](https://github.com/settings/tokens)) |
| | `GITHUB_OWNER` / `GITHUB_REPO` | Repository owner/name (`DegrassiAaron`, `meepleai-monorepo`) |
| n8n MCP | `N8N_BASE_URL`, `N8N_API_KEY` | Create an API key inside the n8n UI (Settings → API Keys) |
| Magic MCP | `MAGIC_API_KEY`, `TWENTYFIRST_API_KEY` | 21st.dev console (https://21st.dev/magic/console) |
| Context7 MCP | `CONTEXT7_API_KEY` | Upstash Context7 console (https://console.upstash.com/context7) |
| Knowledge Graph MCP | `KG_QDRANT_URL` | Default `http://meepleai-qdrant:6333`; no API key required |

Keep this file out of git (`.gitignore` already covers it).

---

## 6. `infisical.env`
Used only for the Infisical POC stack. Required values:

| Key | How to set |
|-----|-----------|
| `INFISICAL_ENCRYPTION_KEY` | `openssl rand -hex 16` |
| `INFISICAL_AUTH_SECRET` | `openssl rand -base64 32` |
| `INFISICAL_DB_PASSWORD` | Any strong password (also reused for `postgresql://infisical:<pwd>@...`) |
| `SMTP_*` | SMTP relay you plan to use (can leave defaults for Gmail) |

The same values live in `infisical.env.example`; copy and replace the placeholders before bringing up `docker-compose.infisical.yml`.

---

## 7. `api.env.ci`, `web.env.ci`, `n8n.env.ci`
These `.example` files are templates for your CI/CD platform. Recommended workflow:

1. Copy the desired `.example` into your secret manager (GitHub Actions → Repository Settings → Secrets and variables).  
2. Replace `http://meepleai-*` endpoints with whatever network layout you use in CI.  
3. Inject the secret into the job via `env:` or `--env-file`.

---

## Verification Checklist
- [ ] `infra/secrets/*.txt` populated (see secrets README)
- [ ] `api.env.dev`, `web.env.dev`, `n8n.env.dev`, `alertmanager.env`, `mcp.env.dev` exist
- [ ] All required API keys (OpenRouter, GitHub, n8n, Magic, Context7) created and stored safely
- [ ] Optional: `infisical.env` if you plan to run the Infisical stack

Once the files are in place you can start the stack:
```bash
cd infra
docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-ollama meepleai-api meepleai-web
```
If anything is missing, Docker will complain about unreadable secrets or env files – refer back to this guide for the exact key/value requirements.
