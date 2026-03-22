# Alpha Zero Trim — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce MeepleAI from 13 bounded contexts / 966 endpoints / 213 routes to a functional Alpha Zero: sign up → add game → upload rulebook → chat about rules.

**Architecture:** Feature-flag approach via environment variable `ALPHA_MODE=true`. When enabled, only Alpha endpoints are registered in Program.cs, only Alpha nav items appear in the frontend. No code is deleted — dormant features remain in the codebase but are not exposed. This is reversible by setting `ALPHA_MODE=false`.

**Tech Stack:** .NET 9 (Program.cs endpoint registration), Next.js 16 (navigation.ts config), Docker Compose profiles

**⚠️ IMPORTANT — Next.js build-time variables:** `NEXT_PUBLIC_*` env vars are **inlined at build time** by Next.js. Setting them as runtime Docker `environment:` has NO effect. They must be passed as **build args** OR set before `pnpm build`. Docker overrides must use `build.args`, not just `environment`.

**⚠️ IMPORTANT — DI vs Endpoints:** Service/handler DI registrations are separate from endpoint registrations. Gating endpoints does NOT affect DI — all handlers remain registered and resolved. The `GameStateHub` SignalR hub must remain unconditionally registered because handlers inject `IHubContext<GameStateHub>`. This is safe: Alpha users won't connect to it.

---

## Alpha Zero Scope (from Spec Panel consensus)

### Active Bounded Contexts (5)
| Context | Alpha Scope |
|---------|------------|
| **Authentication** | Email/password + Google OAuth. No 2FA, no API keys |
| **GameManagement** | Game CRUD + BGG search/import + private games. No sessions, live scoring, toolkits |
| **DocumentProcessing** | PDF upload + chunking. Only unstructured-service |
| **KnowledgeBase** | RAG chat with sourced answers. No agent framework, no A/B testing |
| **UserLibrary** | Basic collection + private games (add/remove). No wishlist, proposals |

### Active Admin Sections (3)
| Section | Alpha Scope |
|---------|------------|
| **Overview** | Dashboard stats |
| **Users** | User management + invitations + access requests |
| **Content** | Game list + KB documents + upload (trimmed sidebar) |

### Active Docker Services (6)
postgres, redis, api, web, embedding-service, unstructured-service

### Active Frontend Routes (~20)
Auth (login, register, reset-password, verify-email, oauth-callback, setup-account) + Dashboard + Library (collection + private tabs only) + Chat (new, thread, agents/create) + Discover + Profile + Admin (overview, users, content)

### Dormant (NOT deleted, just hidden)
SessionTracking, SharedGameCatalog, BusinessSimulations, Gamification, AgentMemory, WorkflowIntegration, UserNotifications + all their endpoints, routes, and admin panels

---

## Task 1: Backend — Alpha Mode Environment Variable

**Files:**
- Modify: `apps/api/src/Api/Program.cs` (add ALPHA_MODE check around endpoint registration)

- [ ] **Step 1: Add ALPHA_MODE configuration read**

At the top of `Program.cs` (after `var app = builder.Build();`, around line 590), add:

```csharp
// Alpha Zero: gate non-essential endpoints behind ALPHA_MODE=true
var isAlphaMode = app.Configuration["ALPHA_MODE"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
if (isAlphaMode)
{
    app.Logger.LogInformation("🎯 ALPHA_MODE enabled — only core endpoints registered");
}
```

Note: Uses `app.Configuration` (not `Environment.GetEnvironmentVariable`) to follow project conventions and support appsettings/user-secrets/env vars uniformly.

- [ ] **Step 2: Wrap non-Alpha endpoints in `if (!isAlphaMode)` block**

Restructure lines 598–781 into two sections:

