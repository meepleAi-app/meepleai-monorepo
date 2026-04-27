import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useFaqHashQuery } from '../useFaqHashQuery';

describe('useFaqHashQuery', () => {
  beforeEach(() => {
    // Reset hash + URL between tests.
    window.history.replaceState(null, '', '/');
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('initial query is empty when no hash present', () => {
    const { result } = renderHook(() => useFaqHashQuery());
    expect(result.current.query).toBe('');
  });

  it('hydrates initial query from #q= hash on mount', () => {
    window.history.replaceState(null, '', '/#q=password');
    const { result } = renderHook(() => useFaqHashQuery());
    expect(result.current.query).toBe('password');
  });

  it('decodes URI-encoded query from hash', () => {
    window.history.replaceState(null, '', `/#q=${encodeURIComponent('hello world')}`);
    const { result } = renderHook(() => useFaqHashQuery());
    expect(result.current.query).toBe('hello world');
  });

  it('returns empty string when decodeURIComponent throws', () => {
    // Malformed % sequence triggers URIError in decodeURIComponent.
    window.history.replaceState(null, '', '/#q=%E0%A4%A');
    const { result } = renderHook(() => useFaqHashQuery());
    expect(result.current.query).toBe('');
  });

  it('updates query immediately on setQuery', () => {
    const { result } = renderHook(() => useFaqHashQuery());
    act(() => {
      result.current.setQuery('install');
    });
    expect(result.current.query).toBe('install');
  });

  it('debounces hash write by 250ms', () => {
    const { result } = renderHook(() => useFaqHashQuery());
    act(() => {
      result.current.setQuery('install');
    });
    // Immediately, hash not yet written.
    expect(window.location.hash).toBe('');
    act(() => {
      vi.advanceTimersByTime(249);
    });
    expect(window.location.hash).toBe('');
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(window.location.hash).toBe(`#q=${encodeURIComponent('install')}`);
  });

  it('coalesces rapid setQuery calls into a single hash write', () => {
    const replaceSpy = vi.spyOn(window.history, 'replaceState');
    const { result } = renderHook(() => useFaqHashQuery());
    act(() => {
      result.current.setQuery('a');
      result.current.setQuery('ab');
      result.current.setQuery('abc');
    });
    act(() => {
      vi.advanceTimersByTime(250);
    });
    expect(replaceSpy).toHaveBeenCalledTimes(1);
    expect(window.location.hash).toBe(`#q=${encodeURIComponent('abc')}`);
    replaceSpy.mockRestore();
  });

  it('strips the hash when query is empty', () => {
    window.history.replaceState(null, '', '/#q=password');
    const { result } = renderHook(() => useFaqHashQuery());
    expect(result.current.query).toBe('password');
    act(() => {
      result.current.setQuery('');
    });
    act(() => {
      vi.advanceTimersByTime(250);
    });
    expect(window.location.hash).toBe('');
  });

  it('strips hash when query is whitespace-only', () => {
    const { result } = renderHook(() => useFaqHashQuery());
    act(() => {
      result.current.setQuery('   ');
    });
    act(() => {
      vi.advanceTimersByTime(250);
    });
    expect(window.location.hash).toBe('');
  });

  it('encodes special characters when writing hash', () => {
    const { result } = renderHook(() => useFaqHashQuery());
    act(() => {
      result.current.setQuery('hello world');
    });
    act(() => {
      vi.advanceTimersByTime(250);
    });
    expect(window.location.hash).toBe(`#q=${encodeURIComponent('hello world')}`);
  });

  it('reacts to hashchange events (back/forward navigation)', () => {
    const { result } = renderHook(() => useFaqHashQuery());
    expect(result.current.query).toBe('');
    act(() => {
      window.history.replaceState(null, '', '/#q=privacy');
      window.dispatchEvent(new HashChangeEvent('hashchange'));
    });
    expect(result.current.query).toBe('privacy');
  });

  it('clears pending debounce timer on unmount', () => {
    const replaceSpy = vi.spyOn(window.history, 'replaceState');
    const { result, unmount } = renderHook(() => useFaqHashQuery());
    act(() => {
      result.current.setQuery('foo');
    });
    const callsBeforeUnmount = replaceSpy.mock.calls.length;
    unmount();
    act(() => {
      vi.advanceTimersByTime(500);
    });
    // No additional history writes after unmount.
    expect(replaceSpy.mock.calls.length).toBe(callsBeforeUnmount);
    replaceSpy.mockRestore();
  });

  it('removes hashchange listener on unmount', () => {
    const removeSpy = vi.spyOn(window, 'removeEventListener');
    const { unmount } = renderHook(() => useFaqHashQuery());
    unmount();
    expect(removeSpy).toHaveBeenCalledWith('hashchange', expect.any(Function));
    removeSpy.mockRestore();
  });
});
