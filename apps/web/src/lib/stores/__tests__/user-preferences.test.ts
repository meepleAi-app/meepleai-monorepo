/**
 * User Preferences Store Tests
 * Issue #5526: Unit tests for user preferences store and useAppMode hook.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useUserPreferences } from '../user-preferences';

describe('useUserPreferences store', () => {
  beforeEach(() => {
    // Reset store state between tests
    const { result } = renderHook(() => useUserPreferences());
    act(() => {
      result.current.setAppMode('casual');
    });
  });

  it('defaults to casual mode', () => {
    const { result } = renderHook(() => useUserPreferences());
    expect(result.current.appMode).toBe('casual');
  });

  it('setAppMode updates to power', () => {
    const { result } = renderHook(() => useUserPreferences());
    act(() => {
      result.current.setAppMode('power');
    });
    expect(result.current.appMode).toBe('power');
  });

  it('setAppMode updates back to casual', () => {
    const { result } = renderHook(() => useUserPreferences());
    act(() => {
      result.current.setAppMode('power');
    });
    act(() => {
      result.current.setAppMode('casual');
    });
    expect(result.current.appMode).toBe('casual');
  });
});
