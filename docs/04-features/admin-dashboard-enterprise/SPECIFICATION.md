# Enterprise Admin Dashboard - Specification

> **Version**: 1.0
> **Date**: 2026-02-05
> **Status**: Draft
> **Scope**: Enterprise-grade admin control panel for MeepleAI

---

## Executive Summary

Dashboard amministrativa enterprise con **7 sezioni principali**, **15 domini funzionali**, gestione multi-tier, audit completo, e strumenti di simulazione avanzati.

### Key Features
- 🔐 **3 ruoli admin** (SuperAdmin, Admin, Editor)
- 📊 **Real-time monitoring** con polling intelligente
- 💰 **Financial ledger** ibrido (manuale + automatico)
- 🤖 **AI Lab** per creazione e testing agenti
- 🧪 **Simulation tools** per forecasting risorse
- 🔍 **Audit log completo** per compliance
- ⚡ **Batch job management** per task async

---

## Architecture Overview

### Layout Structure

```
┌──────────────────────────────────────────────────────────────────┐
│  MeepleAI Admin           [Refresh] [Alerts: 3] [Audit] [User]  │ Header
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌────────────┐                                                  │
│  │            │  Content Area with Horizontal Tabs               │
│  │ 📊 Overview│  ┌──────────────────────────────────────────┐   │
│  │            │  │ [Dashboard] [Alerts] [Quick Actions]    │   │
│  ├────────────┤  ├──────────────────────────────────────────┤   │
│  │            │  │                                          │   │
│  │ 💰 Resources│  │  Tab content with cards, charts, etc.   │   │
│  │            │  │                                          │   │
│  ├────────────┤  └──────────────────────────────────────────┘   │
│  │            │                                                  │
│  │ ⚙️  Ops    │  Each item in list/grid →                       │
│  │            │  Click → Detail page                            │
│  ├────────────┤                                                  │
│  │            │                                                  │
│  │ 🤖 AI Lab  │                                                  │
│  │            │                                                  │
│  ├────────────┤                                                  │
│  │            │                                                  │
│  │ 👥 Users   │                                                  │
│  │            │                                                  │
│  ├────────────┤                                                  │
│  │            │                                                  │
│  │ 💼 Business│                                                  │
│  │            │                                                  │
│  ├────────────┤                                                  │
│  │            │                                                  │
│  │ 🧪 Sims    │                                                  │
│  │            │                                                  │
│  └────────────┘                                                  │
│      ↑                                                            │
│   Vertical Sidebar                                               │
└──────────────────────────────────────────────────────────────────┘
```

---

## Tab Sections (7 Main)

### Section 1: 📊 Overview & Monitoring

**Tabs**: `Dashboard` | `Alerts` | `Quick Actions`

#### 1.1 Dashboard Tab
**Features**:
- 8 KPI cards con trend (vs current 4)
  - Utenti Totali (+156 questo mese)
  - Giochi Catalogo (+42 questa settimana)
  - Proposte Pending (3 urgenti)
  - Chat AI Oggi (+180 vs ieri)
  - **Token € Balance** (€450 / €1000) - NEW
  - **DB Storage** (2.3GB / 10GB, +50MB/day) - NEW
  - **Cache Hit Rate** (94.2%, +2.1% vs ieri) - NEW
  - **Active Alerts** (3 warning, 0 critical) - NEW

- System Health Grid (6 services)
- Recent Audit Log (last 20 actions) - NEW
- Pending Approvals Queue

#### 1.2 Alerts Tab
**Features**:
- Alert rules configuration
- Active alerts (filtri per severity)
- Alert history (last 7 days)
- Notification settings

#### 1.3 Quick Actions Tab
**Features**:
- Action cards con badge pending
- Favorites (customizable per admin)
- Recent actions history

---

### Section 2: 💰 Resources & Infrastructure

**Tabs**: `Tokens` | `Database` | `Cache` | `Vectors` | `Services`

#### 2.1 Tokens Tab
**Features**:
- **OpenRouter Balance**:
  - Current: €450 / €1000 (45%)
  - Proiezione esaurimento: 12 giorni
  - Grafico consumo (7/30 giorni)
  - Top consumers (per tier, per feature)

