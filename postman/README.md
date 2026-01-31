# MeepleAI Postman Collections

Postman collections for testing and exploring the MeepleAI API, organized by user flows and roles.

## Quick Start

```bash
# Install Newman
npm install -g newman newman-reporter-htmlextra

# Run a specific collection
newman run postman/01-authentication.postman_collection.json

# Run with HTML report
newman run postman/01-authentication.postman_collection.json \
  -r cli,htmlextra --reporter-htmlextra-export newman-report.html
```

## Collection Overview

### User Flows (01-05)

| # | Collection | Description |
|---|------------|-------------|
| 01 | [Authentication](01-authentication.postman_collection.json) | Registration, login, OAuth, 2FA, sessions, API keys |
| 02 | [Game Discovery](02-game-discovery.postman_collection.json) | Catalog browsing, search, filters, categories |
| 03 | [Library Management](03-library-management.postman_collection.json) | User library, quotas, custom PDFs, wishlist, play history |
| 04 | [AI Chat](04-ai-chat.postman_collection.json) | Chat threads, SSE streaming, RAG, export |
| 05 | [Game Sessions](05-game-sessions.postman_collection.json) | Session management, state tracking, AI suggestions |

### Editor Flows (06-09)

| # | Collection | Description |
|---|------------|-------------|
| 06 | [Editor: Game Management](06-editor-game-management.postman_collection.json) | Game CRUD, BGG import, cover images, specifications |
| 07 | [Editor: Document Management](07-editor-document-management.postman_collection.json) | PDF upload (standard/chunked), versions, processing |
| 08 | [Editor: Content Management](08-editor-content-management.postman_collection.json) | Quick questions, FAQ, errata, state templates |
| 09 | [Editor: Publication Workflow](09-editor-publication-workflow.postman_collection.json) | Submit for approval, status tracking, resubmission |

### Admin Flows (10-13)

| # | Collection | Description |
|---|------------|-------------|
| 10 | [Admin: Approval Workflow](10-admin-approval-workflow.postman_collection.json) | Publication & deletion approval queues |
| 11 | [Admin: User Management](11-admin-user-management.postman_collection.json) | User CRUD, roles, tiers, bulk operations |
| 12 | [Admin: System Configuration](12-admin-system-configuration.postman_collection.json) | Quotas, feature flags, AI models, prompts |
| 13 | [Admin: Monitoring](13-admin-monitoring.postman_collection.json) | Health checks, alerts, analytics, n8n, cache |

---

## Detailed Collection Reference

### 01 - Authentication Flows

Comprehensive authentication and session management:

**Registration & Login**
- `POST /auth/register` - User registration with email verification
- `POST /auth/login` - Standard login with credentials
- `POST /auth/logout` - Session termination
- `GET /auth/me` - Current user profile
- `POST /auth/refresh-token` - Token refresh

**OAuth & Social Login**
- `GET /auth/oauth/{provider}` - Initiate OAuth flow (Google, Discord, BGG)
- `GET /auth/oauth/{provider}/callback` - OAuth callback handler

**Two-Factor Authentication**
- `POST /auth/2fa/setup` - Initialize 2FA with TOTP
- `POST /auth/2fa/verify` - Verify TOTP code
- `POST /auth/2fa/backup-codes` - Generate backup codes
- `DELETE /auth/2fa` - Disable 2FA

**Password Management**
- `POST /auth/password/forgot` - Request password reset
- `POST /auth/password/reset` - Reset with token
- `PUT /auth/password` - Change password (authenticated)

**API Keys**
- `GET /api/v1/api-keys` - List user's API keys
- `POST /api/v1/api-keys` - Create new API key
- `DELETE /api/v1/api-keys/{id}` - Revoke API key

**Sessions**
- `GET /api/v1/sessions/active` - List active sessions
- `DELETE /api/v1/sessions/{id}` - Terminate specific session
- `DELETE /api/v1/sessions/all` - Terminate all other sessions

### 02 - Game Discovery

Browse and search the public game catalog:

**Catalog Browsing**
- `GET /api/v1/games` - List published games with pagination
- `GET /api/v1/games/{id}` - Get game details
- `GET /api/v1/games/{id}/summary` - Get AI-generated summary

**Search & Filters**
- `GET /api/v1/games/search?q={query}` - Full-text search
- `GET /api/v1/games?category={id}` - Filter by category
- `GET /api/v1/games?mechanic={id}` - Filter by mechanic
- `GET /api/v1/games?playerCount=2&complexity=medium` - Advanced filters

