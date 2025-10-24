/**
 * Common test fixtures for consistent mocking across test suite
 *
 * This file provides reusable factory functions for creating test data.
 * Use these fixtures instead of duplicating mock objects in individual test files.
 *
 * @example
 * import { createMockUser, createMockRouter } from '../fixtures/common-fixtures';
 *
 * const adminUser = createMockUser({ role: 'Admin' });
 * const router = createMockRouter({ query: { gameId: 'demo-chess' } });
 */

import type { NextRouter } from 'next/router';

// =============================================================================
// AUTH FIXTURES
// =============================================================================

/**
 * User type matching backend AuthUser
 */
export type MockUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: 'Admin' | 'Editor' | 'User';
};

/**
 * Auth response type matching backend AuthResponse
 */
export type MockAuthResponse = {
  user: MockUser;
  expiresAt: string;
};

/**
 * Creates a mock user with configurable properties
 *
 * @example
 * // Default user (User role)
 * const user = createMockUser();
 *
 * // Admin user
 * const admin = createMockUser({ role: 'Admin' });
 *
 * // Custom user
 * const custom = createMockUser({
 *   id: 'user-123',
 *   email: 'custom@test.com',
 *   displayName: 'Custom User'
 * });
 */
export const createMockUser = (overrides?: Partial<MockUser>): MockUser => ({
  id: overrides?.id || 'user-1',
  email: overrides?.email || 'test@meepleai.dev',
  displayName: overrides?.displayName !== undefined ? overrides.displayName : 'Test User',
  role: overrides?.role || 'User',
});

/**
 * Creates a mock auth response with user and expiration
 *
 * @example
 * const authResponse = createMockAuthResponse();
 * const adminAuth = createMockAuthResponse({ role: 'Admin' });
 */
export const createMockAuthResponse = (userOverrides?: Partial<MockUser>): MockAuthResponse => ({
  user: createMockUser(userOverrides),
  expiresAt: new Date(Date.now() + 3600000).toISOString(), // 1 hour from now
});

// Preset auth responses for common scenarios
export const mockAdminAuth = (): MockAuthResponse =>
  createMockAuthResponse({
    id: 'admin-1',
    email: 'admin@meepleai.dev',
    displayName: 'Admin User',
    role: 'Admin'
  });

export const mockEditorAuth = (): MockAuthResponse =>
  createMockAuthResponse({
    id: 'editor-1',
    email: 'editor@meepleai.dev',
    displayName: 'Editor User',
    role: 'Editor'
  });

export const mockUserAuth = (): MockAuthResponse =>
  createMockAuthResponse({
    id: 'user-1',
    email: 'user@meepleai.dev',
    displayName: 'Regular User',
    role: 'User'
  });

// =============================================================================
// ROUTER FIXTURES
// =============================================================================

/**
 * Creates a mock Next.js router with configurable properties
 *
 * @example
 * // Default router (root path)
 * const router = createMockRouter();
 *
 * // Router with query params
 * const gameRouter = createMockRouter({ query: { gameId: 'demo-chess' } });
 *
 * // Router with custom path
 * const chatRouter = createMockRouter({
 *   pathname: '/chat',
 *   asPath: '/chat?gameId=demo-chess'
 * });
 */
export const createMockRouter = (overrides?: Partial<NextRouter>): NextRouter => ({
  route: overrides?.route || '/',
  pathname: overrides?.pathname || '/',
  query: overrides?.query || {},
  asPath: overrides?.asPath || '/',
  basePath: '',
  push: jest.fn(),
  replace: jest.fn(),
  reload: jest.fn(),
  back: jest.fn(),
  prefetch: jest.fn().mockResolvedValue(undefined),
  beforePopState: jest.fn(),
  events: {
    on: jest.fn(),
    off: jest.fn(),
    emit: jest.fn(),
  },
  isFallback: false,
  isLocaleDomain: false,
  isReady: true,
  isPreview: false,
  defaultLocale: 'en',
  domainLocales: [],
  locale: undefined,
  locales: undefined,
  ...overrides,
} as unknown as NextRouter);

// =============================================================================
// API CLIENT FIXTURES
// =============================================================================

/**
 * Mock API client type matching our api.ts structure
 */
export type MockApiClient = {
  get: jest.Mock;
  post: jest.Mock;
  put: jest.Mock;
  delete: jest.Mock;
};

/**
 * Creates a mock API client with all methods as Jest mocks
 *
 * @example
 * const mockApi = createMockApiClient();
 * mockApi.get.mockResolvedValue({ data: 'test' });
 */
export const createMockApiClient = (): MockApiClient => ({
  get: jest.fn(),
  post: jest.fn(),
  put: jest.fn(),
  delete: jest.fn(),
});

// =============================================================================
// GAME FIXTURES
// =============================================================================

/**
 * Game type matching backend Game entity
 */
export type MockGame = {
  id: string;
  name: string;
  createdAt: string;
  updatedAt?: string;
};

/**
 * Creates a mock game
 *
 * @example
 * const chess = createMockGame({ id: 'demo-chess', name: 'Chess' });
 */
export const createMockGame = (overrides?: Partial<MockGame>): MockGame => ({
  id: overrides?.id || 'game-1',
  name: overrides?.name || 'Test Game',
  createdAt: overrides?.createdAt || new Date().toISOString(),
  updatedAt: overrides?.updatedAt,
});

