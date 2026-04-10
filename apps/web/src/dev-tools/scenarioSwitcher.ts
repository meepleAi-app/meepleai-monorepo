import { SCENARIO_MANIFEST } from './scenarioManifest';
import { validateScenario } from './scenarioValidator';

import type { MockAuthState } from './mockAuthStore';
import type { ScenarioState } from './scenarioStore';
import type { Scenario } from './types';
import type { QueryClient } from '@tanstack/react-query';
import type { StoreApi } from 'zustand/vanilla';

export interface SwitchScenarioDeps {
  scenarioStore: StoreApi<ScenarioState>;
  authStore: StoreApi<MockAuthState>;
  queryClient: QueryClient;
}

let currentSwitchController: AbortController | null = null;

export function resetSwitcherForTests(): void {
  currentSwitchController = null;
}

export async function switchScenario(
  scenarioName: string,
  deps: SwitchScenarioDeps
): Promise<void> {
  const { scenarioStore, authStore, queryClient } = deps;

  if (currentSwitchController !== null) {
    currentSwitchController.abort();
  }
  const myController = new AbortController();
  currentSwitchController = myController;
  const { signal } = myController;

  scenarioStore.getState().beginSwitch();

  try {
    await queryClient.cancelQueries({ type: 'active' });
    if (signal.aborted) return;

    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('meepledev:scenario-switch-begin'));
    }
    if (signal.aborted) return;

    const raw = SCENARIO_MANIFEST[scenarioName];
    if (raw === undefined) {
      throw new Error(`Unknown scenario: ${scenarioName}`);
    }
    const validation = validateScenario(raw);
    if (!validation.valid) {
      throw new Error(`Scenario "${scenarioName}" is invalid: ${validation.errors.join('; ')}`);
    }
    if (signal.aborted) return;

    const newScenario = raw as Scenario;
    scenarioStore.getState().loadScenario(newScenario);

    authStore.setState({
      currentUser: newScenario.auth.currentUser,
      availableUsers: newScenario.auth.availableUsers,
    });

    scenarioStore.getState().endSwitch();

    if (signal.aborted) return;
    await queryClient.invalidateQueries();
  } finally {
    if (scenarioStore.getState().isSwitching) {
      scenarioStore.getState().endSwitch();
    }
    if (currentSwitchController === myController) {
      currentSwitchController = null;
    }
  }
}
