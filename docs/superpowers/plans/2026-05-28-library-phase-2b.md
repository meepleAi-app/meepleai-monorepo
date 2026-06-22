# Library Phase 2b #1592 — kb + agents Tab Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the kb and agents tabs in `LibraryHub` to real data — replace the `[]` stubs in `useHybridHubItems` with hooks consuming `GET /api/v1/kb-docs` (BE-1 #1588) and `GET /agents?scope=my-library` (BE-2 #1589). FE-only, no BE changes. ~half a day.

**Architecture:** Greenfield Zod schema + client method + hook for the kb listing; extend the existing `agentsClient.getAll` + `useAgents` with a `scope` option (no new hook). An adapter in `useUserKbDocs` derives `updatedAt = processedAt ?? uploadedAt` so the existing `KbDoc` mapper signature stays unchanged (AC2.b.4). The hub orchestrator (`useHybridHubItems`) drops `void agentsQuery`, propagates real errors and loading state, and updates `totalCounts` so the hero stat chips reflect reality.

**Tech Stack:** Next.js 16 · React 19 · TanStack Query · Zod · Vitest · React Testing Library · TypeScript strict

**Spec source:** Issue #1592 body (refined 2026-05-28 via /sc:spec-panel discussion + socratic K1; 9 locked decisions K1-K5, K1.1, K1.3, K1.4, A1).

**⚠️ Branch policy:** parent = `main-dev`. PR target = `main-dev`. Commit messages end with `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>`. Headers ≤80 chars.

---

## File Structure

| File | Responsibility | Status |
|---|---|---|
| `apps/web/src/lib/api/schemas/kb-docs.schemas.ts` | Zod schema for `UserKbDocDto` + `KbDocsListResponse` envelope | Create |
| `apps/web/src/lib/api/clients/kbDocsClient.ts` | Client `listUserKbDocs(params)` calling `GET /api/v1/kb-docs` | Create |
| `apps/web/src/lib/api/index.ts` | Add `kbDocs: KbDocsClient` to the `Api` interface + register in `createApi` | Modify |
| `apps/web/src/hooks/queries/useUserKbDocs.ts` | TanStack Query hook + adapter (`updatedAt = processedAt ?? uploadedAt`) | Create |
| `apps/web/src/lib/api/clients/agentsClient.ts:67-84` | Extend `getAll(activeOnly?, type?, scope?)` to accept and forward `scope` | Modify |
| `apps/web/src/hooks/queries/useAgents.ts` | Add optional `scope?: 'my-library'` to options + forward | Modify |
| `apps/web/src/hooks/queries/useHybridHubItems.ts:49-104` | Wire kb + agents (drop `void agentsQuery`, propagate errors + loading + counts) | Modify |
| `apps/web/src/hooks/queries/__tests__/useUserKbDocs.test.ts` | Adapter unit test (K1.4) + happy-path query | Create |
| `apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts` | Zod schema valid/invalid cases | Create |
| `apps/web/src/hooks/queries/__tests__/useHybridHubItems.test.tsx` | Update 8 tests (mock new hooks, assert non-empty sources + AC2.b.5 partial-failure) | Modify |
| `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx` | Update hero-stats fixture (agents/docs real counts) | Modify |
| `apps/web/src/lib/library/__tests__/hybrid-hub.derive.test.ts` | Extend with cross-entity recent sort scenario (AC2.b.6 / K1.4) | Modify |

---

## Verified reference facts (P74-grounded, do not re-discover)

- **BE-1 envelope** (`KbDocsListResponse`): `{ items: UserKbDocDto[], total: number, page: number, pageSize: number }`. **Field is `total`, NOT `totalCount`**.
- **BE-1 item DTO** (`UserKbDocDto`, 8 fields): `id` Guid, `gameId` Guid?, `gameName` string?, `fileName` string, `processingState` string (`Pending|Uploading|Extracting|Chunking|Embedding|Indexing|Ready|Failed`), `pageCount` int?, `processedAt` DateTime?, `uploadedAt` DateTime. **No `updatedAt` field** — derived FE-side.
- **BE-1 query params**: `page` ≥1 default 1, `pageSize` 1-100 default 20, `sortBy` `recent` only default, `state` `ready|all` default `ready`. Server orders by `ProcessedAt ?? UploadedAt` DESC.
- **BE-2 endpoint**: `GET /api/v1/agents?scope=my-library` — extends existing `/agents` route, returns `{ success, agents: AgentDto[], count }`. `AgentDtoSchema` (`apps/web/src/lib/api/schemas/agents.schemas.ts:14-30`) ALREADY matches the BE DTO precisely — no schema work needed.
- **`agentsClient.getAll`** (`apps/web/src/lib/api/clients/agentsClient.ts:67-84`) — current signature `(activeOnly?, type?)`. Builds URL with `URLSearchParams`. To add `scope`, append a third param + `params.append('scope', scope)`.
- **`useAgents`** (`apps/web/src/hooks/queries/useAgents.ts:10-16`) — current options `{ activeOnly?, type? }`. Forward a `scope?` option.
- **`useHybridHubItems` stubs** (verified): lines 49 (`useAgents({})`), 62 (`agents: []`), 63 (`kb: []`), 70 (`agents: null`), 71 (`kb: null`), 78 (`agents: 0`), 79 (`kb: 0`), 84 (`isLoading` excludes agents/kb), 86 (`void agentsQuery;`).
- **`KbDoc` interface** (`apps/web/src/lib/library/hybrid-hub.mappers.ts:41-50`) — the mapper `kbDocToHubItem` (lines 82-94) consumes only: `id, fileName, gameName, updatedAt, processingState, pageCount`. The Zod schema only needs to cover these + the adapter derives `updatedAt`.
- **`httpClient.get<T>(url, schema)` pattern** (from `knowledgeBaseClient.ts`): pass URL + Zod schema for validation. Schema validates the FULL response envelope.
- **API registry** (`apps/web/src/lib/api/index.ts`): the `Api` interface declares each client (e.g. line 235: `agents: AgentsClient;`); `createApi` wires it (line 423: `agents: createAgentsClient({ httpClient })`). Add `kbDocs` the same way.
- **`hybrid-hub.derive.ts:81`** — `recent` sort: `Date.parse(b.updatedAt) - Date.parse(a.updatedAt)`. Pure function; tests can call it directly with crafted items.

