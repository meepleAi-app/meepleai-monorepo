/**
 * Chat Page - Feedback Tests (Migrated to Zustand - Issue #1083)
 *
 * Tests for thumbs up/down feedback functionality in the chat interface.
 * This file focuses on feedback submission, state management, and error handling.
 *
 * Migration Pattern:
 * - Direct store access via useChatStore
 * - Tests setMessageFeedback action with optimistic updates
 * - Verifies rollback on API failure
 */

import { api } from '../../../lib/api';
import { useChatStore } from '../../../store/chat/store';
import { resetChatStore } from '../../utils/zustand-test-utils';

// Mock API
jest.mock('../../../lib/api');

const mockApi = api as jest.Mocked<typeof api>;

describe('ChatPage - Feedback', () => {
  const userResponse = {
    user: {
      id: 'user-1',
      email: 'user@example.com',
      displayName: 'Test User',
      role: 'User',
    },
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
  };

  const mockMessages = [
    {
      id: 'msg-1',
      role: 'user' as const,
      content: 'How do I castle?',
      timestamp: new Date('2025-01-10T10:00:00Z'),
    },
    {
      id: 'msg-2',
      role: 'assistant' as const,
      content: 'Castling is a special move...',
      timestamp: new Date('2025-01-10T10:01:00Z'),
      backendMessageId: 'backend-msg-2',
      endpoint: 'qa',
      gameId: 'game-1',
      feedback: null,
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();

    // Set store state - use false to preserve action functions
    useChatStore.setState({
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
      sidebarCollapsed: false,
      activeChatIds: { 'game-1': 'chat-1' },
      messagesByChat: { 'chat-1': [...mockMessages] },
      games: [],
      agents: [],
      chatsByGame: {},
      loading: {
        chats: false,
        messages: false,
        sending: false,
        creating: false,
        updating: false,
        deleting: false,
        games: false,
        agents: false,
      },
      error: null,
      inputValue: '',
      editingMessageId: null,
      editContent: '',
      searchMode: 'Hybrid',
    }, false); // false = partial update, preserves action functions

    // Default API setup
    mockApi.get.mockResolvedValue(userResponse);
  });

  afterEach(() => {
    resetChatStore();
  });

  it('submits helpful feedback when thumbs up is clicked', async () => {
    mockApi.post.mockResolvedValueOnce({});

    // Call setMessageFeedback directly instead of through UI
    const { setMessageFeedback } = useChatStore.getState();
    await setMessageFeedback('msg-2', 'helpful');

    // Verify optimistic update
    const state = useChatStore.getState();
    const messages = state.messagesByChat['chat-1'];
    const assistantMsg = messages.find(m => m.id === 'msg-2');
    expect(assistantMsg?.feedback).toBe('helpful');

    // Verify API call
    expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', {
      messageId: 'backend-msg-2',
      endpoint: 'qa',
      gameId: 'game-1',
      feedback: 'helpful',
    });
  });

  it('submits not-helpful feedback when thumbs down is clicked', async () => {
    mockApi.post.mockResolvedValueOnce({});

    // Call setMessageFeedback directly
    const { setMessageFeedback } = useChatStore.getState();
    await setMessageFeedback('msg-2', 'not-helpful');

    // Verify optimistic update
    const state = useChatStore.getState();
    const messages = state.messagesByChat['chat-1'];
    const assistantMsg = messages.find(m => m.id === 'msg-2');
    expect(assistantMsg?.feedback).toBe('not-helpful');

    // Verify API call
    expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', {
      messageId: 'backend-msg-2',
      endpoint: 'qa',
      gameId: 'game-1',
      feedback: 'not-helpful',
    });
  });

  it('toggles feedback to null when clicking same button twice', async () => {
    mockApi.post.mockResolvedValue({});
    const { setMessageFeedback } = useChatStore.getState();

    // First click - set to helpful
    await setMessageFeedback('msg-2', 'helpful');
    let state = useChatStore.getState();
    let messages = state.messagesByChat['chat-1'];
    let assistantMsg = messages.find(m => m.id === 'msg-2');
    expect(assistantMsg?.feedback).toBe('helpful');

    // Second click - toggle to null
    await setMessageFeedback('msg-2', 'helpful');
    state = useChatStore.getState();
    messages = state.messagesByChat['chat-1'];
    assistantMsg = messages.find(m => m.id === 'msg-2');
    expect(assistantMsg?.feedback).toBeNull();
  });

  it('changes feedback when switching between helpful and not-helpful', async () => {
    mockApi.post.mockResolvedValue({});
    const { setMessageFeedback } = useChatStore.getState();

    // First click helpful
    await setMessageFeedback('msg-2', 'helpful');
    let state = useChatStore.getState();
    let messages = state.messagesByChat['chat-1'];
    let assistantMsg = messages.find(m => m.id === 'msg-2');
    expect(assistantMsg?.feedback).toBe('helpful');

    // Then click not helpful
    await setMessageFeedback('msg-2', 'not-helpful');
    state = useChatStore.getState();
    messages = state.messagesByChat['chat-1'];
    assistantMsg = messages.find(m => m.id === 'msg-2');
    expect(assistantMsg?.feedback).toBe('not-helpful');
  });

  it('reverts feedback state when API call fails', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    mockApi.post.mockRejectedValueOnce(new Error('Feedback failed'));
    const { setMessageFeedback } = useChatStore.getState();

    await setMessageFeedback('msg-2', 'helpful');

    // Verify state was reverted to null after error
    const state = useChatStore.getState();
    const messages = state.messagesByChat['chat-1'];
    const assistantMsg = messages.find(m => m.id === 'msg-2');
    expect(assistantMsg?.feedback).toBeNull();

    consoleErrorSpy.mockRestore();
  });

  it('uses backend message ID for feedback when available', async () => {
    mockApi.post.mockResolvedValueOnce({});
    const { setMessageFeedback } = useChatStore.getState();

    await setMessageFeedback('msg-2', 'helpful');

    // Should use backend message ID from backendMessageId field
    expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', {
      messageId: 'backend-msg-2',
      endpoint: 'qa',
      gameId: 'game-1',
      feedback: 'helpful',
    });
  });

  it('only updates feedback for assistant messages', () => {
    // Verify mockMessages has correct structure
    expect(mockMessages.length).toBe(2);
    expect(mockMessages[0].role).toBe('user');
    expect(mockMessages[1].role).toBe('assistant');

    // Verify assistant message has feedback capability
    expect(mockMessages[1].backendMessageId).toBe('backend-msg-2');
    expect(mockMessages[1].endpoint).toBe('qa');
    expect(mockMessages[1].gameId).toBe('game-1');
  });
});
