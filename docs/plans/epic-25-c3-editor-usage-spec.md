# C3: Editor Self-Service Usage Page — Detailed Specification

**Epic**: #25 — LLM Transparency & Editor Experience
**Priority**: 🟡 Important
**Status**: Draft
**Date**: 2026-03-10

## Problem Statement

Editors use LLM features daily but have zero visibility into their personal usage, cost impact, or model distribution. All analytics are currently admin-only (`/admin/agents/analytics`). Editors need self-service access to understand their own AI consumption without admin involvement.

## Design Decision

New page at `/dashboard/my-ai-usage` (NOT under `/admin/`) accessible to **Editor and above** roles. Shows only the current user's data — never exposes other users' information.

## Architecture: F5↔C3 Conflict Resolution

### The Problem

GDPR pseudonymization (Epic F, Issue F5) replaces `UserId` with SHA-256 hash in `llm_request_logs` after **7 days**. This makes per-user queries impossible for days 7-30. But C3 needs 30-day personal usage visibility.

### The Solution: Aggregated Usage Summary Table

Introduce a **pre-aggregated daily summary** entity separate from raw request logs:

```
Raw Logs (llm_request_logs)          Usage Summaries (user_ai_usage_summaries)
├─ Full request detail               ├─ Daily aggregates per user
├─ Pseudonymized after 7 days        ├─ NOT pseudonymized (aggregated = non-PII)
├─ Deleted after 30 days             ├─ Retained 90 days (configurable)
└─ Used for: audit, admin debug      └─ Used for: C3 "My AI Usage" page
```

GDPR compliance rationale: Aggregated counts (e.g., "User X made 47 requests on March 5") are not considered personal data under Art. 4(1) when they don't reveal the content of communications. The summary stores only counts, token sums, and cost totals — no prompt content, no conversation data.

---

## Domain Model

### New Entity: `UserAiUsageDailySummary`

**Bounded Context**: KnowledgeBase (co-located with LlmRequestLog)

```csharp
public sealed class UserAiUsageDailySummary : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public int RequestCount { get; private set; }
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public int TotalTokens { get; private set; }
    public decimal CostUsd { get; private set; }
    public int AverageLatencyMs { get; private set; }
    public string ModelDistributionJson { get; private set; }    // {"gpt-4o": 12, "llama3:8b": 35}
    public string StrategyDistributionJson { get; private set; } // {"Fast": 20, "Balanced": 15, "Premium": 12}
    public string ProviderDistributionJson { get; private set; } // {"ollama": 30, "openrouter": 17}
    public DateTime ComputedAt { get; private set; }

    // Factory method
    public static UserAiUsageDailySummary Create(
        Guid userId, DateOnly date, int requestCount,
        int promptTokens, int completionTokens, int totalTokens,
        decimal costUsd, int averageLatencyMs,
        Dictionary<string, int> modelDistribution,
        Dictionary<string, int> strategyDistribution,
        Dictionary<string, int> providerDistribution)
    {
        // validation: requestCount >= 0, date <= today, etc.
    }
}
```

### EF Configuration

```csharp
public class UserAiUsageDailySummaryConfiguration : IEntityTypeConfiguration<UserAiUsageDailySummary>
{
    public void Configure(EntityTypeBuilder<UserAiUsageDailySummary> builder)
    {
        builder.ToTable("user_ai_usage_summaries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Date).IsRequired();
        builder.Property(x => x.CostUsd).HasPrecision(18, 6);
        builder.Property(x => x.ModelDistributionJson).HasColumnType("jsonb");
        builder.Property(x => x.StrategyDistributionJson).HasColumnType("jsonb");
        builder.Property(x => x.ProviderDistributionJson).HasColumnType("jsonb");

        // Unique constraint: one summary per user per day
        builder.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
        // Query index for date-range queries
        builder.HasIndex(x => new { x.UserId, x.Date })
            .HasDatabaseName("ix_user_ai_usage_summaries_user_date");
    }
}
```

### Background Job: `UserAiUsageAggregationJob`

