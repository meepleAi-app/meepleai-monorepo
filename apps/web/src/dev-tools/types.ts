/**
 * MeepleDev shared types.
 * Keep in sync with docs/superpowers/fixtures/schema/scenario.schema.json
 */

export type UserRole = 'Guest' | 'User' | 'Editor' | 'Admin' | 'SuperAdmin';

export interface MockUser {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface MockGame {
  id: string;
  title: string;
  publisher?: string | null;
  averageRating?: number | null;
  bggId?: number | null;
  [key: string]: unknown;
}

export interface MockSession {
  id: string;
  gameId: string;
  startedAt?: string;
  [key: string]: unknown;
}

export interface MockChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export interface MockChat {
  chatId: string;
  messages: MockChatMessage[];
}

export interface MockLibrary {
  ownedGameIds: string[];
  wishlistGameIds: string[];
}

export interface MockBggGame {
  bggId: number;
  name: string;
  [key: string]: unknown;
}

export interface MockDocument {
  id: string;
  pages: string[];
  [key: string]: unknown;
}

export interface Scenario {
  $schema?: string;
  name: string;
  description: string;
  auth: {
    currentUser: MockUser;
    availableUsers: MockUser[];
  };
  games: MockGame[];
  sessions: MockSession[];
  library: MockLibrary;
  chatHistory: MockChat[];
  bggGames?: MockBggGame[];
  documents?: MockDocument[];
}

/** MSW group toggle state (group name → enabled). */
export type GroupToggles = Record<string, boolean>;

/** Per-endpoint override (phase 2), key = "group.METHOD /path". */
export type EndpointOverrides = Record<string, boolean>;

export interface ToggleConfig {
  groups: GroupToggles;
  overrides: EndpointOverrides;
}

// ============================================================================
// Phase 2 — Dev Panel runtime types
// ============================================================================

/** Tab identifiers for the Dev Panel. */
export type DevPanelTab = 'toggles' | 'scenarios' | 'auth' | 'inspector';

/** Backend toggles state returned by GET /dev/toggles. */
export interface BackendTogglesState {
  toggles: Record<string, boolean>;
  knownServices: string[];
}

/** Request inspector entry (ring buffer item). */
export interface InspectorEntry {
  id: string;
  timestamp: number;
  method: string;
  url: string;
  status: number;
  durationMs: number;
  isMock: boolean;
  mockSource?: string;
}