**ALPHA ENDPOINTS** (always registered):
```csharp
// ═══ ALPHA ZERO: Core endpoints (always active) ═══

// Authentication (core only — no 2FA, no API keys in alpha)
v1Api.MapAuthEndpoints();
v1Api.MapAccessRequestEndpoints();
v1Api.MapUserProfileEndpoints();
v1Api.MapOnboardingEndpoints();
v1Api.MapUserAccountEndpoints();

// Game Management (core only — no sessions, toolkits, game nights)
v1Api.MapGameEndpoints();
v1Api.MapBggEndpoints();
v1Api.MapGameManagementEndpoints();
v1Api.MapPrivateGameEndpoints();          // Alpha: private games support /library?tab=private
v1Api.MapRuleSpecEndpoints();             // Alpha: rule specifications for rulebook queries

// Document Processing (PDF upload + processing)
v1Api.MapPdfEndpoints();
v1Api.MapRulebookEndpoints();
v1Api.MapDocumentCollectionEndpoints();

// KnowledgeBase (RAG chat — core)
v1Api.MapKnowledgeBaseEndpoints();
v1Api.MapChatSessionEndpoints();
v1Api.MapAgentEndpoints();
v1Api.MapModelEndpoints();
v1Api.MapLlmEndpoints();
v1Api.MapAiEndpoints();
v1Api.MapRulebookAnalysisEndpoints();

// User Library (basic collection)
v1Api.MapUserLibraryEndpoints();
v1Api.MapCollectionWizardEndpoints();

// Dashboard
v1Api.MapDashboardEndpoints();

// Admin (users + content only)
v1Api.MapAdminUserEndpoints();
v1Api.MapAdminConfigEndpoints();
v1Api.MapConfigurationEndpoints();
v1Api.MapFeatureFlagEndpoints();
v1Api.MapCacheEndpoints();
v1Api.MapAdminKnowledgeBaseEndpoints();
v1Api.MapAdminSharedGameContentEndpoints();

// Infrastructure
v1Api.MapMonitoringEndpoints();
v1Api.MapSessionEndpoints();
```