- **Schedule**: Daily at 1:30 AM UTC (runs BEFORE pseudonymization at 2:00 AM)
- **Logic**: Aggregate yesterday's `llm_request_logs` WHERE `UserId IS NOT NULL AND NOT IsAnonymized`
- **Idempotent**: Uses UPSERT (ON CONFLICT (UserId, Date) DO UPDATE)
- **Retention**: Deletes summaries older than 90 days
- **Catch-up**: On first run or after downtime, processes all non-aggregated days in the 30-day window

---

## Backend API Contract

### Endpoints

All endpoints require `Editor` role or above. All return only the authenticated user's data.

#### 1. GET `/api/v1/users/me/ai-usage/summary`

Returns aggregated totals for configurable periods.

**Query Parameters**:
- None (returns all three periods)

**Response** (200):
```json
{
  "today": {
    "requestCount": 12,
    "promptTokens": 8400,
    "completionTokens": 3200,
    "totalTokens": 11600,
    "costUsd": 0.0234,
    "averageLatencyMs": 1250
  },
  "last7Days": {
    "requestCount": 87,
    "promptTokens": 62000,
    "completionTokens": 24000,
    "totalTokens": 86000,
    "costUsd": 0.1720,
    "averageLatencyMs": 1180
  },
  "last30Days": {
    "requestCount": 342,
    "promptTokens": 245000,
    "completionTokens": 98000,
    "totalTokens": 343000,
    "costUsd": 0.6840,
    "averageLatencyMs": 1210
  }
}
```

**Implementation**: CQRS Query `GetMyAiUsageSummaryQuery` → handler queries `UserAiUsageDailySummary` with SUM aggregation. Today's data comes from live `llm_request_logs` (not yet aggregated).

#### 2. GET `/api/v1/users/me/ai-usage/daily`

Returns daily breakdown for charts.

**Query Parameters**:
- `days` (int, default: 30, max: 90) — number of days to return

**Response** (200):
```json
{
  "days": [
    {
      "date": "2026-03-10",
      "requestCount": 12,
      "totalTokens": 11600,
      "costUsd": 0.0234,
      "isLive": true
    },
    {
      "date": "2026-03-09",
      "requestCount": 15,
      "totalTokens": 14200,
      "costUsd": 0.0312,
      "isLive": false
    }
  ]
}
```

`isLive: true` indicates today's data is from live query (may update on refresh).

#### 3. GET `/api/v1/users/me/ai-usage/distributions`

Returns model, strategy, and provider distribution for pie charts.

**Query Parameters**:
- `days` (int, default: 30, max: 90)

**Response** (200):
```json
{
  "models": [
    { "name": "llama3:8b", "count": 180, "percentage": 52.6 },
    { "name": "gpt-4o-mini", "count": 95, "percentage": 27.8 },
    { "name": "claude-3-haiku", "count": 67, "percentage": 19.6 }
  ],
  "strategies": [
    { "name": "Fast", "count": 150, "percentage": 43.9 },
    { "name": "Balanced", "count": 120, "percentage": 35.1 },
    { "name": "Premium", "count": 50, "percentage": 14.6 },
    { "name": "HybridRAG", "count": 22, "percentage": 6.4 }
  ],
  "providers": [
    { "name": "ollama", "count": 210, "percentage": 61.4 },
    { "name": "openrouter", "count": 132, "percentage": 38.6 }
  ]
}
```

#### 4. GET `/api/v1/users/me/ai-usage/recent`

Returns recent individual requests (only from the last 7 days — before pseudonymization).

**Query Parameters**:
- `page` (int, default: 1)
- `pageSize` (int, default: 20, max: 50)

**Response** (200):
```json
{
  "items": [
    {
      "requestedAt": "2026-03-10T14:22:00Z",
      "model": "llama3:8b",
      "provider": "ollama",
      "strategy": "Fast",
      "promptTokens": 650,
      "completionTokens": 280,
      "costUsd": 0.0000,
      "latencyMs": 890,
      "success": true
    }
  ],
  "total": 87,
  "page": 1,
  "pageSize": 20,
  "note": "Individual requests are available for the last 7 days only (GDPR compliance)."
}
```

**Implementation**: Queries `llm_request_logs` WHERE `UserId = currentUser AND NOT IsAnonymized AND RequestedAt >= 7 days ago`. Does NOT expose `SessionId`, `ErrorMessage`, or prompt content.

