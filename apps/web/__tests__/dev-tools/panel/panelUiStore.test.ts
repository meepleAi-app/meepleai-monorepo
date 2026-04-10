import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { createPanelUiStore } from '@/dev-tools/panel/stores/panelUiStore';

describe('panelUiStore', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });
  afterEach(() => {
    sessionStorage.clear();
  });

  it('initializes with defaults when sessionStorage is empty', () => {
    const store = createPanelUiStore();
    const state = store.getState();
    expect(state.isOpen).toBe(false);
    expect(state.collapsed).toBe(false);
    expect(state.activeTab).toBe('toggles');
    expect(state.drawerWidth).toBe(420);
  });

  it('reads initial state from sessionStorage if present', () => {
    sessionStorage.setItem('meepledev-panel-is-open', 'true');
    sessionStorage.setItem('meepledev-panel-active-tab', 'inspector');
    sessionStorage.setItem('meepledev-panel-collapsed', 'true');
    sessionStorage.setItem('meepledev-panel-drawer-width', '600');
    const store = createPanelUiStore();
    const state = store.getState();
    expect(state.isOpen).toBe(true);
    expect(state.activeTab).toBe('inspector');
    expect(state.collapsed).toBe(true);
    expect(state.drawerWidth).toBe(600);
  });

  it('setOpen persists to sessionStorage', () => {
    const store = createPanelUiStore();
    store.getState().setOpen(true);
    expect(sessionStorage.getItem('meepledev-panel-is-open')).toBe('true');
    expect(store.getState().isOpen).toBe(true);
  });

  it('toggle flips isOpen', () => {
    const store = createPanelUiStore();
    expect(store.getState().isOpen).toBe(false);
    store.getState().toggle();
    expect(store.getState().isOpen).toBe(true);
    store.getState().toggle();
    expect(store.getState().isOpen).toBe(false);
  });

  it('setActiveTab persists', () => {
    const store = createPanelUiStore();
    store.getState().setActiveTab('scenarios');
    expect(store.getState().activeTab).toBe('scenarios');
    expect(sessionStorage.getItem('meepledev-panel-active-tab')).toBe('scenarios');
  });

  it('setCollapsed persists', () => {
    const store = createPanelUiStore();
    store.getState().setCollapsed(true);
    expect(store.getState().collapsed).toBe(true);
    expect(sessionStorage.getItem('meepledev-panel-collapsed')).toBe('true');
  });

  it('setDrawerWidth persists', () => {
    const store = createPanelUiStore();
    store.getState().setDrawerWidth(500);
    expect(store.getState().drawerWidth).toBe(500);
    expect(sessionStorage.getItem('meepledev-panel-drawer-width')).toBe('500');
  });

  it('falls back to in-memory state when sessionStorage throws', () => {
    const originalSetItem = Storage.prototype.setItem;
    Storage.prototype.setItem = () => {
      throw new Error('QuotaExceededError');
    };
    const store = createPanelUiStore();
    expect(() => store.getState().setOpen(true)).not.toThrow();
    expect(store.getState().isOpen).toBe(true);
    Storage.prototype.setItem = originalSetItem;
  });

  it('rejects invalid activeTab values from sessionStorage', () => {
    sessionStorage.setItem('meepledev-panel-active-tab', 'malicious');
    const store = createPanelUiStore();
    expect(store.getState().activeTab).toBe('toggles');
  });
});
