import { HttpResponse } from 'msw';

/**
 * Returns 503 if a scenario switch is in progress.
 * Uses the scenario bridge to check isSwitching without coupling to dev-tools.
 */
export function guardScenarioSwitching(): Response | null {
  if (typeof window === 'undefined') return null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const meepledev = (window as any).__meepledev_stores__;
  if (meepledev?.scenarioStore?.getState?.().isSwitching === true) {
    return HttpResponse.json(
      { error: 'scenario-switching', message: 'Scenario switch in progress, retry shortly' },
      { status: 503 }
    ) as unknown as Response;
  }
  return null;
}
