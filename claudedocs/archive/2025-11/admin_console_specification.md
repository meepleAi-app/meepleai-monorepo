# 🎛️ MeepleAI Admin Console - Complete Specification

**Document Version**: 1.0.0
**Last Updated**: 2025-11-11
**Status**: Comprehensive Requirements Document

---

## 📋 Executive Summary

Enterprise-grade admin console per gestione completa dell'applicazione MeepleAI, con controllo full-stack su infrastruttura, utenti, API, configurazione, monitoring e analytics.

### 🎯 Obiettivi

1. **Centralizzazione**: Single pane of glass per tutte le operazioni admin
2. **Real-Time Monitoring**: Dashboard live con health checks e metriche
3. **Full Control**: Gestione completa servizi, config, users, API keys
4. **Data-Driven**: KPI business + technical + AI/RAG + content metrics
5. **Automation**: Bulk operations, import/export, alerting configurabile

### 📊 Stato Attuale

**Backend Endpoints Esistenti** (AdminEndpoints.cs):
- ✅ AI Request Logs (`/admin/requests`)
- ✅ AI Statistics (`/admin/stats`)
- ✅ Quality Reports (`/admin/quality/*`)
- ✅ n8n Configuration (`/admin/n8n`)
- ✅ Session Management (`/admin/sessions`)
- ✅ Analytics Dashboard (`/admin/analytics`)
- ✅ Workflow Errors (`/admin/workflows/errors`)
- ✅ Alerting System (`/admin/alerts`)
- ✅ Prompt Management (`/admin/prompts/*`)
- ✅ User Management (`/admin/users`)
- ✅ Dynamic Configuration (`/admin/configurations/*`)
- ✅ Cache Statistics (`/admin/cache/stats`)

**Frontend Pages Esistenti** (apps/web/src/pages/admin/):
- ✅ `/admin/cache` - Cache management
- ✅ `/admin/configuration` - Dynamic config UI
- ✅ `/admin/bulk-export` - Bulk operations
- ✅ `/admin/n8n-templates` - Workflow templates
- ✅ `/admin/users` - User management
- ✅ `/admin/analytics` - Analytics dashboard

**Coverage Esistente**: ~60% delle funzionalità target

---

## 🗺️ Admin Console Sitemap Completo

