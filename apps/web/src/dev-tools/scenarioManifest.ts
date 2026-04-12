import adminBusyScenario from './fixtures/admin-busy.json';
import emptyScenario from './fixtures/empty.json';
import smallLibraryScenario from './fixtures/small-library.json';

/**
 * Static manifest of all available scenarios.
 * Keeping this static (no dynamic imports) ensures Turbopack can bundle
 * scenarios into the dev chunk without path resolution issues.
 *
 * To add a new scenario:
 * 1. Add JSON to docs/superpowers/fixtures/scenarios/
 * 2. Copy to apps/web/src/dev-tools/fixtures/
 * 3. Add import + entry here
 */
export const SCENARIO_MANIFEST: Record<string, unknown> = {
  empty: emptyScenario,
  'small-library': smallLibraryScenario,
  'admin-busy': adminBusyScenario,
};

export function listScenarioNames(): string[] {
  return Object.keys(SCENARIO_MANIFEST);
}
