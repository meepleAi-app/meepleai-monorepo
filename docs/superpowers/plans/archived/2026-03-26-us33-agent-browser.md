# US-33: Agent Browser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Activate the agent browser allowing users to discover, view details, and start chats with published AI agents.

**Architecture:** Frontend-only activation. Agent pages exist but are alpha-gated. Wire up the alpha route allowlist, add `agents` to `ALPHA_NAV_IDS`, and verify the existing pages work end-to-end with the real API client. No new API client work needed — `createAgentsClient` already exists with `getAll`, `getById`, `getSlots`.

**Tech Stack:** Next.js 16, React 19, Zustand, Tailwind 4, shadcn/ui

**Branch:** `feature/us-33-agent-browser` from `frontend-dev`
**PR Target:** `frontend-dev`

---

## Task 1: Create branch and add `/agents` to alpha route allowlist

**Files:**
- Modify: `apps/web/src/proxy.ts`
- Modify: `apps/web/src/config/navigation.ts`

**Steps:**

- [ ] Create branch `feature/us-33-agent-browser` from `frontend-dev`
- [ ] In `apps/web/src/proxy.ts`, add `'/agents'` to `ALPHA_ROUTE_PREFIXES` array (around line 405-423):

```typescript
const ALPHA_ROUTE_PREFIXES = [
  '/dashboard',
  '/library',
  '/chat',
  '/discover',
  '/games',
  '/profile',
  '/settings',
  '/admin',
  '/admin/overview',
  '/admin/shared-games',
  '/admin/knowledge-base',
  '/admin/content',
  '/admin/users',
  '/onboarding',
  '/upload',
  '/setup',
  '/join',
  '/agents',  // US-33: Agent browser
];
```

- [ ] In `apps/web/src/config/navigation.ts`, add `'agents'` to `ALPHA_NAV_IDS` set (around line 208):

```typescript
const ALPHA_NAV_IDS = new Set([
  'welcome',
  'dashboard',
  'library',
  'chat',
  'catalog',
  'profile',
  'admin',
  'agents',  // US-33: Agent browser
]);
```

**Verify:**
```bash
cd apps/web && pnpm typecheck
```

**Commit:** `feat(web): add /agents route to alpha allowlist (US-33)`

---

## Task 2: Clean up agents list page for user context

**Files:**
- Modify: `apps/web/src/app/(authenticated)/agents/page.tsx`

**Steps:**

The existing agents page is already fully functional with search, filtering, sorting, MeepleCard grid, infinite scroll, and the agent creation sheet. However, it currently includes admin-oriented features that should be reviewed.

- [ ] Review the page for any admin-only imports or features that should be conditionally shown
- [ ] Verify the `useAgents({ activeOnly: true })` call correctly filters to only published/active agents for regular users
- [ ] Verify the `AgentCreationSheet` import resolves correctly — this is the user-facing agent creation wizard
- [ ] Ensure the `useEntityActions` hook works for regular users (not just admins)
- [ ] Run the page locally to confirm it renders without errors

**Verify:**
```bash
cd apps/web && pnpm typecheck
```

**Commit:** `feat(web): verify agents list page for user context (US-33)`

---

## Task 3: Verify agent detail page server component

**Files:**
- Modify: `apps/web/src/app/(authenticated)/agents/[id]/page.tsx`

**Steps:**

The agent detail page is a server component that uses `api.agents.getById()` and renders `AgentCharacterSheet`. It needs verification that:

- [ ] The `api.agents.getById(params.id)` call works for non-admin users (check the backend endpoint permissions)
- [ ] The `AgentCharacterSheet` component renders correctly with the `AgentDetailData` shape
- [ ] The page includes proper metadata generation for SEO
- [ ] The `DeckTrackerSync` properly tracks the agent in the card hand
- [ ] Verify the `AgentCharacterSheet` sections: Portrait (stats, game link), Equipaggiamento (KB docs), Area Azione (chat), Storia (threads)
- [ ] Ensure the chat creation flow works: readiness check -> create thread -> ChatThreadView embed

