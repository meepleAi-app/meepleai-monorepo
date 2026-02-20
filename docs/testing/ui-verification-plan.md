# UI Verification Plan — Week Feb 12-19, 2026

## Prerequisites

```bash
# Terminal 1: Infrastructure
cd infra && docker compose up -d postgres qdrant redis

# Terminal 2: API (.NET)
cd apps/api/src/Api && dotnet run   # → :8080

# Terminal 3: Frontend (Next.js)
cd apps/web && pnpm dev             # → :3000
```

**Browser**: Chrome DevTools open → Network tab (filter: `Fetch/XHR` + `EventSource`)
**Console**: Filter by `[API` to catch httpClient logs
**Login**: Admin account for `/admin/*` routes

**Correlation ID**: Every request carries `X-Correlation-ID` header (auto-generated UUID in sessionStorage). Server logs include this for cross-referencing.

---

## Test 1: Admin Shell — Navigation & Layout

### Pages
- `/admin/overview`

### Flow
1. Navigate to `/admin/overview`
2. Verify TopNav has 5 tabs: Overview | Users | Shared Games | **|** Agents | Knowledge Base
3. Click each tab → sidebar items change contextually
4. Click sidebar collapse icon → sidebar collapses, refresh page → stays collapsed (localStorage `admin-dashboard-sidebar-collapsed`)
5. Resize to <768px → hamburger menu, sidebar becomes Sheet drawer
6. Click Moon/Sun icon → dark/light toggle
7. Click user menu → "Back to App" → redirects to `/dashboard`

### Network (DevTools)
| Step | Request | Endpoint | Response |
|------|---------|----------|----------|
| 1 | GET | `/api/v1/admin/stats` | `{ totalUsers, totalGames, ... }` |
| 1 | GET | `/api/v1/admin/users?page=1&pageSize=5` | Paginated user list |
| 1 | GET | Approval queue endpoint | Pending games count |

### Console Logs
- No errors expected on clean load
- If API unreachable: `[API Error] GET /api/v1/admin/stats - Network error`

### Server Logs (Serilog)
```
[INF] HTTP GET /api/v1/admin/stats responded 200 in XXms
[INF] HTTP GET /api/v1/admin/users responded 200 in XXms
```

---

## Test 2: Admin Overview — Stats & Blocks

### Pages
- `/admin/overview`
- `/admin/overview/activity`
- `/admin/overview/system`

### Flow
1. On `/admin/overview`: verify 3 Suspense-wrapped blocks load (StatsOverview, SharedGamesBlock, UserManagementBlock)
2. StatsOverview → KPI cards with real numbers (users, games, agents, etc.)
3. SharedGamesBlock → approval queue with approve/reject actions
4. UserManagementBlock → user list with tier badges, suspend/unsuspend
5. Navigate to Activity Feed (`/admin/overview/activity`)
6. Navigate to System Health (`/admin/overview/system`)

### Network
| Step | Request | Endpoint | Cache Key |
|------|---------|----------|-----------|
| 1 | GET | `/api/v1/admin/stats` | `['admin-stats']` |
| 3 | GET | Approval queue | `['approval-queue', statusFilter, search]` |
| 3 | PUT (on approve) | `/api/v1/games/{id}/publish` | Invalidates `['approval-queue']` + `['admin-stats']` |
| 4 | GET | `/api/v1/admin/users?page=1&pageSize=5` | `['admin-users', ...]` |
| 4 | PUT (tier change) | `/api/v1/admin/users/{id}/tier` | Toast: "Tier updated" |
| 4 | POST (suspend) | `/api/v1/admin/users/{id}/suspend` | Toast: "User suspended" |
| 5 | GET | `/api/v1/admin/activity?limit=...` | Activity timeline |
| 6 | GET | `/api/v1/admin/infrastructure/details` | Infra details |
| 6 | GET | `/api/v1/admin/infrastructure/metrics/timeseries?range=...` | Prometheus data |

### Console on Error
- Activity: `Failed to fetch activity: {error}`
- System: `Failed to fetch infrastructure details: {error}`

