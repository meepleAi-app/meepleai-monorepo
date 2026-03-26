# Admin Secrets Management — Design Spec

**Date**: 2026-03-18
**Status**: Approved
**Branch**: `main-dev`

## Problem Statement

After deploying to staging (or resetting the DB), API keys for external services (OpenRouter, BGG, OAuth, SMTP) need to be configured manually by editing `.secret` files on the server via SSH. This is error-prone and slow — the admin has no way to see which secrets are missing or to update them from the UI.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Target environment | Staging/dev only | No vault/encryption needed, simplicity over security hardening |
| Location in UI | `/admin/monitor/services` as new "Secrets" tab | Next to health checks that show which services are unhealthy due to missing secrets |
| Secret scope | All `.secret` files, grouped by file | Admin sees everything, decides what to edit. Infra secrets get warning badge. |
| Value display | Masked by default, reveal on click | Balance between usability and not exposing keys at a glance |
| Reload mechanism | Graceful shutdown + Docker restart | `IHostApplicationLifetime.StopApplication()`, container `restart: always` handles restart |
| Auth | SuperAdmin only | Most sensitive admin operation |

## API Endpoints

### GET `/api/v1/admin/secrets`

Returns all secret files with their keys and masked values.

**Auth**: SuperAdmin session required.

**Query params**:
- `reveal=true` — return full values instead of masked (optional)

**Response** (200):
```json
{
  "secretsDirectory": "/app/infra/secrets",
  "files": [
    {
      "fileName": "openrouter.secret",
      "category": "OpenRouter",
      "isInfra": false,
      "entries": [
        {
          "key": "OPENROUTER_API_KEY",
          "maskedValue": "sk-or-v1-****LDER",
          "hasValue": true,
          "isPlaceholder": true
        },
        {
          "key": "OPENROUTER_DEFAULT_MODEL",
          "maskedValue": "meta-llama/llama-3.3-70b-instruct:free",
          "hasValue": true,
          "isPlaceholder": false
        }
      ]
    },
    {
      "fileName": "database.secret",
      "category": "Database",
      "isInfra": true,
      "entries": [...]
    }
  ]
}
```

**Masking logic**:
- Values ≤ 4 chars: `****`
- Values 5-10 chars: first 2 + `****` + last 2
- Values > 10 chars: first 6 + `****` + last 4
- Known placeholders (`PLACEHOLDER`, `change_me`, `your_`, empty): `isPlaceholder: true`

**Category mapping** (derived from filename):
```
openrouter.secret → "OpenRouter"
database.secret → "Database" (isInfra: true)
redis.secret → "Redis" (isInfra: true)
qdrant.secret → "Qdrant" (isInfra: true)
jwt.secret → "JWT" (isInfra: true)
admin.secret → "Admin" (isInfra: true)
oauth.secret → "OAuth"
bgg.secret → "BGG"
email.secret → "Email"
embedding-service.secret → "Embedding Service"
reranker-service.secret → "Reranker Service"
monitoring.secret → "Monitoring"
storage.secret → "Storage"
slack.secret → "Slack"
* → titleCase(filename without .secret)
```

### PUT `/api/v1/admin/secrets`

Updates secret values in `.secret` files on disk.

**Auth**: SuperAdmin session required.

**Request body**:
```json
{
  "updates": [
    { "fileName": "openrouter.secret", "key": "OPENROUTER_API_KEY", "value": "sk-or-v1-real-key-here" },
    { "fileName": "bgg.secret", "key": "BGG_API_TOKEN", "value": "my-bgg-token" }
  ]
}
```

**Behavior**:
- For each update, reads the `.secret` file, finds the line starting with `KEY=`, replaces the value.
- If the key doesn't exist in the file, appends `KEY=value` at the end.
- Preserves comments and blank lines.
- Returns which files were modified.

**Response** (200):
```json
{
  "updatedFiles": ["openrouter.secret", "bgg.secret"],
  "updatedKeys": 2
}
```

**Validation**:
- `fileName` must match an existing `.secret` file (no path traversal)
- `key` must match `^[A-Z][A-Z0-9_]*$`
- `value` cannot be empty (use DELETE semantics or set to empty string explicitly)

### POST `/api/v1/admin/secrets/restart`

Triggers a graceful API restart.

**Auth**: SuperAdmin session required.

**Behavior**:
1. Logs the restart request with admin user ID
2. Returns 202 Accepted immediately
3. Calls `IHostApplicationLifetime.StopApplication()` after a 2-second delay (to allow the response to be sent)
4. Docker `restart: always` policy restarts the container