```
🎛️ ADMIN CONSOLE
│
├── 🏠 DASHBOARD OVERVIEW (/admin)                    [NEW]
│   ├── System Status Cards
│   │   ├── 🟢 All Systems Operational
│   │   ├── ⚠️ Warnings (3)
│   │   └── 🔴 Critical Alerts (0)
│   ├── Quick Metrics Grid (4x3)
│   │   ├── 👥 Active Users: 1,234
│   │   ├── 🔑 API Requests/min: 156
│   │   ├── 💬 Active Chats: 89
│   │   ├── 🎮 Games Hosted: 567
│   │   ├── 📊 Uptime: 99.94%
│   │   ├── ⚡ Avg Response: 145ms
│   │   ├── 🧠 RAG Accuracy: 94.2%
│   │   ├── 💰 AI Costs/day: €23.45
│   │   ├── 📦 Cache Hit Rate: 87%
│   │   ├── 🗄️ DB Size: 12.4 GB
│   │   ├── 🔍 Vector Index: 2.3M docs
│   │   └── 🐛 Error Rate: 0.08%
│   ├── Recent Activity Feed
│   │   ├── 2min ago: User alice@example.com logged in
│   │   ├── 5min ago: PDF uploaded: "Wingspan Rules.pdf"
│   │   ├── 12min ago: n8n workflow executed successfully
│   │   └── [View All Activity]
│   └── Quick Actions
│       ├── [👥 Manage Users]
│       ├── [📊 View Analytics]
│       ├── [🔧 Configuration]
│       └── [🚨 View Alerts]
│
├── 🏗️ INFRASTRUCTURE MANAGEMENT (/admin/infrastructure) [NEW]
│   │
│   ├── Services Health Matrix
│   │   ├── PostgreSQL
│   │   │   ├── Status: 🟢 Healthy
│   │   │   ├── Connections: 23/100
│   │   │   ├── DB Size: 12.4 GB
│   │   │   ├── Slow Queries: 2 (>1s)
│   │   │   └── Actions: [View Queries] [Backup] [Optimize]
│   │   │
│   │   ├── Redis
│   │   │   ├── Status: 🟢 Healthy
│   │   │   ├── Memory: 2.1 GB / 4 GB
│   │   │   ├── Keys: 45,678
│   │   │   ├── Hit Rate: 87.3%
│   │   │   └── Actions: [Clear Cache] [View Keys] [Config]
│   │   │
│   │   ├── Qdrant
│   │   │   ├── Status: 🟢 Healthy
│   │   │   ├── Collections: 12
│   │   │   ├── Vectors: 2,345,678
│   │   │   ├── Storage: 8.9 GB
│   │   │   └── Actions: [Optimize] [Reindex] [Backup]
│   │   │
│   │   ├── n8n
│   │   │   ├── Status: 🟢 Healthy
│   │   │   ├── Workflows: 23 active
│   │   │   ├── Executions/hour: 145
│   │   │   ├── Error Rate: 0.2%
│   │   │   └── Actions: [View Workflows] [Logs] [Config]
│   │   │
│   │   ├── Seq (Logging)
│   │   │   ├── Status: 🟢 Healthy
│   │   │   ├── Events/min: 234
│   │   │   ├── Storage: 4.2 GB
│   │   │   ├── Retention: 30 days
│   │   │   └── Actions: [View Logs] [Configure]
│   │   │
│   │   └── Jaeger (Tracing)
│   │       ├── Status: 🟢 Healthy
│   │       ├── Traces/hour: 1,234
│   │       ├── Services: 3
│   │       └── Actions: [View Traces] [Configure]
│   │
│   ├── Resource Monitoring (Real-Time Charts)
│   │   ├── CPU Usage (per container)
│   │   ├── Memory Usage (per container)
│   │   ├── Disk I/O
│   │   ├── Network Traffic
│   │   └── [Last 1h | 24h | 7d | 30d]
│   │
│   ├── Backup & Recovery
│   │   ├── Scheduled Backups
│   │   │   ├── PostgreSQL: Daily 2 AM UTC
│   │   │   ├── Qdrant: Weekly Sundays
│   │   │   ├── Redis: Snapshot every 4h
│   │   │   └── [Configure Schedule]
│   │   │
│   │   ├── Manual Backups
│   │   │   ├── [Backup Database Now]
│   │   │   ├── [Backup Vector Store]
│   │   │   └── [Full System Backup]
│   │   │
│   │   └── Restore Operations
│   │       ├── Backup History (last 30 days)
│   │       ├── [Restore from Backup]
│   │       └── [Point-in-Time Recovery]
│   │
│   └── Service Control Panel
│       ├── Container Management
│       │   ├── [Restart All Services]
│       │   ├── [Restart Single Service] dropdown
│       │   ├── [View Docker Logs]
│       │   └── [Health Check All]
│       │
│       └── Maintenance Mode
│           ├── Toggle: ⚪ Disabled / 🔴 Enabled
│           ├── Maintenance Message (editable)
│           └── [Enable Maintenance Mode]
│
├── 👥 USER MANAGEMENT (/admin/users)                 [ENHANCE]
│   │
│   ├── User List (enhanced with filters)              [EXISTING]
│   │   ├── Search: email, name, role
│   │   ├── Filters: role, status, 2FA enabled, OAuth provider
│   │   ├── Sort: created, last login, email
│   │   └── Pagination: 50 per page
│   │
│   ├── Bulk Operations Panel                          [NEW]
│   │   ├── [☑️ Select All]
│   │   ├── Bulk Actions Dropdown
│   │   │   ├── Change Role (Admin/Editor/User)
│   │   │   ├── Suspend Users
│   │   │   ├── Activate Users
│   │   │   ├── Force Password Reset
│   │   │   ├── Send Email Notification
│   │   │   └── Export Selected (CSV/JSON)
│   │   └── [Apply to Selected (N users)]
│   │
│   ├── Import/Export Operations                       [NEW]
│   │   ├── [📥 Import Users from CSV]
│   │   │   └── Template: email, name, role, send_invite
│   │   ├── [📤 Export All Users]
│   │   │   └── Formats: CSV, JSON, Excel
│   │   └── [📜 View Import History]
│   │
│   ├── User Detail Modal (click on user)              [ENHANCE]
│   │   ├── TAB: Profile
│   │   │   ├── Email, name, avatar
│   │   │   ├── Created, last login, IP
│   │   │   ├── [Edit Profile]
│   │   │   └── [Impersonate User] (view as user)
│   │   │
│   │   ├── TAB: Authentication
│   │   │   ├── 2FA Status: 🟢 Enabled / ⚪ Disabled
│   │   │   ├── OAuth Accounts Linked
│   │   │   ├── Active Sessions (3)
│   │   │   ├── [Force Logout All Sessions]
│   │   │   ├── [Disable 2FA]
│   │   │   └── [Reset Password]
│   │   │
│   │   ├── TAB: Activity Log                         [NEW]
│   │   │   ├── Login history (last 100)
│   │   │   ├── Actions performed (CRUD operations)
│   │   │   ├── API calls made
│   │   │   ├── Files uploaded
│   │   │   └── [Export Activity]
│   │   │
│   │   ├── TAB: Usage Statistics                     [NEW]
│   │   │   ├── Chats created: 45
│   │   │   ├── Games in library: 12
│   │   │   ├── Sessions played: 23
│   │   │   ├── API calls: 1,234
│   │   │   └── Storage used: 234 MB
│   │   │
│   │   └── TAB: Danger Zone
│   │       ├── [Suspend Account]
│   │       ├── [Delete Account]
│   │       └── [Ban User] (permanent)
│   │
│   └── User Audit Trail                               [NEW]
│       ├── Filter by action type
│       ├── Date range selector
│       └── Export audit logs
│
├── 🔑 API KEY MANAGEMENT (/admin/api-keys)           [NEW]
│   │
│   ├── API Keys Overview
│   │   ├── Total Keys: 234
│   │   ├── Active: 189
│   │   ├── Expired: 12
│   │   ├── Suspended: 3
│   │   └── Usage This Month: 1.2M requests
│   │
│   ├── Keys List Table
│   │   ├── Columns: Key Name, Owner, Created, Last Used, Requests/day, Status
│   │   ├── Search: by name, owner email
│   │   ├── Filter: by status, role, usage
│   │   └── Sort: by usage, created, last used
│   │
│   ├── Key Detail View (click on key)
│   │   │
│   │   ├── TAB: Overview
│   │   │   ├── Key Info
│   │   │   │   ├── Name: "Production API Key"
│   │   │   │   ├── Key: mpl_prod_xxxx...xxxx (masked, click to reveal)
│   │   │   │   ├── Owner: alice@example.com
│   │   │   │   ├── Created: 2024-11-15
│   │   │   │   ├── Last Used: 2 minutes ago
│   │   │   │   └── Status: 🟢 Active
│   │   │   │
│   │   │   ├── Actions
│   │   │   │   ├── [Regenerate Key]
│   │   │   │   ├── [Suspend Key]
│   │   │   │   ├── [Delete Key]
│   │   │   │   └── [Copy Key]
│   │   │
│   │   ├── TAB: Usage Analytics                      [NEW - Priority]
│   │   │   ├── Real-Time Metrics (last 24h)
│   │   │   │   ├── Requests/hour chart
│   │   │   │   ├── Response time p95
│   │   │   │   ├── Error rate %
│   │   │   │   └── Bandwidth usage
│   │   │   │
│   │   │   ├── Historical Trends (7/30/90 days)
│   │   │   │   ├── Total requests
│   │   │   │   ├── Unique endpoints hit
│   │   │   │   ├── Average latency
│   │   │   │   └── Cost estimation (AI calls)
│   │   │   │
│   │   │   ├── Endpoint Breakdown
│   │   │   │   ├── Most used endpoints (top 10)
│   │   │   │   ├── Slowest endpoints
│   │   │   │   ├── Error-prone endpoints
│   │   │   │   └── [View All Endpoints]
│   │   │   │
│   │   │   └── Usage Heatmap
│   │   │       └── Requests by hour/day of week
│   │   │
│   │   ├── TAB: Quotas & Limits                      [NEW - Priority]
│   │   │   ├── Rate Limiting
│   │   │   │   ├── Current: 1000 req/min (default User role)
│   │   │   │   ├── Custom Limit: [Input] req/min
│   │   │   │   ├── Burst Allowance: [Input] requests
│   │   │   │   └── [Save Custom Limit]
│   │   │   │
│   │   │   ├── Feature Access Control
│   │   │   │   ├── ☑️ RAG Search (enabled)
│   │   │   │   ├── ☑️ Chat (enabled)
│   │   │   │   ├── ☐ AI Players (disabled - premium)
│   │   │   │   ├── ☐ Workflow Automation (disabled - premium)
│   │   │   │   └── [Update Permissions]
│   │   │   │
│   │   │   ├── Expiration Management
│   │   │   │   ├── Expires: Never / [Date Picker]
│   │   │   │   ├── Auto-renew: ☐ Enabled
│   │   │   │   ├── Alert before expiry: [7] days
│   │   │   │   └── [Save Expiration]
│   │   │   │
│   │   │   └── Cost Limits (future: billing)
│   │   │       ├── Max AI Cost/month: €[100]
│   │   │       ├── Alert threshold: €[80]
│   │   │       └── Action on limit: [Suspend/Alert/Nothing]
│   │   │
│   │   ├── TAB: Lifecycle & Rotation                 [NEW - Priority]
│   │   │   ├── Key Rotation Policy
│   │   │   │   ├── Auto-rotate: ☐ Enabled
│   │   │   │   ├── Rotation Period: [90] days
│   │   │   │   ├── Grace Period: [7] days (old key still works)
│   │   │   │   └── Notification: [Email owner] 7 days before
│   │   │   │
│   │   │   ├── Rotation History
│   │   │   │   ├── List of past rotations (date, reason, actor)
│   │   │   │   └── [Trigger Manual Rotation]
│   │   │   │
│   │   │   └── Security Events
│   │   │       ├── Failed auth attempts: 0 (last 24h)
│   │   │       ├── Suspicious activity: None detected
│   │   │       └── [View Security Log]
│   │   │
│   │   └── TAB: Team Access                          [NEW - Future]
│   │       ├── Shared Key for Team: ☐ Enabled
│   │       ├── Team Members (list)
│   │       ├── Granular Permissions (per member)
│   │       └── Approval Workflow (require admin approval)
│   │
│   ├── Bulk Key Operations                            [NEW]
│   │   ├── [Suspend Selected Keys]
│   │   ├── [Delete Expired Keys]
│   │   ├── [Export Key Usage (CSV)]
│   │   └── [Generate Team Keys]
│   │
│   └── API Key Analytics Dashboard                    [NEW]
│       ├── Top 10 Keys by Usage
│       ├── Cost Attribution (AI calls per key)
│       ├── Error Rate per Key
│       └── Anomaly Detection (unusual patterns)
│
├── 📊 ANALYTICS DASHBOARD (/admin/analytics)          [ENHANCE]
│   │
│   ├── Date Range Selector (global)
│   │   └── [Last 24h | 7d | 30d | Custom Range]
│   │
│   ├── TAB: Business Metrics                          [NEW]
│   │   ├── User Acquisition
│   │   │   ├── New Users (chart)
│   │   │   ├── MAU (Monthly Active Users)
│   │   │   ├── DAU (Daily Active Users)
│   │   │   ├── User Retention (cohort analysis)
│   │   │   └── Churn Rate
│   │   │
│   │   ├── Engagement Metrics
│   │   │   ├── Avg Chats per User
│   │   │   ├── Avg Session Duration
│   │   │   ├── Games per User
│   │   │   ├── Return Rate (7-day, 30-day)
│   │   │   └── Feature Adoption (% users using each feature)
│   │   │
│   │   └── Revenue Metrics (future: premium)
│   │       ├── MRR (Monthly Recurring Revenue)
│   │       ├── Free → Premium Conversion Rate
│   │       ├── LTV (Lifetime Value)
│   │       ├── CAC (Customer Acquisition Cost)
│   │       └── Revenue per User
│   │
│   ├── TAB: Technical Metrics                         [ENHANCE]
│   │   ├── API Performance                            [EXISTING]
│   │   │   ├── Request Rate (req/min chart)
│   │   │   ├── Response Time (p50, p95, p99)
│   │   │   ├── Error Rate (%) + breakdown by code
│   │   │   ├── Throughput (MB/s)
│   │   │   └── [View Slow Queries]
│   │   │
│   │   ├── Database Performance                       [NEW]
│   │   │   ├── Query Performance (slowest queries)
│   │   │   ├── Connection Pool Usage
│   │   │   ├── Transaction Rate
│   │   │   ├── Index Efficiency
│   │   │   └── [Optimize Indexes]
│   │   │
│   │   ├── Cache Performance                          [EXISTING]
│   │   │   ├── Hit Rate % (L1 + L2)
│   │   │   ├── Eviction Rate
│   │   │   ├── Memory Usage
│   │   │   └── [Warm Cache] [Clear Cache]
│   │   │
│   │   └── Uptime & Availability                      [NEW]
│   │       ├── Uptime % (99.9% target)
│   │       ├── Downtime Log (incidents)
│   │       ├── MTBF (Mean Time Between Failures)
│   │       └── MTTR (Mean Time To Recovery)
│   │
│   ├── TAB: AI/RAG Metrics                            [ENHANCE]
│   │   ├── RAG Quality                                [EXISTING]
│   │   │   ├── Accuracy Score (manual eval)
│   │   │   ├── Avg Confidence Score
│   │   │   ├── Citation Correctness %
│   │   │   └── Low-Quality Response Rate
│   │   │
│   │   ├── LLM Usage & Costs                          [NEW]
│   │   │   ├── Requests/day (by model)
│   │   │   ├── Tokens consumed (input/output)
│   │   │   ├── Cost per day/week/month
│   │   │   ├── Cost per user
│   │   │   └── [Optimize Model Selection]
│   │   │
│   │   ├── Embedding Pipeline                         [NEW]
│   │   │   ├── Embeddings Generated/day
│   │   │   ├── Avg Generation Time
│   │   │   ├── Vector Index Size
│   │   │   └── Reindexing Status
│   │   │
│   │   └── User Satisfaction                          [EXISTING]
│   │       ├── Thumbs Up/Down Ratio
│   │       ├── Feedback Comments (recent)
│   │       └── [View All Feedback]
│   │
│   └── TAB: Content Metrics                           [NEW]
│       ├── Game Catalog
│       │   ├── Total Games: 567
│       │   ├── Official: 234 (41%)
│       │   ├── User Uploads: 333 (59%)
│       │   ├── Games Added (chart)
│       │   └── Most Popular Games (top 10 by users)
│       │
│       ├── PDF Processing
│       │   ├── PDFs Uploaded/day
│       │   ├── Avg Processing Time
│       │   ├── Success Rate %
│       │   ├── Failed Uploads (with reasons)
│       │   └── Total Storage Used: 23.4 GB
│       │
│       ├── Chat Activity
│       │   ├── Chats Created/day
│       │   ├── Messages/day
│       │   ├── Avg Messages per Chat
│       │   └── Active Chat Threads: 234
│       │
│       └── Game Sessions
│           ├── Sessions Created/day
│           ├── Avg Session Duration
│           ├── Completion Rate %
│           └── Most Played Games
│
├── 🔧 CONFIGURATION (/admin/configuration)            [EXISTING]
│   │
│   ├── [Keep existing UI - already very complete]
│   │
│   └── ENHANCEMENT: Add Quick Presets                 [NEW]
│       ├── [Load Development Config]
│       ├── [Load Production Config]
│       ├── [Load Performance Optimized]
│       └── [Create Custom Preset]
│
├── 📜 SYSTEM LOGS (/admin/logs)                       [NEW]
│   │
│   ├── Seq Integration Dashboard
│   │   ├── Real-Time Log Stream
│   │   │   ├── Auto-refresh: ☑️ Enabled (5s)
│   │   │   ├── Pause/Resume stream
│   │   │   └── [Export Visible Logs]
│   │   │
│   │   ├── Advanced Filters
│   │   │   ├── Log Level: [All] [Error] [Warning] [Info] [Debug]
│   │   │   ├── Time Range: [Last 1h] [24h] [7d] [Custom]
│   │   │   ├── Service: [API] [Web] [n8n] [All]
│   │   │   ├── Correlation ID: [Input]
│   │   │   ├── User ID: [Input]
│   │   │   ├── Free-text Search: [Input]
│   │   │   └── [Apply Filters]
│   │   │
│   │   ├── Log Entry Display
│   │   │   ├── Timestamp (sortable)
│   │   │   ├── Level badge (🔴 Error, ⚠️ Warning, ℹ️ Info)
│   │   │   ├── Service tag
│   │   │   ├── Message (expandable)
│   │   │   ├── Stack trace (if error)
│   │   │   ├── Correlation ID (clickable)
│   │   │   └── Context (JSON expandable)
│   │   │
│   │   ├── Error Correlation                         [NEW]
│   │   │   ├── Group Similar Errors
│   │   │   ├── Error Frequency (chart)
│   │   │   ├── Affected Users Count
│   │   │   ├── First Seen / Last Seen
│   │   │   └── [Create Alert Rule]
│   │   │
│   │   └── Quick Actions
│   │       ├── [Jump to Seq Dashboard] (external)
│   │       ├── [Jump to Jaeger Trace] (if correlation ID)
│   │       ├── [View Related Logs]
│   │       └── [Download Logs (JSON)]
│   │
│   └── Saved Queries                                  [NEW]
│       ├── My Saved Queries (list)
│       ├── [Save Current Filter]
│       └── [Load Query]
│
├── 💾 CACHE MANAGEMENT (/admin/cache)                 [EXISTING]
│   │
│   ├── [Keep existing UI]
│   │
│   └── ENHANCEMENT: Add Cache Warming                 [NEW]
│       ├── [Warm Popular Games Cache]
│       ├── [Warm Embeddings Cache]
│       └── [Schedule Cache Warming]
│
├── 🔔 ALERTS & MONITORING (/admin/alerts)             [ENHANCE]
│   │
│   ├── Active Alerts List                             [EXISTING]
│   │   ├── Critical (0)
│   │   ├── Warning (3)
│   │   └── Info (12)
│   │
│   ├── Alert Configuration                            [NEW]
│   │   ├── [Create New Alert Rule]
│   │   │   └── MODAL: Alert Rule Builder
│   │   │       ├── Metric: [Dropdown: Error Rate, Latency, etc]
│   │   │       ├── Condition: [> / < / =] [Threshold]
│   │   │       ├── Duration: Alert if true for [5] minutes
│   │   │       ├── Severity: [Critical / Warning / Info]
│   │   │       ├── Channels: ☑️ Email ☑️ Slack ☐ PagerDuty
│   │   │       ├── Throttle: Send max [1] alert per [hour]
│   │   │       └── [Create Alert]
│   │   │
│   │   ├── Existing Rules (list)
│   │   │   ├── Rule name, condition, channels
│   │   │   ├── Status: 🟢 Active / ⚪ Disabled
│   │   │   ├── Last Triggered: timestamp
│   │   │   └── [Edit] [Delete] [Test Alert]
│   │   │
│   │   └── Alert History
│   │       ├── Past alerts (filterable)
│   │       ├── Resolution status
│   │       └── [Export Alert Log]
│   │
│   └── Notification Channels                          [NEW]
│       ├── Email Configuration
│       │   ├── Recipients: [Comma-separated emails]
│       │   ├── Template: [Dropdown]
│       │   └── [Test Email]
│       │
│       ├── Slack Integration
│       │   ├── Webhook URL: [Input]
│       │   ├── Channel: #alerts
│       │   └── [Test Slack Message]
│       │
│       └── PagerDuty Integration (future)
│           ├── API Key: [Input]
│           ├── Service ID: [Input]
│           └── [Test PagerDuty]
│
├── 🔄 N8N WORKFLOWS (/admin/n8n-templates)            [EXISTING]
│   │
│   ├── [Keep existing template library]
│   │
│   └── ENHANCEMENT: Workflow Monitoring               [NEW]
│       ├── Active Workflows (count)
│       ├── Execution Success Rate
│       ├── Failed Executions (last 24h)
│       └── [View n8n Dashboard] (external link)
│
├── 📤 BULK OPERATIONS (/admin/bulk-export)            [EXISTING]
│   │
│   ├── [Keep existing RuleSpec export]
│   │
│   └── ENHANCEMENT: More Bulk Ops                     [NEW]
│       ├── Bulk User Export/Import
│       ├── Bulk Game Import (from BGG)
│       ├── Bulk PDF Processing
│       └── Bulk Configuration Export/Import
│
├── 🔍 AUDIT LOGS (/admin/audit)                       [NEW]
│   │
│   ├── Comprehensive Audit Trail
│   │   ├── Filter by:
│   │   │   ├── Actor (user email)
│   │   │   ├── Action (created, updated, deleted)
│   │   │   ├── Entity (user, game, config, etc)
│   │   │   ├── Date Range
│   │   │   └── IP Address
│   │   │
│   │   ├── Audit Entry Display
│   │   │   ├── Timestamp
│   │   │   ├── Actor (who)
│   │   │   ├── Action (what)
│   │   │   ├── Entity (which resource)
│   │   │   ├── Before/After values (diff view)
│   │   │   ├── IP Address
│   │   │   └── Result (success/failure)
│   │   │
│   │   └── Export & Compliance
│   │       ├── [Export Audit Log (CSV)]
│   │       ├── [Generate Compliance Report]
│   │       └── Retention: 365 days (configurable)
│   │
│   └── Security Events                                [NEW]
│       ├── Failed Login Attempts
│       ├── Permission Violations
│       ├── Suspicious API Usage
│       └── [Block IP] [Create Alert]
│
└── ⚙️ SYSTEM ADMINISTRATION (/admin/system)           [NEW]
    │
    ├── Application Settings
    │   ├── Maintenance Mode
    │   │   ├── Toggle: ⚪ Disabled / 🔴 Enabled
    │   │   ├── Message: [Text Input]
    │   │   ├── Allowed IPs: [Comma-separated]
    │   │   └── [Enable Maintenance]
    │   │
    │   ├── Feature Flags (global)
    │   │   ├── List of all features (inherited from CONFIG)
    │   │   ├── Enable/Disable per environment
    │   │   └── Rollout Percentage (gradual rollout)
    │   │
    │   └── System Information
    │       ├── App Version: v2.1.0
    │       ├── .NET Version: 9.0.1
    │       ├── Environment: Production
    │       ├── Deployed: 2024-11-10 14:23 UTC
    │       └── Deployment History (last 10)
    │
    ├── Background Jobs                                [NEW]
    │   ├── Job Queue Status
    │   │   ├── Pending: 12
    │   │   ├── Running: 3
    │   │   ├── Completed (24h): 1,234
    │   │   ├── Failed (24h): 5
    │   │   └── [View Failed Jobs]
    │   │
    │   ├── Job Types
    │   │   ├── PDF Processing (status, queue size)
    │   │   ├── Embedding Generation (status, queue size)
    │   │   ├── Session Auto-Revocation (last run)
    │   │   ├── Cache Cleanup (schedule)
    │   │   └── Analytics Aggregation (schedule)
    │   │
    │   └── Manual Job Execution
    │       ├── [Run Session Cleanup Now]
    │       ├── [Reindex All Embeddings]
    │       ├── [Aggregate Analytics]
    │       └── [Clear Failed Jobs]
    │
    └── Database Management                            [NEW]
        ├── Migrations Status
        │   ├── Pending Migrations: 0
        │   ├── Applied Migrations (list)
        │   ├── Last Migration: 20251026170110
        │   └── [Apply Pending] [Rollback Last]
        │
        ├── Database Health
        │   ├── Size: 12.4 GB
        │   ├── Tables: 45
        │   ├── Indexes: 123
        │   ├── Unused Indexes: 3 [Optimize]
        │   └── [Vacuum Database] [Analyze Tables]
        │
        └── Data Management
            ├── [Backup Database Now]
            ├── [Restore from Backup]
            ├── [Cleanup Old Data] (sessions >90d, logs >30d)
            └── [Export Schema (SQL)]
```