### Toasts
| Action | Success | Error |
|--------|---------|-------|
| Approve game | "{N} games approved" / "Game approved" | "Failed to approve games..." |
| Reject game | "{N} games rejected" / "Game rejected" | "Failed to reject games..." |
| Update tier | "Tier updated" | "Failed to update tier" |
| Suspend user | "User suspended" | "Failed to suspend user" |
| Unsuspend | "User unsuspended" | "Failed to unsuspend user" |

---

## Test 3: Agents Overview — Metrics & Analytics

### Pages
- `/admin/agents`

### Flow
1. Navigate to `/admin/agents`
2. Verify date range selector (7d/30d/90d)
3. Check 6 KPI cards (invocations, tokens, cost, latency, confidence, satisfaction)
4. UsageChart renders (line chart)
5. CostBreakdownChart renders
6. TopAgentsTable renders with sortable columns (usage/cost/confidence)
7. Click sort header → re-sorts
8. Change date range to 30d → both queries refetch
9. Click manual refresh button → `refetch()` on both queries

### Network
| Step | Request | Endpoint | Cache Key | staleTime |
|------|---------|----------|-----------|-----------|
| 1 | GET | `/api/v1/admin/agents/metrics?startDate=...&endDate=...` | `['agentMetrics', start, end]` | 60s |
| 1 | GET | `/api/v1/admin/agents/metrics/top?limit=10&sortBy=usage&startDate=...&endDate=...` | `['topAgents', sortBy, start, end]` | 60s |
| 8 | GET | Same endpoints with new date range | New cache keys (date changed) | |
| 9 | GET | Same endpoints | Refetch ignoring staleTime | |

### Console on Error
- Shows card: "Failed to load metrics. Please try again."
- Console: `[API Error] GET /api/v1/admin/agents/metrics - {status}`

### Quick Links (verify navigation)
- "Agent Builder" → `/admin/agents/builder`
- "Pipeline Explorer" → `/admin/agents/pipeline`
- "Debug Console" → `/admin/agents/debug`

---

## Test 4: RAG Pipeline Explorer

### Pages
- `/admin/agents/pipeline`

### Flow
1. Navigate to `/admin/agents/pipeline`
2. Dropdown loads last 10 executions (auto-selects first)
3. PipelineDiagram shows 6 nodes: Query → Embedding → VectorSearch → Reranking → LLM → Response
4. Click a node → page scrolls to matching TimelineStep
5. Expand/collapse TimelineStep accordion
6. Check ConfidenceBadge + StrategyBadge in metadata bar
7. If no executions exist → amber card "No RAG Executions Found"

### Network
| Step | Request | Endpoint |
|------|---------|----------|
| 1 | GET | `/api/v1/admin/rag-executions?skip=0&take=10` |
| 2 (auto) | GET | `/api/v1/admin/rag-executions/{firstId}` |
| (dropdown change) | GET | `/api/v1/admin/rag-executions/{newId}` |

### Console on Error
```
Failed to fetch executions: {error}
Failed to fetch execution detail: {error}
Failed to parse execution trace: {error}
```

### Note
- **No React Query** — uses direct `adminClient` calls with `useState`/`useEffect`
- **No auto-polling** — requires page refresh or dropdown change

---

## Test 5: RAG Debug Console

### Pages
- `/admin/agents/debug`

### Flow
1. Navigate to `/admin/agents/debug`
2. Two-column layout: Execution Table (left) + Filters Panel (right, 320px)
3. Verify auto-refresh toggle (green pulsing dot when ON)
4. Change refresh interval: 5s → 10s → 30s
5. Table shows: Time, Status (checkmark/X/lightning), Query (truncated), Strategy badge, Latency badge (green <200ms, amber 200-1000ms, red >1000ms), Confidence badge, Agent
6. Click a table row → loads Execution Detail with WaterfallChart
7. WaterfallChart renders Chrome DevTools-style cascade bars
8. Test filters:
   - Strategy checkboxes (POC/SingleModel/MultiModelConsensus/HybridRAG)
   - Status radio buttons
   - Confidence slider (0-1)
   - Max Latency slider (0-5000ms)
   - Date range inputs
