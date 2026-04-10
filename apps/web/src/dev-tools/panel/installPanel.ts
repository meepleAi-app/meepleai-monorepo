import { devPanelClient } from './api/devPanelClient';
import { createPanelUiStore, type PanelUiState } from './stores/panelUiStore';

import type { StoreApi } from 'zustand/vanilla';

export interface PanelDependencies {
  mockControlStore: StoreApi<unknown>;
  scenarioStore: StoreApi<unknown>;
  authStore: StoreApi<unknown>;
}

export interface InstalledPanel {
  uiStore: StoreApi<PanelUiState>;
  deps: PanelDependencies;
}

export function installPanel(deps: PanelDependencies): InstalledPanel {
  const uiStore = createPanelUiStore();
  void devPanelClient.getToggles().catch(() => {});
  if (typeof console !== 'undefined') {
    console.warn('[MeepleDev Phase 2] Dev Panel installed. Press Ctrl+Shift+M to open.');
  }
  return { uiStore, deps };
}
