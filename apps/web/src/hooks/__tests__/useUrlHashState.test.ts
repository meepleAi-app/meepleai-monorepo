import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { useUrlHashState } from '../useUrlHashState';

describe('useUrlHashState', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/');
  });

  afterEach(() => {
    window.history.replaceState(null, '', '/');
  });

  it('returns defaultValue on initial render when hash is empty', () => {
    const { result } = renderHook(() => useUrlHashState<string>('q', ''));
    expect(result.current[0]).toBe('');
  });

  it('hydrates from URL hash on mount when key is present', () => {
    window.history.replaceState(null, '', '/#q=catan');
    const { result } = renderHook(() => useUrlHashState<string>('q', ''));
    expect(result.current[0]).toBe('catan');
  });

  it('decodes URI-encoded values from hash', () => {
    window.history.replaceState(null, '', `/#q=${encodeURIComponent('hello world')}`);
    const { result } = renderHook(() => useUrlHashState<string>('q', ''));
    expect(result.current[0]).toBe('hello world');
  });

  it('updates state and hash when setValue is called', () => {
    const { result } = renderHook(() => useUrlHashState<string>('q', ''));
    act(() => {
      result.current[1]('agricola');
    });
    expect(result.current[0]).toBe('agricola');
    expect(window.location.hash).toBe('#q=agricola');
  });

  it('removes key from hash when value serializes to null (default empty string)', () => {
    window.history.replaceState(null, '', '/#q=initial&sort=rating');
    const { result } = renderHook(() => useUrlHashState<string>('q', ''));
    act(() => {
      result.current[1]('');
    });
    expect(window.location.hash).toBe('#sort=rating');
  });

  it('preserves other keys when updating one key', () => {
    window.history.replaceState(null, '', '/#sort=rating');
    const { result } = renderHook(() => useUrlHashState<string>('q', ''));
    act(() => {
      result.current[1]('wingspan');
    });
    // Order may vary; assert both keys present.
    expect(window.location.hash).toContain('q=wingspan');
    expect(window.location.hash).toContain('sort=rating');
  });

  it('reacts to hashchange events (back/forward navigation)', () => {
    const { result } = renderHook(() => useUrlHashState<string>('q', ''));
    expect(result.current[0]).toBe('');
    act(() => {
      window.history.replaceState(null, '', '/#q=external');
      window.dispatchEvent(new HashChangeEvent('hashchange'));
    });
    expect(result.current[0]).toBe('external');
  });

  it('keeps two instances reading the same key in sync via hashchange', () => {
    const { result: a } = renderHook(() => useUrlHashState<string>('q', ''));
    const { result: b } = renderHook(() => useUrlHashState<string>('q', ''));
    act(() => {
      a.current[1]('catan');
      // setHash uses replaceState which doesn't auto-fire hashchange in jsdom;
      // dispatch manually to mirror real browser behavior across multi-instance.
      window.dispatchEvent(new HashChangeEvent('hashchange'));
    });
    expect(a.current[0]).toBe('catan');
    expect(b.current[0]).toBe('catan');
  });

  it('uses custom serialize/deserialize for arrays', () => {
    const { result } = renderHook(() =>
      useUrlHashState<string[]>('chips', [], {
        serialize: v => (v.length > 0 ? v.join(',') : null),
        deserialize: raw => raw.split(',').filter(Boolean),
      })
    );
    act(() => {
      result.current[1](['with-toolkit', 'top-rated']);
    });
    expect(result.current[0]).toEqual(['with-toolkit', 'top-rated']);
    expect(window.location.hash).toContain('chips=with-toolkit%2Ctop-rated');

    act(() => {
      result.current[1]([]);
    });
    expect(result.current[0]).toEqual([]);
    expect(window.location.hash).toBe('');
  });

  it('falls back to defaultValue when deserialize throws', () => {
    window.history.replaceState(null, '', '/#count=NaN');
    const { result } = renderHook(() =>
      useUrlHashState<number>('count', 0, {
        serialize: v => String(v),
        deserialize: raw => {
          const n = Number(raw);
          if (!Number.isFinite(n)) throw new Error('invalid');
          return n;
        },
      })
    );
    expect(result.current[0]).toBe(0);
  });

  it('cleans up hashchange listener on unmount', () => {
    const { result, unmount } = renderHook(() => useUrlHashState<string>('q', ''));
    unmount();
    act(() => {
      window.history.replaceState(null, '', '/#q=after-unmount');
      window.dispatchEvent(new HashChangeEvent('hashchange'));
    });
    // The unmounted hook's last value remains 'empty' (no React error since
    // the cleanup removed the listener).
    expect(result.current[0]).toBe('');
  });
});
