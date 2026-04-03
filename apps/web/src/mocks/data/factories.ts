/**
 * Browser-safe mock data factories
 *
 * Pure factory functions for mock data — no test framework dependencies.
 * Used by both src/mocks/handlers/ (browser dev mode) and __tests__/ (tests).
 */

/**
 * Generate a deterministic, valid UUID v4 from a number.
 * Pattern: a0eebc99-9c0b-4ef8-bb6d-<12 hex digits>
 * Group 3 starts with '4' (version), group 4 starts with 'b' (variant) — both pass z.string().uuid().
 */
export const mockId = (n: number): string =>
  `a0eebc99-9c0b-4ef8-bb6d-${n.toString(16).padStart(12, '0')}`;

/**
 * Base URL for MSW handlers.
 * In mock mode (NEXT_PUBLIC_MOCK_MODE=true), browser fetches use relative paths (empty getApiBase())
 * which resolve to http://localhost:3000. Handlers must match the same origin.
 * NEXT_PUBLIC_MOCK_MODE is NOT in .env.local, so cross-env correctly overrides it.
 */
export const HANDLER_BASE =
  process.env.NEXT_PUBLIC_MOCK_MODE === 'true'
    ? 'http://localhost:3000'
    : process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// =============================================================================
// AUTH FIXTURES
// =============================================================================

export type MockUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: 'Admin' | 'Editor' | 'User';
  onboardingCompleted?: boolean;
  onboardingSkipped?: boolean;
};

export type MockAuthResponse = {
  user: MockUser;
  expiresAt: string;
};

export type MockSessionStatusResponse = {
  expiresAt: string;
  lastSeenAt: string | null;
  remainingMinutes: number;
};

export const createMockUser = (overrides?: Partial<MockUser>): MockUser => ({
  id: overrides?.id || mockId(1),
  email: overrides?.email || 'test@meepleai.dev',
  displayName: overrides?.displayName !== undefined ? overrides.displayName : 'Test User',
  role: overrides?.role || 'User',
  onboardingCompleted: overrides?.onboardingCompleted ?? false,
  onboardingSkipped: overrides?.onboardingSkipped ?? false,
});

export const createMockAuthResponse = (userOverrides?: Partial<MockUser>): MockAuthResponse => ({
  user: createMockUser(userOverrides),
  expiresAt: new Date(Date.now() + 3600000).toISOString(),
});

export const mockAdminAuth = (): MockAuthResponse =>
  createMockAuthResponse({
    id: mockId(10),
    email: 'admin@meepleai.dev',
    displayName: 'Admin User',
    role: 'Admin',
  });

export const mockEditorAuth = (): MockAuthResponse =>
  createMockAuthResponse({
    id: mockId(20),
    email: 'editor@meepleai.dev',
    displayName: 'Editor User',
    role: 'Editor',
  });

export const mockUserAuth = (): MockAuthResponse =>
  createMockAuthResponse({
    id: mockId(1),
    email: 'user@meepleai.dev',
    displayName: 'Regular User',
    role: 'User',
  });

export const createMockSessionStatus = (
  overrides?: Partial<MockSessionStatusResponse>
): MockSessionStatusResponse => ({
  expiresAt: overrides?.expiresAt || new Date(Date.now() + 30 * 60 * 1000).toISOString(),
  lastSeenAt: overrides?.lastSeenAt !== undefined ? overrides.lastSeenAt : new Date().toISOString(),
  remainingMinutes: overrides?.remainingMinutes !== undefined ? overrides.remainingMinutes : 30,
});

// =============================================================================
// GAME FIXTURES
// =============================================================================

export type MockGame = {
  id: string;
  title: string;
  createdAt: string;
  updatedAt?: string;
};

export const createMockGame = (overrides?: Partial<MockGame>): MockGame => ({
  id: overrides?.id || mockId(100),
  title: overrides?.title || 'Test Game',
  createdAt: overrides?.createdAt || new Date().toISOString(),
  updatedAt: overrides?.updatedAt,
});

export const mockChessGame = (): MockGame => createMockGame({ id: mockId(101), title: 'Chess' });
export const mockTicTacToeGame = (): MockGame =>
  createMockGame({ id: mockId(102), title: 'Tic-Tac-Toe' });

// =============================================================================
// RULESPEC FIXTURES
// =============================================================================

export type MockRuleAtom = {
  id: string;
  text: string;
  section?: string | null;
  page?: string | null;
  line?: string | null;
};

export type MockRuleSpec = {
  id: string;
  gameId: string;
  version: string;
  createdAt: string;
  createdByUserId: string | null;
  parentVersionId: string | null;
  atoms: MockRuleAtom[];
};

export const createMockRuleAtom = (overrides?: Partial<MockRuleAtom>): MockRuleAtom => ({
  id: overrides?.id || `rule-${Date.now()}`,
  text: overrides?.text || 'Test rule text',
  section: overrides?.section,
  page: overrides?.page,
  line: overrides?.line,
});