---

## 🏗️ Architecture Design

### Backend Services da Creare

```csharp
// 1. Infrastructure Management Service
public interface IInfrastructureManagementService
{
    Task<ServiceHealthReport> GetAllServicesHealthAsync();
    Task<ServiceHealth> GetServiceHealthAsync(string serviceName);
    Task<bool> RestartServiceAsync(string serviceName);
    Task<BackupResult> BackupDatabaseAsync();
    Task<RestoreResult> RestoreDatabaseAsync(string backupId);
    Task<ResourceMetrics> GetResourceMetricsAsync(string serviceName, TimeRange range);
}

// 2. Advanced API Key Service
public interface IAdvancedApiKeyService
{
    Task<ApiKeyUsageAnalytics> GetKeyAnalyticsAsync(string keyId, TimeRange range);
    Task<List<ApiKeyEndpointUsage>> GetEndpointBreakdownAsync(string keyId);
    Task UpdateKeyQuotasAsync(string keyId, KeyQuotas quotas);
    Task<List<SecurityEvent>> GetKeySecurityEventsAsync(string keyId);
    Task RotateKeyAsync(string keyId, int gracePeriodDays);
    Task<List<ApiKey>> GetExpiringKeysAsync(int daysUntilExpiry);
}

// 3. System Logs Service
public interface ISystemLogsService
{
    Task<List<LogEntry>> QuerySeqLogsAsync(LogQueryFilter filter);
    Task<List<ErrorGroup>> GetCorrelatedErrorsAsync(TimeRange range);
    Task<LogExport> ExportLogsAsync(LogQueryFilter filter, ExportFormat format);
    Task SaveLogQueryAsync(string userId, string queryName, LogQueryFilter filter);
}

// 4. Business Analytics Service
public interface IBusinessAnalyticsService
{
    Task<UserAcquisitionMetrics> GetUserAcquisitionAsync(TimeRange range);
    Task<EngagementMetrics> GetEngagementMetricsAsync(TimeRange range);
    Task<ContentMetrics> GetContentMetricsAsync(TimeRange range);
    Task<RevenueMetrics> GetRevenueMetricsAsync(TimeRange range); // Future
}

// 5. Background Jobs Service
public interface IBackgroundJobsService
{
    Task<JobQueueStatus> GetQueueStatusAsync();
    Task<List<FailedJob>> GetFailedJobsAsync();
    Task TriggerJobAsync(string jobType);
    Task RetryFailedJobAsync(Guid jobId);
    Task ClearFailedJobsAsync();
}
```