**Categories & Mechanics**
- `GET /api/v1/categories` - List all categories
- `GET /api/v1/mechanics` - List all mechanics
- `GET /api/v1/categories/{id}/games` - Games in category

### 03 - Library Management

User's personal game library with tier-based quotas:

**Library Operations**
- `GET /api/v1/library` - User's library with pagination
- `POST /api/v1/library/add/{gameId}` - Add game from catalog
- `DELETE /api/v1/library/{id}` - Remove from library
- `GET /api/v1/library/quota` - Check tier quota usage

**Custom PDF Uploads**
- `POST /api/v1/library/custom` - Upload custom game PDF
- `GET /api/v1/library/{id}/documents` - List game documents

**Wishlist**
- `GET /api/v1/library/wishlist` - User's wishlist
- `POST /api/v1/library/wishlist/{gameId}` - Add to wishlist
- `DELETE /api/v1/library/wishlist/{id}` - Remove from wishlist
- `POST /api/v1/library/wishlist/{id}/move-to-library` - Move to library

**Play History**
- `GET /api/v1/library/play-history` - Play sessions log
- `POST /api/v1/library/{id}/log-play` - Log a play session
- `GET /api/v1/library/stats` - Library statistics

### 04 - AI Chat

AI-powered game assistance with RAG:

**Chat Threads**
- `GET /api/v1/chat/threads` - List user's threads
- `POST /api/v1/chat/threads` - Create new thread
- `GET /api/v1/chat/threads/{id}` - Get thread details
- `DELETE /api/v1/chat/threads/{id}` - Delete thread

**Messaging (SSE Streaming)**
- `POST /api/v1/chat/threads/{id}/messages` - Send message (SSE response)
- `GET /api/v1/chat/threads/{id}/messages` - Get thread history
- `POST /api/v1/chat/threads/{id}/regenerate` - Regenerate last response
- `POST /api/v1/chat/threads/{id}/feedback` - Rate AI response

**RAG Knowledge Base**
- `POST /api/v1/rag/search` - Semantic search in game documents
- `GET /api/v1/rag/context/{gameId}` - Get relevant context

**Export**
- `GET /api/v1/chat/threads/{id}/export?format=pdf` - Export as PDF/Markdown

### 05 - Game Sessions

Track game plays with state management:

**Session Management**
- `GET /api/v1/sessions` - List user's sessions
- `POST /api/v1/sessions` - Start new session
- `GET /api/v1/sessions/{id}` - Get session details
- `PUT /api/v1/sessions/{id}` - Update session
- `DELETE /api/v1/sessions/{id}` - End/delete session
- `POST /api/v1/sessions/{id}/pause` - Pause session
- `POST /api/v1/sessions/{id}/resume` - Resume session

**State Tracking**
- `GET /api/v1/sessions/{id}/state` - Get current state
- `PUT /api/v1/sessions/{id}/state` - Update state
- `POST /api/v1/sessions/{id}/state/snapshot` - Create snapshot
- `GET /api/v1/sessions/{id}/state/history` - State history

**AI Assistance**
- `POST /api/v1/sessions/{id}/suggest` - Get AI suggestions
- `POST /api/v1/sessions/{id}/validate` - Validate move/action

**Multiplayer**
- `POST /api/v1/sessions/{id}/invite` - Invite player
- `GET /api/v1/sessions/invites` - Pending invitations
- `POST /api/v1/sessions/invites/{id}/accept` - Accept invite

### 06 - Editor: Game Management

Game catalog management (Editor role required):

**Game CRUD**
- `GET /api/v1/admin/shared-games` - List editor's games
- `POST /api/v1/admin/shared-games` - Create new game
- `GET /api/v1/admin/shared-games/{id}` - Get game details
- `PUT /api/v1/admin/shared-games/{id}` - Update game
- `DELETE /api/v1/admin/shared-games/{id}` - Delete game

**BGG Import**
- `GET /api/v1/bgg/search?q={query}` - Search BoardGameGeek
- `POST /api/v1/admin/shared-games/import-from-bgg` - Import from BGG

**Cover Images**
- `POST /api/v1/admin/shared-games/{id}/cover` - Upload cover image
- `DELETE /api/v1/admin/shared-games/{id}/cover` - Remove cover

**Game Specifications**
- `GET /api/v1/admin/shared-games/{id}/specifications` - Get specs
- `PUT /api/v1/admin/shared-games/{id}/specifications` - Update specs

