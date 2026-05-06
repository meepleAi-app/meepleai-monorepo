# Alpha Zero Scope

MeepleAI Alpha Zero: minimal viable feature set for first real users.

## Core User Flow

Sign up → Add game (via BGG) → Upload rulebook (PDF) → Chat about rules (RAG)

## Bounded Contexts

| Context | Status | Alpha Scope |
|---------|--------|------------|
| Authentication | ACTIVE | Email/password + Google OAuth |
| GameManagement | ACTIVE | Game CRUD + BGG search/import + private games |
| DocumentProcessing | ACTIVE | PDF upload + chunking (unstructured-service) |
| KnowledgeBase | ACTIVE | RAG chat with sourced answers |
| UserLibrary | ACTIVE | Collection + private games |
| Administration | PARTIAL | Users + Content sections only |
| SystemConfiguration | PARTIAL | Basic config + feature flags |
| SessionTracking | DORMANT | Hidden in alpha |
| SharedGameCatalog | DORMANT | Hidden in alpha |
| BusinessSimulations | DORMANT | Hidden in alpha |
| Gamification | DORMANT | Hidden in alpha |
| AgentMemory | DORMANT | Hidden in alpha |
| WorkflowIntegration | DORMANT | Hidden in alpha |
| UserNotifications | DORMANT | Hidden in alpha |

## Frontend Routes

### Active
- Auth: login, register, reset-password, verify-email, oauth-callback, setup-account
- Dashboard (cleaned of dormant widgets)
- Library (collection + private tabs)
- Chat (new, thread, agents/create)
- Discover (game catalog)
- Profile
- Admin: overview, users, content (trimmed sidebar)

### Dormant (redirect to /dashboard)
agents, sessions, play-records, players, knowledge-base, game-nights, chess, badges, pipeline-builder, pricing, notifications, n8n, editor, toolkit

## Docker Services

| Service | Alpha | Full |
|---------|-------|------|
| postgres | YES | YES |
| redis | YES | YES |
| api | YES | YES |
| web | YES | YES |
| embedding-service | YES | YES |
| unstructured-service | YES | YES |
| ollama | via --profile ai | YES |
| reranker-service | via --profile ai | YES |
| smoldocling-service | via --profile ai | YES |
| orchestration-service | via --profile ai | YES |
| prometheus/grafana/alertmanager | NO | --profile monitoring |
| n8n | NO | --profile automation |

## How to Toggle

### Enable Alpha Mode

**Local dev (no Docker):**
```bash
# Backend
ALPHA_MODE=true dotnet run  # or set in launchSettings.json

# Frontend (build-time!)
echo "NEXT_PUBLIC_ALPHA_MODE=true" >> apps/web/.env.local
cd apps/web && pnpm dev  # restart required
```

**Docker:**
```bash
cd infra && make alpha      # with AI services
cd infra && make alpha-core # core only
```

### Disable Alpha Mode

```bash
# Remove ALPHA_MODE from environment / set to false
# Remove NEXT_PUBLIC_ALPHA_MODE from .env.local
# Rebuild frontend: cd apps/web && pnpm build
```

## Implementation Details

- **Backend**: `Program.cs` gates ~120 endpoint groups behind `if (!isAlphaMode)`
- **Frontend nav**: In-place filtering of `UNIFIED_NAV_ITEMS`, `LIBRARY_TABS`, `DASHBOARD_SECTIONS`
- **Dashboard**: Dormant widgets return null via `IS_ALPHA_MODE` check
- **Middleware**: Route guard redirects non-alpha paths to `/dashboard`
- **Docker**: `compose.alpha.yml` override sets env vars + build args
- **Reversible**: Set vars to false, no code deletion occurred
