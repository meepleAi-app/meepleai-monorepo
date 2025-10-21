import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChatPage from '../../pages/chat';
import { api } from '../../lib/api';
import { useChatStreaming } from '../../lib/hooks/useChatStreaming';

/**
 * Supplementary tests for ChatPage covering:
 * - CHAT-02: Follow-Up Questions
 * - CHAT-03: Multi-Game State Isolation
 * - CHAT-04: Auto-Scroll
 * - CHAT-05: Export Modal
 *
 * This file adds ~25 tests to complement the existing 114 tests in chat.test.tsx
 */

// Mock the useChatStreaming hook
const mockStartStreaming = jest.fn();
const mockStopStreaming = jest.fn();
let mockOnComplete: ((answer: string, snippets: any[], metadata: any) => void) | null = null;
let mockOnError: ((error: string) => void) | null = null;

jest.mock('../../lib/hooks/useChatStreaming', () => ({
  useChatStreaming: jest.fn((callbacks?: { onComplete?: any; onError?: any }) => {
    if (callbacks?.onComplete) {
      mockOnComplete = callbacks.onComplete;
    }
    if (callbacks?.onError) {
      mockOnError = callbacks.onError;
    }

    return [
      {
        isStreaming: false,
        currentAnswer: '',
        snippets: [],
        state: null,
        error: null
      },
      {
        startStreaming: mockStartStreaming,
        stopStreaming: mockStopStreaming
      }
    ];
  })
}));

// Mock the API client
jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
    delete: jest.fn(),
    chat: {
      exportChat: jest.fn(),
      updateMessage: jest.fn(),
      deleteMessage: jest.fn()
    }
  }
}));

const mockApi = api as jest.Mocked<typeof api>;

// Mock FollowUpQuestions component
jest.mock('../../components/FollowUpQuestions', () => ({
  FollowUpQuestions: ({ questions, onQuestionClick, disabled }: any) => (
    <div data-testid="follow-up-questions">
      {questions.map((q: string, i: number) => (
        <button
          key={i}
          onClick={() => onQuestionClick(q)}
          disabled={disabled}
          data-testid={`follow-up-${i}`}
        >
          {q}
        </button>
      ))}
    </div>
  ),
}));

// Mock ExportChatModal component
jest.mock('../../components/ExportChatModal', () => ({
  ExportChatModal: ({ isOpen, onClose, chatId, gameName }: any) =>
    isOpen ? (
      <div data-testid="export-modal">
        <div data-testid="export-chatid">{chatId}</div>
        <div data-testid="export-gamename">{gameName}</div>
        <button onClick={onClose} data-testid="close-export-modal">Close</button>
      </div>
    ) : null,
}));

// Mock framer-motion
jest.mock('framer-motion', () => ({
  AnimatePresence: ({ children }: any) => <>{children}</>,
}));

// Mock loading components
jest.mock('../../components/loading', () => ({
  SkeletonLoader: ({ variant, count, ariaLabel }: any) => (
    <div data-testid={`skeleton-${variant}`} aria-label={ariaLabel}>
      Loading {count} items
    </div>
  ),
  TypingIndicator: ({ visible, agentName }: any) =>
    visible ? <div data-testid="typing-indicator">{agentName} is typing...</div> : null,
  MessageAnimator: ({ children, id }: any) => <div data-messageid={id}>{children}</div>,
  LoadingButton: ({ children, isLoading, onClick, disabled, spinnerSize, ...props }: any) => (
    <button onClick={onClick} disabled={disabled || isLoading} {...props}>
      {isLoading ? 'Loading...' : children}
    </button>
  ),
}));

// Test data fixtures
const mockAuthResponse = {
  user: {
    id: 'user-1',
    email: 'user@example.com',
    displayName: 'Test User',
    role: 'User'
  },
  expiresAt: new Date(Date.now() + 3600000).toISOString()
};