**NON-ALPHA ENDPOINTS** (gated):
```csharp
if (!isAlphaMode)
{
    // Auth extended
    v1Api.MapPermissionEndpoints();
    v1Api.MapShareLinkEndpoints();
    v1Api.MapUserAiConsentEndpoints();
    v1Api.MapUserLlmDataEndpoints();
    v1Api.MapUserUsageEndpoints();
    v1Api.MapApiKeyEndpoints();
    v1Api.MapDeviceEndpoints();

    // Game Management extended
    v1Api.MapPlayRecordEndpoints();
    v1Api.MapLiveSessionEndpoints();
    v1Api.MapSessionAttachmentEndpoints();
    v1Api.MapGameToolkitEndpoints();
    v1Api.MapGameToolboxEndpoints();
    v1Api.MapToolStateEndpoints();
    v1Api.MapTurnOrderEndpoints();
    v1Api.MapWhiteboardEndpoints();
    v1Api.MapSessionSnapshotEndpoints();
    v1Api.MapPlaylistEndpoints();
    v1Api.MapGameNightEndpoints();
    v1Api.MapGameNightImprovvisataEndpoints();
    v1Api.MapRuleConflictFaqEndpoints();
    v1Api.MapSessionTrackingEndpoints();
    v1Api.MapSessionStatisticsEndpoints();
    v1Api.MapProposalMigrationEndpoints();

    // Shared Game Catalog
    v1Api.MapSharedGameCatalogEndpoints();

    // Agent Memory
    v1Api.MapAgentMemoryEndpoints();

    // Admin extended
    app.MapAdminGameImportWizardEndpoints();
    v1Api.MapAdminGameWizardEndpoints();
    v1Api.MapAdminAgentTestEndpoints();
    v1Api.MapAdminOpenRouterEndpoints();
    v1Api.MapAdminEmergencyControlsEndpoints();
    v1Api.MapAdminLlmConfigEndpoints();
    app.MapAdminSecretsEndpoints();
    app.MapAdminBulkImportEndpoints();
    v1Api.MapGroup("/admin/catalog-ingestion").MapAdminCatalogIngestionEndpoints();
    app.MapPdfAnalyticsEndpoints();
    app.MapChatAnalyticsEndpoints();
    app.MapModelPerformanceEndpoints();
    v1Api.MapRateLimitAdminEndpoints();
    v1Api.MapGameLibraryConfigEndpoints();
    v1Api.MapChatHistoryConfigEndpoints();
    v1Api.MapSessionLimitsConfigEndpoints();
    v1Api.MapPdfUploadLimitsConfigEndpoints();
    v1Api.MapPdfTierUploadLimitsConfigEndpoints();
    v1Api.MapAdminTierEndpoints();
    v1Api.MapSessionInviteEndpoints();
    v1Api.MapAnalyticsEndpoints();
    v1Api.MapActivityTimelineEndpoints();
    v1Api.MapLlmAnalyticsEndpoints();
    v1Api.MapAdminAgentMetricsEndpoints();
    v1Api.MapArbitroAdminEndpoints();
    v1Api.MapAdminPdfMetricsEndpoints();
    v1Api.MapAdminPdfStorageEndpoints();
    v1Api.MapAdminPdfManagementEndpoints();
    v1Api.MapAdminQueueEndpoints();
    v1Api.MapAdminStorageMigrationEndpoints();
    v1Api.MapAdminEmailEndpoints();
    v1Api.MapAdminEmailTemplateEndpoints();
    v1Api.MapAdminNotificationQueueEndpoints();
    v1Api.MapAdminSlackEndpoints();
    v1Api.MapAdminManualNotificationEndpoints();
    v1Api.MapAdminBusinessStatsEndpoints();
    v1Api.MapAdminAgentDefinitionEndpoints();
    v1Api.MapAgentPlaygroundEndpoints();
    v1Api.MapPlaygroundTestScenarioEndpoints();
    v1Api.MapAdminStrategyEndpoints();
    v1Api.MapAdminRagExecutionEndpoints();
    v1Api.MapAdminDebugChatEndpoints();
    v1Api.MapAdminSandboxEndpoints();
    v1Api.MapRagEnhancementAdminEndpoints();
    v1Api.MapRagEnhancementEstimateEndpoints();
    v1Api.MapAdminRagQualityEndpoints();

    // Alerts & Notifications
    v1Api.MapAlertEndpoints();
    v1Api.MapAlertConfigEndpoints();
    v1Api.MapAlertConfigurationEndpoints();
    v1Api.MapNotificationEndpoints();
    v1Api.MapNotificationPreferencesEndpoints();
    v1Api.MapSlackIntegrationEndpoints();
    v1Api.MapUnsubscribeEndpoints();

    // Library extended
    v1Api.MapEntityLinkUserEndpoints();
    v1Api.MapEntityLinkAdminEndpoints();
    v1Api.MapWishlistEndpoints();
    v1Api.MapAchievementEndpoints();

    // Audit & Analytics
    v1Api.MapAuditEndpoints();
    v1Api.MapAdminAuditLogEndpoints();
    v1Api.MapUserActivityEndpoints();
    v1Api.MapAdminAgentAnalyticsEndpoints();
    v1Api.MapAdminOperationsEndpoints();
    v1Api.MapDatabaseSyncEndpoints();
    v1Api.MapAdminDockerEndpoints();
    v1Api.MapPromptManagementEndpoints();

    // Workflows
    v1Api.MapWorkflowEndpoints();
    v1Api.MapN8nWebhookEndpoints();

    // AI extended
    v1Api.MapTokenManagementEndpoints();
    v1Api.MapAiModelAdminEndpoints();
    v1Api.MapArbitroAgentEndpoints();
    v1Api.MapDecisoreAgentEndpoints();
    v1Api.MapGroup("/agent-typologies").MapAgentTypologyEndpoints();
    v1Api.MapAgentSessionEndpoints();
    v1Api.MapGroup("/admin/ab-tests").MapAdminAbTestEndpoints();
    v1Api.MapGroup("/admin/test-results").MapAdminTestResultEndpoints();
    v1Api.MapLedgerModeEndpoints();
    v1Api.MapRagDashboardEndpoints();
    v1Api.MapRagPipelineEndpoints();
    v1Api.MapGroup("/rag").MapRagStrategyEndpoints();

    // Business Simulations
    v1Api.MapBudgetEndpoints();
    v1Api.MapFinancialLedgerEndpoints();
    v1Api.MapCostCalculatorEndpoints();
    v1Api.MapResourceForecastEndpoints();

    // Batch & Operations
    v1Api.MapBatchJobEndpoints();
    v1Api.MapBatchJobLogsEndpoints();
    v1Api.MapAdminResourcesEndpoints();
    v1Api.MapAdminEmbeddingEndpoints();
    v1Api.MapAdminPipelineEndpoints();
    v1Api.MapAdminKBSettingsEndpoints();
    v1Api.MapTierStrategyAdminEndpoints();
    v1Api.MapRagPipelineAdminEndpoints();
    v1Api.MapRagExecutionAdminEndpoints();
    v1Api.MapAdminMechanicExtractorEndpoints();
    v1Api.MapAdminMiscEndpoints();
    v1Api.MapAdminSeedingEndpoints();
    v1Api.MapReportingEndpoints();
    v1Api.MapTestingMetricsEndpoints();
    v1Api.MapBggImportQueueEndpoints();
}

// ═══ ALWAYS REGISTERED (DI dependencies) ═══
// GameStateHub MUST remain registered unconditionally:
// Multiple handlers inject IHubContext<GameStateHub> and would fail DI resolution if removed.
// Alpha users won't connect to it — no client-side SignalR setup in Alpha routes.
app.MapHub<Api.Hubs.GameStateHub>("/hubs/gamestate");
```

