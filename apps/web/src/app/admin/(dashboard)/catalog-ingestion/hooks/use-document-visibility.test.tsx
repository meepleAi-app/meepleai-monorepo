import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useDocumentVisibility } from './use-document-visibility';

describe('useDocumentVisibility', () => {
  beforeEach(() => {
    Object.defineProperty(document, 'hidden', { value: false, configurable: true });
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns true when document is visible on mount', () => {
    const { result } = renderHook(() => useDocumentVisibility());
    expect(result.current).toBe(true);
  });

  it('returns false when document is hidden on mount', () => {
    Object.defineProperty(document, 'hidden', { value: true, configurable: true });
    const { result } = renderHook(() => useDocumentVisibility());
    expect(result.current).toBe(false);
  });

  it('updates when visibilitychange event fires', () => {
    const { result } = renderHook(() => useDocumentVisibility());
    expect(result.current).toBe(true);

    act(() => {
      Object.defineProperty(document, 'hidden', { value: true, configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
    });
    expect(result.current).toBe(false);

    act(() => {
      Object.defineProperty(document, 'hidden', { value: false, configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
    });
    expect(result.current).toBe(true);
  });

  it('removes listener on unmount', () => {
    const removeSpy = vi.spyOn(document, 'removeEventListener');
    const { unmount } = renderHook(() => useDocumentVisibility());
    unmount();
    expect(removeSpy).toHaveBeenCalledWith('visibilitychange', expect.any(Function));
  });
});
