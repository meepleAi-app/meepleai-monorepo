import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { createPanelUiStore } from '@/dev-tools/panel/stores/panelUiStore';
import { useQueryStringPanelOpen } from '@/dev-tools/panel/hooks/useQueryStringPanelOpen';

describe('useQueryStringPanelOpen', () => {
  beforeEach(() => {
    sessionStorage.clear();
    window.history.replaceState({}, '', '/');
  });

  it('opens panel when ?devpanel=1 is in URL', () => {
    window.history.replaceState({}, '', '/?devpanel=1');
    const store = createPanelUiStore();
    expect(store.getState().isOpen).toBe(false);
    renderHook(() => useQueryStringPanelOpen(store));
    expect(store.getState().isOpen).toBe(true);
  });

  it('strips devpanel param from URL after parsing', () => {
    window.history.replaceState({}, '', '/?devpanel=1&other=keep');
    const store = createPanelUiStore();
    renderHook(() => useQueryStringPanelOpen(store));
    expect(window.location.search).not.toContain('devpanel');
    expect(window.location.search).toContain('other=keep');
  });

  it('does nothing when devpanel param is absent', () => {
    window.history.replaceState({}, '', '/?other=value');
    const store = createPanelUiStore();
    renderHook(() => useQueryStringPanelOpen(store));
    expect(store.getState().isOpen).toBe(false);
    expect(window.location.search).toContain('other=value');
  });

  it('does nothing when devpanel=0', () => {
    window.history.replaceState({}, '', '/?devpanel=0');
    const store = createPanelUiStore();
    renderHook(() => useQueryStringPanelOpen(store));
    expect(store.getState().isOpen).toBe(false);
  });
});