### Database Schema Extensions

```sql
-- API Key Advanced Features
CREATE TABLE api_key_usage_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    api_key_id UUID REFERENCES api_keys(id) ON DELETE CASCADE,
    endpoint VARCHAR(255) NOT NULL,
    http_method VARCHAR(10) NOT NULL,
    response_status INT NOT NULL,
    response_time_ms INT NOT NULL,
    request_size_bytes BIGINT,
    response_size_bytes BIGINT,
    ip_address INET,
    user_agent TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_api_key_usage_key_id ON api_key_usage_logs(api_key_id);
CREATE INDEX idx_api_key_usage_created_at ON api_key_usage_logs(created_at);
CREATE INDEX idx_api_key_usage_endpoint ON api_key_usage_logs(endpoint);

CREATE TABLE api_key_quotas (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    api_key_id UUID UNIQUE REFERENCES api_keys(id) ON DELETE CASCADE,
    requests_per_minute INT DEFAULT 1000,
    burst_allowance INT DEFAULT 100,
    max_ai_cost_per_month DECIMAL(10,2),
    features_allowed JSONB, -- {"rag": true, "ai_players": false}
    expires_at TIMESTAMP,
    auto_renew BOOLEAN DEFAULT false,
    alert_days_before_expiry INT DEFAULT 7,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE api_key_rotation_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    api_key_id UUID REFERENCES api_keys(id) ON DELETE CASCADE,
    old_key_hash TEXT NOT NULL,
    rotation_reason VARCHAR(255),
    rotated_by_user_id UUID REFERENCES users(id),
    grace_period_days INT,
    created_at TIMESTAMP DEFAULT NOW()
);

-- System Logs & Audit
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    actor_email VARCHAR(255),
    action VARCHAR(100) NOT NULL, -- 'created', 'updated', 'deleted'
    entity_type VARCHAR(100) NOT NULL, -- 'user', 'game', 'config'
    entity_id UUID,
    before_state JSONB,
    after_state JSONB,
    ip_address INET,
    user_agent TEXT,
    result VARCHAR(50) NOT NULL, -- 'success', 'failure'
    error_message TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_actor ON audit_logs(actor_user_id);
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at);
CREATE INDEX idx_audit_logs_entity ON audit_logs(entity_type, entity_id);

CREATE TABLE security_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type VARCHAR(100) NOT NULL, -- 'failed_login', 'permission_violation', 'suspicious_activity'
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    api_key_id UUID REFERENCES api_keys(id) ON DELETE SET NULL,
    severity VARCHAR(50) NOT NULL, -- 'low', 'medium', 'high', 'critical'
    description TEXT NOT NULL,
    ip_address INET,
    metadata JSONB,
    is_resolved BOOLEAN DEFAULT false,
    resolved_at TIMESTAMP,
    resolved_by_user_id UUID REFERENCES users(id),
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_security_events_severity ON security_events(severity);
CREATE INDEX idx_security_events_created_at ON security_events(created_at);

-- Background Jobs
CREATE TABLE background_jobs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_type VARCHAR(100) NOT NULL,
    status VARCHAR(50) NOT NULL, -- 'pending', 'running', 'completed', 'failed'
    priority INT DEFAULT 0,
    payload JSONB,
    result JSONB,
    error_message TEXT,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_background_jobs_status ON background_jobs(status);
CREATE INDEX idx_background_jobs_job_type ON background_jobs(job_type);

-- Business Metrics (aggregated)
CREATE TABLE business_metrics_daily (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    metric_date DATE NOT NULL UNIQUE,
    new_users INT DEFAULT 0,
    active_users INT DEFAULT 0,
    chats_created INT DEFAULT 0,
    messages_sent INT DEFAULT 0,
    games_added INT DEFAULT 0,
    sessions_created INT DEFAULT 0,
    pdfs_uploaded INT DEFAULT 0,
    avg_chat_duration_seconds INT,
    revenue_cents BIGINT DEFAULT 0, -- Future: premium subscriptions
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_business_metrics_date ON business_metrics_daily(metric_date DESC);
```