9. Click "Apply Filters" → list reloads
10. Click "Load More" → pagination with skip/take accumulation

### Network
| Step | Request | Endpoint |
|------|---------|----------|
| 1 | GET | `/api/v1/admin/rag-executions?skip=0&take=20` |
| 3 (every N seconds) | GET | Same endpoint (poll refresh) |
| 6 | GET | `/api/v1/admin/rag-executions/{id}` |
| 9 | GET | `/api/v1/admin/rag-executions?strategy=...&status=...&minConfidence=...&maxLatencyMs=...` |
| 10 | GET | `/api/v1/admin/rag-executions?skip=20&take=20` |

### Observable
- **setInterval** visible in Performance tab when auto-refresh is ON
- Requests repeat at chosen interval (5s/10s/30s)
- No SSE here — pure polling

### Console on Error
```
Failed to fetch executions: {error}
Failed to fetch execution detail: {error}
```

---

## Test 6: Strategy Configuration

### Pages
- `/admin/agents/strategy`

### Flow
1. Navigate to `/admin/agents/strategy`
2. 3 overview cards: Active Strategy, Active Models, Avg Confidence+Latency
3. **Retrieval Config** card:
   - TopK: click +/- buttons
   - Min Relevance Score: drag slider
   - Search Type: dropdown (vector/keyword/hybrid)
   - Reranker: toggle switch + model dropdown
   - Cache TTL: dropdown
4. **Generation Config** card:
   - Provider: dropdown (OpenRouter/OpenAI/Ollama)
   - Model: cascading dropdown (changes with provider)
   - Temperature: slider (0-2)
   - Max Tokens: number input
   - Top P: slider (0-1)
   - Budget Mode: switch
5. **Tier Access Matrix**: checkbox grid (tiers × strategies)
   - Toggle a checkbox → mutation fires immediately
6. **Strategy-Model Mappings**: table with Edit buttons
7. Modify any Retrieval/Generation config → amber "Unsaved Changes" banner appears at bottom
8. Click "Save All" → saves + toast
9. Click "Discard" → reverts local changes

### Network
| Step | Request | Endpoint | Cache Key |
|------|---------|----------|-----------|
| 1 | GET | `/api/v1/admin/tier-strategy/matrix` | `['tierStrategyMatrix']` |
| 1 | GET | `/api/v1/admin/tier-strategy/model-mappings` | `['strategyModelMappings']` |
| 5 | PUT | `/api/v1/admin/tier-strategy/access` | Invalidates `['tierStrategyMatrix']` |
| 6 (Edit) | PUT | `/api/v1/admin/tier-strategy/model-mapping` | Invalidates `['strategyModelMappings']` |

### Toasts
| Action | Success | Error |
|--------|---------|-------|
| Save All | "Changes saved" / "Configuration updated successfully" | "Save failed" / `error.message` |

### Important Note
- Retrieval Config and Generation Config are **local state only** (not yet persisted to API)
- Only Tier Access Matrix and Strategy-Model Mappings hit the backend

---

## Test 7: Documents Library (Epic #4789)

### Pages
- `/admin/knowledge-base` → click "Documents" card
- `/admin/knowledge-base/documents`