// Preset games
export const mockChessGame = (): MockGame =>
  createMockGame({ id: 'demo-chess', name: 'Chess' });

export const mockTicTacToeGame = (): MockGame =>
  createMockGame({ id: 'demo-tictactoe', name: 'Tic-Tac-Toe' });

// =============================================================================
// RULESPEC FIXTURES
// =============================================================================

/**
 * RuleAtom type matching backend
 */
export type MockRuleAtom = {
  id: string;
  text: string;
  section?: string | null;
  page?: string | null;
  line?: string | null;
};

/**
 * RuleSpec type matching backend
 */
export type MockRuleSpec = {
  gameId: string;
  version: string;
  createdAt: string;
  rules: MockRuleAtom[];
};

/**
 * Creates a mock rule atom
 *
 * @example
 * const rule = createMockRuleAtom({
 *   id: 'rule-1',
 *   text: 'Players take turns'
 * });
 */
export const createMockRuleAtom = (overrides?: Partial<MockRuleAtom>): MockRuleAtom => ({
  id: overrides?.id || `rule-${Date.now()}`,
  text: overrides?.text || 'Test rule text',
  section: overrides?.section,
  page: overrides?.page,
  line: overrides?.line,
});

/**
 * Creates a mock RuleSpec
 *
 * @example
 * const ruleSpec = createMockRuleSpec({
 *   gameId: 'demo-chess',
 *   rules: [createMockRuleAtom({ text: 'Rule 1' })]
 * });
 */
export const createMockRuleSpec = (overrides?: Partial<MockRuleSpec>): MockRuleSpec => ({
  gameId: overrides?.gameId || 'game-1',
  version: overrides?.version || '1.0.0',
  createdAt: overrides?.createdAt || new Date().toISOString(),
  rules: overrides?.rules || [
    createMockRuleAtom({ id: 'rule-1', text: 'Default rule 1' }),
    createMockRuleAtom({ id: 'rule-2', text: 'Default rule 2' }),
  ],
});

// =============================================================================
// CHAT FIXTURES
// =============================================================================

/**
 * Agent type matching backend
 */
export type MockAgent = {
  id: string;
  gameId: string;
  name: string;
  type: 'qa' | 'explain' | 'setup';
  isActive: boolean;
};

/**
 * Chat message type
 */
export type MockChatMessage = {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
};

/**
 * Chat type matching backend
 */
export type MockChat = {
  id: string;
  gameId: string;
  gameName?: string;
  agentId?: string;
  agentName?: string;
  startedAt?: string;
  lastMessageAt?: string | null;
  createdAt: string;
  messages: MockChatMessage[];
};

/**
 * Creates a mock agent
 *
 * @example
 * const agent = createMockAgent({
 *   gameId: 'demo-chess',
 *   name: 'Chess Expert'
 * });
 */
export const createMockAgent = (overrides?: Partial<MockAgent>): MockAgent => ({
  id: overrides?.id || 'agent-1',
  gameId: overrides?.gameId || 'game-1',
  name: overrides?.name || 'Test Agent',
  type: overrides?.type || 'qa',
  isActive: overrides?.isActive !== undefined ? overrides.isActive : true,
});

/**
 * Creates a mock chat message
 */
export const createMockChatMessage = (overrides?: Partial<MockChatMessage>): MockChatMessage => ({
  id: overrides?.id || `msg-${Date.now()}`,
  role: overrides?.role || 'user',
  content: overrides?.content || 'Test message',
  timestamp: overrides?.timestamp || new Date().toISOString(),
});

/**
 * Creates a mock chat
 *
 * @example
 * const chat = createMockChat({
 *   gameId: 'demo-chess',
 *   messages: [createMockChatMessage({ content: 'Hello' })]
 * });
 */
export const createMockChat = (overrides?: Partial<MockChat>): MockChat => ({
  id: overrides?.id || 'chat-1',
  gameId: overrides?.gameId || 'game-1',
  gameName: overrides?.gameName,
  agentId: overrides?.agentId,
  agentName: overrides?.agentName,
  startedAt: overrides?.startedAt,
  lastMessageAt: overrides?.lastMessageAt !== undefined ? overrides.lastMessageAt : undefined,
  createdAt: overrides?.createdAt || new Date().toISOString(),
  messages: overrides?.messages || [],
});

// =============================================================================
// FETCH RESPONSE FIXTURES
// =============================================================================

/**
 * Creates a mock fetch Response object
 *
 * @example
 * global.fetch = jest.fn(() => createMockResponse(200, { data: 'test' }));
 */
export const createMockResponse = (
  status: number,
  body?: unknown,
  headers?: Record<string, string>
): Promise<Response> => {
  const response: Partial<Response> = {
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 200 ? 'OK' : status === 404 ? 'Not Found' : 'Error',
    json: async () => body,
    text: async () => typeof body === 'string' ? body : JSON.stringify(body),
    headers: new Headers(headers || {}),
  };

  return Promise.resolve(response as Response);
};

/**
 * Creates a mock successful JSON response
 */
export const createJsonResponse = (body: unknown): Promise<Response> =>
  createMockResponse(200, body);

/**
 * Creates a mock error response
 */
export const createErrorResponse = (
  status: number,
  error: string | { error: string }
): Promise<Response> => {
  const errorBody = typeof error === 'string' ? { error } : error;
  return createMockResponse(status, errorBody);
};