- [ ] **Step 3: Verify build compiles**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Program.cs
git commit -m "feat(api): add ALPHA_MODE endpoint gating

Gate non-essential endpoints behind ALPHA_MODE=true env var.
Alpha exposes: auth, games, BGG, PDF, KB chat, library, dashboard, admin users/content.
~120 endpoint groups moved behind feature gate, reversible by unsetting ALPHA_MODE.
GameStateHub remains unconditionally registered (DI dependency)."
```

---

## Task 2: Frontend — Alpha Navigation Trim (In-Place Filtering)

**Files:**
- Modify: `apps/web/src/config/navigation.ts`
- Modify: `apps/web/src/config/library-navigation.ts`
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`

**⚠️ APPROACH (from code review):** To avoid breaking all consumers, we filter the exported arrays IN-PLACE. The raw arrays are renamed to `_ALL_*` (private), and the public exports (`UNIFIED_NAV_ITEMS`, `LIBRARY_TABS`, `DASHBOARD_SECTIONS`) become the filtered versions. This means zero consumer changes needed — `useNavigationItems`, `LibraryNavTabs`, `AdminTabSidebar`, and all helpers continue to work unchanged.

- [ ] **Step 1: Add ALPHA_MODE env var to Next.js**

Add to `apps/web/.env.local` (and `.env.development` if it exists):
```
NEXT_PUBLIC_ALPHA_MODE=true
```

⚠️ This is a **build-time** variable. It takes effect on `pnpm build` or `pnpm dev` restart, NOT at container runtime.

- [ ] **Step 2: Trim navigation.ts — in-place alpha filtering**

In `apps/web/src/config/navigation.ts`:

1. Rename the array definition from `UNIFIED_NAV_ITEMS` to `_ALL_NAV_ITEMS`
2. Add alpha filter logic
3. Re-export `UNIFIED_NAV_ITEMS` as the filtered version

```typescript
const isAlphaMode = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';

const ALPHA_NAV_IDS = new Set([
  'welcome', 'dashboard', 'library', 'chat', 'catalog', 'profile', 'admin',
]);

const _ALL_NAV_ITEMS: UnifiedNavItem[] = [
  // ... existing array contents unchanged ...
];

/** Unified navigation items — filtered by ALPHA_MODE when active */
export const UNIFIED_NAV_ITEMS: UnifiedNavItem[] = isAlphaMode
  ? _ALL_NAV_ITEMS.filter(item => ALPHA_NAV_IDS.has(item.id))
  : _ALL_NAV_ITEMS;
```

**No consumer updates needed** — `useNavigationItems`, `UnifiedHeader`, etc. all import `UNIFIED_NAV_ITEMS` and will automatically get the filtered list.

- [ ] **Step 3: Trim library-navigation.ts — in-place alpha filtering**

Same pattern:

```typescript
const isAlphaMode = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';

const ALPHA_LIBRARY_TAB_IDS = new Set(['collection', 'private']);

const _ALL_LIBRARY_TABS: LibraryTab[] = [
  // ... existing array contents unchanged ...
];

/** Library tabs — filtered by ALPHA_MODE when active */
export const LIBRARY_TABS: LibraryTab[] = isAlphaMode
  ? _ALL_LIBRARY_TABS.filter(tab => ALPHA_LIBRARY_TAB_IDS.has(tab.id))
  : _ALL_LIBRARY_TABS;
```