---

## 🎨 UI/UX Design Patterns

### Dashboard Overview Layout

```
┌──────────────────────────────────────────────────────────────┐
│ 🎛️ ADMIN CONSOLE                        DegrassiAaron ▼    │
├──────────────────────────────────────────────────────────────┤
│ ┌─────────────┬────────────────────────────────────────────┐│
│ │ SIDEBAR NAV │ MAIN CONTENT AREA                          ││
│ │ (240px)     │                                            ││
│ │             │ ┌──────────────────────────────────────────┐││
│ │ 🏠 Dashboard│ │ SYSTEM STATUS                            │││
│ │ 🏗️ Infra    │ │ ──────────────────────────────────────── │││
│ │ 👥 Users    │ │ 🟢 All Systems Operational               │││
│ │ 🔑 API Keys │ │ Uptime: 99.94% • Last Check: 5s ago      │││
│ │ 📊 Analytics│ └──────────────────────────────────────────┘││
│ │ 🔧 Config   │                                            ││
│ │ 📜 Logs     │ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐││
│ │ 💾 Cache    │ │ 👥 1,234│ │ 🔑 156 │ │ 💬 89  │ │ 🎮 567 │││
│ │ 🔔 Alerts   │ │  Users  │ │ API/min│ │  Chats │ │  Games │││
│ │ 🔄 n8n      │ └────────┘ └────────┘ └────────┘ └────────┘││
│ │ 📤 Bulk Ops │                                            ││
│ │ 🔍 Audit    │ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐││
│ │ ⚙️ System   │ │ 📊 99.9%│ │ ⚡ 145ms│ │ 🧠 94.2│ │ 💰 €23 │││
│ │             │ │ Uptime  │ │ Latency│ │ RAG Acc│ │ AI Cost│││
│ │ ─────────── │ └────────┘ └────────┘ └────────┘ └────────┘││
│ │ 🚪 Logout   │                                            ││
│ └─────────────┴────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────┘
```