---

## ⚠️ Brownfield risks

- **R1**: `useAgents` is consumed elsewhere (currently called with `{}`). Adding an optional `scope?` param is non-breaking. Verify by grep: `grep -rn "useAgents(" apps/web/src` before commit (Task 4 step 2).
- **R2**: `useHybridHubItems.test.tsx` has 8 existing tests asserting `agents: []`/`kb: []`. They WILL FAIL after Task 5 wires real data — they must be updated together (atomic commit) or the test suite breaks between tasks. Task 6 handles this.
- **R3**: The `agentsClient.getAll` URL building uses `URLSearchParams` — passing `scope: 'my-library'` builds `?scope=my-library` correctly. Just append the third param.
- **R4**: `AgentDto.gameId` and `gameName` are already `nullable().optional()` in the schema — agents with `gameId=null` (system agents) won't fail validation. No schema change.

---

## Task 1: Zod schema for `UserKbDocDto` + envelope

**Goal**: Define the FE Zod schema mirroring BE-1's `UserKbDocDto` + envelope, with valid/invalid unit tests.

**Files:**
- Create: `apps/web/src/lib/api/schemas/kb-docs.schemas.ts`
- Create: `apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts`

- [ ] **Step 1: Write the failing schema test**

Create `apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import {
  KbDocsListResponseSchema,
  ProcessingStateSchema,
  UserKbDocDtoSchema,
  type UserKbDocDto,
} from '../kb-docs.schemas';

describe('kb-docs schemas', () => {
  const validDoc: UserKbDocDto = {
    id: '11111111-1111-1111-1111-111111111111',
    gameId: '22222222-2222-2222-2222-222222222222',
    gameName: 'Catan',
    fileName: 'catan-rules.pdf',
    processingState: 'Ready',
    pageCount: 24,
    processedAt: '2026-05-28T11:00:00+00:00',
    uploadedAt: '2026-05-28T09:00:00+00:00',
  };

  it('accepts a full Ready doc', () => {
    expect(() => UserKbDocDtoSchema.parse(validDoc)).not.toThrow();
  });

  it('accepts gameId/gameName null (orphan doc)', () => {
    const parsed = UserKbDocDtoSchema.parse({ ...validDoc, gameId: null, gameName: null });
    expect(parsed.gameId).toBeNull();
    expect(parsed.gameName).toBeNull();
  });

  it('accepts processedAt null + pageCount null (Pending doc)', () => {
    const parsed = UserKbDocDtoSchema.parse({
      ...validDoc,
      processingState: 'Pending',
      processedAt: null,
      pageCount: null,
    });
    expect(parsed.processedAt).toBeNull();
    expect(parsed.pageCount).toBeNull();
  });

  it.each(['Pending', 'Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing', 'Ready', 'Failed'])(
    'accepts processingState "%s"',
    (state) => {
      expect(() => ProcessingStateSchema.parse(state)).not.toThrow();
    }
  );

  it('rejects unknown processingState', () => {
    expect(() => UserKbDocDtoSchema.parse({ ...validDoc, processingState: 'Unknown' })).toThrow();
  });

  it('rejects malformed uploadedAt (not ISO-8601)', () => {
    expect(() => UserKbDocDtoSchema.parse({ ...validDoc, uploadedAt: 'yesterday' })).toThrow();
  });

  it('parses the list envelope { items, total, page, pageSize }', () => {
    const envelope = { items: [validDoc], total: 1, page: 1, pageSize: 20 };
    const parsed = KbDocsListResponseSchema.parse(envelope);
    expect(parsed.total).toBe(1);
    expect(parsed.items).toHaveLength(1);
  });

  it('rejects envelope that uses totalCount instead of total (BE-1 contract)', () => {
    const envelope = { items: [], totalCount: 0, page: 1, pageSize: 20 };
    expect(() => KbDocsListResponseSchema.parse(envelope)).toThrow();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts 2>&1 | tail -10
```
Expected: FAIL — module `'../kb-docs.schemas'` does not exist.

- [ ] **Step 3: Create the schema**

Create `apps/web/src/lib/api/schemas/kb-docs.schemas.ts`:

```typescript
/**
 * KB Docs Schemas (Issue #1592 Phase 2b)
 *
 * Zod schemas for the cross-game per-user KB documents listing endpoint
 * (BE-1 #1588): GET /api/v1/kb-docs?page=&pageSize=&sortBy=recent&state=ready|all.
 *
 * Matches `KbDocsListResponse` + `UserKbDocDto` from
 * apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/ListUserKbDocsQuery.cs.
 * The envelope field is `total` (NOT `totalCount`) — verified against the BE record.
 */

import { z } from 'zod';

/**
 * The 8 raw enum values of `PdfProcessingState` projected as strings by BE-1.
 * @see apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Enums/PdfProcessingState.cs
 */
export const ProcessingStateSchema = z.enum([
  'Pending',
  'Uploading',
  'Extracting',
  'Chunking',
  'Embedding',
  'Indexing',
  'Ready',
  'Failed',
]);

export type ProcessingState = z.infer<typeof ProcessingStateSchema>;

/**
 * UserKbDocDto — lightweight cross-game user-scoped projection (BE-1).
 * Does NOT include `filePath`, `fileSizeBytes`, `documentType`, etc. — see issue #1592.
 */
export const UserKbDocDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid().nullable(),
  gameName: z.string().nullable(),
  fileName: z.string().min(1),
  processingState: ProcessingStateSchema,
  pageCount: z.number().int().positive().nullable(),
  processedAt: z.string().datetime({ offset: true }).nullable(),
  uploadedAt: z.string().datetime({ offset: true }),
});

export type UserKbDocDto = z.infer<typeof UserKbDocDtoSchema>;

/**
 * KbDocsListResponse envelope — note `total`, not `totalCount`.
 */
export const KbDocsListResponseSchema = z.object({
  items: z.array(UserKbDocDtoSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
}).strict();

export type KbDocsListResponse = z.infer<typeof KbDocsListResponseSchema>;
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts 2>&1 | tail -10
```
Expected: PASS (7 tests + 8 from `it.each` = 15 total).

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/lib/api/schemas/kb-docs.schemas.ts apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts
git -c commit.gpgsign=false commit -m "feat(kb-docs): #1592 add UserKbDocDto Zod schema (Phase 2b)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: `kbDocsClient.listUserKbDocs` + api registry wiring

**Goal**: Create the client method that calls `GET /api/v1/kb-docs` with Zod validation, and register it in the api registry.

**Files:**
- Create: `apps/web/src/lib/api/clients/kbDocsClient.ts`
- Modify: `apps/web/src/lib/api/index.ts` (add `kbDocs: KbDocsClient` interface field + `createKbDocsClient` in `createApi`)

- [ ] **Step 1: Create the client**

Create `apps/web/src/lib/api/clients/kbDocsClient.ts`:

```typescript
/**
 * KB Docs Client (Issue #1592 Phase 2b)
 *
 * Modular client for the cross-game per-user KB documents listing endpoint
 * (BE-1 #1588). Currently exposes one method: `listUserKbDocs`. Future readers/
 * detail endpoints stay in `knowledgeBaseClient.ts` (separation of concerns —
 * this client is scoped to the new cross-game user listing endpoint).
 */

import {
  KbDocsListResponseSchema,
  type KbDocsListResponse,
} from '../schemas/kb-docs.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateKbDocsClientParams {
  httpClient: HttpClient;
}

export interface ListUserKbDocsParams {
  page?: number;
  pageSize?: number;
  /** Server orders by `ProcessedAt ?? UploadedAt` DESC. Only `recent` is supported in v1. */
  sortBy?: 'recent';
  /** `ready` (default) filters to ProcessingState=Ready; `all` returns every state. */
  state?: 'ready' | 'all';
}

export interface KbDocsClient {
  listUserKbDocs(params?: ListUserKbDocsParams): Promise<KbDocsListResponse>;
}

export function createKbDocsClient({ httpClient }: CreateKbDocsClientParams): KbDocsClient {
  return {
    async listUserKbDocs(params: ListUserKbDocsParams = {}): Promise<KbDocsListResponse> {
      const qs = new URLSearchParams();
      if (params.page !== undefined) qs.append('page', String(params.page));
      if (params.pageSize !== undefined) qs.append('pageSize', String(params.pageSize));
      if (params.sortBy !== undefined) qs.append('sortBy', params.sortBy);
      if (params.state !== undefined) qs.append('state', params.state);

      const url = `/api/v1/kb-docs${qs.toString() ? `?${qs.toString()}` : ''}`;
      const response = await httpClient.get<KbDocsListResponse>(url, KbDocsListResponseSchema);
      return response;
    },
  };
}
```

- [ ] **Step 2: Register in the api root**

In `apps/web/src/lib/api/index.ts`:

1. Add the import near the other client imports:
```typescript
import { createKbDocsClient, type KbDocsClient } from './clients/kbDocsClient';
```

2. Add the field to the `Api` interface (alphabetical with neighbors — e.g. after the `agents: AgentsClient;` declaration around line 235):
```typescript
/** Cross-game per-user KB documents listing (Issue #1592 Phase 2b) */
kbDocs: KbDocsClient;
```

3. Register it in the `createApi` factory (next to the other `createXxxClient({ httpClient })` calls, near line 423):
```typescript
kbDocs: createKbDocsClient({ httpClient }),
```

(Use Read on `apps/web/src/lib/api/index.ts` to find the exact neighbor lines before editing.)

- [ ] **Step 3: Build (typecheck) the FE**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -10
```
Expected: 0 errors. The `api.kbDocs.listUserKbDocs(...)` call site is now type-safe.

- [ ] **Step 4: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/lib/api/clients/kbDocsClient.ts apps/web/src/lib/api/index.ts
git -c commit.gpgsign=false commit -m "feat(kb-docs): #1592 add kbDocsClient.listUserKbDocs (Phase 2b)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: `useUserKbDocs` hook + adapter (`updatedAt = processedAt ?? uploadedAt`)

**Goal**: TanStack Query hook calling `api.kbDocs.listUserKbDocs` + the K1.1 adapter that derives `updatedAt`. The adapter is the bridge between BE field names and the `KbDoc` FE interface — keeping `kbDocToHubItem` untouched (AC2.b.4).

**Files:**
- Create: `apps/web/src/hooks/queries/useUserKbDocs.ts`
- Create: `apps/web/src/hooks/queries/__tests__/useUserKbDocs.test.ts`

- [ ] **Step 1: Write the failing test (adapter unit + happy-path)**

Create `apps/web/src/hooks/queries/__tests__/useUserKbDocs.test.ts`:

```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { toKbDoc, useUserKbDocs } from '../useUserKbDocs';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';