- **Token Pool per Tier**:
  ```
  Tier     | Limit/month | Current Usage | Users
  ─────────┼─────────────┼───────────────┼──────
  Free     | 10K tokens  | 2.3M total   | 2,500
  Basic    | 50K tokens  | 1.2M total   | 300
  Pro      | 200K tokens | 800K total   | 45
  Enterprise| Unlimited  | 1.5M total   | 2
  ```

- **Actions**:
  - [Adjust Tier Limits]
  - [Add € Credits]
  - [View Detailed Breakdown]
  - [Export Usage Report]

#### 2.2 Database Tab
**Features**:
- Storage usage (2.3GB / 10GB)
- Growth trend (7/30/90 giorni)
- Top tables by size
- Slow queries log
- **Actions**:
  - [Run VACUUM] (Level 1 confirm)
  - [Analyze Tables]
  - [Schedule Cleanup Job]

#### 2.3 Cache Tab
**Features**:
- Redis metrics (memory, hit rate, keys count)
- Hot keys (most accessed)
- Eviction stats
- **Actions**:
  - [Clear All Cache] (Level 2 confirm)
  - [Clear Pattern] (Level 1 confirm)
  - [View Key Details]

#### 2.4 Vectors Tab
**Features**:
- Qdrant collection stats
- Vector count per agent
- Memory usage
- Index health
- **Actions**:
  - [Rebuild Index] (Level 2 confirm)
  - [Optimize Collections]
  - [Export Metadata]

#### 2.5 Services Tab
**Features**:
- Service status dashboard (API, DB, Redis, Qdrant, AI, BGG)
- Latency graphs
- Uptime percentage
- **Actions**:
  - [Restart Service] (Level 2 confirm + require "CONFIRM" typing)
  - [View Logs]
  - [Health Check]

---

### Section 3: ⚙️ Operations & Admin Tools

**Tabs**: `Services` | `Cache` | `Email` | `Impersonate` | `Batch Jobs`

#### 3.1 Services Tab
(Vedi Section 2.5 - duplicate or link?)

#### 3.2 Cache Management Tab
(Vedi Section 2.3 - duplicate or link?)

#### 3.3 Email Tab
**Features**:
- Inbox (system emails, notifications)
- Sent emails (audit trail)
- Email templates
- **Actions**:
  - [Read Email]
  - [Send Test Email]
  - [Configure SMTP]