**No consumer updates needed** — `LibraryNavTabs.tsx` imports `LIBRARY_TABS` directly and will get filtered list.

- [ ] **Step 4: Trim admin-dashboard-navigation.ts — in-place alpha filtering with sidebar trim**

Same pattern, plus sidebar item filtering for the `content` section:

```typescript
const isAlphaMode = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';

const ALPHA_SECTION_IDS = new Set(['overview', 'content', 'users']);

const ALPHA_CONTENT_SIDEBAR_LABELS = new Set([
  'All Games', 'Add Game', 'KB Overview', 'Documents', 'Upload & Process',
]);

const _ALL_DASHBOARD_SECTIONS: DashboardSection[] = [
  // ... existing array contents unchanged ...
];

/** Dashboard sections — filtered by ALPHA_MODE when active */
export const DASHBOARD_SECTIONS: DashboardSection[] = isAlphaMode
  ? _ALL_DASHBOARD_SECTIONS
      .filter(s => ALPHA_SECTION_IDS.has(s.id))
      .map(s => s.id === 'content'
        ? { ...s, sidebarItems: s.sidebarItems.filter(i => ALPHA_CONTENT_SIDEBAR_LABELS.has(i.label)) }
        : s
      )
  : _ALL_DASHBOARD_SECTIONS;
```

**No consumer updates needed** — `AdminTabSidebar`, `getActiveSection()`, `isSectionActive()`, etc. all reference `DASHBOARD_SECTIONS` and will automatically get the filtered list.

- [ ] **Step 5: Verify frontend builds**

Run: `cd apps/web && pnpm build`
Expected: Build succeeded with no broken imports

- [ ] **Step 6: Verify filtered nav in dev mode**

Run: `cd apps/web && pnpm dev`
Open http://localhost:3000, log in, check:
- Main nav shows only: Dashboard, Library, Chat, Discover, Admin
- Library tabs show only: Collection, Games (private)
- Admin sidebar shows only: Overview, Content (trimmed), Users
- No `Agents`, `Sessions`, `Play Records`, `Players`, `Knowledge Base` visible

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/config/navigation.ts apps/web/src/config/library-navigation.ts apps/web/src/config/admin-dashboard-navigation.ts apps/web/.env.local
git commit -m "feat(web): alpha mode nav trim (in-place filtering)

Gate non-Alpha navigation behind NEXT_PUBLIC_ALPHA_MODE=true (build-time).
Alpha nav: dashboard, library (collection+private), chat, discover, admin.
Admin: only overview, content (trimmed sidebar), users sections.
Uses in-place array filtering — zero consumer changes needed.
Hidden: agents, sessions, play-records, players, knowledge-base, wishlist, proposals."
```

---

## Task 3: Frontend — Dashboard Widget Cleanup

**Files:**
- Modify: `apps/web/src/components/dashboard/DashboardContextBar.tsx`
- Modify: `apps/web/src/components/dashboard/agents-section.tsx`
- Modify: `apps/web/src/components/dashboard/session-hero.tsx`
- Modify: `apps/web/src/components/dashboard/zones/GameNightZone.tsx`
- Modify: `apps/web/src/components/dashboard/zones/AgentsSidebar.tsx`
- Modify: `apps/web/src/components/dashboard/OnboardingFlow.tsx`
- Modify: `apps/web/src/components/dashboard/exploration/ActiveSessionBanner.tsx`

**Context:** The dashboard page contains widgets with hard links to dormant routes (`/agents`, `/sessions`, `/game-nights`, `/players`). Even though the middleware guard redirects direct navigation, these links are visible and confusing in Alpha mode.

- [ ] **Step 1: Create alpha mode utility**

Create `apps/web/src/lib/alpha-mode.ts`:

```typescript
/** Whether the app is running in Alpha Zero mode (build-time flag) */
export const IS_ALPHA_MODE = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';
```

- [ ] **Step 2: Conditionally hide dormant dashboard sections**

For each file listed above, wrap the dormant-route linking components in an alpha check:

```typescript
import { IS_ALPHA_MODE } from '@/lib/alpha-mode';