**Note:** The `AgentCharacterSheet` has a "Configura" button that links to `/library/games/{gameId}/agent` — verify this route exists or update the link.

**Verify:**
```bash
cd apps/web && pnpm typecheck
```

**Commit:** `feat(web): verify agent detail page for user access (US-33)`

---

## Task 4: Update agent icon in navigation

**Files:**
- Modify: `apps/web/src/config/navigation.ts`

**Steps:**

The agents nav item currently uses the generic `Users` icon. Replace it with `Bot` from lucide-react to better represent AI agents.

- [ ] Add `Bot` to the lucide-react import in `apps/web/src/config/navigation.ts`
- [ ] Change the `agents` nav item `icon` from `Users` to `Bot`
- [ ] Change `iconName` from `'users'` to `'bot'`
- [ ] Remove the unused `Users` import if no other nav item uses it

```typescript
{
  id: 'agents',
  href: '/agents',
  icon: Bot,
  iconName: 'bot',
  label: 'Agenti',
  ariaLabel: 'Navigate to AI agents catalog',
  priority: 7,
  testId: 'nav-agents',
  activePattern: /^\/agents/,
  visibility: { authOnly: true },
  group: 'strumenti',
},
```

**Verify:**
```bash
cd apps/web && pnpm typecheck
```

**Commit:** `feat(web): use Bot icon for agents navigation item (US-33)`

---

## Task 5: Add component tests for agents page

**Files:**
- Modify: `apps/web/src/app/(authenticated)/agents/__tests__/agents-page.test.tsx`

**Steps:**

The existing test file already has a structure with mocks for `api.agents.getAll`, `api.agents.getSlots`, and `api.agents.createWithSetup`. Extend it to cover the user-facing scenarios.

- [ ] Add test: renders agent cards when API returns agents
- [ ] Add test: shows empty state when no agents exist
- [ ] Add test: search filters agents by name
- [ ] Add test: type filter filters agents by type
- [ ] Add test: clicking an agent card navigates to detail page (desktop)
- [ ] Add test: slot indicator shows used/total slots
- [ ] Ensure all mocks use the `createTestQueryClient` helper

**Verify:**
```bash
cd apps/web && pnpm vitest run src/app/\(authenticated\)/agents/__tests__/agents-page.test.tsx
```

**Commit:** `test(web): extend agents page tests for user scenarios (US-33)`

---

## Task 6: Add component tests for AgentCharacterSheet

**Files:**
- Create: `apps/web/src/components/agent/__tests__/AgentCharacterSheet.test.tsx`

**Steps:**

- [ ] Create test file with standard mocks for `next/navigation`, `@/lib/api`, `@/hooks/queries/useAgentData`, `@/hooks/useAgentStatus`
- [ ] Add test: renders agent name and type badge
- [ ] Add test: renders stats grid (invocations, chat count, docs count, last used)
- [ ] Add test: renders linked game link when gameId is present
- [ ] Add test: shows KB empty state when no documents
- [ ] Add test: shows chat readiness states (loading, not ready, ready)
- [ ] Add test: renders thread history list

**Mock data shape:**
```typescript
const mockAgentData: AgentDetailData = {
  id: 'agent-1',
  name: 'Tutor Catan',
  type: 'Tutor',
  strategyName: 'rules-qa',
  strategyParameters: {},
  isActive: true,
  isIdle: false,
  invocationCount: 42,
  lastInvokedAt: '2026-03-25T10:00:00Z',
  createdAt: '2026-03-01T00:00:00Z',
  gameId: 'game-1',
  gameName: 'Catan',
};
```

**Verify:**
```bash
cd apps/web && pnpm vitest run src/components/agent/__tests__/AgentCharacterSheet.test.tsx
```

**Commit:** `test(web): add AgentCharacterSheet component tests (US-33)`

---

## Task 7: Verify navigation integration end-to-end

