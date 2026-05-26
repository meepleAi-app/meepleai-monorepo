# Backend Prompts — Admin Console (SP5)

Prompt template pronti per Claude Code. 18 mockup, uno per prompt. **Adatta i path al codebase**.

## Premessa per ogni prompt

```
Vincoli generali admin:
- Tutte le route /admin/* richiedono role admin o superadmin (verifica in middleware)
- Ogni POST/PATCH/DELETE produce un audit_log entry (middleware auditLogger)
- Mobile fallback: <880px mostra "console solo desktop"
- Dark theme default
- Density alta (admin-base.css)
- Pagination cursor-based, NO offset
- Stati: default + loading + empty + error
- Server-side filter+sort per liste >100 row
- Virtualization per >200 row visibili
- data-testid solo dove specificato
```

---

## 1.1 A1 · Admin Overview

**Mock**: `design/admin/sp5-admin-overview.html` → `/admin/overview`

```
Implementa /admin/overview basato su design/admin/sp5-admin-overview.html.

Min role: admin. Audit log per ogni mutazione (qui poche: solo quick actions).

Backend GET /api/admin/overview restituisce:
{
  kpis: {
    usersTotal: number,
    usersDelta24h: number,
    sessionsActive: number,
    sessionsDeltaPercent: number,
    agentsLive: number,
    kbStorageBytes: number,
    kbStorageDeltaBytes7d: number
  },
  alerts: AdminAlert[],   // up to 4, ordered by severity
  activityFeed: ActivityEntry[],  // last 12h, up to 20
  charts: {
    uptime7d: { value: number, slo: number, sparkline: number[] },
    requestsPerMin: { value: number, sparkline: number[] },
    errorRate: { value: number, threshold: number, sparkline: number[] },
    aiCostToday: { value: number, budget: number, sparkline: number[] }
  },
  deployment: { env, lastDeployAt, buildId, commit, by, status }
}

Cache server-side 30s. SWR/RQ frontend 30s.

Quick actions (alcune richiedono superadmin):
- Impersonate user (superadmin + 2FA step-up)
- Reset rate limit (admin)
- Force cache flush (superadmin + confirm)
- Pausa ingestion (admin + confirm)
- Broadcast banner (superadmin + typed-confirm)
- Emergency shutdown (superadmin + typed-confirm "EMERGENCY")

Componenti da creare:
- AdminKPICard (entity-tinted + sparkline)
- AdminAlertBanner
- AdminActivityFeed
- ChartPanel (4 varianti: uptime, requests, errors, cost)
- QuickActionsPanel
- StatusDot + meeple-pulse animation

Keyboard: R refresh, ⌘K search.
```

---

## 1.2 A2 · Admin Users

**Mock**: `design/admin/sp5-admin-users.html` → `/admin/users[?selected=u-X]`

```
Implementa /admin/users basato su design/admin/sp5-admin-users.html.

Min role: admin per lista/sospensione. Superadmin per promote/delete/impersonate.

Backend:
- GET /api/admin/users?q=&role=&status=&lastSeenWithin=&plan=&cursor=&sort=&dir=
  → cursor-based pagination, restituisce { items: User[], cursor, total, facets }
- GET /api/admin/users/{id} → full profile + sessions + 2fa status + audit history
- PATCH /api/admin/users/{id} { role?, suspended? } (audit + step-up se role→admin/superadmin)
- POST /api/admin/users/{id}/force-logout (admin + confirm)
- POST /api/admin/users/{id}/reset-password (admin)
- POST /api/admin/users/{id}/suspend|unsuspend (admin)
- DELETE /api/admin/users/{id} (superadmin + step-up + typed-confirm "ELIMINA UTENTE {NAME}")
- POST /api/admin/users/{id}/impersonate-start → JWT ttl=900s (superadmin + step-up)
- POST /api/admin/impersonate-end
- POST /api/admin/users/{id}/export-gdpr → genera ZIP async + job

Frontend:
- src/features/admin/users/UsersPage.tsx
- AdminDataTable con virtualization
- Filter chips (role/status/lastSeen/plan)
- BulkActionsBar quando >0 selezionati
- Multi-select con shift-click range
- Right side drill-down panel sticky con:
  - Hero (avatar grad + nome + role chip + status)
  - Tabs: Profilo · Sessioni (n) · Agenti (n) · Audit (n) · Billing
  - DangerZone (force logout, delete account)

ImpersonateFlow:
- Step-up modal 2FA TOTP
- Banner persistente fixed top "TU SEI {USER} · termina »"
- Tutte le altre pagine accessibili tranne /admin/*
- Auto-end dopo 15 min con countdown banner
- audit_log su ogni request durante impersonate

Keyboard: j/k navigate rows · Enter open detail · Escape close drawer.
```