export const createMockRuleSpec = (overrides?: Partial<MockRuleSpec>): MockRuleSpec => ({
  id: overrides?.id || 'rulespec-1',
  gameId: overrides?.gameId || 'game-1',
  version: overrides?.version || 'v1',
  createdAt: overrides?.createdAt || new Date().toISOString(),
  createdByUserId: overrides?.createdByUserId !== undefined ? overrides.createdByUserId : null,
  parentVersionId: overrides?.parentVersionId !== undefined ? overrides.parentVersionId : null,
  atoms: overrides?.atoms || [
    createMockRuleAtom({ id: 'atom-1', text: 'Default rule 1' }),
    createMockRuleAtom({ id: 'atom-2', text: 'Default rule 2' }),
  ],
});

// =============================================================================
// CHAT FIXTURES
// =============================================================================

export type MockAgent = {
  id: string;
  name: string;
  type: string;
  strategyName: string;
  strategyParameters: Record<string, unknown>;
  isActive: boolean;
  createdAt: string;
  lastInvokedAt: string | null;
  invocationCount: number;
  isRecentlyUsed: boolean;
  isIdle: boolean;
};

export type MockChatMessage = {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
};

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

export const createMockAgent = (overrides?: Partial<MockAgent>): MockAgent => ({
  id: overrides?.id || 'agent-1',
  name: overrides?.name || 'Test Agent',
  type: overrides?.type || 'qa',
  strategyName: overrides?.strategyName || 'hybrid-rag',
  strategyParameters: overrides?.strategyParameters || {},
  isActive: overrides?.isActive !== undefined ? overrides.isActive : true,
  createdAt: overrides?.createdAt || new Date().toISOString(),
  lastInvokedAt: overrides?.lastInvokedAt !== undefined ? overrides.lastInvokedAt : null,
  invocationCount: overrides?.invocationCount || 0,
  isRecentlyUsed: overrides?.isRecentlyUsed !== undefined ? overrides.isRecentlyUsed : false,
  isIdle: overrides?.isIdle !== undefined ? overrides.isIdle : true,
});

export const createMockChatMessage = (overrides?: Partial<MockChatMessage>): MockChatMessage => ({
  id: overrides?.id || `msg-${Date.now()}`,
  role: overrides?.role || 'user',
  content: overrides?.content || 'Test message',
  timestamp: overrides?.timestamp || new Date().toISOString(),
});

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
// PDF DOCUMENT FIXTURES
// =============================================================================

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

export const createMockPdfDocument = (overrides?: Partial<MockPdfDocument>): MockPdfDocument => {
  const base: MockPdfDocument = {
    id: overrides?.id ?? `pdf-${Date.now()}`,
    fileName: overrides?.fileName ?? 'test-document.pdf',
    fileSizeBytes: overrides?.fileSizeBytes ?? 1024 * 100,
    uploadedAt: overrides?.uploadedAt ?? new Date().toISOString(),
    uploadedByUserId: overrides?.uploadedByUserId ?? 'user-1',
  };
  if (overrides?.processingStatus !== undefined) base.processingStatus = overrides.processingStatus;
  if (overrides?.processingError !== undefined) base.processingError = overrides.processingError;
  if (overrides?.status !== undefined) base.status = overrides.status;
  if (overrides?.logUrl !== undefined) base.logUrl = overrides.logUrl;
  return base;
};

export const createMockPdfList = (count: number = 3): MockPdfDocument[] =>
  Array.from({ length: count }, (_, i) =>
    createMockPdfDocument({ id: `pdf-${i + 1}`, fileName: `document-${i + 1}.pdf` })
  );

// =============================================================================
// SESSION FIXTURES
// =============================================================================

export type MockSession = {
  id: string;
  sessionCode: string;
  sessionType: 'Generic' | 'GameSpecific';
  gameName?: string;
  gameIcon?: string;
  sessionDate: Date;
  status: 'Active' | 'Paused' | 'Finalized';
  participantCount: number;
};

export type MockSessionParticipant = {
  id: string;
  displayName: string;
  isOwner: boolean;
  isCurrentUser: boolean;
  avatarColor: string;
  totalScore: number;
  rank?: number;
  isTyping?: boolean;
};

export const createMockSession = (overrides?: Partial<MockSession>): MockSession => ({
  id: overrides?.id || `session-${Date.now()}`,
  sessionCode: overrides?.sessionCode || 'ABC123',
  sessionType: overrides?.sessionType || 'GameSpecific',
  gameName: overrides?.gameName,
  gameIcon: overrides?.gameIcon,
  sessionDate: overrides?.sessionDate || new Date('2024-01-15T10:00:00Z'),
  status: overrides?.status || 'Active',
  participantCount: overrides?.participantCount ?? 4,
});

export const createMockSessionParticipant = (
  overrides?: Partial<MockSessionParticipant>
): MockSessionParticipant => ({
  id: overrides?.id || `participant-${Date.now()}`,
  displayName: overrides?.displayName || 'Test Player',
  isOwner: overrides?.isOwner ?? false,
  isCurrentUser: overrides?.isCurrentUser ?? false,
  avatarColor: overrides?.avatarColor || '#3b82f6',
  totalScore: overrides?.totalScore ?? 0,
  rank: overrides?.rank,
  isTyping: overrides?.isTyping,
});
