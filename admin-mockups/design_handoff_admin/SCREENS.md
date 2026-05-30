# SCREENS — Inventario completo Admin Console

18 schermate. Per ognuna: mock file, route attesa, ruolo minimo, endpoint backend, priorità.

**Priorità**:
P0 = critical / quotidiano
P1 = importante
P2 = nice to have
P3 = solo per dev / superadmin esoterico

---

## 🛡 Admin Console (10 schermate)

| # | Mock | Route | Min role | Endpoint principale | P |
|---|---|---|---|---|---|
| A1 | `sp5-admin-overview` | `/admin/overview` | admin | `GET /api/admin/overview` | P0 |
| A2 | `sp5-admin-users` | `/admin/users` + `/admin/users/[id]` | admin | `GET /api/admin/users` · `GET /api/admin/users/{id}` | P0 |
| A3 | `sp5-admin-content` | `/admin/content` | admin | `GET /api/admin/moderation/queue` · `POST .../{id}/approve|reject` | P1 |
| A4 | `sp5-admin-ai` | `/admin/ai` + `/admin/rag-quality` | admin | `GET /api/admin/ai/metrics` · `GET .../queries/{id}/drill` | P0 |
| A5 | `sp5-admin-kb` | `/admin/knowledge-base` | admin | `GET /api/admin/kb/tree` · `GET .../docs/{id}` | P0 |
| A5b | `sp5-kb-upload-flow` | `/admin/knowledge-base/upload` | admin | `POST /api/admin/kb/upload` (SSE progress) | P0 |
| A6 | `sp5-admin-catalog` | `/admin/catalog-ingestion` | admin | `GET /api/admin/catalog/runs` · `POST .../run-now` | P1 |
| A7 | `sp5-admin-config` | `/admin/config` | admin (read) / superadmin (write prd) | `GET /api/admin/flags` · `PATCH .../{key}` | P1 |
| A8 | `sp5-admin-monitor` | `/admin/monitor` | admin | `GET /api/admin/metrics?range=` · `SSE .../events/live` | P1 |
| A9 | `sp5-admin-notifications` | `/admin/notifications` + `/admin/notifications/templates/[id]` | admin | `GET /api/admin/notif/templates` · `POST .../test-send` | P2 |

---

## 🛠 Power-User Tools (8 schermate)

| # | Mock | Route | Min role | Endpoint principale | P |
|---|---|---|---|---|---|
| B1 | `sp5-editor` | `/editor/[type]/[id]` | admin | `GET /api/admin/editor/{type}/{id}` · `PATCH/POST commit` | P1 |
| B2 | `sp5-pipeline-builder` | `/pipeline-builder/[id]` | admin · publish=superadmin | `GET /api/admin/pipelines/{id}` · `POST .../test-run` · `POST .../publish` | P2 |
| B3 | `sp5-n8n` | `/n8n` | admin (read) · keys=superadmin | `GET /api/admin/n8n/workflows` · `POST .../keys` (rotate) | P2 |
| B4 | `sp5-upload-advanced` | `/upload` | admin | `POST /api/admin/upload/bulk` (multipart + SSE) | P1 |
| B5 | `sp5-play-records` | `/play-records` | user (proprio) / admin (cross-user) | `GET /api/play-records?user=` | P2 |
| B6 | `sp5-versions` | `/versions/[artifact]` | admin · publish=superadmin | `GET /api/admin/versions/{artifact}` · `POST .../publish` | P1 |
| B7 | `sp5-private-games` | `/private-games` | user (proprio) | `GET /api/me/private-games` · `POST publish-review` | P2 |
| B8 | `sp5-dev-tools` | `/dev` | superadmin + env=dev | (mock) | P3 dev-only |

---

## 🧩 Surplus — Platform & Operations + AI Tooling (13 schermate)

Mockup per le funzioni admin reali prive di mockup SP5 (vedi `SURPLUS_DESIGN_PROMPTS.md` + `ADMIN_AUDIT.md`). Generati su claude.ai, verificati, integrati in `admin-nav.js` (gruppi "Platform & Operations" e "AI Tooling & Data Quality").

### 🛡 Platform & Operations (C)

