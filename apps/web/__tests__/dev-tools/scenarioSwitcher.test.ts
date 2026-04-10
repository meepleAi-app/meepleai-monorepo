import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient } from '@tanstack/react-query';
import { switchScenario, resetSwitcherForTests } from '@/dev-tools/scenarioSwitcher';
import { createScenarioStore } from '@/dev-tools/scenarioStore';
import { createMockAuthStore } from '@/dev-tools/mockAuthStore';
import { SCENARIO_MANIFEST } from '@/dev-tools/scenarioManifest';

const initialScenario = SCENARIO_MANIFEST.empty as Parameters<typeof createScenarioStore>[0];

describe('scenarioSwitcher', () => {
  beforeEach(() => {
    resetSwitcherForTests();
  });

  it('happy path: switches scenario, fires CustomEvent, invalidates queries', async () => {
    const scenarioStore = createScenarioStore(initialScenario);
    const authStore = createMockAuthStore({
      scenarioUser: initialScenario.auth.currentUser,
      availableUsers: initialScenario.auth.availableUsers,
      envRole: null,
      queryStringRole: null,
    });
    const queryClient = new QueryClient();
    const cancelSpy = vi.spyOn(queryClient, 'cancelQueries');
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    let eventFired = false;
    window.addEventListener('meepledev:scenario-switch-begin', () => {
      eventFired = true;
    });

    await switchScenario('small-library', { scenarioStore, authStore, queryClient });

    expect(scenarioStore.getState().scenario.name).toBe('small-library');
    expect(scenarioStore.getState().isSwitching).toBe(false);
    expect(eventFired).toBe(true);
    expect(cancelSpy).toHaveBeenCalled();
    expect(invalidateSpy).toHaveBeenCalled();
  });

  it('rejects unknown scenario name without leaving isSwitching=true', async () => {
    const scenarioStore = createScenarioStore(initialScenario);
    const authStore = createMockAuthStore({
      scenarioUser: initialScenario.auth.currentUser,
      availableUsers: initialScenario.auth.availableUsers,
      envRole: null,
      queryStringRole: null,
    });
    const queryClient = new QueryClient();

    await expect(
      switchScenario('non-existent', { scenarioStore, authStore, queryClient })
    ).rejects.toThrow();
    expect(scenarioStore.getState().isSwitching).toBe(false);
    expect(scenarioStore.getState().scenario.name).toBe('empty');
  });

  it('last-click-wins: rapid switches converge to final selection', async () => {
    const scenarioStore = createScenarioStore(initialScenario);
    const authStore = createMockAuthStore({
      scenarioUser: initialScenario.auth.currentUser,
      availableUsers: initialScenario.auth.availableUsers,
      envRole: null,
      queryStringRole: null,
    });
    const queryClient = new QueryClient();

    const p1 = switchScenario('small-library', { scenarioStore, authStore, queryClient });
    const p2 = switchScenario('admin-busy', { scenarioStore, authStore, queryClient });
    const p3 = switchScenario('empty', { scenarioStore, authStore, queryClient });

    await Promise.allSettled([p1, p2, p3]);

    expect(scenarioStore.getState().scenario.name).toBe('empty');
    expect(scenarioStore.getState().isSwitching).toBe(false);
  });

  it('try/finally guarantees endSwitch even on exception', async () => {
    const scenarioStore = createScenarioStore(initialScenario);
    const authStore = createMockAuthStore({
      scenarioUser: initialScenario.auth.currentUser,
      availableUsers: initialScenario.auth.availableUsers,
      envRole: null,
      queryStringRole: null,
    });
    const queryClient = new QueryClient();
    vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(() => {
      throw new Error('boom');
    });

    await expect(
      switchScenario('small-library', { scenarioStore, authStore, queryClient })
    ).rejects.toThrow('boom');
    expect(scenarioStore.getState().isSwitching).toBe(false);
  });
});