### 07 - Editor: Document Management

PDF upload and processing (Editor role required):

**Standard Upload**
- `POST /api/v1/admin/shared-games/{id}/documents` - Upload PDF
- `GET /api/v1/admin/shared-games/{id}/documents` - List documents
- `DELETE /api/v1/admin/shared-games/{id}/documents/{docId}` - Delete document

**Chunked Upload** (Large files)
- `POST /api/v1/admin/shared-games/{id}/documents/init-chunked` - Initialize
- `PUT /api/v1/admin/shared-games/{id}/documents/{uploadId}/chunk/{n}` - Upload chunk
- `POST /api/v1/admin/shared-games/{id}/documents/{uploadId}/complete` - Complete upload

**Document Versions**
- `GET /api/v1/admin/shared-games/{id}/documents/{docId}/versions` - List versions
- `POST /api/v1/admin/shared-games/{id}/documents/{docId}/versions` - Upload new version
- `PUT /api/v1/admin/shared-games/{id}/documents/{docId}/active-version` - Set active

**Processing**
- `GET /api/v1/admin/shared-games/{id}/documents/{docId}/status` - Processing status
- `POST /api/v1/admin/shared-games/{id}/documents/{docId}/reprocess` - Trigger reprocess
- `GET /api/v1/admin/shared-games/{id}/documents/{docId}/preview` - Preview extracted

**Rule Specifications**
- `POST /api/v1/admin/shared-games/{id}/generate-rule-spec` - Generate from PDF
- `GET /api/v1/admin/shared-games/{id}/rule-spec` - Get rule spec
- `PUT /api/v1/admin/shared-games/{id}/rule-spec` - Update rule spec

### 08 - Editor: Content Management

Quick questions, FAQ, errata, and state templates (Editor role required):

**Quick Questions**
- `GET /api/v1/admin/shared-games/{id}/quick-questions` - List questions
- `POST /api/v1/admin/shared-games/{id}/quick-questions` - Add question
- `PUT /api/v1/admin/shared-games/{id}/quick-questions/{qId}` - Update
- `DELETE /api/v1/admin/shared-games/{id}/quick-questions/{qId}` - Delete
- `PUT /api/v1/admin/shared-games/{id}/quick-questions/reorder` - Reorder

**FAQ Management**
- `GET /api/v1/admin/shared-games/{id}/faqs` - List FAQs
- `POST /api/v1/admin/shared-games/{id}/faqs` - Create FAQ
- `PUT /api/v1/admin/shared-games/{id}/faqs/{faqId}` - Update FAQ
- `DELETE /api/v1/admin/shared-games/{id}/faqs/{faqId}` - Delete FAQ
- `POST /api/v1/admin/shared-games/{id}/faqs/generate` - AI-generate FAQs

**Errata Management**
- `GET /api/v1/admin/shared-games/{id}/errata` - List errata
- `POST /api/v1/admin/shared-games/{id}/errata` - Create erratum
- `PUT /api/v1/admin/shared-games/{id}/errata/{errataId}` - Update
- `DELETE /api/v1/admin/shared-games/{id}/errata/{errataId}` - Delete

**State Templates**
- `GET /api/v1/admin/shared-games/{id}/state-template` - Get template
- `PUT /api/v1/admin/shared-games/{id}/state-template` - Update template
- `POST /api/v1/admin/shared-games/{id}/state-template/generate` - AI-generate
- `POST /api/v1/admin/shared-games/{id}/state-template/validate` - Validate schema

### 09 - Editor: Publication Workflow

Submit games for approval (Editor role required):

**Submission**
- `POST /api/v1/admin/shared-games/{id}/submit-for-approval` - Submit game
- Requirements: name, description (50+ chars), cover image, processed PDF

**Status Tracking**
- `GET /api/v1/admin/shared-games?status=pending_approval` - Pending games
- `GET /api/v1/admin/shared-games?status=rejected` - Rejected games
- `GET /api/v1/admin/shared-games?status=published` - Published games
- `GET /api/v1/admin/shared-games/{id}/status` - Detailed status with queue position

**Resubmission**
- `GET /api/v1/admin/shared-games/{id}/rejection` - Get rejection details
- `POST /api/v1/admin/shared-games/{id}/submit-for-approval` - Resubmit after fixes

### 10 - Admin: Approval Workflow

Manage publication and deletion queues (Admin role required):