---

## 1.3 A3 · Content Moderation

**Mock**: `design/admin/sp5-admin-content.html` → `/admin/content`

```
Implementa /admin/content basato su design/admin/sp5-admin-content.html.

Min role: admin.

Tabs:
1. Games queue (giochi community submitted, in attesa)
2. Toolkit condivisi
3. KB docs (proposed da utenti per gioco condiviso)
4. Comments flagged (segnalati da N utenti)

Backend:
- GET /api/admin/moderation/queue?type=&status=in_review&cursor=
- GET /api/admin/moderation/{id}/preview → full content per review
- POST /api/admin/moderation/{id}/approve (audit)
- POST /api/admin/moderation/{id}/reject { reason }  (audit, notifica autore)
- POST /api/admin/moderation/{id}/request-changes { notes }
- POST /api/admin/moderation/comments/{id}/delete
- POST /api/admin/moderation/users/{id}/mute?duration_days=
- POST /api/admin/moderation/users/{id}/ban (superadmin + typed-confirm)

UI:
- ModerationQueue (rows con preview button)
- ContentStatusChip
- CommentCard con flag-count badge + delete/mute/ban actions
- Spoiler-block edit modal (per spoiler-tag wrapping)

Notifiche outbound:
- Approve game → notif a designer "Il tuo gioco è online!"
- Reject → notif con reason
- Comment delete → notif silenziosa solo se autore richiede transparency settings
```

---

## 1.4 A4 · AI / RAG Quality

**Mock**: `design/admin/sp5-admin-ai.html` → `/admin/ai` + `/admin/rag-quality`

```
Implementa /admin/ai basato su design/admin/sp5-admin-ai.html.

Min role: admin.

Backend:
- GET /api/admin/ai/metrics?range=1h|24h|7d|30d → 5 KPI
- GET /api/admin/ai/metrics/timeseries?range=&kind=p50|p95|errorRate
- GET /api/admin/ai/queries?sort=worst&limit=10
  → ordered by composite: low_rating + high_latency + low_confidence
- GET /api/admin/ai/queries/{id}/drill
  → { query, response, confidence, retrievedChunks[], latencyBreakdown, tokens, cost, debug }
- POST /api/admin/ai/queries/{id}/rerun { kbVersion?: 'current' | 'beta' }
- POST /api/admin/ai/queries/{id}/flag-hallucination
- POST /api/admin/ai/queries/{id}/notify-user
- GET /api/admin/ai/agents/usage?range=

Frontend:
- 5 KPI sparklines (Query/day, Avg latency, P95, Cost/day, Hallucination rate)
- TrendChart (multi-line p50/p95/error% over 7d)
- WorstQueriesTable
- QueryDrillDown sidebar:
  - Header con confidence-badge entity-event/agent/toolkit + meta
  - RetrievalChunkList con highlight mark e score colorato
  - Response card + feedback utente
  - LatencyBreakdownBar (4 segmenti: retrieval, rerank, llm, postprocess)
  - Debug expandable (tokens, cache, model, embedder, indexer version)
  - Actions row (rerun current/beta, flag hallucination, copy query, notify author)

Componenti nuovi: QueryDrillDown, RetrievalChunkList, LatencyBreakdownBar, ConfidenceBadge (riuso da sp6/sp7).
```

---

## 1.5 A5 · Knowledge Base

