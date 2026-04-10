'use client';

import type { MockAuthState } from '@/dev-tools/mockAuthStore';
import type { ScenarioState } from '@/dev-tools/scenarioStore';

import { DevPanel } from './DevPanel';
import { useKeyboardShortcut } from './hooks/useKeyboardShortcut';
import { useQueryStringPanelOpen } from './hooks/useQueryStringPanelOpen';

import type { MockControlState, HandlerGroup } from './sections/TogglesSection';
import type { PanelUiState } from './stores/panelUiStore';
import type { QueryClient } from '@tanstack/react-query';
import type { StoreApi } from 'zustand/vanilla';

export interface DevPanelMountProps {
  uiStore: StoreApi<PanelUiState>;
  mockControlStore: StoreApi<MockControlState>;
  handlerGroups: HandlerGroup[];
  worker: { resetHandlers: (...handlers: unknown[]) => void };
  scenarioStore: StoreApi<ScenarioState>;
  authStore: StoreApi<MockAuthState>;
  queryClient: QueryClient;
}

export function DevPanelMount({
  uiStore,
  mockControlStore,
  handlerGroups,
  worker,
  scenarioStore,
  authStore,
  queryClient,
}: DevPanelMountProps): React.JSX.Element {
  useKeyboardShortcut({ ctrl: true, shift: true, key: 'm' }, () => {
    uiStore.getState().toggle();
  });
  useQueryStringPanelOpen(uiStore);
  return (
    <DevPanel
      uiStore={uiStore}
      mockControlStore={mockControlStore}
      handlerGroups={handlerGroups}
      worker={worker}
      scenarioStore={scenarioStore}
      authStore={authStore}
      queryClient={queryClient}
    />
  );
}
