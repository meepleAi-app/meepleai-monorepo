# Monorepo Audit Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all critical/major issues identified by the spec-panel audit: security holes, architecture violations, dead code, bundle bloat, missing error boundaries, and stale docs.

**Architecture:** Organized by priority (P0→P3). Each task is independent — can be parallelized across subagents. Backend fixes use existing `NotFoundException`/`ConflictException` from `Api.Middleware.Exceptions`. Frontend fixes follow existing patterns (e.g., `error.tsx` from `library/error.tsx`).

**Tech Stack:** .NET 9 (MediatR, EF Core) | Next.js 16 (React 19, Zustand, Tailwind 4)

---

## P0 — Security (CRITICAL)

### Task 1: Remove console.log from proxy middleware

**Files:**
- Modify: `apps/web/src/proxy.ts:127-131, 139-141, 154-157, 178-179`

- [ ] **Step 1: Replace console.log with conditional debug logger**

In `apps/web/src/proxy.ts`, replace all 4 `console.log` calls with the existing `logger` (if available) or remove them entirely. The proxy runs on every authenticated request — logging cookie fragments is a data leak.

```typescript
// REMOVE these 4 blocks (lines ~127-131, ~139-141, ~154-157, ~178-179):

// ❌ DELETE:
// eslint-disable-next-line no-console
console.log(
  `[proxy] Session validation CACHE HIT for ${cookieValue.substring(0, 10)}... valid=${cached.valid}`
);

// ❌ DELETE:
// eslint-disable-next-line no-console
console.log('[proxy] No cookie header found in request');

// ❌ DELETE:
// eslint-disable-next-line no-console
console.log(
  `[proxy] Validating session at ${apiUrl} with cookie: ${cookieHeader.substring(0, 50)}...`
);

// ❌ DELETE:
// eslint-disable-next-line no-console
console.log(`[proxy] Session validation response: ${response.status} ok=${response.ok}`);
```

Remove all 4 blocks entirely. The `metrics.recordCacheHit()` / `recordCacheMiss()` / `recordValidationSuccess()` / `recordValidationFailure()` calls already provide observability without leaking data.

- [ ] **Step 2: Verify proxy still works**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/proxy.ts
git commit -m "fix(security): remove console.log leaking cookie data from proxy middleware"
```

---

### Task 2: Fix dummy auth token in useGameStateSignalR

**Files:**
- Modify: `apps/web/src/hooks/useGameStateSignalR.ts:47-49`

- [ ] **Step 1: Replace hardcoded token with real auth**

The hook already runs inside authenticated pages. Use the session cookie (passed automatically by the browser) instead of a bearer token. SignalR over the same origin with `withCredentials` sends cookies automatically.

```typescript
// In useGameStateSignalR.ts, replace lines 46-50:
// ❌ OLD:
.withUrl(hubUrl, {
  accessTokenFactory: async () => {
    // TODO: Get auth token from your auth system
    return 'your-auth-token';
  },
})

// ✅ NEW:
.withUrl(hubUrl, {
  withCredentials: true,
})
```

- [ ] **Step 2: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/useGameStateSignalR.ts
git commit -m "fix(security): replace hardcoded dummy auth token in SignalR hook with cookie auth"
```

---

### Task 3: Add auth to workflow error and Prometheus endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/WorkflowEndpoints.cs:305-309`
- Modify: `apps/api/src/Api/Routing/AlertEndpoints.cs:28-32`

- [ ] **Step 1: Add API key validation to workflow error endpoint**

In `WorkflowEndpoints.cs`, add `.RequireAuthorization()` to the `/logs/workflow-error` endpoint. N8N should send the auth cookie or API key — unauthenticated POST endpoints that write to DB are a storage DoS vector.

```csharp
// WorkflowEndpoints.cs line ~305, AFTER .WithDescription(...)
// ❌ OLD: no auth
.Produces(StatusCodes.Status200OK)
.Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

// ✅ NEW: require auth
.RequireAuthorization()
.Produces(StatusCodes.Status200OK)
.Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);
```