**Mock**: `design/admin/sp5-admin-kb.html` → `/admin/knowledge-base`

```
Implementa /admin/knowledge-base basato su design/admin/sp5-admin-kb.html.

Min role: admin per read/upload/reindex. Admin + confirm per delete chunk.
Superadmin per reindex con strategy=raptor (costoso).

Backend:
- GET /api/admin/kb/tree → { games: [{ id, title, em, docs: [{ id, title, chunks, status }] }] }
- GET /api/admin/kb/docs/{id} → { doc, ingestionLog, metadata, usedByAgents }
- GET /api/admin/kb/docs/{id}/chunks?q=&topK=&minScore=&chapter=&cursor=
  → ricerca semantica con highlight
- GET /api/admin/kb/docs/{id}/chunks/{chunkId} → full chunk text + embedding (debug)
- POST /api/admin/kb/docs/{id}/reindex { strategy: 'standard' | 'raptor' }
- DELETE /api/admin/kb/docs/{id} (admin + confirm)
- DELETE /api/admin/kb/chunks/{id} (superadmin + confirm)
- GET /api/admin/kb/docs/{id}/embeddings.json (debug export)

UI:
- KBTree (left sidebar 300px, accordion per gioco, sticky)
- DocumentHero (cover game-color + title + meta + 5 stats KPI)
- AdminTabs: Preview · Chunks (n) · IngestionLog · UsedBy (n)
- ChunkTable con search inline, score colored (hi=kb, md=warn, lo=event)
- IngestionLog pre-formatted con colorate (ok=toolkit, warn=yellow, err=event)

NB: chunk preview deve avere `<mark>` highlight per query match (server o client).
```

---

## 1.6 A5b · KB Upload Flow

**Mock**: `design/admin/sp5-kb-upload-flow.html` → `/admin/knowledge-base/upload`

```
Implementa /admin/knowledge-base/upload basato su design/admin/sp5-kb-upload-flow.html.

FSM 5 stati: empty → idempotency-check → uploading → processing → complete|error.

Backend:
- POST /api/admin/kb/idempotency-check { sha256 } → { match: KbDoc | null, similarity }
- POST /api/admin/kb/upload (multipart) { file, gameId, language?, ocr?, smolDocling?, chunkSize?, indexerVersion? }
  → 202 Accepted { jobId }
- GET /api/jobs/{id} → { stage: 'upload'|'parse'|'layout'|'embed'|'index', progress, eta, log[], error? }
- SSE /api/jobs/{id}/events → live progress updates
- POST /api/jobs/{id}/cancel
- POST /api/jobs/{id}/retry { skipStage?: number }

UI FSM:
- StateEmpty: dropzone tabindex+keyboard accessible, settings card readonly
- StateUploading: dropzone con progress + cancel
- StateProcessing: dropzone shrink + queue card con 5-stage timeline (each con dot pulsing for running)
- StateComplete: success banner + queue history + first 3 chunks preview
- StateError: danger banner con error code + log download + retry option
- GuardWarn (idempotency-warn): banner warning con CTA (Vai a KB / Sovrascrivi / Aggiungi come nuovo)

UploadDropzone:
- Accessible: keyboard space/enter, drag-over visual feedback
- Multi-file (queue ognuno come job separato)
- Cap size 100MB · MIME check

ProcessingTimeline:
- 5 stage cards in grid 5col
- Each: name + state + sub-state animated bar for running

Componente nuovo: UploadFSMProvider context per condividere state tra dropzone/settings/queue.
```

---

## 1.7 A6 · Catalog Ingestion

**Mock**: `design/admin/sp5-admin-catalog.html` → `/admin/catalog-ingestion`

