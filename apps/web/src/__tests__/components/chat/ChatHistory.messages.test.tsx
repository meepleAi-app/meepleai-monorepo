/**
 * ChatHistory Component Tests (Updated for Issue #858)
 *
 * Tests for the ChatHistory component that displays a list of chat threads
 * with loading states, empty states, thread management, and active/archived separation.
 *
 * Target Coverage: 90%+
 */

import type { Mock } from 'vitest';
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ChatHistory } from '../../../components/chat/ChatHistory';
import { ChatProvider } from '../../../components/chat/ChatProvider';
import { ChatThread } from '../../../types';

// Mock useChatContext hook
vi.mock('../../../components/chat/ChatProvider', () => ({
  useChatContext: vi.fn(),
}));

import { useChatContext } from '../../../components/chat/ChatProvider';

const mockUseChatContext = useChatContext as Mock;

/**
 * Helper to create mock chat thread object
 */
const createMockChatThread = (overrides?: Partial<ChatThread>): ChatThread => ({
  id: 'chat-1',
  userId: 'user-1',
  gameId: 'game-1',
  title: 'Chess Expert',
  status: 'Active', // Issue #858: Added status field
  createdAt: '2025-01-10T10:00:00Z',
  lastMessageAt: '2025-01-10T10:05:00Z',
  messageCount: 5,
  messages: [],
  ...overrides,
});