### Flow
1. From KB hub (`/admin/knowledge-base`), click "Documents" card → navigates correctly (link fix #4783)
2. 4 analytics cards at top: Total Documents, Completed, Processing, Storage
3. Storage Health Bar: PostgreSQL (docs/chunks), Qdrant (vectors/memory), File storage (count/size), Health badge
4. Search input → type text → table filters (resets page to 1)
5. Status dropdown: All/Pending/Processing/Completed/Failed → filters table
6. Table columns: checkbox, Document name, Game, Status badge (color-coded), Pages, Chunks, Size, Uploaded date, Actions
7. Click "Reindex" action → `POST /api/v1/admin/pdfs/{id}/reindex` → toast
8. Select checkboxes → "Bulk Delete" button appears
9. Click "Bulk Delete" → AlertDialog confirmation → confirm → `POST /api/v1/admin/pdfs/bulk/delete`
10. Click "Purge Stale" → AlertDialog → `POST /api/v1/admin/pdfs/maintenance/purge-stale`
11. Click "Cleanup Orphans" → AlertDialog → `POST /api/v1/admin/pdfs/maintenance/cleanup-orphans`
12. Pagination: prev/next + "Page N of M"

### Network
| Step | Request | Endpoint | Cache Key | staleTime |
|------|---------|----------|-----------|-----------|
| 2 | GET | `/api/v1/admin/pdfs/analytics/distribution` | `['admin','pdfs','distribution']` | 60s |
| 3 | GET | `/api/v1/admin/pdfs/storage/health` | `['admin','pdfs','storage-health']` | 60s |
| 2 | GET | `/api/v1/pdfs/admin/pdfs?page=1&pageSize=20&sortBy=uploadedAt&sortOrder=desc` | `['admin','pdfs',{page,pageSize,status,search}]` | 30s |
| 4 | GET | Same + `&search=text` (page resets to 1) | New cache key | |
| 5 | GET | Same + `&status=Completed` | New cache key | |
| 7 | POST | `/api/v1/admin/pdfs/{id}/reindex` | Invalidates `['admin','pdfs']` | |
| 9 | POST | `/api/v1/admin/pdfs/bulk/delete` body: `{pdfIds:[...]}` | Invalidates + clears selection | |
| 10 | POST | `/api/v1/admin/pdfs/maintenance/purge-stale` | Invalidates `['admin','pdfs']` | |
| 11 | POST | `/api/v1/admin/pdfs/maintenance/cleanup-orphans` | Invalidates `['admin','pdfs']` | |

### Server Logs
```
[INF] HTTP GET /api/v1/pdfs/admin/pdfs responded 200
[INF] HTTP POST /api/v1/admin/pdfs/{id}/reindex responded 200
[INF] Admin {AdminId} bulk deleting {Count} PDFs
[INF] Admin {AdminId} purging stale documents
[INF] Admin {AdminId} cleaning up orphans
```

---

## Test 8: Vector Collections (Epic #4789)

### Pages
- `/admin/knowledge-base/vectors`

### Flow
1. Navigate to `/admin/knowledge-base/vectors`
2. 4 stat cards: Total Collections, Total Vectors, Dimensions, Avg Health
3. Grid of VectorCollectionCard components (real data from Qdrant)
4. Click Refresh button → spinning icon during refetch
5. Empty state: "No vector collections found"
6. Error state: red banner with "Retry" button

### Network
| Step | Request | Endpoint | Cache Key | staleTime |
|------|---------|----------|-----------|-----------|
| 1 | GET | `/api/v1/admin/kb/vector-collections` | `['admin','vector-collections']` | 60s |
| 4 | GET | Same (refetch) | Same | |

### Backend
- Calls `IQdrantClientAdapter` directly (no MediatR) — actual Qdrant cluster data

### Console on Error
- Red banner in UI with error message
- Console: `[API Error] GET /api/v1/admin/kb/vector-collections - {status}`

---

## Test 9: Processing Queue Dashboard + SSE (Epic #4729)

### Pages
- `/admin/knowledge-base/queue`

### Flow
1. Navigate to `/admin/knowledge-base/queue`
2. **SSE Connection Indicator** in header:
   - Green pulsing dot = `connected`
   - Yellow = `connecting` / `reconnecting`
   - Red = `error` / `closed`
   - Click when error/closed → manual reconnect
3. QueueStatsBar shows counts per status (4 mini-queries)
4. QueueList shows jobs with status badges
5. QueueFilters: status, search, date range
6. Click a job row → JobDetailPanel opens on right
7. JobStepTimeline: expandable steps with timing
8. JobLogViewer: scrollable log output
9. **Drag & Drop**: drag a job to change priority → `PUT /api/v1/admin/queue/reorder`
10. Job actions: Cancel → `POST .../cancel`, Retry → `POST .../retry`, Remove → `DELETE .../{jobId}`

### Network — Initial Load
| Request | Endpoint | Cache Key | staleTime |
|---------|----------|-----------|-----------|
| GET | `/api/v1/admin/queue?page=1&pageSize=20` | `['admin','queue', filters]` | 30s (SSE on) / 10s (SSE off) |
| GET×4 | `/api/v1/admin/queue?status=Queued&pageSize=1` (×4 statuses) | `['admin','queue','stats','Queued']` etc | 15s |
| EventSource | `/api/v1/admin/queue/stream` | — | Persistent SSE connection |

### Network — SSE Events (EventSource tab in DevTools)
| Event Type | Trigger | React Query Effect |
|------------|---------|-------------------|
| `Heartbeat` | Every ~15s from server | None (keepalive) |
| `JobQueued` | New job enqueued | Invalidates `['admin','queue']` |
| `JobStarted` | Job begins processing | Invalidates `['admin','queue']` |
| `StepCompleted` | Processing step done | Invalidates detail + list (debounced 200ms) |
| `LogEntry` | New log line | Invalidates detail (debounced 200ms) |
| `JobCompleted` | Job finished OK | Invalidates `['admin','queue']` |
| `JobFailed` | Job errored | Invalidates `['admin','queue']` |
| `JobRemoved` | Job deleted | Invalidates `['admin','queue']` |
| `JobRetried` | Job re-queued | Invalidates `['admin','queue']` |
| `QueueReordered` | Priority changed | Invalidates `['admin','queue']` |

### Network — Per-Job SSE (when detail panel open)
| Request | Endpoint |
|---------|----------|
| EventSource | `/api/v1/admin/queue/{jobId}/stream` |

**Auto-closes** on `JobCompleted` or `JobFailed` events.
**Reconnect**: max 5 attempts, 1s→16s exponential backoff.

### Network — Actions
| Action | Method | Endpoint | Toast Success | Toast Error |
|--------|--------|----------|---------------|-------------|
| Cancel | POST | `/api/v1/admin/queue/{id}/cancel` | "Job cancelled" | "Failed to cancel job." |
| Retry | POST | `/api/v1/admin/queue/{id}/retry` | "Job retried" / "re-queued" | "Failed to retry job." |
| Remove | DELETE | `/api/v1/admin/queue/{id}` | "Job removed" | "Failed to remove job." |
| Reorder | PUT | `/api/v1/admin/queue/reorder` body: `{orderedJobIds}` | — | "Failed to reorder queue." |

### Polling Fallback (when SSE disconnected)
| Cache Key | refetchInterval |
|-----------|-----------------|
| Queue list | 15s |
| Job detail | 10s (stops on terminal status) |
| Queue stats | 30s |

### Server Logs
```
[INF] SSE client connected to /api/v1/admin/queue/stream
[INF] SSE event: JobStarted for job {JobId}
[INF] SSE event: StepCompleted for job {JobId}, step {StepName}
[INF] SSE client disconnected from /api/v1/admin/queue/stream
```

---

## Test 10: Agent Hub (Epic #4681)

### Pages
- `/agents`
- `/agents/[id]`

### Flow
1. Navigate to `/agents`
2. Agents grouped by game (game header → agent cards below)
3. RAG-Ready filter toggle
4. Click "Create Agent" → creation flow (Tiered Config form)
5. Click an agent card → `/agents/[id]` detail page
6. MeepleCard actions: "Add Agent" / "Chat" buttons visible

### Network
| Step | Request | Endpoint |
|------|---------|----------|
| 1 | GET | `/api/v1/user/agents?grouped=true` (or similar) |
| 4 | POST | `/api/v1/agents/create-with-setup` |
| 5 | GET | `/api/v1/agents/{id}` |

---

## Test 11: MeepleCard Navigation System (Epic #4688)

### Pages (all new)
| Route | What to verify |
|-------|----------------|
| `/games` | MeepleCard with CardNavigationFooter |
| `/games/[id]` | Game detail + navigation footer links |
| `/players` | EntityListView with 4 view modes |
| `/players/[id]` | Player detail + navigation footer |
| `/chat` | Chat list with navigation |
| `/chat/[threadId]` | Chat detail with navigation context |
| `/knowledge-base/[id]` | KB document detail + nav footer |
| `/demo/entity-list-view` | All view modes demo |
| `/demo/entity-list-complete` | Complete demo with all filters |

### Flow
1. Go to `/games` → verify cards have navigation footer (row of entity-colored icon buttons)
2. Click a game → `/games/[id]` → verify footer shows links to related agents, chat, sessions
3. Click "Players" in footer → navigates to `/players`
4. On `/players`: switch View Modes (Grid → List → Carousel → Table)
5. In List mode: verify glassmorphic Sidebar Filters appear
6. Click a player → `/players/[id]` → verify navigation footer
7. Navigate to `/chat` → verify list renders
8. Click a thread → `/chat/[threadId]` → verify navigation context
9. Check Breadcrumb Trail updates as you navigate between entity pages
10. Visit `/demo/entity-list-view` → all 4 view modes working
11. Visit `/demo/entity-list-complete` → search, sort, filters all working

### Network
- **CardNavigationFooter**: No API calls (pure config from `entity-navigation.ts`)
- **EntityListView**: No API calls internally (receives `items` prop)
- Data fetching happens in parent page components

### Observable
- View mode persisted in `localStorage` (key = `persistenceKey` prop)
- Search/sort/filter are all **client-side** (no network activity)
- Switching view modes triggers no API calls

---

## Test 12: Budget Display System

### Pages
- `/dashboard/budget`

### Flow
1. Navigate to `/dashboard/budget`
2. 3 stat cards: Daily Credits (with progress bar + reset timer), Weekly Credits, Status badge
3. Progress bar colors: green (<80%), yellow (80-95%), red (>95%)
4. Daily reset countdown timer
5. Tier comparison grid: Basic (1K/day), Pro (5K/day, "Popular" badge), Enterprise (infinite)
6. Click "View Upgrade Options" → navigates to `/dashboard/settings/billing`
7. If `isBlocked === true`: alert card "Budget Exhausted" visible at top
8. Wait 60s → data refreshes automatically (polling)

### Network
| Step | Request | Endpoint | Polling |
|------|---------|----------|---------|
| 1 | GET | `/api/v1/budget/user/{userId}` | Every 60s via setInterval |

### Note
- Uses `useEffect` + `setInterval` (NOT React Query)
- No React Query cache invalidation
- Dependent on `useAuth()` providing `user.id`

### Console on Error
- Red card with `AlertCircle` + error message string
- Console: `[API Error] GET /api/v1/budget/user/... - {status}`

---

## Test 13: Agent Slots & Creation Flow

### Pages
- `/agent/slots`
- (AgentConfigSheet — triggered from game pages)

### Flow
1. Navigate to `/agent/slots`
2. SlotCards show: active (in use), available (free), locked (tier limit)
3. Usage statistics section
4. Open AgentConfigSheet from a game page
5. Configure agent: select game, model, strategy, template
6. Click "Start Chat" → POST creation + navigate to `/chat/new?gameId=...`

### Network
| Step | Request | Endpoint | Cache Key | staleTime |
|------|---------|----------|-----------|-----------|
| 1 | GET | `/api/v1/user/agent-slots` | `['agent-slots','user']` | 30s |
| 6 | POST | `/api/v1/agents/create-with-setup` | Invalidates: `['agent-slots']`, `['agents']`, `['user-library']` | |

### Toasts (creation)
| Scenario | Toast |
|----------|-------|
| Success | Navigates to chat |
| "Agent limit reached" | "No agent slots available" + upgrade message |
| "unique name" conflict | "Agent name conflict" |
| Other error | "Agent creation failed" + `error.message` |

---

## Test 14: GameSelector & ModelSelector (Current Branch #4774/#4775)

### Pages
- Any page that opens AgentConfigSheet (e.g., game detail pages)

### Flow — GameSelector
1. Open AgentConfigSheet
2. GameSelector shows loading state: `<Loader2>` spinner + "Loading games..."
3. After load: dropdown with user's library games
4. If library has >5 games: search input appears at top
5. Each game shows: title, publisher, `📚 Rulebook` badge (if `hasPdfDocuments`), `★` star (if `isFavorite`)
6. Select a game → `onChange(gameId, game)` fires
7. **Error state**: red box with `AlertCircle` + "Failed to load games. Please try again."
8. **Empty state**: Library icon + "No games in your library" + "Add games to your collection first"

### Network — GameSelector
| Request | Endpoint | Cache Key | staleTime |
|---------|----------|-----------|-----------|
| GET | `/api/v1/library?page=1&pageSize=100` | `['library','list',{params:{page:1,pageSize:100}}]` | 2 min |

### Flow — ModelSelector
1. ModelSelector shows loading: `<Loader2>` + "Loading models..."
2. After load: dropdown with active AI models
3. Each model shows: provider icon (emoji), display name, provider label, cost/1K tokens
4. Primary model has "Default" badge
5. Select a model → cost breakdown appears below
6. **Error state**: red box with `AlertCircle` + "Failed to load models. Please try again."
7. **Empty state**: Sparkles icon + "No models available"

### Network — ModelSelector
| Request | Endpoint | Cache Key | staleTime | refetchInterval |
|---------|----------|-----------|-----------|-----------------|
| GET | `/api/v1/admin/ai-models?status=active&page=1&pageSize=50` | `['aiModels','list',{...}]` | 2 min | 2 min |

### Important
- GameSelector search is **client-side** (no API call on keystroke)
- ModelSelector uses **admin endpoint** — requires admin role

---

## Test 15: Admin Game Wizard (End-to-End)

### Pages
- `/admin/agents/builder` → "Create New Agent"
- Wizard flow across 5 phases

### Flow
1. `/admin/agents/builder` → click "Create New Agent" → `/admin/agent-definitions/create`
2. **Phase 1**: BGG Search — search for a game, select from results
3. **Phase 2**: PDF Upload — upload a PDF rulebook
4. **Phase 3**: Processing Monitor — real-time SSE progress
5. **Phase 4**: Auto-create Agent — agent created automatically on PDF completion
6. **Phase 5**: Agent Testing — interactive chat test

### Network — Phase 1
| Request | Endpoint |
|---------|----------|
| POST | `/api/v1/admin/games/wizard/create` |

### Network — Phase 2
| Request | Endpoint |
|---------|----------|
| POST | `/api/v1/admin/games/wizard/{gameId}/launch-processing` |

### Network — Phase 3 (SSE)
| Request | Endpoint | Type |
|---------|----------|------|
| GET | `/api/v1/admin/games/wizard/{gameId}/progress/stream` | **SSE** `text/event-stream` |

Polls DB every 1.5s, auto-closes after 5 idle polls.

### Network — Phase 5
| Request | Endpoint |
|---------|----------|
| POST | `/api/v1/admin/games/{gameId}/agent/auto-test` |

### Server Logs
```
[INF] Admin {AdminId} creating game from wizard
[INF] Admin {AdminId} launching processing for game {GameId}
[INF] SSE client connected to wizard progress stream for game {GameId}
[INF] Admin {AdminId} running auto-test for game {GameId}
```

---

## Test 16: Monitoring — Real Prometheus Data

### Pages
- `/admin/overview/system`

### Flow
1. Navigate to `/admin/overview/system`
2. Verify charts show real time-series data (not flat/mock lines)
3. Data should vary over time if services are active

### Network
| Request | Endpoint |
|---------|----------|
| GET | `/api/v1/admin/infrastructure/details` |
| GET | `/api/v1/admin/infrastructure/metrics/timeseries?range=1h` |

### Observable
- If Prometheus is not running: charts may show empty/no-data state
- Server logs show metrics query to Prometheus

---

## Network Patterns Reference

### httpClient Retry Behavior
- **Retryable**: 500, 502, 503, network errors → 3 attempts, 1s base delay, exponential backoff + 30% jitter
- **Non-retryable**: 4xx client errors (400, 401, 403, 404, 409, 422)
- **Rate limited**: 429 → honors `Retry-After` header with adaptive backoff
- **Circuit breaker**: Trips after repeated failures per endpoint path

### Console Log Patterns
| Pattern | Meaning |
|---------|---------|
| `[API Error] GET /api/v1/... - Network error` | Server unreachable |
| `[API Error] GET /api/v1/... - 401` | Auth expired (GET returns null silently) |
| `[API Error] POST /api/v1/... - 401` | Auth expired (throws) |
| `[API Error] GET /api/v1/... - 500` | Server error (will retry 3x) |
| `[API Warning] ...` | Non-critical issue |
| `[API Info] Adaptive backoff: ...` | Rate limit handling |

### Server Log Patterns (Serilog)
Every request logged with:
- `RequestId` / `CorrelationId` (matches `X-Correlation-ID` header)
- `RequestPath`, `RequestMethod`
- `UserId`, `UserEmail` (if authenticated)
- Response status + duration in ms

### SSE Connection Lifecycle
```
Browser: new EventSource('/api/v1/admin/queue/stream')
Server:  [INF] SSE client connected
Server:  [data: Heartbeat] every ~15s
Server:  [data: JobStarted {...}]
Browser: event → invalidateQueries
...
Browser: EventSource.close()
Server:  [INF] SSE client disconnected
```

---

## Quick Reference: All API Endpoints Hit During Full Test Run

### Admin Stats & Overview
- `GET /api/v1/admin/stats`
- `GET /api/v1/admin/users?...`
- `GET /api/v1/admin/activity?...`
- `GET /api/v1/admin/infrastructure/details`
- `GET /api/v1/admin/infrastructure/metrics/timeseries?range=...`

### Agents
- `GET /api/v1/admin/agents/metrics?startDate=...&endDate=...`
- `GET /api/v1/admin/agents/metrics/top?limit=10&sortBy=...`
- `GET /api/v1/admin/rag-executions?skip=...&take=...`
- `GET /api/v1/admin/rag-executions/{id}`
- `GET /api/v1/admin/tier-strategy/matrix`
- `GET /api/v1/admin/tier-strategy/model-mappings`
- `PUT /api/v1/admin/tier-strategy/access`
- `PUT /api/v1/admin/tier-strategy/model-mapping`

### Knowledge Base & Documents
- `GET /api/v1/admin/kb/vector-collections`
- `GET /api/v1/pdfs/admin/pdfs?...`
- `GET /api/v1/admin/pdfs/analytics/distribution`
- `GET /api/v1/admin/pdfs/storage/health`
- `POST /api/v1/admin/pdfs/{id}/reindex`
- `POST /api/v1/admin/pdfs/bulk/delete`
- `POST /api/v1/admin/pdfs/maintenance/purge-stale`
- `POST /api/v1/admin/pdfs/maintenance/cleanup-orphans`

### Processing Queue
- `GET /api/v1/admin/queue?...`
- `GET /api/v1/admin/queue/{jobId}`
- `POST /api/v1/admin/queue/{jobId}/cancel`
- `POST /api/v1/admin/queue/{jobId}/retry`
- `DELETE /api/v1/admin/queue/{jobId}`
- `PUT /api/v1/admin/queue/reorder`
- **SSE** `GET /api/v1/admin/queue/stream`
- **SSE** `GET /api/v1/admin/queue/{jobId}/stream`

### User-facing
- `GET /api/v1/library?page=1&pageSize=100`
- `GET /api/v1/admin/ai-models?status=active&page=1&pageSize=50`
- `GET /api/v1/user/agent-slots`
- `POST /api/v1/agents/create-with-setup`
- `GET /api/v1/budget/user/{userId}`

### Wizard
- `POST /api/v1/admin/games/wizard/create`
- `POST /api/v1/admin/games/wizard/{gameId}/launch-processing`
- **SSE** `GET /api/v1/admin/games/wizard/{gameId}/progress/stream`
- `POST /api/v1/admin/games/{gameId}/agent/auto-test`