Update the `.WithDescription()` on line ~308:
```csharp
// ❌ OLD:
.WithDescription("Webhook endpoint for n8n to log workflow errors (no auth required)")
// ✅ NEW:
.WithDescription("Webhook endpoint for n8n to log workflow errors")
```

- [ ] **Step 2: Add auth to Prometheus webhook endpoint**

In `AlertEndpoints.cs`, add `.RequireAuthorization()`:

```csharp
// AlertEndpoints.cs line ~28-32
// ❌ OLD:
group.MapPost("/alerts/prometheus", ProcessPrometheusWebhookAsync)
.WithName("PrometheusAlertWebhook")
.WithTags("Alerting")
.WithDescription("Webhook endpoint for Prometheus AlertManager (no auth required)")
.Produces(StatusCodes.Status200OK);

// ✅ NEW:
group.MapPost("/alerts/prometheus", ProcessPrometheusWebhookAsync)
.WithName("PrometheusAlertWebhook")
.WithTags("Alerting")
.WithDescription("Webhook endpoint for Prometheus AlertManager")
.RequireAuthorization()
.Produces(StatusCodes.Status200OK);
```

- [ ] **Step 3: Build backend**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/WorkflowEndpoints.cs apps/api/src/Api/Routing/AlertEndpoints.cs
git commit -m "fix(security): add auth to workflow-error and prometheus webhook endpoints"
```

---

### Task 4: Audit git history for committed secrets

**Files:**
- No code changes — audit + rotation task

- [ ] **Step 1: Check if secrets were ever committed**

```bash
git log --all --full-history --diff-filter=A -- "infra/secrets/*.secret" --oneline
```

Expected: Either empty (never committed) or a list of commits (compromised — must rotate).

- [ ] **Step 2: Verify .gitignore coverage**

```bash
cat .gitignore | grep -i secret
```

Expected: `infra/secrets/*.secret` pattern present.

- [ ] **Step 3: Document findings and rotation needs**

If any secrets were committed, create a follow-up issue for key rotation. At minimum:
- OpenRouter API key
- DeepSeek API key
- OAuth client secrets (Google, GitHub, Discord)
- R2 storage credentials
- Gmail App Password (revoke immediately)
- Slack webhook URL

---

## P1 — Architecture & Consistency

### Task 5: Replace InvalidOperationException with proper exceptions in handlers

**Files:**
- Modify: Multiple handler files (see list below)

The codebase already has `NotFoundException` at `Api.Middleware.Exceptions.NotFoundException` (→ 404) and `ConflictException` at `Api.Middleware.Exceptions.ConflictException` (→ 409). The `InvalidOperationException` bubbles as 500.

**Category A — "Not found" errors → `NotFoundException`:**

- [ ] **Step 1: Fix Administration handlers**

```csharp
// CreatePromptTemplateCommandHandler.cs:44
// ❌ OLD:
throw new InvalidOperationException($"User {command.CreatedByUserId} not found");
// ✅ NEW:
throw new NotFoundException("User", command.CreatedByUserId.ToString());

// CreatePromptVersionCommandHandler.cs — same pattern for "Template X not found" and "User X not found"
// UpdateAlertRuleCommandHandler.cs:19 — "AlertRule X not found"
// EnableAlertRuleCommandHandler.cs:24 — same
// GetAlertConfigurationQueryHandler.cs:28 — "No configuration found"
// GetPromptVersionsQueryHandler.cs:33 — "Prompt template X not found"
// GetPromptVersionHistoryQueryHandler.cs:32 — "Template not found"
// GetPromptAuditLogQueryHandler.cs:37 — "Template not found"
// GenerateEvaluationReportQueryHandler.cs:37 — "Evaluation X not found"
```

Add `using Api.Middleware.Exceptions;` to each file's usings.

Replace each `throw new InvalidOperationException($"X not found")` with:
```csharp
throw new NotFoundException("ResourceType", resourceId?.ToString());
```

- [ ] **Step 2: Fix GameManagement handlers**

```csharp
// UpdateRuleSpecCommandHandler.cs:72 — "Game {gameId} not found"
throw new NotFoundException("Game", gameId.ToString());

// UpdateRuleSpecCommandHandler.cs:80 — "User {userId} not found"
throw new NotFoundException("User", userId.ToString());

// GenerateRuleSpecFromPdfCommandHandler.cs:39 — "PDF document {id} not found"
throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());

// StartGameSessionCommandHandler.cs:66 — "Game with ID not found"
throw new NotFoundException("Game", command.GameId.ToString());

// ResumeGameSessionCommandHandler.cs:32 — "Session with ID not found"
throw new NotFoundException("GameSession", command.SessionId.ToString());

// AbandonGameSessionCommandHandler.cs:32 — same
throw new NotFoundException("GameSession", command.SessionId.ToString());

// UpdateRuleCommentCommandHandler.cs:40 — "Comment {id} not found"
throw new NotFoundException("RuleComment", command.CommentId.ToString());

// ResolveRuleCommentCommandHandler.cs:36 — same
throw new NotFoundException("RuleComment", command.CommentId.ToString());

// ComputeRuleSpecDiffQueryHandler.cs:38,58 — "RuleSpec version X not found"
throw new NotFoundException("RuleSpecVersion", query.FromVersion.ToString());
throw new NotFoundException("RuleSpecVersion", query.ToVersion.ToString());

// GetQuickQuestionsQueryHandler.cs:44 (SharedGameCatalog)
throw new NotFoundException("SharedGame", query.SharedGameId.ToString());
```

- [ ] **Step 3: Fix AgentMemory handlers — feature flag disabled**

These throw `InvalidOperationException("Feature X is disabled")`. This is not a 404 or 409 — it's a 403 (Forbidden). If no `ForbiddenException` exists, use `ConflictException` as the closest semantic match that doesn't produce 500:

```csharp
// AddHouseRuleCommandHandler.cs:43, CreateGroupMemoryCommandHandler.cs:42,
// UpdateGroupPreferencesCommandHandler.cs:44, ClaimGuestPlayerCommandHandler.cs:43,
// AddMemoryNoteCommandHandler.cs:42

// ❌ OLD:
throw new InvalidOperationException("Feature AgentMemory.Enabled is disabled");
// ✅ NEW:
throw new ConflictException("Feature AgentMemory.Enabled is disabled");
```

**Category B — Conflict errors → `ConflictException`:**

- [ ] **Step 4: Fix conflict-type IOEs**

```csharp
// CreatePromptTemplateCommandHandler.cs:37
// ❌ OLD:
throw new InvalidOperationException($"Template with name '{command.Name}' already exists");
// ✅ NEW:
throw new ConflictException($"Template with name '{command.Name}' already exists");

// UpdateRuleSpecCommandHandler.cs:125 — "Version X already exists"
throw new ConflictException($"Version {version} already exists for game {command.GameId}");

// InitializeGameStateCommandHandler.cs:59 — "already has state initialized"
throw new ConflictException($"GameSession {command.GameSessionId} already has state initialized");
```

- [ ] **Step 5: Build and run tests**

```bash
cd apps/api/src/Api && dotnet build
cd ../../../.. && cd apps/api/tests/Api.Tests && dotnet test --filter "Category=Unit" --no-build
```

Expected: Build succeeds. Some tests may need updating if they assert `InvalidOperationException` — fix those too.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/
git commit -m "fix(backend): replace InvalidOperationException with NotFoundException/ConflictException in 25+ handlers

Fixes issue #2568 compliance. InvalidOperationException was producing 500 for
business errors that should be 404 or 409."
```

---

### Task 6: Delete orphan Zustand stores

**Files:**
- Delete: `apps/web/src/lib/stores/navStore.ts` (zero imports — `nav-store.ts` is canonical)
- Delete: `apps/web/src/lib/stores/gameTableDrawerStore.ts` (zero imports — `game-table-drawer-store.ts` is canonical)
- Delete: `apps/web/src/lib/stores/cascadeNavigationStore.ts` (only imported by its own test)
- Delete: `apps/web/src/lib/stores/__tests__/cascadeNavigationStore.test.ts` (tests the orphan)

- [ ] **Step 1: Verify no production code imports these files**

```bash
cd apps/web
grep -r "navStore" src/ --include="*.ts" --include="*.tsx" | grep -v "nav-store" | grep -v node_modules | grep -v __tests__
grep -r "gameTableDrawerStore" src/ --include="*.ts" --include="*.tsx" | grep -v "game-table-drawer-store" | grep -v node_modules | grep -v __tests__
grep -r "cascadeNavigationStore" src/ --include="*.ts" --include="*.tsx" | grep -v "cascade-navigation-store" | grep -v node_modules | grep -v __tests__
```

Expected: No results (zero production imports).

- [ ] **Step 2: Delete orphan files**

```bash
rm apps/web/src/lib/stores/navStore.ts
rm apps/web/src/lib/stores/gameTableDrawerStore.ts
rm apps/web/src/lib/stores/cascadeNavigationStore.ts
rm apps/web/src/lib/stores/__tests__/cascadeNavigationStore.test.ts
```

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -u apps/web/src/lib/stores/
git commit -m "chore(frontend): delete 3 orphan Zustand stores (navStore, gameTableDrawerStore, cascadeNavigationStore)

These were duplicates of the canonical kebab-case versions. Zero production imports."
```

---

### Task 7: Delete orphan addGameWizardStore (old version)

**Files:**
- Delete: `apps/web/src/stores/addGameWizardStore.ts`
- Modify: `apps/web/src/hooks/useAddGameWizard.ts:8` — repoint to canonical store
- Modify: `apps/web/src/__tests__/stores/addGameWizardStore.test.ts` — repoint or delete
- Modify: `apps/web/src/__tests__/components/collection/wizard/SearchSelectGame.test.tsx:11`
- Modify: `apps/web/src/__tests__/components/collection/AddGameWizard.test.tsx:11`

This is a larger task — the old store (`src/stores/addGameWizardStore.ts`) has different types than the canonical one (`src/lib/stores/add-game-wizard-store.ts`). The old store is used by `useAddGameWizard.ts` hook and 3 test files.

- [ ] **Step 1: Analyze type differences between the two stores**

Read both stores fully and map type differences:
- Old: `WizardStep`, `CustomGameData`, `useAddGameWizardStore`
- New: `WizardStep`, `WizardEntryPoint`, `SelectedGameData`, `useAddGameWizardStore`

Determine which components actually use the old hook vs the new store directly.

- [ ] **Step 2: Check if useAddGameWizard hook is used in production**

```bash
grep -r "useAddGameWizard" apps/web/src/ --include="*.ts" --include="*.tsx" | grep -v __tests__ | grep -v node_modules
```

If `useAddGameWizard` is not used in production components (only in tests and its own definition), delete the hook and its tests. If it IS used, migrate it to import from `@/lib/stores/add-game-wizard-store`.

- [ ] **Step 3: Delete or migrate based on findings**

If unused in production:
```bash
rm apps/web/src/stores/addGameWizardStore.ts
rm apps/web/src/hooks/useAddGameWizard.ts
rm apps/web/src/__tests__/stores/addGameWizardStore.test.ts
rm apps/web/src/__tests__/components/collection/wizard/SearchSelectGame.test.tsx
rm apps/web/src/__tests__/components/collection/AddGameWizard.test.tsx
```

If used: migrate imports to `@/lib/stores/add-game-wizard-store` and update types.

- [ ] **Step 4: Verify build**

Run: `cd apps/web && pnpm build && pnpm test --run`
Expected: Both pass.

- [ ] **Step 5: Commit**

```bash
git add -u apps/web/src/
git commit -m "chore(frontend): consolidate add-game wizard to single canonical store

Removes duplicate addGameWizardStore.ts and related dead hook/tests.
The canonical store at lib/stores/add-game-wizard-store.ts is the only source of truth."
```

---

### Task 8: Delete AgentChatPOC.tsx

**Files:**
- Delete: `apps/web/src/components/agent/AgentChatPOC.tsx`

- [ ] **Step 1: Verify no imports**

```bash
grep -r "AgentChatPOC" apps/web/src/ --include="*.ts" --include="*.tsx" | grep -v "AgentChatPOC.tsx"
```

Expected: No results.

- [ ] **Step 2: Delete the file**

```bash
rm apps/web/src/components/agent/AgentChatPOC.tsx
```

- [ ] **Step 3: Commit**

```bash
git add -u apps/web/src/components/agent/AgentChatPOC.tsx
git commit -m "chore(frontend): delete orphan AgentChatPOC component (zero imports)"
```

---

## P2 — Bundle Optimization & Error Boundaries

### Task 9: Add dynamic() imports for Monaco and Recharts

**Files:**
- Modify: `apps/web/src/components/admin/agent-builder/PromptEditorStep.tsx:10`
- Modify: `apps/web/src/components/admin/charts/APIRequestsChart.tsx:15`
- Modify: `apps/web/src/components/admin/charts/AIUsageDonut.tsx:14`
- Modify: `apps/web/src/components/admin/usage/CostBreakdownPanel.tsx:12`

- [ ] **Step 1: Wrap Monaco import in PromptEditorStep**

```tsx
// PromptEditorStep.tsx
// ❌ OLD (line 10):
import Editor from '@monaco-editor/react';

// ✅ NEW:
import dynamic from 'next/dynamic';
const Editor = dynamic(() => import('@monaco-editor/react'), {
  ssr: false,
  loading: () => <div className="h-64 animate-pulse rounded-lg bg-muted" />,
});
```

- [ ] **Step 2: Wrap Recharts imports in chart components**

For each of `APIRequestsChart.tsx`, `AIUsageDonut.tsx`, `CostBreakdownPanel.tsx`, wrap the recharts imports:

```tsx
// ❌ OLD:
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';

// ✅ NEW — re-export pattern:
// Keep the static import but wrap the entire component export with dynamic()
// in the parent that imports these charts.
```

Alternative (simpler): In the files that import these chart components, use `dynamic()`:

```tsx
// In the parent component that renders APIRequestsChart:
const APIRequestsChart = dynamic(
  () => import('@/components/admin/charts/APIRequestsChart'),
  { ssr: false, loading: () => <div className="h-48 animate-pulse rounded-lg bg-muted" /> }
);
```

Check which parent components import them and add `dynamic()` there.

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds. Admin charts load lazily.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/admin/
git commit -m "perf(frontend): lazy-load Monaco Editor and Recharts in admin components

Saves ~1MB from initial admin bundle (Monaco ~800KB, Recharts ~250KB)."
```

---

### Task 10: Add missing error.tsx for 5 route groups

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/error.tsx`
- Create: `apps/web/src/app/(authenticated)/sessions/[id]/error.tsx`
- Create: `apps/web/src/app/(authenticated)/game-nights/error.tsx`
- Create: `apps/web/src/app/(authenticated)/players/error.tsx`
- Create: `apps/web/src/app/(authenticated)/profile/error.tsx`

- [ ] **Step 1: Create error.tsx for dashboard**

Follow the existing pattern from `apps/web/src/app/(authenticated)/library/error.tsx`:

```tsx
// apps/web/src/app/(authenticated)/dashboard/error.tsx
'use client';

import { useEffect } from 'react';

import { AlertTriangle, ArrowLeft, RefreshCcw } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { logger } from '@/lib/logger';

interface ErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function DashboardError({ error, reset }: ErrorProps) {
  useEffect(() => {
    logger.error('Dashboard Error:', error);
  }, [error]);

  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div
        className="w-full max-w-md overflow-hidden rounded-2xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9] p-8 text-center"
        style={{ boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)' }}
      >
        <div className="mx-auto mb-6 flex h-14 w-14 items-center justify-center rounded-xl bg-[hsla(25,95%,38%,0.1)]">
          <AlertTriangle className="h-7 w-7 text-[hsl(25,95%,38%)]" />
        </div>
        <h2 className="mb-2 font-quicksand text-xl font-bold text-[#2D2A26]">
          Errore dashboard
        </h2>
        <p className="mb-6 font-nunito text-sm text-[#6B665C]">
          Si è verificato un errore nel caricamento. Riprova o torna indietro.
        </p>
        {process.env.NODE_ENV === 'development' && (
          <div className="mb-6 rounded-xl bg-[rgba(45,42,38,0.04)] p-3 text-left">
            <p className="mb-1 font-quicksand text-xs font-semibold uppercase tracking-wider text-[#9C958A]">
              Dettagli errore
            </p>
            <p className="font-mono text-xs text-[#6B665C]">{error.message}</p>
            {error.digest && (
              <p className="mt-1 font-mono text-xs text-[#9C958A]">Digest: {error.digest}</p>
            )}
          </div>
        )}
        <div className="flex flex-col gap-3 sm:flex-row sm:justify-center">
          <Button
            onClick={reset}
            className="bg-[hsl(25,95%,38%)] font-quicksand font-semibold text-white hover:bg-[hsl(25,95%,45%)]"
          >
            <RefreshCcw className="mr-2 h-4 w-4" />
            Riprova
          </Button>
          <Button
            asChild
            variant="outline"
            className="border-[rgba(45,42,38,0.12)] font-quicksand font-semibold text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]"
          >
            <Link href="/dashboard">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Dashboard
            </Link>
          </Button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Create error.tsx for remaining 4 routes**

Repeat the same pattern for each route, changing only:
- Component name: `SessionDetailError`, `GameNightsError`, `PlayersError`, `ProfileError`
- Title text: `"Errore sessione"`, `"Errore serate di gioco"`, `"Errore giocatori"`, `"Errore profilo"`
- Back link href: `/sessions`, `/game-nights`, `/players`, `/profile`

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/error.tsx \
       apps/web/src/app/\(authenticated\)/sessions/\[id\]/error.tsx \
       apps/web/src/app/\(authenticated\)/game-nights/error.tsx \
       apps/web/src/app/\(authenticated\)/players/error.tsx \
       apps/web/src/app/\(authenticated\)/profile/error.tsx
git commit -m "feat(frontend): add error.tsx boundaries for dashboard, sessions, game-nights, players, profile"
```

---

## P3 — Documentation & Cleanup

### Task 11: Archive executed plans

**Files:**
- Move: `docs/superpowers/plans/2026-04-15-deepseek-llm-client.md` → `docs/superpowers/plans/archived/`
- Move: `docs/superpowers/plans/2026-04-17-session-vision-ai.md` → `docs/superpowers/plans/archived/`
- Move: `docs/superpowers/plans/2026-04-18-chat-inline-citations.md` → `docs/superpowers/plans/archived/`
- Move: `docs/superpowers/plans/2026-04-16-copyright-leak-guard.md` → `docs/superpowers/plans/archived/`
- Move: `docs/superpowers/plans/2026-04-17-manapips-interactive-flow.md` → `docs/superpowers/plans/archived/`
- Move: `docs/superpowers/plans/2026-04-14-batch-embedding-rag-test-snapshot.md` → `docs/superpowers/plans/archived/`

- [ ] **Step 1: Verify each plan's executed status**

Cross-reference with MEMORY.md executed plans list and recent git commits:
- `deepseek-llm-client` → PR#421 ✅
- `session-vision-ai` → PR#466 ✅
- `chat-inline-citations` → PR#470 ✅
- `copyright-leak-guard` → check if PR#470 covers it
- `manapips-interactive-flow` → tests exist, verify merge
- `batch-embedding-rag-test-snapshot` → MEMORY notes snapshot created

```bash
git log --oneline --all | grep -i "deepseek\|vision\|citation\|copyright\|manapips\|batch-embed"
```

- [ ] **Step 2: Move confirmed executed plans to archived/**

```bash
mkdir -p docs/superpowers/plans/archived
# Move each confirmed plan:
mv docs/superpowers/plans/2026-04-15-deepseek-llm-client.md docs/superpowers/plans/archived/
# Repeat for each confirmed plan
```

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/plans/
git commit -m "docs: archive 6 executed plans (deepseek, vision, citations, copyright, manapips, batch-embed)"
```

---

### Task 12: Update ADR-007 for DeepSeek provider

**Files:**
- Modify: `docs/architecture/adr/adr-007-hybrid-llm.md`

- [ ] **Step 1: Read current ADR-007**

```bash
cat docs/architecture/adr/adr-007-hybrid-llm.md
```

- [ ] **Step 2: Add DeepSeek as third provider**

Add a section documenting:
- DeepSeek as third LLM provider (OpenAI-compatible API)
- Routing chain: OpenRouter → DeepSeek → Ollama fallback
- Cost: $0.14/M tokens
- Secret: `infra/secrets/deepseek.secret` with `DEEPSEEK_API_KEY`
- PR reference: #421

- [ ] **Step 3: Commit**

```bash
git add docs/architecture/adr/adr-007-hybrid-llm.md
git commit -m "docs(adr): update ADR-007 to include DeepSeek as third LLM provider (PR#421)"
```

---

### Task 13: Update user-stories-tracking.md

**Files:**
- Modify: `docs/roadmap/user-stories-tracking.md`

- [ ] **Step 1: Read current tracking file**

```bash
cat docs/roadmap/user-stories-tracking.md
```

- [ ] **Step 2: Update completed items**

Mark as complete:
- US-52 (foto tavolo alla chat) → Implemented by PR#466 (Session Vision AI)
- Any other US items completed by recent PRs (#468, #469, #470)

Update the "Last updated" date to `2026-04-18`.
Update counts in the metrics table.

- [ ] **Step 3: Commit**

```bash
git add docs/roadmap/user-stories-tracking.md
git commit -m "docs(roadmap): update user stories tracking — mark US-52 complete, update counts"
```

---

### Task 14: Delete stale orphan planning docs

**Files:**
- Delete: `docs/development/orphan-components-integration-plan.md`
- Delete: `docs/development/orphan-components-follow-up-plan.md`
- Delete: `docs/development/orphan-components-follow-up-plan-v2.md`

- [ ] **Step 1: Verify these plans are fully superseded**

The v2-only-consolidation PRs (#336, #339, #344, #347) deleted all the orphaned components these plans describe. Confirm no remaining items:

```bash
grep -l "TODO\|PENDING\|NOT DONE" docs/development/orphan-components-*.md
```

- [ ] **Step 2: Delete stale planning docs**

```bash
rm docs/development/orphan-components-integration-plan.md
rm docs/development/orphan-components-follow-up-plan.md
rm docs/development/orphan-components-follow-up-plan-v2.md
```

- [ ] **Step 3: Commit**

```bash
git add -u docs/development/
git commit -m "docs: delete 3 stale orphan-component planning docs (superseded by v2-consolidation PRs)"
```

---

## Summary

| Priority | Tasks | Effort | Impact |
|----------|-------|--------|--------|
| **P0** | 1-4 (security) | ~4h | Eliminates active security risks |
| **P1** | 5-8 (architecture) | ~6h | Correct HTTP status codes, remove dead code |
| **P2** | 9-10 (bundle/UX) | ~4h | ~1MB bundle savings, error resilience |
| **P3** | 11-14 (docs) | ~3h | Documentation accuracy, cleaner repo |
| **Total** | 14 tasks | ~17h | — |

**Parallelization:** Tasks 1-4 are independent. Tasks 5-8 are independent. Tasks 9-10 are independent. Tasks 11-14 are independent. Maximum parallelism: 4 agents per priority tier.
