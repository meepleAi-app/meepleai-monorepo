/**
 * Scenario bridge — lets MSW handlers read the current scenario without
 * importing from @/dev-tools.
 *
 * Why the bridge?
 * `src/mocks/` MUST stay self-contained so the `@/dev-tools` folder can be
 * removed entirely for production builds (see
 * `.github/workflows/dev-tools-isolation.yml`). A direct import from
 * `@/dev-tools/install` would break that contract. Instead, `install.ts`
 * calls {@link setScenarioBridge} at bootstrap with adapter callbacks that
 * delegate to the real `scenarioStore`, and handlers call
 * {@link getScenarioBridge} to read current data.
 *
 * When the dev-tools module is absent (prod build), {@link getScenarioBridge}
 * returns `null` and handlers fall back to empty arrays — keeping the
 * behavior safe but useless outside of dev-mode, which is exactly what we want.
 *
 * The bridge is deliberately structural (collection-level accessors) instead
 * of exposing the raw Zustand store. This keeps the mocks/ namespace free of
 * any type dependency on zustand and lets us swap the backing store later
 * without touching 14 handler files.
 */

/** Shape of a mock game as used by MSW handlers. */
export interface BridgeMockGame {
  id: string;
  title: string;
  publisher?: string;
  averageRating?: number;
  bggId?: number;
  [key: string]: unknown;
}

/** Shape of a mock session as used by MSW handlers. */
export interface BridgeMockSession {
  id: string;
  gameId: string;
  startedAt?: string;
  [key: string]: unknown;
}

/** Shape of a mock chat thread as used by MSW handlers. */
export interface BridgeMockChat {
  chatId: string;
  messages: Array<{ role: 'user' | 'assistant' | 'system'; content: string }>;
}

/** Shape of the mock library as used by MSW handlers. */
export interface BridgeMockLibrary {
  ownedGameIds: string[];
  wishlistGameIds: string[];
}

/**
 * Bridge contract: collection-level accessors + CRUD mutators that handlers
 * can call to read/write the current scenario state.
 */
export interface ScenarioBridge {
  // Readers
  getGames: () => BridgeMockGame[];
  getSessions: () => BridgeMockSession[];
  getChatHistory: () => BridgeMockChat[];
  getLibrary: () => BridgeMockLibrary;
  getScenarioName: () => string;

  // Writers (mutate the underlying scenarioStore)
  addGame: (game: BridgeMockGame) => void;
  updateGame: (id: string, patch: Partial<BridgeMockGame>) => void;
  removeGame: (id: string) => void;
  toggleOwned: (gameId: string) => void;
  toggleWishlist: (gameId: string) => void;
}

let bridge: ScenarioBridge | null = null;

/**
 * Set the scenario bridge. Called once by `@/dev-tools/install.ts` at
 * bootstrap. Calling it again replaces the bridge (supports HMR).
 */
export function setScenarioBridge(next: ScenarioBridge): void {
  bridge = next;
}

/**
 * Get the scenario bridge, or null if dev-tools are not installed.
 * Handlers should treat null as "no scenario data available" and return
 * empty collections.
 */
export function getScenarioBridge(): ScenarioBridge | null {
  return bridge;
}

/**
 * Clear the scenario bridge. Used by tests to reset between cases.
 */
export function clearScenarioBridge(): void {
  bridge = null;
}