### Color-Coded Health Status

- 🟢 **Green**: Healthy (90-100% of capacity)
- 🟡 **Yellow**: Warning (70-90% of capacity)
- 🔴 **Red**: Critical (<70% or service down)

### Real-Time Updates

- WebSocket connection for live metrics
- Auto-refresh every 5 seconds (configurable)
- Notification toasts for critical events
- Sound alerts (optional, configurable)

---

## 📊 KPI Dashboard Layout

### Metrics Organization

**Level 1: High-Level KPIs** (Dashboard Overview)
- 🟢/🔴 System Health
- 👥 Active Users (MAU/DAU)
- 📊 Uptime %
- 💰 Daily Costs

**Level 2: Category Dashboards** (Dedicated Pages)
- Business Metrics (/admin/analytics?tab=business)
- Technical Metrics (/admin/analytics?tab=technical)
- AI/RAG Metrics (/admin/analytics?tab=ai-rag)
- Content Metrics (/admin/analytics?tab=content)

**Level 3: Drill-Down Views** (Modal/Detail Pages)
- Per-user analytics
- Per-game analytics
- Per-API-key analytics
- Time-series analysis

---

## 🔒 Security & Permissions

### Role-Based Access Control (RBAC)

| Feature | Admin | Editor | User |
|---------|-------|--------|------|
| **Dashboard Overview** | ✅ Full | ✅ Read-only | ❌ |
| **Infrastructure Control** | ✅ Full | ❌ | ❌ |
| **User Management** | ✅ Full | ✅ View only | ❌ |
| **API Key Management** | ✅ All keys | ✅ Own keys | ✅ Own keys |
| **Analytics** | ✅ All data | ✅ All data | ❌ |
| **Configuration** | ✅ Edit | ✅ View | ❌ |
| **System Logs** | ✅ Full | ✅ View | ❌ |
| **Audit Logs** | ✅ Full | ✅ View own | ❌ |
| **Alerts** | ✅ Configure | ✅ View | ❌ |
| **Backup/Restore** | ✅ Execute | ❌ | ❌ |