```
Implementa /admin/catalog-ingestion basato su design/admin/sp5-admin-catalog.html.

Min role: admin.

Backend:
- GET /api/admin/catalog/status → SyncStatus hero
- GET /api/admin/catalog/runs?cursor= → history runs
- GET /api/admin/catalog/runs/{id} → detail con log
- POST /api/admin/catalog/run-now { provider: 'bgg'|'csv'|'manual' } (admin)
- GET /api/admin/catalog/queue?status=pending|failed → items
- POST /api/admin/catalog/queue/{id}/retry
- POST /api/admin/catalog/queue/retry-bulk (failed items)
- POST /api/admin/catalog/import-csv (multipart)

UI:
- SyncStatusHero con last-run + next-scheduled + provider settings inline
- SyncRunTimeline (table runs con status dot + delta added/updated/failed)
- PendingQueueList (priority high/stale)
- FailedItemsList (con error code mono + retry button inline)

Componenti nuovi: SyncStatusHero, SyncRunTimeline.

Rate limit consciousness: mostra "stiamo a 47/60 req/min" se BGG.
```

---

## 1.8 A7 · Config / Feature Flags

**Mock**: `design/admin/sp5-admin-config.html` → `/admin/config`

```
Implementa /admin/config basato su design/admin/sp5-admin-config.html.

Min role: admin per read e cambio in dev/stg. Superadmin per cambio in prd + audit + step-up.

Backend:
- GET /api/admin/flags?env=dev|stg|prd
  → { flags: [{ key, value, type, description, lastChangedAt, lastChangedBy, scopes: ['dev','stg','prd'] }] }
- PATCH /api/admin/flags/batch { env, changes: { [key]: value } } → dry-run preview
- POST /api/admin/flags/apply { env, changes } → commit (audit + step-up if env=prd)
- GET /api/admin/flags/history/{key}?limit=

UI:
- Tabs per categoria (General, Features, AI, Integrations, Security)
- FlagRow grid: name+desc | envs available | toggle | last-changed meta
- Dirty state tracking client-side
- DirtyStateBar sticky-bottom con:
  - count "N modifiche non salvate"
  - Revert, Preview diff (modal con before/after), Apply

Apply flow:
1. Validation BE (type-check, deps)
2. If env=prd → step-up modal 2FA
3. Audit log + notification a Slack #ops
4. Optimistic update + rollback su error
```

---

## 1.9 A8 · Monitor

**Mock**: `design/admin/sp5-admin-monitor.html` → `/admin/monitor`

```
Implementa /admin/monitor basato su design/admin/sp5-admin-monitor.html.

Min role: admin.

Backend:
- GET /api/admin/metrics/strip?range= → 6 KPI sparklines
- GET /api/admin/metrics/requests-by-endpoint?range= → stacked area
- GET /api/admin/metrics/errors-by-route?range= → bar chart
- GET /api/admin/metrics/signups-sessions?range=
- GET /api/admin/metrics/session-duration-histogram?range=
- SSE /api/admin/events/live?level=&route= → streaming events

UI:
- TimeRangePicker (1h/24h/7gg/30gg/custom)
- KpiStrip 6 cards
- 4 BigChart (con header + sub + svg)
- LiveEventLog (sticky-bottom panel, 80-col mono, virtualized)

LiveEventLog component:
- Subscribe SSE on mount, unsubscribe on unmount
- Auto-reconnect with exponential backoff
- Filter bar: level (debug|info|warn|err|ok), route (regex)
- Pause/Resume button (when paused, buffer events, show count)
- Click row → expand JSON payload modal
- Virtualize per 200+ rows visible

Componenti nuovi: TimeRangePicker, KPISparkline, LiveEventLog.
```

---

## 1.10 A9 · Notifications Templates

**Mock**: `design/admin/sp5-admin-notifications.html` → `/admin/notifications`

