import Ajv2020, { type ErrorObject } from 'ajv/dist/2020';
import addFormats from 'ajv-formats';

// Schema lives both in docs/superpowers/fixtures/schema/ (canonical) and here
// (apps/web/src/dev-tools/schema/) because Turbopack does not reliably resolve
// deep relative imports crossing package boundaries at runtime.
import scenarioSchema from './schema/scenario.schema.json';

import type { Scenario } from './types';

const ajv = new Ajv2020({ allErrors: true, strict: false });
addFormats(ajv);
const validator = ajv.compile(scenarioSchema);

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

export function validateScenario(data: unknown): ValidationResult {
  const valid = validator(data);
  if (valid) {
    return { valid: true, errors: [] };
  }
  const errors = (validator.errors ?? []).map(
    (e: ErrorObject) => `${e.instancePath || '(root)'}: ${e.message ?? 'unknown'}`
  );
  return { valid: false, errors };
}

/** Fallback used when loading a scenario fails. */
export const SCENARIO_FALLBACK: Scenario = {
  name: 'empty',
  description: 'Empty fallback scenario (scenario load failed)',
  auth: {
    currentUser: {
      id: 'MOCK-00000000-0000-0000-0000-000000000000',
      email: 'guest@meeple.local',
      displayName: 'Guest',
      role: 'Guest',
    },
    availableUsers: [],
  },
  games: [],
  sessions: [],
  library: { ownedGameIds: [], wishlistGameIds: [] },
  chatHistory: [],
  bggGames: [],
  documents: [],
};
