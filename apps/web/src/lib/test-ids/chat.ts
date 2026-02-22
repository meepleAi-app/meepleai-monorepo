/**
 * Test ID constants for Chat-related components.
 *
 * Covers: ChatStatusBadge, ChatAgentInfo, ChatStatsDisplay,
 *         ChatGameContext, ChatUnreadBadge, NewChatView
 *
 * Import from '@/lib/test-ids' in both components and tests.
 */

export const CHAT_TEST_IDS = {
  // ChatStatusBadge
  statusBadge: (status: string) => `chat-status-${status}` as const,

  // ChatAgentInfo
  agentInfo: 'chat-agent-info',

  // ChatStatsDisplay
  statsDisplay: 'chat-stats-display',
  messageCount: 'message-count',
  lastMessageAt: 'last-message-at',
  duration: 'chat-duration',

  // ChatGameContext
  gameContext: 'chat-game-context',

  // ChatUnreadBadge
  unreadBadge: 'chat-unread-badge',

  // NewChatView sections
  gameSelectionSection: 'game-selection-section',
  agentSelectionSection: 'agent-selection-section',
  agentCard: (type: string) => `agent-card-${type}` as const,
  quickStartSection: 'quick-start-section',
  startChatBtn: 'start-chat-btn',
  gameCard: (id: string) => `game-card-${id}` as const,
  gameSearchInput: 'game-search-input',
  skipGameBtn: 'skip-game-btn',
  newChatError: 'new-chat-error',
} as const;