```
Implementa /admin/notifications basato su design/admin/sp5-admin-notifications.html.

Min role: admin. Broadcast = superadmin + typed-confirm.

Backend:
- GET /api/admin/notif/templates → all
- GET /api/admin/notif/templates/{id} → with versions[]
- PATCH /api/admin/notif/templates/{id} { subject, body, variables, channels[] }
- POST /api/admin/notif/templates/{id}/preview { variables } → rendered HTML
- POST /api/admin/notif/templates/{id}/test-send { to, variables }
- POST /api/admin/notif/templates/{id}/publish (admin + audit)
- POST /api/admin/notif/broadcast (superadmin + typed-confirm)
  Body: { audience: 'all'|'premium'|'admins'|filter, channels: [], subject, body }

UI:
- Left list templates (filter by channel)
- Right editor area:
  - Tabs Edit · Preview · TestSend · History
  - Markdown editor with Handlebars vars (highlight {{var}})
  - Variables helper panel (collapsible)
  - PreviewFrame iframe-mockup 380px width (email/mobile/desktop toggle)
  - Test-send form (recipient, sample variables)
  - HistoryTab versions timeline

Components nuovi: TemplateEditor, TemplatePreviewFrame, VariableHelper.

Variables helper: parse template body, extract {{x.y}}, show available with example values from current context.
```

---

## 2.1 B1 · Editor

**Mock**: `design/admin/sp5-editor.html` → `/editor/[type]/[id]`

```
Implementa /editor basato su design/admin/sp5-editor.html.

Min role: admin per editare draft. Superadmin per publish.

Backend:
- GET /api/admin/editor/{type}/{id} → { blocks: Block[], metadata, version, status }
- POST /api/admin/editor/{type}/{id}/blocks { block } (insert)
- PATCH /api/admin/editor/{type}/{id}/blocks/{blockId} { block }
- DELETE /api/admin/editor/{type}/{id}/blocks/{blockId}
- POST /api/admin/editor/{type}/{id}/reorder { blockIds: [] }
- POST /api/admin/editor/{type}/{id}/commit { message, version? } → new version
- POST /api/admin/editor/{type}/{id}/publish (superadmin)
- POST /api/admin/editor/{type}/{id}/autosave (debounced 30s)

UI:
- 3-column layout: tree 240px | editor 1fr | metadata+preview 320px
- TreeNav (giochi → rules/faq/scenarios/glossary)
- BlockEditor:
  - Hover row reveals handle (drag, +, delete)
  - Toolbar sticky (B I U S, H2/H3/code, list/table/quote, undo/redo)
  - Slash commands "/" for inserting blocks
  - Keyboard ⌘B bold ⌘K link ⌘/ commands
- Live PreviewFrame (renders blocks → final HTML/PDF preview)
- Metadata panel (slug, lang, tags, linked entities, status)
- VersionHistoryPanel (last 5 + "See all")

Auto-save UX: "⚪ Auto-save in 12s" counter → "✓ Salvato 14:22" toast.
Commit dialog: "Apply changes" forces version bump + commit message.

Components nuovi: BlockEditor, PreviewFrame, VersionHistoryPanel, SlashCommandMenu.
```

---

## 2.2 B2 · Pipeline Builder

**Mock**: `design/admin/sp5-pipeline-builder.html` → `/pipeline-builder/[id]`

```
Implementa /pipeline-builder basato su design/admin/sp5-pipeline-builder.html.

Min role: admin per draft/test. Superadmin per publish in prod.

Backend:
- GET /api/admin/pipelines → list
- GET /api/admin/pipelines/{id} → { nodes, edges, metadata, status, lastRunStats }
- PATCH /api/admin/pipelines/{id} { nodes, edges }
- POST /api/admin/pipelines/{id}/test-run { query } → { perNodeOutput, totalLatency, totalCost }
- POST /api/admin/pipelines/{id}/publish (superadmin)
- GET /api/admin/pipelines/{id}/runs?limit= → recent

UI:
- 3-col: palette 220px | canvas 1fr | config 300px
- Palette categorie (Retrieval, Processing, Output, Integrations)
- Drag nodes da palette → canvas
- Canvas con grid background + SVG edges (Bezier)
- Node component:
  - Header type-colored
  - Name + props summary
  - In/out dots (left/right)
  - Hover → handle + connect
  - Stats footer (lat/cost/hit-rate) se ran recently
- ConfigPanel context-aware al node selected
- TestRun bottom panel con json output + warnings
- Error nodes (border red) con tooltip "property X missing"

Librerie suggerite: React Flow, dagre per layout auto.

Components nuovi: FlowCanvas (wrapper React Flow), NodePalette, NodeConfigPanel.
```

