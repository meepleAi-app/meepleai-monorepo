import { renderHook } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { useMiniNavConfigStore } from '@/lib/stores/mini-nav-config-store';

import { useMiniNavConfig } from '../useMiniNavConfig';

describe('useMiniNavConfig', () => {
  beforeEach(() => {
    useMiniNavConfigStore.getState().clear();
  });

  it('sets config on mount', () => {
    renderHook(() =>
      useMiniNavConfig({
        breadcrumb: 'Home',
        tabs: [{ id: 'a', label: 'A', href: '/' }],
        activeTabId: 'a',
      })
    );
    expect(useMiniNavConfigStore.getState().config?.breadcrumb).toBe('Home');
  });

  it('clears config on unmount', () => {
    const { unmount } = renderHook(() =>
      useMiniNavConfig({
        breadcrumb: 'Home',
        tabs: [{ id: 'a', label: 'A', href: '/' }],
        activeTabId: 'a',
      })
    );
    expect(useMiniNavConfigStore.getState().config).not.toBeNull();
    unmount();
    expect(useMiniNavConfigStore.getState().config).toBeNull();
  });

  it('updates config when props change', () => {
    const { rerender } = renderHook(
      ({ activeTabId }: { activeTabId: string }) =>
        useMiniNavConfig({
          breadcrumb: 'Home',
          tabs: [
            { id: 'a', label: 'A', href: '/a' },
            { id: 'b', label: 'B', href: '/b' },
          ],
          activeTabId,
        }),
      { initialProps: { activeTabId: 'a' } }
    );
    expect(useMiniNavConfigStore.getState().config?.activeTabId).toBe('a');
    rerender({ activeTabId: 'b' });
    expect(useMiniNavConfigStore.getState().config?.activeTabId).toBe('b');
  });

  it('does not re-trigger setConfig when rerendered with equivalent inline config', () => {
    const setConfigSpy = vi.spyOn(useMiniNavConfigStore.getState(), 'setConfig');

    const { rerender } = renderHook(() =>
      useMiniNavConfig({
        breadcrumb: 'Home',
        tabs: [{ id: 'a', label: 'A', href: '/' }],
        activeTabId: 'a',
      })
    );

    const initialCallCount = setConfigSpy.mock.calls.length;
    // Simulate a page re-render that produces a new but equivalent inline object
    rerender();
    const finalCallCount = setConfigSpy.mock.calls.length;

    // setConfig should NOT be called again on an equivalent rerender
    expect(finalCallCount).toBe(initialCallCount);
    setConfigSpy.mockRestore();
  });
});
