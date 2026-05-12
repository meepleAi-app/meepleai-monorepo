/**
 * WS-D Foundation: shared `?state=` URL override helper for visual regression tests.
 *
 * Gated by NEXT_PUBLIC_VISUAL_TEST_BUILD env flag — production builds eliminate
 * all bodies via terser dead-code-elimination (bundle delta: 0 KB).
 *
 * Three-layer API:
 *   - readStateOverride: standalone, no React (fast unit tests)
 *   - readTypedStateOverride: standalone + type-safety
 *   - useStateOverride: React hook for component consumers
 *
 * Spec: docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md
 * Issue: #1070 (umbrella #1066)
 */

/** Compile-time flag set by Next.js build via env var. */
export const IS_VISUAL_TEST_BUILD: boolean = process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD === '1';

/**
 * Read `?state=` URL param. Returns null in production regardless of URL contents.
 */
export function readStateOverride(searchParams: URLSearchParams | null): string | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (!searchParams) return null;
  return searchParams.get('state');
}

/**
 * Type-safe variant. Validates raw value is one of `allowedStates`.
 * Returns null when:
 *  - IS_VISUAL_TEST_BUILD=false
 *  - searchParams is null
 *  - state param missing
 *  - state value not in allowedStates
 */
export function readTypedStateOverride<S extends string>(
  searchParams: URLSearchParams | null,
  allowedStates: readonly S[]
): S | null {
  const raw = readStateOverride(searchParams);
  if (raw === null) return null;
  if (!allowedStates.includes(raw as S)) return null;
  return raw as S;
}

import { useSearchParams } from 'next/navigation';

/**
 * React hook variant. Reads `?state=` from Next.js `useSearchParams()`.
 *
 * In production (IS_VISUAL_TEST_BUILD=false), the hook still runs (Rules of
 * Hooks require unconditional calls) but the body returns null early so the
 * `?state=` value is ignored. `next/navigation` is already in the bundle for
 * other routes, so the import adds no incremental cost.
 *
 * @example
 *   const STATES = ['default', 'loading', 'error'] as const;
 *   const state = useStateOverride(STATES); // 'default' | 'loading' | 'error' | null
 */
export function useStateOverride<S extends string>(allowedStates: readonly S[]): S | null {
  const params = useSearchParams() as URLSearchParams | null;
  // Rules of Hooks: useSearchParams must be called unconditionally.
  // Production gate applies AFTER the hook invocation.
  if (!IS_VISUAL_TEST_BUILD) return null;
  return readTypedStateOverride(params, allowedStates);
}
