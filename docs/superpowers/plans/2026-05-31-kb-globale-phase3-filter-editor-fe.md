# KB Globale Phase 3 — FilterAccordion + KbEditorDesktop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the two deferred FE components (FilterAccordion + KbEditorDesktop) of `/knowledge-base/global`, completing Issue #1482 now that BE blockers #1686 (server-side facets) + #1687 (PATCH /kb-docs/{id}) are merged on `main-dev`.

**Architecture:** FE-only single PR. Two new components + two new hooks + schema/client extensions wired into the existing `KbGlobaleView` orchestrator (Phase 1+2 PR #1688/#1701). FilterAccordion is eager-loaded (Phase 1 bundle, <5KB); KbEditorDesktop is lazy via `dynamic({ssr:false})` (mirrors Phase 2 viewer/drawer pattern). Cache invalidation aggressive post-PATCH (DEC-2). Edit affordance only in `KbHomeDesktop` recent cards (DEC-3 anti-info-leak preserved via BE 404).

**Tech Stack:** Next.js App Router 16, React 19, TanStack Query v5, Zod, React Hook Form (already in tree), MSW for integration tests, Vitest + jest-axe, DS-15 design tokens.

**Issue:** #1737 · **Epic:** #1475 · **Parent:** #1482 · **BE shipped:** PR #1730 (#1686) + PR #1732 (#1687) + PR #1736 (#1731)
**Decisions doc:** [issue #1737 comment](https://github.com/meepleAi-app/meepleai-monorepo/issues/1737#issuecomment-4586313755) DEC-1..DEC-7 + S1..S6

---

## File Structure

### Modify (5 files)

| File | Change |
|---|---|
| `apps/web/src/lib/api/schemas/kb-globale.schemas.ts` | Extend `GlobalKbSearchRequestSchema`: add `docType?: string[]`, `gameId?: string[]`, `language?: string` |
| `apps/web/src/lib/api/schemas/kb-docs.schemas.ts` | Extend `UserKbDocDtoSchema`: add `title?: string \| null`, `tags?: string[]` (default `[]`), `updatedBy?: string \| null` |
| `apps/web/src/lib/api/clients/kbDocsClient.ts` | Extend `searchGlobal` to accept new filter fields; add new `patchKbDocMetadata(id, body)` method |
| `apps/web/src/hooks/queries/useGlobalKbSearch.ts` | Extend `UseGlobalKbSearchOptions`: accept `filters?: GlobalKbSearchFilters`; include filters in `queryKey` + `queryFn` |
| `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx` | Parse `?docType=`/`?game=`/`?lang=`/`?edit=1`; wire filters into `useGlobalKbSearch`; lazy-mount `KbEditorDesktop`; extend LABELS |

### Create (8 files)

| File | Responsibility |
|---|---|
| `apps/web/src/lib/api/schemas/kb-docs-patch.schemas.ts` | `PatchKbDocMetadataRequestSchema` request body (Title?, DocumentType?, Language?, Tags?) |
| `apps/web/src/hooks/mutations/useUpdateKbDocMeta.ts` | `useMutation` hook for `PATCH /kb-docs/{id}`; aggressive cache invalidation (DEC-2) |
| `apps/web/src/hooks/mutations/__tests__/useUpdateKbDocMeta.test.tsx` | Unit tests for mutation hook (success/404/422/cache invalidation) |
| `apps/web/src/components/features/kb-globale/FilterAccordion.tsx` | 3 facets (DocType multi, GameId multi, Language single) with URL SSOT via callbacks |
| `apps/web/src/components/features/kb-globale/__tests__/FilterAccordion.test.tsx` | Unit + jest-axe (S1/S2/S3 + a11y) |
| `apps/web/src/components/features/kb-globale/KbEditorDesktop.tsx` | Form: title (text), documentType (select), language (select), tags (chip input); partial update; FluentValidation envelope rendering |
| `apps/web/src/components/features/kb-globale/__tests__/KbEditorDesktop.test.tsx` | Unit + jest-axe (S4/S5/S6 + form a11y) |
| `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.phase3.integration.test.tsx` | MSW integration covering S1-S6 e2e through orchestrator |

### Modify (3 ancillary)

| File | Change |
|---|---|
| `apps/web/messages/it.json` | Add `kbGlobale.filters.*` + `kbGlobale.editor.*` namespace |
| `apps/web/messages/en.json` | Mirror it.json |
| `docs/for-developers/frontend/v2-migration-matrix.md` | Flip kb-globale components #4 (FilterAccordion) + #8 (KbEditorDesktop) from `pending` → `done`; reference PR |

---

## Task 1: Schemas extension

**Files:**
- Modify: `apps/web/src/lib/api/schemas/kb-globale.schemas.ts`
- Modify: `apps/web/src/lib/api/schemas/kb-docs.schemas.ts`
- Create: `apps/web/src/lib/api/schemas/kb-docs-patch.schemas.ts`
- Test: `apps/web/src/lib/api/schemas/__tests__/kb-globale.schemas.test.ts` (modify, add filter tests)
- Test: `apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts` (modify, add new field tests)
- Test: `apps/web/src/lib/api/schemas/__tests__/kb-docs-patch.schemas.test.ts` (create)

- [ ] **Step 1: Write failing tests for GlobalKbSearchRequestSchema filters**

Add to `apps/web/src/lib/api/schemas/__tests__/kb-globale.schemas.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { GlobalKbSearchRequestSchema } from '../kb-globale.schemas';

describe('GlobalKbSearchRequestSchema — filters (Phase 3 #1737)', () => {
  it('accepts docType as string[]', () => {
    const result = GlobalKbSearchRequestSchema.safeParse({
      query: 'azul',
      docType: ['Rulebook', 'Errata'],
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.docType).toEqual(['Rulebook', 'Errata']);
    }
  });

  it('accepts gameId as uuid[]', () => {
    const result = GlobalKbSearchRequestSchema.safeParse({
      query: 'azul',
      gameId: ['00000000-0000-0000-0000-000000000001'],
    });
    expect(result.success).toBe(true);
  });

  it('rejects gameId with non-uuid string', () => {
    const result = GlobalKbSearchRequestSchema.safeParse({
      query: 'azul',
      gameId: ['not-a-uuid'],
    });
    expect(result.success).toBe(false);
  });

  it('accepts language as single string', () => {
    const result = GlobalKbSearchRequestSchema.safeParse({ query: 'azul', language: 'it' });
    expect(result.success).toBe(true);
  });

  it('allows all filter fields to be omitted (backwards-compat)', () => {
    const result = GlobalKbSearchRequestSchema.safeParse({ query: 'azul' });
    expect(result.success).toBe(true);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm test apps/web/src/lib/api/schemas/__tests__/kb-globale.schemas.test.ts`
Expected: 5 FAIL with "Unrecognized key" or "undefined property".

- [ ] **Step 3: Extend GlobalKbSearchRequestSchema**

Modify `apps/web/src/lib/api/schemas/kb-globale.schemas.ts` — replace `GlobalKbSearchRequestSchema` block:

```ts
/**
 * GlobalKbSearchRequest — the POST /knowledge-base/search/global request body.
 * Phase 3 (#1737): adds optional facet filters per BE PR #1730 (#1686).
 * - docType: 7-value allowlist BE-side (D-6 of #1686); FE sends raw strings.
 * - gameId: UUID array; non-accessible IDs return 200 empty (D-4 anti-info-leak).
 * - language: ISO 639-1 lowercase {en,it,de,fr,es} (D-7 of #1686).
 */
export const GlobalKbSearchRequestSchema = z.object({
  query: z.string().min(1, 'Query must not be empty'),
  limit: z.number().int().positive().optional(),
  cursor: z.string().nullable().optional(),
  mode: SearchModeSchema.optional(),
  docType: z.array(z.string()).optional(),
  gameId: z.array(z.string().uuid()).optional(),
  language: z.string().optional(),
});

export type GlobalKbSearchRequest = z.infer<typeof GlobalKbSearchRequestSchema>;

/**
 * GlobalKbSearchFilters — typed FE-side helper for the orchestrator/components.
 * Used by useGlobalKbSearch options + FilterAccordion props.
 */
export interface GlobalKbSearchFilters {
  docType?: readonly string[];
  gameId?: readonly string[];
  language?: string;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm test apps/web/src/lib/api/schemas/__tests__/kb-globale.schemas.test.ts`
Expected: 5 PASS.

- [ ] **Step 5: Write failing tests for UserKbDocDtoSchema extension**

Add to `apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts`:

```ts
import { UserKbDocDtoSchema } from '../kb-docs.schemas';

describe('UserKbDocDtoSchema — Phase 3 #1687 metadata fields', () => {
  const baseFixture = {
    id: '00000000-0000-0000-0000-000000000001',
    gameId: '00000000-0000-0000-0000-000000000002',
    gameName: 'Azul',
    fileName: 'azul-rules.pdf',
    processingState: 'Ready' as const,
    pageCount: 24,
    processedAt: '2026-05-31T00:00:00Z',
    uploadedAt: '2026-05-31T00:00:00Z',
    updatedAt: '2026-05-31T00:00:00Z',
  };

  it('accepts title as nullable string', () => {
    const result = UserKbDocDtoSchema.safeParse({ ...baseFixture, title: 'Azul Master Edition' });
    expect(result.success).toBe(true);
  });

  it('accepts title as null', () => {
    const result = UserKbDocDtoSchema.safeParse({ ...baseFixture, title: null });
    expect(result.success).toBe(true);
  });

  it('accepts tags as string array', () => {
    const result = UserKbDocDtoSchema.safeParse({ ...baseFixture, tags: ['strategy', 'family'] });
    expect(result.success).toBe(true);
  });

  it('accepts tags as empty array', () => {
    const result = UserKbDocDtoSchema.safeParse({ ...baseFixture, tags: [] });
    expect(result.success).toBe(true);
  });

  it('accepts updatedBy as nullable uuid', () => {
    const result = UserKbDocDtoSchema.safeParse({
      ...baseFixture,
      updatedBy: '00000000-0000-0000-0000-000000000003',
    });
    expect(result.success).toBe(true);
  });

  it('back-compat: validates without title/tags/updatedBy (existing payloads)', () => {
    const result = UserKbDocDtoSchema.safeParse(baseFixture);
    expect(result.success).toBe(true);
  });
});
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `pnpm test apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts`
Expected: 6 FAIL.

- [ ] **Step 7: Extend UserKbDocDtoSchema additively**

Modify `apps/web/src/lib/api/schemas/kb-docs.schemas.ts` — replace `UserKbDocDtoSchema` block:

```ts
/**
 * UserKbDocDto — lightweight cross-game user-scoped projection (BE-1 #1588).
 * Phase 3 (#1737): adds title/tags/updatedBy per BE PR #1732 (#1687).
 * All Phase 3 fields are optional/nullable for backwards-compat with pre-#1687 payloads.
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
  updatedAt: z.string().datetime({ offset: true }),
  // Phase 3 (#1687) additions:
  title: z.string().nullable().optional(),
  tags: z.array(z.string()).optional(),
  updatedBy: z.string().uuid().nullable().optional(),
});

export type UserKbDocDto = z.infer<typeof UserKbDocDtoSchema>;
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `pnpm test apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts`
Expected: 6 PASS (new) + all pre-existing tests still pass.

- [ ] **Step 9: Write tests for new PatchKbDocMetadataRequestSchema**

Create `apps/web/src/lib/api/schemas/__tests__/kb-docs-patch.schemas.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { PatchKbDocMetadataRequestSchema } from '../kb-docs-patch.schemas';

describe('PatchKbDocMetadataRequestSchema (Phase 3 #1737)', () => {
  it('accepts fully populated body', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({
      title: 'New Title',
      documentType: 'Rulebook',
      language: 'it',
      tags: ['strategy', 'family'],
    });
    expect(result.success).toBe(true);
  });

  it('accepts empty body (no-op)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({});
    expect(result.success).toBe(true);
  });

  it('accepts title=empty-string (clear semantic per D-4 #1687)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({ title: '' });
    expect(result.success).toBe(true);
  });

  it('accepts tags=[] (clear semantic)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({ tags: [] });
    expect(result.success).toBe(true);
  });

  it('rejects title > 200 chars (D-5 #1687)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({ title: 'x'.repeat(201) });
    expect(result.success).toBe(false);
  });

  it('rejects tag > 50 chars (D-8 #1687)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({ tags: ['x'.repeat(51)] });
    expect(result.success).toBe(false);
  });

  it('rejects > 20 tags (D-8 #1687)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({
      tags: Array.from({ length: 21 }, (_, i) => `tag${i}`),
    });
    expect(result.success).toBe(false);
  });
});
```

- [ ] **Step 10: Run tests to verify they fail**

Run: `pnpm test apps/web/src/lib/api/schemas/__tests__/kb-docs-patch.schemas.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 11: Create PatchKbDocMetadataRequestSchema**

Create `apps/web/src/lib/api/schemas/kb-docs-patch.schemas.ts`:

```ts
/**
 * PATCH /api/v1/kb-docs/{id} request schema (Phase 3 #1737, BE PR #1732 #1687).
 *
 * Partial update semantics (D-4 #1687):
 * - JSON `null` (omitted in TS) = no-op
 * - empty string ("") = clear the field
 * - empty array ([]) = clear tags
 *
 * Constraints (D-5/D-8 #1687):
 * - title: max 200 chars
 * - tags: max 20 items, each max 50 chars
 *
 * Allowlist enforcement is BE-side (FE sends raw strings):
 * - documentType: 7-value DocumentCategory enum BE-side (D-6)
 * - language: 10-value LanguageCode whitelist BE-side (D-7)
 */
import { z } from 'zod';

const TITLE_MAX = 200;
const TAG_MAX_LENGTH = 50;
const TAGS_MAX_COUNT = 20;

export const PatchKbDocMetadataRequestSchema = z.object({
  title: z.string().max(TITLE_MAX, `Title must not exceed ${TITLE_MAX} characters.`).optional(),
  documentType: z.string().optional(),
  language: z.string().optional(),
  tags: z
    .array(z.string().max(TAG_MAX_LENGTH, `Tag must not exceed ${TAG_MAX_LENGTH} characters.`))
    .max(TAGS_MAX_COUNT, `No more than ${TAGS_MAX_COUNT} tags allowed.`)
    .optional(),
});

export type PatchKbDocMetadataRequest = z.infer<typeof PatchKbDocMetadataRequestSchema>;
```

- [ ] **Step 12: Run tests to verify they pass**

Run: `pnpm test apps/web/src/lib/api/schemas/__tests__/kb-docs-patch.schemas.test.ts`
Expected: 7 PASS.

- [ ] **Step 13: Commit**

```bash
git add apps/web/src/lib/api/schemas/kb-globale.schemas.ts \
        apps/web/src/lib/api/schemas/kb-docs.schemas.ts \
        apps/web/src/lib/api/schemas/kb-docs-patch.schemas.ts \
        apps/web/src/lib/api/schemas/__tests__/kb-globale.schemas.test.ts \
        apps/web/src/lib/api/schemas/__tests__/kb-docs.schemas.test.ts \
        apps/web/src/lib/api/schemas/__tests__/kb-docs-patch.schemas.test.ts
git commit -m "feat(kb-globale): #1737 Task 1 — schemas for filters + PATCH metadata"
```

---

## Task 2: API client extension

**Files:**
- Modify: `apps/web/src/lib/api/clients/kbDocsClient.ts`
- Test: `apps/web/src/lib/api/clients/__tests__/kbDocsClient.test.ts` (modify)

- [ ] **Step 1: Write failing tests for searchGlobal filters + patchKbDocMetadata**

Add to `apps/web/src/lib/api/clients/__tests__/kbDocsClient.test.ts`:

```ts
import { describe, expect, it, vi } from 'vitest';
import { createKbDocsClient } from '../kbDocsClient';
import type { HttpClient } from '../../core/httpClient';

describe('kbDocsClient.searchGlobal — filters (Phase 3 #1737)', () => {
  it('forwards docType filter to POST body', async () => {
    const mockPost = vi.fn().mockResolvedValue({ results: [], hasMore: false, nextCursor: null });
    const client = createKbDocsClient({
      httpClient: { post: mockPost, get: vi.fn(), put: vi.fn(), delete: vi.fn(), patch: vi.fn() } as unknown as HttpClient,
    });
    await client.searchGlobal({ query: 'azul', docType: ['Rulebook'] });
    expect(mockPost).toHaveBeenCalledWith(
      '/api/v1/knowledge-base/search/global',
      expect.objectContaining({ query: 'azul', docType: ['Rulebook'] }),
      expect.anything()
    );
  });

  it('forwards gameId + language filters to POST body', async () => {
    const mockPost = vi.fn().mockResolvedValue({ results: [], hasMore: false, nextCursor: null });
    const client = createKbDocsClient({
      httpClient: { post: mockPost, get: vi.fn(), put: vi.fn(), delete: vi.fn(), patch: vi.fn() } as unknown as HttpClient,
    });
    await client.searchGlobal({
      query: 'azul',
      gameId: ['00000000-0000-0000-0000-000000000001'],
      language: 'it',
    });
    expect(mockPost).toHaveBeenCalledWith(
      '/api/v1/knowledge-base/search/global',
      expect.objectContaining({
        gameId: ['00000000-0000-0000-0000-000000000001'],
        language: 'it',
      }),
      expect.anything()
    );
  });
});

describe('kbDocsClient.patchKbDocMetadata (Phase 3 #1737)', () => {
  it('calls PATCH /api/v1/kb-docs/{id} with body', async () => {
    const mockPatch = vi.fn().mockResolvedValue({
      id: '00000000-0000-0000-0000-000000000001',
      fileName: 'azul.pdf',
      gameId: null,
      gameName: null,
      processingState: 'Ready',
      pageCount: 24,
      processedAt: '2026-05-31T00:00:00Z',
      uploadedAt: '2026-05-31T00:00:00Z',
      updatedAt: '2026-05-31T00:00:00Z',
      title: 'New Title',
      tags: ['strategy'],
      updatedBy: '00000000-0000-0000-0000-000000000003',
    });
    const client = createKbDocsClient({
      httpClient: { post: vi.fn(), get: vi.fn(), put: vi.fn(), delete: vi.fn(), patch: mockPatch } as unknown as HttpClient,
    });
    const result = await client.patchKbDocMetadata('00000000-0000-0000-0000-000000000001', {
      title: 'New Title',
      tags: ['strategy'],
    });
    expect(mockPatch).toHaveBeenCalledWith(
      '/api/v1/kb-docs/00000000-0000-0000-0000-000000000001',
      { title: 'New Title', tags: ['strategy'] },
      expect.anything()
    );
    expect(result.title).toBe('New Title');
  });

  it('propagates 404 errors (D-2 anti-info-leak)', async () => {
    const mockPatch = vi.fn().mockRejectedValue(new Error('HTTP 404'));
    const client = createKbDocsClient({
      httpClient: { post: vi.fn(), get: vi.fn(), put: vi.fn(), delete: vi.fn(), patch: mockPatch } as unknown as HttpClient,
    });
    await expect(
      client.patchKbDocMetadata('00000000-0000-0000-0000-000000000001', { title: 'X' })
    ).rejects.toThrow('HTTP 404');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm test apps/web/src/lib/api/clients/__tests__/kbDocsClient.test.ts`
Expected: 4 FAIL — `patchKbDocMetadata` not defined; `searchGlobal` doesn't include filter fields.

- [ ] **Step 3: Extend kbDocsClient**

Modify `apps/web/src/lib/api/clients/kbDocsClient.ts` — replace existing implementation:

```ts
import { KbDocsListResponseSchema, type KbDocsListResponse, type UserKbDocDto, UserKbDocDtoSchema } from '../schemas/kb-docs.schemas';
import { PatchKbDocMetadataRequestSchema, type PatchKbDocMetadataRequest } from '../schemas/kb-docs-patch.schemas';
import {
  GlobalKbSearchResponseSchema,
  type GlobalKbSearchRequest,
  type GlobalKbSearchResponse,
} from '../schemas/kb-globale.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateKbDocsClientParams {
  httpClient: HttpClient;
}

export interface ListUserKbDocsParams {
  page?: number;
  pageSize?: number;
  sortBy?: 'recent';
  state?: 'ready' | 'all';
}

export interface KbDocsClient {
  listUserKbDocs(params?: ListUserKbDocsParams): Promise<KbDocsListResponse>;
  searchGlobal(req: GlobalKbSearchRequest): Promise<GlobalKbSearchResponse>;
  /** Phase 3 #1737 / BE PR #1732 #1687 — PATCH /api/v1/kb-docs/{id} for owner metadata edit. */
  patchKbDocMetadata(id: string, body: PatchKbDocMetadataRequest): Promise<UserKbDocDto>;
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
      return (
        response ?? { items: [], total: 0, page: params.page ?? 1, pageSize: params.pageSize ?? 20 }
      );
    },

    async searchGlobal(req: GlobalKbSearchRequest): Promise<GlobalKbSearchResponse> {
      // Phase 3 (#1737): req now optionally includes docType, gameId, language
      // — forwarded verbatim to BE per PR #1730 (#1686) DTO shape.
      const response = await httpClient.post<GlobalKbSearchResponse>(
        '/api/v1/knowledge-base/search/global',
        req,
        GlobalKbSearchResponseSchema
      );
      return response ?? { results: [], hasMore: false, nextCursor: null };
    },

    async patchKbDocMetadata(
      id: string,
      body: PatchKbDocMetadataRequest
    ): Promise<UserKbDocDto> {
      const validated = PatchKbDocMetadataRequestSchema.parse(body);
      const response = await httpClient.patch<UserKbDocDto>(
        `/api/v1/kb-docs/${id}`,
        validated,
        UserKbDocDtoSchema
      );
      if (response == null) {
        throw new Error('PATCH /kb-docs returned no body');
      }
      return response;
    },
  };
}
```

NOTE: if `HttpClient` interface doesn't expose `patch`, **first** verify and extend it. The Phase 1 client used `get`/`post`. Check `apps/web/src/lib/api/core/httpClient.ts` for the `patch` method signature; if absent, add it following the existing `post` pattern. This is a pre-requisite for Step 3.

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm test apps/web/src/lib/api/clients/__tests__/kbDocsClient.test.ts`
Expected: 4 PASS (new) + all pre-existing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/clients/kbDocsClient.ts \
        apps/web/src/lib/api/clients/__tests__/kbDocsClient.test.ts
# include httpClient.ts if patch method was added
git commit -m "feat(kb-globale): #1737 Task 2 — kbDocsClient.searchGlobal filters + patchKbDocMetadata"
```

---

## Task 3: useGlobalKbSearch — filters extension

**Files:**
- Modify: `apps/web/src/hooks/queries/useGlobalKbSearch.ts`
- Test: `apps/web/src/hooks/queries/__tests__/useGlobalKbSearch.test.tsx` (modify)

- [ ] **Step 1: Write failing tests for filter propagation**

Add to `apps/web/src/hooks/queries/__tests__/useGlobalKbSearch.test.tsx`:

```ts
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useGlobalKbSearch } from '../useGlobalKbSearch';
import { api } from '@/lib/api';

// Helper: wrap hook with QueryClient
function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('useGlobalKbSearch — filters (Phase 3 #1737)', () => {
  it('passes docType filter to client call', async () => {
    const spy = vi.spyOn(api.kbDocs, 'searchGlobal').mockResolvedValue({
      results: [], hasMore: false, nextCursor: null,
    });
    renderHook(
      () => useGlobalKbSearch({ query: 'azul', filters: { docType: ['Rulebook'] } }),
      { wrapper }
    );
    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(expect.objectContaining({ docType: ['Rulebook'] }))
    );
    spy.mockRestore();
  });

  it('passes all filter fields to client call', async () => {
    const spy = vi.spyOn(api.kbDocs, 'searchGlobal').mockResolvedValue({
      results: [], hasMore: false, nextCursor: null,
    });
    renderHook(
      () =>
        useGlobalKbSearch({
          query: 'azul',
          filters: {
            docType: ['Rulebook'],
            gameId: ['00000000-0000-0000-0000-000000000001'],
            language: 'it',
          },
        }),
      { wrapper }
    );
    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        expect.objectContaining({
          docType: ['Rulebook'],
          gameId: ['00000000-0000-0000-0000-000000000001'],
          language: 'it',
        })
      )
    );
    spy.mockRestore();
  });

  it('queryKey includes filters (different filters = different cache entry)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'searchGlobal').mockResolvedValue({
      results: [], hasMore: false, nextCursor: null,
    });
    const { rerender } = renderHook(
      ({ filters }: { filters: { docType?: readonly string[] } }) =>
        useGlobalKbSearch({ query: 'azul', filters }),
      { wrapper, initialProps: { filters: { docType: ['Rulebook'] } } }
    );
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));
    rerender({ filters: { docType: ['Errata'] } });
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(2));
    spy.mockRestore();
  });

  it('omitted filters are not sent (backwards-compat with Phase 1+2 callers)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'searchGlobal').mockResolvedValue({
      results: [], hasMore: false, nextCursor: null,
    });
    renderHook(() => useGlobalKbSearch({ query: 'azul' }), { wrapper });
    await waitFor(() => expect(spy).toHaveBeenCalled());
    const call = spy.mock.calls[0]?.[0];
    expect(call).not.toHaveProperty('docType');
    expect(call).not.toHaveProperty('gameId');
    expect(call).not.toHaveProperty('language');
    spy.mockRestore();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm test apps/web/src/hooks/queries/__tests__/useGlobalKbSearch.test.tsx`
Expected: 4 FAIL — `filters` prop unrecognized.

- [ ] **Step 3: Extend useGlobalKbSearch**

Modify `apps/web/src/hooks/queries/useGlobalKbSearch.ts` — replace `kbGlobaleSearchKeys`, `UseGlobalKbSearchOptions`, and the hook body:

```ts
import { useInfiniteQuery } from '@tanstack/react-query';