### Audit Trail Requirements

**All Admin Actions Must Be Logged**:
- Who performed the action (user_id, email)
- What action was performed (create, update, delete, restart, etc.)
- When it was performed (timestamp)
- What resource was affected (entity_type, entity_id)
- Result (success/failure)
- Before/after state (for updates)
- IP address and user agent

**Retention**: 365 days minimum (configurable per compliance needs)

---

## 🚀 Implementation Priority

### Phase 1: MVP Foundation (Sprint 1-2) - 4 weeks

**Goal**: Completare dashboard overview e funzionalità critiche

#### Sprint 1: Infrastructure & API Keys (2 weeks)

**Issues da Creare**:
1. **Infrastructure Management Dashboard**
   - Backend: InfrastructureManagementService
   - Endpoints: Health checks, service status
   - Frontend: /admin/infrastructure page
   - Real-time WebSocket updates
   - **Estimate**: 40h

2. **Advanced API Key Analytics**
   - Backend: AdvancedApiKeyService, usage logging
   - DB Migration: api_key_usage_logs table
   - Endpoints: Usage analytics, quotas
   - Frontend: /admin/api-keys page with charts
   - **Estimate**: 32h

3. **API Key Quotas & Lifecycle**
   - Backend: Quota management, rotation logic
   - DB Migration: api_key_quotas, rotation_history tables
   - Endpoints: Quota CRUD, rotation triggers
   - Frontend: Quotas config UI, rotation scheduler
   - **Estimate**: 24h

**Total Sprint 1**: 96h (~2 weeks with 2 developers)

#### Sprint 2: Logs & Analytics Enhancement (2 weeks)

**Issues da Creare**:
4. **System Logs Viewer**
   - Backend: SystemLogsService (Seq API integration)
   - Endpoints: Log query, export, saved queries
   - Frontend: /admin/logs page with advanced filtering
   - Error correlation logic
   - **Estimate**: 40h

