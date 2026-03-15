import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useHoloVisibility } from '../useHoloVisibility';

describe('useHoloVisibility', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'IntersectionObserver',
      vi.fn().mockImplementation(() => ({
        observe: vi.fn(),
        unobserve: vi.fn(),
        disconnect: vi.fn(),
      }))
    );
  });

  it('returns a ref and isVisible boolean', () => {
    const { result } = renderHook(() => useHoloVisibility());
    expect(result.current.ref).toBeDefined();
    expect(typeof result.current.isVisible).toBe('boolean');
  });

  it('defaults to not visible', () => {
    const { result } = renderHook(() => useHoloVisibility());
    expect(result.current.isVisible).toBe(false);
  });
});
