# Enterprise Admin Dashboard - Specification

> **Version**: 1.0 | **Date**: 2026-02-05 | **Status**: Draft

---

## Executive Summary

**7 sections** | **15 domains** | Multi-tier management | Full audit | Advanced simulations

### Key Features

- 🔐 3 admin roles (SuperAdmin, Admin, Editor)
- 📊 Real-time monitoring (intelligent polling)
- 💰 Financial ledger (manual + automatic)
- 🤖 AI Lab (create & test agents)
- 🧪 Simulation tools (resource forecasting)
- 🔍 Complete audit log (compliance)
- ⚡ Batch job management (async tasks)

---

## Layout Structure

```
┌──────────────────────────────────────────────────────────┐
│  MeepleAI Admin   [Refresh] [Alerts: 3] [Audit] [User]  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────┐  Content Area with Tabs                   │
│  │ Overview │  ┌────────────────────────────────┐       │
│  │ Resources│  │ [Dashboard] [Alerts] [Quick]  │       │
│  │ Ops      │  ├────────────────────────────────┤       │
│  │ AI Lab   │  │ Tab content (cards, charts)   │       │
│  │ Users    │  └────────────────────────────────┘       │
│  │ Business │                                            │
│  │ Sims     │  Click list item → Detail page            │
│  └──────────┘                                            │
│   Vertical Sidebar                                       │
└──────────────────────────────────────────────────────────┘
```

---

## Tab Sections (7)

### 1. 📊 Overview & Monitoring

**Tabs**: Dashboard | Alerts | Quick Actions

**Dashboard**: 8 KPI cards (Users +156, Games +42, Proposals 3 urgent, Chats +180, Token € €450/€1000, DB 2.3GB/10GB, Cache 94.2%, Alerts 3 warning) | System Health Grid (6 services) | Recent Audit Log (20) | Pending Approvals

**Alerts**: Rules config | Active alerts (severity filters) | History (7 days) | Notification settings

**Quick Actions**: Cards with pending badges | Favorites (customizable) | Recent history

---

### 2. 💰 Resources & Infrastructure

**Tabs**: Tokens | Database | Cache | Vectors | Services

**Tokens**:
- OpenRouter: €450/€1000 (45%), 12 days to depletion
- Graph: 7/30 days consumption
- Per tier: Free (10K/month, 2.3M total, 2500 users) | Basic (50K, 1.2M, 300) | Pro (200K, 800K, 45) | Enterprise (∞, 1.5M, 2)
- Actions: [Adjust Limits] [Add Credits] [Export]

**Database**: 2.3GB/10GB, growth (7/30/90d), top tables, slow queries | Actions: [VACUUM] [Analyze] [Cleanup Job]

**Cache**: Redis (memory, hit rate, keys), hot keys, evictions | Actions: [Clear All] [Clear Pattern] [Key Details]

**Vectors**: Qdrant (collections, count/agent, memory, index health) | Actions: [Rebuild] [Optimize] [Export]

**Services**: Status dashboard (6 services), latency graphs, uptime % | Actions: [Restart] (L2) [View Logs] [Health Check]

---

### 3. ⚙️ Operations & Admin Tools

**Tabs**: Services | Cache | Email | Impersonate | Batch Jobs

**Email**: Inbox (system), Sent (audit), Templates | Actions: [Read] [Send Test] [Configure SMTP]

**Impersonate**: User search, recent impersonations, active sessions | Actions: [Impersonate] (L2) [End] [View Chat History]

**Batch Jobs**: Queue table (ID, Type, Status, Progress, Created), Job details (params, logs, result/error) | Actions: [Create] [Cancel] [Retry] [Delete]

---

### 4. 🤖 AI Platform

**Tabs**: AI Lab | Agents | Models | Chat Analytics | PDF Analytics