5. **Enhanced Analytics Dashboard**
   - Backend: BusinessAnalyticsService
   - DB Migration: business_metrics_daily table
   - Endpoints: Business, technical, AI/RAG, content metrics
   - Frontend: Enhanced /admin/analytics with 4 tabs
   - **Estimate**: 48h

6. **Dashboard Overview Page**
   - Frontend: /admin page (entry point)
   - Real-time metrics cards (8-12 KPIs)
   - Recent activity feed
   - Quick actions panel
   - **Estimate**: 24h

**Total Sprint 2**: 112h (~2 weeks with 2 developers)

---

### Phase 2: Advanced Features (Sprint 3-4) - 4 weeks

#### Sprint 3: Audit & Security (2 weeks)

**Issues da Creare**:
7. **Comprehensive Audit Logs**
   - Backend: AuditLoggingService (enhance existing)
   - DB Migration: audit_logs, security_events tables
   - Endpoints: Audit query, export, compliance reports
   - Frontend: /admin/audit page
   - **Estimate**: 32h

8. **Security Events Dashboard**
   - Backend: SecurityEventService
   - Detection logic: Failed logins, permission violations
   - Endpoints: Security events, IP blocking
   - Frontend: Security events UI in /admin/audit
   - **Estimate**: 24h

9. **Alert Configuration UI**
   - Frontend: Enhanced /admin/alerts page
   - Alert rule builder modal
   - Channel configuration (Email, Slack, PagerDuty)
   - Alert testing and history
   - **Estimate**: 32h

**Total Sprint 3**: 88h (~2 weeks)

#### Sprint 4: System Administration (2 weeks)

**Issues da Creare**:
10. **System Administration Panel**
    - Backend: SystemAdministrationService
    - Endpoints: Maintenance mode, background jobs
    - DB Migration: background_jobs table
    - Frontend: /admin/system page
    - **Estimate**: 40h

11. **Background Jobs Dashboard**
    - Backend: BackgroundJobsService (Hangfire integration)
    - Endpoints: Job queue status, manual triggers
    - Frontend: Background jobs UI
    - Job monitoring and retry logic
    - **Estimate**: 32h

12. **User Management Bulk Operations**
    - Backend: Enhanced UserManagementService
    - Endpoints: Bulk update, import CSV, export
    - Frontend: Bulk selection UI in /admin/users
    - Import/export wizard
    - **Estimate**: 24h

**Total Sprint 4**: 96h (~2 weeks)

---

### Phase 3: Polish & Optimization (Sprint 5) - 2 weeks

**Issues da Creare**:
13. **Admin Console Navigation & UX**
    - Unified sidebar navigation
    - Breadcrumbs
    - Quick search (global admin search)
    - Responsive design for admin pages
    - **Estimate**: 24h

14. **Real-Time Dashboard Optimizations**
    - WebSocket pooling for all admin pages
    - Metrics aggregation service
    - Chart performance optimization
    - Data export optimization (streaming)
    - **Estimate**: 24h

15. **Admin Console Testing Suite**
    - Unit tests for all new services (95% coverage)
    - Integration tests with Testcontainers
    - E2E tests for critical admin flows
    - Performance tests (admin pages <1s load)
    - **Estimate**: 48h

**Total Sprint 5**: 96h (~2 weeks)

---

## 🎯 Total Effort Estimate

```yaml
Phase 1 (MVP): 208 hours (5.2 weeks with 2 devs)
  Sprint 1: 96h (Infra + API Keys)
  Sprint 2: 112h (Logs + Analytics)

Phase 2 (Advanced): 184 hours (4.6 weeks with 2 devs)
  Sprint 3: 88h (Audit + Security)
  Sprint 4: 96h (System Admin + Bulk Ops)

Phase 3 (Polish): 96 hours (2.4 weeks with 2 devs)
  Sprint 5: 96h (UX + Testing)

────────────────────────────────────
TOTAL: 488 hours (~12 weeks with 2 devs)
       OR ~6 weeks with 4 devs
```

---

## 📈 Success Metrics

### Technical Metrics
- Dashboard load time: <1s
- Real-time updates: <5s latency
- API response time: <500ms p95
- Test coverage: 95%+ for admin module
- Zero security vulnerabilities

### User Experience Metrics
- Admin task completion time: -50% vs manual
- Error resolution time: -70% with logs viewer
- User onboarding time: -60% with bulk import
- System monitoring efficiency: +80% vs external tools

### Business Impact
- Operational costs: -30% (automation)
- Incident resolution: -50% faster (better logs)
- User support efficiency: +40% (better user insights)
- Compliance reporting: Automated (vs 8h manual)

---

## 🔗 Next Steps

### Immediate Actions

1. **Review Specification** (this document)
2. **Prioritize Features** (confirm Phase 1 scope)
3. **Create GitHub Issues** (15 issues for Phases 1-3)
4. **Setup Development Environment** (verify access to Seq, Jaeger, etc.)
5. **Kickoff Phase 1 Sprint 1**

### Questions for Stakeholders

1. **Budget**: Approvato budget per 12 settimane development?
2. **Team**: Disponibili 2-4 developers per admin console?
3. **Timeline**: Accettabile 12-week timeline o serve prioritizzazione?
4. **Compliance**: Requisiti specifici per audit logs (GDPR, SOC2, etc.)?
5. **Hosting**: Infrastructure control funziona con Docker/Kubernetes?

---

## 📚 References

- [Complete Product Specification](./meepleai_complete_specification.md)
- [Roadmap 2025](./roadmap_meepleai_evolution_2025.md)
- Existing AdminEndpoints.cs (apps/api/src/Api/Routing/AdminEndpoints.cs)
- Existing Admin Pages (apps/web/src/pages/admin/)
- [Observability Docs](../docs/observability.md)
- [Security Docs](../docs/SECURITY.md)

---

**Document Status**: ✅ Complete - Ready for Review
**Next Step**: Create 15 GitHub Issues for Implementation
**Estimated Timeline**: 12 weeks (2-4 developers)