---

## Frontend Specification

### Route: `/dashboard/my-ai-usage`

**Access**: Editor role and above (`isEditorOrAbove(user)`)
**Layout**: Dashboard layout (not admin layout)
**Navigation**: Add "AI Usage" item to dashboard sidebar with 🤖 icon

### Page Structure

```
┌─────────────────────────────────────────────────────────┐
│  Il mio utilizzo AI                                     │
│  Scopri come utilizzi le funzionalità AI di MeepleAI    │
├──────────┬──────────┬──────────┬────────────────────────┤
│  Oggi    │  7 Giorni│ 30 Giorni│                        │
│  12 req  │  87 req  │  342 req │  ← Summary cards       │
│  11.6K   │  86K     │  343K    │     (tokens)           │
│  $0.02   │  $0.17   │  $0.68   │     (cost)             │
├──────────┴──────────┴──────────┴────────────────────────┤
│                                                         │
│  ┌─ Richieste giornaliere (30g) ───────────────────┐   │
│  │  [Bar chart: daily request count]                │   │
│  └──────────────────────────────────────────────────┘   │
│                                                         │
│  ┌─ Distribuzione Modelli ─┐ ┌─ Distribuzione Strategie│
│  │  [Donut chart]          │ │  [Donut chart]          │
│  │  llama3:8b  52.6%       │ │  Fast      43.9%        │
│  │  gpt-4o    27.8%        │ │  Balanced  35.1%        │
│  │  claude    19.6%        │ │  Premium   14.6%        │
│  └─────────────────────────┘ └──────────────────────────┘
│                                                         │
│  ┌─ Richieste recenti (ultimi 7 giorni) ───────────┐   │
│  │  Timestamp  │ Modello │ Strategia │ Tokens │ Costo│  │
│  │  14:22      │ llama3  │ Fast      │ 930    │ $0.00│  │
│  │  14:10      │ gpt-4o  │ Balanced  │ 1420   │ $0.01│  │
│  │  ...                                              │  │
│  │  [1] [2] [3] ... pagination                       │  │
│  └──────────────────────────────────────────────────┘   │
│                                                         │
│  ⓘ I dati individuali sono disponibili per gli ultimi   │
│    7 giorni. I totali aggregati coprono 30 giorni.      │
└─────────────────────────────────────────────────────────┘
```

### Component Breakdown

| Component | Description | Data Source |
|-----------|-------------|-------------|
| `UsageSummaryCards` | 3 period cards (today/7d/30d) with req count, tokens, cost | `/me/ai-usage/summary` |
| `DailyRequestChart` | Bar chart with daily request count | `/me/ai-usage/daily` |
| `ModelDistributionChart` | Donut chart with model breakdown | `/me/ai-usage/distributions` |
| `StrategyDistributionChart` | Donut chart with strategy breakdown | `/me/ai-usage/distributions` |
| `RecentRequestsTable` | Paginated table of recent individual requests | `/me/ai-usage/recent` |
| `UsageInfoBanner` | Info notice about 7-day detail vs 30-day aggregate | Static |

### Charting Library

Use **recharts** (already in `apps/web/package.json` as dependency of existing admin analytics pages). If not present, use a lightweight alternative compatible with Next.js App Router.

### Empty State

When user has no AI usage:
- Summary cards show 0 for all values
- Charts show "Nessun dato disponibile" placeholder
- Recent requests table shows "Non hai ancora utilizzato le funzionalità AI. Prova a chattare con un agente!"
- No error state — empty is a valid state

### Loading State

- Skeleton loaders for summary cards (3 rectangles)
- Skeleton loaders for charts (circular placeholder)
- Skeleton rows for table

### Error State

- Toast notification on API error
- Retry button per section (not full page reload)
- Stale data display with "Ultimo aggiornamento: X minuti fa" label

---

## Acceptance Criteria

### Functional