**AI Lab**:
- **Agent Builder**: Create form (name, type, model, prompt, tools) | Templates gallery | Import/Export JSON
- **Playground**: Chat interface | Debug panel (tokens, latency, RAG context) | Test scenarios
- **Strategy Editor**: Visual pipeline (drag-drop) | RAG config (hybrid/semantic/keyword) | Retrieval params (top-k, threshold, reranker) | Prompt template (Monaco editor)
- **A/B Testing**: Side-by-side | Performance metrics | Quality scoring

**Agents**: Catalog (list/grid), Usage stats per agent (calls/day 30d graph, avg tokens, success rate, avg latency, cost €, last used) | Actions: [Edit] [Clone] [Delete] (L1) [Export Stats]

**Models**: Performance comparison (GPT-4, GPT-4o, Claude 3.5) → Latency, cost, quality | Usage breakdown (% per model) | Cost per model (€)

**Chat Analytics**: Total conversations graph | Avg messages/conv | Topic clustering (ML) | Sentiment (pos/neu/neg) | User satisfaction (stars) | Actions: [Analyze Conversation] [Export]

**PDF Analytics**: Total uploaded trend | Avg processing time | Success/failure rate | Storage breakdown (per game/user) | OCR quality | Actions: [View Failed] [Reprocess] [Export Report]

---

### 5. 👥 Users & Content

**Tabs**: Users | Shared Library | Feature Flags | User Limits

**Users**: Table (Email, Tier, Registration, Last Login, Token Usage, Status) | Bulk actions | Per user: [View Details] [Change Tier] [Block] (L1) [Impersonate] (L2) [View Tokens] [Reset Password]

**Shared Library**: Games catalog (grid/list) | Filters (status, category, source) | Bulk ops | Import queue (BGG) | Actions: [Add] [Edit] [Import BGG] [Delete] (L2)

**Feature Flags**: Table with Global, User Role, Editor, Admin columns | Actions: [Toggle Global] (L2) [Set Role Override] [View History]

**User Limits**: Per tier table (Collection Size, PDF Uploads/month, Agents Created, Messages/day) | Actions: [Edit Tier Limit] (L1) [Reset Defaults] [Apply] (L2)

---

### 6. 💼 Business & Analytics

**Tabs**: Usage Stats | Financial Ledger | Reports

**Usage Stats**: DAU/MAU, Sessions (avg duration, bounce), Feature adoption (%), Retention (cohorts), Geo distribution | Charts: User growth (7/30/90d), Feature funnel, Peak hours heatmap

**Financial Ledger**:
- **Automatic** (read-only): Token costs, Subscription payments
- **Manual**: Form (Date, Type, Amount, Category, Description) | Categories: Infrastructure, Marketing, Support, Other
- **Dashboard**: Current month balance, Revenue vs Costs (12 months), Category breakdown, Profit margin
- Actions: [Add Entry] [Edit] (L1) [Delete] (L2) [Export PDF/CSV/Excel]

**Reports**: Templates (Monthly Summary, Quarterly, Annual, Custom Range) | Actions: [Generate] [Schedule Recurring] [Export All]

---

### 7. 🧪 Simulations & Forecasting

**Tabs**: Cost Calculator | Resource Forecast | Batch Jobs

**Cost Calculator**:
```
Input: Agent Type, Model, Strategy, Avg Messages, Context, Users
Output:
  Per Request: Input tokens ~2500, Output ~800, Cost €0.045
  Monthly (100 users, 5 msg/day): 15K requests, 49.5M tokens, €675 total, €6.75/user
  ⚠️ Exceeds budget (€450)

Saved Scenarios: List | Compare | Export spreadsheet
```