| # | Mock | Route | Min role | Endpoint principale | P |
|---|---|---|---|---|---|
| C1 | `sp5-admin-infra` | `/admin/monitor/containers` | admin | `GET /api/v1/admin/docker` · `/admin/operations/batch-jobs` (SSE) | P1 |
| C2 | `sp5-admin-database-sync` | `/admin/database-sync` · `/admin/staging-access` | superadmin | `/api/v1/admin/database-sync` · `/admin/staging-allowlist` | P1 |
| C3 | `sp5-admin-providers` | `/admin/providers` | admin (rotate=superadmin) | `/api/v1/admin/providers` · `/admin/circuit-breakers` | P0 |
| C4 | `sp5-admin-emergency` | `/admin/llm/emergency` | superadmin + step-up | `POST /api/v1/admin/llm/emergency/*` | P1 |
| C5 | `sp5-admin-budget` | `/admin/business` | admin | `/api/v1/admin/business` · `/admin/budget` · `/admin/cost-calculator` | P1 |
| C6 | `sp5-admin-secrets` | `/admin/secrets` | superadmin + step-up | `/api/v1/admin/secrets` | P1 |
| C7 | `sp5-admin-alerts` | `/admin/monitor` (tab alerts) | admin | `/api/v1/admin/alert-rules` | P1 |

### 🛠 AI Tooling & Data Quality (D)

| # | Mock | Route | Min role | Endpoint principale | P |
|---|---|---|---|---|---|
| D1 | `sp5-admin-mechanic-extractor` | `/admin/knowledge-base/mechanic-extractor` | admin | `/api/v1/admin/mechanic-analyses` · `.../mechanic-extractor/run` | P1 |
| D2 | `sp5-admin-sandbox` | `/admin/agents/sandbox` · `/admin/agents/debug-chat` | admin | `POST /api/v1/admin/agents/debug-chat/stream` (SSE) | P2 |
| D3 | `sp5-admin-ab-testing` | `/admin/agents/ab-testing` | admin | `/api/v1/admin/ab-tests` | P2 |
| D4 | `sp5-admin-prompts` | `/admin/agents/...prompts` | admin | `/api/v1/admin/prompts` | P2 |
| D5 | `sp5-admin-rag-backup` | `/admin/...rag-backup` · seeding | admin (seed=superadmin) | `/api/v1/admin/rag-backup` · `/admin/seeding` (SSE) | P2 |
| D6 | `sp5-admin-integrations` | `/admin/slack` · `/admin/content/email-templates` | admin | `/api/v1/admin/slack` · `/admin/email-templates` | P2 |

---

## Endpoint admin completi (per QUICK_START audit)

### Overview & monitor
- `GET /api/admin/overview` → aggregate
- `GET /api/admin/metrics?range=1h|24h|7d|30d&kind=`
- `SSE /api/admin/events/live` → live event stream
- `GET /api/admin/health` → SLO/uptime

### Users
- `GET /api/admin/users?q=&role=&status=&cursor=`
- `GET /api/admin/users/{id}`
- `PATCH /api/admin/users/{id}` (role/status)
- `POST /api/admin/users/{id}/suspend` · `POST .../unsuspend`
- `POST /api/admin/users/{id}/force-logout`
- `POST /api/admin/users/{id}/reset-password`
- `POST /api/admin/users/{id}/impersonate-start` → JWT short-lived
- `POST /api/admin/impersonate-end`
- `DELETE /api/admin/users/{id}` (superadmin + typed-confirm)
- `GET /api/admin/users/{id}/sessions`
- `GET /api/admin/users/{id}/audit-log`

### Content moderation
- `GET /api/admin/moderation/queue?type=game|toolkit|kb|comment`
- `POST /api/admin/moderation/{id}/approve`
- `POST /api/admin/moderation/{id}/reject` (with reason)
- `POST /api/admin/moderation/comments/{id}/delete`
- `POST /api/admin/moderation/users/{id}/mute?duration=`

### AI / RAG
- `GET /api/admin/ai/metrics?range=`
- `GET /api/admin/ai/queries?sort=worst&limit=`
- `GET /api/admin/ai/queries/{id}/drill` → retrieval + response + latency
- `POST /api/admin/ai/queries/{id}/rerun` (with optional KB version)
- `POST /api/admin/ai/queries/{id}/flag-hallucination`
- `GET /api/admin/ai/agents/usage?range=`