#### 3.4 Impersonate Tab
**Features**:
- User search
- Recent impersonations (audit log)
- Active impersonate sessions
- **Actions**:
  - [Impersonate User] (Level 2 confirm)
  - [End Impersonation]
  - [View User's Chat History]

#### 3.5 Batch Jobs Tab
**Features**:
- Job queue (status: Queued, Running, Completed, Failed)
- Job types: ResourceForecast, CostAnalysis, DataCleanup, BggSync
- Progress indicators
- Job history (last 50)
- **Actions**:
  - [Create New Job]
  - [Cancel Queued Job]
  - [Retry Failed Job]
  - [Schedule Recurring Job]

---

### Section 4: 🤖 AI Platform

**Tabs**: `AI Lab` | `Agents` | `Models` | `Chat Analytics` | `PDF Analytics`

#### 4.1 AI Lab Tab
**Features**:
- **Agent Builder**:
  - Create new agent (form: name, type, model, system prompt, tools)
  - Agent templates gallery
  - Import/Export JSON config

- **Agent Playground**:
  - Chat interface per testare agent
  - Debug panel (token count, latency, RAG context)
  - Test scenarios predefiniti

- **Strategy Editor**:
  - Visual pipeline builder (drag-drop)
  - RAG strategy config (hybrid, semantic, keyword)
  - Retrieval params (top-k, threshold, reranker)
  - Prompt template editor (Monaco editor)

- **A/B Testing**:
  - Side-by-side comparison
  - Performance metrics
  - Quality scoring

#### 4.2 Agents Tab
**Features**:
- Agent catalog (lista/griglia)
- Usage stats per agent:
  - Calls/day (graph 30 days)
  - Avg tokens per request
  - Success rate
  - Avg response time
  - Cost breakdown (€ per model)
- **Actions**:
  - [Edit Agent]
  - [Clone Agent]
  - [Delete Agent] (Level 1 confirm)
  - [Export Stats]

#### 4.3 Models Tab
**Features**:
- Model performance comparison
  - GPT-4, GPT-4o, Claude 3.5 Sonnet, etc.
  - Latency, cost, quality metrics
- Model usage breakdown (% requests per model)
- Cost per model (€)

#### 4.4 Chat Analytics Tab
**Features**:
- Total conversations (graph)
- Avg messages per conversation
- Topic clustering (ML-based)
- Sentiment analysis (positive/neutral/negative)
- User satisfaction (star ratings)
- **Actions**:
  - [Analyze Conversation] (search by user/game)
  - [Export Analytics]

#### 4.5 PDF Analytics Tab
**Features**:
- Total PDFs uploaded (trend)
- Avg processing time
- Success/failure rate
- Storage breakdown (per game, per user)
- OCR quality metrics
- **Actions**:
  - [View Failed PDFs]
  - [Reprocess PDF]
  - [Export Report]

---

### Section 5: 👥 Users & Content

**Tabs**: `Users` | `Shared Library` | `Feature Flags` | `User Limits`

#### 5.1 Users Tab
**Features**:
- User table (search, filter, sort)
  - Columns: Email, Tier, Registration, Last Login, Token Usage, Status
- Bulk actions (select multiple)
- **Actions per user**:
  - [View Details] → Detail page
  - [Change Tier]
  - [Block/Unblock] (Level 1 confirm)
  - [Impersonate] (Level 2 confirm)
  - [View Token Usage]
  - [Reset Password]

#### 5.2 Shared Library Tab
**Features**:
- Games catalog (grid/list view)
- Filters (status, category, source)
- Bulk operations
- Import queue (BGG)
- **Actions**:
  - [Add Game]
  - [Edit Game]
  - [Bulk Import from BGG]
  - [Delete Game] (Level 2 confirm)

#### 5.3 Feature Flags Tab
**Features**:
- Feature flag table:
  ```
  Feature          | Global | User Role | Editor | Admin
  ─────────────────┼────────┼───────────┼────────┼──────
  PDF Upload       | ON     | ON        | ON     | ON
  Agent Creation   | ON     | OFF       | ON     | ON
  Private Games    | ON     | ON (Beta) | ON     | ON
  ```

- **Actions**:
  - [Toggle Global] (Level 2 confirm)
  - [Set Role Override]
  - [View Change History]

#### 5.4 User Limits Tab
**Features**:
- Limit configuration per tier:
  ```
  Limit                | Free | Basic | Pro    | Enterprise
  ─────────────────────┼──────┼───────┼────────┼───────────
  Collection Size      | 20   | 50    | 200    | Unlimited
  PDF Uploads/month    | 5    | 20    | 100    | Unlimited
  Agents Created       | 1    | 3     | 10     | Unlimited
  Messages/day         | 10   | 50    | 200    | Unlimited
  ```

- **Actions**:
  - [Edit Tier Limit] (Level 1 confirm)
  - [Reset to Defaults]
  - [Apply Changes] (Level 2 confirm)

---

### Section 6: 💼 Business & Analytics

**Tabs**: `Usage Stats` | `Financial Ledger` | `Reports`

#### 6.1 Usage Stats Tab
**Features**:
- App usage metrics:
  - DAU / MAU (Daily/Monthly Active Users)
  - Sessions (avg duration, bounce rate)
  - Feature adoption (% users using each feature)
  - Retention (cohort analysis)
  - Geo distribution (if tracked)

- Charts:
  - User growth (7/30/90 days)
  - Feature usage funnel
  - Peak hours heatmap

#### 6.2 Financial Ledger Tab
**Features**:
- **Automatic Entries** (read-only):
  - Token usage costs (da OpenRouter/Anthropic API)
  - Subscription payments (da Stripe webhook)

- **Manual Entries** (editable):
  - Form: Date, Type (Income/Expense), Amount, Category, Description
  - Categories: Infrastructure, Marketing, Support, Other

- **Dashboard**:
  - Current month balance
  - Revenue vs Costs graph (12 months)
  - Breakdown per category
  - Profit margin

- **Actions**:
  - [Add Manual Entry]
  - [Edit Entry] (Level 1 confirm)
  - [Delete Entry] (Level 2 confirm)
  - [Export Ledger] (PDF, CSV, Excel)

#### 6.3 Reports Tab
**Features**:
- Report templates:
  - Monthly Summary (users, revenue, costs)
  - Quarterly Report
  - Annual Report
  - Custom Date Range

- **Actions**:
  - [Generate Report]
  - [Schedule Recurring Report]
  - [Export All Reports]

---

### Section 7: 🧪 Simulations & Forecasting

**Tabs**: `Cost Calculator` | `Resource Forecast` | `Batch Jobs`

#### 7.1 Cost Calculator Tab
**Features**:
- **Agent Cost Simulator**:
  ```
  Input:
  ┌─────────────────────────────────────┐
  │ Agent Type:       [FAQ ▼]          │
  │ Model:            [GPT-4o ▼]       │
  │ Strategy:         [Hybrid RAG ▼]   │
  │ Avg Messages:     [5]              │
  │ Context Size:     [Medium ▼]       │
  │ Users Estimate:   [100]            │
  │                                     │
  │ [Calculate] [Save Scenario]        │
  └─────────────────────────────────────┘

  Output:
  ┌─────────────────────────────────────┐
  │ Per Request:                        │
  │ • Input tokens:   ~2,500           │
  │ • Output tokens:  ~800             │
  │ • Cost:           €0.045           │
  │                                     │
  │ Monthly (100 users, 5 msg/day):    │
  │ • Total requests: ~15,000          │
  │ • Total tokens:   ~49.5M           │
  │ • Total cost:     €675/month       │
  │ • Per user:       €6.75/month      │
  │                                     │
  │ ⚠️ Warning: Exceeds current        │
  │    monthly budget (€450)           │
  │                                     │
  │ [Export] [Run Batch Simulation]    │
  └─────────────────────────────────────┘
  ```

- **Saved Scenarios**:
  - List of saved calculations
  - Compare scenarios
  - Export to spreadsheet

#### 7.2 Resource Forecast Tab
**Features**:
- **Scenario Builder**:
  ```
  Growth Assumptions:
  ┌─────────────────────────────────────┐
  │ Current Users:      2,847          │
  │ Growth Rate:        +5% /month     │
  │ Forecast Period:    [12 months ▼]  │
  │                                     │
  │ Usage Patterns:                     │
  │ • Messages/user/day:    [3]        │
  │ • PDF uploads/user:     [2/month]  │
  │ • Agents/user:          [1]        │
  │ • Collection size/user: [25 games] │
  │                                     │
  │ [Run Forecast]                      │
  └─────────────────────────────────────┘

  Forecast Results:
  ┌─────────────────────────────────────┐
  │ 12-Month Projection:                │
  │                                     │
  │ Users:  2,847 → 5,113 (+79%)       │
  │                                     │
  │ Database:                           │
  │ • Current: 2.3GB → 12mo: 8.7GB     │
  │ • Action: ⚠️ Upgrade tier M9        │
  │                                     │
  │ Token Budget:                       │
  │ • Current: €450 → 12mo: €1,200     │
  │ • Action: ⚠️ Negotiate volume deal  │
  │                                     │
  │ Cache:                              │
  │ • Current: 450MB → 12mo: 980MB     │
  │ • Action: ✅ OK (under 2GB limit)   │
  │                                     │
  │ Qdrant:                             │
  │ • Current: 1.2M → 12mo: 4.8M vec   │
  │ • Action: ✅ OK (under 10M limit)   │
  │                                     │
  │ [Export Report] [Save Scenario]     │
  │ [Schedule Monthly Review]           │
  └─────────────────────────────────────┘
  ```

#### 7.3 Batch Jobs Tab
**Features**:
- Job queue table:
  ```
  ID    | Type              | Status    | Progress | Created
  ──────┼───────────────────┼───────────┼──────────┼─────────
  #1234 | ResourceForecast  | Running   | 67%      | 2h ago
  #1233 | CostAnalysis      | Completed | 100%     | 5h ago
  #1232 | BggSync           | Failed    | 45%      | Yesterday
  #1231 | DataCleanup       | Queued    | 0%       | Yesterday
  ```

- Job details (click row):
  - Input parameters
  - Logs (real-time if running)
  - Result (if completed)
  - Error details (if failed)

- **Actions**:
  - [Create Job]
  - [Cancel Job] (if queued/running)
  - [Retry Job] (if failed)
  - [Delete Job] (Level 1 confirm)

---

## Security & Audit

### Admin Roles

| Role | Permissions |
|------|-------------|
| **SuperAdmin** | Full access, can manage other admins, modify global feature flags |
| **Admin** | Operations, monitoring, user management, NO global feature flags |
| **Editor** | Content management (games, PDFs), limited analytics, NO operations |

### Confirmation Levels

**Level 1: Warning Modal**
```
┌──────────────────────────────────┐
│ ⚠️  Attenzione                   │
│                                  │
│ Stai per pulire tutta la cache.  │
│ Questa operazione potrebbe       │
│ rallentare temporaneamente       │
│ l'applicazione.                  │
│                                  │
│ [Annulla]  [Conferma]           │
└──────────────────────────────────┘
```

**Level 2: Critical Action**
```
┌──────────────────────────────────┐
│ 🚨 Azione Critica                │
│                                  │
│ Stai per riavviare il servizio   │
│ API Backend. Tutti gli utenti    │
│ connessi verranno disconnessi.   │
│                                  │
│ Digita CONFIRM per procedere:    │
│ [____________]                   │
│                                  │
│ [Annulla]  [Conferma]           │
└──────────────────────────────────┘
```

### Audit Log

**Entry Structure**:
```typescript
{
  id: "audit_abc123",
  timestamp: "2026-02-05T14:30:00Z",
  adminUser: {
    id: "user_123",
    email: "admin@meepleai.com",
    role: "Admin"
  },
  action: "ServiceRestart",
  target: "API Backend",
  result: "Success",
  metadata: {
    confirmationMethod: "Level2",
    downtime: "3.2s",
    affectedUsers: 45
  }
}
```

**UI Features**:
- Real-time log (WebSocket or SSE)
- Filters (admin, action, date, result)
- Export (CSV, Excel)
- Retention: 90 giorni default (configurabile)

---

## Data Models

### 1. Financial Ledger

```typescript
interface LedgerEntry {
  id: string;
  date: DateTime;
  type: 'Income' | 'Expense';
  category: 'Subscription' | 'TokenUsage' | 'Infrastructure' | 'Marketing' | 'Other';
  amount: number; // Euro
  source: 'Automatic' | 'Manual';
  description: string;

  // Relazioni
  relatedUserId?: string; // per subscription
  relatedInvoiceId?: string;

  // Metadata
  metadata: {
    tokenCount?: number; // se TokenUsage
    service?: string; // "OpenRouter", "AWS S3"
    invoiceUrl?: string;
    paymentMethod?: string;
  };

  // Audit
  createdBy: string;
  createdAt: DateTime;
  updatedBy?: string;
  updatedAt?: DateTime;
}

// Aggregations
interface LedgerSummary {
  period: 'month' | 'quarter' | 'year';
  startDate: DateTime;
  endDate: DateTime;
  totalIncome: number;
  totalExpenses: number;
  netProfit: number;
  breakdownByCategory: Record<string, number>;
}
```

### 2. Token Tier Management

```typescript
interface TokenTier {
  id: string;
  name: 'Free' | 'Basic' | 'Pro' | 'Enterprise';
  limits: {
    tokensPerMonth: number;
    tokensPerDay: number;
    messagesPerDay: number;
    maxCollectionSize: number;
    maxPdfUploadsPerMonth: number;
    maxAgentsCreated: number;
  };
  pricing: {
    monthlyFee: number; // €
    costPerExtraToken?: number; // per overages
  };
}

interface UserTokenUsage {
  userId: string;
  tierId: string;
  currentMonth: {
    tokensUsed: number;
    messagesCount: number;
    cost: number;
    lastReset: DateTime;
  };
  status: {
    isBlocked: boolean;
    isNearLimit: boolean; // 80% of limit
    warnings: DateTime[]; // timestamp di warning inviati
  };
  history: {
    month: string; // "2026-01"
    tokensUsed: number;
    cost: number;
  }[];
}
```

### 3. Feature Flags

```typescript
interface FeatureFlag {
  id: string;
  key: string; // "PdfUpload", "AgentCreation", "PrivateGames"
  displayName: string;
  description: string;

  enabled: {
    global: boolean;
    roleOverrides: {
      User?: boolean;
      Editor?: boolean;
      Admin?: boolean;
    };
  };

  metadata: {
    createdAt: DateTime;
    createdBy: string;
    lastModified: DateTime;
    lastModifiedBy: string;
    changeHistory: {
      timestamp: DateTime;
      adminEmail: string;
      changes: Record<string, any>;
    }[];
  };
}

interface UserLimit {
  id: string;
  feature: string; // "CollectionSize", "PdfUploads", "AgentsCreated"
  displayName: string;

  limits: {
    globalDefault: number;
    tierOverrides: {
      Free?: number;
      Basic?: number;
      Pro?: number;
      Enterprise?: number;
    };
  };

  metadata: {
    lastModified: DateTime;
    lastModifiedBy: string;
  };
}
```

### 4. Batch Jobs

```typescript
interface BatchJob {
  id: string;
  type: 'ResourceForecast' | 'CostAnalysis' | 'DataCleanup' | 'BggSync' | 'AgentBenchmark';
  status: 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

  config: {
    parameters: Record<string, any>; // input specifico per job type
    scheduledAt?: DateTime; // per job ricorrenti
    recurrence?: string; // cron expression
  };

  execution: {
    startedAt?: DateTime;
    completedAt?: DateTime;
    duration?: number; // secondi
    progress: number; // 0-100
  };

  result?: {
    data: any; // risultato job-specific
    outputFileUrl?: string; // per report generati
    summary: string;
  };

  error?: {
    message: string;
    stack?: string;
    retryCount: number;
  };

  audit: {
    createdBy: string;
    createdAt: DateTime;
  };
}
```

### 5. Agent Definition (AI Lab)

```typescript
interface AgentDefinition {
  id: string;
  name: string;
  type: 'FAQ' | 'Rules' | 'Strategy' | 'Recommendation' | 'Custom';
  status: 'Draft' | 'Testing' | 'Published' | 'Archived';

  config: {
    model: string; // "gpt-4o", "claude-3-5-sonnet"
    systemPrompt: string;
    temperature: number;
    maxTokens: number;
    tools: string[]; // enabled tools
  };

  strategy: {
    type: 'Hybrid' | 'Semantic' | 'Keyword';
    retrievalParams: {
      topK: number;
      threshold: number;
      useReranker: boolean;
    };
    promptTemplate: string;
  };

  stats: {
    totalCalls: number;
    avgTokensPerCall: number;
    avgLatency: number;
    successRate: number;
    costToDate: number; // €
    lastUsed?: DateTime;
  };

  metadata: {
    createdBy: string;
    createdAt: DateTime;
    lastModified: DateTime;
    version: number;
  };
}
```

---

## Component Architecture

### Shared Components

#### ConfirmationModal
```typescript
interface ConfirmationModalProps {
  level: 1 | 2;
  title: string;
  message: string;
  actionLabel?: string;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
}
```

#### AuditLogViewer
```typescript
interface AuditLogViewerProps {
  filters?: {
    adminId?: string;
    action?: string;
    dateRange?: [Date, Date];
  };
  limit?: number;
  exportEnabled?: boolean;
}
```

#### ResourceUsageCard
```typescript
interface ResourceUsageCardProps {
  resource: 'Tokens' | 'Database' | 'Cache' | 'Vectors';
  current: number;
  limit: number;
  unit: string;
  trend: {
    value: number;
    label: string;
  };
  projectedDepletion?: string; // "12 giorni"
  topConsumers?: { label: string; value: number }[];
}
```

#### TierLimitTable
```typescript
interface TierLimitTableProps {
  limits: UserLimit[];
  editable: boolean;
  onEdit: (limitId: string, newValues: Record<string, number>) => void;
}
```

---

## API Endpoints (New)

### Resources
```
GET  /api/v1/admin/resources/tokens          # Token balance + usage
GET  /api/v1/admin/resources/database        # DB metrics
GET  /api/v1/admin/resources/cache           # Redis metrics
GET  /api/v1/admin/resources/vectors         # Qdrant metrics
POST /api/v1/admin/resources/tokens/add      # Add € credits
```

### Operations
```
POST /api/v1/admin/operations/service/restart  # Restart service (Level 2)
POST /api/v1/admin/operations/cache/clear      # Clear cache (Level 2)
GET  /api/v1/admin/operations/email            # List emails
POST /api/v1/admin/operations/impersonate      # Start impersonate (Level 2)
DELETE /api/v1/admin/operations/impersonate    # End impersonate
```

### AI Platform
```
GET  /api/v1/admin/ai/agents                   # List agents
POST /api/v1/admin/ai/agents                   # Create agent
PUT  /api/v1/admin/ai/agents/{id}              # Update agent
GET  /api/v1/admin/ai/agents/{id}/stats        # Agent usage stats
POST /api/v1/admin/ai/agents/{id}/test         # Test agent
GET  /api/v1/admin/ai/analytics/chats          # Chat analytics
GET  /api/v1/admin/ai/analytics/pdf            # PDF analytics
```

### Business
```
GET  /api/v1/admin/business/usage              # App usage stats
GET  /api/v1/admin/business/ledger             # Financial entries
POST /api/v1/admin/business/ledger             # Add manual entry
PUT  /api/v1/admin/business/ledger/{id}        # Edit entry
DELETE /api/v1/admin/business/ledger/{id}      # Delete entry (Level 2)
GET  /api/v1/admin/business/ledger/export      # Export (PDF/CSV/Excel)
GET  /api/v1/admin/business/reports/{type}     # Generate report
```

### User Management
```
GET  /api/v1/admin/users                       # List users
PUT  /api/v1/admin/users/{id}/tier             # Change tier
POST /api/v1/admin/users/{id}/block            # Block user (Level 1)
POST /api/v1/admin/users/{id}/unblock          # Unblock user
GET  /api/v1/admin/users/{id}/token-usage      # User token details
```

### Feature Flags
```
GET  /api/v1/admin/flags                       # List feature flags
PUT  /api/v1/admin/flags/{key}                 # Update flag (Level 2)
GET  /api/v1/admin/limits                      # List user limits
PUT  /api/v1/admin/limits/{key}                # Update limit (Level 2)
```

### Simulations
```
POST /api/v1/admin/simulations/agent-cost      # Calculate agent cost
POST /api/v1/admin/simulations/resource-forecast # Run forecast
GET  /api/v1/admin/simulations/scenarios       # Saved scenarios
POST /api/v1/admin/simulations/scenarios       # Save scenario
DELETE /api/v1/admin/simulations/scenarios/{id} # Delete scenario
```

### Batch Jobs
```
GET  /api/v1/admin/batch-jobs                  # List jobs
POST /api/v1/admin/batch-jobs                  # Create job
GET  /api/v1/admin/batch-jobs/{id}             # Job details
POST /api/v1/admin/batch-jobs/{id}/cancel      # Cancel job
POST /api/v1/admin/batch-jobs/{id}/retry       # Retry failed job
DELETE /api/v1/admin/batch-jobs/{id}           # Delete job
```

### Audit
```
GET  /api/v1/admin/audit-log                   # List audit entries
GET  /api/v1/admin/audit-log/export            # Export audit log
```

---

## Implementation Roadmap

### Phase 1: Core Dashboard (3 settimane)

**Week 1-2: Infrastructure**
- [ ] Layout components (VerticalSidebar, TabContent)
- [ ] Confirmation modal system (Level 1 & 2)
- [ ] Audit log backend (DB table, write interceptor)
- [ ] Audit log frontend (viewer component)
- [ ] Admin role system (backend permission checks)

**Week 2-3: Resources Tab**
- [ ] Token dashboard (€ balance, consumption)
- [ ] Database metrics (size, growth)
- [ ] Cache stats (Redis integration)
- [ ] Qdrant vectors (collection stats)
- [ ] Service control panel

**Deliverables**:
- ✅ Working sidebar navigation
- ✅ Overview + Resources tabs functional
- ✅ Confirmation system working
- ✅ Audit log tracking critical actions

---

### Phase 2: User & Content Management (2 settimane)

**Week 1: Users**
- [ ] User management table
- [ ] Tier management (change tier)
- [ ] Token usage per user
- [ ] Block/Unblock users
- [ ] Impersonate mode

**Week 2: Content & Flags**
- [ ] Shared library management
- [ ] Feature flags UI (toggle global + role)
- [ ] User limits configuration (per tier)
- [ ] Bulk operations

**Deliverables**:
- ✅ Complete user management
- ✅ Feature flags system
- ✅ Tier-based limits

---

### Phase 3: AI Platform (4 settimane)

**Week 1-2: AI Lab**
- [ ] Agent builder UI (create/edit form)
- [ ] Agent playground (test chat interface)
- [ ] Strategy editor (RAG config)
- [ ] Agent templates gallery

**Week 3: Analytics**
- [ ] Agent usage stats dashboard
- [ ] Chat analytics (topic clustering, sentiment)
- [ ] Model performance comparison

**Week 4: PDF Analytics**
- [ ] PDF stats dashboard
- [ ] Processing metrics
- [ ] Storage breakdown
- [ ] Failed PDF viewer

**Deliverables**:
- ✅ Functional AI Lab
- ✅ Complete AI analytics
- ✅ PDF analytics

---

### Phase 4: Business & Simulations (3 settimane)

**Week 1: Business Analytics**
- [ ] App usage stats (DAU/MAU, retention)
- [ ] Feature adoption funnel
- [ ] Geo distribution

**Week 2: Financial Ledger**
- [ ] Ledger entry table (auto + manual)
- [ ] Add/Edit/Delete manual entries
- [ ] Dashboard (revenue vs costs)
- [ ] Export (PDF, CSV, Excel)

**Week 3: Simulations**
- [ ] Agent cost calculator
- [ ] Resource forecasting simulator
- [ ] Batch job management
- [ ] Scenario save/compare

**Deliverables**:
- ✅ Complete business analytics
- ✅ Financial ledger system
- ✅ Simulation tools

---

## Summary Timeline

| Phase | Scope | Duration | Cumulative |
|-------|-------|----------|------------|
| 1 | Core Dashboard + Resources | 3 weeks | 3 weeks |
| 2 | Users + Content Management | 2 weeks | 5 weeks |
| 3 | AI Platform | 4 weeks | 9 weeks |
| 4 | Business + Simulations | 3 weeks | **12 weeks** |

**Total: ~12 settimane (3 mesi) per dashboard enterprise completa**

**MVP (Phase 1)**: 3 settimane
- Overview con KPI estesi
- Resources management
- Operations panel con conferme
- Audit log funzionante

---

## Next Steps

Questa è la specifica ad alto livello. Vuoi che:

1. **Inizi l'implementazione** di Phase 1 (Core Dashboard)?
2. **Approfondisco** una sezione specifica (es. AI Lab design dettagliato)?
3. **Creo wireframe visivi** per ogni tab?
4. **Genero il data model completo** con migrazioni DB?

Dimmi come preferisci procedere!