- [ ] **AC-1**: Editor navigates to `/dashboard/my-ai-usage` and sees summary cards for today, 7-day, and 30-day periods
- [ ] **AC-2**: Summary cards display request count, total tokens, and cost estimate for each period
- [ ] **AC-3**: Daily request bar chart shows 30 days of data with correct counts
- [ ] **AC-4**: Model distribution donut chart shows breakdown with percentages
- [ ] **AC-5**: Strategy distribution donut chart shows Fast/Balanced/Premium/HybridRAG breakdown
- [ ] **AC-6**: Recent requests table shows individual requests from last 7 days only
- [ ] **AC-7**: Recent requests table is paginated (20 items per page)
- [ ] **AC-8**: All data is scoped to the authenticated user only — no other users' data visible
- [ ] **AC-9**: Users with "User" role cannot access the page (redirect to dashboard)
- [ ] **AC-10**: Empty state displays correctly when user has no AI usage history
- [ ] **AC-11**: Info banner explains the 7-day detail vs 30-day aggregate distinction

### Non-Functional

- [ ] **NFR-1**: Page loads in < 2 seconds for users with up to 10K requests in 30 days
- [ ] **NFR-2**: API responses are cached for 5 minutes (stale-while-revalidate pattern)
- [ ] **NFR-3**: Page is mobile responsive (single-column layout on mobile, charts stack vertically)
- [ ] **NFR-4**: Charts render without JavaScript errors on Chrome, Firefox, Safari, Edge
- [ ] **NFR-5**: Background aggregation job completes within 60 seconds for up to 100K daily records

### Security

- [ ] **SEC-1**: API endpoints enforce Editor role minimum via middleware
- [ ] **SEC-2**: Backend filters by `UserId = currentUser` — no parameter allows querying other users
- [ ] **SEC-3**: Recent requests endpoint does not expose `SessionId`, `ErrorMessage`, or prompt content
- [ ] **SEC-4**: Cost data shows actual values (not estimates) — same data visible in admin analytics

---

## Specification by Example (Given/When/Then)

### Scenario 1: Editor views usage summary
```
Given: Editor "alice" has made 12 AI requests today, 87 in last 7 days, 342 in last 30 days
When: Alice navigates to /dashboard/my-ai-usage
Then: She sees three summary cards with correct counts
And: Token totals and cost estimates match her actual usage
```

### Scenario 2: Editor with no usage history
```
Given: Editor "bob" was just promoted to Editor role and has never used AI features
When: Bob navigates to /dashboard/my-ai-usage
Then: Summary cards show 0 for all values
And: Charts show "Nessun dato disponibile" placeholder
And: Recent requests table shows empty state message
And: No error is displayed
```

### Scenario 3: User role denied access
```
Given: "charlie" has "User" role (not Editor)
When: Charlie tries to navigate to /dashboard/my-ai-usage
Then: He is redirected to /dashboard with "Accesso non autorizzato" message
```

### Scenario 4: Recent requests limited to 7 days
```
Given: Editor "alice" has requests from today and from 10 days ago
When: She views the recent requests table
Then: Only today's requests appear (not the 10-day-old ones)
And: Info banner explains "I dettagli individuali sono disponibili per gli ultimi 7 giorni"
And: 30-day summary cards still include ALL 30 days of aggregated data
```

### Scenario 5: Data isolation between users
```
Given: Editor "alice" and Editor "bob" both use AI features
When: Alice views /dashboard/my-ai-usage
Then: She sees ONLY her own request counts, costs, and history
And: Bob's usage is not visible in any widget
```

### Scenario 6: Today's live data
```
Given: Editor "alice" sends an AI request at 14:22
When: She refreshes /dashboard/my-ai-usage at 14:23
Then: Today's summary card reflects the new request (live query, not cached)
And: Daily chart shows today's bar updated
And: Recent requests table shows the 14:22 request
```

---

## Dependencies

- **Aggregation job** must run BEFORE pseudonymization job (1:30 AM < 2:00 AM)
- **recharts** or equivalent charting library in `apps/web/`
- **UserAiUsageDailySummary** migration must be applied before aggregation job starts
- No dependency on Epic F completion — aggregation table is independent

## Estimated Effort

| Component | Effort |
|-----------|--------|
| Domain entity + migration | 2h |
| Aggregation background job | 4h |
| 4 CQRS queries + handlers | 6h |
| 4 API endpoints + routing | 3h |
| Frontend page + 5 components | 8h |
| Tests (unit + integration) | 6h |
| **Total** | **~29h** |