---

## 2.3 B3 · n8n Integrations

**Mock**: `design/admin/sp5-n8n.html` → `/n8n`

```
Implementa /n8n basato su design/admin/sp5-n8n.html.

Min role: admin per read. Superadmin per gestione keys + disconnect.

Backend:
- GET /api/admin/n8n/connection → status + endpoint
- POST /api/admin/n8n/test-connection
- POST /api/admin/n8n/disconnect (superadmin + confirm)
- GET /api/admin/n8n/workflows → list with stats 24h
- GET /api/admin/n8n/workflows/{id}/runs?cursor=
- GET /api/admin/n8n/webhooks/log?cursor=&level=&status=
- GET /api/admin/n8n/keys → list (only key prefix, never full)
- POST /api/admin/n8n/keys { name, scope, expiresInDays } (superadmin)
  → returns full key ONCE in response, never again
- DELETE /api/admin/n8n/keys/{id} (superadmin + confirm)
- POST /api/admin/n8n/keys/{id}/rotate

UI:
- ConnectionHero con endpoint + actions (test/rotate/disconnect)
- Tabs: Workflows (n) · Webhooks (n) · Logs · API keys (n)
- Workflows table con rate-bar (last 7 runs visualized)
- Webhook log compact stream
- API keys list con expiry warning <30d

Security: quando generi nuova key, mostra UNA volta in modal "Salva questa key, non verrà mostrata di nuovo. [Copia]". Audit log con key prefix.
```

---

## 2.4 B4 · Upload avanzato

**Mock**: `design/admin/sp5-upload-advanced.html` → `/upload`

```
Implementa /upload basato su design/admin/sp5-upload-advanced.html.

Backend:
- POST /api/admin/upload/bulk (multipart, files[])
- GET /api/admin/upload/queue → active jobs
- GET /api/admin/upload/history?cursor=
- SSE /api/jobs/{id}/events

UI:
- Left: dropzone + queue list + history list
- Right: detailed processing view of selected job
  - 4 KPI top (current stage, progress %, ETA, cost stim)
  - Pipeline timeline 5 step (riuso A5b component)
  - Metadata grid (lingua, ocr, chunk size, indexer)
  - Actions grid (download orig, view extracted, retry stage, delete KB)

Riuso pesante di ProcessingTimeline da A5b.
Differenza vs A5b: qui è multi-file con queue prioritization.

Components nuovi: BulkUploadQueue.
```

---

## 2.5 B5 · Play Records

**Mock**: `design/admin/sp5-play-records.html` → `/play-records`

```
Implementa /play-records basato su design/admin/sp5-play-records.html.

NOTA: questo è SCOPE personale (user role), non admin-only.

Backend:
- GET /api/me/play-records?game=&from=&to=&player=&winner=&duration=&location=&sort=&cursor=
- POST /api/me/play-records { gameId, playedAt, players[], duration, winner, scores[], notes, photos[] }
- PATCH /api/me/play-records/{id}
- DELETE /api/me/play-records/{id}
- GET /api/me/play-records/stats?range= → wins per game, top opponents, KPI

UI Tabs:
1. List (sortable table con date · game · players · duration · winner · notes)
2. Calendar (mensile, click giorno → modal con partite)
3. Analytics (4 KPI + wins per gioco bar + top opponents card)

Filter chips (multi-select) sticky-top.
Quick analytics row 4 KPI.
List paginated cursor + lazy load.

NB: usa locale data formatting (Europe/Rome).
```

---

## 2.6 B6 · Versions

**Mock**: `design/admin/sp5-versions.html` → `/versions/[artifact]`

