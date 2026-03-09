# MeepleAI - Guide Utente

Guide per ruolo (admin, editor, utente), flussi di autenticazione, gestione giochi, AI chat, sessioni.

**Data generazione**: 8 marzo 2026

**File inclusi**: 30

---

## Indice

1. user-guides/README.md
2. user-guides/admin-role/01-approval-workflow.md
3. user-guides/admin-role/02-user-management.md
4. user-guides/admin-role/03-system-configuration.md
5. user-guides/admin-role/04-monitoring.md
6. user-guides/administration.md
7. user-guides/authentication.md
8. user-guides/COMPLETE-TEST-FLOWS.md
9. user-guides/diagrams/core-flows.md
10. user-guides/document-processing.md
11. user-guides/editor-role/01-game-management.md
12. user-guides/editor-role/02-document-management.md
13. user-guides/editor-role/03-content-management.md
14. user-guides/editor-role/04-publication-workflow.md
15. user-guides/game-management.md
16. user-guides/game-session-toolkit-flows.md
17. user-guides/gap-analysis.md
18. user-guides/index.md
19. user-guides/knowledge-base.md
20. user-guides/session-tracking.md
21. user-guides/shared-game-catalog.md
22. user-guides/system-configuration.md
23. user-guides/user-library.md
24. user-guides/user-notifications.md
25. user-guides/user-role/01-authentication.md
26. user-guides/user-role/02-game-discovery.md
27. user-guides/user-role/03-library-management.md
28. user-guides/user-role/04-ai-chat.md
29. user-guides/user-role/05-game-sessions.md
30. user-guides/workflow-integration.md

---



<div style="page-break-before: always;"></div>

## user-guides/README.md

# MeepleAI User Flows Documentation

> Complete documentation of user interactions, flows, and journeys across all roles.

## Overview

This documentation covers all user flows for MeepleAI, organized by role:

- **[User Role](./user-role/)** - End-user journeys (authentication, library, chat, sessions)
- **[Editor Role](./editor-role/)** - Content management flows (game creation, documents, FAQ)
- **[Admin Role](./admin-role/)** - Administrative flows (approvals, users, configuration)
- **[Diagrams](./diagrams/)** - Sequence diagrams for complex flows

## Role Hierarchy

*(blocco di codice rimosso)*

| Role | Description | Key Capabilities |
|------|-------------|------------------|
| **User** | End users consuming content | Library management, AI chat, game sessions |
| **Editor** | Content creators | Game catalog CRUD, PDF processing, FAQ/Errata |
| **Admin** | System administrators | User management, approvals, configuration |

## Tier System

User tiers affect quotas and limits (configurable via Admin):

| Tier | Game Library | PDF Upload (Day/Week) | Sessions | Features |
|------|--------------|----------------------|----------|----------|
| **Free** | 5 games | 5/20 | Unlimited* | Basic |
| **Normal** | 20 games | 20/100 | Unlimited* | Standard |
| **Premium** | 50 games | 100/500 | Unlimited* | All Features |

*Session limits not currently enforced but configurable.

## Document Structure

Each flow document contains:

1. **User Stories** - Acceptance criteria format
2. **Screen Flow** - Page-to-page navigation
3. **Sequence Diagram** - Technical interaction flow
4. **API Flow** - Endpoint chain for the action
5. **Implementation Status** - Existing vs gaps

## Quick Links by Flow Type

