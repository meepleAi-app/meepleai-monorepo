import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ScenariosSection } from '@/dev-tools/panel/sections/ScenariosSection';
import { createScenarioStore } from '@/dev-tools/scenarioStore';
import { createMockAuthStore } from '@/dev-tools/mockAuthStore';
import { resetSwitcherForTests } from '@/dev-tools/scenarioSwitcher';
import { SCENARIO_MANIFEST } from '@/dev-tools/scenarioManifest';
import type { Scenario } from '@/dev-tools/types';

describe('Scenario switch integration', () => {
  it('full switch flow: select → store updates → invalidateQueries called', async () => {
    resetSwitcherForTests();
    const initial = SCENARIO_MANIFEST.empty as Scenario;
    const scenarioStore = createScenarioStore(initial);
    const authStore = createMockAuthStore({
      scenarioUser: initial.auth.currentUser,
      availableUsers: initial.auth.availableUsers,
      envRole: null,
      queryStringRole: null,
    });
    const queryClient = new QueryClient();
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    const user = userEvent.setup();
    render(
      <QueryClientProvider client={queryClient}>
        <ScenariosSection
          scenarioStore={scenarioStore}
          authStore={authStore}
          queryClient={queryClient}
        />
      </QueryClientProvider>
    );

    const select = screen.getByTestId('scenario-select');
    await user.selectOptions(select, 'small-library');

    await waitFor(() => {
      expect(scenarioStore.getState().scenario.name).toBe('small-library');
    });
    expect(invalidateSpy).toHaveBeenCalled();
    expect(scenarioStore.getState().isSwitching).toBe(false);
  });

  it('scenario switch fires CustomEvent for SSE cleanup', async () => {
    resetSwitcherForTests();
    const initial = SCENARIO_MANIFEST.empty as Scenario;
    const scenarioStore = createScenarioStore(initial);
    const authStore = createMockAuthStore({
      scenarioUser: initial.auth.currentUser,
      availableUsers: initial.auth.availableUsers,
      envRole: null,
      queryStringRole: null,
    });
    const queryClient = new QueryClient();

    let eventFired = false;
    window.addEventListener('meepledev:scenario-switch-begin', () => {
      eventFired = true;
    });

    const user = userEvent.setup();
    render(
      <QueryClientProvider client={queryClient}>
        <ScenariosSection
          scenarioStore={scenarioStore}
          authStore={authStore}
          queryClient={queryClient}
        />
      </QueryClientProvider>
    );

    await user.selectOptions(screen.getByTestId('scenario-select'), 'admin-busy');

    await waitFor(() => {
      expect(eventFired).toBe(true);
    });
  });
});
