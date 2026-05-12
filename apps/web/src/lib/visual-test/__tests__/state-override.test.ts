/**
 * @vitest-environment jsdom
 *
 * Tests for WS-D Foundation `state-override.ts` helper.
 * Refs #1070 (umbrella #1066).
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useSearchParams } from 'next/navigation';

// Hoisted mock for next/navigation. Tests configure return value per-case via
// `mockUseSearchParams.mockReturnValue(...)`. This is the recommended pattern
// for top-level ESM imports in vitest — `vi.doMock` in beforeEach does not
// retroactively replace the bound `useSearchParams` reference inside
// state-override.ts after its first module load.
vi.mock('next/navigation', () => ({
  useSearchParams: vi.fn(),
}));

const mockUseSearchParams = vi.mocked(useSearchParams);

describe('readStateOverride', () => {
  const ORIGINAL_ENV = process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;

  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    if (ORIGINAL_ENV === undefined) delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    else process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = ORIGINAL_ENV;
  });

  it('returns null when IS_VISUAL_TEST_BUILD=false (production)', async () => {
    delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    const { readStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    expect(readStateOverride(params)).toBeNull();
  });

  it('returns ?state value when IS_VISUAL_TEST_BUILD=true', async () => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
    const { readStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    expect(readStateOverride(params)).toBe('loading');
  });

  it('returns null when searchParams is null', async () => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
    const { readStateOverride } = await import('../state-override');
    expect(readStateOverride(null)).toBeNull();
  });

  it('returns null when searchParams lacks state key', async () => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
    const { readStateOverride } = await import('../state-override');
    const params = new URLSearchParams('other=foo');
    expect(readStateOverride(params)).toBeNull();
  });
});

describe('readTypedStateOverride', () => {
  const ORIGINAL_ENV = process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;

  beforeEach(() => {
    vi.resetModules();
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
  });

  afterEach(() => {
    if (ORIGINAL_ENV === undefined) delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    else process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = ORIGINAL_ENV;
  });

  it('returns typed value when state in allowedStates', async () => {
    const { readTypedStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    const result = readTypedStateOverride(params, ['default', 'loading', 'error'] as const);
    expect(result).toBe('loading');
  });

  it('returns null when state value not in allowedStates', async () => {
    const { readTypedStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=bogus');
    const result = readTypedStateOverride(params, ['default', 'loading'] as const);
    expect(result).toBeNull();
  });

  it('returns null when allowedStates is empty', async () => {
    const { readTypedStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    const result = readTypedStateOverride(params, []);
    expect(result).toBeNull();
  });

  it('returns null when IS_VISUAL_TEST_BUILD=false even with valid state', async () => {
    delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    const { readTypedStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    const result = readTypedStateOverride(params, ['default', 'loading'] as const);
    expect(result).toBeNull();
  });
});

describe('useStateOverride', () => {
  const ORIGINAL_ENV = process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;

  beforeEach(() => {
    vi.resetModules();
    mockUseSearchParams.mockReset();
  });

  afterEach(() => {
    if (ORIGINAL_ENV === undefined) delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    else process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = ORIGINAL_ENV;
  });

  it('returns null in production regardless of route params', async () => {
    delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    mockUseSearchParams.mockReturnValue(
      new URLSearchParams('state=loading') as unknown as ReturnType<typeof useSearchParams>
    );
    const { useStateOverride } = await import('../state-override');
    const { result } = renderHook(() => useStateOverride(['default', 'loading'] as const));
    expect(result.current).toBeNull();
  });

  it('returns typed value in visual-test mode', async () => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
    mockUseSearchParams.mockReturnValue(
      new URLSearchParams('state=loading') as unknown as ReturnType<typeof useSearchParams>
    );
    const { useStateOverride } = await import('../state-override');
    const { result } = renderHook(() => useStateOverride(['default', 'loading'] as const));
    expect(result.current).toBe('loading');
  });
});
