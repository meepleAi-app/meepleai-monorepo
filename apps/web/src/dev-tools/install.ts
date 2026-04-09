import { createMockAuthStore, readRoleFromEnv, readRoleFromQueryString } from './mockAuthStore';
import { createMockControlStore, parseGroupList } from './mockControlCore';
import { SCENARIO_MANIFEST } from './scenarioManifest';
import { createScenarioStore } from './scenarioStore';
import { validateScenario, SCENARIO_FALLBACK } from './scenarioValidator';

import type { Scenario } from './types';

const KNOWN_GROUPS = [
  'auth',
  'games',
  'chat',
  'documents',
  'library',
  'shared-games',
  'catalog',
  'admin',
  'sessions',
  'game-nights',
  'players',
  'notifications',
  'badges',
] as const;

function loadScenarioByName(name: string): Scenario {
  const raw = SCENARIO_MANIFEST[name];
  if (!raw) {
    console.warn(`[MeepleDev] Scenario "${name}" not found in manifest; using fallback`);
    return SCENARIO_FALLBACK;
  }
  const result = validateScenario(raw);
  if (!result.valid) {
    console.warn(`[MeepleDev] Scenario "${name}" failed validation:`, result.errors);
    return SCENARIO_FALLBACK;
  }
  return raw as Scenario;
}

export interface InstalledDevTools {
  controlStore: ReturnType<typeof createMockControlStore>;
  scenarioStore: ReturnType<typeof createScenarioStore>;
  authStore: ReturnType<typeof createMockAuthStore>;
}

export function installDevTools(): InstalledDevTools {
  const enableList = parseGroupList(process.env.NEXT_PUBLIC_MSW_ENABLE);
  const disableList = parseGroupList(process.env.NEXT_PUBLIC_MSW_DISABLE);
  const scenarioName = process.env.NEXT_PUBLIC_DEV_SCENARIO ?? 'empty';

  const scenario = loadScenarioByName(scenarioName);

  const controlStore = createMockControlStore({
    allGroups: [...KNOWN_GROUPS],
    enableList,
    disableList,
  });

  const scenarioStore = createScenarioStore(scenario);

  const authStore = createMockAuthStore({
    scenarioUser: scenario.auth.currentUser,
    availableUsers: scenario.auth.availableUsers,
    envRole: readRoleFromEnv(),
    queryStringRole: readRoleFromQueryString(),
  });

  if (typeof console !== 'undefined') {
    console.warn(
      `[MeepleDev] installed · scenario=${scenario.name} · groups=`,
      controlStore.getState().toggles.groups
    );
  }

  return { controlStore, scenarioStore, authStore };
}
