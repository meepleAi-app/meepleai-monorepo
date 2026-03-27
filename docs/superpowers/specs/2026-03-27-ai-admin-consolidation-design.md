# AI Admin Consolidation — Design Spec

**Date**: 2026-03-27
**Status**: Approved
**Scope**: Consolidate 13 admin AI pages into 7, add Mission Control hub

## Problem

The admin AI section has 13 sidebar items that are:
1. Flat and unorganized — hard to find what you need
2. Not discoverable — users don't know these pages exist
3. Fragmented — related features spread across separate pages (e.g., Debug Console and Pipeline Explorer show different views of the same execution data)

All 13 pages are functional with real API calls, but the navigation makes them invisible.

## Solution

Consolidate 13 pages into 7 by merging related pages as tabs within unified views. Add a Mission Control landing page as the entry point.

## Page Architecture

### 1. Mission Control (`/admin/agents`)

**Replaces**: Current All Agents overview (KPI-only view)
**New page**: Yes (replaces existing page content)

**Layout (3 rows):**

| Row | Content | Data Source |
|-----|---------|-------------|
| KPI Cards (5) | Executions today, Avg latency, Error rate, Tokens consumed, Cost today | `getAgentMetrics()`, `getRagExecutionStats()`, `getOpenRouterStatus()` |
| Health + Actions | Service health (embedding, reranker, OpenRouter, Qdrant) + Quick action buttons linking to other 6 pages | `getEmbeddingInfo()`, `getOpenRouterStatus()`, `getVectorCollections()` |
| Recent Executions | Mini-table of last 5 RAG executions with link to Inspector | `getRagExecutions({ take: 5 })` |

**Quick Actions**: Test RAG Query → Playground, Inspect Executions → Inspector, Cost Report → Usage, New Agent → Definitions

**No new backend endpoints required.**

### 2. RAG Inspector (`/admin/agents/inspector`)

**Merges**: Debug Console + Pipeline Explorer
**Tabs:**

| Tab | Content | Source Components |
|-----|---------|-------------------|
| Esecuzioni | Live execution table with filters, auto-refresh (5s/10s/30s), stats bar | From `/agents/debug` page |
| Pipeline | 6-step pipeline diagram with timing per step | From `/agents/pipeline` — `PipelineDiagram` component |
| Waterfall | Cascade chart for selected execution | Existing `WaterfallChart` component |

**Detail panel**: Click any execution row → sheet/panel with full trace, pipeline breakdown, chunks retrieved, prompt sent, response, and Replay button.

**Cross-tab navigation**: Selecting an execution in Esecuzioni tab and clicking "Pipeline" opens Pipeline tab pre-loaded with that execution. Same for Waterfall.

### 3. RAG Playground (`/admin/agents/playground`)

**Merges**: RAG Sandbox + Debug Chat
**Tabs:**

| Tab | Content | Source Components |
|-----|---------|-------------------|
| Query Tester | Split layout: left (query input + 6 config params), right (response + chunks + metrics) | Refactored from `SandboxClient` |
| Chat Debug | Streaming conversation with pipeline events | From `/agents/debug-chat` — `useDebugChatStream` hook |
| Compare | Side-by-side execution comparison with delta metrics | New — uses existing `CompareRagExecutionsQuery` backend endpoint |

**Query Tester parameters:**
- Strategy (dropdown: POC, SingleModel, MultiModel, HybridRAG)
- Model (dropdown from available models)
- Temperature (slider 0-1)
- Top-K Chunks (number input)
- Game Scope (dropdown from shared games)
- Agent (dropdown from agent definitions)

**Compare tab**: Select two executions (from history or run two variants) → shows response, latency, token count, cost, confidence side-by-side with computed deltas (% faster, % cheaper, etc.)

### 4. Agent Definitions (`/admin/agents/definitions`)

**Merges**: Definitions list + Agent Builder
**Structure**: List page with inline Builder accessible via Sheet/side panel (not separate route).

Current `/agents/definitions` list page stays as-is. The Builder (`/agents/builder`) becomes a Sheet that opens from a "New Agent" or "Edit" action on the list, instead of navigating away.

Sub-routes preserved:
- `/admin/agents/definitions/[id]` — detail view
- `/admin/agents/definitions/[id]/edit` — edit view
- `/admin/agents/definitions/create` — create (opens builder)

### 5. Configuration (`/admin/agents/config`)

**Merges**: Strategy Config + Models & Prompts + Chat Limits
**Tabs:**

| Tab | Content | Source Page |
|-----|---------|------------|
| Strategy | RAG pipeline strategy selection and tier mapping | From `/agents/strategy` |
| Models | LLM model management, health, change history | From `/agents/models` |
| Limits | Chat request limits per tier | From `/agents/chat-limits` |

### 6. Usage & Costs (`/admin/agents/usage`)

**Merges**: Usage & Costs + Chat History
**Tabs:**

| Tab | Content | Source Page |
|-----|---------|------------|
| OpenRouter | KPI cards, timeline chart, cost breakdown, rate limit gauge | From current `/agents/usage` |
| Token Balance | User token balance, tier usage, top consumers | From `getTokenBalance()`, `getTokenTierUsage()`, `getTopConsumers()` endpoints |
| Chat Log | Past agent conversations (filterable) | From `/agents/chat-history` |

**Token Balance tab is partially new** — the backend endpoints exist (`/api/v1/admin/token-management/*`) but no dedicated frontend page uses them yet. Components to build: balance card, tier usage chart, top consumers table.

### 7. Analytics (`/admin/agents/analytics`)