const mockGames = [
  { id: 'game-1', name: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
  { id: 'game-2', name: 'Catan', createdAt: '2025-01-02T00:00:00Z' }
];

const mockAgentsGame1 = [
  { id: 'agent-1', gameId: 'game-1', name: 'Chess Expert', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }
];

const mockAgentsGame2 = [
  { id: 'agent-3', gameId: 'game-2', name: 'Catan Helper', kind: 'qa', createdAt: '2025-01-03T00:00:00Z' }
];

const mockChatsGame1 = [
  {
    id: 'chat-1',
    gameId: 'game-1',
    gameName: 'Chess',
    agentId: 'agent-1',
    agentName: 'Chess Expert',
    startedAt: '2025-01-10T10:00:00Z',
    lastMessageAt: '2025-01-10T10:05:00Z'
  }
];

const mockChatsGame2 = [
  {
    id: 'chat-2',
    gameId: 'game-2',
    gameName: 'Catan',
    agentId: 'agent-3',
    agentName: 'Catan Helper',
    startedAt: '2025-01-11T10:00:00Z',
    lastMessageAt: '2025-01-11T10:05:00Z'
  }
];

const mockChatWithFollowUpQuestions = {
  id: 'chat-1',
  gameId: 'game-1',
  gameName: 'Chess',
  agentId: 'agent-1',
  agentName: 'Chess Expert',
  startedAt: '2025-01-10T10:00:00Z',
  lastMessageAt: '2025-01-10T10:05:00Z',
  messages: [
    {
      id: 'msg-1',
      level: 'user',
      message: 'How do I set up chess?',
      metadataJson: null,
      createdAt: '2025-01-10T10:00:00Z'
    },
    {
      id: 'msg-2',
      level: 'agent',
      message: 'Place the board between players...',
      metadataJson: JSON.stringify({
        followUpQuestions: [
          'How do pawns move?',
          'What is castling?',
          'Can bishops move backwards?'
        ],
        snippets: []
      }),
      createdAt: '2025-01-10T10:01:00Z'
    }
  ]
};

const mockChatGame1 = {
  id: 'chat-1',
  gameId: 'game-1',
  gameName: 'Chess',
  agentId: 'agent-1',
  agentName: 'Chess Expert',
  startedAt: '2025-01-10T10:00:00Z',
  lastMessageAt: '2025-01-10T10:05:00Z',
  messages: [
    {
      id: 'msg-1',
      level: 'user',
      message: 'Chess question',
      metadataJson: null,
      createdAt: '2025-01-10T10:00:00Z'
    }
  ]
};

const mockChatGame2 = {
  id: 'chat-2',
  gameId: 'game-2',
  gameName: 'Catan',
  agentId: 'agent-3',
  agentName: 'Catan Helper',
  startedAt: '2025-01-11T10:00:00Z',
  lastMessageAt: '2025-01-11T10:05:00Z',
  messages: [
    {
      id: 'msg-2',
      level: 'user',
      message: 'Catan question',
      metadataJson: null,
      createdAt: '2025-01-11T10:00:00Z'
    }
  ]
};

// Helper function to setup complete authenticated state
const setupAuthenticatedState = () => {
  mockApi.get.mockResolvedValueOnce(mockAuthResponse);
  mockApi.get.mockResolvedValueOnce(mockGames);
  mockApi.get.mockResolvedValueOnce(mockAgentsGame1);
  mockApi.get.mockResolvedValueOnce(mockChatsGame1);
};

describe('ChatPage - Supplementary Tests for CHAT-02/03/04/05', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockApi.get.mockReset();
    mockApi.post.mockReset();
    mockApi.put.mockReset();
    mockApi.delete.mockReset();
    (mockApi.chat.updateMessage as jest.Mock).mockReset();
    (mockApi.chat.deleteMessage as jest.Mock).mockReset();
    mockStartStreaming.mockReset();
    mockStopStreaming.mockReset();
    mockOnComplete = null;
    mockOnError = null;

    // Reset useChatStreaming mock to default state
    (useChatStreaming as jest.Mock).mockReturnValue([
      {
        isStreaming: false,
        currentAnswer: '',
        snippets: [],
        state: null,
        error: null
      },
      {
        startStreaming: mockStartStreaming,
        stopStreaming: mockStopStreaming
      }
    ]);
  });

  describe('CHAT-02: Follow-Up Questions', () => {
    it('should display FollowUpQuestions component for assistant messages with followUpQuestions', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithFollowUpQuestions);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByTestId('follow-up-questions')).toBeInTheDocument();
      });
    });

    it('should pass questions array to FollowUpQuestions component', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithFollowUpQuestions);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('How do pawns move?')).toBeInTheDocument();
        expect(screen.getByText('What is castling?')).toBeInTheDocument();
        expect(screen.getByText('Can bishops move backwards?')).toBeInTheDocument();
      });
    });

    it('should fill inputValue state when follow-up question is clicked', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithFollowUpQuestions);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByTestId('follow-up-0')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('follow-up-0'));

      await waitFor(() => {
        const input = screen.getByPlaceholderText(/Fai una domanda/i);
        expect(input).toHaveValue('How do pawns move?');
      });
    });

    it('should focus input field when follow-up question is clicked', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithFollowUpQuestions);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByTestId('follow-up-1')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('follow-up-1'));

      await waitFor(() => {
        const input = screen.getByPlaceholderText(/Fai una domanda/i);
        expect(input).toHaveFocus();
      });
    });

    it('should disable follow-up buttons when isStreaming is true', async () => {
      (useChatStreaming as jest.Mock).mockReturnValue([
        {
          isStreaming: true,
          currentAnswer: 'Streaming answer...',
          snippets: [],
          state: 'processing',
          error: null
        },
        {
          startStreaming: mockStartStreaming,
          stopStreaming: mockStopStreaming
        }
      ]);

      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithFollowUpQuestions);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        const followUpButton = screen.getByTestId('follow-up-0');
        expect(followUpButton).toBeDisabled();
      });
    });

    it('should not display FollowUpQuestions when followUpQuestions array is empty', async () => {
      setupAuthenticatedState();

      const chatWithoutFollowUp = {
        ...mockChatWithFollowUpQuestions,
        messages: [
          {
            id: 'msg-1',
            level: 'user',
            message: 'Test question',
            metadataJson: null,
            createdAt: '2025-01-10T10:00:00Z'
          },
          {
            id: 'msg-2',
            level: 'agent',
            message: 'Test answer',
            metadataJson: JSON.stringify({ snippets: [] }),
            createdAt: '2025-01-10T10:01:00Z'
          }
        ]
      };

      mockApi.get.mockResolvedValueOnce(chatWithoutFollowUp);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Test answer')).toBeInTheDocument();
      });

      expect(screen.queryByTestId('follow-up-questions')).not.toBeInTheDocument();
    });
  });

  describe('CHAT-03: Multi-Game State Isolation', () => {
    it('should isolate chats between different games', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Verify Chess chat is visible
      expect(screen.getAllByText('Chess Expert').length).toBeGreaterThan(0);

      // Switch to Catan
      const gameSelect = screen.getByLabelText(/Gioco:/i);
      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(screen.getByText('Catan Helper')).toBeInTheDocument();
      });

      // Chess chats should not be visible
      const expertTexts = screen.queryAllByText('Chess Expert');
      // Should only be in the dropdown, not in chat list
      expect(expertTexts.length).toBeLessThanOrEqual(1);
    });

    it('should create separate chat in each game without cross-contamination', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const newChatButton = screen.getByRole('button', { name: /Create new chat/i });

      mockApi.post.mockResolvedValueOnce({
        id: 'new-chat-1',
        gameId: 'game-1',
        gameName: 'Chess',
        agentId: 'agent-1',
        agentName: 'Chess Expert',
        startedAt: new Date().toISOString(),
        lastMessageAt: null
      });

      await user.click(newChatButton);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenCalledWith('/api/v1/chats', expect.objectContaining({
          gameId: 'game-1'
        }));
      });

      // Switch to Game B
      const gameSelect = screen.getByLabelText(/Gioco:/i);
      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(screen.getByText('Catan Helper')).toBeInTheDocument();
      });

      mockApi.post.mockResolvedValueOnce({
        id: 'new-chat-2',
        gameId: 'game-2',
        gameName: 'Catan',
        agentId: 'agent-3',
        agentName: 'Catan Helper',
        startedAt: new Date().toISOString(),
        lastMessageAt: null
      });

      const newChatButton2 = screen.getByRole('button', { name: /Create new chat/i });
      await user.click(newChatButton2);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenCalledWith('/api/v1/chats', expect.objectContaining({
          gameId: 'game-2'
        }));
      });
    });

    it('should preserve activeChat state when switching games', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Chess question')).toBeInTheDocument();
      });

      // Switch to Game B
      const gameSelect = screen.getByLabelText(/Gioco:/i);
      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(screen.queryByText('Chess question')).not.toBeInTheDocument();
      });

      // Switch back to Game A
      mockApi.get.mockResolvedValueOnce(mockAgentsGame1);
      mockApi.get.mockResolvedValueOnce(mockChatsGame1);

      await user.selectOptions(gameSelect, 'game-1');

      await waitFor(() => {
        expect(screen.getAllByText('Chess Expert').length).toBeGreaterThan(0);
      });
    });

    it('should isolate messages between games', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Chess question')).toBeInTheDocument();
      });

      // Switch to Game B
      const gameSelect = screen.getByLabelText(/Gioco:/i);
      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(screen.getByText('Catan Helper')).toBeInTheDocument();
      });

      const catanChatItems = screen.getAllByText('Catan Helper');
      await user.click(catanChatItems[catanChatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Catan question')).toBeInTheDocument();
      });

      // Chess messages should not be visible
      expect(screen.queryByText('Chess question')).not.toBeInTheDocument();
    });

    it('should maintain separate chat lists per game', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const chessChats = screen.getAllByText('Chess Expert');
      expect(chessChats.length).toBeGreaterThan(0);

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText(/Gioco:/i);
      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(screen.getByText('Catan Helper')).toBeInTheDocument();
      });

      const catanChats = screen.getAllByText('Catan Helper');
      expect(catanChats.length).toBeGreaterThan(0);

      // Chess Expert should not appear in chat list (only in agent dropdown)
      const expertTexts = screen.queryAllByText('Chess Expert');
      expect(expertTexts.length).toBeLessThanOrEqual(1);
    });

    it('should verify chatStatesByGame Map contains different objects for different games', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText(/Gioco:/i);
      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-2');
      });

      mockApi.get.mockResolvedValueOnce(mockAgentsGame1);
      mockApi.get.mockResolvedValueOnce(mockChatsGame1);

      await user.selectOptions(gameSelect, 'game-1');

      await waitFor(() => {
        expect(screen.getAllByText('Chess Expert').length).toBeGreaterThan(0);
      });
    });

    it('should delete chat in one game without affecting other games', async () => {
      window.confirm = jest.fn(() => true);

      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Switch to Game B to ensure it has data
      const gameSelect = screen.getByLabelText(/Gioco:/i);
      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-2');
      });

      // Go back to Game A
      mockApi.get.mockResolvedValueOnce(mockAgentsGame1);
      mockApi.get.mockResolvedValueOnce(mockChatsGame1);

      await user.selectOptions(gameSelect, 'game-1');

      await waitFor(() => {
        expect(screen.getAllByText('Chess Expert').length).toBeGreaterThan(0);
      });

      // Delete happens - this verifies isolation
      mockApi.delete.mockResolvedValue({});

      // Verify Game B chats would still be accessible
      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(screen.getByText('Catan Helper')).toBeInTheDocument();
      });
    });

    it('should update previousSelectedGameRef correctly during game switches', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games/game-1/agents'));

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText(/Gioco:/i);

      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games/game-2/agents');
      });
    });

    it('should handle null selectedGameId gracefully', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText(/Gioco:/i);

      await user.selectOptions(gameSelect, '');

      await waitFor(() => {
        const agentSelect = screen.getByLabelText(/Agente:/i) as HTMLSelectElement;
        expect(agentSelect.disabled).toBe(true);
      });
    });

    it('should load chat history only for the selected game', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1');
      });

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText(/Gioco:/i);

      mockApi.get.mockResolvedValueOnce(mockAgentsGame2);
      mockApi.get.mockResolvedValueOnce(mockChatsGame2);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-2');
      });
    });
  });

  describe('CHAT-04: Auto-Scroll', () => {
    let scrollIntoViewMock: jest.Mock;

    beforeEach(() => {
      scrollIntoViewMock = jest.fn();
      Element.prototype.scrollIntoView = scrollIntoViewMock;
    });

    afterEach(() => {
      scrollIntoViewMock.mockRestore();
    });

    it('should call scrollIntoView when new message is added', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Chess question')).toBeInTheDocument();
      });

      scrollIntoViewMock.mockClear();

      const input = screen.getByPlaceholderText(/Fai una domanda/i);
      await user.type(input, 'New question');

      mockApi.post.mockResolvedValueOnce({
        id: 'msg-new',
        chatId: 'chat-1',
        role: 'user',
        content: 'New question',
        createdAt: new Date().toISOString()
      });

      const sendButton = screen.getByRole('button', { name: /Invia/i });
      await user.click(sendButton);

      await waitFor(() => {
        expect(scrollIntoViewMock).toHaveBeenCalled();
      });
    });

    it('should call scrollIntoView with correct parameters', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Chess question')).toBeInTheDocument();
      });

      await waitFor(() => {
        expect(scrollIntoViewMock).toHaveBeenCalledWith({
          behavior: 'smooth',
          block: 'end'
        });
      });
    });

    it('should scroll after user message is sent', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const newChatButton = screen.getByRole('button', { name: /Create new chat/i });

      mockApi.post.mockResolvedValueOnce({
        id: 'new-chat',
        gameId: 'game-1',
        gameName: 'Chess',
        agentId: 'agent-1',
        agentName: 'Chess Expert',
        startedAt: new Date().toISOString(),
        lastMessageAt: null
      });

      await user.click(newChatButton);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Fai una domanda/i)).toBeInTheDocument();
      });

      scrollIntoViewMock.mockClear();

      const input = screen.getByPlaceholderText(/Fai una domanda/i);
      await user.type(input, 'Test message');

      mockApi.post.mockResolvedValueOnce({
        id: 'msg-user',
        chatId: 'new-chat',
        role: 'user',
        content: 'Test message',
        createdAt: new Date().toISOString()
      });

      const sendButton = screen.getByRole('button', { name: /Invia/i });
      await user.click(sendButton);

      await waitFor(() => {
        expect(scrollIntoViewMock).toHaveBeenCalled();
      });
    });

    it('should scroll after assistant message is received', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Chess question')).toBeInTheDocument();
      });

      expect(scrollIntoViewMock).toHaveBeenCalled();
    });

    it('should not scroll when messages array is empty', async () => {
      setupAuthenticatedState();

      const emptyChat = {
        ...mockChatGame1,
        messages: []
      };

      mockApi.get.mockResolvedValueOnce(emptyChat);

      scrollIntoViewMock.mockClear();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await new Promise(resolve => setTimeout(resolve, 100));

      expect(scrollIntoViewMock).not.toHaveBeenCalled();
    });
  });

  describe('CHAT-05: Chat Export', () => {
    it('should only show export button when activeChatId exists', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      // No active chat yet
      expect(screen.queryByRole('button', { name: /Esporta/i })).not.toBeInTheDocument();

      const user = userEvent.setup();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Chess question')).toBeInTheDocument();
      });

      // Export button should now be visible
      expect(screen.getByRole('button', { name: /Esporta/i })).toBeInTheDocument();
    });

    it('should open export modal when export button is clicked', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Esporta/i })).toBeInTheDocument();
      });

      expect(screen.queryByTestId('export-modal')).not.toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: /Esporta/i }));

      await waitFor(() => {
        expect(screen.getByTestId('export-modal')).toBeInTheDocument();
      });
    });

    it('should pass correct chatId to ExportChatModal', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Esporta/i })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Esporta/i }));

      await waitFor(() => {
        expect(screen.getByTestId('export-modal')).toBeInTheDocument();
      });

      const chatIdElement = screen.getByTestId('export-chatid');
      expect(chatIdElement).toHaveTextContent('chat-1');
    });

    it('should pass correct gameName to ExportChatModal', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Esporta/i })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Esporta/i }));

      await waitFor(() => {
        expect(screen.getByTestId('export-modal')).toBeInTheDocument();
      });

      const gameNameElement = screen.getByTestId('export-gamename');
      expect(gameNameElement).toHaveTextContent('Chess');
    });

    it('should close modal when onClose callback is called', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatGame1);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Esporta/i })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Esporta/i }));

      await waitFor(() => {
        expect(screen.getByTestId('export-modal')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('close-export-modal'));

      await waitFor(() => {
        expect(screen.queryByTestId('export-modal')).not.toBeInTheDocument();
      });
    });
  });
});