import { useDebounce } from '@/hooks/useDebounce';
import { api } from '@/lib/api';
import type {
  GlobalKbSearchFilters,
  GlobalKbSearchResult,
  GlobalKbSearchResponse,
  SearchMode,
} from '@/lib/api/schemas/kb-globale.schemas';

export const KB_GLOBALE_SEARCH_STALE_TIME_MS = 5 * 60 * 1000;

/** Stable serialized form of filters for queryKey identity. */
function serializeFilters(filters?: GlobalKbSearchFilters): string {
  if (!filters) return '';
  const parts: string[] = [];
  if (filters.docType && filters.docType.length > 0) {
    parts.push(`docType:${[...filters.docType].sort().join(',')}`);
  }
  if (filters.gameId && filters.gameId.length > 0) {
    parts.push(`gameId:${[...filters.gameId].sort().join(',')}`);
  }
  if (filters.language) parts.push(`language:${filters.language}`);
  return parts.join('|');
}

export const kbGlobaleSearchKeys = {
  all: ['kb-globale', 'search'] as const,
  byQuery: (query: string, mode: SearchMode | null, filtersKey: string) =>
    [...kbGlobaleSearchKeys.all, { query, mode, filtersKey }] as const,
};

export interface UseGlobalKbSearchOptions {
  query: string;
  mode?: SearchMode;
  debounceMs?: number;
  enabled?: boolean;
  /** Phase 3 (#1737): optional server-side facet filters. */
  filters?: GlobalKbSearchFilters;
}