```
Implementa /versions basato su design/admin/sp5-versions.html.

Min role: admin per read/restore. Superadmin per publish.

Backend:
- GET /api/admin/versions/artifacts → list artifacts (rules, toolkits, prompts, glossaries)
- GET /api/admin/versions/{artifact} → versions timeline
- GET /api/admin/versions/{artifact}/diff?from=&to= → unified diff (left+right)
- POST /api/admin/versions/{artifact}/{vid}/publish (superadmin)
- POST /api/admin/versions/{artifact}/{vid}/restore (admin)
- GET /api/admin/versions/{artifact}/{vid}/changelog

UI:
- Left list artifacts (search + filter type)
- Right:
  - VersionTimeline (rows: tag · commit msg · author · status · diff size · checkbox compare)
  - Compare selector (header: v2.0.0 ↔ v2.1.0 + View diff)
  - DiffViewer side-by-side (left=v1 red del, right=v2 green add)

Components nuovi: VersionTimeline, VersionDiffViewer, CompareSelector.

Diff lib: usa diff-match-patch o diff2html.
```

---

## 2.7 B7 · Private Games

**Mock**: `design/admin/sp5-private-games.html` → `/private-games`

```
Implementa /private-games basato su design/admin/sp5-private-games.html.

SCOPE: user-level (proprio account).

Backend:
- GET /api/me/private-games → list
- POST /api/me/private-games { title, cover, players, duration, weight }
- GET /api/me/private-games/{id} → full detail with all chapters/rules
- PATCH /api/me/private-games/{id}
- GET /api/me/private-games/{id}/publish-checklist → { items: [{ id, label, done, notes }] }
- POST /api/me/private-games/{id}/submit-for-review → moves to admin moderation queue

Schema DB: games table has is_private bool + owner_user_id. NEVER exposed by /api/public/*.

UI:
- Hero card: highlight gioco "ready for review" with checklist 7/9
- Grid card layout
- PrivateGameCard:
  - Cover gradient + emoji + 🔒 lock badge
  - Name + meta (players · duration · weight)
  - Status chip (draft|in playtest|ready|published)
  - Stats (version · partite test · last update)
- New card (dashed) per creare nuovo

Components nuovi: PublishChecklist, PrivateGameCard.

PublishChecklist:
- Items con criteri auto-computed (es. "almeno 5 partite test" → controlla play_records collegate)
- Submit disabled finché completamento < 100%
- Show completion ring + percent in side-card
```

---

## 2.8 B8 · Dev Tools

**Mock**: `design/admin/sp5-dev-tools.html` → `/dev`

```
Implementa /dev basato su design/admin/sp5-dev-tools.html.

⚠ DEV-ONLY: wrap entire route in NODE_ENV check.
Tree-shake con entry-point separato dev-entry.tsx.

NO backend changes — tutto client-side (MSW handlers, Zustand stores, RQ cache, localStorage).

Sections:
1. Scenarios (MSW) — switch handler bundle:
   - empty | small-library-marco | power-user-aaron | alpha-features-on | degraded-api | offline-mode
   - Click "Load" → swap MSW worker handlers, page reload
2. Auth role switcher — pills user/premium/admin/superadmin → set mock JWT in localStorage
3. Runtime feature flags — localStorage flags (alphaMode, devTools, etc.) + toggles
4. Memory store inspector — Zustand stores JSON tree
5. React Query cache inspector — query keys with fresh/stale status + invalidate buttons
6. Danger reset — localStorage/sessionStorage/IndexedDB/cookies/zustand/rq cache/nuke-all

Components nuovi: ScenarioPicker, AuthRoleSwitcher, FlagRuntimeToggle, StoreInspector, CacheInspector, ResetPanel.

Importa @tanstack/react-query-devtools come optional dep solo in dev.
Storybook link in foot.

⚠ Verifica con build production che TUTTO il /dev codebase sia tree-shaken (bundle analyzer).
```

---

## Sequenza consigliata

Vedi `SCREENS.md` per lo Sprint plan a 8 settimane. Pilot suggerito: **A1 Overview** poi **A2 Users**.

Per ogni pagina, segui sempre:
1. Backend audit prima (esistono già endpoint?)
2. Schema migration se serve
3. Componenti riusabili first
4. Page composition
5. Permission gates
6. Audit log middleware verifica
7. Tests (unit + integration + E2E critici)
8. PR review da 2 reviewer (admin = alto rischio)
