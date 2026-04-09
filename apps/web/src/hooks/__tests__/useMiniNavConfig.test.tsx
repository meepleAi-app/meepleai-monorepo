import { renderHook } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

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
});