**Publication Queue**
- `GET /api/v1/admin/shared-games/pending-approvals` - FIFO queue
- `GET /api/v1/admin/shared-games/{id}/review` - Review details
- `POST /api/v1/admin/shared-games/{id}/approve-publication` - Approve
- `POST /api/v1/admin/shared-games/{id}/reject-publication` - Reject with reason

**Deletion Queue**
- `GET /api/v1/admin/shared-games/pending-deletions` - Pending deletions
- `GET /api/v1/admin/shared-games/{id}/deletion-request` - Request details
- `POST /api/v1/admin/shared-games/{id}/approve-deletion` - Approve deletion
- `POST /api/v1/admin/shared-games/{id}/reject-deletion` - Reject deletion

**Statistics**
- `GET /api/v1/admin/shared-games/queue-stats` - Queue statistics

### 11 - Admin: User Management

User administration (Admin role required):

**User CRUD**
- `GET /api/v1/admin/users` - List users with pagination
- `GET /api/v1/admin/users?search={query}` - Search users
- `GET /api/v1/admin/users?role={role}` - Filter by role (User, Editor, Admin)
- `GET /api/v1/admin/users?tier={tier}` - Filter by tier (Free, Normal, Premium)
- `POST /api/v1/admin/users` - Create user
- `GET /api/v1/admin/users/{id}` - Get user details
- `PUT /api/v1/admin/users/{id}` - Update user
- `DELETE /api/v1/admin/users/{id}` - Delete user

**Roles & Tiers**
- `PUT /api/v1/admin/users/{id}/role` - Change role
- `PUT /api/v1/admin/users/{id}/tier` - Change tier

**Bulk Operations**
- `POST /api/v1/admin/users/bulk/password-reset` - Bulk password reset
- `POST /api/v1/admin/users/bulk/role-change` - Bulk role change
- `POST /api/v1/admin/users/bulk/import` - Import from CSV
- `GET /api/v1/admin/users/bulk/export` - Export to CSV

**User Activity**
- `GET /api/v1/admin/users/{id}/activity` - Activity timeline
- `GET /api/v1/admin/users/{id}/activity?type=LOGIN` - Filter by type

### 12 - Admin: System Configuration

System settings (Admin role required):

**Quota Configuration**
- `GET /api/v1/admin/system/game-library-limits` - Get limits by tier
- `PUT /api/v1/admin/system/game-library-limits` - Update limits

**Feature Flags**
- `GET /api/v1/admin/feature-flags` - List all flags
- `POST /api/v1/admin/feature-flags` - Create flag
- `PUT /api/v1/admin/feature-flags/{name}` - Update flag

**AI Models**
- `GET /api/v1/admin/ai-models` - List configured models
- `POST /api/v1/admin/ai-models` - Add model
- `POST /api/v1/admin/ai-models/set-primary` - Set primary model
- `PUT /api/v1/admin/ai-models/{id}` - Update/disable model

**Prompt Management**
- `GET /api/v1/admin/prompts` - List system prompts
- `GET /api/v1/admin/prompts/{id}` - Get prompt details
- `PUT /api/v1/admin/prompts/{id}` - Update prompt
- `GET /api/v1/admin/prompts/{id}/versions` - Version history
- `POST /api/v1/admin/prompts/{id}/versions/new` - Create version
- `GET /api/v1/admin/prompts/{id}/compare` - Compare versions

**API Key Administration**
- `GET /api/v1/admin/api-keys/stats` - All API keys with usage
- `DELETE /api/v1/admin/api-keys/{id}` - Revoke API key
- `GET /api/v1/admin/api-keys/bulk/export` - Export to CSV

### 13 - Admin: Monitoring

System monitoring and operations (Admin role required):

**System Health**
- `GET /health` - Basic health check (no auth)
- `GET /health/live` - Kubernetes liveness probe
- `GET /health/ready` - Kubernetes readiness probe
- `GET /health/detailed` - Detailed health (requires auth)

**Alert Management**
- `GET /api/v1/admin/alerts` - Active alerts
- `POST /api/v1/admin/alerts/{id}/acknowledge` - Acknowledge alert
- `GET /api/v1/admin/alert-rules` - List alert rules
- `POST /api/v1/admin/alert-rules` - Create rule
- `PUT /api/v1/admin/alert-rules/{id}` - Update rule

**Analytics**
- `GET /api/v1/admin/analytics?period=30d` - Dashboard stats
- `GET /api/v1/admin/analytics/users` - User metrics
- `GET /api/v1/admin/analytics/games` - Game popularity
- `POST /api/v1/admin/reports` - Generate report (PDF/CSV)