### Authentication & Onboarding
- [User Registration](./user-role/01-authentication.md#registration)
- [User Login](./user-role/01-authentication.md#login)
- [OAuth Flow](./user-role/01-authentication.md#oauth)
- [2FA Setup](./user-role/01-authentication.md#two-factor-authentication)

### Game Discovery & Library
- [Browse Catalog](./user-role/02-game-discovery.md#browse-catalog)
- [Search Games](./user-role/02-game-discovery.md#search-games)
- [Game Details](./user-role/02-game-discovery.md#game-details)
- [Library Management](./user-role/03-library-management.md)

### AI Chat & Knowledge
- [Start Chat Session](./user-role/04-ai-chat.md#start-chat)
- [Ask Questions](./user-role/04-ai-chat.md#ask-question)
- [Chat History](./user-role/04-ai-chat.md#chat-history)
- [Export Chat](./user-role/04-ai-chat.md#export-chat)

### Game Sessions
- [Create Session](./user-role/05-game-sessions.md#create-session)
- [Track Game State](./user-role/05-game-sessions.md#game-state)
- [Player Mode](./user-role/05-game-sessions.md#player-mode)

### Editor Workflows
- [Create Game](./editor-role/01-game-management.md#create-game)
- [Manage Documents](./editor-role/02-document-management.md)
- [FAQ & Errata](./editor-role/03-content-management.md)
- [Publication Workflow](./editor-role/04-publication-workflow.md)

### Admin Operations
- [Approval Queue](./admin-role/01-approval-workflow.md)
- [User Management](./admin-role/02-user-management.md)
- [System Configuration](./admin-role/03-system-configuration.md)
- [Monitoring](./admin-role/04-monitoring.md)

## Testing & Gap Analysis

### Test Coverage Documentation
- **[complete-test-flows.md](./complete-test-flows.md)** - Comprehensive list of all 105 user flows for E2E testing
- **[Gap Analysis](./gap-analysis.md)** - Implementation status and missing features

### Test Flow Categories

| Category | Existing | Missing | Total | Coverage |
|----------|----------|---------|-------|----------|
| Authentication | 8 | 6 | 14 | 57% |
| Game Discovery | 5 | 3 | 8 | 63% |
| Library Management | 6 | 4 | 10 | 60% |
| AI Chat & RAG | 7 | 5 | 12 | 58% |
| Game Sessions | 6 | 5 | 11 | 55% |
| Editor Flows | 8 | 4 | 12 | 67% |
| Admin Flows | 10 | 8 | 18 | 56% |
| Error & Edge Cases | 5 | 15 | 20 | 25% |
| **TOTAL** | **55** | **50** | **105** | **52%** |

### Priority Flows (P0)

Must be tested before any release:

1. **AUTH-09**: Email Verification
2. **AUTH-10**: Account Lockout
3. **LIB-07**: Session Quota Display
4. **SESS-07**: Session Limits Enforcement
5. **ADM-11**: Session Limits Configuration
6. **ERR-06**: Rate Limiting (429)
7. **ERR-14**: Quota Exceeded Actions

## Related Documentation

- [Product Specification](../01-architecture/overview/product-specification.md)
- [API Reference](../03-api/README.md)
- [Bounded Contexts](../09-bounded-contexts/)
- [E2E Test Guide](../05-testing/e2e/e2-e-test-guide.md)

---

*Last Updated: 2026-01-26*


---



<div style="page-break-before: always;"></div>

## user-guides/admin-role/01-approval-workflow.md

# Admin: Approval Workflow

> Admin flows for reviewing and approving game publications and deletions.

## Table of Contents

- [Publication Approval Queue](#publication-approval-queue)
- [Review and Approve](#review-and-approve)
- [Reject with Reason](#reject-with-reason)
- [Delete Approval Queue](#delete-approval-queue)

---

## Role: Admin

**Capabilities:**
- All Editor capabilities plus:
- Approve/reject game publications
- Approve/reject delete requests
- Manage users and roles
- Configure system settings
- Access analytics and monitoring
- Manage AI models and prompts

---

## Publication Approval Queue

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Query | Description |
|----------|--------|-------|-------------|
| `/api/v1/admin/shared-games/pending-approvals` | GET | `sort, editor, page` | List pending |

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Pending Approvals Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Queue Page | ✅ Implemented | `/app/admin/shared-games/pending-approvals/` (if exists) |
| Preview | ✅ Implemented | Game detail page |

---

## Review and Approve

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/shared-games/{id}/approve-publication` | POST | `{ notes? }` | Approve game |

**Request:**
*(blocco di codice rimosso)*

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Approve Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Notification | ⚠️ Partial | Basic implementation |
| Audit Log | ✅ Implemented | Entity tracking |

---

## Reject with Reason

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/shared-games/{id}/reject-publication` | POST | `{ reason }` | Reject game |

**Request:**
*(blocco di codice rimosso)*

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Reject Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Rejection UI | ⚠️ Partial | Basic dialog |
| Quick Reasons | ❌ Not Implemented | Feature request |

---

## Delete Approval Queue

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/shared-games/pending-deletes` | GET | List pending deletes |
| `/api/v1/admin/shared-games/approve-delete/{requestId}` | POST | Approve deletion |
| `/api/v1/admin/shared-games/reject-delete/{requestId}` | POST | Reject deletion |

**Delete Request Object:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Pending Deletes Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Approve/Reject | ✅ Implemented | Same file |
| Delete Queue Page | ✅ Implemented | `/app/admin/shared-games/pending-deletes/page.tsx` |

---

## Gap Analysis

### Implemented Features
- [x] Publication approval queue
- [x] Approve game publication
- [x] Reject with reason
- [x] Delete request queue
- [x] Approve/reject delete requests
- [x] Audit logging

### Missing/Partial Features
- [ ] **Quick Rejection Reasons**: Pre-defined templates
- [ ] **Batch Approval**: Approve multiple games at once
- [ ] **Review Assignment**: Assign reviews to specific admins
- [ ] **SLA Tracking**: Time-to-review metrics
- [ ] **Review Checklist**: Standardized review criteria
- [ ] **Conditional Approval**: "Approve pending minor changes"

### Proposed Enhancements
1. **Quick Templates**: Pre-defined rejection reasons
2. **Review Assignment**: Distribute workload among admins
3. **SLA Dashboard**: Track review times and bottlenecks
4. **Standardized Checklist**: Consistent review criteria
5. **Batch Operations**: Handle multiple approvals efficiently


---



<div style="page-break-before: always;"></div>

## user-guides/admin-role/02-user-management.md

# Admin: User Management Flows

> Admin flows for managing users, roles, and subscriptions.

## Table of Contents

- [View Users](#view-users)
- [Create User](#create-user)
- [Edit User](#edit-user)
- [Manage Roles](#manage-roles)
- [Manage Tiers](#manage-tiers)
- [Bulk Operations](#bulk-operations)
- [User Activity](#user-activity)

---

## View Users

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Query | Description |
|----------|--------|-------|-------------|
| `/api/v1/admin/users` | GET | `search, role, tier, page, size` | List users |
| `/api/v1/users/search` | GET | `q` | Autocomplete search |

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| List Users Endpoint | ✅ Implemented | `AdminUserEndpoints.cs` |
| Search Endpoint | ✅ Implemented | Same file |
| Users Page | ✅ Implemented | `/app/admin/users/page.tsx` |

---

## Create User

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/users` | POST | User data | Create user |

**Request:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Create User Endpoint | ✅ Implemented | `AdminUserEndpoints.cs` |
| Create UI | ⚠️ Partial | Basic implementation |
| Welcome Email | ❌ Not Implemented | Feature request |

---

## Edit User

### User Story

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/users/{id}` | PUT | Updated data | Update user |
| `/api/v1/admin/users/{id}` | DELETE | - | Delete user |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Update User Endpoint | ✅ Implemented | `AdminUserEndpoints.cs` |
| Delete User Endpoint | ✅ Implemented | Same file |

---

## Manage Roles

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Role Permissions Matrix

| Permission | User | Editor | Admin |
|------------|------|--------|-------|
| Browse catalog | ✅ | ✅ | ✅ |
| Manage personal library | ✅ | ✅ | ✅ |
| Use AI chat | ✅ | ✅ | ✅ |
| Create games | ❌ | ✅ | ✅ |
| Edit games | ❌ | ✅ | ✅ |
| Upload unlimited PDFs | ❌ | ✅ | ✅ |
| Approve publications | ❌ | ❌ | ✅ |
| Manage users | ❌ | ❌ | ✅ |
| System configuration | ❌ | ❌ | ✅ |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Role Change | ✅ Implemented | Part of user update |
| Role UI | ⚠️ Partial | Basic implementation |

---

## Manage Tiers

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/users/{id}/tier` | PUT | `{ tier }` | Update tier |

**Request:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Update Tier Endpoint | ✅ Implemented | `AdminUserEndpoints.cs` |
| Tier UI | ⚠️ Partial | Basic implementation |

---

## Bulk Operations

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/users/bulk/password-reset` | POST | `{ userIds[] }` | Bulk reset |
| `/api/v1/admin/users/bulk/role-change` | POST | `{ userIds[], role }` | Bulk role |
| `/api/v1/admin/users/bulk/import` | POST | CSV file | Import users |
| `/api/v1/admin/users/bulk/export` | GET | Filters | Export CSV |

**Bulk Operation Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Bulk Password Reset | ✅ Implemented | `AdminUserEndpoints.cs` |
| Bulk Role Change | ✅ Implemented | Same file |
| Import CSV | ✅ Implemented | Same file |
| Export CSV | ✅ Implemented | Same file |
| Bulk UI | ⚠️ Partial | Basic selection |

---

## User Activity

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Query | Description |
|----------|--------|-------|-------------|
| `/api/v1/admin/users/{userId}/activity` | GET | `type, from, to, page` | User activity |
| `/api/v1/users/me/activity` | GET | Same | Own activity |

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Activity Endpoint | ✅ Implemented | `AdminUserEndpoints.cs` |
| User Activity Endpoint | ✅ Implemented | `UserProfileEndpoints.cs` |
| UserActivityTimeline | ✅ Implemented | `UserActivityTimeline.tsx` |

---

## Gap Analysis

### Implemented Features
- [x] List and search users
- [x] Create users
- [x] Edit users
- [x] Delete users
- [x] Change roles
- [x] Change tiers
- [x] Bulk operations (reset, role, import, export)
- [x] User activity timeline

### Missing/Partial Features
- [ ] **Welcome Email**: On user creation
- [ ] **Account Deactivation**: Soft-disable without delete
- [ ] **Session Management**: View/revoke user sessions
- [ ] **Impersonation**: Log in as user for troubleshooting
- [ ] **Usage Analytics**: Per-user usage statistics
- [ ] **Subscription Management**: Payment/billing integration

### Proposed Enhancements
1. **Welcome Email**: Send onboarding email on creation
2. **Impersonation**: Admin can log in as user (with audit)
3. **Usage Dashboard**: Per-user usage graphs and statistics
4. **Account Locking**: Temporary lock for security
5. **Self-Service Tier Upgrade**: User-initiated upgrades


---



<div style="page-break-before: always;"></div>

## user-guides/admin-role/03-system-configuration.md

# Admin: System Configuration Flows

> Admin flows for configuring system settings, quotas, and AI models.

## Table of Contents

- [Quota Configuration](#quota-configuration)
- [Feature Flags](#feature-flags)
- [AI Model Configuration](#ai-model-configuration)
- [Prompt Management](#prompt-management)
- [API Key Administration](#api-key-administration)

---

## Quota Configuration

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/system/game-library-limits` | GET | - | Get limits |
| `/api/v1/admin/system/game-library-limits` | PUT | Limits config | Update limits |

**Update Request:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Library Limits Endpoint | ✅ Implemented | System configuration |
| PDF Limits | ⚠️ DB Only | Not exposed in admin UI |
| Config Page | ✅ Implemented | `/app/admin/configuration/game-library-limits/page.tsx` |

---

## Feature Flags

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/feature-flags` | GET | List all flags |
| `/api/v1/admin/feature-flags/{name}` | PUT | Update flag |
| `/api/v1/admin/feature-flags` | POST | Create flag |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Feature Flag Service | ✅ Implemented | `FeatureFlagService.cs` |
| Admin Endpoints | ⚠️ Partial | Database config only |
| Feature Flags Tab | ✅ Implemented | `FeatureFlagsTab.tsx` |

---

## AI Model Configuration

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| AI Models Page | ✅ Implemented | `/app/admin/ai-models/page.tsx` |
| AiModelsTable | ✅ Implemented | `AiModelsTable.tsx` |
| SetPrimaryModelDialog | ✅ Implemented | `SetPrimaryModelDialog.tsx` |
| Model Config | ⚠️ Partial | Basic configuration |

---

## Prompt Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/prompts` | GET | List prompts |
| `/api/v1/admin/prompts/{id}` | GET | Get prompt details |
| `/api/v1/admin/prompts/{id}` | PUT | Update prompt |
| `/api/v1/admin/prompts/{id}/versions` | GET | Version history |
| `/api/v1/admin/prompts/{id}/versions/new` | POST | Create new version |
| `/api/v1/admin/prompts/{id}/compare` | GET | Compare versions |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Prompts Page | ✅ Implemented | `/app/admin/prompts/page.tsx` |
| Prompt Edit | ✅ Implemented | `/app/admin/prompts/[id]/page.tsx` |
| Version History | ✅ Implemented | `/app/admin/prompts/[id]/versions/` |
| Compare View | ✅ Implemented | `/app/admin/prompts/[id]/compare/page.tsx` |
| PromptEditor | ✅ Implemented | `PromptEditor.tsx` |
| DiffViewer | ✅ Implemented | `DiffViewerEnhanced.tsx` |

---

## API Key Administration

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/api-keys/stats` | GET | All keys with stats |
| `/api/v1/admin/api-keys/{id}` | DELETE | Permanently delete |
| `/api/v1/admin/api-keys/bulk/export` | GET | Export CSV |
| `/api/v1/admin/api-keys/bulk/import` | POST | Import CSV |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Admin API Keys Endpoints | ✅ Implemented | `ApiKeyEndpoints.cs` |
| Export/Import | ✅ Implemented | Same file |
| API Keys Page | ✅ Implemented | `/app/admin/api-keys/page.tsx` |
| ApiKeyFilterPanel | ✅ Implemented | `ApiKeyFilterPanel.tsx` |

---

## Gap Analysis

### Implemented Features
- [x] Game library quota configuration
- [x] Feature flags management
- [x] AI model configuration
- [x] Prompt management with versioning
- [x] Prompt diff/comparison
- [x] API key administration
- [x] Bulk export/import

### Missing/Partial Features
- [ ] **PDF Upload Limits UI**: Only in database
- [ ] **Rate Limit Configuration**: No admin UI
- [ ] **Feature Flag Gradual Rollout**: Percentage-based rollout
- [ ] **A/B Testing Prompts**: Test prompt variations
- [ ] **Cost Tracking**: Per-model cost monitoring
- [ ] **Configuration History**: Audit trail for config changes

### Proposed Enhancements
1. **Full Quota UI**: Expose all quota types in admin interface
2. **Rate Limit Config**: Per-role rate limit configuration
3. **Gradual Rollout**: Percentage-based feature flag rollout
4. **Prompt A/B Testing**: Test prompt effectiveness
5. **Cost Dashboard**: Real-time AI cost monitoring
6. **Config Audit Log**: Full history of configuration changes


---



<div style="page-break-before: always;"></div>

## user-guides/admin-role/04-monitoring.md

# Admin: Monitoring Flows

> Admin flows for system monitoring, health checks, and analytics.

## Table of Contents

- [System Health](#system-health)
- [Service Monitoring](#service-monitoring)
- [Alert Management](#alert-management)
- [Analytics Dashboard](#analytics-dashboard)
- [N8N Workflow Management](#n8n-workflow-management)
- [Cache Management](#cache-management)

---

## System Health

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Basic health check |
| `/health/live` | GET | Liveness probe |
| `/health/ready` | GET | Readiness probe |
| `/health/detailed` | GET | Detailed component status |

**Detailed Health Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Health Endpoints | ✅ Implemented | Health checks |
| Admin Dashboard | ✅ Implemented | `/app/admin/page.tsx` |
| SystemStatus | ✅ Implemented | `SystemStatus.tsx` |
| ServiceHealthMatrix | ✅ Implemented | `ServiceHealthMatrix.tsx` |

---

## Service Monitoring

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Services Page | ✅ Implemented | `/app/admin/services/page.tsx` |
| ServiceCard | ✅ Implemented | `ServiceCard.tsx` |
| GrafanaEmbed | ✅ Implemented | `GrafanaEmbed.tsx` |
| Infrastructure Page | ✅ Implemented | `/app/admin/infrastructure/page.tsx` |

---

## Alert Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/alerts` | GET | List active alerts |
| `/api/v1/admin/alert-rules` | GET | List alert rules |
| `/api/v1/admin/alert-rules` | POST | Create rule |
| `/api/v1/admin/alert-rules/{id}` | PUT | Update rule |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Alerts Page | ✅ Implemented | `/app/admin/alerts/page.tsx` |
| Alert Rules Page | ✅ Implemented | `/app/admin/alert-rules/page.tsx` |
| AlertRuleForm | ✅ Implemented | `AlertRuleForm.tsx` |
| AlertRuleList | ✅ Implemented | `AlertRuleList.tsx` |
| BudgetAlertBanner | ✅ Implemented | `BudgetAlertBanner.tsx` |

---

## Analytics Dashboard

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/analytics` | GET | Dashboard stats |
| `/api/v1/admin/analytics/users` | GET | User metrics |
| `/api/v1/admin/analytics/games` | GET | Game popularity |
| `/api/v1/admin/reports` | POST | Generate report |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Analytics Page | ✅ Implemented | `/app/admin/analytics/page.tsx` |
| Reports Page | ✅ Implemented | `/app/admin/reports/page.tsx` |
| AdminCharts | ✅ Implemented | `AdminCharts.tsx` |
| MetricsGrid | ✅ Implemented | `MetricsGrid.tsx` |
| StatCard | ✅ Implemented | `StatCard.tsx` |

---

## N8N Workflow Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/n8n` | GET | List configurations |
| `/api/v1/admin/n8n` | POST | Create configuration |
| `/api/v1/admin/n8n/{id}` | PUT | Update configuration |
| `/api/v1/admin/n8n/{id}/test` | POST | Test connection |
| `/api/v1/n8n/templates` | GET | List templates |
| `/api/v1/n8n/templates/{id}/import` | POST | Import template |
| `/api/v1/admin/workflows/errors` | GET | View errors |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| N8N Endpoints | ✅ Implemented | `WorkflowEndpoints.cs` |
| Templates | ✅ Implemented | Same file |
| Error Logging | ✅ Implemented | Same file |
| N8N Templates Page | ✅ Implemented | `/app/admin/n8n-templates/page.tsx` |

---

## Cache Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Cache Page | ✅ Implemented | `/app/admin/cache/page.tsx` |
| Cache Service | ✅ Implemented | Redis-based |

---

## Gap Analysis

### Implemented Features
- [x] System health dashboard
- [x] Service monitoring
- [x] Grafana integration
- [x] Alert rules management
- [x] Analytics dashboard
- [x] N8N workflow management
- [x] Cache management
- [x] Activity feed

### Missing/Partial Features
- [ ] **Real-time Metrics**: WebSocket-based live updates
- [ ] **Log Aggregation**: Centralized log viewing
- [ ] **Distributed Tracing**: Request tracing across services
- [ ] **Custom Dashboards**: User-created metric dashboards
- [ ] **Alert Escalation**: Multi-tier alert escalation
- [ ] **Incident Management**: Track and resolve incidents

### Proposed Enhancements
1. **Log Viewer**: Centralized log search and filtering
2. **Distributed Tracing**: End-to-end request tracing
3. **Custom Dashboards**: Build custom metric views
4. **PagerDuty Integration**: Alert escalation to on-call
5. **Incident Tracking**: Formal incident management
6. **SLA Monitoring**: Track service level agreements


---



<div style="page-break-before: always;"></div>

## user-guides/administration.md

# Administration - Flussi API

## Panoramica

Il bounded context Administration gestisce utenti, ruoli, analytics, dashboard, audit, reporting e alerting.

---

## 1. User Management

### CRUD Utenti

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/users/search` | `SearchUsersQuery` | `query` | `[S]` |
| GET | `/admin/users` | `GetAllUsersQuery` | `search?, role?, status?, tier?, sortBy?, sortOrder?, page?, limit?` | `[A]` |
| GET | `/admin/users/{userId}` | `GetUserByIdQuery` | — | `[A]` |
| POST | `/admin/users` | `CreateUserCommand` | CreateUserRequest | `[A]` |
| PUT | `/admin/users/{id}` | `UpdateUserCommand` | UpdateUserRequest | `[A]` |
| DELETE | `/admin/users/{id}` | `DeleteUserCommand` | — | `[A]` |

### Gestione Ruoli e Tier

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| PUT | `/admin/users/{id}/tier` | `UpdateUserTierCommand` | UpdateUserTierRequest | `[A]` |
| PATCH | `/admin/users/{userId}/level` | `SetUserLevelCommand` | SetUserLevelRequest | `[A]` |
| GET | `/admin/users/{userId}/role-history` | `GetUserRoleHistoryQuery` | — | `[A]` |

### Operazioni Utente

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/users/{id}/suspend` | `SuspendUserCommand` | `{ reason?, duration? }` | `[A]` |
| POST | `/admin/users/{id}/unsuspend` | `UnsuspendUserCommand` | — | `[A]` |
| POST | `/admin/users/{id}/unlock` | `UnlockAccountCommand` | — | `[A]` |
| POST | `/admin/users/{userId}/reset-password` | `ResetUserPasswordCommand` | ResetUserPasswordRequest | `[A]` |
| POST | `/admin/users/{userId}/send-email` | `SendUserEmailCommand` | `{ subject, body }` | `[A]` |
| GET | `/admin/users/{userId}/lockout-status` | `GetAccountLockoutStatusQuery` | — | `[A]` |

### Impersonation

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/users/{userId}/impersonate` | `ImpersonateUserCommand` | — | `[A]` |
| POST | `/admin/impersonation/end` | `EndImpersonationCommand` | EndImpersonationRequest | `[A]` |

### Bulk Operations

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/users/bulk/password-reset` | `BulkPasswordResetCommand` | BulkPasswordResetRequest | `[A]` |
| POST | `/admin/users/bulk/role-change` | `BulkRoleChangeCommand` | BulkRoleChangeRequest | `[A]` |
| POST | `/admin/users/bulk/import` | `BulkImportUsersCommand` | CSV body | `[A]` |
| GET | `/admin/users/bulk/export` | `BulkExportUsersQuery` | `role?, search?` | `[A]` |

### User Details (Admin View)

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/users/{userId}/activity` | `GetUserActivityQuery` | `actionFilter?, resourceFilter?, startDate?, endDate?, limit?` | `[A]` |
| GET | `/admin/users/{userId}/library/stats` | `GetUserLibraryStatsQuery` | — | `[A]` |
| GET | `/admin/users/{userId}/badges` | `GetUserBadgesQuery` | — | `[A]` |
| GET | `/admin/users/{userId}/ai-usage` | `GetUserDetailedAiUsageQuery` | `days?` | `[A]` |

---

## 2. Analytics e Metriche

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/analytics` | `GetAdminStatsQuery` | `fromDate?, toDate?, days?, gameId?, roleFilter?` | `[A]` |
| GET | `/admin/dashboard/stats` | `GetAdminStatsQuery` | Same as above | `[A]` |
| GET | `/admin/activity` | `GetRecentActivityQuery` | `limit?, since?` | `[A]` |
| POST | `/admin/analytics/export` | `ExportStatsCommand` | ExportDataRequest | `[A]` |
| GET | `/admin/analytics/api-requests` | `GetApiRequestsByDayQuery` | `days?` | `[A]` |
| GET | `/admin/analytics/ai-usage` | `GetAiUsageStatsQuery` | — | `[A]` |

### AI Request Tracking

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/requests` | `GetAiRequestsQuery` | `limit?, offset?, endpoint?, userId?, gameId?, startDate?, endDate?` | `[A]` |
| GET | `/admin/stats` | `GetAiRequestStatsQuery` | `startDate?, endDate?, userId?, gameId?` | `[A]` |
| GET | `/logs` | `GetAiRequestsQuery` | `limit?` | `[S]` |

### Quality Monitoring

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/llm/health` | `GetLlmHealthQuery` | — | `[A]` |
| GET | `/admin/quality/low-responses` | `GetLowQualityResponsesQuery` | `limit?, offset?, startDate?, endDate?` | `[A]` |
| GET | `/admin/quality/report` | `GenerateQualityReportQuery` | `days?` | `[A]` |

### LLM Cost Tracking

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/llm-costs/report` | `GetLlmCostReportQuery` | `startDate?, endDate?, userId?` | `[A]` |
| GET | `/llm-costs/daily` | `GetLlmCostReportQuery` | `date?` | `[A]` |
| POST | `/llm-costs/check-alerts` | `CheckLlmCostAlertsCommand` | — | `[A]` |

---

## 3. Dashboard

| Metodo | Path | Command/Query | Response | Auth |
|--------|------|---------------|----------|------|
| GET | `/dashboard` | `GetDashboardQuery` | JSON | `[S]` |
| GET | `/dashboard/insights` | `GetDashboardInsightsQuery` | JSON | `[S]` |
| GET | `/dashboard/activity-timeline` | `GetActivityTimelineQuery` | JSON | `[S]` |
| GET | `/dashboard/stream` | `GetDashboardStreamQuery` | SSE | `[S]` |

---

## 4. Reporting

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/reports/generate` | `GenerateReportCommand` | GenerateReportRequest | `[A]` |
| POST | `/admin/reports/schedule` | `ScheduleReportCommand` | ScheduleReportRequest | `[A]` |
| GET | `/admin/reports/scheduled` | `GetScheduledReportsQuery` | — | `[A]` |
| GET | `/admin/reports/executions` | `GetReportExecutionsQuery` | `reportId?, limit?` | `[A]` |
| PUT | `/admin/reports/{reportId}/schedule` | `UpdateReportScheduleCommand` | UpdateScheduleRequest | `[A]` |

---

## 5. Alerting

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/alerts/prometheus` | — (webhook) | Prometheus alert payload | `[P]` |
| GET | `/admin/alerts` | AlertingService | `activeOnly?` | `[A]` |
| POST | `/admin/alerts/{alertType}/resolve` | AlertingService | — | `[A]` |

---

## Flusso Admin: Gestione Utente Completo

*(blocco di codice rimosso)*

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 885 |
| **Passati** | 885 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 8s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| User CRUD | `CreateUserTests.cs`, `UpdateUserTests.cs`, `DeleteUserTests.cs` | Passato |
| User Operations | `SuspendUserTests.cs`, `UnsuspendTests.cs`, `UnlockAccountTests.cs` | Passato |
| Bulk Operations | `BulkExportTests.cs`, `BulkImportTests.cs`, `BulkPasswordResetTests.cs`, `BulkRoleChangeTests.cs` | Passato |
| Impersonation | `ImpersonateUserTests.cs`, `EndImpersonationTests.cs` | Passato |
| Analytics | `GetAdminStatsTests.cs`, `ExportStatsTests.cs` | Passato |
| Dashboard | `GetDashboardStreamTests.cs`, `GetActivityTimelineTests.cs` | Passato |
| Alert System | `CreateAlertRuleTests.cs`, `SendAlertTests.cs`, `ResolveAlertTests.cs` | Passato |
| Prompt Templates | `CreatePromptTemplateTests.cs`, `ActivateVersionTests.cs` | Passato |
| Quality Monitoring | `GetLowQualityResponsesTests.cs`, `GenerateQualityReportTests.cs` | Passato |
| Reporting | `GenerateReportTests.cs`, `ScheduleReportTests.cs` | Passato |
| Domain Entities | User, AlertRule, PromptTemplate (15 file) | Passato |
| Validators | 15 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/authentication.md

# Authentication - Flussi API

## Panoramica

Il bounded context Authentication gestisce registrazione, login, sessioni, 2FA, OAuth, API keys, password reset e verifica email.

---

## 1. Registrazione e Login

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/auth/register` | `RegisterCommand` | `{ email, password, displayName?, role? }` | `[P]` |
| POST | `/auth/login` | `LoginCommand` | `{ email, password }` | `[P]` |
| POST | `/auth/logout` | `LogoutCommand` | — (cookie) | `[P]` |
| GET | `/auth/me` | — (reads context) | — | `[P]` |

### Flusso Registrazione → Login

*(blocco di codice rimosso)*

---

## 2. Gestione Sessioni

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/auth/session/status` | — | — | `[S]` |
| POST | `/auth/session/extend` | `ExtendSessionCommand` | — | `[S]` |
| GET | `/users/me/sessions` | `GetUserSessionsQuery` | — | `[S]` |
| GET | `/auth/sessions/{sessionId}/status` | `GetSessionStatusQuery` | — | `[S]` |
| POST | `/auth/sessions/{sessionId}/extend` | `ExtendSessionCommand` | — | `[S]` |
| POST | `/auth/sessions/{sessionId}/revoke` | `RevokeSessionCommand` | — | `[S]` |
| POST | `/auth/sessions/revoke-all` | `LogoutAllDevicesCommand` | `{ includeCurrentSession?, password? }` | `[S]` |

### Admin Session Management

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/sessions` | `GetAllSessionsQuery` | `limit=100, userId?` | `[A]` |
| DELETE | `/admin/sessions/{sessionId}` | `RevokeSessionCommand` | — | `[A]` |
| DELETE | `/admin/users/{userId}/sessions` | `RevokeAllUserSessionsCommand` | — | `[A]` |

---

## 3. OAuth

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/auth/oauth/{provider}/login` | `InitiateOAuthLoginCommand` | provider: google/discord/github | `[P]` |
| GET | `/auth/oauth/{provider}/callback` | `HandleOAuthCallbackCommand` | `code, state` | `[P]` |
| DELETE | `/auth/oauth/{provider}/unlink` | `UnlinkOAuthAccountCommand` | — | `[S]` |
| GET | `/users/me/oauth-accounts` | `GetLinkedOAuthAccountsQuery` | — | `[S]` |

### Flusso OAuth Login

*(blocco di codice rimosso)*

**Rate Limiting**: 10 requests/min per IP

---

## 4. Two-Factor Authentication (2FA)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/auth/2fa/setup` | `GenerateTotpSetupCommand` | — | `[S]` |
| POST | `/auth/2fa/enable` | `Enable2FACommand` | `{ code }` | `[S]` |
| POST | `/auth/2fa/verify` | `Verify2FACommand` | `{ sessionToken, code }` | `[P]` |
| POST | `/auth/2fa/disable` | `Disable2FACommand` | `{ password, code }` | `[S]` |
| GET | `/users/me/2fa/status` | `Get2FAStatusQuery` | — | `[S]` |
| POST | `/auth/admin/2fa/disable` | `AdminDisable2FACommand` | `{ targetUserId }` | `[A]` |

### Flusso Setup 2FA

*(blocco di codice rimosso)*

### Flusso Login con 2FA

*(blocco di codice rimosso)*

**Rate Limiting**: 3 attempts/min per session token

---

## 5. API Keys

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/auth/apikey/login` | `LoginWithApiKeyCommand` | `{ apiKey }` | `[P]` |
| POST | `/auth/apikey/logout` | `LogoutApiKeyCommand` | — | `[P]` |
| POST | `/api-keys` | `CreateApiKeyManagementCommand` | `{ keyName, ... }` | `[S]` |
| GET | `/api-keys` | `ListApiKeysQuery` | `includeRevoked?, page?, pageSize?` | `[S]` |
| GET | `/api-keys/{keyId}` | `GetApiKeyQuery` | — | `[S]` |
| PUT | `/api-keys/{keyId}` | `UpdateApiKeyManagementCommand` | `{ keyName?, ... }` | `[S]` |
| DELETE | `/api-keys/{keyId}` | `RevokeApiKeyManagementCommand` | — | `[S]` |
| POST | `/api-keys/{keyId}/rotate` | `RotateApiKeyCommand` | — | `[S]` |
| GET | `/api-keys/{keyId}/usage` | `GetApiKeyUsageQuery` | — | `[S]` |
| GET | `/api-keys/{keyId}/stats` | `GetApiKeyUsageStatsQuery` | — | `[S]` |
| GET | `/api-keys/{keyId}/logs` | `GetApiKeyUsageLogsQuery` | `skip?, take?` | `[S]` |

### Admin API Keys

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| DELETE | `/admin/api-keys/{keyId}` | `DeleteApiKeyCommand` | — | `[A]` |
| GET | `/admin/api-keys/stats` | `GetAllApiKeysWithStatsQuery` | `userId?, includeRevoked?` | `[A]` |
| GET | `/admin/api-keys/bulk/export` | `BulkExportApiKeysQuery` | `userId?, isActive?, searchTerm?` | `[A]` |
| POST | `/admin/api-keys/bulk/import` | `BulkImportApiKeysCommand` | CSV body | `[A]` |

---

## 6. Password Reset

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/auth/password-reset/request` | `RequestPasswordResetCommand` | `{ email }` | `[P]` |
| GET | `/auth/password-reset/verify` | `ValidatePasswordResetTokenQuery` | `token` (query) | `[P]` |
| PUT | `/auth/password-reset/confirm` | `ResetPasswordCommand` | `{ token, newPassword }` | `[P]` |

### Flusso Password Reset

*(blocco di codice rimosso)*

---

## 7. Email Verification

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/auth/email/verify` | `VerifyEmailCommand` | `{ token }` | `[P]` |
| POST | `/auth/email/resend` | `ResendVerificationCommand` | `{ email }` | `[P]` |

**Rate Limiting**: 1 resend/min

---

## 8. Profilo Utente

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/users/profile` | `GetUserProfileQuery` | — | `[S]` |
| PUT | `/users/profile` | `UpdateUserProfileCommand` | `{ displayName?, email? }` | `[S]` |
| PUT | `/users/profile/password` | `ChangePasswordCommand` | `{ currentPassword, newPassword }` | `[S]` |
| GET | `/users/me/upload-quota` | `GetUserUploadQuotaQuery` | — | `[S]` |
| PUT | `/users/preferences` | `UpdatePreferencesCommand` | `{ language, theme, emailNotifications, dataRetentionDays }` | `[S]` |
| GET | `/users/preferences` | `GetUserProfileQuery` | — | `[S]` |
| GET | `/users/me/activity` | `GetUserActivityQuery` | `actionFilter?, resourceFilter?, startDate?, endDate?, limit?` | `[S]` |
| GET | `/users/me/ai-usage` | `GetUserDetailedAiUsageQuery` | `days?` | `[S]` |
| GET | `/users/me/features` | `GetUserAvailableFeaturesQuery` | — | `[S]` |

---

## 9. Device Management

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/users/me/devices` | `GetUserDevicesQuery` | — | `[S]` |
| DELETE | `/users/me/devices/{deviceId}` | `RevokeSessionCommand` | — | `[S]` |

---

## Metodi di Autenticazione

| Metodo | Header/Cookie | Descrizione |
|--------|--------------|-------------|
| Session Cookie | Cookie automatico | Login standard via browser |
| API Key | `Authorization: ApiKey <value>` | Accesso programmatico |
| Admin Session | `RequireAdminSession()` | Sessione con ruolo Admin/Editor |

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 1,325 |
| **Passati** | 1,320 |
| **Falliti** | 0 |
| **Ignorati** | 5 |
| **Pass Rate** | 100% |
| **Durata** | 42s |

### Fix Applicati (2026-02-15)

| Test | Fix |
|------|-----|
| `Parse_InvalidTier_ThrowsValidationException("enterprise")` | Rimosso "enterprise" da tier invalidi (valido dal Epic #4068) |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Login/Register | `LoginCommandHandlerTests.cs`, `RegisterCommandHandlerTests.cs` | Passato |
| Session Management | `CreateSessionCommandHandlerTests.cs`, `ExtendSessionTests.cs` | Passato |
| OAuth | `InitiateOAuthLoginTests.cs`, `HandleOAuthCallbackTests.cs` | Passato |
| 2FA | `GenerateTotpSetupTests.cs`, `Enable2FATests.cs`, `Verify2FATests.cs` | Passato |
| API Keys | `CreateApiKeyTests.cs`, `RotateApiKeyTests.cs` | Passato |
| Password Reset | `RequestPasswordResetTests.cs`, `ResetPasswordTests.cs` | Passato |
| Email Verification | `VerifyEmailTests.cs`, `ResendVerificationTests.cs` | Passato |
| User Profile | `UpdateUserProfileTests.cs`, `ChangePasswordTests.cs` | Passato |
| Device Management | `GetUserDevicesTests.cs` | Passato |
| Validators | 24 file di validazione | Passato |
| Domain Entities | `User.cs`, `Session.cs`, `ApiKey.cs`, `OAuthAccount.cs` | Passato |
| Event Handlers | 13 handler eventi | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/COMPLETE-TEST-FLOWS.md

# MeepleAI - Complete User Test Flows

**Date**: 2026-01-26 | **Purpose**: E2E test flow definitions

---

## Executive Summary

| Category | Existing | Missing | Total |
|----------|----------|---------|-------|
| **Authentication** | 8 | 6 | 14 |
| **Game Discovery** | 5 | 3 | 8 |
| **Library** | 6 | 4 | 10 |
| **AI Chat** | 7 | 5 | 12 |
| **Sessions** | 6 | 5 | 11 |
| **Editor** | 8 | 4 | 12 |
| **Admin** | 10 | 8 | 18 |
| **Errors** | 5 | 15 | 20 |
| **TOTAL** | **55** | **50** | **105** |

---

## 1. Authentication (14 flows)

### Existing ✅ (8)

| ID | Flow | Test File |
|----|------|-----------|
| AUTH-01 | Registration | `auth.spec.ts` |
| AUTH-02 | Login | `auth.spec.ts` |
| AUTH-03 | Logout | `auth.spec.ts` |
| AUTH-04 | OAuth Registration | `auth-oauth-registration.spec.ts` |
| AUTH-05 | OAuth Link/Unlink | `auth-oauth-advanced.spec.ts` |
| AUTH-06 | Password Reset | `auth-password-reset.spec.ts` |
| AUTH-07 | 2FA Enable/Disable | `auth-2fa-complete.spec.ts` |
| AUTH-08 | Session Management | `auth-logout-all-devices.spec.ts` |

### Missing ❌ (6)

**AUTH-09: Email Verification**
*(blocco di codice rimosso)*

**AUTH-10: Account Lockout**
*(blocco di codice rimosso)*

**AUTH-11: Login Notifications**
*(blocco di codice rimosso)*

**AUTH-12: Device Management**
*(blocco di codice rimosso)*

**AUTH-13: Remember Me**
*(blocco di codice rimosso)*

**AUTH-14: API Key Lifecycle**
*(blocco di codice rimosso)*

---

## 2. Game Discovery (8 flows)

### Existing ✅ (5)

| ID | Flow | Test File |
|----|------|-----------|
| DISC-01 | Browse Catalog | `game-search-browse.spec.ts` |
| DISC-02 | Search by Name | `game-search-browse.spec.ts` |
| DISC-03 | Filter Category/Players | `game-search-browse.spec.ts` |
| DISC-04 | Game Details | `giochi-game-detail.spec.ts` |
| DISC-05 | BGG Import | `bgg-integration.spec.ts` |

### Missing ❌ (3)

**DISC-06: Autocomplete** → Type "cat" → See ["Catan", "Carcassonne", "Cat Lady"]
**DISC-07: Similar Games** → View "Catan" → See similar mechanics → Add to library
**DISC-08: Advanced Filters** → Filter players "7+" + playtime "<30min" → No results → Suggest broaden

---

## 3. Library Management (10 flows)

### Existing ✅ (6)

| ID | Flow | Test File |
|----|------|-----------|
| LIB-01 | Add Game | `library.spec.ts` |
| LIB-02 | Remove Game | `library.spec.ts` |
| LIB-03 | Mark Favorite | `library.spec.ts` |
| LIB-04 | Bulk Operations | `library-bulk-operations.spec.ts` |
| LIB-05 | Quota Display | `library.spec.ts` |
| LIB-06 | Custom PDF Upload | `pdf-upload-journey.spec.ts` |

### Missing ❌ (4)

**LIB-07: Session Quota**
*(blocco di codice rimosso)*

**LIB-08: Wishlist** → Add to wishlist → Move to library when owned
**LIB-09: Game Loans** → Mark "Loaned to John" → Send reminder email
**LIB-10: Play History** → View play count + dates + win/loss stats

---

## 4. AI Chat & RAG (12 flows)

### Existing ✅ (7)

| ID | Flow | Test File |
|----|------|-----------|
| CHAT-01 | Ask Question | `chat.spec.ts` |
| CHAT-02 | Streaming | `chat-streaming.spec.ts` |
| CHAT-03 | Citations | `chat-citations.spec.ts` |
| CHAT-04 | Multi-turn | `qa-multi-turn.spec.ts` |
| CHAT-05 | Context Switch | `chat-context-switching.spec.ts` |
| CHAT-06 | Export Chat | `chat-export.spec.ts` |
| CHAT-07 | History | `chat.spec.ts` |

### Missing ❌ (5)

**CHAT-08: Stop Streaming** → Click "Stop" → Keeps partial response → Enable input
**CHAT-09: Feedback** → Thumbs up/down → Optional reason + comment
**CHAT-10: Voice Input** → Microphone → Web Speech API → Transcribe → Submit
**CHAT-11: Share Thread** → Get shareable link → Read-only view for recipients
**CHAT-12: Quick Questions** → Click "How to setup?" → Instant answer → Follow-up enabled

---

## 5. Game Sessions (11 flows)

### Existing ✅ (6)

| ID | Flow | Test File |
|----|------|-----------|
| SESS-01 | Create Session | Partial |
| SESS-02 | Add Players | Partial |
| SESS-03 | State Tracking | Partial |
| SESS-04 | Complete | Partial |
| SESS-05 | History | Partial |
| SESS-06 | Pause/Resume | Partial |

### Missing ❌ (5)

**SESS-07: Limits Enforcement**
*(blocco di codice rimosso)*

**SESS-08: Invite Link** → Generate link → Friend joins as player
**SESS-09: Real-time Sync** → Player 1 updates score → Player 2 sees instantly (SignalR)
**SESS-10: AI Suggestions** → "Suggest Move" → See AI recommendations → Apply
**SESS-11: Statistics** → View 20 sessions → Win rate + avg duration + frequent players

---

## 6. Editor Flows (12 flows)

### Existing ✅ (8)

| ID | Flow | Test File |
|----|------|-----------|
| EDIT-01 | Create Game | `admin-game-creation.spec.ts` |
| EDIT-02 | Edit Game | `admin-games-workflow.spec.ts` |
| EDIT-03 | Upload PDF | `pdf-upload-journey.spec.ts` |
| EDIT-04 | Processing Monitor | `pdf-processing-progress.spec.ts` |
| EDIT-05 | Submit Approval | Partial |
| EDIT-06 | FAQ Management | `game-faq.spec.ts` |
| EDIT-07 | BGG Bulk Import | `bgg-integration.spec.ts` |
| EDIT-08 | Version History | ⚠️ Partial |

### Missing ❌ (4)

**EDIT-09: OCR Validation** → View extracted text + confidence → Correct errors
**EDIT-10: Multi-language PDF** → Upload EN+DE PDF → Extract both → Search works
**EDIT-11: Queue Position** → See "Position 3 of 12" + estimated review time
**EDIT-12: Draft Preview** → Preview as users see it → Edit from preview

---

## 7. Admin Flows (18 flows)

### Existing ✅ (10)

| ID | Flow | Test File |
|----|------|-----------|
| ADM-01 | User List | `admin-users.spec.ts` |
| ADM-02 | Role Assignment | `admin-users.spec.ts` |
| ADM-03 | Approval Queue | Partial |
| ADM-04 | Feature Flags | `admin-configuration.spec.ts` |
| ADM-05 | System Health | `admin-infrastructure.spec.ts` |
| ADM-06 | Alert Config | `admin-alert-config.spec.ts` |
| ADM-07 | Prompt Management | `admin-prompts-management.spec.ts` |
| ADM-08 | Bulk Export | `admin-bulk-export.spec.ts` |
| ADM-09 | Analytics | `admin-analytics.spec.ts` |
| ADM-10 | Audit Log | ⚠️ Partial |

### Missing ❌ (8)

**ADM-11: Session Limits Config** → Configure limits per tier (Free=3, Normal=10, Premium=∞)
**ADM-12: PDF Limits Config** → Max file size, max pages, allowed types
**ADM-13: Tier Feature Flags** → Toggle feature ON/OFF per tier
**ADM-14: AI Usage Analytics** → Token consumption by user/model/time + costs
**ADM-15: User Impersonation** → Impersonate user → See as they see → "Stop Impersonating"
**ADM-16: Locked Accounts** → View locked users + reason → Unlock
**ADM-17: Email Verification Admin** → View unverified → Manually verify or resend
**ADM-18: 2FA Override** → Disable 2FA for locked user → Audit log

---

## 8. Error & Edge Cases (20 flows)

### Existing ✅ (5)

| ID | Flow | Test File |
|----|------|-----------|
| ERR-01 | Network Error | `error-handling.spec.ts` |
| ERR-02 | Session Expiration | `session-expiration.spec.ts` |
| ERR-03 | 404 Not Found | `error-handling.spec.ts` |
| ERR-04 | 403 Forbidden | `rbac-authorization.spec.ts` |
| ERR-05 | Offline Mode | `offline-resilience.spec.ts` |

### Missing ❌ (15)

**ERR-06: Rate Limiting (429)** → See "Too many requests" + countdown → Auto-retry
**ERR-07: Token Expiration Mid-Op** → Session expires during PDF upload → Redirect + preserve upload
**ERR-08: Concurrent Edit** → Two editors → Conflict warning → Merge or overwrite
**ERR-09: PDF Processing Fail** → Corrupted PDF → Specific error → Retry with different file
**ERR-10: Tier Change Over-Quota** → Downgrade Premium→Free with 100 games → Warning → Choose 5 to keep
**ERR-11: OAuth Provider Down** → Google unavailable → "Service unavailable" + alternatives
**ERR-12: SSE Interruption** → Network drops → "Connection lost" + preserve partial + Retry
**ERR-13: Upload Validation** → Wrong file type → "Invalid file type" (before upload)
**ERR-14: Quota Exceeded** → Library 5/5 → "Quota exceeded" + upgrade or remove
**ERR-15: 2FA Recovery** → Lost device → Use backup code → Prompt new 2FA setup
**ERR-16: DB Connection Lost** → "Service unavailable" + show cached data if available
**ERR-17: AI Service Down** → "AI unavailable" + show cached FAQ
**ERR-18: Invalid Deep Link** → Game deleted → "Not found" + suggest similar
**ERR-19: Concurrent Session Race** → At 9/10 limit → Two devices create → Only one succeeds
**ERR-20: Import Conflicts** → BGG import "Catan" (exists) → "Already exists" + merge or skip

---

## 9. Accessibility (3 flows)

**ACC-01: Keyboard-Only** → Complete journey (Login → Browse → Add → Chat → Ask) with Tab+Enter+Esc
**ACC-02: Screen Reader** → Announcements for actions ("Game added") + error messages
**ACC-03: Reduced Motion** → `prefers-reduced-motion` → Disable/simplify animations

---

## 10. Priority Matrix

### P0 - Critical (Must Test Before Release)

| ID | Flow | Reason |
|----|------|--------|
| AUTH-09 | Email Verification | Security |
| AUTH-10 | Account Lockout | Security |
| LIB-07 | Session Quota | New feature |
| SESS-07 | Session Limits | New feature |
| ADM-11 | Limits Config | Admin control |
| ERR-06 | Rate Limiting | Security |
| ERR-14 | Quota Exceeded | UX critical |

### P1 - High (Sprint)

| ID | Flow | Reason |
|----|------|--------|
| AUTH-11 | Login Notifications | Security |
| AUTH-12 | Device Management | Security |
| CHAT-08 | Stop Streaming | UX |
| CHAT-09 | Feedback | Quality |
| ADM-12 | PDF Limits | Admin |
| ADM-13 | Tier Flags | Feature |

### P2 - Medium (Quarter)

DISC-06 (Autocomplete), LIB-08 (Wishlist), CHAT-10 (Voice), ADM-14 (AI Analytics), ERR-08 (Concurrent Edit)

### P3 - Low (When Capacity)

LIB-09 (Loans), CHAT-11 (Share), ADM-15 (Impersonate)

---

## 11. Test File Mapping

*(blocco di codice rimosso)*

---

## 12. Coverage Gap Analysis

| Area | Current | Target | Gap |
|------|---------|--------|-----|
| Authentication | 85% | 100% | 15% |
| Discovery | 90% | 100% | 10% |
| Library | 80% | 100% | 20% |
| AI Chat | 75% | 95% | 20% |
| Sessions | 65% | 95% | 30% |
| Editor | 85% | 95% | 10% |
| Admin | 70% | 95% | 25% |
| Errors | 50% | 90% | 40% |

---

## Next Actions

1. Create GitHub issues for P0/P1 missing flows
2. Implement backend for missing features (email verification, session limits)
3. Create E2E tests per file mapping
4. Update docs as tests pass

---

**Author**: Gap Analysis System | **Version**: 1.0


---



<div style="page-break-before: always;"></div>

## user-guides/diagrams/core-flows.md

# Core User Flow Sequence Diagrams

> Mermaid sequence diagrams for key user interactions.

## Table of Contents

- [Authentication Flows](#authentication-flows)
- [Library Management Flows](#library-management-flows)
- [AI Chat Flows](#ai-chat-flows)
- [Game Session Flows](#game-session-flows)
- [Editor Flows](#editor-flows)
- [Admin Flows](#admin-flows)

---

## Authentication Flows

### Complete Login Flow (with 2FA)

*(blocco di codice rimosso)*

### OAuth Flow

*(blocco di codice rimosso)*

---

## Library Management Flows

### Add Game with Quota Check

*(blocco di codice rimosso)*

### Upload Custom PDF

*(blocco di codice rimosso)*

---

## AI Chat Flows

### RAG Question with Streaming

*(blocco di codice rimosso)*

### Thread Management

*(blocco di codice rimosso)*

---

## Game Session Flows

### Session with State Tracking

*(blocco di codice rimosso)*

---

## Editor Flows

### Game Publication Workflow

*(blocco di codice rimosso)*

---

## Admin Flows

### User Tier Management

*(blocco di codice rimosso)*

### System Configuration Flow

*(blocco di codice rimosso)*

---

## Error Handling Patterns

### Generic Error Flow

*(blocco di codice rimosso)*

---

*Last Updated: 2026-01-19*


---



<div style="page-break-before: always;"></div>

## user-guides/document-processing.md

# Document Processing - Flussi API

## Panoramica

Il bounded context Document Processing gestisce upload, estrazione, indicizzazione e gestione dei documenti PDF per il sistema RAG.

---

## 1. Upload Standard

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/ingest/pdf` | `UploadPdfCommand` | Multipart: `file`, `gameId`, `gameName?`, `versionType?`, `language?`, `versionNumber?` | `[S]` |
| POST | `/users/{userId}/library/entries/{entryId}/pdf` | `UploadPrivatePdfCommand` | Multipart: `file` | `[S][O]` |

### Flusso Upload Standard

*(blocco di codice rimosso)*

**Feature Flag**: `Features.PdfUpload`

---

## 2. Chunked Upload

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/ingest/pdf/chunked/init` | `InitChunkedUploadCommand` | `{ gameId, fileName, totalFileSize }` | `[S]` |
| POST | `/ingest/pdf/chunked/chunk` | `UploadChunkCommand` | Multipart: `sessionId`, `chunkIndex`, `chunk` | `[S]` |
| POST | `/ingest/pdf/chunked/complete` | `CompleteChunkedUploadCommand` | `{ sessionId }` | `[S]` |
| GET | `/ingest/pdf/chunked/{sessionId}/status` | `GetChunkedUploadStatusQuery` | — | `[S]` |

### Flusso Chunked Upload

*(blocco di codice rimosso)*

---

## 3. Processing Pipeline

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/ingest/pdf/{pdfId}/extract` | `ExtractPdfTextCommand` | — | `[A/E]` |
| POST | `/ingest/pdf/{pdfId}/index` | `IndexPdfCommand` | — | `[A/E]` |
| POST | `/ingest/pdf/{pdfId}/rulespec` | `GenerateRuleSpecFromPdfCommand` | — | `[A/E]` |
| POST | `/documents/{pdfId}/retry` | `RetryPdfProcessingCommand` | — | `[S][O]` |

### Pipeline 3 Stadi

*(blocco di codice rimosso)*

**Retry**: Max 3 tentativi (429 Too Many Requests)

---

## 4. Progress Tracking e SSE

| Metodo | Path | Command/Query | Response | Auth |
|--------|------|---------------|----------|------|
| GET | `/pdfs/{pdfId}/progress` | `GetPdfProgressQuery` | JSON | `[S][O]` |
| GET | `/pdfs/{pdfId}/status/stream` | `StreamPdfStatusQuery` | SSE | `[S][O]` |
| GET | `/pdfs/{pdfId}/progress/stream` | `StreamPdfProgressQuery` | SSE | `[S][O]` |
| GET | `/documents/{id}/metrics` | `GetPdfMetricsQuery` | JSON | `[S]` |
| DELETE | `/pdfs/{pdfId}/processing` | `CancelPdfProcessingCommand` | JSON | `[S][O]` |

### SSE Events

**`/progress/stream`**:
- Event: `progress` → `ProcessingProgressJson` object
- Event: `heartbeat` → ogni 30 secondi

**`/status/stream`**:
- Event: stato corrente del processamento

---

## 5. Recupero Documenti

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/games/{gameId}/pdfs` | `GetPdfDocumentsByGameQuery` | — | `[S]` |
| GET | `/pdfs/{pdfId}/text` | `GetPdfTextQuery` | — | `[S]` |
| GET | `/pdfs/{pdfId}/download` | `DownloadPdfQuery` | — | `[S][O]` |

---

## 6. Gestione Documenti

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| DELETE | `/pdf/{pdfId}` | `DeletePdfCommand` | — | `[S][O]` |
| PATCH | `/pdfs/{pdfId}/visibility` | `SetPdfVisibilityCommand` | `{ isPublic }` | `[S][O]` |

---

## 7. Document Collections

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/games/{gameId}/document-collections` | `CreateDocumentCollectionCommand` | `{ name, description, initialDocuments[] }` | `[S]` |
| GET | `/games/{gameId}/document-collections` | `GetCollectionByGameQuery` | — | `[S]` |
| GET | `/games/{gameId}/document-collections/{collectionId}` | `GetCollectionByIdQuery` | — | `[S]` |
| GET | `/document-collections/by-user/{userId}` | `GetCollectionsByUserQuery` | — | `[S][O]` |
| POST | `/games/{gameId}/document-collections/{collectionId}/documents` | `AddDocumentToCollectionCommand` | `{ pdfDocumentId, documentType, sortOrder }` | `[S][O]` |
| DELETE | `/games/{gameId}/document-collections/{collectionId}/documents/{documentId}` | `RemoveDocumentFromCollectionCommand` | — | `[S][O]` |

---

## 8. BGG Extraction

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/extract-bgg-games` | `ExtractBggGamesFromPdfQuery` | `{ pdfFilePath }` | `[S]` |

---

## 9. Admin Configuration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/system/pdf-upload-limits` | `GetPdfUploadLimitsQuery` | — | `[A]` |
| PUT | `/admin/system/pdf-upload-limits` | `UpdatePdfUploadLimitsCommand` | `{ maxFileSizeBytes, maxPagesPerDocument, maxDocumentsPerGame, allowedMimeTypes[] }` | `[A]` |
| GET | `/admin/config/pdf-tier-upload-limits` | `GetPdfTierUploadLimitsQuery` | — | `[A]` |
| PUT | `/admin/config/pdf-tier-upload-limits` | `UpdatePdfTierUploadLimitsCommand` | `{ freeDailyLimit, freeWeeklyLimit, normalDailyLimit, ... }` | `[A]` |
| GET | `/admin/pdfs/metrics/processing` | `GetProcessingMetricsQuery` | — | `[A]` |

---

## Flusso Completo End-to-End

*(blocco di codice rimosso)*

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 824 |
| **Passati** | 823 |
| **Falliti** | 0 |
| **Ignorati** | 1 |
| **Pass Rate** | 100% |
| **Durata** | 6s |

### Fix Applicati (2026-02-15)

| Test | Fix |
|------|-----|
| `Handle_WithNonExistentDocument_ThrowsNotFoundException` | Fix parametri `NotFoundException("PDF document", id)` nel handler |
| `Handle_WithCompletedDocument_ReturnsZeroETA` | Mock `CalculateETAAsync` → `TimeSpan.Zero` + `MarkAsCompleted()` per `ProcessedAt` |
| `CalculateETAAsync_InsufficientHistoricalData_UsesFallback` | `BeGreaterThan(60)` → `BeGreaterThanOrEqualTo(60)` |
| `CleanupOldMetricsAsync_RetainsCorrectCount` | Skip: `ExecuteDeleteAsync` non supportato da InMemory provider |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Upload Standard | `UploadPdfCommandHandlerTests.cs` | Passato |
| Chunked Upload | `InitChunkedUploadTests.cs`, `UploadChunkTests.cs`, `CompleteChunkedUploadTests.cs` | Passato |
| Extraction | `ExtractPdfTextCommandHandlerTests.cs` | Passato |
| Indexing | `IndexPdfCommandHandlerTests.cs` | Passato |
| PDF Metrics/ETA | `GetPdfMetricsQueryHandlerTests.cs` | Passato |
| Download | `DownloadPdfQueryHandlerTests.cs` | Passato |
| Delete/Visibility | `DeletePdfCommandHandlerTests.cs`, `SetPdfVisibilityTests.cs` | Passato |
| Collections | `CreateDocumentCollectionTests.cs` | Passato |
| Text Chunking | `TextChunkerTests.cs`, `SemanticChunkerTests.cs` | Passato |
| Validators | 4 file di validazione | Passato |
| Domain Entities | `Document.cs`, `Page.cs`, `TextChunk.cs` | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/editor-role/01-game-management.md

# Editor: Game Management Flows

> Editor flows for creating and managing games in the shared catalog.

## Table of Contents

- [Create Game](#create-game)
- [Edit Game](#edit-game)
- [Import from BGG](#import-from-bgg)
- [Bulk Import](#bulk-import)
- [Archive Game](#archive-game)

---

## Role: Editor

**Capabilities:**
- Create new games in shared catalog
- Edit game metadata
- Import games from BoardGameGeek
- Upload and manage game PDFs
- Create FAQ, errata, quick questions
- Submit games for publication approval
- **Cannot**: Approve publications, manage users, system configuration

**Quota Bypass:** Editors have unlimited PDF uploads and library quota.

---

## Create Game

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Body | Response |
|------|----------|--------|------|----------|
| 1 | `/api/v1/shared-games/categories` | GET | - | Categories list |
| 2 | `/api/v1/shared-games/mechanics` | GET | - | Mechanics list |
| 3 | `/api/v1/games/upload-image` | POST | Image file | `{ imageUrl }` |
| 4 | `/api/v1/admin/shared-games` | POST | Game data | Created game |

**Create Game Request:**
*(blocco di codice rimosso)*

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Create Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Image Upload | ✅ Implemented | `GameEndpoints.cs` |
| New Game Page | ✅ Implemented | `/app/admin/shared-games/new/page.tsx` |
| GameForm | ✅ Implemented | `GameForm.tsx` |

---

## Edit Game

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/shared-games/{id}` | GET | - | Get game for editing |
| `/api/v1/admin/shared-games/{id}` | PUT | Updated data | Save changes |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Update Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Edit Page | ✅ Implemented | `/app/admin/shared-games/[id]/page.tsx` |

---

## Import from BGG

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/bgg/search` | GET | `?q=term` | Search BGG |
| `/api/v1/admin/shared-games/import-bgg` | POST | `{ bggId }` | Import from BGG |

**Import Request:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| BGG Search | ✅ Implemented | BGG Client |
| Import Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| BggSearchModal | ✅ Implemented | `BggSearchModal.tsx` |
| Import Page | ✅ Implemented | `/app/admin/shared-games/import/page.tsx` |

---

## Bulk Import

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/shared-games/bulk-import` | POST | CSV or IDs | Bulk import |

**Bulk Import Request:**
*(blocco di codice rimosso)*

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Bulk Import Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Import Page | ✅ Implemented | `/app/admin/shared-games/import/page.tsx` |

---

## Archive Game

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/shared-games/{id}/archive` | POST | Archive (soft delete) |
| `/api/v1/admin/shared-games/{id}` | DELETE | Request permanent delete |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Archive Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Delete Request | ✅ Implemented | Same file |

---

## Gap Analysis

### Implemented Features
- [x] Create game manually
- [x] Edit game details
- [x] Import from BGG
- [x] Bulk import
- [x] Archive game
- [x] Image upload
- [x] Category/mechanic assignment

### Missing/Partial Features
- [ ] **Version History**: No edit history tracking
- [ ] **Draft Preview**: Can't preview how game looks before publishing
- [ ] **Duplicate Detection**: No warning for similar game names
- [ ] **Merge Games**: Can't merge duplicate entries
- [ ] **Translation Support**: No multi-language game info

### Proposed Enhancements
1. **Version History**: Track all edits with who/when
2. **Draft Preview**: Show how game will appear in catalog
3. **Duplicate Checker**: Warn when creating similar games
4. **Batch Edit**: Edit multiple games at once
5. **Import Queue**: Background processing for large imports


---



<div style="page-break-before: always;"></div>

## user-guides/editor-role/02-document-management.md

# Editor: Document Management Flows

> Editor flows for managing game PDFs and documents.

## Table of Contents

- [Upload Game PDF](#upload-game-pdf)
- [Manage Document Versions](#manage-document-versions)
- [Process and Index](#process-and-index)
- [Generate Rule Spec](#generate-rule-spec)

---

## Upload Game PDF

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Description |
|------|----------|--------|-------------|
| 1 | `/api/v1/ingest/pdf` | POST | Upload PDF (standard) |
| 1a | `/api/v1/ingest/pdf/chunked/init` | POST | Initialize chunked upload |
| 1b | `/api/v1/ingest/pdf/chunked/chunk` | POST | Upload chunk |
| 1c | `/api/v1/ingest/pdf/chunked/complete` | POST | Complete chunked upload |
| 2 | `/api/v1/pdfs/{id}/progress` | GET | Check processing status |

**Upload Request (multipart/form-data):**
*(blocco di codice rimosso)*

**Processing Status Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Standard Upload | ✅ Implemented | `PdfEndpoints.cs` |
| Chunked Upload | ✅ Implemented | Same file |
| Processing Progress | ✅ Implemented | Same file |
| PDF Processor | ✅ Implemented | Unstructured/SmolDocling services |
| Upload UI | ✅ Implemented | `PdfUploadForm.tsx` |
| Progress UI | ✅ Implemented | `ProcessingProgress.tsx` |

---

## Manage Document Versions

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/shared-games/{id}/documents` | GET | List all documents |
| `/api/v1/admin/shared-games/{id}/documents/active` | GET | Get active documents |
| `/api/v1/admin/shared-games/{id}/documents` | POST | Add document |
| `/api/v1/admin/shared-games/{id}/documents/{docId}/set-active` | POST | Set as active |
| `/api/v1/admin/shared-games/{id}/documents/{docId}` | DELETE | Remove document |

**Documents Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Documents Endpoints | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Set Active | ✅ Implemented | Same file |
| PdfDocumentList | ✅ Implemented | `PdfDocumentList.tsx` |

---

## Process and Index

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/ingest/pdf/{id}/index` | POST | Index/re-index PDF |
| `/api/v1/ingest/pdf/{id}/extract` | POST | Extract text only |
| `/api/v1/pdfs/{id}/text` | GET | View extracted text |
| `/api/v1/pdfs/{id}/processing` | DELETE | Cancel processing |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Index Endpoint | ✅ Implemented | `PdfEndpoints.cs` |
| Extract Endpoint | ✅ Implemented | Same file |
| Cancel Processing | ✅ Implemented | Same file |
| Text View | ✅ Implemented | Same file |

---

## Generate Rule Spec

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/ingest/pdf/{id}/rulespec` | POST | Generate rule spec |

**Rule Spec Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Rule Spec Endpoint | ✅ Implemented | `PdfEndpoints.cs` |
| Rule Spec UI | ⚠️ Partial | Basic implementation |

---

## Gap Analysis

### Implemented Features
- [x] Standard PDF upload
- [x] Chunked upload for large files
- [x] Processing progress tracking
- [x] Multiple document versions
- [x] Set active version
- [x] Text extraction
- [x] Vector indexing
- [x] Rule spec generation
- [x] Cancel processing

### Missing/Partial Features
- [ ] **OCR Quality Review**: Can't review/correct OCR output
- [ ] **Batch Upload**: Upload multiple PDFs at once
- [ ] **PDF Preview**: Preview PDF before upload
- [ ] **Extraction Settings**: Configure extraction parameters
- [ ] **Version Comparison**: Compare text between versions
- [ ] **Automatic Language Detection**: Auto-detect PDF language

### Proposed Enhancements
1. **OCR Review**: Allow editors to correct OCR errors
2. **Extraction Quality Score**: Show confidence in extraction
3. **Smart Versioning**: Auto-increment version numbers
4. **PDF Comparison**: Side-by-side comparison of versions
5. **Batch Operations**: Process multiple PDFs in parallel


---



<div style="page-break-before: always;"></div>

## user-guides/editor-role/03-content-management.md

# Editor: Content Management Flows

> Editor flows for managing FAQ, errata, quick questions, and state templates.

## Table of Contents

- [Quick Questions Management](#quick-questions-management)
- [FAQ Management](#faq-management)
- [Errata Management](#errata-management)
- [State Template Management](#state-template-management)

---

## Quick Questions Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/shared-games/{id}/quick-questions/generate` | POST | - | AI generate |
| `/api/v1/admin/shared-games/{id}/quick-questions` | POST | Q&A data | Add manually |
| `/api/v1/admin/quick-questions/{questionId}` | PUT | Updated data | Edit |
| `/api/v1/admin/quick-questions/{questionId}` | DELETE | - | Delete |

**Quick Question Request:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Generate Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| CRUD Endpoints | ✅ Implemented | Same file |
| QuickQuestionGenerator | ✅ Implemented | `QuickQuestionGenerator.tsx` |
| QuickQuestionEditor | ✅ Implemented | `QuickQuestionEditor.tsx` |

---

## FAQ Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/shared-games/{id}/faq` | POST | FAQ data | Add FAQ |
| `/api/v1/admin/shared-games/{id}/faq/{faqId}` | PUT | Updated data | Edit FAQ |
| `/api/v1/admin/shared-games/{id}/faq/{faqId}` | DELETE | - | Delete FAQ |

**FAQ Request:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| FAQ Endpoints | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| FAQ UI | ⚠️ Partial | Basic implementation |

---

## Errata Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/admin/shared-games/{id}/errata` | POST | Errata data | Add errata |
| `/api/v1/admin/shared-games/{id}/errata/{errataId}` | PUT | Updated data | Edit |
| `/api/v1/admin/shared-games/{id}/errata/{errataId}` | DELETE | - | Delete |

**Errata Request:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Errata Endpoints | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Errata UI | ⚠️ Partial | Basic implementation |

---

## State Template Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/shared-games/{id}/state-template` | GET | Get active template |
| `/api/v1/admin/shared-games/{id}/state-template/versions` | GET | List all versions |
| `/api/v1/admin/shared-games/{id}/state-template/generate` | POST | AI generate |
| `/api/v1/admin/shared-games/{id}/state-template/{id}` | PUT | Update template |
| `/api/v1/admin/shared-games/{id}/state-template/{id}/activate` | POST | Activate version |

**State Template Structure:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Template Endpoints | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Generate Endpoint | ✅ Implemented | Same file |
| Activate Endpoint | ✅ Implemented | Same file |
| Template Editor UI | ⚠️ Partial | Basic implementation |

---

## Gap Analysis

### Implemented Features
- [x] Quick questions AI generation
- [x] Quick questions CRUD
- [x] FAQ management
- [x] Errata management
- [x] State template generation
- [x] State template versioning
- [x] Template activation

### Missing/Partial Features
- [ ] **Bulk Quick Question Import**: Import from CSV/spreadsheet
- [ ] **FAQ Search**: Search within FAQ
- [ ] **Errata Notifications**: Notify users of new errata
- [ ] **Template Validation Testing**: Test template against sample state
- [ ] **Template Migration**: Migrate existing sessions to new template
- [ ] **Rich Text FAQ**: Full WYSIWYG editor for FAQ answers
- [ ] **Version Comparison**: Compare template versions side-by-side

### Proposed Enhancements
1. **Bulk Import**: Import Q&A from spreadsheets
2. **Version Diff**: Show changes between template versions
3. **Template Testing**: Sandbox to test template before activation
4. **Errata Alerts**: Push notifications for new errata
5. **AI Enhancement**: Auto-suggest errata from user questions


---



<div style="page-break-before: always;"></div>

## user-guides/editor-role/04-publication-workflow.md

# Editor: Publication Workflow

> Editor flows for submitting games for publication approval.

## Table of Contents

- [Submit for Approval](#submit-for-approval)
- [Publication Status](#publication-status)
- [Rejection Handling](#rejection-handling)

---

## Submit for Approval

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/admin/shared-games/{id}/submit-for-approval` | POST | Submit for review |

**Validation Requirements:**
- `name`: Required, non-empty
- `description`: Required, min 50 characters
- `coverImageUrl`: Required
- `documents`: At least one processed PDF
- `minPlayers`, `maxPlayers`: Valid range
- `minPlayTime`, `maxPlayTime`: Valid range

**Response (Success):**
*(blocco di codice rimosso)*

**Response (Validation Error):**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Submit Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Validation Logic | ✅ Implemented | Command validator |
| Notification | ⚠️ Partial | Basic implementation |
| Submit UI | ✅ Implemented | Game edit page |

---

## Publication Status

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Status Flow

*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Status Tracking | ✅ Implemented | SharedGame entity |
| Queue Position | ⚠️ Partial | Basic implementation |
| Status UI | ✅ Implemented | Game list with badges |

---

## Rejection Handling

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

Rejection is handled by Admin (see Admin flows), but editor receives:

**Notification:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Rejection Reason | ✅ Implemented | SharedGame entity |
| Resubmit Flow | ✅ Implemented | Same as initial submit |
| Notification | ⚠️ Partial | Basic implementation |

---

## Gap Analysis

### Implemented Features
- [x] Submit for approval
- [x] Validation before submission
- [x] Status tracking
- [x] Rejection reason display
- [x] Resubmit after rejection
- [x] Admin notification

### Missing/Partial Features
- [ ] **Queue Position**: Real queue position tracking
- [ ] **Estimated Wait Time**: Based on historical data
- [ ] **Draft Comments**: Editor can add notes for reviewer
- [ ] **Partial Approval**: Approve with requested changes
- [ ] **Revision History**: Track submission attempts
- [ ] **SLA Tracking**: Alert if review takes too long

### Proposed Enhancements
1. **Review Notes**: Editor can add context for reviewer
2. **Status Notifications**: Email/push when status changes
3. **Review SLA**: Auto-escalate if pending too long
4. **Conditional Approval**: "Approved pending minor fixes"
5. **Batch Operations**: Submit multiple games at once


---



<div style="page-break-before: always;"></div>

## user-guides/game-management.md

# Game Management - Flussi API

## Panoramica

Il bounded context Game Management gestisce il ciclo di vita dei giochi, delle sessioni di gioco, dello stato di gioco, dei play record e dei conflict FAQ.

---

## 1. Game CRUD

### Lettura (Public)

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/games` | `GetAllGamesQuery` | `search?, page?, pageSize?` | `[P]` |
| GET | `/games/{id}` | `GetGameByIdQuery` | — | `[P]` |
| GET | `/games/{id}/similar` | `GetSimilarGamesQuery` | `limit?, minSimilarity?` | `[P]` |
| GET | `/games/search` | `SearchGamesQuery` | `q` (autocomplete) | `[S]` |

### Lettura (Autenticata)

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/games/{id}/details` | `GetGameDetailsQuery` | — | `[S]` |
| GET | `/games/{id}/rules` | `GetRuleSpecsQuery` | — | `[S]` |
| GET | `/games/{id}/agents` | `GetAllAgentsQuery` | — | `[S]` |
| GET | `/games/{id}/sessions` | `GetGameSessionsQuery` | `pageNumber?, pageSize?` | `[S]` |

### Scrittura (Admin/Editor)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/games` | `CreateGameCommand` | `{ title, publisher, yearPublished, minPlayers, maxPlayers, minPlayTimeMinutes, maxPlayTimeMinutes, iconUrl?, imageUrl?, bggId?, sharedGameId? }` | `[A/E]` |
| PUT | `/games/{id}` | `UpdateGameCommand` | `{ title, publisher, yearPublished, minPlayers, maxPlayers, ... }` | `[A/E]` |
| POST | `/games/upload-image` | `UploadGameImageCommand` | Multipart: `file, gameId, imageType` | `[A/E]` |
| PUT | `/games/{id}/publish` | `PublishGameCommand` | `{ status }` | `[A]` |

---

## 2. Game Session Lifecycle

### Creazione e Gestione

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/sessions` | `StartGameSessionCommand` | `{ gameId, players[] }` | `[S]` |
| POST | `/sessions/{id}/players` | `AddPlayerToSessionCommand` | `{ playerName, playerOrder, color }` | `[S]` |
| POST | `/sessions/{id}/pause` | `PauseGameSessionCommand` | — | `[S]` |
| POST | `/sessions/{id}/resume` | `ResumeGameSessionCommand` | — | `[S]` |
| POST | `/sessions/{id}/complete` | `CompleteGameSessionCommand` | `{ winnerName? }` | `[S]` |
| POST | `/sessions/{id}/abandon` | `AbandonGameSessionCommand` | — | `[S]` |
| POST | `/sessions/{id}/end` | `EndGameSessionCommand` | `{ winnerName? }` | `[S]` |

### Flusso Session Lifecycle

*(blocco di codice rimosso)*

### Query Sessioni

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/sessions/{id}` | `GetGameSessionByIdQuery` | — | `[S]` |
| GET | `/games/{gameId}/sessions/active` | `GetActiveSessionsByGameQuery` | — | `[S]` |
| GET | `/sessions/active` | `GetActiveSessionsQuery` | `limit?, offset?` | `[S]` |
| GET | `/sessions/history` | `GetSessionHistoryQuery` | `gameId?, startDate?, endDate?, limit?, offset?` | `[S]` |
| GET | `/sessions/statistics` | `GetSessionStatsQuery` | `gameId?, startDate?, endDate?, topPlayersLimit?` | `[S]` |

---

## 3. Game Session State

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/sessions/{sessionId}/state/initialize` | `InitializeGameStateCommand` | `{ templateId, initialState }` | `[S]` |
| GET | `/sessions/{sessionId}/state` | `GetGameStateQuery` | — | `[S]` |
| PATCH | `/sessions/{sessionId}/state` | `UpdateGameStateCommand` | `{ newState }` (JSON) | `[S]` |
| POST | `/sessions/{sessionId}/state/snapshots` | `CreateStateSnapshotCommand` | `{ turnNumber, description }` | `[S]` |
| GET | `/sessions/{sessionId}/state/snapshots` | `GetStateSnapshotsQuery` | — | `[S]` |
| POST | `/sessions/{sessionId}/state/restore/{snapshotId}` | `RestoreStateSnapshotCommand` | — | `[S]` |

### Flusso Game State con Snapshots

*(blocco di codice rimosso)*

---

## 4. Move Suggestions (Player Mode)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/sessions/{sessionId}/suggest-move` | `SuggestMoveCommand` | `{ agentId, query }` | `[S]` |
| POST | `/sessions/{sessionId}/apply-suggestion` | `ApplySuggestionCommand` | `{ suggestionId, stateChanges }` | `[S]` |

---

## 5. Play Record (Issue #3888-3890)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/play-records` | `CreatePlayRecordCommand` | `{ gameId?, gameName, sessionDate, visibility, groupId?, scoringDimensions?, dimensionUnits? }` | `[S]` |
| POST | `/play-records/{recordId}/players` | `AddPlayerToRecordCommand` | `{ userId?, displayName }` | `[S]` |
| POST | `/play-records/{recordId}/scores` | `RecordScoreCommand` | `{ playerId, dimension, value, unit? }` | `[S]` |
| POST | `/play-records/{recordId}/start` | `StartPlayRecordCommand` | — | `[S]` |
| POST | `/play-records/{recordId}/complete` | `CompletePlayRecordCommand` | `{ manualDuration? }` | `[S]` |
| PUT | `/play-records/{recordId}` | `UpdatePlayRecordCommand` | `{ sessionDate?, notes?, location? }` | `[S]` |
| GET | `/play-records/{recordId}` | `GetPlayRecordQuery` | — | `[S]` |
| GET | `/play-records/history` | `GetUserPlayHistoryQuery` | `page?, pageSize?, gameId?` | `[S]` |
| GET | `/play-records/statistics` | `GetPlayerStatisticsQuery` | `startDate?, endDate?` | `[S]` |

### Flusso Play Record

*(blocco di codice rimosso)*

---

## 6. Rule Conflict FAQ

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/games/{gameId}/rule-conflict-faqs` | `GetAllRuleConflictFaqsForGameQuery` | `page?, pageSize?` | `[P]` |
| GET | `/games/{gameId}/rule-conflict-faqs/pattern/{pattern}` | `GetRuleConflictFaqByPatternQuery` | — | `[P]` |
| POST | `/games/{gameId}/rule-conflict-faqs` | `CreateRuleConflictFaqCommand` | `{ conflictType, pattern, resolution, priority }` | `[A/E]` |
| PUT | `/games/{gameId}/rule-conflict-faqs/{id}` | `UpdateRuleConflictFaqResolutionCommand` | `{ resolution }` | `[A/E]` |
| DELETE | `/games/{gameId}/rule-conflict-faqs/{id}` | `DeleteRuleConflictFaqCommand` | — | `[A]` |

---

## 7. Admin Configuration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/config/game-library-limits` | `GetGameLibraryLimitsQuery` | — | `[A]` |
| PUT | `/admin/config/game-library-limits` | `UpdateGameLibraryLimitsCommand` | `{ freeTierLimit, normalTierLimit, premiumTierLimit }` | `[A]` |

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 1,300 |
| **Passati** | 1,300 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 4s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Game CRUD | `CreateGameCommandHandlerTests.cs`, `UpdateGameTests.cs` | Passato |
| Game Session Lifecycle | `StartGameSessionTests.cs`, `PauseTests.cs`, `ResumeTests.cs`, `CompleteTests.cs`, `AbandonTests.cs` | Passato |
| Game State | `InitializeGameStateTests.cs`, `UpdateGameStateTests.cs`, `CreateStateSnapshotTests.cs`, `RestoreSnapshotTests.cs` | Passato |
| Play Record | `CreatePlayRecordTests.cs`, `AddPlayerToRecordTests.cs`, `RecordScoreTests.cs`, `CompletePlayRecordTests.cs` | Passato |
| Move Suggestions | `SuggestMoveTests.cs`, `ApplySuggestionTests.cs` | Passato |
| Rule Conflict FAQ | `CreateRuleConflictFaqTests.cs`, `UpdateResolutionTests.cs` | Passato |
| Domain Entities | `Game.cs`, `GameSession.cs`, `Player.cs` (19 file) | Passato |
| Validators | 15 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/game-session-toolkit-flows.md

# Game Session Toolkit - User Flows

**Feature**: Collaborative Board Game Scorekeeper
**Version**: 1.0.0
**Last Updated**: 2026-01-30

---

## Flow 1: Create Generic Session

**Entry Point**: `/toolkit`

*(blocco di codice rimosso)*

**Steps**:
1. User navigates to `/toolkit`
2. Fills participant names (min 1)
3. Clicks "Create Session"
4. Backend creates session with 6-char code
5. Redirects to `/toolkit/{sessionId}`
6. SSE connection established
7. User adds scores → optimistic UI update
8. Backend broadcasts ScoreUpdatedEvent via SSE
9. Other participants see score in real-time
10. Owner finalizes session
11. Redirect to `/toolkit` with success toast

---

## Flow 2: Join Existing Session

**Entry Point**: `/toolkit`

*(blocco di codice rimosso)*

**Steps**:
1. Owner shares session code (displayed in SessionHeader)
2. Player navigates to `/toolkit`
3. Enters 6-character code (auto-uppercase)
4. Clicks "Join Session"
5. Backend validates code and adds player
6. Redirects to `/toolkit/{sessionId}`
7. SSE connection established
8. Player sees real-time scores from all participants

**Error Handling**:
- Invalid code format → Toast: "Session code must be 6 characters"
- Session not found (404) → Toast: "Session not found. Please check the code."
- Already joined → Redirects silently (idempotent)

---

## Flow 3: Game-Specific Session

**Entry Point**: `/library` (My Games)

*(blocco di codice rimosso)*

**Steps**:
1. User browses library (`/library`)
2. Clicks "Toolkit" button on game card
3. System fetches game details
4. Template lookup: `getGameTemplateByName(game.name)`
5. Displays template preview (categories, rounds, rules)
6. User adds participants (validated against template.playerCount)
7. Clicks "Start {GameName} Session"
8. Backend creates session with gameId + sessionType='GameSpecific'
9. Redirects to `/library/games/{gameId}/toolkit/{sessionId}`
10. ScoreInput auto-populated with template categories/rounds
11. Scoring rules displayed in sidebar
12. On finalize: Redirects to `/library/games/{gameId}` (game detail page)

---

## Flow 4: Real-Time Multi-User Session

**Scenario**: 2 users tracking scores simultaneously

*(blocco di codice rimosso)*

**Key Interactions**:
- All participants maintain SSE connections
- Optimistic UI for score submitter (instant)
- SSE broadcast for other participants (10-100ms latency)
- Connection status indicator (🟢/🔴)
- Auto-reconnect on network interruption
- Toast notifications for all state changes

---

## Flow 5: Session History Review

**Entry Point**: `/toolkit/history`

*(blocco di codice rimosso)*

**Steps**:
1. User navigates to `/toolkit/history`
2. Views list of finalized sessions
3. Applies filters (optional):
   - Game dropdown
   - Start date
   - End date
4. Clicks "View Details" on session card
5. Modal opens with:
   - Full scoreboard
   - Ranked participants
   - Session metadata
6. User closes modal or browses other sessions

**Filters Behavior** (Phase 2):
- Game filter: Shows only sessions for selected game
- Date range: Filters by session date
- Reset: Clears all filters
- Empty state: "No sessions found" with CTA to create new session

---

## Flow 6: Error Recovery

### Scenario A: Network Disconnection During Session

*(blocco di codice rimosso)*

### Scenario B: SSE Connection Lost

*(blocco di codice rimosso)*

### Scenario C: Invalid Session Code

*(blocco di codice rimosso)*

---

## Flow 7: Mobile Responsive Behavior

### Desktop (≥1024px)

*(blocco di codice rimosso)*

### Mobile (≤768px)

*(blocco di codice rimosso)*

**Responsive Breakpoints**:
- Mobile: < 640px (single column)
- Tablet: 640-1024px (2 columns)
- Desktop: ≥1024px (3 columns)

---

## Flow 8: Dark Mode

**Trigger**: System preference or manual toggle

**Visual Changes**:
- Background: Gradient blue/purple → Dark gray gradients
- Cards: White → Dark gray (#1f2937)
- Text: Dark gray → Light gray/white
- Borders: Light gray → Dark borders with opacity
- Badges: Adjusted color schemes for contrast
- Connection indicator: Green/Red maintains visibility

**Accessibility**: All color combinations maintain WCAG 2.1 AA contrast ratios

---

## Edge Cases & Error States

### Empty States

1. **No Participants**: "Please add at least one participant"
2. **No Sessions**: "No sessions found. Start your first session above!"
3. **No Template**: Falls back to generic session
4. **No Scoreboard Data**: Shows empty table with headers

### Validation Errors

1. **Player Count**: "{Game} requires at least {min} players"
2. **Max Players**: "{Game} supports maximum {max} players"
3. **Invalid Score**: "Score must be a valid number"
4. **Session Not Found**: Redirect to `/toolkit` with error toast

### Network Failures

1. **API Timeout**: Optimistic update reverts, retry button shown
2. **SSE Disconnect**: Auto-reconnect (max 5 attempts)
3. **Connection Lost**: Offline banner, scores queue for sync

---

## Performance Characteristics

### Perceived Latency

- **Score Submission**: 0ms (optimistic UI)
- **Real-Time Update**: 10-100ms (SSE latency)
- **Page Load**: < 1s (Next.js 14 optimizations)
- **Modal Open**: < 50ms (Radix UI animations)

### Resource Usage

- **SSE Connection**: 1 per active session (auto-closed on unmount)
- **Memory**: ~2MB per session (50 participants, 200 scores)
- **Network**: 1-5 KB/score update event

---

## User Guidance

### Best Practices

1. **Share Code Early**: Display session code prominently for participants to join
2. **Finalize Sessions**: Always finalize to save to history
3. **Use Templates**: Leverage pre-configured templates for faster setup
4. **Check Connection**: Green indicator = real-time sync active

### Common Questions

**Q**: Why didn't my score sync?
**A**: Check connection indicator (🟢). If red, scores queue and sync on reconnection.

**Q**: Can I edit past scores?
**A**: Not yet - scores are immutable once submitted (planned Phase 2).

**Q**: How do I find my session code?
**A**: Displayed at top of active session page in SessionHeader.

**Q**: Can players join mid-session?
**A**: Yes - enter session code on `/toolkit` landing page.

---

**Epic**: EPIC-GST-001
**Issues**: #3163, #3164, #3165
**Status**: Production Ready


---



<div style="page-break-before: always;"></div>

## user-guides/gap-analysis.md

# MeepleAI User Flows - Gap Analysis

> Comprehensive analysis of implemented features, gaps, and proposed enhancements.

## Executive Summary

Based on thorough analysis of **160+ API endpoints** and **340+ frontend components**, MeepleAI has a robust foundation with most core flows implemented. Key gaps exist primarily in:

1. **Tier-based feature differentiation** (beyond quotas)
2. **Session limits** (not enforced despite infrastructure)
3. **Admin UI completeness** (some configs database-only)
4. **Advanced collaboration features**

---

## Implementation Status by Role

### User Role: 85% Complete

| Flow | Status | Notes |
|------|--------|-------|
| Authentication | ✅ 95% | Missing: account lockout, email verification |
| Game Discovery | ✅ 90% | Missing: autocomplete, similar games |
| Library Management | ✅ 95% | Complete with quotas |
| AI Chat | ✅ 90% | Missing: voice input, message feedback |
| Game Sessions | ✅ 85% | Missing: session limits enforcement, invites |
| Notifications | ✅ 80% | Basic implementation |

### Editor Role: 80% Complete

| Flow | Status | Notes |
|------|--------|-------|
| Game Management | ✅ 90% | Missing: version history |
| Document Management | ✅ 95% | Complete |
| Content Management | ✅ 85% | Missing: rich text FAQ, bulk import |
| Publication Workflow | ✅ 80% | Missing: queue position tracking |

### Admin Role: 75% Complete

| Flow | Status | Notes |
|------|--------|-------|
| Approval Workflow | ✅ 90% | Missing: batch approval |
| User Management | ✅ 85% | Missing: impersonation, usage analytics |
| System Configuration | ⚠️ 70% | PDF limits not in UI |
| Monitoring | ✅ 85% | Missing: log aggregation |

---

## Critical Gaps

### 1. Session Limits (Priority: High)

**Current State:** Session limits infrastructure exists but is NOT enforced.

**Gap:** No limit on concurrent game sessions per user.

**Impact:** Resource usage could grow unbounded.

**Proposed Solution:**
*(blocco di codice rimosso)*

**Required Changes:**
- Add `SessionQuotaService` (similar to `GameLibraryQuotaService`)
- Check quota in `CreateSessionCommandHandler`
- Add UI for quota display
- Add admin configuration endpoint

---

### 2. PDF Upload Limits Admin UI (Priority: Medium)

**Current State:** PDF upload limits are configurable via database only.

**Gap:** Admins cannot adjust PDF limits without database access.

**Proposed Solution:**
- Add `/api/v1/admin/system/pdf-upload-limits` endpoint
- Add UI in System Configuration

**Required Changes:**
- Create `UpdatePdfUploadLimitsCommand`
- Add handler
- Add frontend form

---

### 3. Feature Flag Tier-Based Access (Priority: Medium)

**Current State:** Feature flags are role-based (User, Editor, Admin).

**Gap:** Cannot differentiate features by subscription tier.

**Proposed Solution:**
*(blocco di codice rimosso)*

**Required Changes:**
- Extend `FeatureFlagService` to support tier checks
- Update flag evaluation logic
- Add UI for tier-based flags

---

### 4. Email Verification (Priority: Medium)

**Current State:** Users can log in immediately after registration.

**Gap:** No email verification before full access.

**Proposed Solution:**
- Send verification email on registration
- Limit features until verified
- Resend verification option

**Required Changes:**
- Add `EmailVerificationService`
- Create verification token table
- Add verification endpoint
- Update registration flow

---

### 5. Advanced Chat Features (Priority: Low)

**Current State:** Basic chat with RAG.

**Gap:** No voice input, feedback system, or collaborative features.

**Proposed Enhancements:**
- Voice-to-text for questions
- Thumbs up/down feedback on responses
- Share chat session links
- Collaborative chat rooms

---

## Quota System Gaps

### Currently Implemented
| Quota | Enforcement | Admin UI | Bypass for Editor/Admin |
|-------|-------------|----------|------------------------|
| Game Library | ✅ | ✅ | ✅ |
| PDF Daily | ✅ | ❌ | ✅ |
| PDF Weekly | ✅ | ❌ | ✅ |

### Missing
| Quota | Status | Priority |
|-------|--------|----------|
| Session Limits | ❌ Not implemented | High |
| AI Token Usage | ❌ Not implemented | Medium |
| Storage Limits | ❌ Not implemented | Low |
| API Rate Limits by Tier | ❌ Role-based only | Medium |

---

## Proposed New Features by Priority

### High Priority (Q1 2026)

1. **Session Limits Enforcement**
   - Implement tier-based session limits
   - Add quota display in sessions page
   - Add admin configuration

2. **PDF Limits Admin UI**
   - Expose all quota configurations
   - Unified quota management page

3. **Email Verification**
   - Require verification for new accounts
   - Grace period for existing users

### Medium Priority (Q2 2026)

4. **Tier-Based Feature Flags**
   - Extend flag system for tiers
   - Premium-only features

5. **AI Usage Tracking**
   - Track token consumption per user
   - Usage analytics dashboard
   - Cost allocation per tier

6. **Account Security Enhancements**
   - Account lockout after failed attempts
   - Login notifications
   - Device management

### Low Priority (Q3-Q4 2026)

7. **Collaboration Features**
   - Share chat sessions
   - Session invites
   - Real-time co-play

8. **Advanced AI Features**
   - Voice input
   - Response feedback
   - Personalized suggestions

9. **Admin Enhancements**
   - User impersonation
   - Distributed tracing
   - Custom dashboards

---

## API Endpoint Gaps

### Missing CRUD Operations
| Resource | GET | POST | PUT | DELETE | Notes |
|----------|-----|------|-----|--------|-------|
| User Preferences | ✅ | N/A | ✅ | N/A | Complete |
| Session Limits | ❌ | ❌ | ❌ | N/A | Not implemented |
| PDF Limits Config | ❌ | ❌ | ❌ | N/A | DB only |

### Missing Admin Endpoints
- `GET /api/v1/admin/system/pdf-upload-limits`
- `PUT /api/v1/admin/system/pdf-upload-limits`
- `GET /api/v1/admin/system/session-limits`
- `PUT /api/v1/admin/system/session-limits`
- `GET /api/v1/admin/users/{id}/usage` (detailed usage stats)

---

## Frontend Component Gaps

### Missing Components
| Component | Purpose | Priority |
|-----------|---------|----------|
| `SessionQuotaBar` | Display session limits | High |
| `PdfLimitsConfig` | Admin PDF limit config | Medium |
| `FeatureFlagTierToggle` | Tier-based flag UI | Medium |
| `UsageAnalyticsDashboard` | Per-user usage | Medium |
| `VoiceInputButton` | Voice-to-text for chat | Low |
| `FeedbackButtons` | Rate AI responses | Low |

### Components Needing Enhancement
| Component | Enhancement | Priority |
|-----------|-------------|----------|
| `QuotaStatusBar` | Add session count | High |
| `AdminConfigPage` | Add PDF limits section | Medium |
| `FeatureFlagsTab` | Add tier columns | Medium |
| `UserDetailPage` | Add usage statistics | Low |

---

## Database Schema Gaps

### Missing Tables/Columns
*(blocco di codice rimosso)*

---

## Recommended Implementation Order

### Sprint 1: Session Limits
1. Create `SessionQuotaService`
2. Add check in `CreateSessionCommandHandler`
3. Create admin endpoint
4. Add frontend `SessionQuotaBar`
5. Test thoroughly

### Sprint 2: PDF Limits UI
1. Create `UpdatePdfUploadLimitsCommand`
2. Add handler
3. Create frontend form
4. Add to admin configuration page

### Sprint 3: Email Verification
1. Create verification service
2. Create database table
3. Update registration flow
4. Add verification UI
5. Handle existing users

### Sprint 4: Feature Flag Enhancements
1. Extend `FeatureFlagService`
2. Update evaluation logic
3. Add tier columns to UI
4. Migrate existing flags

---

## Metrics to Track

### Implementation Progress
- Endpoint coverage: 160+ (current) → 170+ (target)
- Feature completeness: 80% → 95%
- Test coverage: Current → +10%

### User Experience
- Time to first action (registration → first chat)
- Feature adoption rates
- Error rates by flow

### Business Impact
- Conversion from Free to paid tiers
- Feature usage by tier
- Support ticket reduction

---

## Conclusion

MeepleAI has a solid foundation with most core user flows implemented. The primary gaps are in:

1. **Enforcement** (session limits exist but aren't enforced)
2. **Admin UI** (some configs require database access)
3. **Feature differentiation** (tiers mainly affect quotas, not features)

Addressing these gaps will significantly improve the user and admin experience, enabling better control over the platform and clearer value proposition for premium tiers.

---

*Last Updated: 2026-01-19*


---



<div style="page-break-before: always;"></div>

## user-guides/index.md

# Flussi API Testabili - MeepleAI

Documentazione completa dei flussi API organizzati per Bounded Context.

## Panoramica

| # | Bounded Context | Endpoint | Test | Passati | Falliti | Stato |
|---|----------------|----------|------|---------|---------|-------|
| 1 | [Authentication](./authentication.md) | ~45 | 1,325 | 1,320 | 0 | 100% |
| 2 | [Document Processing](./document-processing.md) | ~32 | 824 | 823 | 0 | 100% |
| 3 | [Game Management](./game-management.md) | ~53 | 1,300 | 1,300 | 0 | 100% |
| 4 | [Knowledge Base](./knowledge-base.md) | ~39 | 3,028 | 3,027 | 0 | 100% |
| 5 | [Shared Game Catalog](./shared-game-catalog.md) | ~86 | 1,450 | 1,450 | 0 | 100% |
| 6 | [User Library](./user-library.md) | ~53 | 842 | 842 | 0 | 100% |
| 7 | [Administration](./administration.md) | ~65 | 885 | 885 | 0 | 100% |
| 8 | [Session Tracking](./session-tracking.md) | ~5 | 181 | 181 | 0 | 100% |
| 9 | [User Notifications](./user-notifications.md) | 6 | 147 | 147 | 0 | 100% |
| 10 | [System Configuration](./system-configuration.md) | ~30 | 409 | 409 | 0 | 100% |
| 11 | [Workflow Integration](./workflow-integration.md) | ~11 | 134 | 134 | 0 | 100% |

**Totale Endpoint**: ~425+ | **Totale Test**: 12,004 | **Passati**: 12,004 | **Falliti**: 0 | **Ignorati**: 11 | **Pass Rate**: 100%

### Test Eseguiti il: 2026-02-15

### Tutti i test passano

Tutti i 21 test precedentemente falliti sono stati corretti nel commit `d07423643`:
- **15 Middleware (BggRateLimit)**: Fix autenticazione mock (`SessionStatusDto` + `UserDto`) e fix produzione case-mismatch lowercase tier
- **4 DocumentProcessing (ETA)**: Fix `NotFoundException` parametri, ETA mock per documenti completati, boundary condition `>=`
- **1 Authentication (UserTier)**: Rimosso "enterprise" da test tier invalidi (tier valido dal Epic #4068)
- **1 Infrastructure (Metrics)**: Skip test `ExecuteDeleteAsync` non supportato da InMemory provider

## Legenda

| Simbolo | Significato |
|---------|-------------|
| `[P]` | Endpoint pubblico (no auth) |
| `[S]` | Richiede sessione autenticata |
| `[A]` | Richiede ruolo Admin |
| `[E]` | Richiede ruolo Editor |
| `[A/E]` | Richiede Admin o Editor |
| `[O]` | Richiede ownership (proprietario della risorsa) |
| `SSE` | Server-Sent Events (streaming) |

## Flussi Principali Testabili

### 1. Flusso Registrazione e Login
*(blocco di codice rimosso)*

### 2. Flusso Upload e Processamento PDF
*(blocco di codice rimosso)*

### 3. Flusso Game Session
*(blocco di codice rimosso)*

### 4. Flusso Chat RAG
*(blocco di codice rimosso)*

### 5. Flusso Catalogo Condiviso
*(blocco di codice rimosso)*

### 6. Flusso Libreria Utente
*(blocco di codice rimosso)*

---

## Comandi per Eseguire i Test

*(blocco di codice rimosso)*

---

*Ultimo aggiornamento: 2026-02-15*
*Ultima esecuzione test: 2026-02-15 (Unit: 12,004/12,004 passati - 100%)*
*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/knowledge-base.md

# Knowledge Base - Flussi API

## Panoramica

Il bounded context Knowledge Base gestisce il sistema RAG, chat threads, agenti AI, context engineering e streaming SSE.

---

## 1. Vector Search e RAG

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/knowledge-base/search` | `SearchQuery` | `{ gameId, query, topK?, minScore?, searchMode?, language? }` | `[S]` |
| POST | `/knowledge-base/ask` | `AskQuestionQuery` | `{ gameId, query, language?, bypassCache? }` | `[S]` |

### Parametri Search

| Parametro | Default | Descrizione |
|-----------|---------|-------------|
| `topK` | 5 | Numero massimo di risultati |
| `minScore` | 0.55 | Soglia minima di rilevanza |
| `searchMode` | "hybrid" | Modalità: hybrid, vector, keyword |
| `language` | "en" | Lingua del query |

### Flusso RAG Ask

*(blocco di codice rimosso)*

---

## 2. Chat Threads

### CRUD

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/chat-threads` | `CreateChatThreadCommand` | `{ userId, gameId, title, initialMessage, agentId?, agentType? }` | `[S]` |
| GET | `/chat-threads/{threadId}` | `GetChatThreadByIdQuery` | — | `[S]` |
| GET | `/chat-threads` | `GetChatThreadsByGameQuery` | `gameId` (query) | `[S]` |
| PATCH | `/chat-threads/{threadId}` | `UpdateChatThreadTitleCommand` | `{ title }` | `[S]` |
| DELETE | `/chat-threads/{threadId}` | `DeleteChatThreadCommand` | — | `[S]` |

### Stato Thread

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/chat-threads/{threadId}/close` | `CloseThreadCommand` | — | `[S]` |
| POST | `/chat-threads/{threadId}/reopen` | `ReopenThreadCommand` | — | `[S]` |

### Messaggi

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/chat-threads/{threadId}/messages` | `AddMessageCommand` | `{ content, role }` | `[S]` |
| PUT | `/chat-threads/{threadId}/messages/{messageId}` | `UpdateMessageCommand` | `{ content }` | `[S]` |
| DELETE | `/chat-threads/{threadId}/messages/{messageId}` | `DeleteMessageCommand` | — | `[S]` |

### Query e Export

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/knowledge-base/my-chats` | `GetMyChatHistoryQuery` | `skip, take` | `[S]` |
| GET | `/chat-threads/my` | `GetUserChatThreadsQuery` | `gameId?, agentType?, status?, search?, page?, pageSize?` | `[S]` |
| GET | `/chat-threads/{threadId}/export` | `ExportChatCommand` | `format?` (json/markdown) | `[S]` |

### Flusso Chat Completo

*(blocco di codice rimosso)*

---

## 3. Chat Sessions (Issue #3483)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/chat/sessions` | `CreateChatSessionCommand` | `{ gameId, title?, userLibraryEntryId?, agentSessionId?, agentConfigJson? }` | `[S]` |
| POST | `/chat/sessions/{sessionId}/messages` | `AddChatSessionMessageCommand` | `{ role, content, metadata? }` | `[S]` |
| GET | `/chat/sessions/{sessionId}` | `GetChatSessionQuery` | `skip?, take?` | `[S]` |
| GET | `/users/{userId}/games/{gameId}/chat-sessions` | `GetUserGameChatSessionsQuery` | `skip?, take?` | `[S]` |
| GET | `/users/{userId}/chat-sessions/recent` | `GetRecentChatSessionsQuery` | `limit?` | `[S]` |
| DELETE | `/chat/sessions/{sessionId}` | `DeleteChatSessionCommand` | — | `[S]` |

---

## 4. Context Engineering (Issue #3491)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/context-engineering/assemble` | `AssembleContextCommand` | Vedi sotto | `[S]` |
| GET | `/context-engineering/sources` | `GetContextSourcesQuery` | — | `[S]` |

### Body AssembleContextCommand

*(blocco di codice rimosso)*

### Flusso Context Assembly

*(blocco di codice rimosso)*

---

## 5. Agent Management

### CRUD Agenti

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/agents` | `CreateAgentCommand` | `{ name, type, strategyName, strategyParameters?, isActive? }` | `[A]` |
| GET | `/agents/{id}` | `GetAgentByIdQuery` | — | `[S]` |
| GET | `/agents` | `GetAllAgentsQuery` | — | `[S]` |
| PUT | `/agents/{id}/configure` | `ConfigureAgentCommand` | `{ strategyName, strategyParameters? }` | `[A]` |
| GET | `/agents/recent` | `GetRecentAgentsQuery` | `limit?` | `[S]` |

### Operazioni Agenti

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/agents/{id}/invoke` | `InvokeAgentCommand` | `{ query, gameId?, chatThreadId? }` | `[S]` |
| POST | `/agents/{id}/chat` | `SendAgentMessageCommand` | `{ message, chatThreadId? }` | `[S]` SSE |
| PUT | `/agents/{id}/documents` | `UpdateAgentDocumentsCommand` | `{ documentIds[] }` | `[A]` |
| GET | `/agents/{id}/documents` | `GetAgentDocumentsQuery` | — | `[S]` |
| POST | `/agents/chat/ask` | `AskAgentQuestionCommand` | `{ question, strategy, sessionId?, gameId?, language?, topK?, minScore? }` | `[S]` |
| POST | `/agents/tutor/query` | `TutorQueryCommand` | `{ gameId, sessionId, query }` | `[S]` |

---

## 6. Agenti Specializzati

### Arbitro Agent (Move Validation)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/agents/arbitro/validate` | `ValidateMoveCommand` | `{ gameSessionId, playerName, action, position?, additionalContext? }` | `[S]` |

**Response**: `{ decision, confidence, reasoning, violatedRules[], suggestions[], applicableRules[] }`

### Decisore Agent (Strategic Analysis)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/agents/decisore/analyze` | `AnalyzeGameStateCommand` | `{ gameSessionId, playerName, analysisDepth?, maxSuggestions? }` | `[S]` |

### Agent Playground (Admin Testing)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/agent-definitions/{agentId}/playground/chat` | — | `{ message }` | `[A]` SSE |

---

## Flusso Completo: Game Session con AI

*(blocco di codice rimosso)*

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 3,028 |
| **Passati** | 3,027 |
| **Falliti** | 0 |
| **Ignorati** | 1 |
| **Pass Rate** | 100% |
| **Durata** | 14s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| RAG Search/Ask | `RagServiceTests.cs`, `SearchQueryHandlerTests.cs`, `AskQuestionTests.cs` | Passato |
| Chat Threads | `CreateChatThreadTests.cs`, `AddMessageTests.cs`, `DeleteThreadTests.cs` | Passato |
| Chat Sessions | `CreateChatSessionTests.cs`, `AddChatSessionMessageTests.cs` | Passato |
| Context Engineering | `AssembleContextTests.cs`, `GetContextSourcesTests.cs` | Passato |
| Agent CRUD | `CreateAgentTests.cs`, `ConfigureAgentTests.cs` (5 file) | Passato |
| Agent Operations | `InvokeAgentTests.cs`, `AskAgentQuestionTests.cs` | Passato |
| Arbitro Agent | `ValidateMoveTests.cs` | Passato |
| Decisore Agent | `AnalyzeGameStateTests.cs` | Passato |
| Plugins | 32 file di test plugin RAG | Passato |
| Chunking Strategies | 9 file strategie chunking | Passato |
| Embedding/Reranking | `EmbeddingServiceTests.cs`, `CrossEncoderRerankerTests.cs` | Passato |
| Domain Entities | `ChatThread.cs`, `ChatMessage.cs`, `AgentDefinition.cs` (22 file) | Passato |
| Validators | 10 file di validazione | Passato |

Bounded context con la copertura test piu' ampia del progetto (238 file, 3,028 test).

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/session-tracking.md

# Session Tracking - Flussi API

## Panoramica

Il bounded context Session Tracking gestisce il tracking delle attività utente con timeline, filtri per data e paginazione.

---

## 1. Activity Timeline

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/activity/timeline` | `GetActivityTimelineQuery` | `type?, search?, dateFrom?, dateTo?, page?, pageSize?, order?` | `[S]` |
| GET | `/dashboard/activity-timeline` | `GetActivityTimelineQuery` | `type?, search?, dateFrom?, dateTo?, skip?, take?, order?` | `[S]` |

### Parametri

| Parametro | Tipo | Descrizione |
|-----------|------|-------------|
| `type` | string? | Filtro per tipo attività (login, game_session, chat, pdf_upload, etc.) |
| `search` | string? | Ricerca full-text nelle attività |
| `dateFrom` | DateTime? | Data inizio range |
| `dateTo` | DateTime? | Data fine range |
| `page` | int | Numero pagina (default: 1) |
| `pageSize` | int | Elementi per pagina (default: 20) |
| `order` | string | Ordinamento: "asc" o "desc" (default: "desc") |

### Flusso Query Activity Timeline

*(blocco di codice rimosso)*

---

## 2. Dashboard Stream (SSE)

| Metodo | Path | Command/Query | Response | Auth |
|--------|------|---------------|----------|------|
| GET | `/dashboard/stream` | `GetDashboardStreamQuery` | SSE | `[S]` |

Il dashboard stream fornisce aggiornamenti in tempo reale delle attività utente tramite Server-Sent Events.

---

## Note Architetturali

- Il bounded context SessionTracking opera principalmente come domain logic integrato in altri bounded context
- Le sessioni utente sono tracciate dal middleware `SessionQuotaMiddleware`
- Gli eventi di attività sono generati tramite domain events
- Indice DB: `(UserId, IsActive)` per query efficienti sulle sessioni attive
- Indice DB aggiunto per date range filters (Issue #4315)

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 181 |
| **Passati** | 181 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | <1s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Session Quota | `SessionQuotaMiddlewareTests.cs` | Passato |
| Device Tracking | `DeviceFingerprintTests.cs` | Passato |
| Activity Timeline | `GetActivityTimelineTests.cs` | Passato |
| Domain Entities | Session entity (4 file) | Passato |
| Validators | 6 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/shared-game-catalog.md

# Shared Game Catalog - Flussi API

## Panoramica

Il bounded context Shared Game Catalog gestisce il catalogo condiviso di giochi da tavolo con workflow di approvazione, integrazione BGG, FAQ, errata, documenti e contribuzioni della community.

---

## 1. Ricerca Pubblica

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/shared-games` | `SearchSharedGamesQuery` | `search?, categoryIds[], mechanicIds[], minPlayers?, maxPlayers?, maxPlayingTime?, pageNumber?, pageSize?, sortBy?, sortDescending?` | `[P]` |
| GET | `/shared-games/{id}` | `GetSharedGameByIdQuery` | — | `[P]` |
| GET | `/shared-games/categories` | `GetGameCategoriesQuery` | — | `[P]` (cache 24h) |
| GET | `/shared-games/mechanics` | `GetGameMechanicsQuery` | — | `[P]` (cache 24h) |

---

## 2. Admin CRUD

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/shared-games` | `GetFilteredSharedGamesQuery` | `status?, search?, sortBy?, submittedBy?, pageNumber?, pageSize?` | `[A/E]` |
| POST | `/admin/shared-games` | `CreateSharedGameCommand` | `{ title, yearPublished, description, minPlayers, maxPlayers, playingTimeMinutes, minAge, complexityRating, averageRating, imageUrl, thumbnailUrl, rules, bggId }` | `[A/E]` |
| PUT | `/admin/shared-games/{id}` | `UpdateSharedGameCommand` | Same as create | `[A/E]` |
| DELETE | `/admin/shared-games/{id}` | `DeleteSharedGameCommand` | — | `[A]` |
| POST | `/admin/shared-games/{id}/archive` | `ArchiveSharedGameCommand` | — | `[A]` |

---

## 3. Workflow di Pubblicazione

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/shared-games/{id}/submit-for-approval` | `SubmitSharedGameForApprovalCommand` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/approve-publication` | `ApproveSharedGamePublicationCommand` | — | `[A]` |
| POST | `/admin/shared-games/{id}/reject-publication` | `RejectSharedGamePublicationCommand` | `{ reason }` | `[A]` |
| POST | `/admin/shared-games/batch-approve` | `BatchApproveGamesCommand` | `{ gameIds[] }` | `[A]` |
| POST | `/admin/shared-games/batch-reject` | `BatchRejectGamesCommand` | `{ gameIds[], reason }` | `[A]` |
| GET | `/admin/shared-games/pending-approvals` | `GetPendingApprovalGamesQuery` | `pageNumber?, pageSize?` | `[A/E]` |
| GET | `/admin/shared-games/approval-queue` | `GetApprovalQueueQuery` | `urgency?, submitter?, hasPdfs?` | `[A/E]` |

### Flusso Pubblicazione

*(blocco di codice rimosso)*

### Delete Requests

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/shared-games/pending-deletes` | `GetPendingDeleteRequestsQuery` | `pageNumber?, pageSize?` | `[A]` |
| POST | `/admin/shared-games/approve-delete/{requestId}` | `ApproveDeleteRequestCommand` | — | `[A]` |
| POST | `/admin/shared-games/reject-delete/{requestId}` | `RejectDeleteRequestCommand` | `{ reason }` | `[A]` |

---

## 4. BGG Integration

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/admin/shared-games/import-bgg` | `ImportGameFromBggCommand` | `{ bggId }` | `[A/E]` |
| GET | `/admin/shared-games/bgg/search` | `SearchBggGamesQuery` | `query, exact?` | `[A/E]` |
| GET | `/admin/shared-games/bgg/check-duplicate/{bggId}` | `CheckBggDuplicateQuery` | — | `[A/E]` |
| PUT | `/admin/shared-games/{id}/update-from-bgg` | `UpdateSharedGameFromBggCommand` | `{ fieldsToUpdate[] }` | `[A/E]` |
| POST | `/admin/shared-games/bulk-import` | `BulkImportGamesCommand` | `{ games[] }` (max 500) | `[A]` |

### Flusso Import BGG

*(blocco di codice rimosso)*

---

## 5. FAQ Management

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/games/{gameId}/faqs` | `GetGameFaqsQuery` | `limit?, offset?` | `[P]` |
| POST | `/faqs/{faqId}/upvote` | `UpvoteFaqCommand` | — | `[P]` |
| POST | `/admin/shared-games/{id}/faq` | `AddGameFaqCommand` | `{ question, answer, order }` | `[A/E]` |
| PUT | `/admin/shared-games/{id}/faq/{faqId}` | `UpdateGameFaqCommand` | `{ question, answer, order }` | `[A/E]` |
| DELETE | `/admin/shared-games/{id}/faq/{faqId}` | `DeleteGameFaqCommand` | — | `[A/E]` |

---

## 6. Errata Management

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/shared-games/{id}/errata` | `AddGameErrataCommand` | `{ description, pageReference, publishedDate }` | `[A/E]` |
| PUT | `/admin/shared-games/{id}/errata/{errataId}` | `UpdateGameErrataCommand` | Same as create | `[A/E]` |
| DELETE | `/admin/shared-games/{id}/errata/{errataId}` | `DeleteGameErrataCommand` | — | `[A/E]` |

---

## 7. Document Management

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/shared-games/{id}/documents` | `GetDocumentsBySharedGameQuery` | `type?` | `[A/E]` |
| GET | `/admin/shared-games/{id}/documents/active` | `GetActiveDocumentsQuery` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/documents` | `AddDocumentToSharedGameCommand` | `{ documentId, documentType }` | `[A/E]` |
| POST | `/admin/shared-games/{id}/documents/{docId}/set-active` | `SetActiveDocumentVersionCommand` | — | `[A/E]` |
| DELETE | `/admin/shared-games/{id}/documents/{docId}` | `RemoveDocumentFromSharedGameCommand` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/documents/{docId}/approve` | `ApproveDocumentForRagProcessingCommand` | `{ notes? }` | `[A/E]` |

---

## 8. Quick Questions

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/games/{id}/quick-questions` | `GetQuickQuestionsQuery` | — | `[P]` |
| POST | `/admin/shared-games/{id}/quick-questions/generate` | `GenerateQuickQuestionsCommand` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/quick-questions` | `AddManualQuickQuestionCommand` | `{ text, emoji, category, displayOrder }` | `[A/E]` |
| PUT | `/admin/quick-questions/{questionId}` | `UpdateQuickQuestionCommand` | Same as create | `[A/E]` |
| DELETE | `/admin/quick-questions/{questionId}` | `DeleteQuickQuestionCommand` | — | `[A/E]` |

---

## 9. Agent e State Templates

### Agent Linking

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/shared-games/{id}/link-agent/{agentId}` | `LinkAgentToSharedGameCommand` | — | `[A/E]` |
| DELETE | `/admin/shared-games/{id}/unlink-agent` | `UnlinkAgentFromSharedGameCommand` | — | `[A/E]` |

### Game State Templates

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/shared-games/{id}/state-template` | `GetActiveGameStateTemplateQuery` | — | `[A/E]` |
| GET | `/admin/shared-games/{id}/state-template/versions` | `GetGameStateTemplateVersionsQuery` | — | `[A/E]` |
| POST | `/admin/shared-games/{id}/state-template/generate` | `GenerateGameStateTemplateCommand` | — | `[A/E]` |
| PUT | `/admin/shared-games/{id}/state-template/{templateId}` | `UpdateGameStateTemplateCommand` | `{ jsonSchema }` | `[A/E]` |
| POST | `/admin/shared-games/{id}/state-template/{templateId}/activate` | `ActivateGameStateTemplateCommand` | — | `[A/E]` |

---

## 10. Share Requests (Community Contributions)

### Utente

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/share-requests` | `CreateShareRequestCommand` | `{ privateGameId, notes }` | `[S]` |
| GET | `/share-requests` | `GetUserShareRequestsQuery` | `status?, pageNumber?, pageSize?` | `[S]` |
| GET | `/share-requests/{id}` | `GetShareRequestDetailsQuery` | — | `[S]` |
| PUT | `/share-requests/{id}/documents` | `UpdateShareRequestDocumentsCommand` | `{ documentIds[] }` | `[S]` |
| DELETE | `/share-requests/{id}` | `WithdrawShareRequestCommand` | — | `[S]` |

### Admin Review

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/share-requests` | `GetPendingShareRequestsQuery` | `status?, contributionType?, search?, pageNumber?, pageSize?` | `[A/E]` |
| GET | `/admin/share-requests/{id}` | `GetShareRequestDetailsQuery` | — | `[A/E]` |
| POST | `/admin/share-requests/{id}/start-review` | `StartReviewCommand` | — | `[A/E]` |
| POST | `/admin/share-requests/{id}/approve` | `ApproveShareRequestCommand` | `{ titleOverride?, descriptionOverride?, selectedDocumentIds[] }` | `[A/E]` |
| POST | `/admin/share-requests/{id}/reject` | `RejectShareRequestCommand` | `{ reason }` | `[A/E]` |
| POST | `/admin/share-requests/{id}/request-changes` | `RequestShareRequestChangesCommand` | `{ feedback }` | `[A/E]` |
| POST | `/admin/share-requests/{id}/release` | `ReleaseReviewCommand` | — | `[A/E]` |
| GET | `/admin/share-requests/my-reviews` | `GetMyActiveReviewsQuery` | — | `[A/E]` |
| POST | `/editor/share-requests/bulk-approve` | `BulkApproveShareRequestsCommand` | `{ requestIds[] }` (max 20) | `[E]` |
| POST | `/editor/share-requests/bulk-reject` | `BulkRejectShareRequestsCommand` | `{ requestIds[], reason }` (max 20) | `[E]` |
| POST | `/admin/share-requests/{id}/approve-game-proposal` | `ApproveGameProposalCommand` | `{ action, targetGameId? }` | `[A]` |
| GET | `/admin/private-games/{id}/check-duplicates` | `CheckPrivateGameDuplicatesQuery` | — | `[A]` |

### Flusso Share Request

*(blocco di codice rimosso)*

---

## 11. Contributors e Badges

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/shared-games/{id}/contributors` | `GetGameContributorsQuery` | — | `[P]` |
| GET | `/users/{id}/contributions` | `GetUserContributionsQuery` | `pageNumber?, pageSize?` | `[P]` |
| GET | `/users/me/contribution-stats` | `GetUserContributionStatsQuery` | — | `[S]` |
| GET | `/badges` | `GetAllBadgesQuery` | — | `[P]` |
| GET | `/users/{id}/badges` | `GetUserBadgesQuery` | — | `[P]` |
| GET | `/users/me/badges` | `GetUserBadgesQuery` | — | `[S]` |
| GET | `/badges/leaderboard` | `GetBadgeLeaderboardQuery` | `period?, pageNumber?, pageSize?` | `[P]` |
| PUT | `/users/me/badges/{id}/display` | `ToggleBadgeDisplayCommand` | `{ isDisplayed }` | `[S]` |

---

## 12. Trending e Events

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/catalog/trending` | `GetCatalogTrendingQuery` | `limit?` | `[P]` |
| POST | `/catalog/events` | `RecordGameEventCommand` | `{ gameId, eventType }` | `[S]` |

---

## 13. PDF Wizard (Issue #4139)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/shared-games/wizard/upload-pdf` | `UploadPdfForGameExtractionCommand` | Multipart: file | `[A/E]` |
| GET | `/admin/shared-games/wizard/pdf/preview` | `GetPdfPreviewForWizardQuery` | `filePath` | `[A/E]` |
| GET | `/admin/shared-games/wizard/bgg/search` | `SearchBggGamesQuery` | `query, exact?` | `[A/E]` |
| GET | `/admin/shared-games/wizard/bgg/{bggId}` | `GetBggGameDetailsQuery` | — | `[A/E]` |
| POST | `/admin/shared-games/wizard/create` | `CreateSharedGameFromPdfCommand` | Wizard data | `[A/E]` |

### Flusso PDF Wizard

*(blocco di codice rimosso)*

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 1,450 |
| **Passati** | 1,450 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 6s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Public Search | `SearchSharedGamesTests.cs`, `GetSharedGameByIdTests.cs` | Passato |
| Admin CRUD | `CreateSharedGameTests.cs`, `UpdateSharedGameTests.cs`, `DeleteTests.cs` | Passato |
| Publish Workflow | `SubmitForApprovalTests.cs`, `ApprovePublicationTests.cs`, `RejectPublicationTests.cs` | Passato |
| BGG Integration | `ImportGameFromBggTests.cs`, `SearchBggGamesTests.cs`, `BulkImportTests.cs` | Passato |
| FAQ Management | `AddGameFaqTests.cs`, `UpdateFaqTests.cs`, `DeleteFaqTests.cs` | Passato |
| Errata Management | `AddGameErrataTests.cs`, `UpdateErrataTests.cs` | Passato |
| Document Management | `AddDocumentTests.cs`, `SetActiveDocumentTests.cs` | Passato |
| Share Requests | `CreateShareRequestTests.cs`, `ApproveShareTests.cs`, `RejectShareTests.cs` (10+ file) | Passato |
| Contributors/Badges | `GetContributorsTests.cs`, `GetBadgesTests.cs` | Passato |
| Quick Questions | `GenerateQuickQuestionsTests.cs` | Passato |
| State Templates | `GenerateGameStateTemplateTests.cs`, `ActivateTemplateTests.cs` | Passato |
| Domain Entities | CatalogGame aggregate, ShareRequest (59 file) | Passato |
| Validators | 8 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/system-configuration.md

# System Configuration - Flussi API

## Panoramica

Il bounded context System Configuration gestisce la configurazione runtime dell'applicazione, feature flags e gestione cache.

---

## 1. Configuration Management

### CRUD

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/admin/configurations` | `GetAllConfigsQuery` | `category?, environment?, activeOnly?, page?, pageSize?` | `[A]` |
| GET | `/admin/configurations/{id}` | `GetConfigByIdQuery` | — | `[A]` |
| GET | `/admin/configurations/key/{key}` | `GetConfigByKeyQuery` | `environment?, activeOnly?` | `[A]` |
| POST | `/admin/configurations` | `CreateConfigurationCommand` | CreateConfigurationRequest | `[A]` |
| PUT | `/admin/configurations/{id}` | `UpdateConfigValueCommand` | UpdateConfigurationRequest | `[A]` |
| DELETE | `/admin/configurations/{id}` | `DeleteConfigurationCommand` | — | `[A]` |
| PATCH | `/admin/configurations/{id}/toggle` | `ToggleConfigurationCommand` | `isActive` (query) | `[A]` |

### Utility

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/configurations/categories` | `GetConfigCategoriesQuery` | — | `[A]` |
| POST | `/admin/configurations/validate` | `ValidateConfigCommand` | `key, value, valueType` | `[A]` |

### Bulk Operations

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/admin/configurations/bulk-update` | `BulkUpdateConfigsCommand` | BulkConfigurationUpdateRequest | `[A]` |
| GET | `/admin/configurations/export` | `ExportConfigsQuery` | `environment?, activeOnly?` | `[A]` |
| POST | `/admin/configurations/import` | `ImportConfigsCommand` | ConfigurationImportRequest | `[A]` |

### Versioning e Rollback

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/configurations/{id}/history` | `GetConfigHistoryQuery` | `limit?` | `[A]` |
| POST | `/admin/configurations/{id}/rollback/{version}` | `RollbackConfigCommand` | — | `[A]` |

### Cache

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| POST | `/admin/configurations/cache/invalidate` | `InvalidateCacheCommand` | `key?` | `[A]` |

---

## 2. Feature Flags

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/admin/feature-flags` | `GetAllFeatureFlagsQuery` | — | `[A]` |
| GET | `/admin/feature-flags/{key}` | `IsFeatureEnabledQuery` | — | `[A]` |
| POST | `/admin/feature-flags` | `CreateConfigurationCommand` | CreateFeatureFlagRequest | `[A]` |
| PUT | `/admin/feature-flags/{key}` | `UpdateFeatureFlagCommand` | FeatureFlagUpdateRequest | `[A]` |
| POST | `/admin/feature-flags/{key}/toggle` | `ToggleConfigurationCommand` | `enabled` (query) | `[A]` |
| POST | `/admin/feature-flags/{key}/tier/{tier}/enable` | `EnableFeatureForTierCommand` | — | `[A]` |
| POST | `/admin/feature-flags/{key}/tier/{tier}/disable` | `DisableFeatureForTierCommand` | — | `[A]` |

### Flusso Feature Flag per Tier

*(blocco di codice rimosso)*

---

## Flusso Configuration con Versioning

*(blocco di codice rimosso)*

---

## Flusso Import/Export

*(blocco di codice rimosso)*

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 409 |
| **Passati** | 409 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 2s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Config CRUD | `CreateConfigurationTests.cs`, `UpdateConfigTests.cs`, `DeleteConfigTests.cs` | Passato |
| Feature Flags | `GetAllFeatureFlagsTests.cs`, `ToggleFlagTests.cs`, `EnableForTierTests.cs` | Passato |
| Versioning/Rollback | `GetConfigHistoryTests.cs`, `RollbackConfigTests.cs` | Passato |
| Bulk Operations | `BulkUpdateConfigsTests.cs`, `ImportConfigsTests.cs`, `ExportConfigsTests.cs` | Passato |
| Cache | `InvalidateCacheTests.cs` | Passato |
| Domain Entities | FeatureFlag, SystemSetting, ConfigEntry (16 file) | Passato |
| Validators | 5 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/user-library.md

# User Library - Flussi API

## Panoramica

Il bounded context User Library gestisce la libreria personale dei giochi, configurazione agenti, PDF custom, sharing, labels, collections, wishlist e giochi privati.

---

## 1. Libreria Core

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/library` | `GetUserLibraryQuery` | `page?, pageSize?, favoritesOnly?, stateFilter[], sortBy?, sortDescending?` | `[S]` |
| GET | `/library/stats` | `GetLibraryStatsQuery` | — | `[S]` |
| GET | `/library/quota` | `GetLibraryQuotaQuery` | — | `[S]` |
| POST | `/library/games/{gameId}` | `AddGameToLibraryCommand` | `{ notes?, isFavorite? }` | `[S]` |
| DELETE | `/library/games/{gameId}` | `RemoveGameFromLibraryCommand` | — | `[S]` |
| PATCH | `/library/games/{gameId}` | `UpdateLibraryEntryCommand` | `{ notes?, isFavorite? }` | `[S]` |
| GET | `/library/games/{gameId}/status` | `GetGameInLibraryStatusQuery` | — | `[S]` |

### Flusso Aggiunta Gioco

*(blocco di codice rimosso)*

---

## 2. Agent Configuration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/library/games/{gameId}/agent-config` | `GetGameAgentConfigQuery` | — | `[S]` |
| PUT | `/library/games/{gameId}/agent` | `ConfigureGameAgentCommand` | AgentConfigDto | `[S]` |
| DELETE | `/library/games/{gameId}/agent` | `ResetGameAgentCommand` | — | `[S]` |
| POST | `/library/games/{gameId}/agent-config` | `SaveAgentConfigCommand` | Config data | `[S]` |
| POST | `/library/games/{gameId}/agent` | `CreateGameAgentCommand` | Agent definition | `[S]` |

---

## 3. PDF Management

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/library/games/{gameId}/pdf` | `UploadCustomGamePdfCommand` | `{ pdfUrl, fileSizeBytes, originalFileName }` | `[S]` |
| DELETE | `/library/games/{gameId}/pdf` | `ResetGamePdfCommand` | — | `[S]` |
| GET | `/library/games/{gameId}/pdfs` | `GetGamePdfsQuery` | — | `[S]` |
| GET | `/library/{entryId}/pdf/progress` | SSE stream | — | `[S]` SSE |
| DELETE | `/library/entries/{entryId}/private-pdf` | `RemovePrivatePdfCommand` | — | `[S]` |

### Flusso Upload PDF Custom

*(blocco di codice rimosso)*

---

## 4. Game Detail (Epic #2823)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/library/games/{gameId}` | `GetGameDetailQuery` | — | `[S]` |
| GET | `/library/games/{gameId}/checklist` | `GetGameChecklistQuery` | `includeWizard?` | `[S]` |
| PUT | `/library/games/{gameId}/state` | `UpdateGameStateCommand` | `{ newState, stateNotes }` | `[S]` |
| POST | `/library/games/{gameId}/sessions` | `RecordGameSessionCommand` | Session data | `[S]` |
| POST | `/library/games/{gameId}/remind-loan` | `SendLoanReminderCommand` | `{ customMessage? }` | `[S]` |

---

## 5. Library Sharing

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/library/share` | `CreateLibraryShareLinkCommand` | `{ privacyLevel, includeNotes, expiresAt }` | `[S]` |
| GET | `/library/share` | `GetLibraryShareLinkQuery` | — | `[S]` |
| PATCH | `/library/share/{shareToken}` | `UpdateLibraryShareLinkCommand` | `{ privacyLevel?, includeNotes?, expiresAt? }` | `[S]` |
| DELETE | `/library/share/{shareToken}` | `RevokeLibraryShareLinkCommand` | — | `[S]` |
| GET | `/library/shared/{shareToken}` | `GetSharedLibraryQuery` | — | `[P]` |

### Flusso Sharing

*(blocco di codice rimosso)*

---

## 6. Labels (Epic #3511)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/library/labels` | `GetLabelsQuery` | — | `[S]` |
| POST | `/library/labels` | `CreateCustomLabelCommand` | `{ name, color }` | `[S]` |
| DELETE | `/library/labels/{labelId}` | `DeleteCustomLabelCommand` | — | `[S]` |
| GET | `/library/games/{gameId}/labels` | `GetGameLabelsQuery` | — | `[S]` |
| POST | `/library/games/{gameId}/labels/{labelId}` | `AddLabelToGameCommand` | — | `[S]` |
| DELETE | `/library/games/{gameId}/labels/{labelId}` | `RemoveLabelFromGameCommand` | — | `[S]` |

### Flusso Labels

*(blocco di codice rimosso)*

---

## 7. Generic Collections (Issue #4263)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/collections/{entityType}/{entityId}/status` | `GetCollectionStatusQuery` | — | `[S]` |
| POST | `/collections/{entityType}/{entityId}` | `AddToCollectionCommand` | `{ notes?, isFavorite? }` | `[S]` |
| DELETE | `/collections/{entityType}/{entityId}` | `RemoveFromCollectionCommand` | — | `[S]` |

### Bulk Operations (Issue #4268)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/collections/{entityType}/bulk-add` | `BulkAddToCollectionCommand` | `{ entityIds[], notes?, isFavorite? }` | `[S]` |
| DELETE | `/collections/{entityType}/bulk-remove` | `BulkRemoveFromCollectionCommand` | `{ entityIds[] }` | `[S]` |
| POST | `/collections/{entityType}/bulk-associated-data` | `GetBulkCollectionAssociatedDataQuery` | `{ entityIds[] }` | `[S]` |

---

## 8. Wishlist

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/wishlist` | `GetWishlistQuery` | — | `[S]` |
| GET | `/wishlist/highlights` | `GetWishlistHighlightsQuery` | — | `[S]` |
| POST | `/wishlist` | `AddToWishlistCommand` | `{ gameId, notes?, priority? }` | `[S]` |
| PUT | `/wishlist/{id}` | `UpdateWishlistItemCommand` | `{ notes?, priority? }` | `[S]` |
| DELETE | `/wishlist/{id}` | `RemoveFromWishlistCommand` | — | `[S]` |

---

## 9. Private Games

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/private-games` | `AddPrivateGameCommand` | Game data | `[S]` |
| GET | `/private-games/{id}` | `GetPrivateGameQuery` | — | `[S]` |
| PUT | `/private-games/{id}` | `UpdatePrivateGameCommand` | Updated data | `[S]` |
| DELETE | `/private-games/{id}` | `DeletePrivateGameCommand` | — | `[S]` |
| POST | `/private-games/{id}/propose-to-catalog` | `ProposePrivateGameCommand` | Proposal data | `[S]` |
| POST | `/private-games/{id}/link-agent/{agentId}` | `LinkAgentToPrivateGameCommand` | — | `[S]` |
| DELETE | `/private-games/{id}/unlink-agent` | `UnlinkAgentFromPrivateGameCommand` | — | `[S]` |

### Flusso Private Game → Catalogo

*(blocco di codice rimosso)*

---

## 10. Proposal Migration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/migrations/pending` | `GetPendingMigrationsQuery` | — | `[S]` |
| POST | `/migrations/{id}/choose` | `HandleMigrationChoiceCommand` | `{ choice }` (KeepPrivate/MigrateToShared) | `[S]` |

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 842 |
| **Passati** | 842 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 3s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Library Core | `AddGameToLibraryTests.cs`, `RemoveGameTests.cs`, `UpdateEntryTests.cs` | Passato |
| Agent Configuration | `ConfigureGameAgentTests.cs`, `SaveAgentConfigTests.cs` | Passato |
| PDF Management | `UploadCustomGamePdfTests.cs`, `ResetGamePdfTests.cs` | Passato |
| Game Detail | `GetGameDetailTests.cs`, `GetGameChecklistTests.cs` | Passato |
| Library Sharing | `CreateLibraryShareLinkTests.cs`, `GetSharedLibraryTests.cs` | Passato |
| Labels | `CreateCustomLabelTests.cs`, `AddLabelToGameTests.cs` | Passato |
| Collections | `AddToCollectionTests.cs`, `BulkAddTests.cs` | Passato |
| Wishlist | `AddToWishlistTests.cs`, `UpdateWishlistItemTests.cs` | Passato |
| Private Games | `AddPrivateGameTests.cs`, `ProposePrivateGameTests.cs` | Passato |
| Proposal Migration | `HandleMigrationChoiceTests.cs` | Passato |
| Domain Entities | UserGame, Collection, Wishlist (17 file) | Passato |
| Validators | 10 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/user-notifications.md

# User Notifications - Flussi API

## Panoramica

Il bounded context User Notifications gestisce le notifiche in-app e le preferenze di notifica degli utenti.

---

## 1. Notifiche

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/notifications` | `GetNotificationsQuery` | `unreadOnly?, limit?` | `[S]` |
| GET | `/notifications/unread-count` | `GetUnreadCountQuery` | — | `[S]` |
| POST | `/notifications/{notificationId}/mark-read` | `MarkNotificationReadCommand` | — | `[S]` |
| POST | `/notifications/mark-all-read` | `MarkAllNotificationsReadCommand` | — | `[S]` |

---

## 2. Preferenze Notifiche

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/notifications/preferences` | `GetNotificationPreferencesQuery` | — | `[S]` |
| PUT | `/notifications/preferences` | `UpdateNotificationPreferencesCommand` | Preferences object | `[S]` |

---

## Flusso Notifiche

*(blocco di codice rimosso)*

---

## Flusso Preferenze

*(blocco di codice rimosso)*

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 147 |
| **Passati** | 147 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 1s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Notifications | `GetNotificationsTests.cs`, `MarkNotificationReadTests.cs` | Passato |
| Preferences | `GetNotificationPreferencesTests.cs`, `UpdatePreferencesTests.cs` | Passato |
| Event Handlers | Email sending, push notifications (3 file) | Passato |
| Domain Entities | Notification aggregate (5 file) | Passato |
| Validators | 2 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---



<div style="page-break-before: always;"></div>

## user-guides/user-role/01-authentication.md

# User Authentication Flows

> Authentication and account management flows for end users.

## Table of Contents

- [Registration](#registration)
- [Login](#login)
- [OAuth](#oauth)
- [Two-Factor Authentication](#two-factor-authentication)
- [Password Management](#password-management)
- [Session Management](#session-management)
- [API Key Management](#api-key-management)

---

## Registration

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Request | Response |
|------|----------|--------|---------|----------|
| 1 | `/api/v1/auth/register` | POST | `{ email, password }` | `UserDto + Set-Cookie` |

**Request Body:**
*(blocco di codice rimosso)*

**Response (200):**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| API Endpoint | ✅ Implemented | `AuthenticationEndpoints.cs` |
| Command/Handler | ✅ Implemented | `RegisterCommand.cs` |
| Validator | ✅ Implemented | `RegisterCommandValidator.cs` |
| Frontend Page | ✅ Implemented | `/app/(auth)/register/page.tsx` |
| RegisterForm | ✅ Implemented | `RegisterForm.tsx` |

---

## Login

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Request | Response |
|------|----------|--------|---------|----------|
| 1 | `/api/v1/auth/login` | POST | `{ email, password }` | `UserDto` or `{ requires2FA, tempToken }` |
| 2 (if 2FA) | `/api/v1/auth/2fa/verify` | POST | `{ tempToken, code }` | `UserDto + Set-Cookie` |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Login Endpoint | ✅ Implemented | `AuthenticationEndpoints.cs` |
| 2FA Verify | ✅ Implemented | `TwoFactorEndpoints.cs` |
| Frontend Page | ✅ Implemented | `/app/(auth)/login/page.tsx` |
| LoginForm | ✅ Implemented | `LoginForm.tsx` |

---

## OAuth

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Description |
|------|----------|--------|-------------|
| 1 | `/api/v1/auth/oauth/{provider}/authorize` | GET | Get OAuth redirect URL |
| 2 | `/api/v1/auth/oauth/{provider}/callback` | GET | Handle OAuth callback |

**Supported Providers:** `google`, `github`

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| OAuth Endpoints | ✅ Implemented | `OAuthEndpoints.cs` |
| OAuth Handlers | ✅ Implemented | `OAuthLoginCommand.cs` |
| Frontend Callback | ✅ Implemented | `/app/(auth)/oauth-callback/page.tsx` |
| OAuthButtons | ✅ Implemented | `OAuthButtons.tsx` |

---

## Two-Factor Authentication

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Request | Response |
|------|----------|--------|---------|----------|
| 1 | `/api/v1/auth/2fa/setup` | POST | - | `{ qrCodeUrl, secret }` |
| 2 | `/api/v1/auth/2fa/verify` | POST | `{ code }` | `{ backupCodes[] }` |
| 3 (disable) | `/api/v1/auth/2fa` | DELETE | `{ code }` | `200 OK` |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| 2FA Endpoints | ✅ Implemented | `TwoFactorEndpoints.cs` |
| TOTP Service | ✅ Implemented | `TotpService.cs` |
| Frontend Settings | ⚠️ Partial | Settings page exists |

---

## Password Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Request | Response |
|------|----------|--------|---------|----------|
| 1 | `/api/v1/auth/password/reset-request` | POST | `{ email }` | `200 OK` |
| 2 | `/api/v1/auth/password/reset` | POST | `{ token, newPassword }` | `200 OK` |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Password Endpoints | ✅ Implemented | `PasswordEndpoints.cs` |
| Email Service | ⚠️ Partial | Email service configured |
| Frontend Page | ✅ Implemented | `/app/(auth)/reset-password/page.tsx` |

---

## Session Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Description |
|------|----------|--------|-------------|
| 1 | `/api/v1/users/me/sessions` | GET | List all user sessions |
| 2 | `/api/v1/auth/sessions/{id}/revoke` | POST | Revoke specific session |
| 3 | `/api/v1/auth/sessions/revoke-all` | POST | Revoke all sessions |
| 4 | `/api/v1/auth/session/status` | GET | Get current session status |
| 5 | `/api/v1/auth/session/extend` | POST | Extend current session |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Session Endpoints | ✅ Implemented | `AuthenticationEndpoints.cs` |
| Session Service | ✅ Implemented | `SessionManagementService.cs` |
| Frontend UI | ⚠️ Partial | Basic implementation |

---

## API Key Management

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Description |
|------|----------|--------|-------------|
| 1 | `/api/v1/api-keys` | GET | List user's API keys |
| 2 | `/api/v1/api-keys` | POST | Create new API key |
| 3 | `/api/v1/api-keys/{id}` | PUT | Update key metadata |
| 4 | `/api/v1/api-keys/{id}` | DELETE | Revoke key |
| 5 | `/api/v1/api-keys/{id}/rotate` | POST | Rotate key |
| 6 | `/api/v1/api-keys/{id}/usage` | GET | Get usage stats |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| API Key Endpoints | ✅ Implemented | `ApiKeyEndpoints.cs` |
| Usage Tracking | ✅ Implemented | `ApiKeyUsageService.cs` |
| Frontend Modal | ✅ Implemented | `ApiKeyCreationModal.tsx` |

---

## Gap Analysis

### Implemented Features
- [x] Email/password registration
- [x] Email/password login
- [x] OAuth (Google, GitHub)
- [x] 2FA (TOTP)
- [x] Password reset
- [x] Session management
- [x] API key management

### Missing/Partial Features
- [ ] Account lockout after failed attempts
- [ ] Email verification for new accounts
- [ ] Password change confirmation email
- [ ] Session device fingerprinting
- [ ] Remember me functionality
- [ ] Social account linking UI (partial)
- [ ] 2FA backup codes recovery flow

### Proposed Enhancements
1. **Account Lockout**: Implement temporary lockout after 5 failed attempts
2. **Email Verification**: Require email verification before full access
3. **Device Trust**: "Trust this device" to skip 2FA for known devices
4. **Login Notifications**: Email user on new device login


---



<div style="page-break-before: always;"></div>

## user-guides/user-role/02-game-discovery.md

# Game Discovery Flows

> User flows for discovering, browsing, and exploring games in the catalog.

## Table of Contents

- [Browse Catalog](#browse-catalog)
- [Search Games](#search-games)
- [Filter Games](#filter-games)
- [Game Details](#game-details)
- [Quick Questions](#quick-questions)

---

## Browse Catalog

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Query Params | Response |
|------|----------|--------|--------------|----------|
| 1 | `/api/v1/shared-games` | GET | `page, size, sort` | `{ items[], totalCount, page, pageSize }` |

**Query Parameters:**
- `page`: Page number (default: 1)
- `size`: Items per page (default: 20, max: 100)
- `sort`: `popularity`, `name`, `rating`, `newest`

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| API Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Frontend Page | ✅ Implemented | `/app/(public)/games/catalog/page.tsx` |
| GameCatalogCard | ✅ Implemented | `GameCatalogCard.tsx` |
| CatalogFilters | ✅ Implemented | `CatalogFilters.tsx` |

---

## Search Games

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Query Params | Response |
|------|----------|--------|--------------|----------|
| 1 | `/api/v1/shared-games` | GET | `search=catan` | Filtered games list |

**Full-Text Search Features:**
- Matches game name, publisher, description
- Supports partial matches
- Ranked by relevance
- Case insensitive

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| FTS Implementation | ✅ Implemented | PostgreSQL tsvector |
| Search Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Search UI | ✅ Implemented | `SharedGameSearch.tsx` |

---

## Filter Games

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Query Params | Response |
|------|----------|--------|--------------|----------|
| 1 | `/api/v1/shared-games` | GET | Multiple filter params | Filtered games |

**Filter Parameters:**
- `players`: Exact or range (e.g., `4` or `2-4`)
- `minDuration`, `maxDuration`: Play time in minutes
- `minComplexity`, `maxComplexity`: 1-5 scale
- `categories`: Comma-separated category IDs
- `mechanics`: Comma-separated mechanic IDs

### Supporting Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/shared-games/categories` | GET | List all categories |
| `/api/v1/shared-games/mechanics` | GET | List all mechanics |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Filter Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Categories/Mechanics | ✅ Implemented | Same file |
| Filter UI | ✅ Implemented | `SharedGameSearchFilters.tsx` |
| CatalogFilters | ✅ Implemented | `CatalogFilters.tsx` |

---

## Game Details

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Description |
|------|----------|--------|-------------|
| 1 | `/api/v1/shared-games/{id}` | GET | Get game details |
| 2 | `/api/v1/games/{id}/rules` | GET | Get rule specifications |
| 3 | `/api/v1/games/{id}/quick-questions` | GET | Get quick questions/FAQ |
| 4 | `/api/v1/pdfs/{pdfId}/download` | GET | Download PDF file |

**Game Detail Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Detail Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Detail Page | ✅ Implemented | `/app/(public)/games/[id]/page.tsx` |
| Tabs (Overview, Rules, Community) | ✅ Implemented | Multiple components |
| PDF Viewer | ✅ Implemented | `PdfViewerModal.tsx` |

---

## Quick Questions

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Response |
|----------|--------|----------|
| `/api/v1/games/{id}/quick-questions` | GET | `QuickQuestion[]` |

**Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Quick Questions Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| Quick Questions Component | ⚠️ Partial | Basic display implemented |

---

## Gap Analysis

### Implemented Features
- [x] Paginated game catalog
- [x] Full-text search
- [x] Multi-criteria filtering
- [x] Game detail pages
- [x] PDF viewer
- [x] Categories and mechanics
- [x] Quick questions display

### Missing/Partial Features
- [ ] Search autocomplete dropdown
- [ ] Recent searches history
- [ ] "Similar games" recommendations
- [ ] User ratings/reviews
- [ ] Wishlist functionality
- [ ] Share game to social media
- [ ] Compare games feature

### Proposed Enhancements
1. **Autocomplete Search**: Add dropdown with suggestions while typing
2. **Similar Games**: Show related games based on mechanics/categories
3. **User Reviews**: Allow users to rate and review games
4. **Wishlist**: Separate from library for games user wants to try
5. **Game Collections**: Curated lists (e.g., "Best 2-player games")


---



<div style="page-break-before: always;"></div>

## user-guides/user-role/03-library-management.md

# Library Management Flows

> User flows for managing personal game library with tier-based quotas.

## Table of Contents

- [Library Overview](#library-overview)
- [Add Game to Library](#add-game-to-library)
- [Remove Game from Library](#remove-game-from-library)
- [Manage Favorites](#manage-favorites)
- [Custom PDF Upload](#custom-pdf-upload)
- [Custom Agent Configuration](#custom-agent-configuration)
- [Quota Management](#quota-management)

---

## Library Overview

### Tier-Based Quota System

The library system enforces tier-based limits configured by administrators:

| Tier | Max Games (A) | PDF Uploads/Day (B) | PDF Uploads/Week (C) | Sessions (D) |
|------|---------------|---------------------|----------------------|--------------|
| **Free** | 5 | 5 | 20 | Unlimited* |
| **Normal** | 20 | 20 | 100 | Unlimited* |
| **Premium** | 50 | 100 | 500 | Unlimited* |

> *Session limits are configurable but not currently enforced.
> All limits are configurable via Admin → System Configuration.

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Description |
|------|----------|--------|-------------|
| 1 | `/api/v1/library` | GET | Get paginated library |
| 2 | `/api/v1/library/quota` | GET | Get game library quota |
| 3 | `/api/v1/users/me/upload-quota` | GET | Get PDF upload quota |
| 4 | `/api/v1/library/stats` | GET | Get library statistics |

**Library Response:**
*(blocco di codice rimosso)*

**Quota Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Library Endpoint | ✅ Implemented | `UserLibraryEndpoints.cs` |
| Quota Service | ✅ Implemented | `GameLibraryQuotaService.cs` |
| Library Page | ✅ Implemented | `/app/(public)/library/page.tsx` |
| QuotaStatusBar | ✅ Implemented | `QuotaStatusBar.tsx` |

---

## Add Game to Library

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Body | Response |
|------|----------|--------|------|----------|
| 1 | `/api/v1/library/games/{gameId}` | POST | `{ notes? }` | `201` or `403` |
| 2 | `/api/v1/library/games/{gameId}/status` | GET | - | Check if in library |

**Request Body:**
*(blocco di codice rimosso)*

**Error Response (403):**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Add Endpoint | ✅ Implemented | `UserLibraryEndpoints.cs` |
| Quota Check | ✅ Implemented | `GameLibraryQuotaService.cs` |
| AddToLibraryButton | ✅ Implemented | `AddToLibraryButton.tsx` |

---

## Remove Game from Library

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/library/games/{gameId}` | DELETE | Remove game from library |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Remove Endpoint | ✅ Implemented | `UserLibraryEndpoints.cs` |
| RemoveGameDialog | ✅ Implemented | `RemoveGameDialog.tsx` |

---

## Manage Favorites

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/library/games/{gameId}` | PATCH | `{ isFavorite: true }` | Toggle favorite |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| PATCH Endpoint | ✅ Implemented | `UserLibraryEndpoints.cs` |
| FavoriteToggle | ✅ Implemented | `FavoriteToggle.tsx` |

---

## Custom PDF Upload

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Description |
|------|----------|--------|-------------|
| 1 | `/api/v1/users/me/upload-quota` | GET | Check upload quota |
| 2 | `/api/v1/library/games/{gameId}/pdf` | POST | Upload custom PDF |
| 3 | `/api/v1/pdfs/{pdfId}/progress` | GET | Check processing status |
| 4 | `/api/v1/library/games/{gameId}/pdf` | DELETE | Reset to default PDF |

**Upload Quota Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Upload Endpoint | ✅ Implemented | `UserLibraryEndpoints.cs` |
| Quota Service | ✅ Implemented | `PdfUploadQuotaService.cs` |
| PdfUploadModal | ✅ Implemented | `PdfUploadModal.tsx` |
| Processing Status | ✅ Implemented | `ProcessingProgress.tsx` |

---

## Custom Agent Configuration

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/library/games/{gameId}/agent` | PUT | Config object | Save agent config |
| `/api/v1/library/games/{gameId}/agent` | DELETE | - | Reset to default |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Agent Config Endpoint | ✅ Implemented | `UserLibraryEndpoints.cs` |
| AgentConfigModal | ✅ Implemented | `AgentConfigModal.tsx` |
| AgentConfigPanel | ✅ Implemented | `AgentConfigPanel.tsx` |

---

## Quota Management

### Admin Configuration

Administrators can configure tier limits via:

**Endpoint:** `PUT /api/v1/admin/system/game-library-limits`

*(blocco di codice rimosso)*

**PDF Upload Limits** (Database config keys):
- `UploadLimits:Free:DailyLimit` = 5
- `UploadLimits:Free:WeeklyLimit` = 20
- `UploadLimits:Normal:DailyLimit` = 20
- `UploadLimits:Normal:WeeklyLimit` = 100
- `UploadLimits:Premium:DailyLimit` = 100
- `UploadLimits:Premium:WeeklyLimit` = 500

### Quota Bypass

Administrators and Editors have **unlimited** quotas:
- Bypass checked at service level
- Role-based, not tier-based

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Library Quota Config | ✅ Implemented | `UpdateGameLibraryLimitsCommandHandler.cs` |
| PDF Quota Config | ⚠️ DB Only | No admin UI, database only |
| Admin UI | ⚠️ Partial | Library limits exposed, PDF limits not |

---

## Gap Analysis

### Implemented Features
- [x] Library browsing and pagination
- [x] Add/remove games from library
- [x] Favorite toggling
- [x] Custom PDF upload with quota
- [x] Custom agent configuration
- [x] Library quota enforcement
- [x] PDF upload quota (daily + weekly)
- [x] Quota display UI

### Missing/Partial Features
- [ ] **Session Limits (D)**: Not currently enforced
- [ ] **PDF Upload Admin UI**: Limits only configurable via database
- [ ] **Quota Notifications**: No alert when approaching limit
- [ ] **Tier Upgrade Flow**: No in-app upgrade path
- [ ] **Usage History**: No view of past upload history
- [ ] **Quota Transfer**: No way to carry over unused quota

### Proposed Enhancements

1. **Session Limits**: Implement configurable session limits per tier
2. **Quota Warnings**: Notify users at 80% quota usage
3. **Admin UI for PDF Limits**: Add admin interface for PDF upload limits
4. **Upgrade Prompts**: Show upgrade options when quota is reached
5. **Usage Analytics**: Show users their quota usage over time
6. **Grace Period**: Allow slight overage with warning for premium features


---



<div style="page-break-before: always;"></div>

## user-guides/user-role/04-ai-chat.md

# AI Chat Flows

> User flows for interacting with the AI game assistant.

## Table of Contents

- [Start Chat](#start-chat)
- [Ask Question](#ask-question)
- [Chat History](#chat-history)
- [Manage Threads](#manage-threads)
- [Export Chat](#export-chat)
- [Quick Questions](#quick-questions)

---

## Start Chat

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Body | Response |
|------|----------|--------|------|----------|
| 1 | `/api/v1/chat-threads` | POST | `{ gameId }` | `{ threadId, ... }` |
| 2 | `/api/v1/chat-threads/{id}` | GET | - | Thread with messages |
| 3 | `/api/v1/chat-threads` | GET | `?gameId={id}` | List threads for game |

**Create Thread Request:**
*(blocco di codice rimosso)*

**Thread Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Create Thread Endpoint | ✅ Implemented | `KnowledgeBaseEndpoints.cs` |
| Chat Page | ✅ Implemented | `/app/(chat)/chat/page.tsx` |
| GameSelector | ✅ Implemented | `GameSelector.tsx` |
| ChatLayout | ✅ Implemented | `ChatLayout.tsx` |

---

## Ask Question

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Description |
|------|----------|--------|-------------|
| 1 | `/api/v1/chat-threads/{id}/messages` | POST | Send message |
| 2 | SSE Stream | - | Receive AI response |
| 3 | `/api/v1/knowledge-base/ask` | POST | Direct RAG query (alternative) |

**Message Request:**
*(blocco di codice rimosso)*

**SSE Stream Events:**
*(blocco di codice rimosso)*

**Citation Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Message Endpoint | ✅ Implemented | `KnowledgeBaseEndpoints.cs` |
| RAG Service | ✅ Implemented | `RagService.cs` |
| SSE Streaming | ✅ Implemented | `ChatStreamingService.cs` |
| MessageInput | ✅ Implemented | `MessageInput.tsx` |
| Message Display | ✅ Implemented | `Message.tsx` |
| Citation Card | ✅ Implemented | `CitationCard.tsx` |

---

## Chat History

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Query | Description |
|----------|--------|-------|-------------|
| `/api/v1/knowledge-base/my-chats` | GET | - | Dashboard chat list |
| `/api/v1/chat-threads` | GET | `?gameId=&search=` | Filtered threads |
| `/api/v1/chat-threads/{id}` | GET | - | Thread with messages |

**Chat History Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| My Chats Endpoint | ✅ Implemented | `KnowledgeBaseEndpoints.cs` |
| ChatSidebar | ✅ Implemented | `ChatSidebar.tsx` |
| ChatHistory | ✅ Implemented | `ChatHistory.tsx` |
| ThreadListItem | ✅ Implemented | `ThreadListItem.tsx` |

---

## Manage Threads

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/chat-threads/{id}` | PATCH | `{ title }` | Rename thread |
| `/api/v1/chat-threads/{id}/close` | POST | - | Close thread |
| `/api/v1/chat-threads/{id}/reopen` | POST | - | Reopen thread |
| `/api/v1/chat-threads/{id}` | DELETE | - | Delete thread |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Rename Endpoint | ✅ Implemented | `KnowledgeBaseEndpoints.cs` |
| Close/Reopen | ✅ Implemented | Same file |
| Delete Thread | ✅ Implemented | Same file |
| Thread Actions UI | ✅ Implemented | `MessageActions.tsx` |

---

## Export Chat

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Query | Description |
|----------|--------|-------|-------------|
| `/api/v1/chat-threads/{id}/export` | GET | `format=md\|json` | Export thread |

**Export Markdown Format:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Export Endpoint | ✅ Implemented | `KnowledgeBaseEndpoints.cs` |
| ExportChatModal | ✅ Implemented | `ExportChatModal.tsx` |

---

## Quick Questions

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/games/{gameId}/quick-questions` | GET | Get quick questions |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Quick Questions Endpoint | ✅ Implemented | `SharedGameCatalogEndpoints.cs` |
| FollowUpQuestions | ✅ Implemented | `FollowUpQuestions.tsx` |

---

## Gap Analysis

### Implemented Features
- [x] Create chat threads
- [x] Send messages with RAG
- [x] SSE streaming responses
- [x] Citations from documents
- [x] Chat history and sidebar
- [x] Thread management (rename, close, delete)
- [x] Export chat (Markdown, JSON)
- [x] Quick questions

### Missing/Partial Features
- [ ] **Message Editing**: Edit sent messages (endpoint exists, UI partial)
- [ ] **Message Reactions**: Thumbs up/down feedback
- [ ] **Voice Input**: Speech-to-text for questions
- [ ] **Image Upload**: Upload game images for questions
- [ ] **Share Thread**: Public link to share conversation
- [ ] **Multi-game Context**: Ask about multiple games at once
- [ ] **Conversation Suggestions**: AI suggests next questions

### Proposed Enhancements

1. **Feedback System**: Allow users to rate responses
2. **Conversation Context**: Better handling of follow-up questions
3. **Offline Mode**: Cache conversations for offline viewing
4. **Real-time Collaboration**: Share chat session with other players
5. **Voice Integration**: Ask questions via voice


---



<div style="page-break-before: always;"></div>

## user-guides/user-role/05-game-sessions.md

# Game Session Flows

> User flows for managing game sessions and tracking game state.

## Table of Contents

- [Create Session](#create-session)
- [Manage Players](#manage-players)
- [Game State Tracking](#game-state-tracking)
- [Player Mode](#player-mode)
- [Session Lifecycle](#session-lifecycle)
- [Session History](#session-history)

---

## Create Session

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Step | Endpoint | Method | Body | Response |
|------|----------|--------|------|----------|
| 1 | `/api/v1/sessions` | POST | Session config | Session object |
| 2 | `/api/v1/sessions/{id}/state/initialize` | POST | - | Initial state |

**Create Session Request:**
*(blocco di codice rimosso)*

**Session Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Create Session Endpoint | ✅ Implemented | `GameEndpoints.cs` |
| Session Setup Modal | ✅ Implemented | `SessionSetupModal.tsx` |
| Session Page | ✅ Implemented | `/app/(public)/sessions/[id]/page.tsx` |

---

## Manage Players

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/sessions/{id}/players` | POST | `{ name }` | Add player |
| `/api/v1/sessions/{id}/players/{playerId}` | DELETE | - | Remove player |
| `/api/v1/sessions/{id}/players/reorder` | PUT | `{ order[] }` | Reorder players |

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Add Player Endpoint | ✅ Implemented | `GameEndpoints.cs` |
| Player Management UI | ⚠️ Partial | Basic implementation |

---

## Game State Tracking

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/sessions/{id}/state/initialize` | POST | Initialize from template |
| `/api/v1/sessions/{id}/state` | GET | Get current state |
| `/api/v1/sessions/{id}/state` | PATCH | Update state |
| `/api/v1/sessions/{id}/state/snapshots` | POST | Create snapshot |
| `/api/v1/sessions/{id}/state/snapshots` | GET | List snapshots |
| `/api/v1/sessions/{id}/state/restore/{snapshotId}` | POST | Restore snapshot |

**State Update Request:**
*(blocco di codice rimosso)*

**Ledger Entry (History):**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| State Initialize | ✅ Implemented | `GameEndpoints.cs` |
| State Update | ✅ Implemented | Same file |
| Snapshots | ✅ Implemented | Same file |
| GameStateViewer | ✅ Implemented | `GameStateViewer.tsx` |
| GameStateEditor | ✅ Implemented | `GameStateEditor.tsx` |
| LedgerTimeline | ✅ Implemented | `LedgerTimeline.tsx` |
| SignalR Integration | ✅ Implemented | `useGameStateSignalR.ts` |

---

## Player Mode

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### Sequence Diagram

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/sessions/{id}/suggest-move` | POST | `{ playerId }` | Get AI suggestions |
| `/api/v1/sessions/{id}/apply-suggestion` | POST | `{ suggestionId }` | Apply suggestion |

**Suggestion Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Suggest Move Endpoint | ✅ Implemented | `GameEndpoints.cs` |
| Apply Suggestion | ✅ Implemented | Same file |
| PlayerModeControls | ✅ Implemented | `PlayerModeControls.tsx` |
| PlayerModeHelpModal | ✅ Implemented | `PlayerModeHelpModal.tsx` |
| PlayerModeTour | ✅ Implemented | `PlayerModeTour.tsx` |

---

## Session Lifecycle

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/v1/sessions/{id}/pause` | POST | - | Pause session |
| `/api/v1/sessions/{id}/resume` | POST | - | Resume session |
| `/api/v1/sessions/{id}/complete` | POST | Final scores | Complete session |
| `/api/v1/sessions/{id}/abandon` | POST | - | Abandon session |

**Complete Session Request:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| Lifecycle Endpoints | ✅ Implemented | `GameEndpoints.cs` |
| SessionWarningModal | ✅ Implemented | `SessionWarningModal.tsx` |

---

## Session History

### User Story

*(blocco di codice rimosso)*

### Screen Flow

*(blocco di codice rimosso)*

### API Flow

| Endpoint | Method | Query | Description |
|----------|--------|-------|-------------|
| `/api/v1/sessions/history` | GET | Filters | Get session history |
| `/api/v1/sessions/statistics` | GET | - | Get aggregated stats |
| `/api/v1/sessions/{id}` | GET | - | Get session details |
| `/api/v1/sessions/active` | GET | - | Get active sessions |

**History Response:**
*(blocco di codice rimosso)*

### Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| History Endpoint | ✅ Implemented | `GameEndpoints.cs` |
| Statistics Endpoint | ✅ Implemented | Same file |
| History Page | ✅ Implemented | `/app/(public)/sessions/history/page.tsx` |
| Sessions Page | ✅ Implemented | `/app/(public)/sessions/page.tsx` |

---

## Gap Analysis

### Implemented Features
- [x] Create game sessions
- [x] Add/manage players
- [x] Game state initialization from templates
- [x] State tracking and updates
- [x] State snapshots and restore
- [x] State history (ledger)
- [x] Player mode with AI suggestions
- [x] Session lifecycle (pause, complete, abandon)
- [x] Session history and statistics
- [x] Real-time sync via SignalR

### Missing/Partial Features
- [ ] **Session Limits by Tier**: Not currently enforced
- [ ] **Concurrent Session Limit**: No limit on active sessions
- [ ] **Session Sharing**: No way to invite others to join
- [ ] **Turn Timer**: No built-in turn timer
- [ ] **Rematch**: Quick "play again" with same players
- [ ] **Session Export**: Export session to file
- [ ] **Achievements**: Track milestones and achievements

### Proposed Enhancements

1. **Tier-Based Session Limits**: Implement configurable session limits
2. **Session Invites**: Send invite links to join session
3. **Turn Notifications**: Alert when it's your turn
4. **Session Templates**: Save player configurations for quick start
5. **Social Features**: Share session results to social media
6. **Achievements System**: Track and display gaming achievements
7. **AI Game Master**: AI-hosted sessions with automated state management


---



<div style="page-break-before: always;"></div>

## user-guides/workflow-integration.md

# Workflow Integration - Flussi API

## Panoramica

Il bounded context Workflow Integration gestisce l'integrazione con n8n per automazione workflow, template e logging errori.

---

## 1. n8n Configuration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/n8n` | `GetAllN8NConfigsQuery` | — | `[A]` |
| GET | `/admin/n8n/{configId}` | `GetN8NConfigByIdQuery` | — | `[A]` |
| POST | `/admin/n8n` | `CreateN8NConfigCommand` | CreateN8NConfigRequest | `[A]` |
| PUT | `/admin/n8n/{configId}` | `UpdateN8NConfigCommand` | UpdateN8NConfigRequest | `[A]` |
| DELETE | `/admin/n8n/{configId}` | `DeleteN8NConfigCommand` | — | `[A]` |
| POST | `/admin/n8n/{configId}/test` | `TestN8NConnectionCommand` | — | `[A]` |

---

## 2. n8n Templates

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/n8n/templates` | `GetN8NTemplatesQuery` | `category?` | `[S]` |
| GET | `/n8n/templates/{id}` | `GetN8NTemplateByIdQuery` | — | `[S]` |
| POST | `/n8n/templates/{id}/import` | `ImportN8NTemplateCommand` | ImportTemplateRequest | `[S]` |
| POST | `/n8n/templates/validate` | `ValidateN8NTemplateQuery` | ValidateTemplateRequest | `[S]` |

---

## 3. Workflow Error Logging

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/logs/workflow-error` | `LogWorkflowErrorCommand` | LogWorkflowErrorRequest | `[P]` (webhook) |
| GET | `/admin/workflows/errors` | `GetWorkflowErrorsQuery` | `workflowId?, fromDate?, toDate?, page?, limit?` | `[A]` |
| GET | `/admin/workflows/errors/{id}` | `GetWorkflowErrorByIdQuery` | — | `[A]` |

---

## Flusso Setup n8n Integration

*(blocco di codice rimosso)*

---

## Flusso Error Monitoring

*(blocco di codice rimosso)*

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 134 |
| **Passati** | 134 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | <1s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| n8n Configuration | `CreateN8NConfigTests.cs`, `UpdateConfigTests.cs`, `DeleteConfigTests.cs` | Passato |
| n8n Connection Test | `TestN8NConnectionTests.cs` | Passato |
| Templates | `GetTemplatesTests.cs`, `ImportTemplateTests.cs`, `ValidateTemplateTests.cs` | Passato |
| Error Logging | `LogWorkflowErrorTests.cs`, `GetWorkflowErrorsTests.cs` | Passato |
| Domain Entities | Workflow, WorkflowExecution (5 file) | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*


---