**Files:**
- Review: `apps/web/src/components/layout/` (sidebar components that consume `UNIFIED_NAV_ITEMS`)

**Steps:**

- [ ] Verify that the sidebar/HybridSidebar renders the "Agenti" item under the "strumenti" group
- [ ] Verify the nav item is visible when `isAlphaMode=true` (since we added `'agents'` to `ALPHA_NAV_IDS`)
- [ ] Verify the nav item is visible when `isAlphaMode=false` (it was always in `_ALL_NAV_ITEMS`)
- [ ] Check that `isUnifiedNavItemActive` correctly highlights the nav item when on `/agents` or `/agents/[id]`
- [ ] Run the full frontend type check and lint

**Verify:**
```bash
cd apps/web && pnpm typecheck && pnpm lint
```

**Commit:** `feat(web): verify agents navigation integration (US-33)`

---

## Task 8: Create PR and close issue

**Steps:**

- [ ] Run full test suite: `cd apps/web && pnpm test`
- [ ] Run type check: `cd apps/web && pnpm typecheck`
- [ ] Run lint: `cd apps/web && pnpm lint`
- [ ] Push branch: `git push -u origin feature/us-33-agent-browser`
- [ ] Create PR to `frontend-dev` with title: `feat(web): activate agent browser for users (US-33)`
- [ ] PR body should include:
  - Summary of changes (alpha ungating, nav activation, test coverage)
  - Screenshots if available
  - Test plan checklist
- [ ] Request code review
- [ ] After approval, merge PR
- [ ] Delete local branch: `git branch -D feature/us-33-agent-browser`
- [ ] Update issue status on GitHub

---

## Summary

| Task | Type | Files | Est. Time |
|------|------|-------|-----------|
| 1. Alpha route + nav allowlist | Config | 2 modify | 2 min |
| 2. Agents list page review | Review/Fix | 1 modify | 3 min |
| 3. Agent detail page verify | Review/Fix | 1 modify | 3 min |
| 4. Bot icon for nav | Enhancement | 1 modify | 2 min |
| 5. Agents page tests | Testing | 1 modify | 5 min |
| 6. CharacterSheet tests | Testing | 1 create | 5 min |
| 7. Navigation integration | Verification | 0 (review) | 3 min |
| 8. PR and close | Process | 0 | 3 min |

**Total estimated time:** ~26 minutes

## Key Files Reference

| File | Purpose |
|------|---------|
| `apps/web/src/proxy.ts` | Alpha route guard (ALPHA_ROUTE_PREFIXES) |
| `apps/web/src/config/navigation.ts` | Navigation items + ALPHA_NAV_IDS |
| `apps/web/src/app/(authenticated)/agents/page.tsx` | Agent list page (client component) |
| `apps/web/src/app/(authenticated)/agents/[id]/page.tsx` | Agent detail page (server component) |
| `apps/web/src/components/agent/AgentCharacterSheet.tsx` | RPG-style agent detail layout |
| `apps/web/src/hooks/queries/useAgents.ts` | React Query hook for agent list |
| `apps/web/src/hooks/queries/useAgentSlots.ts` | React Query hook for agent slots |
| `apps/web/src/lib/api/clients/agentsClient.ts` | API client for agents CRUD |
| `apps/web/src/config/entity-navigation.ts` | Entity cross-navigation graph |

## Existing Infrastructure (No Changes Needed)

- **API Client**: `createAgentsClient` already has `getAll`, `getById`, `getSlots`, `create`, `configure`, `invoke`
- **React Query Hooks**: `useAgents`, `useAgentSlots`, `useHasAvailableSlots` already exist
- **MeepleCard entity="agent"**: Already supported with blue color scheme (HSL 220 70% 55%)
- **Entity Navigation**: Agent navigation graph already defined in `entity-navigation.ts`
- **Agent Components**: `AgentCharacterSheet`, `AgentCreationSheet`, `AgentConfigSheet`, `AgentSelector` all exist