### KB
- `GET /api/admin/kb/tree` → tree per gioco con count
- `GET /api/admin/kb/docs/{id}`
- `GET /api/admin/kb/docs/{id}/chunks?q=&topK=`
- `POST /api/admin/kb/upload` (multipart, async → SSE job)
- `POST /api/admin/kb/docs/{id}/reindex`
- `DELETE /api/admin/kb/docs/{id}`
- `GET /api/admin/kb/idempotency-check?hash=` → potential duplicate
- `GET /api/jobs/{id}` (status async)
- `SSE /api/jobs/{id}/events`

### Catalog
- `GET /api/admin/catalog/runs?cursor=`
- `POST /api/admin/catalog/run-now`
- `POST /api/admin/catalog/queue/{id}/retry`
- `GET /api/admin/catalog/queue?status=`

### Config / Flags
- `GET /api/admin/flags?env=`
- `PATCH /api/admin/flags/{key}` { env, value } → dirty
- `POST /api/admin/flags/apply` → commit batched changes (audit)

### Notifications
- `GET /api/admin/notif/templates`
- `GET /api/admin/notif/templates/{id}` (with versions)
- `PATCH /api/admin/notif/templates/{id}`
- `POST /api/admin/notif/templates/{id}/test-send?to=`
- `POST /api/admin/notif/broadcast` (superadmin) { audience, body, channels }

### Editor / Versions
- `GET /api/admin/editor/{type}/{id}` → blocks
- `POST /api/admin/editor/{type}/{id}/commit` → new version
- `GET /api/admin/versions/{artifact}?limit=`
- `GET /api/admin/versions/{artifact}/diff?from=&to=`
- `POST /api/admin/versions/{artifact}/{vid}/publish`
- `POST /api/admin/versions/{artifact}/{vid}/restore`

### Pipeline & n8n
- `GET /api/admin/pipelines`
- `GET /api/admin/pipelines/{id}`
- `POST /api/admin/pipelines/{id}/test-run`
- `POST /api/admin/pipelines/{id}/publish` (superadmin)
- `GET /api/admin/n8n/workflows`
- `GET /api/admin/n8n/webhooks/log?cursor=`
- `GET /api/admin/n8n/keys` · `POST .../keys` · `DELETE .../keys/{id}`

### Upload avanzato
- `POST /api/admin/upload/bulk` (multipart)
- `GET /api/admin/upload/queue`
- `GET /api/admin/upload/history?cursor=`

### Play records (personal)
- `GET /api/me/play-records?game=&from=&to=&cursor=`
- `POST /api/me/play-records` { gameId, players, duration, winner, notes }
- `PATCH /api/me/play-records/{id}` · `DELETE`
- `GET /api/me/play-records/stats?range=`

### Private games (personal)
- `GET /api/me/private-games`
- `POST /api/me/private-games`
- `GET /api/me/private-games/{id}`
- `PATCH /api/me/private-games/{id}` (any field)
- `GET /api/me/private-games/{id}/publish-checklist`
- `POST /api/me/private-games/{id}/submit-for-review`

---

## Sprint plan consigliato

### Sprint 1 — Foundation (settimana 1)
- RBAC + audit_log middleware
- AdminShell, AdminSidebar, AdminTopbar
- AdminDataTable + BulkActionsBar + AdminKPICard
- A1 Overview (pilot)

### Sprint 2 — Users + Content (settimana 2)
- A2 Users (lista + drill + impersonate flow)
- Step-up 2FA modal
- ConfirmModal con typed-confirm
- A3 Content moderation

### Sprint 3 — KB management (settimana 3)
- A5 KB tree + doc viewer
- A5b Upload flow (FSM 5 stati)
- B4 Upload avanzato (queue + history)

### Sprint 4 — AI quality (settimana 4)
- A4 AI/RAG metrics + drill-down
- B6 Versions (timeline + diff)

### Sprint 5 — Operations (settimana 5)
- A6 Catalog ingestion
- A7 Config / Flags
- A8 Monitor + LiveEventLog SSE — **tracciato F4.1 #1718** (mockup `admin/sp5-admin-monitor.html`)

### Sprint 6 — Content & Tools (settimana 6)
- A9 Notifications + TemplateEditor
- B1 Editor (block-based)

### Sprint 7 — Advanced (settimana 7)
- B2 Pipeline builder (canvas)
- B3 n8n integrations
- B7 Private games
- B5 Play records

### Sprint 8 — Polish & Dev (settimana 8)
- B8 Dev tools (env=dev only)
- Tests E2E flow critici (impersonate, KB upload, broadcast)
- Performance: virtualization tabelle, lazy loading drill-down