vi.mock('@/lib/api', () => ({
  api: {
    kbDocs: {
      listUserKbDocs: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

const dtoBase: UserKbDocDto = {
  id: '11111111-1111-1111-1111-111111111111',
  gameId: '22222222-2222-2222-2222-222222222222',
  gameName: 'Catan',
  fileName: 'catan-rules.pdf',
  processingState: 'Ready',
  pageCount: 24,
  processedAt: '2026-05-28T11:00:00+00:00',
  uploadedAt: '2026-05-28T09:00:00+00:00',
};

describe('toKbDoc (adapter)', () => {
  it('uses processedAt as updatedAt when present (K1.1 last-activity)', () => {
    const result = toKbDoc(dtoBase);
    expect(result.updatedAt).toBe('2026-05-28T11:00:00+00:00');
  });

  it('falls back to uploadedAt when processedAt is null (K1.1)', () => {
    const result = toKbDoc({ ...dtoBase, processedAt: null });
    expect(result.updatedAt).toBe('2026-05-28T09:00:00+00:00');
  });

  it('preserves the other fields unchanged', () => {
    const result = toKbDoc(dtoBase);
    expect(result).toMatchObject({
      id: dtoBase.id,
      gameId: dtoBase.gameId,
      gameName: dtoBase.gameName,
      fileName: dtoBase.fileName,
      processingState: dtoBase.processingState,
      pageCount: dtoBase.pageCount,
      processedAt: dtoBase.processedAt,
    });
  });
});

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useUserKbDocs', () => {
  beforeEach(() => {
    vi.mocked(api.kbDocs.listUserKbDocs).mockReset();
  });

  it('fetches with default params { page:1, pageSize:20, sortBy:recent, state:ready }', async () => {
    vi.mocked(api.kbDocs.listUserKbDocs).mockResolvedValue({
      items: [dtoBase],
      total: 1,
      page: 1,
      pageSize: 20,
    });

    const { result } = renderHook(() => useUserKbDocs(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.kbDocs.listUserKbDocs).toHaveBeenCalledWith({
      page: 1,
      pageSize: 20,
      sortBy: 'recent',
      state: 'ready',
    });
    expect(result.current.data?.items).toHaveLength(1);
    expect(result.current.data?.items[0].updatedAt).toBe('2026-05-28T11:00:00+00:00');
  });

  it('exposes total count from the envelope', async () => {
    vi.mocked(api.kbDocs.listUserKbDocs).mockResolvedValue({
      items: [],
      total: 42,
      page: 1,
      pageSize: 20,
    });

    const { result } = renderHook(() => useUserKbDocs(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.total).toBe(42);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/hooks/queries/__tests__/useUserKbDocs.test.ts 2>&1 | tail -10
```
Expected: FAIL — module `'../useUserKbDocs'` does not exist.

- [ ] **Step 3: Implement the hook + adapter**

Create `apps/web/src/hooks/queries/useUserKbDocs.ts`:

```typescript
/**
 * useUserKbDocs — Phase 2b (#1592) hook for the cross-game per-user KB documents
 * listing (BE-1 #1588). Calls `GET /api/v1/kb-docs` with default
 * `{ page:1, pageSize:20, sortBy:'recent', state:'ready' }` (K3 #1592).
 *
 * Adapter (K1.1): the BE DTO has `processedAt?` + `uploadedAt` but NOT a single
 * `updatedAt`. Server-side `sortBy=recent` orders by `ProcessedAt ?? UploadedAt`;
 * we mirror that on the FE so the `kbDocToHubItem` mapper signature stays
 * unchanged (AC2.b.4). Follow-up #1645 tracks exposing `updatedAt` explicit BE-side.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  KbDocsListResponse,
  UserKbDocDto,
} from '@/lib/api/schemas/kb-docs.schemas';
import type { KbDoc } from '@/lib/library/hybrid-hub.mappers';

/** Adapter: DTO → FE `KbDoc` (derives `updatedAt`). Pure, unit-testable. */
export function toKbDoc(dto: UserKbDocDto): KbDoc {
  return {
    id: dto.id,
    gameId: dto.gameId,
    gameName: dto.gameName,
    fileName: dto.fileName,
    processingState: dto.processingState,
    pageCount: dto.pageCount,
    processedAt: dto.processedAt,
    // K1.1: server-side sortBy=recent uses ProcessedAt ?? UploadedAt — mirror it.
    updatedAt: dto.processedAt ?? dto.uploadedAt,
  };
}

export interface UseUserKbDocsResult {
  /** Adapted items (DTO → KbDoc) — what the mapper consumes. */
  items: KbDoc[];
  total: number;
  page: number;
  pageSize: number;
}

export function useUserKbDocs(): UseQueryResult<UseUserKbDocsResult> {
  return useQuery({
    queryKey: ['kb-docs', 'user', { page: 1, pageSize: 20, sortBy: 'recent', state: 'ready' }],
    queryFn: async () => {
      const response: KbDocsListResponse = await api.kbDocs.listUserKbDocs({
        page: 1,
        pageSize: 20,
        sortBy: 'recent',
        state: 'ready',
      });
      return {
        items: response.items.map(toKbDoc),
        total: response.total,
        page: response.page,
        pageSize: response.pageSize,
      };
    },
    staleTime: 5 * 60 * 1000, // 5 min, aligns with useAgents
  });
}
```

- [ ] **Step 4: Run to verify GREEN**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/hooks/queries/__tests__/useUserKbDocs.test.ts 2>&1 | tail -10
```
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/hooks/queries/useUserKbDocs.ts apps/web/src/hooks/queries/__tests__/useUserKbDocs.test.ts
git -c commit.gpgsign=false commit -m "feat(kb-docs): #1592 useUserKbDocs hook + adapter (Phase 2b K1.1)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: Extend `agentsClient.getAll` + `useAgents` with `scope`

**Goal**: Add a `scope?: 'my-library'` optional param to the existing agents client + hook (A1 — extend, NOT a new hook).

**Files:**
- Modify: `apps/web/src/lib/api/clients/agentsClient.ts:67-84`
- Modify: `apps/web/src/hooks/queries/useAgents.ts:10-16`

- [ ] **Step 1: Verify no breaking call-sites (R1 brownfield check)**

```bash
grep -rn "useAgents(\|api\.agents\.getAll(" apps/web/src 2>&1 | head -20
```
Expected: list of call-sites. Confirm none pass positional `scope` (only `{}` or `{activeOnly,type}`).

- [ ] **Step 2: Update `agentsClient.getAll`**

In `apps/web/src/lib/api/clients/agentsClient.ts` (lines 67-84), replace the `getAll` method with:

```typescript
    /**
     * Get all agents with optional filtering
     * Implements GetAllAgentsQuery from backend
     * @param activeOnly If true, only return active agents
     * @param type Optional agent type filter
     * @param scope Optional scope: `'my-library'` returns agents whose game is in
     *   the caller's library + system agents (BE-2 #1589). Omit for global list.
     * Resolved by Wave B.2 hotfix #641 (2026-05-03) — route registered in AgentsEndpoints.cs.
     */
    async getAll(
      activeOnly?: boolean,
      type?: string,
      scope?: 'my-library',
    ): Promise<AgentDto[]> {
      const params = new URLSearchParams();
      if (activeOnly !== undefined) {
        params.append('activeOnly', activeOnly.toString());
      }
      if (type) {
        params.append('type', type);
      }
      if (scope) {
        params.append('scope', scope);
      }

      const url = `/api/v1/agents${params.toString() ? `?${params.toString()}` : ''}`;
      const response = await httpClient.get<{
        success: boolean;
        agents: AgentDto[];
        count: number;
      }>(url, GetAllAgentsResponseSchema);

      return response?.agents ?? [];
    },
```

- [ ] **Step 3: Update `useAgents`**

Replace the entire content of `apps/web/src/hooks/queries/useAgents.ts` with:

```typescript
/**
 * useAgents - React Query hook for agent catalog
 * Issue #4126: API Integration
 * Issue #1592 (Phase 2b): `scope?: 'my-library'` option forwards to BE-2 #1589.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

export interface UseAgentsOptions {
  activeOnly?: boolean;
  type?: string;
  /** BE-2 #1589: `'my-library'` returns library-game agents + system agents. */
  scope?: 'my-library';
}

export function useAgents(options: UseAgentsOptions = {}) {
  return useQuery({
    queryKey: ['agents', options],
    queryFn: () => api.agents.getAll(options.activeOnly, options.type, options.scope),
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}
```

- [ ] **Step 4: Typecheck + run existing agents-related tests (no regression)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -6
pnpm vitest run --testNamePattern "useAgents|agentsClient" 2>&1 | tail -10
```
Expected: typecheck 0 errors; existing tests still pass (additive change).

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/lib/api/clients/agentsClient.ts apps/web/src/hooks/queries/useAgents.ts
git -c commit.gpgsign=false commit -m "feat(agents): #1592 add scope=my-library option to useAgents (Phase 2b A1)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: Wire kb + agents in `useHybridHubItems` (drop `void`, errors, isLoading)

**Goal**: Replace lines 49-104 of `useHybridHubItems.ts`. The 8 stub touch-points become real wiring (K4 errors + isLoading + counts).

**Files:**
- Modify: `apps/web/src/hooks/queries/useHybridHubItems.ts`

- [ ] **Step 1: Replace the file with the wired version**

Replace the entire content of `apps/web/src/hooks/queries/useHybridHubItems.ts` with:

```typescript
/**
 * useHybridHubItems — Phase 2a (#1605) + Phase 2b (#1592) orchestration hook
 * for the `/library` hybrid hub.
 *
 * Calls all 5 entity sources (games / sessions / chat / agents / kb), maps each
 * DTO to a `HybridHubItem` via the Phase 1 mappers, caps each source to
 * `PER_SOURCE_CAP`, and reports per-source errors so the hub can degrade
 * gracefully (AC9.1 + AC2.b.5 partial-failure).
 *
 * Phase 2b changes: agents source is now wired via `useAgents({scope:'my-library'})`
 * (BE-2 #1589); kb source is wired via `useUserKbDocs` (BE-1 #1588). The hook
 * propagates real errors and includes both in `isLoading`.
 *
 * Returns `HybridHubSources` (the shape `deriveHybridItems` consumes) — the
 * hook is the data layer; tab/query/sort derivation stays in `LibraryHub`.
 */

import { useMemo } from 'react';

import type { HybridHubSources } from '@/lib/library/hybrid-hub.derive';
import {
  agentToHubItem,
  chatToHubItem,
  kbDocToHubItem,
  libraryEntryToHubItem,
  sessionToHubItem,
} from '@/lib/library/hybrid-hub.mappers';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

import { useActiveSessions } from './useActiveSessions';
import { useAgents } from './useAgents';
import { useRecentChatSessions } from './useChatSessions';
import { useLibrary } from './useLibrary';
import { useUserKbDocs } from './useUserKbDocs';

export const PER_SOURCE_CAP = 20;

export type HybridHubSourceKey = keyof HybridHubSources;

export interface UseHybridHubItemsResult {
  readonly sources: HybridHubSources;
  readonly isLoading: boolean;
  readonly allFailed: boolean;
  readonly partialErrors: Record<HybridHubSourceKey, Error | null>;
  readonly totalCounts: Record<HybridHubSourceKey, number>;
}

export function useHybridHubItems(): UseHybridHubItemsResult {
  const libraryQuery = useLibrary({
    page: 1,
    pageSize: 50,
    sortBy: 'addedAt',
    sortDescending: true,
  });
  const sessionsQuery = useActiveSessions(PER_SOURCE_CAP);
  const chatQuery = useRecentChatSessions(50);
  // BE-2 #1589: library-scoped (game ∈ caller's library + system agents).
  const agentsQuery = useAgents({ scope: 'my-library' });
  // BE-1 #1588: state=ready, page 1, pageSize=20, sortBy=recent (K3).
  const kbQuery = useUserKbDocs();

  return useMemo(() => {
    const gameItems = (libraryQuery.data?.items ?? []).map(libraryEntryToHubItem);
    const sessionItems = (sessionsQuery.data?.sessions ?? []).map(sessionToHubItem);
    const chatItems = (chatQuery.data?.sessions ?? []).map(chatToHubItem);
    const agentItems = (agentsQuery.data ?? []).map(agentToHubItem);
    const kbItems = (kbQuery.data?.items ?? []).map(kbDocToHubItem);

    const cap = (items: readonly HybridHubItem[]) => items.slice(0, PER_SOURCE_CAP);

    const sources: HybridHubSources = {
      games: libraryQuery.isError ? [] : cap(gameItems),
      sessions: sessionsQuery.isError ? [] : cap(sessionItems),
      chat: chatQuery.isError ? [] : cap(chatItems),
      agents: agentsQuery.isError ? [] : cap(agentItems),
      kb: kbQuery.isError ? [] : cap(kbItems),
    };

    const partialErrors: Record<HybridHubSourceKey, Error | null> = {
      games: libraryQuery.isError ? (libraryQuery.error ?? new Error('library')) : null,
      sessions: sessionsQuery.isError ? (sessionsQuery.error ?? new Error('sessions')) : null,
      chat: chatQuery.isError ? (chatQuery.error ?? new Error('chat')) : null,
      agents: agentsQuery.isError ? (agentsQuery.error ?? new Error('agents')) : null,
      kb: kbQuery.isError ? (kbQuery.error ?? new Error('kb')) : null,
    };

    const totalCounts: Record<HybridHubSourceKey, number> = {
      games: gameItems.length,
      sessions: sessionItems.length,
      chat: chatItems.length,
      agents: agentItems.length,
      kb: kbItems.length,
    };

    const allErrors = [
      libraryQuery.isError,
      sessionsQuery.isError,
      chatQuery.isError,
      agentsQuery.isError,
      kbQuery.isError,
    ];
    const allFailed = allErrors.every(Boolean);
    const isLoading =
      libraryQuery.isLoading ||
      sessionsQuery.isLoading ||
      chatQuery.isLoading ||
      agentsQuery.isLoading ||
      kbQuery.isLoading;

    return { sources, isLoading, allFailed, partialErrors, totalCounts };
  }, [
    libraryQuery.data,
    libraryQuery.isError,
    libraryQuery.error,
    libraryQuery.isLoading,
    sessionsQuery.data,
    sessionsQuery.isError,
    sessionsQuery.error,
    sessionsQuery.isLoading,
    chatQuery.data,
    chatQuery.isError,
    chatQuery.error,
    chatQuery.isLoading,
    agentsQuery.data,
    agentsQuery.isError,
    agentsQuery.error,
    agentsQuery.isLoading,
    kbQuery.data,
    kbQuery.isError,
    kbQuery.error,
    kbQuery.isLoading,
  ]);
}
```

Key changes from the prior version:
- Removed `void agentsQuery;` line.
- Added `useUserKbDocs()` + `kbQuery` integration.
- `useAgents({ scope: 'my-library' })` replaces `useAgents({})`.
- `sources.agents` and `sources.kb` now map real items + cap.
- `partialErrors.agents`/`.kb` propagate real errors.
- `totalCounts.agents`/`.kb` reflect real lengths.
- `isLoading` includes agents + kb.
- `allFailed` now requires ALL 5 sources to fail (was 3).
- `useMemo` deps include agentsQuery and kbQuery fields (no `agentsQuery` opaque dep).

- [ ] **Step 2: Typecheck**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck 2>&1 | tail -6
```
Expected: 0 errors. (The existing tests will fail until Task 6 updates them — that's expected.)

- [ ] **Step 3: Commit (atomic with Task 6 if you choose to combine — but a clean separate commit is fine since the hook's typecheck is green)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/hooks/queries/useHybridHubItems.ts
git -c commit.gpgsign=false commit -m "feat(library): #1592 wire kb + agents in useHybridHubItems (Phase 2b)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: Update `useHybridHubItems.test.tsx` (8 tests + partial-failure scenario)

**Goal**: Tests previously asserted `agents: []`/`kb: []`/`agents: 0`/`kb: 0`. Now they must mock the new hooks + assert non-empty sources + the AC2.b.5 partial-failure scenario.

**Files:**
- Modify: `apps/web/src/hooks/queries/__tests__/useHybridHubItems.test.tsx`

- [ ] **Step 1: Read the existing test file fully**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && cat apps/web/src/hooks/queries/__tests__/useHybridHubItems.test.tsx | head -140
```

Identify: the mock setup for `useAgents` (currently mocks empty/discarded), the 3 mocked source hooks, and the 8 test cases.

- [ ] **Step 2: Run tests to confirm they FAIL post-Task 5**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/hooks/queries/__tests__/useHybridHubItems.test.tsx 2>&1 | tail -20
```
Expected: some tests fail because they assert `sources.agents === []` and `sources.kb === []`, but Task 5's wiring now maps real items from the mocked hooks. Also some assert `totalCounts.kb === 0` / `totalCounts.agents === 0`.

- [ ] **Step 3: Update the test file**

Update the mock setup to include `useUserKbDocs` (new) and pass real items from `useAgents`. Add the AC2.b.5 partial-failure test. The exact diff depends on the file's structure (read first) — the general changes:

1. **Add mock for `useUserKbDocs`** at the top:
   ```typescript
   vi.mock('../useUserKbDocs', () => ({
     useUserKbDocs: vi.fn(),
   }));
   import { useUserKbDocs } from '../useUserKbDocs';
   ```

2. **In `beforeEach`, set the default `useUserKbDocs` mock**:
   ```typescript
   vi.mocked(useUserKbDocs).mockReturnValue({
     data: { items: [], total: 0, page: 1, pageSize: 20 },
     isError: false,
     isLoading: false,
     error: null,
   } as any);
   ```

3. **Update existing tests that assert `agents: []`** — provide real agent items via the `useAgents` mock and assert non-empty `sources.agents`. Example pattern:
   ```typescript
   vi.mocked(useAgents).mockReturnValue({
     data: [{ id: 'a1', name: 'A1', type: 'Tutor', /* ... full AgentDto */ }],
     isError: false,
     isLoading: false,
     error: null,
   } as any);
   // ...
   expect(result.current.sources.agents).toHaveLength(1);
   expect(result.current.totalCounts.agents).toBe(1);
   ```

4. **Similarly update tests that assert `kb: []`** — provide items via `useUserKbDocs` mock.

5. **Add the AC2.b.5 partial-failure test**:
   ```typescript
   it('AC2.b.5: kb endpoint fails, agents OK — graceful degradation', () => {
     vi.mocked(useUserKbDocs).mockReturnValue({
       data: undefined,
       isError: true,
       isLoading: false,
       error: new Error('500 server error'),
     } as any);
     vi.mocked(useAgents).mockReturnValue({
       data: [{ id: 'a1', name: 'A1', type: 'Tutor', /* full AgentDto */ }],
       isError: false,
       isLoading: false,
       error: null,
     } as any);

     const { result } = renderHook(() => useHybridHubItems(), { wrapper });

     expect(result.current.sources.agents).toHaveLength(1);
     expect(result.current.sources.kb).toEqual([]);
     expect(result.current.partialErrors.kb).toBeInstanceOf(Error);
     expect(result.current.partialErrors.agents).toBeNull();
     expect(result.current.allFailed).toBe(false);
   });
   ```

The `AgentDto` shape required by the mock has all 14 fields — use a minimal helper:
```typescript
const minimalAgent = (id: string): AgentDto => ({
  id,
  name: `Agent ${id}`,
  type: 'Tutor',
  strategyName: 'HybridSearch',
  strategyParameters: {},
  isActive: true,
  createdAt: '2026-05-28T10:00:00+00:00',
  lastInvokedAt: null,
  invocationCount: 0,
  isRecentlyUsed: false,
  isIdle: true,
});
```

(Read the existing `useHybridHubItems.test.tsx` test fixtures — if there's already an agent factory, reuse it; otherwise add this helper.)

- [ ] **Step 4: Run to verify GREEN**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/hooks/queries/__tests__/useHybridHubItems.test.tsx 2>&1 | tail -15
```
Expected: ALL existing 8 tests + the new partial-failure test PASS.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/hooks/queries/__tests__/useHybridHubItems.test.tsx
git -c commit.gpgsign=false commit -m "test(library): #1592 update useHybridHubItems tests for kb+agents (Phase 2b)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 7: Update `LibraryHub.test.tsx` hero-stats fixture

**Goal**: Hero stats test at line 385 of `LibraryHub.test.tsx` currently asserts `stats[1]` (agents) = `'0'` and `stats[2]` (docs) = `'0'`. The `makeHub()` fixture hardcodes `agents: [], kb: []` / `agents: 0, kb: 0`. Update to non-zero where the test scenario warrants.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx`

- [ ] **Step 1: Identify the touch-points**

```bash
grep -n "agents:\|kb:\|totalCounts\|stats\[" apps/web/src/app/\(authenticated\)/library/_components/__tests__/LibraryHub.test.tsx | head -30
```

Expected: list of lines that fixture/assert the stub counts.

- [ ] **Step 2: Update the `makeHub` fixture default**

The `makeHub` factory probably has signature like `makeHub(overrides?: Partial<UseHybridHubItemsResult>): UseHybridHubItemsResult`. Its default should now allow non-empty agents/kb. Two options:

**Option A — keep default empty, override per-test**: leave `makeHub` returning empties; in the hero-stats test, override:
```typescript
const hub = makeHub({
  sources: { games: gameItems, sessions: [], chat: [], agents: [agentItem], kb: [kbItem] },
  totalCounts: { games: gameItems.length, sessions: 0, chat: 0, agents: 1, kb: 1 },
});
// ...
expect(stats[1].value).toBe('1'); // agents
expect(stats[2].value).toBe('1'); // docs
```

**Option B — update default to non-zero**: change `makeHub` default to provide minimal `agentItem`/`kbItem` so all hero stats are > 0 by default. Use Option A if the test file has many tests asserting `=== '0'` (low blast radius); Option B if most tests want non-zero (avoids per-test overrides).

READ the file first to decide. Apply the chosen option to the `stats[1]`/`stats[2]` assertion line(s).

- [ ] **Step 3: Run to verify GREEN**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/app/\(authenticated\)/library/_components/__tests__/LibraryHub.test.tsx 2>&1 | tail -15
```
Expected: ALL tests PASS.

- [ ] **Step 4: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add "apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx"
git -c commit.gpgsign=false commit -m "test(library): #1592 update LibraryHub hero-stats fixture (Phase 2b)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 8: Extend `hybrid-hub.derive.test.ts` with cross-entity sort scenario (AC2.b.6 / K1.4)

**Goal**: Prove a kb-doc with `processedAt=11:00` outranks a chat with `lastMessageAt=10:30` outranks a session with `completedAt=10:00` in the `recent` sort across the mixed feed.

**Files:**
- Modify: `apps/web/src/lib/library/__tests__/hybrid-hub.derive.test.ts`

- [ ] **Step 1: Append the scenario test**

In `apps/web/src/lib/library/__tests__/hybrid-hub.derive.test.ts`, add:

```typescript
import type { ChatHubItem, KbHubItem, SessionHubItem } from '../hybrid-hub.types';

describe('deriveHybridItems — cross-entity recent sort (#1592 AC2.b.6 / K1.4)', () => {
  it('orders kb-doc (processedAt=11:00) > chat (lastMessageAt=10:30) > session (completedAt=10:00)', () => {
    const kbDoc: KbHubItem = {
      id: 'kb1',
      entity: 'kb',
      title: 'rules.pdf',
      updatedAt: '2026-05-28T11:00:00+00:00',
      href: '/knowledge-base/kb1',
      processingState: 'Ready',
    };
    const chat: ChatHubItem = {
      id: 'c1',
      entity: 'chat',
      title: 'chat-1',
      updatedAt: '2026-05-28T10:30:00+00:00',
      href: '/chats/c1',
      messageCount: 5,
    };
    const session: SessionHubItem = {
      id: 's1',
      entity: 'session',
      title: 'session-1',
      updatedAt: '2026-05-28T10:00:00+00:00',
      href: '/sessions/s1',
      status: 'completed',
      playerCount: 4,
    };

    const sources = { games: [], sessions: [session], chat: [chat], agents: [], kb: [kbDoc] };
    const result = deriveHybridItems(sources, 'all', '', 'recent');

    expect(result.map((i) => i.id)).toEqual(['kb1', 'c1', 's1']);
  });
});
```

(Adapt the `SessionHubItem` field names — `status`, `playerCount` — to the real `hybrid-hub.types.ts` shape. Read it if uncertain.)

- [ ] **Step 2: Run to verify GREEN**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/lib/library/__tests__/hybrid-hub.derive.test.ts 2>&1 | tail -10
```
Expected: existing tests + the new cross-entity sort test PASS.

- [ ] **Step 3: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/lib/library/__tests__/hybrid-hub.derive.test.ts
git -c commit.gpgsign=false commit -m "test(library): #1592 cross-entity recent sort kb>chat>session (Phase 2b K1.4)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Final acceptance check

After all 8 tasks complete, run:

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck && pnpm lint && pnpm vitest run --testNamePattern "Hybrid|LibraryHub|useUserKbDocs|kb-docs|useAgents|agentsClient" 2>&1 | tail -20
```

Expected: typecheck 0 errors; lint clean; tests all PASS.

| AC | Verified by |
|---|---|
| AC2.b.1 (kb tab renders real cards) | Tasks 1-3 (schema/client/hook) + Task 5 (wiring) |
| AC2.b.2 (agents library-scoped) | Task 4 (scope param) + Task 5 (wiring with `scope: 'my-library'`) |
| AC2.b.3 (hero chips reflect real counts) | Task 5 (`totalCounts.agents`/`.kb` from real lengths) + Task 7 (test) |
| AC2.b.4 (mapper signature unchanged via adapter) | Task 3 (`toKbDoc` adapter — `kbDocToHubItem` mapper unchanged) |
| AC2.b.5 (partial-failure graceful) | Task 6 (new test) |
| AC2.b.6 (cross-entity sort) | Task 8 (new derive test) |

---

## Out of scope (follow-ups)

- **K1.3 BE enhancement**: expose `updatedAt` explicit in `UserKbDocDto` → already filed as issue #1645.
- **Phase 3b #1593**: `useActivityFeed` consuming `GET /api/v1/activity` (BE-3 #1590 already merged); orthogonal to 2b.
- **Selection-mode for agents/kb tabs**: remains game-scoped per Phase 2a AC9.
- **Infinite scroll / pagination beyond 20**: K3 explicit YAGNI for 2b.
