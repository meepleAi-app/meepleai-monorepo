/**
 * useStatePreview — unit tests.
 *
 * Asse B WP5 T5 (Issue #1897). Validates DEC-4 prod-guard behavior + dev hook.
 */

import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ReactNode } from 'react';

import { StatePreviewProvider } from '../state-preview-provider';
import { useStatePreview } from '../use-state-preview';

function makeWrapper() {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <StatePreviewProvider>{children}</StatePreviewProvider>;
  };
}

describe('useStatePreview (dev mode)', () => {
  beforeEach(() => {
    vi.stubEnv('NODE_ENV', 'development');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('returns "default" when no state has been set for pageId', () => {
    const { result } = renderHook(() => useStatePreview('library'), {
      wrapper: makeWrapper(),
    });
    expect(result.current.state).toBe('default');
  });

  it('setStateFor updates state and reflects in subsequent reads', () => {
    const { result, rerender } = renderHook(() => useStatePreview('library'), {
      wrapper: makeWrapper(),
    });

    act(() => {
      result.current.setStateFor('library', 'empty');
    });
    rerender();

    expect(result.current.state).toBe('empty');
  });

  it('tracks multiple pageIds independently', () => {
    const { result: libraryHook } = renderHook(() => useStatePreview('library'), {
      wrapper: makeWrapper(),
    });
    // Same provider tree via single wrapper instance — share state across hooks
    const wrapper = makeWrapper();
    const libRender = renderHook(() => useStatePreview('library'), { wrapper });
    const gamesRender = renderHook(() => useStatePreview('games'), { wrapper });

    act(() => {
      libRender.result.current.setStateFor('library', 'loading');
    });
    libRender.rerender();
    gamesRender.rerender();

    expect(libRender.result.current.state).toBe('loading');
    expect(gamesRender.result.current.state).toBe('default');
    // first hook left untouched: still default (uses different provider tree)
    expect(libraryHook.current.state).toBe('default');
  });

  it('works outside provider tree — returns default context safely', () => {
    const { result } = renderHook(() => useStatePreview('orphan'));
    expect(result.current.state).toBe('default');
    // setStateFor is the no-op default; calling it must not throw
    expect(() => result.current.setStateFor('orphan', 'error')).not.toThrow();
  });
});

describe('useStatePreview (production mode — DEC-4 prod-guard)', () => {
  beforeEach(() => {
    vi.stubEnv('NODE_ENV', 'production');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('always returns "default" even after setStateFor — hook short-circuits', () => {
    const { result, rerender } = renderHook(() => useStatePreview('library'), {
      wrapper: makeWrapper(),
    });

    act(() => {
      result.current.setStateFor('library', 'error');
    });
    rerender();

    expect(result.current.state).toBe('default');
  });

  it('setStateFor is a no-op in production', () => {
    const { result } = renderHook(() => useStatePreview('library'), {
      wrapper: makeWrapper(),
    });
    // Must not throw, must do nothing observable
    expect(() => result.current.setStateFor('library', 'offline')).not.toThrow();
    expect(result.current.state).toBe('default');
  });
});
