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
 * Session status response type (AUTH-05)
 */
export type MockSessionStatusResponse = {
  expiresAt: string;
  lastSeenAt: string | null;
  remainingMinutes: number;
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

/**
 * Creates a mock session status response
 *
 * @example
 * const sessionStatus = createMockSessionStatus();
 * const expiringSession = createMockSessionStatus({ remainingMinutes: 4 });
 */
export const createMockSessionStatus = (
  overrides?: Partial<MockSessionStatusResponse>
): MockSessionStatusResponse => ({
  expiresAt: overrides?.expiresAt || new Date(Date.now() + 30 * 60 * 1000).toISOString(), // 30 minutes from now
  lastSeenAt: overrides?.lastSeenAt !== undefined ? overrides.lastSeenAt : new Date().toISOString(),
  remainingMinutes: overrides?.remainingMinutes !== undefined ? overrides.remainingMinutes : 30,
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

// =============================================================================
// ANALYTICS FIXTURES (ADMIN-02)
// =============================================================================

/**
 * Dashboard metrics type matching backend
 */
export type MockDashboardMetrics = {
  totalUsers: number;
  activeSessions: number;
  apiRequestsToday: number;
  totalPdfDocuments: number;
  totalChatMessages: number;
  averageConfidenceScore: number;
  totalRagRequests: number;
  totalTokensUsed: number;
};

/**
 * Time series data point type
 */
export type MockTimeSeriesDataPoint = {
  date: string;
  count: number;
  averageValue?: number | null;
};

/**
 * Dashboard stats response type
 */
export type MockDashboardStats = {
  metrics: MockDashboardMetrics;
  userTrend: MockTimeSeriesDataPoint[];
  sessionTrend: MockTimeSeriesDataPoint[];
  apiRequestTrend: MockTimeSeriesDataPoint[];
  pdfUploadTrend: MockTimeSeriesDataPoint[];
  chatMessageTrend: MockTimeSeriesDataPoint[];
  generatedAt: string;
};

/**
 * Creates mock dashboard metrics with realistic numbers
 *
 * @example
 * const metrics = createMockDashboardMetrics();
 * const customMetrics = createMockDashboardMetrics({ totalUsers: 500 });
 */
export const createMockDashboardMetrics = (
  overrides?: Partial<MockDashboardMetrics>
): MockDashboardMetrics => ({
  totalUsers: overrides?.totalUsers ?? 150,
  activeSessions: overrides?.activeSessions ?? 42,
  apiRequestsToday: overrides?.apiRequestsToday ?? 1250,
  totalPdfDocuments: overrides?.totalPdfDocuments ?? 35,
  totalChatMessages: overrides?.totalChatMessages ?? 8420,
  averageConfidenceScore: overrides?.averageConfidenceScore ?? 0.87,
  totalRagRequests: overrides?.totalRagRequests ?? 5320,
  totalTokensUsed: overrides?.totalTokensUsed ?? 1250000,
});

/**
 * Creates mock time series data for charts
 */
export const createMockTimeSeriesData = (
  days: number = 7,
  baseValue: number = 100
): MockTimeSeriesDataPoint[] => {
  const data: MockTimeSeriesDataPoint[] = [];
  const now = new Date();

  for (let i = days - 1; i >= 0; i--) {
    const date = new Date(now);
    date.setDate(date.getDate() - i);
    data.push({
      date: date.toISOString(),
      count: baseValue + Math.floor(Math.random() * 50),
      averageValue: Math.random() * 0.5 + 0.5, // 0.5 to 1.0
    });
  }

  return data;
};

/**
 * Creates complete mock dashboard stats with all required fields
 *
 * @example
 * const stats = createMockDashboardStats();
 * const customStats = createMockDashboardStats({
 *   metrics: createMockDashboardMetrics({ totalUsers: 500 }),
 *   userTrend: createMockTimeSeriesData(30, 100)
 * });
 */
export const createMockDashboardStats = (
  overrides?: Partial<MockDashboardStats>
): MockDashboardStats => ({
  metrics: overrides?.metrics ?? createMockDashboardMetrics(),
  userTrend: overrides?.userTrend ?? createMockTimeSeriesData(7, 10),
  sessionTrend: overrides?.sessionTrend ?? createMockTimeSeriesData(7, 30),
  apiRequestTrend: overrides?.apiRequestTrend ?? createMockTimeSeriesData(7, 500),
  pdfUploadTrend: overrides?.pdfUploadTrend ?? createMockTimeSeriesData(7, 5),
  chatMessageTrend: overrides?.chatMessageTrend ?? createMockTimeSeriesData(7, 200),
  generatedAt: overrides?.generatedAt ?? new Date().toISOString(),
});

// =============================================================================
// PDF DOCUMENT FIXTURES
// =============================================================================

/**
 * PDF document type matching backend
 */
export type MockPdfDocument = {
  id: string;
  fileName: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string;
  processingStatus?: 'pending' | 'processing' | 'completed' | 'failed';
  processingError?: string | null;
  status?: string;
  logUrl?: string | null;
};

/**
 * Creates a mock PDF document with complete structure
 *
 * @example
 * const pdf = createMockPdfDocument();
 * const failedPdf = createMockPdfDocument({
 *   processingStatus: 'failed',
 *   processingError: 'Invalid PDF format'
 * });
 */
export const createMockPdfDocument = (
  overrides?: Partial<MockPdfDocument>
): MockPdfDocument => {
  const base: MockPdfDocument = {
    id: overrides?.id ?? `pdf-${Date.now()}`,
    fileName: overrides?.fileName ?? 'test-document.pdf',
    fileSizeBytes: overrides?.fileSizeBytes ?? 1024 * 100, // 100KB
    uploadedAt: overrides?.uploadedAt ?? new Date().toISOString(),
    uploadedByUserId: overrides?.uploadedByUserId ?? 'user-1',
  };

  // Add optional fields if provided
  if (overrides?.processingStatus !== undefined) {
    base.processingStatus = overrides.processingStatus;
  }
  if (overrides?.processingError !== undefined) {
    base.processingError = overrides.processingError;
  }
  if (overrides?.status !== undefined) {
    base.status = overrides.status;
  }
  if (overrides?.logUrl !== undefined) {
    base.logUrl = overrides.logUrl;
  }

  return base;
};

/**
 * Creates a list of mock PDF documents
 */
export const createMockPdfList = (count: number = 3): MockPdfDocument[] => {
  return Array.from({ length: count }, (_, i) =>
    createMockPdfDocument({
      id: `pdf-${i + 1}`,
      fileName: `document-${i + 1}.pdf`,
    })
  );
};

// =============================================================================
// TYPE VALIDATION HELPERS
// =============================================================================

/**
 * Type guard to validate mock user structure
 */
export const isValidMockUser = (obj: any): obj is MockUser => {
  return (
    typeof obj === 'object' &&
    obj !== null &&
    typeof obj.id === 'string' &&
    typeof obj.email === 'string' &&
    (typeof obj.displayName === 'string' || obj.displayName === null) &&
    ['Admin', 'Editor', 'User'].includes(obj.role)
  );
};

/**
 * Type guard to validate mock auth response
 */
export const isValidMockAuthResponse = (obj: any): obj is MockAuthResponse => {
  return (
    typeof obj === 'object' &&
    obj !== null &&
    isValidMockUser(obj.user) &&
    typeof obj.expiresAt === 'string'
  );
};

/**
 * Type guard to validate mock game
 */
export const isValidMockGame = (obj: any): obj is MockGame => {
  return (
    typeof obj === 'object' &&
    obj !== null &&
    typeof obj.id === 'string' &&
    typeof obj.name === 'string' &&
    typeof obj.createdAt === 'string'
  );
};

/**
 * Type guard to validate mock RuleSpec
 */
export const isValidMockRuleSpec = (obj: any): obj is MockRuleSpec => {
  return (
    typeof obj === 'object' &&
    obj !== null &&
    typeof obj.gameId === 'string' &&
    typeof obj.version === 'string' &&
    typeof obj.createdAt === 'string' &&
    Array.isArray(obj.rules) &&
    obj.rules.every((rule: any) =>
      typeof rule === 'object' &&
      rule !== null &&
      typeof rule.id === 'string' &&
      typeof rule.text === 'string'
    )
  );
};

/**
 * Validates and throws helpful error if mock data is incomplete
 *
 * @example
 * validateMockData('User', mockUser, isValidMockUser);
 */
export const validateMockData = <T>(
  name: string,
  data: any,
  validator: (obj: any) => obj is T
): asserts data is T => {
  if (!validator(data)) {
    throw new Error(
      `Invalid mock ${name} structure. Ensure all required fields are present.\n` +
      `Received: ${JSON.stringify(data, null, 2)}`
    );
  }
};