// In render:
{!IS_ALPHA_MODE && (
  <AgentsSidebar />  // or GameNightZone, SessionHero, etc.
)}
```

**Specific files and what to hide:**

| File | What to hide | Condition |
|------|-------------|-----------|
| `DashboardContextBar.tsx` | Links to `/sessions/new`, `/game-nights/new` | `!IS_ALPHA_MODE` |
| `agents-section.tsx` | Entire component (links to `/agents`) | `!IS_ALPHA_MODE` |
| `session-hero.tsx` | Entire component (links to `/sessions`) | `!IS_ALPHA_MODE` |
| `GameNightZone.tsx` | Entire component (links to `/game-nights`) | `!IS_ALPHA_MODE` |
| `AgentsSidebar.tsx` | Entire component (links to `/agents`) | `!IS_ALPHA_MODE` |
| `OnboardingFlow.tsx` | Steps linking to `/players`, `/sessions/new` | `!IS_ALPHA_MODE` |
| `ActiveSessionBanner.tsx` | Entire component (links to `/sessions/live`) | `!IS_ALPHA_MODE` |

- [ ] **Step 3: Verify dashboard renders cleanly in Alpha mode**

Run: `cd apps/web && pnpm dev`
Navigate to dashboard. Verify:
- No broken links to dormant features
- Dashboard shows: games overview, library summary, recent chat, getting started (if new user)
- No agents sidebar, session hero, game night zone, active session banner

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/alpha-mode.ts apps/web/src/components/dashboard/
git commit -m "feat(web): hide dormant dashboard widgets in alpha mode

Conditionally hide dashboard components that link to dormant features:
agents sidebar, session hero, game night zone, active session banner.
Uses IS_ALPHA_MODE build-time flag for tree-shaking."
```

---

## Task 4: Frontend — Middleware Route Guard

**Files:**
- Modify: `apps/web/src/middleware.ts` (or equivalent auth middleware)

- [ ] **Step 1: Identify middleware file**

Run: `grep -rl "middleware" apps/web/src/ --include="middleware.ts" | head -5`

Also check: `apps/web/middleware.ts` (Next.js convention — can be at project root)

- [ ] **Step 2: Add alpha route guard**

In the Next.js middleware, add a check: if `NEXT_PUBLIC_ALPHA_MODE=true`, redirect non-Alpha authenticated routes to `/dashboard`.

```typescript
import { IS_ALPHA_MODE } from '@/lib/alpha-mode';
// OR inline: const IS_ALPHA_MODE = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';
// (middleware may not support @ imports depending on Next.js config)

const ALPHA_ROUTE_PREFIXES = [
  '/dashboard',
  '/library',
  '/chat',
  '/discover',
  '/games',        // legacy redirect
  '/profile',
  '/settings',
  '/admin/overview',
  '/admin/shared-games',
  '/admin/knowledge-base',
  '/admin/content',
  '/admin/users',
  '/onboarding',
  '/upload',
  '/setup',
  '/join',
];
```

Add to the middleware function (after auth checks):

```typescript
if (IS_ALPHA_MODE) {
  const pathname = request.nextUrl.pathname;
  const isAlphaRoute = ALPHA_ROUTE_PREFIXES.some(prefix => pathname.startsWith(prefix));
  const isAuthRoute = pathname.startsWith('/login') || pathname.startsWith('/register')
    || pathname.startsWith('/reset-password') || pathname.startsWith('/verify-email')
    || pathname.startsWith('/oauth-callback') || pathname.startsWith('/setup-account');
  const isPublicRoute = pathname === '/' || pathname.startsWith('/api/')
    || pathname.startsWith('/health') || pathname.startsWith('/offline');

  if (!isAlphaRoute && !isAuthRoute && !isPublicRoute) {
    return NextResponse.redirect(new URL('/dashboard', request.url));
  }
}
```

- [ ] **Step 3: Verify non-Alpha routes redirect**

Manual test in browser:
- Navigate to `/agents` → should redirect to `/dashboard`
- Navigate to `/game-nights` → should redirect to `/dashboard`
- Navigate to `/admin/agents` → should redirect to `/dashboard`
- Navigate to `/sessions` → should redirect to `/dashboard`
- Navigate to `/dashboard` → should load normally
- Navigate to `/chat` → should load normally
- Navigate to `/library` → should load normally

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/middleware.ts
git commit -m "feat(web): alpha mode route guard in middleware

