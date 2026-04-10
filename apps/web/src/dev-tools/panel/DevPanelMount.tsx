'use client';

import { DevPanel } from './DevPanel';
import { useKeyboardShortcut } from './hooks/useKeyboardShortcut';
import { useQueryStringPanelOpen } from './hooks/useQueryStringPanelOpen';

import type { PanelUiState } from './stores/panelUiStore';
import type { StoreApi } from 'zustand/vanilla';

export interface DevPanelMountProps {
  uiStore: StoreApi<PanelUiState>;
}

export function DevPanelMount({ uiStore }: DevPanelMountProps): React.JSX.Element {
  useKeyboardShortcut({ ctrl: true, shift: true, key: 'm' }, () => {
    uiStore.getState().toggle();
  });
  useQueryStringPanelOpen(uiStore);
  return <DevPanel uiStore={uiStore} />;
}
