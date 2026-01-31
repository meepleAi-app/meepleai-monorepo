/**
 * E2E Test Hooks Type Definitions (Issue #1807)
 *
 * Programmatic API for E2E tests to bypass UI interactions.
 * Only available in test environment (NODE_ENV=test).
 */

interface MeepleAITestHooks {
  chat?: {
    /**
     * Programmatically select a game (bypasses GameSelector UI)
     */
    selectGame: (gameId: string) => Promise<void>;

    /**
     * Programmatically select game and wait for agent auto-selection
     */
    selectGameAndAgent: (gameId: string, agentId: string) => Promise<void>;

    /**
     * Programmatically send a message (bypasses MessageInput form)
     */
    sendMessage: (content: string) => Promise<void>;
  };
}

declare global {
  interface Window {
    __MEEPLEAI_TEST_HOOKS__?: MeepleAITestHooks;
  }
}

export {};