**Keeps**: Current analytics page, enhanced with sub-tabs
**Tabs:**

| Tab | Content |
|-----|---------|
| Overview | Agent metrics KPI cards, usage charts (existing) |
| Top Agents | Top agents by invocations, cost, confidence (existing) |
| Trends | Time-series trends (to be built from existing `getRagExecutionStats()` with date ranges) |

## Sidebar Navigation Update

Update `admin-dashboard-navigation.ts` AI section from 13 flat items to 7:

```typescript
sidebarItems: [
  { href: '/admin/agents', label: 'Mission Control', icon: GaugeIcon },
  { href: '/admin/agents/inspector', label: 'RAG Inspector', icon: SearchIcon },
  { href: '/admin/agents/playground', label: 'RAG Playground', icon: FlaskConicalIcon },
  { href: '/admin/agents/definitions', label: 'Agent Definitions', icon: ListIcon },
  { href: '/admin/agents/config', label: 'Configuration', icon: Settings2Icon },
  { href: '/admin/agents/usage', label: 'Usage & Costs', icon: TrendingUpIcon },
  { href: '/admin/agents/analytics', label: 'Analytics', icon: BarChartIcon },
]
```

## Route Migration

Old routes that get redirects or removal:

| Old Route | New Route | Action |
|-----------|-----------|--------|
| `/admin/agents` | `/admin/agents` | Replace page content (Mission Control) |
| `/admin/agents/debug` | `/admin/agents/inspector` | Redirect |
| `/admin/agents/pipeline` | `/admin/agents/inspector?tab=pipeline` | Redirect |
| `/admin/agents/debug-chat` | `/admin/agents/playground?tab=chat` | Redirect |
| `/admin/agents/sandbox` | `/admin/agents/playground` | Redirect |
| `/admin/agents/builder` | `/admin/agents/definitions` | Redirect (builder opens as sheet) |
| `/admin/agents/strategy` | `/admin/agents/config` | Redirect |
| `/admin/agents/models` | `/admin/agents/config?tab=models` | Redirect |
| `/admin/agents/chat-limits` | `/admin/agents/config?tab=limits` | Redirect |
| `/admin/agents/chat-history` | `/admin/agents/usage?tab=chat-log` | Redirect |
| `/admin/agents/ab-testing/*` | `/admin/agents/playground?tab=compare` | Redirect |
| `/admin/agents/definitions/*` | `/admin/agents/definitions/*` | Keep as-is |
| `/admin/agents/usage` | `/admin/agents/usage` | Keep (enhanced) |
| `/admin/agents/analytics` | `/admin/agents/analytics` | Keep (enhanced) |

Redirects implemented via Next.js `redirect()` in old page.tsx files — not removed to avoid broken bookmarks.

## Implementation Strategy

**Core principle**: Recompose, don't rewrite. Existing page components become tab content within new wrapper pages. No business logic changes.

**Steps (ordered by dependency):**

1. **Mission Control page** — new page, no dependencies on other changes
2. **RAG Inspector** — wrap Debug Console + Pipeline components in tabbed layout
3. **RAG Playground** — wrap Sandbox + Debug Chat in tabs, add Compare tab (new)
4. **Configuration** — wrap Strategy + Models + Limits in tabs
5. **Usage & Costs** — add Token Balance tab + Chat History tab to existing Usage page
6. **Agent Definitions** — integrate Builder as Sheet into list page
7. **Analytics** — add Trends sub-tab
8. **Sidebar update** — update navigation config
9. **Redirects** — add redirect() to old routes
10. **Cleanup** — remove orphaned page files that are now redirects

## Data Dependencies

| API Endpoint | Used By | Status |
|--------------|---------|--------|
| `GET /api/v1/admin/agents/metrics` | Mission Control, Analytics | Existing |
| `GET /api/v1/admin/rag-executions` | Mission Control, Inspector | Existing |
| `GET /api/v1/admin/rag-executions/{id}` | Inspector detail panel | Existing |
| `GET /api/v1/admin/rag-executions/stats` | Mission Control, Inspector, Analytics | Existing |
| `GET /api/v1/admin/embedding/info` | Mission Control health | Existing |
| `GET /api/v1/admin/embedding/metrics` | Mission Control health | Existing |
| `POST /api/v1/admin/rag-pipeline/test` | Playground Query Tester | Existing |
| `POST /api/v1/admin/rag-pipeline/{id}/compare` | Playground Compare | Existing |
| `GET /api/v1/admin/token-management/balance` | Usage Token Balance | Existing |
| `GET /api/v1/admin/token-management/tier-usage` | Usage Token Balance | Existing |
| `GET /api/v1/admin/token-management/top-consumers` | Usage Token Balance | Existing |
| All OpenRouter endpoints | Usage OpenRouter tab | Existing |
| All strategy/model/limit endpoints | Configuration tabs | Existing |

**No new backend endpoints required.** All data sources already exist.

## Testing Strategy

- **Existing tests**: The 13 current pages have tests. Tests for components that move (not rewrite) should still pass.
- **New tests needed**: Mission Control page, tab navigation in consolidated pages, Compare tab in Playground, Token Balance tab in Usage.
- **Redirect tests**: Verify old routes redirect correctly.
- **Typecheck**: Must pass after all changes.

## Out of Scope

- Backend API changes
- New data models or database changes
- A/B testing framework (existing sub-routes under `/ab-testing` redirect to Compare tab)
- RAG quality page (`/admin/rag-quality`) — separate section, not part of AI consolidation
- Grafana/monitoring integration
