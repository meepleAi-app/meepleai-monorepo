/**
 * Shared type for mocking `@/lib/api/client` in unit tests.
 *
 * Pre-existing tests boilerplate looked like:
 *
 *   vi.mock('@/lib/api/client', () => ({
 *     apiClient: { get: vi.fn() },
 *   }));
 *
 *   import { apiClient } from '@/lib/api/client';
 *   const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
 *
 * Two problems with that:
 *   1. Untyped cast on every method access (`as ReturnType<typeof vi.fn>`).
 *   2. Each test enumerates only the verbs it happens to need, so adding
 *      a `.post(...)` call site to the hook requires updating the mock
 *      factory in the test too.
 *
 * Note on Vitest hoisting: `vi.mock` is hoisted ABOVE all static imports,
 * so its factory cannot reference imported identifiers (would throw
 * `Cannot access __vi_import_X__ before initialization`). `vi.hoisted`
 * has the same constraint — its body cannot reference imports. That's
 * why the supported pattern centralizes only the TYPE here and keeps the
 * mock-instance construction inline in each test file.
 *
 * Canonical usage:
 *
 *   import { vi } from 'vitest';
 *   import type { MockedApiClient } from '@/test-utils/api-client-mock';
 *
 *   const mockApi = vi.hoisted<MockedApiClient>(() => ({
 *     get: vi.fn(),
 *     post: vi.fn(),
 *     put: vi.fn(),
 *     patch: vi.fn(),
 *     delete: vi.fn(),
 *     head: vi.fn(),
 *     options: vi.fn(),
 *   }));
 *   vi.mock('@/lib/api/client', () => ({ apiClient: mockApi }));
 *
 *   beforeEach(() => {
 *     vi.clearAllMocks();
 *     mockApi.get.mockResolvedValue(SAMPLE);
 *   });
 *
 *   it('...', () => {
 *     expect(mockApi.get).toHaveBeenCalledWith('/api/v1/foo', expect.anything());
 *   });
 *
 * Benefits vs the prior boilerplate:
 *   - Strong typing end-to-end (no `as ReturnType<typeof vi.fn>` casts).
 *   - All 7 HTTP verbs pre-mocked, so adding a verb call site to the
 *     production hook doesn't require touching the mock factory.
 *   - `mockApi.get` is a typed `Mock` instance directly (callable as
 *     `mockApi.get.mockResolvedValue(...)` without intermediate variables).
 *
 * Refs:
 *   - PR #1229 / PR #1230 follow-up #2 (review of #1229 surfaced the
 *     mock-boilerplate gap across 6 test files).
 *   - Vitest docs on hoisting: https://vitest.dev/api/vi.html#vi-hoisted
 */

import type { Mock } from 'vitest';

/**
 * Strongly-typed shape of the mocked `apiClient` instance. Each HTTP verb
 * is a `vi.fn()` Mock callable as the production method, e.g.
 * `mockApi.get.mockResolvedValue(...)`.
 *
 * The 7 verbs mirror `HttpClient` in `apps/web/src/lib/api/core/httpClient.ts`.
 */
export type MockedApiClient = {
  readonly get: Mock;
  readonly post: Mock;
  readonly put: Mock;
  readonly patch: Mock;
  readonly delete: Mock;
  readonly head: Mock;
  readonly options: Mock;
};
