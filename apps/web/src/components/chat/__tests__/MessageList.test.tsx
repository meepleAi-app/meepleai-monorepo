import { render, screen } from '@testing-library/react';
import { MessageList } from '../MessageList';

// Mock useChatContext hook
jest.mock('../ChatProvider', () => ({
  useChatContext: jest.fn()
}));

import { useChatContext } from '../ChatProvider';

// Mock child components
jest.mock('../Message', () => ({
  Message: ({ message, isUser }: any) => (
    <li data-testid={`message-${message.id}`} data-role={message.role}>
      {message.content}
    </li>
  )
}));

jest.mock('@/components/loading/SkeletonLoader', () => ({
  SkeletonLoader: ({ variant, count, ariaLabel }: any) => (
    <div data-testid="skeleton-loader" data-variant={variant} data-count={count} aria-label={ariaLabel}>
      Loading skeletons...
    </div>
  )
}));

describe('MessageList', () => {
  const mockUseChatContext = useChatContext as jest.MockedFunction<typeof useChatContext>;

  const mockMessages = [
    {
      id: 'msg-1',
      content: 'Hello, how do I play Catan?',
      role: 'user',
      timestamp: new Date('2024-01-15T10:00:00Z')
    },
    {
      id: 'msg-2',
      content: 'Here are the rules for Catan...',
      role: 'assistant',
      timestamp: new Date('2024-01-15T10:01:00Z')
    },
    {
      id: 'msg-3',
      content: 'Thanks! What about the trade rules?',
      role: 'user',
      timestamp: new Date('2024-01-15T10:02:00Z')
    }
  ];

  const defaultContextValue = {
    messages: mockMessages,
    activeChatId: 'chat-123',
    loading: { creating: false, messages: false, sending: false },
    games: [],
    selectedGameId: null,
    selectedAgentId: null,
    agents: [],
    threads: [],
    activeThread: null,
    createChat: jest.fn(),
    setSelectedGameId: jest.fn(),
    setSelectedAgentId: jest.fn(),
    loadThreadMessages: jest.fn(),
    sendMessage: jest.fn(),
    deleteMessage: jest.fn(),
    editMessage: jest.fn(),
    regenerateMessage: jest.fn()
  } as any;

  beforeEach(() => {
    jest.clearAllMocks();
    mockUseChatContext.mockReturnValue(defaultContextValue);
  });

  describe('Loading State', () => {
    it('should display loading message when messages are loading', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [],
        loading: { creating: false, messages: true, sending: false }
      });

      render(<MessageList />);

      expect(screen.getByText('Caricamento messaggi...')).toBeInTheDocument();
    });

    it('should display skeleton loader when messages are loading', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [],
        loading: { creating: false, messages: true, sending: false }
      });

      render(<MessageList />);

      const skeleton = screen.getByTestId('skeleton-loader');
      expect(skeleton).toBeInTheDocument();
      expect(skeleton).toHaveAttribute('data-variant', 'message');
      expect(skeleton).toHaveAttribute('data-count', '3');
    });

    it('should have loading region with polite aria-live', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [],
        loading: { creating: false, messages: true, sending: false }
      });

      render(<MessageList />);

      const region = screen.getByRole('region', { name: /chat messages/i });
      expect(region).toBeInTheDocument();

      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('aria-live', 'polite');
    });
  });

  describe('Empty State', () => {
    it('should display empty message when no messages exist', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [],
        activeChatId: null
      });

      render(<MessageList />);

      expect(screen.getByText('Nessun messaggio ancora.')).toBeInTheDocument();
    });

    it('should show "start asking" message when chat is active', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [],
        activeChatId: 'chat-123'
      });

      render(<MessageList />);

      expect(screen.getByText('Inizia facendo una domanda!')).toBeInTheDocument();
    });

    it('should show "select or create chat" message when no active chat', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [],
        activeChatId: null
      });

      render(<MessageList />);

      expect(screen.getByText('Seleziona una chat esistente o creane una nuova per iniziare.')).toBeInTheDocument();
    });
  });

  describe('Messages Display', () => {
    it('should render all messages', () => {
      render(<MessageList />);

      expect(screen.getByTestId('message-msg-1')).toBeInTheDocument();
      expect(screen.getByTestId('message-msg-2')).toBeInTheDocument();
      expect(screen.getByTestId('message-msg-3')).toBeInTheDocument();
    });

    it('should render messages in correct order', () => {
      render(<MessageList />);

      const messageElements = screen.getAllByTestId(/message-msg-/);
      expect(messageElements).toHaveLength(3);
      expect(messageElements[0]).toHaveAttribute('data-testid', 'message-msg-1');
      expect(messageElements[1]).toHaveAttribute('data-testid', 'message-msg-2');
      expect(messageElements[2]).toHaveAttribute('data-testid', 'message-msg-3');
    });

    it('should pass isUser prop correctly for user messages', () => {
      render(<MessageList />);

      const userMessage = screen.getByTestId('message-msg-1');
      expect(userMessage).toHaveAttribute('data-role', 'user');
    });

    it('should pass isUser prop correctly for assistant messages', () => {
      render(<MessageList />);

      const assistantMessage = screen.getByTestId('message-msg-2');
      expect(assistantMessage).toHaveAttribute('data-role', 'assistant');
    });

    it('should render message content', () => {
      render(<MessageList />);

      expect(screen.getByText('Hello, how do I play Catan?')).toBeInTheDocument();
      expect(screen.getByText('Here are the rules for Catan...')).toBeInTheDocument();
      expect(screen.getByText('Thanks! What about the trade rules?')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have region role with aria-label', () => {
      render(<MessageList />);

      const region = screen.getByRole('region', { name: /chat messages/i });
      expect(region).toBeInTheDocument();
      expect(region).toHaveAttribute('aria-label', 'Chat messages');
    });

    it('should have log role for messages list', () => {
      render(<MessageList />);

      const log = screen.getByRole('log');
      expect(log).toBeInTheDocument();
      expect(log).toHaveAttribute('aria-live', 'polite');
      expect(log).toHaveAttribute('aria-atomic', 'false');
    });

    it('should use ul element for messages list', () => {
      const { container } = render(<MessageList />);

      const ul = container.querySelector('ul');
      expect(ul).toBeInTheDocument();
      expect(ul).toHaveClass('list-none');
    });
  });

  describe('Edge Cases', () => {
    it('should handle single message', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [mockMessages[0]]
      });

      render(<MessageList />);

      const messageElements = screen.getAllByTestId(/message-msg-/);
      expect(messageElements).toHaveLength(1);
    });

    it('should handle many messages', () => {
      const manyMessages = Array.from({ length: 50 }, (_, i) => ({
        id: `msg-${i}`,
        content: `Message ${i}`,
        role: i % 2 === 0 ? 'user' : 'assistant',
        timestamp: new Date()
      }));

      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: manyMessages
      });

      render(<MessageList />);

      const messageElements = screen.getAllByTestId(/message-msg-/);
      expect(messageElements).toHaveLength(50);
    });

    it('should transition from loading to messages', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [],
        loading: { creating: false, messages: true, sending: false }
      });

      const { rerender } = render(<MessageList />);

      expect(screen.getByText('Caricamento messaggi...')).toBeInTheDocument();

      // Simulate loading complete
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: mockMessages,
        loading: { creating: false, messages: false, sending: false }
      });

      rerender(<MessageList />);

      expect(screen.queryByText('Caricamento messaggi...')).not.toBeInTheDocument();
      expect(screen.getByTestId('message-msg-1')).toBeInTheDocument();
    });

    it('should transition from empty to messages', () => {
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: [],
        activeChatId: 'chat-123'
      });

      const { rerender } = render(<MessageList />);

      expect(screen.getByText('Nessun messaggio ancora.')).toBeInTheDocument();

      // Add messages
      mockUseChatContext.mockReturnValue({
        ...defaultContextValue,
        messages: mockMessages,
        activeChatId: 'chat-123'
      });

      rerender(<MessageList />);

      expect(screen.queryByText('Nessun messaggio ancora.')).not.toBeInTheDocument();
      expect(screen.getByTestId('message-msg-1')).toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('should have proper background color', () => {
      render(<MessageList />);

      const region = screen.getByRole('region', { name: /chat messages/i });
      expect(region).toHaveClass('bg-white');
    });

    it('should be scrollable', () => {
      render(<MessageList />);

      const region = screen.getByRole('region', { name: /chat messages/i });
      expect(region).toHaveClass('overflow-y-auto');
    });
  });
});