Non-Alpha routes redirect to /dashboard when NEXT_PUBLIC_ALPHA_MODE=true.
Blocked: agents, sessions, play-records, players, knowledge-base, game-nights,
admin AI/system/analytics, notifications, badges, chess, pipeline-builder."
```

---

## Task 5: Docker — Alpha Compose Profile

**Files:**
- Create: `infra/docker-compose.alpha.yml` (override for alpha mode)
- Modify: `infra/Makefile` (add `alpha` target)

- [ ] **Step 1: Create alpha compose override**

```yaml
# docker-compose.alpha.yml — Alpha Zero configuration
# Sets ALPHA_MODE for backend and frontend.
#
# For local dev:    make alpha
# For core only:    make alpha-core
#
# NOTE: NEXT_PUBLIC_ALPHA_MODE is a BUILD-TIME variable.
# For Docker builds, it must be passed as a build arg.
# For local dev (pnpm dev), set it in .env.local instead.

services:
  api:
    environment:
      - ALPHA_MODE=true

  web:
    build:
      args:
        - NEXT_PUBLIC_ALPHA_MODE=true
    environment:
      - NEXT_PUBLIC_ALPHA_MODE=true
```

- [ ] **Step 2: Verify Dockerfile supports build arg**

Check `apps/web/Dockerfile` for `ARG NEXT_PUBLIC_ALPHA_MODE`. If not present, add it before the `RUN pnpm build` step:

```dockerfile
ARG NEXT_PUBLIC_ALPHA_MODE=false
ENV NEXT_PUBLIC_ALPHA_MODE=$NEXT_PUBLIC_ALPHA_MODE
```

- [ ] **Step 3: Add Makefile targets**

Add to `infra/Makefile`:

```makefile
## Alpha Zero (minimal feature set)
alpha: ## Start Alpha Zero with AI services
	docker compose -f docker-compose.yml -f compose.dev.yml -f docker-compose.alpha.yml --profile ai up -d

alpha-core: ## Start Alpha Zero core only (no AI services)
	docker compose -f docker-compose.yml -f compose.dev.yml -f docker-compose.alpha.yml up -d

alpha-down: ## Stop Alpha Zero environment
	docker compose -f docker-compose.yml -f compose.dev.yml -f docker-compose.alpha.yml --profile ai down
```

Note: Includes `compose.dev.yml` for dev-specific port bindings, secrets, and env files.

- [ ] **Step 4: Commit**

```bash
git add infra/docker-compose.alpha.yml infra/Makefile
git commit -m "infra: add alpha zero compose profile and Makefile targets

docker-compose.alpha.yml sets ALPHA_MODE=true for api and web.
Web build arg ensures NEXT_PUBLIC_ALPHA_MODE is baked at build time.
make alpha / make alpha-core / make alpha-down for quick startup.
Includes compose.dev.yml for proper dev port/secret configuration."
```

---

## Task 6: End-to-End Validation — The 4 Alpha Scenarios

**Files:**
- No new files — manual testing against running Alpha instance

- [ ] **Step 1: Start Alpha environment**

```bash
cd infra && make alpha
```

Wait for all services to be healthy: `docker compose ps`

- [ ] **Step 2: Scenario 1 — Registration**

1. Open http://localhost:3000
2. Click Register
3. Create account with email/password
4. Verify login succeeds
5. Verify dashboard loads
6. Verify dashboard has NO links to agents, sessions, game nights

Expected: Clean registration flow, no broken UI, dashboard shows only Alpha content

- [ ] **Step 3: Scenario 2 — Add a game**

1. Navigate to Library or Discover
2. Search for "Gloomhaven" via BGG
3. Add game to personal collection
4. Verify game appears in Library (Collection or Private tab)

Expected: BGG search works, game added successfully

- [ ] **Step 4: Scenario 3 — Upload rulebook**

1. Navigate to a game detail or Upload section
2. Upload a PDF rulebook (<50MB)
3. Wait for processing to complete
4. Verify document appears in KB

Expected: PDF uploads, processing starts, document indexed

- [ ] **Step 5: Scenario 4 — Chat about rules**

1. Navigate to Chat → New chat
2. Select the game with uploaded rulebook
3. Ask: "How does combat work?"
4. Verify response includes sourced answer from rulebook

Expected: RAG pipeline works, response has citations

- [ ] **Step 6: Verify non-Alpha routes are hidden**

1. Try navigating to `/agents` → should redirect to dashboard
2. Try navigating to `/game-nights` → should redirect to dashboard
3. Try navigating to `/admin/agents` → should redirect to dashboard
4. Verify nav bar only shows: Dashboard, Library, Chat, Discover, Admin
5. Verify admin sidebar only shows: Overview, Content, Users

Expected: All dormant features hidden from UI

- [ ] **Step 7: Document any broken paths**

If any scenario fails, document the issue and fix before proceeding.

- [ ] **Step 8: Commit any fixes**

```bash
git commit -m "fix: alpha zero E2E validation fixes"
```

---

## Task 7: Cleanup & Documentation

**Files:**
- Modify: `CLAUDE.md` (update with Alpha mode info)
- Create: `docs/alpha-zero-scope.md` (reference doc)

- [ ] **Step 1: Add Alpha mode section to CLAUDE.md**

Add under Quick Reference table:

```markdown
### Alpha Mode