**Resource Forecast**:
```
Growth Assumptions: 2,847 users +5%/month, 12 months
  Usage: 3 msg/user/day, 2 PDF/month, 1 agent, 25 games

Projection:
  Users: 2,847 → 5,113 (+79%)
  Database: 2.3GB → 8.7GB → ⚠️ Upgrade M9
  Tokens: €450 → €1,200 → ⚠️ Negotiate volume
  Cache: 450MB → 980MB → ✅ OK (<2GB)
  Qdrant: 1.2M → 4.8M vectors → ✅ OK (<10M)

[Export] [Save Scenario] [Schedule Review]
```

---

## Security & Audit

### Admin Roles

| Role | Permissions |
|------|-------------|
| **SuperAdmin** | Full access, manage admins, modify global flags |
| **Admin** | Operations, monitoring, users, NO global flags |
| **Editor** | Content (games, PDFs), limited analytics, NO ops |

### Confirmation Levels

**Level 1**: Warning modal → [Cancel] [Confirm]
**Level 2**: Critical action → Type "CONFIRM" → [Cancel] [Confirm]

### Audit Log

```typescript
{
  id: "audit_abc123",
  timestamp: "2026-02-05T14:30:00Z",
  adminUser: { id, email, role },
  action: "ServiceRestart",
  target: "API Backend",
  result: "Success",
  metadata: { confirmationMethod: "Level2", downtime: "3.2s", affectedUsers: 45 }
}
```

**UI**: Real-time log (WebSocket/SSE), Filters (admin, action, date, result), Export (CSV/Excel), Retention: 90 days

---

## Key Data Models

### Financial Ledger

```typescript
interface LedgerEntry {
  id: string; date: DateTime;
  type: 'Income' | 'Expense';
  category: 'Subscription' | 'TokenUsage' | 'Infrastructure' | 'Marketing' | 'Other';
  amount: number; // Euro
  source: 'Automatic' | 'Manual';
  description: string;
  metadata?: { tokenCount?, service?, invoiceUrl?, paymentMethod? };
  audit: { createdBy, createdAt, updatedBy?, updatedAt? };
}
```

### Token Tier

```typescript
interface TokenTier {
  name: 'Free' | 'Basic' | 'Pro' | 'Enterprise';
  limits: { tokensPerMonth, tokensPerDay, messagesPerDay, maxCollectionSize, maxPdfUploads, maxAgents };
  pricing: { monthlyFee, costPerExtraToken? };
}

interface UserTokenUsage {
  userId; tierId;
  currentMonth: { tokensUsed, messagesCount, cost, lastReset };
  status: { isBlocked, isNearLimit, warnings: DateTime[] };
  history: { month, tokensUsed, cost }[];
}
```

### Agent Definition

```typescript
interface AgentDefinition {
  id; name; type: 'FAQ'|'Rules'|'Strategy'|'Recommendation'|'Custom';
  status: 'Draft'|'Testing'|'Published'|'Archived';
  config: { model, systemPrompt, temperature, maxTokens, tools };
  strategy: { type: 'Hybrid'|'Semantic'|'Keyword', retrievalParams, promptTemplate };
  stats: { totalCalls, avgTokens, avgLatency, successRate, cost, lastUsed };
  metadata: { createdBy, createdAt, version };
}
```

---

## API Endpoints (New)

### Resources
```
GET  /admin/resources/tokens          # Balance + usage
GET  /admin/resources/database        # DB metrics
GET  /admin/resources/cache           # Redis
GET  /admin/resources/vectors         # Qdrant
POST /admin/resources/tokens/add      # Add credits
```

### Operations
```
POST /admin/operations/service/restart  # L2
POST /admin/operations/cache/clear      # L2
POST /admin/operations/impersonate      # L2
DELETE /admin/operations/impersonate
```

### AI Platform
```
GET  /admin/ai/agents                   # List
POST /admin/ai/agents                   # Create
PUT  /admin/ai/agents/{id}              # Update
GET  /admin/ai/agents/{id}/stats        # Usage
POST /admin/ai/agents/{id}/test         # Test
GET  /admin/ai/analytics/chats          # Chat analytics
GET  /admin/ai/analytics/pdf            # PDF analytics
```