**Response** (202):
```json
{
  "message": "API restart initiated. Service will be back in ~10 seconds.",
  "restartedAt": "2026-03-18T15:30:00Z"
}
```

## Frontend

### Page Location

New tab "Secrets" on `/admin/monitor/services` page.

### Layout

```
┌─────────────────────────────────────────────────┐
│ Services │ Secrets                               │  ← tab bar
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌─ OpenRouter ─────────────────────────────┐   │
│  │ OPENROUTER_API_KEY    [sk-or-v****ER] 👁  │   │  ← masked, eye to reveal
│  │ OPENROUTER_DEFAULT_MODEL [meta-llama...] │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌─ BGG ────────────────────────────────────┐   │
│  │ BGG_API_TOKEN         [****] 👁  ⚠ empty │   │  ← warning if placeholder
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌─ ⚠ Database (Infra) ────────────────────┐   │  ← infra badge
│  │ POSTGRES_USER         [meepleai]         │   │
│  │ POSTGRES_PASSWORD     [Meepl****2026] 👁 │   │
│  │ POSTGRES_DB           [meepleai_staging] │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌──────────────────────────────────────────┐   │
│  │  💾 Salva modifiche (2 campi modificati) │   │  ← yellow dirty indicator
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌──────────────────────────────────────────┐   │
│  │  ⚡ Riavvia API per applicare i secret   │   │  ← appears after save
│  └──────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Component Structure

- `AdminSecretsTab.tsx` — main tab component
- `SecretFileCard.tsx` — card per file with entries
- `SecretEntryInput.tsx` — masked input with reveal toggle
- `useAdminSecrets.ts` — React Query hook for GET/PUT
- `RestartApiBanner.tsx` — post-save restart prompt with health polling

### UX Flow

1. Page loads → fetches `GET /admin/secrets` → renders cards
2. Admin edits a value → field turns yellow (dirty)
3. Admin clicks "Salva modifiche" → `PUT /admin/secrets` with only changed values
4. Success toast → banner appears: "Secret aggiornati. Riavvia per applicare."
5. Admin clicks "Riavvia ora" → `POST /admin/secrets/restart`
6. Spinner + "Riavvio in corso..." → polls `/api/v1/health` every 2 seconds
7. When health returns 200 → success toast "API riavviata con successo"

### Error Handling

- Save fails → error toast with message, fields stay dirty
- Restart fails → error toast, manual restart instructions shown
- Health poll timeout (>30s) → warning: "L'API non risponde. Verifica i log del server."

## Files Affected

### New Files (Backend)
- `apps/api/src/Api/Routing/AdminSecretsEndpoints.cs` — 3 endpoints
- `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Commands/UpdateSecretsCommand.cs`
- `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Handlers/UpdateSecretsCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Queries/GetSecretsQuery.cs`
- `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Handlers/GetSecretsQueryHandler.cs`
- `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/DTOs/SecretFileDto.cs`

### New Files (Frontend)
- `apps/web/src/app/(authenticated)/admin/monitor/services/secrets-tab.tsx`
- `apps/web/src/components/admin/secrets/SecretFileCard.tsx`
- `apps/web/src/components/admin/secrets/SecretEntryInput.tsx`
- `apps/web/src/components/admin/secrets/RestartApiBanner.tsx`
- `apps/web/src/lib/api/clients/adminSecretsClient.ts`
- `apps/web/src/hooks/useAdminSecrets.ts`

### Modified Files
- `apps/api/src/Api/Program.cs` — register new endpoints
- `apps/web/src/app/(authenticated)/admin/monitor/services/page.tsx` — add Secrets tab

## Security

- **SuperAdmin only**: All 3 endpoints require `RequireAdminSession()` + role check
- **Path traversal prevention**: `fileName` validated against existing files in secrets directory, no `..` or `/` allowed
- **Audit logging**: All secret updates logged with admin user ID, key names (NOT values), and timestamp
- **No secret values in logs**: Only key names are logged, never values
- **Reveal requires explicit action**: Values masked by default, full values only with `?reveal=true`

## Testing

| Test | Type | Description |
|------|------|-------------|
| GET returns masked values | Unit | Verify masking logic for various value lengths |
| PUT updates file correctly | Unit | Write to temp file, verify content preserved |
| Path traversal blocked | Unit | `../etc/passwd` rejected |
| Key format validated | Unit | Only `^[A-Z][A-Z0-9_]*$` accepted |
| Non-admin rejected | Integration | 403 for non-SuperAdmin users |
| Restart triggers shutdown | Integration | `StopApplication()` called |
| Frontend renders cards | Unit | Vitest: cards show per file with masked values |
| Dirty state tracking | Unit | Vitest: modified fields tracked correctly |