Set `ALPHA_MODE=true` (backend) and `NEXT_PUBLIC_ALPHA_MODE=true` (frontend, build-time) to run in Alpha Zero mode.

**Alpha scope**: Auth → Games + BGG → PDF upload → RAG Chat → Library (collection + private)
**Active BCs**: Authentication, GameManagement, DocumentProcessing, KnowledgeBase, UserLibrary
**Admin**: Overview, Users, Content (trimmed) sections only
**Docker**: `cd infra && make alpha`
**Disable**: Set both vars to `false` and rebuild web (`pnpm build`)

⚠️ `NEXT_PUBLIC_ALPHA_MODE` is a **build-time** variable — changing it requires rebuild, not just restart.
```

- [ ] **Step 2: Create alpha scope reference doc**

Create `docs/alpha-zero-scope.md` with:
- Active vs dormant bounded contexts table
- Active vs hidden frontend routes
- Docker service matrix (6 alpha vs 13 full)
- How to toggle alpha mode on/off
- Known limitations (e.g., dormant dashboard widgets hidden but routes exist)

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md docs/alpha-zero-scope.md
git commit -m "docs: add Alpha Zero mode documentation

Document Alpha scope, active BCs, route guards, Docker targets.
Note build-time nature of NEXT_PUBLIC_ALPHA_MODE."
```

---

## Summary

| Task | What | Est. |
|------|------|------|
| 1 | Backend ALPHA_MODE endpoint gating | 30 min |
| 2 | Frontend nav trim (in-place filtering) | 30 min |
| 3 | Dashboard widget cleanup | 30 min |
| 4 | Middleware route guard | 20 min |
| 5 | Docker alpha compose + Makefile | 15 min |
| 6 | E2E validation (4 scenarios) | 45 min |
| 7 | Documentation | 15 min |
| **Total** | | **~3 hours** |

All changes are reversible by setting `ALPHA_MODE=false` / `NEXT_PUBLIC_ALPHA_MODE=false` + rebuild.

---

## Review Issues Addressed

| Issue | Fix Applied |
|-------|------------|
| ❌ CRITICAL-1: NEXT_PUBLIC is build-time | Added build args to Docker, noted build-time throughout |
| ❌ CRITICAL-2: MapRuleSpecEndpoints missing | Added to Alpha block |
| ❌ CRITICAL-3: GameStateHub unaddressed | Added explicit note + always-registered block |
| ⚠️ MAJOR-1: Dashboard hard links | Added Task 3 (dashboard widget cleanup) |
| ⚠️ MAJOR-2: UNIFIED_NAV_ITEMS consumer bypass | In-place filtering — export name unchanged |
| ⚠️ MAJOR-3: LIBRARY_TABS consumer bypass | In-place filtering — export name unchanged |
| ⚠️ MAJOR-4: DASHBOARD_SECTIONS consumer bypass | In-place filtering — export name unchanged |
| ⚠️ MAJOR-5: PrivateGameEndpoints in wrong block | Moved to Alpha block |
| ℹ️ MINOR-1: GetEnvironmentVariable vs IConfiguration | Uses app.Configuration |
| ℹ️ MINOR-4: Missing compose.dev.yml | Added to Makefile targets |