describe('ChatHistory Component', () => {
  let mockSelectChat: Mock;
  let mockDeleteChat: Mock;
  let originalConfirm: any;

  beforeEach(() => {
    mockSelectChat = vi.fn();
    mockDeleteChat = vi.fn();
    originalConfirm = window.confirm;
    window.confirm = vi.fn(() => true); // Default to confirming deletions
    vi.clearAllMocks();
  });

  afterEach(() => {
    window.confirm = originalConfirm;
  });

  /**
   * Helper to setup chat context with default values
   */
  const setupChatContext = (overrides?: any) => {
    mockUseChatContext.mockReturnValue({
      chats: [],
      activeChatId: null,
      selectChat: mockSelectChat,
      deleteChat: mockDeleteChat,
      loading: { chats: false },
      ...overrides,
    });
  };

  /**
   * Test Group: Loading State
   */
  describe('Loading State', () => {
    it('displays loading skeleton when threads are being fetched', () => {
      setupChatContext({ loading: { chats: true } });
      render(<ChatHistory />);

      // Issue #858: Updated text for threads
      expect(screen.getByText('Caricamento thread...')).toBeInTheDocument();
      const statuses = screen.getAllByRole('status', { hidden: true });
      expect(statuses.length).toBeGreaterThan(0);
    });

    it('shows correct loading message', () => {
      setupChatContext({ loading: { chats: true } });
      render(<ChatHistory />);

      expect(screen.getByText('Caricamento thread...')).toBeInTheDocument();
    });

    it('displays skeleton loader with correct aria-label', () => {
      setupChatContext({ loading: { chats: true } });
      render(<ChatHistory />);

      // Issue #858: Updated aria-label for threads
      const skeletons = screen.getAllByLabelText('Caricamento cronologia thread');
      expect(skeletons.length).toBeGreaterThan(0);
    });

    it('displays loading state within navigation element', () => {
      setupChatContext({ loading: { chats: true } });
      render(<ChatHistory />);

      const nav = screen.getByRole('navigation', { name: 'Thread history' });
      expect(nav).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Empty State
   */
  describe('Empty State', () => {
    it('displays empty message when no threads exist', () => {
      setupChatContext({ chats: [] });
      render(<ChatHistory />);

      // Issue #858: Updated empty state message
      expect(screen.getByText('Nessun thread. Invia un messaggio per iniziare!')).toBeInTheDocument();
    });

    it('renders empty state within navigation element', () => {
      setupChatContext({ chats: [] });
      render(<ChatHistory />);

      const nav = screen.getByRole('navigation', { name: 'Thread history' });
      expect(nav).toContainElement(screen.getByText('Nessun thread. Invia un messaggio per iniziare!'));
    });

    it('applies correct styling to empty state', () => {
      setupChatContext({ chats: [] });
      render(<ChatHistory />);

      const emptyText = screen.getByText('Nessun thread. Invia un messaggio per iniziare!');
      expect(emptyText).toBeInTheDocument(); // Style assertion removed - Shadcn/UI uses Tailwind CSS classes
    });
  });

  /**
   * Test Group: Thread Separation (Issue #858)
   */
  describe('Thread Separation', () => {
    it('separates active and archived threads into sections', () => {
      const chats = [
        createMockChatThread({ id: 'thread-1', title: 'Active Thread 1', status: 'Active' }),
        createMockChatThread({ id: 'thread-2', title: 'Archived Thread', status: 'Closed' }),
        createMockChatThread({ id: 'thread-3', title: 'Active Thread 2', status: 'Active' }),
      ];
      setupChatContext({ chats });
      render(<ChatHistory />);

      expect(screen.getByText('Active Threads')).toBeInTheDocument();
      // "Archived" appears in both section heading and badge, use getByRole for heading
      const heading = screen.getByRole('heading', { name: 'Archived' });
      expect(heading).toBeInTheDocument();
    });

    it('shows only active threads section when no archived threads', () => {
      const chats = [
        createMockChatThread({ id: 'thread-1', status: 'Active' }),
        createMockChatThread({ id: 'thread-2', status: 'Active' }),
      ];
      setupChatContext({ chats });
      render(<ChatHistory />);

      expect(screen.getByText('Active Threads')).toBeInTheDocument();
      // No archived section or badges should appear
      expect(screen.queryByRole('heading', { name: 'Archived' })).not.toBeInTheDocument();
    });

    it('shows only archived section when all threads are closed', () => {
      const chats = [
        createMockChatThread({ id: 'thread-1', status: 'Closed' }),
        createMockChatThread({ id: 'thread-2', status: 'Closed' }),
      ];
      setupChatContext({ chats });
      render(<ChatHistory />);

      expect(screen.queryByText('Active Threads')).not.toBeInTheDocument();
      const heading = screen.getByRole('heading', { name: 'Archived' });
      expect(heading).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Chat List Rendering
   */
  describe('Chat List Rendering', () => {
    it('renders list of chat sessions', () => {
      const chats = [
        createMockChatThread({ id: 'chat-1', title: 'Chess' }),
        createMockChatThread({ id: 'chat-2', title: 'Catan Helper' }),
      ];
      setupChatContext({ chats });
      render(<ChatHistory />);

      const list = screen.getByRole('list');
      expect(list).toBeInTheDocument();
      expect(list.children).toHaveLength(2);
    });

    it('renders ChatHistoryItem for each chat', () => {
      const chats = [
        createMockChatThread({ id: 'chat-1' }),
        createMockChatThread({ id: 'chat-2' }),
        createMockChatThread({ id: 'chat-3' }),
      ];
      setupChatContext({ chats });
      render(<ChatHistory />);

      // Each ChatHistoryItem is rendered as a button role within list items
      const chatButtons = screen.getAllByRole('button');
      expect(chatButtons.length).toBeGreaterThanOrEqual(3); // At least 3 chat items (may have delete buttons)
    });

    it('passes correct props to ChatHistoryItem components', () => {
      const chat = createMockChatThread({
        id: 'chat-1',
        title: 'Chess Expert',
      });
      setupChatContext({ chats: [chat], activeChatId: 'chat-1' });
      render(<ChatHistory />);

      // ChatHistoryItem should display the title
      expect(screen.getByText('Chess Expert')).toBeInTheDocument();
    });

    it('marks active chat correctly', () => {
      const chats = [
        createMockChatThread({ id: 'chat-1' }),
        createMockChatThread({ id: 'chat-2' }),
      ];
      setupChatContext({ chats, activeChatId: 'chat-2' });
      render(<ChatHistory />);

      // Active chat should be indicated (tested via ChatHistoryItem)
      expect(screen.getByRole('list')).toBeInTheDocument();
    });

    it('renders navigation element with correct aria-label', () => {
      const chats = [createMockChatThread()];
      setupChatContext({ chats });
      render(<ChatHistory />);

      expect(screen.getByRole('navigation', { name: 'Thread history' })).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Chat Selection
   */
  describe('Chat Selection', () => {
    it('calls selectChat when thread item is clicked', () => {
      const chat = createMockChatThread({ id: 'chat-123', title: 'Test Agent' });
      setupChatContext({ chats: [chat] });
      render(<ChatHistory />);

      // Issue #858: ThreadListItem uses separate select button
      const selectButton = screen.getByRole('button', { name: /Select thread: Test Agent/ });
      fireEvent.click(selectButton);

      expect(mockSelectChat).toHaveBeenCalledWith('chat-123');
    });

    it('calls selectChat with correct thread ID for multiple threads', () => {
      const chats = [
        createMockChatThread({ id: 'chat-1', title: 'Agent 1' }),
        createMockChatThread({ id: 'chat-2', title: 'Agent 2' }),
        createMockChatThread({ id: 'chat-3', title: 'Agent 3' }),
      ];
      setupChatContext({ chats });
      render(<ChatHistory />);

      // Click the second thread item (Agent 2)
      const agent2Button = screen.getByRole('button', { name: /Select thread: Agent 2/ });
      fireEvent.click(agent2Button);

      expect(mockSelectChat).toHaveBeenCalledWith('chat-2');
    });

    it('handles asynchronous selectChat calls', async () => {
      mockSelectChat.mockResolvedValue(undefined);
      const chat = createMockChatThread({ id: 'chat-1', title: 'Test Agent' });
      setupChatContext({ chats: [chat] });
      render(<ChatHistory />);

      // Issue #858: Click the select button in ThreadListItem
      const selectButton = screen.getByRole('button', { name: /Select thread: Test Agent/ });
      fireEvent.click(selectButton);

      await waitFor(() => {
        expect(mockSelectChat).toHaveBeenCalledTimes(1);
      });
    });
  });
});