**N8N Workflows**
- `GET /api/v1/admin/n8n` - List configurations
- `POST /api/v1/admin/n8n` - Create configuration
- `POST /api/v1/admin/n8n/{id}/test` - Test connection
- `GET /api/v1/n8n/templates` - Workflow templates
- `POST /api/v1/n8n/templates/{id}/import` - Import template
- `GET /api/v1/admin/workflows/errors` - Recent errors

**Cache Management**
- `GET /api/v1/admin/cache/stats` - Cache statistics
- `GET /api/v1/admin/cache/entries` - List cache entries
- `DELETE /api/v1/admin/cache/{name}` - Clear specific cache
- `DELETE /api/v1/admin/cache/all` - Clear all caches

---

## Legacy Test Collection

### MeepleAI-API-Tests.postman_collection.json

A consolidated smoke test suite for CI/CD:

1. **Health Checks** - Basic API health verification (no auth required)
   - `/health/ready` - Readiness probe
   - `/health/live` - Liveness probe

2. **Authentication** - User registration, login, and session management
   - Register new user (generates unique email per run)
   - Get current user (`/auth/me`)
   - Session status check
   - Logout
   - Login with credentials
   - Error cases (invalid credentials, empty credentials)

3. **Game Management** - Game CRUD operations (requires auth)
   - Get all games
   - Get game by ID
   - Error cases (unauthenticated access, non-existent game)

4. **KnowledgeBase** - RAG search and Q&A (requires auth + game data)
   - Search endpoint (skips gracefully if no games available)
   - Error cases (empty query, invalid game ID)

5. **Cleanup** - Final logout

**Test Count**: 17 tests total

---

## Running Tests

### Prerequisites

- Node.js and npm installed
- Newman installed globally: `npm install -g newman newman-reporter-htmlextra`
- API running on `http://localhost:8080`

### Running Individual Collections

```bash
# Authentication flows
newman run postman/01-authentication.postman_collection.json

# Game discovery
newman run postman/02-game-discovery.postman_collection.json

# Admin monitoring
newman run postman/13-admin-monitoring.postman_collection.json
```

### Running with Environment

```bash
newman run postman/01-authentication.postman_collection.json \
  -e postman/Local-Development.postman_environment.json
```

### Running All Collections

```bash
# Run all collections sequentially
for f in postman/*.postman_collection.json; do
  newman run "$f" -e postman/Local-Development.postman_environment.json
done
```

### CI/CD

Tests are automatically run in GitHub Actions CI pipeline as part of the `ci-api-smoke` job.

---

## Test Design Principles

1. **Idempotent** - Tests can be run multiple times without side effects
2. **Self-contained** - Each test generates its own test data (unique emails)
3. **CI-friendly** - Tests skip gracefully when dependencies are unavailable
4. **Session-aware** - Tests use cookies for authentication
5. **Auto-populating** - Test scripts automatically capture IDs for chained requests

---

## Environment Variables

The `Local-Development.postman_environment.json` file contains:

- `baseUrl`: API base URL (default: `http://localhost:8080/api/v1`)
- `healthBaseUrl`: Health check base URL (default: `http://localhost:8080`)
- `testPassword`: Default test password (default: `TestPassword123!`)

**Note**: `testEmail`, `testUserId`, `accessToken`, and entity IDs are dynamically generated during test execution.

---

## Collection Variables

Each collection includes these variables:

| Variable | Description |
|----------|-------------|
| `baseUrl` | API base URL (default: `http://localhost:8080`) |
| `accessToken` | JWT token (auto-populated by auth tests) |
| `gameId` | Current game ID (auto-populated) |
| `threadId` | Current chat thread ID |
| `sessionId` | Current game session ID |
| `userId` | Current user ID |

---

## Troubleshooting

### Authentication failures
- Ensure the API is running and healthy: `curl http://localhost:8080/health`
- Check that cookies are being set correctly (session cookie name: `meepleai_session`)
- Verify JWT token is being captured in collection variables

### KnowledgeBase tests skipped
- This is expected in CI environments without seeded game data
- Tests use `pm.execution.skipRequest()` to skip gracefully

### Health check failures
- Verify PostgreSQL, Redis, and Qdrant are running
- Check API logs for connection errors

### Permission errors (403)
- Verify user has correct role for endpoint (User, Editor, Admin)
- Check `accessToken` is being sent in Authorization header

### Rate limiting (429)
- Wait and retry
- Check rate limit headers in response