### Business
```
GET  /admin/business/usage              # App stats
GET  /admin/business/ledger             # Entries
POST /admin/business/ledger             # Add manual
PUT  /admin/business/ledger/{id}        # Edit
DELETE /admin/business/ledger/{id}      # Delete (L2)
GET  /admin/business/ledger/export      # PDF/CSV/Excel
```

### User Management
```
GET  /admin/users                       # List
PUT  /admin/users/{id}/tier             # Change
POST /admin/users/{id}/block            # Block (L1)
GET  /admin/users/{id}/token-usage      # Details
```

### Feature Flags & Limits
```
GET  /admin/flags                       # List
PUT  /admin/flags/{key}                 # Update (L2)
GET  /admin/limits                      # List
PUT  /admin/limits/{key}                # Update (L2)
```

### Simulations
```
POST /admin/simulations/agent-cost      # Calculate
POST /admin/simulations/resource-forecast # Run
GET  /admin/simulations/scenarios       # Saved
POST /admin/simulations/scenarios       # Save
```

### Batch Jobs
```
GET  /admin/batch-jobs                  # List
POST /admin/batch-jobs                  # Create
POST /admin/batch-jobs/{id}/cancel      # Cancel
POST /admin/batch-jobs/{id}/retry       # Retry
```

---

## Implementation Roadmap

### Phase 1: Core Dashboard (3 weeks)

**Week 1-2**: Infrastructure
- Layout (VerticalSidebar, TabContent)
- Confirmation modals (L1 & L2)
- Audit log (DB table, write interceptor, viewer)
- Admin role system (permission checks)

**Week 2-3**: Resources Tab
- Token dashboard (€ balance, consumption)
- Database metrics (size, growth)
- Cache stats (Redis)
- Qdrant vectors (collection stats)
- Service control panel

**Deliverables**: Working sidebar, Overview + Resources tabs, Confirmation system, Audit log

---

### Phase 2: User & Content (2 weeks)

**Week 1**: Users
- User table, Tier management, Token usage/user, Block/Unblock, Impersonate

**Week 2**: Content & Flags
- Shared library, Feature flags UI (global + role), User limits (per tier), Bulk ops

**Deliverables**: Complete user management, Feature flags, Tier limits

---

### Phase 3: AI Platform (4 weeks)

**Week 1-2**: AI Lab
- Agent builder (create/edit), Playground (test chat), Strategy editor (RAG config), Templates gallery

**Week 3**: Analytics
- Agent stats, Chat analytics (clustering, sentiment), Model comparison

**Week 4**: PDF Analytics
- PDF stats, Processing metrics, Storage breakdown, Failed PDF viewer

**Deliverables**: Functional AI Lab, Complete AI analytics, PDF analytics

---

### Phase 4: Business & Simulations (3 weeks)

**Week 1**: Business
- App usage (DAU/MAU, retention), Feature funnel, Geo distribution

**Week 2**: Ledger
- Entry table (auto + manual), Add/Edit/Delete, Dashboard (revenue vs costs), Export (PDF/CSV/Excel)

**Week 3**: Simulations
- Agent cost calculator, Resource forecasting, Batch job management, Scenario save/compare

**Deliverables**: Business analytics, Financial ledger, Simulation tools

---

## Summary Timeline

| Phase | Scope | Duration | Cumulative |
|-------|-------|----------|------------|
| 1 | Core + Resources | 3 weeks | 3 weeks |
| 2 | Users + Content | 2 weeks | 5 weeks |
| 3 | AI Platform | 4 weeks | 9 weeks |
| 4 | Business + Sims | 3 weeks | **12 weeks** |

**MVP (Phase 1)**: 3 weeks → Overview KPIs, Resources management, Operations panel, Audit log

---

**Next Steps**: Begin Phase 1 implementation | Detail specific section | Create wireframes | Generate DB migrations