export interface UseGlobalKbSearchResult {
  results: readonly GlobalKbSearchResult[];
  hasMore: boolean;
  isLoading: boolean;
  isFetchingNextPage: boolean;
  error: Error | null;
  fetchNextPage: () => void;
}

export function useGlobalKbSearch(opts: UseGlobalKbSearchOptions): UseGlobalKbSearchResult {
  const { query, mode, debounceMs = 250, enabled, filters } = opts;

  const debouncedQuery = useDebounce(query.trim(), debounceMs);
  const effectiveEnabled = enabled ?? debouncedQuery.length >= 2;
  const filtersKey = serializeFilters(filters);

  const q = useInfiniteQuery<
    GlobalKbSearchResponse,
    Error,
    { pages: GlobalKbSearchResponse[] },
    ReturnType<typeof kbGlobaleSearchKeys.byQuery>,
    string | null
  >({
    queryKey: kbGlobaleSearchKeys.byQuery(debouncedQuery, mode ?? null, filtersKey),
    queryFn: ({ pageParam }) =>
      api.kbDocs.searchGlobal({
        query: debouncedQuery,
        mode,
        cursor: pageParam,
        // Phase 3: only include non-empty filters to keep wire format clean.
        ...(filters?.docType && filters.docType.length > 0
          ? { docType: [...filters.docType] }
          : {}),
        ...(filters?.gameId && filters.gameId.length > 0
          ? { gameId: [...filters.gameId] }
          : {}),
        ...(filters?.language ? { language: filters.language } : {}),
      }),
    initialPageParam: null,
    getNextPageParam: (lastPage: GlobalKbSearchResponse) =>
      lastPage.hasMore ? lastPage.nextCursor : undefined,
    enabled: effectiveEnabled,
    staleTime: KB_GLOBALE_SEARCH_STALE_TIME_MS,
  });

  const results: readonly GlobalKbSearchResult[] = q.data?.pages.flatMap(p => p.results) ?? [];
  const pages = q.data?.pages;
  const hasMore =
    pages != null && pages.length > 0 ? (pages[pages.length - 1]?.hasMore ?? false) : false;

  return {
    results,
    hasMore,
    isLoading: q.isLoading,
    isFetchingNextPage: q.isFetchingNextPage,
    error: q.error,
    fetchNextPage: () => {
      void q.fetchNextPage();
    },
  };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm test apps/web/src/hooks/queries/__tests__/useGlobalKbSearch.test.tsx`
Expected: 4 new PASS + all pre-existing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/queries/useGlobalKbSearch.ts \
        apps/web/src/hooks/queries/__tests__/useGlobalKbSearch.test.tsx
git commit -m "feat(kb-globale): #1737 Task 3 — useGlobalKbSearch accepts facet filters"
```

---

## Task 4: useUpdateKbDocMeta — mutation hook with aggressive cache invalidation

**Files:**
- Create: `apps/web/src/hooks/mutations/useUpdateKbDocMeta.ts`
- Create: `apps/web/src/hooks/mutations/__tests__/useUpdateKbDocMeta.test.tsx`

- [ ] **Step 1: Write failing tests for mutation + cache invalidation**

Create `apps/web/src/hooks/mutations/__tests__/useUpdateKbDocMeta.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import type { JSX, ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useUpdateKbDocMeta } from '../useUpdateKbDocMeta';
import { api } from '@/lib/api';

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const DTO_FIXTURE = {
  id: '00000000-0000-0000-0000-000000000001',
  gameId: null,
  gameName: null,
  fileName: 'azul.pdf',
  processingState: 'Ready' as const,
  pageCount: 24,
  processedAt: '2026-05-31T00:00:00Z',
  uploadedAt: '2026-05-31T00:00:00Z',
  updatedAt: '2026-05-31T00:00:00Z',
  title: 'New Title',
  tags: ['strategy'],
  updatedBy: '00000000-0000-0000-0000-000000000003',
};

describe('useUpdateKbDocMeta (Phase 3 #1737)', () => {
  it('calls api.kbDocs.patchKbDocMetadata on mutate', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockResolvedValue(DTO_FIXTURE);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      await result.current.mutateAsync({
        id: '00000000-0000-0000-0000-000000000001',
        body: { title: 'New Title' },
      });
    });
    expect(spy).toHaveBeenCalledWith(
      '00000000-0000-0000-0000-000000000001',
      { title: 'New Title' }
    );
    spy.mockRestore();
  });

  it('invalidates ["kb-docs","user"] queries on success', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockResolvedValue(DTO_FIXTURE);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      await result.current.mutateAsync({
        id: '00000000-0000-0000-0000-000000000001',
        body: { title: 'New Title' },
      });
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['kb-docs', 'user'] });
    spy.mockRestore();
  });

  it('invalidates ["kb-globale","search"] queries on success (DEC-2 aggressive)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockResolvedValue(DTO_FIXTURE);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      await result.current.mutateAsync({
        id: '00000000-0000-0000-0000-000000000001',
        body: { title: 'New Title' },
      });
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['kb-globale', 'search'] });
    spy.mockRestore();
  });

  it('exposes error on 404 (S5 anti-info-leak)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockRejectedValue(new Error('HTTP 404'));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      try {
        await result.current.mutateAsync({
          id: '00000000-0000-0000-0000-000000000001',
          body: { title: 'X' },
        });
      } catch {
        /* expected */
      }
    });
    await waitFor(() => expect(result.current.error).toBeInstanceOf(Error));
    expect(result.current.error?.message).toContain('404');
    spy.mockRestore();
  });

  it('does NOT invalidate on error (cache stays consistent)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockRejectedValue(new Error('HTTP 422'));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      try {
        await result.current.mutateAsync({
          id: '00000000-0000-0000-0000-000000000001',
          body: { title: 'X' },
        });
      } catch {
        /* expected */
      }
    });
    expect(invalidateSpy).not.toHaveBeenCalled();
    spy.mockRestore();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm test apps/web/src/hooks/mutations/__tests__/useUpdateKbDocMeta.test.tsx`
Expected: 5 FAIL — module not found.

- [ ] **Step 3: Implement useUpdateKbDocMeta**

Create `apps/web/src/hooks/mutations/useUpdateKbDocMeta.ts`:

```ts
/**
 * useUpdateKbDocMeta — Phase 3 #1737 mutation hook for PATCH /api/v1/kb-docs/{id}.
 *
 * Cache invalidation (DEC-2 aggressive — issue #1737 spec-panel):
 *   - ['kb-docs', 'user']         → KbHomeDesktop recent docs
 *   - ['kb-globale', 'search']    → search results (title affects display)
 *   - ['kb-docs', 'detail', id]   → per-doc detail cache (viewer hero)
 *
 * BE contract (PR #1732 #1687):
 *   - 200 + extended UserKbDocDto (Title, Tags, UpdatedBy added)
 *   - 404 = not accessible (anti-info-leak D-2, NOT 403)
 *   - 422 = validation envelope (FluentValidation)
 */
import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';
import type { PatchKbDocMetadataRequest } from '@/lib/api/schemas/kb-docs-patch.schemas';

export interface UpdateKbDocMetaInput {
  id: string;
  body: PatchKbDocMetadataRequest;
}

export type UseUpdateKbDocMetaResult = UseMutationResult<
  UserKbDocDto,
  Error,
  UpdateKbDocMetaInput
>;

export function useUpdateKbDocMeta(): UseUpdateKbDocMetaResult {
  const qc = useQueryClient();
  return useMutation<UserKbDocDto, Error, UpdateKbDocMetaInput>({
    mutationFn: ({ id, body }) => api.kbDocs.patchKbDocMetadata(id, body),
    onSuccess: (_data, vars) => {
      // DEC-2 aggressive invalidation: any cached query whose key starts with
      // these prefixes will refetch on next mount/observer.
      qc.invalidateQueries({ queryKey: ['kb-docs', 'user'] });
      qc.invalidateQueries({ queryKey: ['kb-globale', 'search'] });
      qc.invalidateQueries({ queryKey: ['kb-docs', 'detail', vars.id] });
    },
  });
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm test apps/web/src/hooks/mutations/__tests__/useUpdateKbDocMeta.test.tsx`
Expected: 5 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/mutations/useUpdateKbDocMeta.ts \
        apps/web/src/hooks/mutations/__tests__/useUpdateKbDocMeta.test.tsx
git commit -m "feat(kb-globale): #1737 Task 4 — useUpdateKbDocMeta mutation hook with DEC-2 aggressive invalidation"
```

---

## Task 5: FilterAccordion component

**Files:**
- Create: `apps/web/src/components/features/kb-globale/FilterAccordion.tsx`
- Create: `apps/web/src/components/features/kb-globale/__tests__/FilterAccordion.test.tsx`

- [ ] **Step 1: Write failing tests covering S1/S2/S3 + a11y**

Create `apps/web/src/components/features/kb-globale/__tests__/FilterAccordion.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';

import { FilterAccordion } from '../FilterAccordion';

expect.extend(toHaveNoViolations);

const DEFAULT_LABELS = {
  heading: 'Filtri',
  docTypeLabel: 'Tipo documento',
  gameIdLabel: 'Gioco',
  languageLabel: 'Lingua',
  clearAll: 'Cancella filtri',
  docTypeOptions: {
    Rulebook: 'Regolamento',
    Expansion: 'Espansione',
    Errata: 'Errata',
    QuickStart: 'Quick Start',
    Reference: 'Riferimento',
    PlayerAid: 'Aiuto giocatore',
    Other: 'Altro',
  },
  languageOptions: { en: 'English', it: 'Italiano', de: 'Deutsch', fr: 'Français', es: 'Español' },
};

const GAMES_FIXTURE = [
  { id: '00000000-0000-0000-0000-000000000001', name: 'Azul' },
  { id: '00000000-0000-0000-0000-000000000002', name: 'Wingspan' },
];

describe('FilterAccordion (Phase 3 #1737)', () => {
  it('renders all 3 facet sections', () => {
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{}}
        onChange={vi.fn()}
        labels={DEFAULT_LABELS}
      />
    );
    expect(screen.getByText('Tipo documento')).toBeInTheDocument();
    expect(screen.getByText('Gioco')).toBeInTheDocument();
    expect(screen.getByText('Lingua')).toBeInTheDocument();
  });

  it('renders 7 docType options from allowlist (drops faq per DEC-7)', () => {
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{}}
        onChange={vi.fn()}
        labels={DEFAULT_LABELS}
      />
    );
    // Open the section first (assuming default-collapsed accordion behavior).
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    expect(screen.getByLabelText('Regolamento')).toBeInTheDocument();
    expect(screen.getByLabelText('Espansione')).toBeInTheDocument();
    expect(screen.getByLabelText('Errata')).toBeInTheDocument();
    expect(screen.getByLabelText('Altro')).toBeInTheDocument();
    expect(screen.queryByLabelText(/FAQ/i)).not.toBeInTheDocument();
  });

  // S1: facet apply
  it('S1: calls onChange with new docType selection on toggle', () => {
    const onChange = vi.fn();
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{}}
        onChange={onChange}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    fireEvent.click(screen.getByLabelText('Regolamento'));
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ docType: ['Rulebook'] })
    );
  });

  // S2: facet clear
  it('S2: clearAll button resets all facets', () => {
    const onChange = vi.fn();
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{ docType: ['Rulebook'], language: 'it' }}
        onChange={onChange}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /cancella filtri/i }));
    expect(onChange).toHaveBeenCalledWith({});
  });

  // S3: URL SSOT round-trip (controlled component reflects external `selected`)
  it('S3: pre-selects facets from controlled `selected` prop', () => {
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{ docType: ['Rulebook'], language: 'it' }}
        onChange={vi.fn()}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    const checkbox = screen.getByLabelText('Regolamento') as HTMLInputElement;
    expect(checkbox.checked).toBe(true);
  });

  it('multi-select GameId: toggle adds a uuid to the array', () => {
    const onChange = vi.fn();
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{ gameId: ['00000000-0000-0000-0000-000000000001'] }}
        onChange={onChange}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /gioco/i }));
    fireEvent.click(screen.getByLabelText('Wingspan'));
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        gameId: expect.arrayContaining([
          '00000000-0000-0000-0000-000000000001',
          '00000000-0000-0000-0000-000000000002',
        ]),
      })
    );
  });

  it('single-select Language: replaces the previous value', () => {
    const onChange = vi.fn();
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{ language: 'it' }}
        onChange={onChange}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /lingua/i }));
    fireEvent.click(screen.getByLabelText('English'));
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ language: 'en' }));
  });

  it('a11y: no jest-axe violations', async () => {
    const { container } = render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{}}
        onChange={vi.fn()}
        labels={DEFAULT_LABELS}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm test apps/web/src/components/features/kb-globale/__tests__/FilterAccordion.test.tsx`
Expected: 8 FAIL — module not found.

- [ ] **Step 3: Implement FilterAccordion**

Create `apps/web/src/components/features/kb-globale/FilterAccordion.tsx`:

```tsx
/**
 * FilterAccordion — Phase 3 #1737 / Issue #1482 component #4.
 *
 * Three server-side facets backed by BE PR #1730 (#1686):
 *   - DocType  : multi-select, 7-value allowlist (D-6 #1686); drops `faq` per DEC-7
 *   - GameId   : multi-select, dropdown of accessible games (DEC-1: derived from useUserKbDocs)
 *   - Language : single-select, 5-value ISO 639-1 allowlist (D-7 #1686)
 *
 * Controlled component — parent owns `selected` state (URL SSOT in KbGlobaleView)
 * and reacts to `onChange`.
 *
 * Empty selection = no filter (D-3/D-5 #1686, byte-identical baseline).
 *
 * a11y: each facet section is a <details>+<summary> for native accordion;
 * checkboxes/radios for inputs; visible clearAll button at top.
 */
'use client';

import { type JSX } from 'react';
import type { GlobalKbSearchFilters } from '@/lib/api/schemas/kb-globale.schemas';

export interface FilterAccordionLabels {
  heading: string;
  docTypeLabel: string;
  gameIdLabel: string;
  languageLabel: string;
  clearAll: string;
  /** Map: BE enum string → display label */
  docTypeOptions: Readonly<Record<string, string>>;
  languageOptions: Readonly<Record<string, string>>;
}

export interface GameOption {
  id: string;
  name: string;
}

export interface FilterAccordionProps {
  availableGames: readonly GameOption[];
  selected: GlobalKbSearchFilters;
  onChange: (next: GlobalKbSearchFilters) => void;
  labels: FilterAccordionLabels;
}

const DOC_TYPE_ALLOWLIST = [
  'Rulebook',
  'Expansion',
  'Errata',
  'QuickStart',
  'Reference',
  'PlayerAid',
  'Other',
] as const;

const LANGUAGE_ALLOWLIST = ['en', 'it', 'de', 'fr', 'es'] as const;

export function FilterAccordion(props: FilterAccordionProps): JSX.Element {
  const { availableGames, selected, onChange, labels } = props;
  const selectedDocTypes = selected.docType ?? [];
  const selectedGameIds = selected.gameId ?? [];
  const selectedLanguage = selected.language ?? '';

  const toggleDocType = (value: string) => {
    const next = selectedDocTypes.includes(value)
      ? selectedDocTypes.filter(v => v !== value)
      : [...selectedDocTypes, value];
    onChange({ ...selected, docType: next.length > 0 ? next : undefined });
  };

  const toggleGameId = (id: string) => {
    const next = selectedGameIds.includes(id)
      ? selectedGameIds.filter(v => v !== id)
      : [...selectedGameIds, id];
    onChange({ ...selected, gameId: next.length > 0 ? next : undefined });
  };

  const setLanguage = (lang: string) => {
    onChange({ ...selected, language: lang || undefined });
  };

  const clearAll = () => onChange({});

  const hasAnyFilter =
    selectedDocTypes.length > 0 || selectedGameIds.length > 0 || selectedLanguage !== '';

  return (
    <aside
      className="bg-card border border-border rounded-lg p-4 flex flex-col gap-3"
      aria-label={labels.heading}
    >
      <div className="flex items-center justify-between">
        <h2 className="font-semibold text-foreground">{labels.heading}</h2>
        {hasAnyFilter && (
          <button
            type="button"
            onClick={clearAll}
            className="text-sm text-muted-foreground hover:text-foreground underline"
          >
            {labels.clearAll}
          </button>
        )}
      </div>

      {/* DocType facet */}
      <details className="border-t border-border-strong pt-2">
        <summary className="cursor-pointer font-medium text-sm text-foreground">
          {labels.docTypeLabel}
          {selectedDocTypes.length > 0 && (
            <span className="ml-2 text-muted-foreground">({selectedDocTypes.length})</span>
          )}
        </summary>
        <div className="mt-2 flex flex-col gap-1">
          {DOC_TYPE_ALLOWLIST.map(opt => (
            <label key={opt} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={selectedDocTypes.includes(opt)}
                onChange={() => toggleDocType(opt)}
              />
              <span>{labels.docTypeOptions[opt] ?? opt}</span>
            </label>
          ))}
        </div>
      </details>

      {/* GameId facet */}
      <details className="border-t border-border-strong pt-2">
        <summary className="cursor-pointer font-medium text-sm text-foreground">
          {labels.gameIdLabel}
          {selectedGameIds.length > 0 && (
            <span className="ml-2 text-muted-foreground">({selectedGameIds.length})</span>
          )}
        </summary>
        <div className="mt-2 flex flex-col gap-1 max-h-60 overflow-auto">
          {availableGames.map(g => (
            <label key={g.id} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={selectedGameIds.includes(g.id)}
                onChange={() => toggleGameId(g.id)}
              />
              <span>{g.name}</span>
            </label>
          ))}
        </div>
      </details>

      {/* Language facet */}
      <details className="border-t border-border-strong pt-2">
        <summary className="cursor-pointer font-medium text-sm text-foreground">
          {labels.languageLabel}
          {selectedLanguage && (
            <span className="ml-2 text-muted-foreground">
              ({labels.languageOptions[selectedLanguage] ?? selectedLanguage})
            </span>
          )}
        </summary>
        <div className="mt-2 flex flex-col gap-1" role="radiogroup">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="kb-globale-language"
              checked={selectedLanguage === ''}
              onChange={() => setLanguage('')}
            />
            <span className="text-muted-foreground">—</span>
          </label>
          {LANGUAGE_ALLOWLIST.map(lang => (
            <label key={lang} className="flex items-center gap-2 text-sm">
              <input
                type="radio"
                name="kb-globale-language"
                checked={selectedLanguage === lang}
                onChange={() => setLanguage(lang)}
              />
              <span>{labels.languageOptions[lang] ?? lang}</span>
            </label>
          ))}
        </div>
      </details>
    </aside>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm test apps/web/src/components/features/kb-globale/__tests__/FilterAccordion.test.tsx`
Expected: 8 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/kb-globale/FilterAccordion.tsx \
        apps/web/src/components/features/kb-globale/__tests__/FilterAccordion.test.tsx
git commit -m "feat(kb-globale): #1737 Task 5 — FilterAccordion component (3 facets + a11y)"
```

---

## Task 6: KbEditorDesktop component

**Files:**
- Create: `apps/web/src/components/features/kb-globale/KbEditorDesktop.tsx`
- Create: `apps/web/src/components/features/kb-globale/__tests__/KbEditorDesktop.test.tsx`

- [ ] **Step 1: Write failing tests covering S4/S5/S6 + a11y**

Create `apps/web/src/components/features/kb-globale/__tests__/KbEditorDesktop.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { JSX, ReactNode } from 'react';

import { KbEditorDesktop } from '../KbEditorDesktop';
import { api } from '@/lib/api';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';

expect.extend(toHaveNoViolations);

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const DOC_FIXTURE: UserKbDocDto = {
  id: '00000000-0000-0000-0000-000000000001',
  gameId: null,
  gameName: null,
  fileName: 'azul.pdf',
  processingState: 'Ready',
  pageCount: 24,
  processedAt: '2026-05-31T00:00:00Z',
  uploadedAt: '2026-05-31T00:00:00Z',
  updatedAt: '2026-05-31T00:00:00Z',
  title: 'Old Title',
  tags: ['family'],
  updatedBy: null,
};

const LABELS = {
  heading: 'Modifica metadati',
  titleLabel: 'Titolo',
  documentTypeLabel: 'Tipo documento',
  languageLabel: 'Lingua',
  tagsLabel: 'Tag',
  saveLabel: 'Salva',
  cancelLabel: 'Annulla',
  notFoundError: 'Documento non trovato',
  genericError: 'Errore generico, riprova.',
  documentTypeOptions: { Rulebook: 'Regolamento' },
  languageOptions: { it: 'Italiano', en: 'English' },
};

describe('KbEditorDesktop (Phase 3 #1737)', () => {
  it('renders form pre-filled with doc fields', () => {
    const qc = new QueryClient();
    render(
      <KbEditorDesktop
        doc={DOC_FIXTURE}
        onClose={vi.fn()}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );
    expect((screen.getByLabelText('Titolo') as HTMLInputElement).value).toBe('Old Title');
  });

  // S4: PATCH success
  it('S4: submits PATCH with changed title and closes on success', async () => {
    const spy = vi
      .spyOn(api.kbDocs, 'patchKbDocMetadata')
      .mockResolvedValue({ ...DOC_FIXTURE, title: 'New Title' });
    const onClose = vi.fn();
    const qc = new QueryClient();
    render(
      <KbEditorDesktop doc={DOC_FIXTURE} onClose={onClose} labels={LABELS} />,
      { wrapper: makeWrapper(qc) }
    );
    const titleInput = screen.getByLabelText('Titolo') as HTMLInputElement;
    fireEvent.change(titleInput, { target: { value: 'New Title' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() => expect(spy).toHaveBeenCalled());
    expect(spy).toHaveBeenCalledWith(DOC_FIXTURE.id, expect.objectContaining({ title: 'New Title' }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
    spy.mockRestore();
  });

  // S5: PATCH 404
  it('S5: shows generic notFoundError on 404 (anti-info-leak)', async () => {
    const spy = vi
      .spyOn(api.kbDocs, 'patchKbDocMetadata')
      .mockRejectedValue(new Error('HTTP 404'));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    render(
      <KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />,
      { wrapper: makeWrapper(qc) }
    );
    fireEvent.change(screen.getByLabelText('Titolo'), { target: { value: 'X' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() => expect(screen.getByText('Documento non trovato')).toBeInTheDocument());
    spy.mockRestore();
  });

  // S6: PATCH 422 field errors
  it('S6: shows inline field error on 422 (FluentValidation envelope)', async () => {
    const validationError = Object.assign(new Error('HTTP 422'), {
      status: 422,
      fieldErrors: { title: 'Title must not exceed 200 characters.' },
    });
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockRejectedValue(validationError);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    render(
      <KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />,
      { wrapper: makeWrapper(qc) }
    );
    fireEvent.change(screen.getByLabelText('Titolo'), { target: { value: 'X' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() =>
      expect(screen.getByText(/title must not exceed 200/i)).toBeInTheDocument()
    );
    // aria-describedby links input to error
    expect(screen.getByLabelText('Titolo')).toHaveAttribute('aria-describedby');
    spy.mockRestore();
  });

  it('omits unchanged fields from PATCH body (partial update D-4 #1687)', async () => {
    const spy = vi
      .spyOn(api.kbDocs, 'patchKbDocMetadata')
      .mockResolvedValue(DOC_FIXTURE);
    const qc = new QueryClient();
    render(
      <KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />,
      { wrapper: makeWrapper(qc) }
    );
    // Don't change anything, just click Save.
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() => expect(spy).toHaveBeenCalled());
    const body = spy.mock.calls[0]?.[1];
    expect(body).toEqual({}); // no-op patch
    spy.mockRestore();
  });

  it('cancel button calls onClose without submitting', () => {
    const onClose = vi.fn();
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata');
    const qc = new QueryClient();
    render(
      <KbEditorDesktop doc={DOC_FIXTURE} onClose={onClose} labels={LABELS} />,
      { wrapper: makeWrapper(qc) }
    );
    fireEvent.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onClose).toHaveBeenCalled();
    expect(spy).not.toHaveBeenCalled();
    spy.mockRestore();
  });

  it('a11y: no jest-axe violations', async () => {
    const qc = new QueryClient();
    const { container } = render(
      <KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />,
      { wrapper: makeWrapper(qc) }
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm test apps/web/src/components/features/kb-globale/__tests__/KbEditorDesktop.test.tsx`
Expected: 7 FAIL — module not found.

- [ ] **Step 3: Implement KbEditorDesktop**

Create `apps/web/src/components/features/kb-globale/KbEditorDesktop.tsx`:

```tsx
/**
 * KbEditorDesktop — Phase 3 #1737 / Issue #1482 component #8.
 *
 * Metadata-edit form for owner-curated KB docs. Calls PATCH /api/v1/kb-docs/{id}
 * (BE PR #1732 #1687) with partial update semantics (D-4): null = no-op, ""/[] = clear.
 *
 * Error handling:
 *   - 404 → generic "Documento non trovato" (D-2 anti-info-leak)
 *   - 422 → inline field errors via aria-describedby (FluentValidation envelope)
 *   - other → generic error toast
 *
 * Owner-only affordance enforcement is upstream (DEC-3 — only mounted from
 * KbHomeDesktop recent cards which are BE-filtered to owned docs).
 *
 * Mounted lazy via dynamic({ ssr: false }) by KbGlobaleView (DEC-5 bundle).
 */
'use client';

import { type JSX, useState, useId } from 'react';

import { useUpdateKbDocMeta } from '@/hooks/mutations/useUpdateKbDocMeta';
import type { PatchKbDocMetadataRequest } from '@/lib/api/schemas/kb-docs-patch.schemas';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';

export interface KbEditorDesktopLabels {
  heading: string;
  titleLabel: string;
  documentTypeLabel: string;
  languageLabel: string;
  tagsLabel: string;
  saveLabel: string;
  cancelLabel: string;
  notFoundError: string;
  genericError: string;
  documentTypeOptions: Readonly<Record<string, string>>;
  languageOptions: Readonly<Record<string, string>>;
}

export interface KbEditorDesktopProps {
  doc: UserKbDocDto;
  onClose: () => void;
  labels: KbEditorDesktopLabels;
}

interface FieldError {
  title?: string;
  documentType?: string;
  language?: string;
  tags?: string;
}

interface HttpFieldError extends Error {
  status?: number;
  fieldErrors?: Record<string, string>;
}

function isHttpFieldError(err: unknown): err is HttpFieldError {
  return err instanceof Error && 'status' in err;
}

function buildPatchBody(
  initial: UserKbDocDto,
  draft: { title: string; documentType: string; language: string; tags: string[] }
): PatchKbDocMetadataRequest {
  const body: PatchKbDocMetadataRequest = {};
  if (draft.title !== (initial.title ?? '')) {
    body.title = draft.title; // empty string ⇒ clear (D-4)
  }
  if (draft.documentType !== ((initial as { documentType?: string }).documentType ?? '')) {
    body.documentType = draft.documentType || undefined;
  }
  if (draft.language !== ((initial as { language?: string }).language ?? '')) {
    body.language = draft.language || undefined;
  }
  const initialTags = initial.tags ?? [];
  const tagsChanged =
    draft.tags.length !== initialTags.length ||
    draft.tags.some((t, i) => t !== initialTags[i]);
  if (tagsChanged) {
    body.tags = draft.tags;
  }
  return body;
}

export function KbEditorDesktop(props: KbEditorDesktopProps): JSX.Element {
  const { doc, onClose, labels } = props;
  const titleId = useId();
  const titleErrId = useId();
  const tagsId = useId();
  const tagsErrId = useId();

  const [title, setTitle] = useState(doc.title ?? '');
  const [documentType, setDocumentType] = useState((doc as { documentType?: string }).documentType ?? '');
  const [language, setLanguage] = useState((doc as { language?: string }).language ?? '');
  const [tagsInput, setTagsInput] = useState((doc.tags ?? []).join(', '));
  const [errors, setErrors] = useState<FieldError>({});
  const [topError, setTopError] = useState<string | null>(null);

  const mutation = useUpdateKbDocMeta();

  const docTypeOptions = Object.keys(labels.documentTypeOptions);
  const languageOptions = Object.keys(labels.languageOptions);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    setTopError(null);

    const tagsArray = tagsInput
      .split(',')
      .map(t => t.trim())
      .filter(t => t.length > 0);

    const body = buildPatchBody(doc, {
      title,
      documentType,
      language,
      tags: tagsArray,
    });

    mutation.mutate(
      { id: doc.id, body },
      {
        onSuccess: () => {
          onClose();
        },
        onError: (err: unknown) => {
          if (isHttpFieldError(err)) {
            if (err.status === 404) {
              setTopError(labels.notFoundError);
              return;
            }
            if (err.status === 422 && err.fieldErrors) {
              setErrors(err.fieldErrors as FieldError);
              return;
            }
          }
          setTopError(labels.genericError);
        },
      }
    );
  }

  return (
    <section
      className="bg-card border border-border rounded-lg p-6 flex flex-col gap-4"
      aria-label={labels.heading}
    >
      <h2 className="font-semibold text-foreground">{labels.heading}</h2>
      {topError && (
        <div role="alert" className="text-sm text-destructive">
          {topError}
        </div>
      )}
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <label htmlFor={titleId} className="text-sm font-medium">
            {labels.titleLabel}
          </label>
          <input
            id={titleId}
            type="text"
            value={title}
            onChange={e => setTitle(e.target.value)}
            aria-describedby={errors.title ? titleErrId : undefined}
            aria-invalid={errors.title ? true : undefined}
            className="border border-border rounded px-2 py-1"
          />
          {errors.title && (
            <p id={titleErrId} className="text-sm text-destructive">
              {errors.title}
            </p>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium" htmlFor={`${titleId}-doctype`}>
            {labels.documentTypeLabel}
          </label>
          <select
            id={`${titleId}-doctype`}
            value={documentType}
            onChange={e => setDocumentType(e.target.value)}
            className="border border-border rounded px-2 py-1"
          >
            <option value="">—</option>
            {docTypeOptions.map(opt => (
              <option key={opt} value={opt}>
                {labels.documentTypeOptions[opt]}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium" htmlFor={`${titleId}-lang`}>
            {labels.languageLabel}
          </label>
          <select
            id={`${titleId}-lang`}
            value={language}
            onChange={e => setLanguage(e.target.value)}
            className="border border-border rounded px-2 py-1"
          >
            <option value="">—</option>
            {languageOptions.map(opt => (
              <option key={opt} value={opt}>
                {labels.languageOptions[opt]}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor={tagsId} className="text-sm font-medium">
            {labels.tagsLabel}
          </label>
          <input
            id={tagsId}
            type="text"
            value={tagsInput}
            onChange={e => setTagsInput(e.target.value)}
            placeholder="tag1, tag2, …"
            aria-describedby={errors.tags ? tagsErrId : undefined}
            aria-invalid={errors.tags ? true : undefined}
            className="border border-border rounded px-2 py-1"
          />
          {errors.tags && (
            <p id={tagsErrId} className="text-sm text-destructive">
              {errors.tags}
            </p>
          )}
        </div>

        <div className="flex items-center gap-3 mt-2">
          <button
            type="submit"
            disabled={mutation.isPending}
            className="bg-primary text-primary-foreground rounded px-4 py-2 hover:opacity-90 disabled:opacity-50"
          >
            {labels.saveLabel}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="border border-border rounded px-4 py-2 hover:bg-muted"
          >
            {labels.cancelLabel}
          </button>
        </div>
      </form>
    </section>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm test apps/web/src/components/features/kb-globale/__tests__/KbEditorDesktop.test.tsx`
Expected: 7 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/kb-globale/KbEditorDesktop.tsx \
        apps/web/src/components/features/kb-globale/__tests__/KbEditorDesktop.test.tsx
git commit -m "feat(kb-globale): #1737 Task 6 — KbEditorDesktop component (4 fields, partial update, 404/422 handling)"
```

---

## Task 7: Orchestrator wiring (KbGlobaleView + KbSearchResultsDesktop)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx`
- Modify: `apps/web/src/components/features/kb-globale/KbSearchResultsDesktop.tsx`
- Modify: `apps/web/src/components/features/kb-globale/KbHomeDesktop.tsx` (add edit-affordance per DEC-3)
- Test: `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx` (modify, add Phase 3 unit assertions)

- [ ] **Step 1: Write failing tests for orchestrator URL parsing of filters + edit param**

Add to `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx`:

```tsx
describe('KbGlobaleView — Phase 3 (#1737) URL parsing', () => {
  it('passes docType from ?docType=Rulebook,Errata to useGlobalKbSearch filters', () => {
    // Mock useSearchParams to return ?q=azul&docType=Rulebook,Errata
    // Assert: useGlobalKbSearch spy is called with filters: { docType: ['Rulebook','Errata'] }
    // (Use the test pattern already established in this file for searchParams mocking.)
  });

  it('passes gameId from ?game=uuid1,uuid2 to useGlobalKbSearch filters', () => {
    // Similar to above, for gameId.
  });

  it('passes language from ?lang=it to useGlobalKbSearch filters', () => {
    // Similar to above, for language.
  });

  it('lazy-mounts KbEditorDesktop when ?docId=X&edit=1 present and doc is in useUserKbDocs', () => {
    // Mock useUserKbDocs to return [{ id: X, ... }]; mock useSearchParams to return ?docId=X&edit=1
    // Assert: KbEditorDesktop is rendered.
  });

  it('does NOT mount KbEditorDesktop when ?docId=Y&edit=1 but doc Y not in useUserKbDocs (DEC-3)', () => {
    // Mock useUserKbDocs returning [{ id: X, ... }] (not Y); URL ?docId=Y&edit=1
    // Assert: KbEditorDesktop is NOT rendered (no edit affordance for non-owned docs).
  });
});
```

NOTE: implement using the existing mocking pattern in the same test file (mock `next/navigation`'s `useSearchParams` + `useRouter`; the file already does this for Phase 1+2 tests — replicate that style for these 5 new tests).

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm test apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx`
Expected: 5 new FAIL.

- [ ] **Step 3: Extend KbGlobaleView**

Modify `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx`:

A) **Add lazy import** near the existing Phase 2 lazy block (after `DrawerShellLazy`):

```tsx
const KbEditorDesktopLazy = dynamic(
  () =>
    import('@/components/features/kb-globale/KbEditorDesktop').then(m => ({
      default: m.KbEditorDesktop,
    })),
  { ssr: false }
);
```

B) **Extend LABELS** — add two new top-level sections (sketch — full content lives in i18n catalogs Task 8):

```tsx
const LABELS = {
  // ... existing hero, home, results, viewer, drawer ...
  filters: {
    heading: 'Filtri',
    docTypeLabel: 'Tipo documento',
    gameIdLabel: 'Gioco',
    languageLabel: 'Lingua',
    clearAll: 'Cancella filtri',
    docTypeOptions: {
      Rulebook: 'Regolamento',
      Expansion: 'Espansione',
      Errata: 'Errata',
      QuickStart: 'Quick Start',
      Reference: 'Riferimento',
      PlayerAid: 'Aiuto giocatore',
      Other: 'Altro',
    },
    languageOptions: { en: 'English', it: 'Italiano', de: 'Deutsch', fr: 'Français', es: 'Español' },
  },
  editor: {
    heading: 'Modifica metadati',
    titleLabel: 'Titolo',
    documentTypeLabel: 'Tipo documento',
    languageLabel: 'Lingua',
    tagsLabel: 'Tag',
    saveLabel: 'Salva',
    cancelLabel: 'Annulla',
    notFoundError: 'Documento non trovato',
    genericError: 'Errore generico, riprova.',
    documentTypeOptions: {
      Rulebook: 'Regolamento',
      Expansion: 'Espansione',
      Errata: 'Errata',
      QuickStart: 'Quick Start',
      Reference: 'Riferimento',
      PlayerAid: 'Aiuto giocatore',
      Other: 'Altro',
    },
    languageOptions: { en: 'English', it: 'Italiano', de: 'Deutsch', fr: 'Français', es: 'Español' },
  },
};
```

C) **Parse filter URL params** (inside the component body, right after the existing URL state block):

```tsx
// Phase 3 (#1737): URL SSOT for facets.
const docTypeParam = searchParams.get('docType');
const gameParam = searchParams.get('game');
const langParam = searchParams.get('lang');
const editParam = searchParams.get('edit') === '1';

const filters: GlobalKbSearchFilters = useMemo(() => {
  const f: GlobalKbSearchFilters = {};
  if (docTypeParam) f.docType = docTypeParam.split(',').filter(Boolean);
  if (gameParam) f.gameId = gameParam.split(',').filter(Boolean);
  if (langParam) f.language = langParam;
  return f;
}, [docTypeParam, gameParam, langParam]);
```

Add `GlobalKbSearchFilters` to the existing import from `@/lib/api/schemas/kb-globale.schemas`.

D) **Pass filters to useGlobalKbSearch**:

```tsx
const search = useGlobalKbSearch({
  query: q,
  mode,
  enabled: !isHomeBranch,
  filters,
});
```

E) **Add filter URL update helper**:

```tsx
const setFilters = useCallback(
  (next: GlobalKbSearchFilters) => {
    const params = new URLSearchParams(searchParams.toString());
    if (next.docType && next.docType.length > 0) {
      params.set('docType', [...next.docType].join(','));
    } else {
      params.delete('docType');
    }
    if (next.gameId && next.gameId.length > 0) {
      params.set('game', [...next.gameId].join(','));
    } else {
      params.delete('game');
    }
    if (next.language) {
      params.set('lang', next.language);
    } else {
      params.delete('lang');
    }
    const qs = params.toString();
    router.push(qs ? `/knowledge-base/global?${qs}` : '/knowledge-base/global');
  },
  [router, searchParams]
);
```

F) **Derive available games from useUserKbDocs** (DEC-1):

```tsx
const availableGames = useMemo(() => {
  const seen = new Set<string>();
  const games: { id: string; name: string }[] = [];
  for (const doc of recentDocs) {
    if (doc.gameId && doc.gameName && !seen.has(doc.gameId)) {
      seen.add(doc.gameId);
      games.push({ id: doc.gameId, name: doc.gameName });
    }
  }
  return games;
}, [recentDocs]);
```

G) **Compute edit-mount safety** (DEC-3):

```tsx
// Edit affordance: only mount if doc is in useUserKbDocs (BE-filtered to owned).
const editTargetDoc = useMemo(() => {
  if (!editParam || !docIdParam) return null;
  return recentDocs.find(d => d.id === docIdParam) ?? null;
}, [editParam, docIdParam, recentDocs]);

// NOTE: editTargetDoc is the KbDoc projection from useUserKbDocs (lacks title/tags
// fields). KbEditorDesktop expects UserKbDocDto. We pass the raw DTO when available.
const editTargetDto = useMemo<UserKbDocDto | null>(() => {
  const raw = recent.data?.items.find((d: KbDoc) => d.id === docIdParam);
  // recent.data.items are already KbDoc-mapped; need the source DTO.
  // Use the raw response from useUserKbDocs if we can re-derive, otherwise refetch the detail.
  // For simplicity, expose a `rawItems` field in useUserKbDocs (Task 7 sub-fix below).
  return null; // placeholder — real impl below in next sub-step.
}, [docIdParam, recent.data?.items]);
```

WAIT — there's a refactor dependency: `useUserKbDocs` currently maps to `KbDoc` which strips `title/tags/updatedBy`. KbEditorDesktop needs the full `UserKbDocDto`. Two options:

- **A**: Expose `rawItems: UserKbDocDto[]` in `useUserKbDocs` result alongside `items: KbDoc[]`.
- **B**: Fetch the full DTO via `useKbDocDetail(docId)` when entering edit mode.

**Choose A** — additive, no extra fetch, and `useUserKbDocs.data.items` already has the data we need before mapping. Implementation:

H) **Modify `useUserKbDocs.ts`** to expose raw items:

```ts
export interface UseUserKbDocsResult {
  items: KbDoc[];
  rawItems: UserKbDocDto[]; // Phase 3 (#1737) — needed by KbEditorDesktop (full DTO with title/tags)
  total: number;
  page: number;
  pageSize: number;
}
```

In the queryFn:

```ts
return {
  items: response.items.map(toKbDoc),
  rawItems: response.items,
  total: response.total,
  page: response.page,
  pageSize: response.pageSize,
};
```

I) **Use rawItems for editor mount**:

```tsx
const editTargetDto = useMemo<UserKbDocDto | null>(() => {
  if (!editParam || !docIdParam) return null;
  return recent.data?.rawItems.find(d => d.id === docIdParam) ?? null;
}, [editParam, docIdParam, recent.data?.rawItems]);
```

J) **Render FilterAccordion in results branch + KbEditorDesktop conditionally**. Replace the render JSX block:

```tsx
{isHomeBranch ? (
  <KbHomeDesktop
    recentDocs={recentDocs}
    isLoading={recent.isLoading}
    error={(recent.error as Error | null) ?? null}
    labels={LABELS.home}
    onRetry={handleHomeRetry}
    onEditClick={doc => {
      const params = new URLSearchParams(searchParams.toString());
      params.set('docId', doc.id);
      params.set('edit', '1');
      router.push(`/knowledge-base/global?${params.toString()}`);
    }}
  />
) : (
  <div className="grid grid-cols-1 md:grid-cols-[260px_1fr] gap-4">
    <FilterAccordion
      availableGames={availableGames}
      selected={filters}
      onChange={setFilters}
      labels={LABELS.filters}
    />
    <KbSearchResultsDesktop
      query={q}
      results={search.results}
      hasMore={search.hasMore}
      isLoading={search.isLoading}
      isFetchingNextPage={search.isFetchingNextPage}
      error={search.error}
      onLoadMore={search.fetchNextPage}
      labels={LABELS.results}
      onRetry={handleResultsRetry}
      onResultClick={r => openViewer({ docId: r.docId, page: r.pageNumber ?? 1 })}
    />
  </div>
)}

{/* Phase 3: lazy-mount KbEditorDesktop when ?edit=1 + owned doc resolvable */}
{editTargetDto && (
  <KbEditorDesktopLazy
    doc={editTargetDto}
    labels={LABELS.editor}
    onClose={() => {
      const params = new URLSearchParams(searchParams.toString());
      params.delete('edit');
      router.push(`/knowledge-base/global?${params.toString()}`);
    }}
  />
)}
```

K) **Import FilterAccordion + types** at top of file:

```tsx
import { FilterAccordion } from '@/components/features/kb-globale/FilterAccordion';
import type { GlobalKbSearchFilters } from '@/lib/api/schemas/kb-globale.schemas';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';
```

- [ ] **Step 4: Modify KbHomeDesktop to surface edit affordance**

In `apps/web/src/components/features/kb-globale/KbHomeDesktop.tsx`:

1. Add `onEditClick?: (doc: KbDoc) => void` to props
2. In each recent-doc card, if `onEditClick` is provided, add an "Edit" button (icon or label)
3. Update LABELS prop interface to include `editLabel: string`
4. Add corresponding test in `KbHomeDesktop.test.tsx`:

```tsx
it('Phase 3 #1737: renders Edit button per recent doc when onEditClick provided', () => {
  const onEditClick = vi.fn();
  render(
    <KbHomeDesktop
      recentDocs={[/* fixture */]}
      isLoading={false}
      error={null}
      labels={{ ...LABELS, editLabel: 'Modifica' }}
      onRetry={vi.fn()}
      onEditClick={onEditClick}
    />
  );
  fireEvent.click(screen.getByRole('button', { name: /modifica/i }));
  expect(onEditClick).toHaveBeenCalled();
});
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `pnpm test apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx apps/web/src/components/features/kb-globale/__tests__/KbHomeDesktop.test.tsx`
Expected: 5 new KbGlobaleView tests PASS + 1 new KbHomeDesktop test PASS + all pre-existing tests still pass.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/KbGlobaleView.tsx \
        apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx \
        apps/web/src/components/features/kb-globale/KbHomeDesktop.tsx \
        apps/web/src/components/features/kb-globale/__tests__/KbHomeDesktop.test.tsx \
        apps/web/src/hooks/queries/useUserKbDocs.ts
git commit -m "feat(kb-globale): #1737 Task 7 — wire FilterAccordion + KbEditorDesktop into orchestrator"
```

---

## Task 8: MSW integration tests S1-S6 + i18n + matrix update

**Files:**
- Create: `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.phase3.integration.test.tsx`
- Modify: `apps/web/messages/it.json`
- Modify: `apps/web/messages/en.json`
- Modify: `docs/for-developers/frontend/v2-migration-matrix.md`

- [ ] **Step 1: Write 6 MSW integration scenarios S1-S6**

Create `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.phase3.integration.test.tsx`:

```tsx
/**
 * KbGlobaleView Phase 3 integration tests (#1737)
 * — MSW-mocked scenarios S1-S6 from spec-panel decisions
 *
 * S1: Facet apply — selecting docType refetches with filter, results filtered
 * S2: Facet clear — clearAll resets URL + refetch baseline
 * S3: URL SSOT round-trip — initial URL with filters pre-selects FilterAccordion
 * S4: PATCH success — Edit→change title→Save → 200 → cache invalidates → UI updates
 * S5: PATCH 404 — generic "Documento non trovato" message (anti-info-leak)
 * S6: PATCH 422 — FluentValidation envelope → inline field error with aria-describedby
 */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { setupServer } from 'msw/node';
import { http, HttpResponse } from 'msw';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { KbGlobaleView } from '../KbGlobaleView';
// Import the test-only helper from existing integration test pattern if available;
// otherwise replicate it inline (look at Phase 1+2 integration test file for the
// pattern that mocks next/navigation searchParams/router via testing-library-vitest's
// vi.mock helpers).

// === Fixtures ===
const DOC_FIXTURE = {
  id: '00000000-0000-0000-0000-000000000001',
  gameId: '00000000-0000-0000-0000-000000000010',
  gameName: 'Azul',
  fileName: 'azul.pdf',
  processingState: 'Ready' as const,
  pageCount: 24,
  processedAt: '2026-05-31T00:00:00Z',
  uploadedAt: '2026-05-31T00:00:00Z',
  updatedAt: '2026-05-31T00:00:00Z',
  title: 'Old Title',
  tags: ['family'],
  updatedBy: null,
};

const SEARCH_RESULT_RULEBOOK = {
  chunkId: 'c1', docId: DOC_FIXTURE.id, docTitle: 'Azul Rules',
  gameId: DOC_FIXTURE.gameId!, gameName: 'Azul', docType: 'Rulebook',
  headingPath: 'Setup', snippet: 'Place tiles...', pageNumber: 3, score: 0.9,
};
const SEARCH_RESULT_ERRATA = {
  ...SEARCH_RESULT_RULEBOOK, chunkId: 'c2', docType: 'Errata', snippet: 'Errata correction...',
};

const server = setupServer(
  http.get('/api/v1/kb-docs', () =>
    HttpResponse.json({ items: [DOC_FIXTURE], total: 1, page: 1, pageSize: 20 })
  ),
  http.post('/api/v1/knowledge-base/search/global', async ({ request }) => {
    const body = (await request.json()) as { docType?: string[] };
    if (body.docType?.includes('Rulebook') && !body.docType?.includes('Errata')) {
      return HttpResponse.json({
        results: [SEARCH_RESULT_RULEBOOK], hasMore: false, nextCursor: null,
      });
    }
    return HttpResponse.json({
      results: [SEARCH_RESULT_RULEBOOK, SEARCH_RESULT_ERRATA],
      hasMore: false, nextCursor: null,
    });
  }),
  http.patch('/api/v1/kb-docs/:id', async ({ params, request }) => {
    const body = (await request.json()) as { title?: string };
    if (body.title === 'TRIGGER_404') return new HttpResponse(null, { status: 404 });
    if (body.title === 'TRIGGER_422') {
      return HttpResponse.json(
        { errors: { title: 'Title must not exceed 200 characters.' } },
        { status: 422 }
      );
    }
    return HttpResponse.json({ ...DOC_FIXTURE, title: body.title ?? DOC_FIXTURE.title });
  })
);

beforeEach(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.close());

function renderWithUrl(searchParamsString: string) {
  // Use the test helper that mocks next/navigation per the established pattern.
  // ... (replicate from KbGlobaleView.integration.test.tsx Phase 1+2)
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <KbGlobaleView />
    </QueryClientProvider>
  );
}

describe('KbGlobaleView Phase 3 — S1-S6 (#1737)', () => {
  it('S1: docType filter applied refetches with new params', async () => {
    renderWithUrl('?q=azul');
    await waitFor(() => expect(screen.getByText(/Azul Rules/i)).toBeInTheDocument());
    // Find FilterAccordion section, expand DocType, click Rulebook
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    fireEvent.click(screen.getByLabelText(/regolamento/i));
    // After refetch, only Rulebook result visible (Errata snippet absent).
    await waitFor(() => expect(screen.queryByText(/errata correction/i)).not.toBeInTheDocument());
  });

  it('S2: clearAll resets all facets and refetches baseline', async () => {
    renderWithUrl('?q=azul&docType=Rulebook');
    // Wait for filtered result.
    await waitFor(() => expect(screen.getByText(/Azul Rules/i)).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /cancella filtri/i }));
    // After clear, both results visible.
    await waitFor(() => expect(screen.getByText(/errata correction/i)).toBeInTheDocument());
  });

  it('S3: URL ?docType=Rulebook&lang=it pre-selects the FilterAccordion checkboxes', async () => {
    renderWithUrl('?q=azul&docType=Rulebook&lang=it');
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    const rulebookCheckbox = screen.getByLabelText(/regolamento/i) as HTMLInputElement;
    expect(rulebookCheckbox.checked).toBe(true);
    fireEvent.click(screen.getByRole('button', { name: /lingua/i }));
    const italianRadio = screen.getByLabelText(/italiano/i) as HTMLInputElement;
    expect(italianRadio.checked).toBe(true);
  });

  it('S4: PATCH success → cache invalidates → UI shows updated title', async () => {
    renderWithUrl('?docId=00000000-0000-0000-0000-000000000001&edit=1');
    await waitFor(() => expect(screen.getByLabelText(/titolo/i)).toBeInTheDocument());
    fireEvent.change(screen.getByLabelText(/titolo/i), { target: { value: 'Brand New Title' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    // Editor closes (onClose called); KbHomeDesktop refetches → shows new title.
    await waitFor(() =>
      expect(screen.queryByText(/modifica metadati/i)).not.toBeInTheDocument()
    );
  });

  it('S5: PATCH 404 → generic notFoundError message (anti-info-leak)', async () => {
    renderWithUrl('?docId=00000000-0000-0000-0000-000000000001&edit=1');
    await waitFor(() => expect(screen.getByLabelText(/titolo/i)).toBeInTheDocument());
    fireEvent.change(screen.getByLabelText(/titolo/i), { target: { value: 'TRIGGER_404' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() =>
      expect(screen.getByText(/documento non trovato/i)).toBeInTheDocument()
    );
    // Verify NO "non autorizzato" / "forbidden" text (anti-info-leak).
    expect(screen.queryByText(/non autorizzato|forbidden/i)).not.toBeInTheDocument();
  });

  it('S6: PATCH 422 → inline field error with aria-describedby', async () => {
    renderWithUrl('?docId=00000000-0000-0000-0000-000000000001&edit=1');
    await waitFor(() => expect(screen.getByLabelText(/titolo/i)).toBeInTheDocument());
    fireEvent.change(screen.getByLabelText(/titolo/i), { target: { value: 'TRIGGER_422' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() =>
      expect(screen.getByText(/title must not exceed 200/i)).toBeInTheDocument()
    );
    expect(screen.getByLabelText(/titolo/i)).toHaveAttribute('aria-describedby');
  });
});
```

NOTE: the test uses an `errors` envelope shape `{ errors: { title: '...' } }` for 422 — the FE error parser (in `useUpdateKbDocMeta` or a shared HTTP error handler) must extract this into `HttpFieldError.fieldErrors`. If the existing httpClient doesn't do this automatically, add the extraction logic when the subagent encounters the test failure. The PR body for #1732 should document the exact 422 envelope shape.

- [ ] **Step 2: Run integration tests to verify they fail**

Run: `pnpm test apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/__tests__/KbGlobaleView.phase3.integration.test.tsx`
Expected: 6 FAIL (mix of missing UI, missing 422 envelope parsing, etc.)

- [ ] **Step 3: Add 422 envelope parsing if not already present**

Verify `apps/web/src/lib/api/core/httpClient.ts` parses 422 responses into a typed error with `status` + `fieldErrors`. If not, extend the error path to convert `{ errors: Record<string, string> }` → an `HttpFieldError` with `status: 422, fieldErrors: errors`. Then the existing `isHttpFieldError` check in `KbEditorDesktop` works.

- [ ] **Step 4: Run integration tests until all 6 pass**

Iterate until 6 PASS.

- [ ] **Step 5: Add i18n catalogs**

Modify `apps/web/messages/it.json`:

```json
{
  "kbGlobale": {
    "filters": {
      "heading": "Filtri",
      "docTypeLabel": "Tipo documento",
      "gameIdLabel": "Gioco",
      "languageLabel": "Lingua",
      "clearAll": "Cancella filtri",
      "docTypeOptions": {
        "Rulebook": "Regolamento",
        "Expansion": "Espansione",
        "Errata": "Errata",
        "QuickStart": "Quick Start",
        "Reference": "Riferimento",
        "PlayerAid": "Aiuto giocatore",
        "Other": "Altro"
      },
      "languageOptions": {
        "en": "English",
        "it": "Italiano",
        "de": "Deutsch",
        "fr": "Français",
        "es": "Español"
      }
    },
    "editor": {
      "heading": "Modifica metadati",
      "titleLabel": "Titolo",
      "documentTypeLabel": "Tipo documento",
      "languageLabel": "Lingua",
      "tagsLabel": "Tag",
      "saveLabel": "Salva",
      "cancelLabel": "Annulla",
      "notFoundError": "Documento non trovato",
      "genericError": "Errore generico, riprova."
    }
  }
}
```

Modify `apps/web/messages/en.json` with English mirror (translate each label).

NOTE: KbGlobaleView LABELS const may currently hold strings inline (Phase 1+2 style "Task 10 deferred"). If the codebase has migrated to MESSAGES catalog via `useTranslations()`, update the orchestrator to read from `useTranslations('kbGlobale')` for the new sections. Inspect the existing pattern in the file before applying.

- [ ] **Step 6: Update v2-migration-matrix.md**

Modify `docs/for-developers/frontend/v2-migration-matrix.md`:

Find the `kb-globale` section. Flip the rows for:
- Component #4 `FilterAccordion` — `pending` → `done`, add `PR #<this-PR>` in PR column
- Component #8 `KbEditorDesktop` — `pending` → `done`, add `PR #<this-PR>` in PR column

(After PR open, replace `<this-PR>` with the actual PR number.)

- [ ] **Step 7: Run full test suite + quality gates**

Run all in sequence (must all pass):

```bash
pnpm test
pnpm typecheck
pnpm lint
pnpm lint:tokens
```

Expected: PASS PASS PASS PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/__tests__/KbGlobaleView.phase3.integration.test.tsx \
        apps/web/messages/it.json \
        apps/web/messages/en.json \
        docs/for-developers/frontend/v2-migration-matrix.md \
        apps/web/src/lib/api/core/httpClient.ts  # if 422 parsing was extended
git commit -m "feat(kb-globale): #1737 Task 8 — S1-S6 MSW integration + i18n + matrix"
```

---

## Final checklist (before opening PR)

- [ ] All 8 tasks committed
- [ ] `git log --oneline main-dev..HEAD` shows 8 commits prefixed `feat(kb-globale): #1737 Task N`
- [ ] `pnpm test` PASS (~60 new tests + 0 regression in pre-existing)
- [ ] `pnpm typecheck` exit 0
- [ ] `pnpm lint` exit 0
- [ ] `pnpm lint:tokens` exit 0
- [ ] Bundle delta verified ≤ +30 KB (check `.next` analyze if needed)
- [ ] Matrix updated with PR number after `gh pr create`

## Decisions reference (in case of mid-impl ambiguity)

- DEC-1: GameId facet = derived from `useUserKbDocs` (Task 7 step F)
- DEC-2: Aggressive cache invalidation (Task 4 step 3 onSuccess)
- DEC-3: Edit affordance only in KbHomeDesktop (Task 7 step 4 + step 3 G/I)
- DEC-4: G/W/T scenarios S1-S6 (Task 8 step 1)
- DEC-5: FilterAccordion eager (Task 7 import), KbEditorDesktop lazy (Task 7 step 3 A)
- DEC-6: 6 MSW + jest-axe per component + cache invalidation test (Task 4-6-8)
- DEC-7: `docType: z.string()` raw, drop faq labels (Task 1 + Task 5 DOC_TYPE_ALLOWLIST)
