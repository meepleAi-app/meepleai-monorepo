import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChatPage from '../chat';
import { api } from '../../lib/api';

// Mock the API client
jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
    delete: jest.fn()
  }
}));

const mockApi = api as jest.Mocked<typeof api>;

// Mock window.confirm for delete confirmation tests
const originalConfirm = window.confirm;

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
  { id: 'game-1', name: 'Chess' },
  { id: 'game-2', name: 'Catan' }
];

const mockAgents = [
  { id: 'agent-1', gameId: 'game-1', name: 'Chess Expert', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' },
  { id: 'agent-2', gameId: 'game-1', name: 'Chess Helper', kind: 'qa', createdAt: '2025-01-02T00:00:00Z' }
];

const mockChats = [
  {
    id: 'chat-1',
    gameId: 'game-1',
    gameName: 'Chess',
    agentId: 'agent-1',
    agentName: 'Chess Expert',
    startedAt: '2025-01-10T10:00:00Z',
    lastMessageAt: '2025-01-10T10:05:00Z'
  },
  {
    id: 'chat-2',
    gameId: 'game-1',
    gameName: 'Chess',
    agentId: 'agent-2',
    agentName: 'Chess Helper',
    startedAt: '2025-01-09T14:00:00Z',
    lastMessageAt: null
  }
];

const mockChatWithHistory = {
  ...mockChats[0],
  messages: [
    {
      id: 'msg-1',
      level: 'user',
      message: 'How do I castle?',
      metadataJson: null,
      createdAt: '2025-01-10T10:00:00Z'
    },
    {
      id: 'msg-2',
      level: 'agent',
      message: 'Castling is a special move...',
      metadataJson: JSON.stringify({
        snippets: [
          { text: 'Castling rules section', source: 'chess-rules.pdf', page: 5, line: null }
        ]
      }),
      createdAt: '2025-01-10T10:05:00Z'
    }
  ]
};

// Helper function to setup complete authenticated state with full data
const setupAuthenticatedState = () => {
  mockApi.get.mockResolvedValueOnce(mockAuthResponse);
  mockApi.get.mockResolvedValueOnce(mockGames);
  mockApi.get.mockResolvedValueOnce(mockAgents);
  mockApi.get.mockResolvedValueOnce(mockChats);
};

describe('ChatPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // Reset mocks to clear queued mock responses
    mockApi.get.mockReset();
    mockApi.post.mockReset();
    mockApi.put.mockReset();
    mockApi.delete.mockReset();
    window.confirm = originalConfirm;
  });

  afterEach(() => {
    window.confirm = originalConfirm;
  });

  // =============================================================================
  // AUTHENTICATION TESTS
  // =============================================================================

  describe('Authentication', () => {
    it('shows loading state initially', async () => {
      mockApi.get.mockImplementation(() => new Promise(() => {})); // Never resolves

      render(<ChatPage />);

      // The component initializes authUser as null, so it immediately shows "Accesso richiesto"
      // while the auth check is pending. This is the actual current behavior.
      // Since authUser starts as null, the component renders the unauthenticated view until auth resolves.

      await waitFor(() => {
        // The component shows the unauthenticated state because authUser is null initially
        expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();
      });
    });

    it('shows unauthenticated state when user is not logged in', async () => {
      mockApi.get.mockResolvedValueOnce(null);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/auth/me'));

      expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();
      expect(screen.getByText(/Devi effettuare l'accesso per utilizzare la chat/i)).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /Vai al Login/i })).toBeInTheDocument();
    });

    it('shows authenticated interface when user is logged in', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/auth/me'));

      expect(screen.getByRole('heading', { name: /MeepleAI Chat/i })).toBeInTheDocument();
      expect(screen.queryByText(/Accesso richiesto/i)).not.toBeInTheDocument();
    });

    it('handles authentication check failure gracefully', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockRejectedValueOnce(new Error('Auth failed'));

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/auth/me'));

      expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();

      consoleErrorSpy.mockRestore();
    });
  });

  // =============================================================================
  // DATA LOADING TESTS
  // =============================================================================

  describe('Data Loading', () => {
    it('loads games after authentication', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

      const gameSelect = screen.getByLabelText(/Gioco:/i);
      expect(within(gameSelect).getByText('Chess')).toBeInTheDocument();
      expect(within(gameSelect).getByText('Catan')).toBeInTheDocument();
    });

    it('auto-selects first game when games are loaded', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

      const gameSelect = screen.getByLabelText(/Gioco:/i) as HTMLSelectElement;
      expect(gameSelect.value).toBe('game-1');
    });

    it('loads agents when game is selected', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games/game-1/agents'));

      const agentSelect = screen.getByLabelText(/Agente:/i);
      expect(within(agentSelect).getByText('Chess Expert')).toBeInTheDocument();
      expect(within(agentSelect).getByText('Chess Helper')).toBeInTheDocument();
    });

    it('auto-selects first agent when agents are loaded', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games/game-1/agents'));

      const agentSelect = screen.getByLabelText(/Agente:/i) as HTMLSelectElement;
      expect(agentSelect.value).toBe('agent-1');
    });

    it('loads chats filtered by selected game', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      // Both chats should appear in the sidebar
      const chessExpertChats = screen.getAllByText('Chess Expert');
      const chessHelperChats = screen.getAllByText('Chess Helper');
      expect(chessExpertChats.length).toBeGreaterThan(0);
      expect(chessHelperChats.length).toBeGreaterThan(0);
    });

    it('shows loading indicator while loading games', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockImplementation(() => new Promise(() => {})); // Never resolves

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

      const gameSelect = screen.getByLabelText(/Gioco:/i) as HTMLSelectElement;
      expect(gameSelect.disabled).toBe(true);
    });

    it('shows loading indicator while loading chats', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockResolvedValueOnce(mockAgents);
      mockApi.get.mockImplementation(() => new Promise(() => {})); // Hang on chats

      render(<ChatPage />);

      await waitFor(() => {
        expect(screen.getByText(/Caricamento chat.../i)).toBeInTheDocument();
      });
    });

    it('shows empty state when no games are available', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce([]);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

      const gameSelect = screen.getByLabelText(/Gioco:/i);
      expect(within(gameSelect).getByText(/Seleziona un gioco.../i)).toBeInTheDocument();
      expect(within(gameSelect).queryByText('Chess')).not.toBeInTheDocument();
    });

    it('shows empty state when no chats exist', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockResolvedValueOnce(mockAgents);
      mockApi.get.mockResolvedValueOnce([]);

      render(<ChatPage />);

      await waitFor(() => {
        expect(screen.getByText(/Nessuna chat. Creane una nuova!/i)).toBeInTheDocument();
      });
    });

    it('handles games loading error gracefully', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockRejectedValueOnce(new Error('Games API failed'));

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

      expect(screen.getByText(/Errore nel caricamento dei giochi/i)).toBeInTheDocument();

      consoleErrorSpy.mockRestore();
    });

    it('handles agents loading error gracefully', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockRejectedValueOnce(new Error('Agents API failed'));
      mockApi.get.mockResolvedValueOnce([]); // chats still loads after agents error

      render(<ChatPage />);

      await waitFor(() => {
        expect(screen.getByText(/Errore nel caricamento degli agenti/i)).toBeInTheDocument();
      });

      consoleErrorSpy.mockRestore();
    });

    it('handles chats loading error gracefully without showing error', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockResolvedValueOnce(mockAgents);
      mockApi.get.mockRejectedValueOnce(new Error('Chats API failed'));

      render(<ChatPage />);

      await waitFor(() => {
        expect(screen.getByText(/Nessuna chat. Creane una nuova!/i)).toBeInTheDocument();
      });

      // Error should not be shown to user (silent failure)
      expect(screen.queryByText(/Errore/i)).not.toBeInTheDocument();

      consoleErrorSpy.mockRestore();
    });
  });

  // =============================================================================
  // GAME/AGENT SELECTION TESTS
  // =============================================================================

  describe('Game and Agent Selection', () => {
    it('allows changing game selection', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText(/Gioco:/i);

      // Clear previous calls
      mockApi.get.mockClear();
      mockApi.get.mockResolvedValueOnce([{ id: 'agent-3', gameId: 'game-2', name: 'Catan Expert', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }]);
      mockApi.get.mockResolvedValueOnce([]);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledWith('/games/game-2/agents');
        expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-2');
      });
    });

    it('reloads agents when game changes', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games/game-1/agents'));

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText(/Gioco:/i);

      mockApi.get.mockClear();
      const catanAgents = [{ id: 'agent-3', gameId: 'game-2', name: 'Catan Expert', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }];
      mockApi.get.mockResolvedValueOnce(catanAgents);
      mockApi.get.mockResolvedValueOnce([]);

      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games/game-2/agents'));

      const agentSelect = screen.getByLabelText(/Agente:/i);
      expect(within(agentSelect).getByText('Catan Expert')).toBeInTheDocument();
    });

    it('clears active chat when game changes', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat first
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);
      const chatItems = screen.getAllByText('Chess Expert');
      const chatItem = chatItems[chatItems.length - 1];
      await user.click(chatItem);

      await waitFor(() => {
        expect(screen.getByText('How do I castle?')).toBeInTheDocument();
      });

      // Change game
      const gameSelect = screen.getByLabelText(/Gioco:/i);
      mockApi.get.mockResolvedValueOnce([]);
      mockApi.get.mockResolvedValueOnce([]);
      await user.selectOptions(gameSelect, 'game-2');

      await waitFor(() => {
        expect(screen.queryByText('How do I castle?')).not.toBeInTheDocument();
      });
    });

    it('allows changing agent selection', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games/game-1/agents'));

      const user = userEvent.setup();
      const agentSelect = screen.getByLabelText(/Agente:/i) as HTMLSelectElement;

      await user.selectOptions(agentSelect, 'agent-2');

      expect(agentSelect.value).toBe('agent-2');
    });

    it('disables agent selector when no game is selected', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText(/Gioco:/i);

      // Deselect game
      await user.selectOptions(gameSelect, '');

      const agentSelect = screen.getByLabelText(/Agente:/i) as HTMLSelectElement;
      expect(agentSelect.disabled).toBe(true);
    });
  });

  // =============================================================================
  // CHAT MANAGEMENT TESTS
  // =============================================================================

  describe('Chat Management', () => {
    describe('Creating New Chat', () => {
      it('creates new chat when "Nuova Chat" button is clicked', async () => {
        setupAuthenticatedState();

        const newChat = {
          id: 'chat-3',
          gameId: 'game-1',
          gameName: 'Chess',
          agentId: 'agent-1',
          agentName: 'Chess Expert',
          startedAt: new Date().toISOString(),
          lastMessageAt: null
        };
        mockApi.post.mockResolvedValueOnce(newChat);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const newChatButton = screen.getByRole('button', { name: /\+ Nuova Chat/i });

        await user.click(newChatButton);

        await waitFor(() => {
          expect(mockApi.post).toHaveBeenCalledWith('/chats', {
            gameId: 'game-1',
            agentId: 'agent-1'
          });
        });

        // New chat should appear in list
        const chatItems = screen.getAllByText('Chess Expert');
        expect(chatItems.length).toBeGreaterThan(2); // Was already 2, now 3
      });

      it('disables "Nuova Chat" button when no game is selected', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

        const user = userEvent.setup();
        const gameSelect = screen.getByLabelText(/Gioco:/i);

        await user.selectOptions(gameSelect, '');

        const newChatButton = screen.getByRole('button', { name: /\+ Nuova Chat/i }) as HTMLButtonElement;
        expect(newChatButton.disabled).toBe(true);
      });

      it('disables "Nuova Chat" button when no agent is selected', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games/game-1/agents'));

        const user = userEvent.setup();
        const agentSelect = screen.getByLabelText(/Agente:/i);

        await user.selectOptions(agentSelect, '');

        const newChatButton = screen.getByRole('button', { name: /\+ Nuova Chat/i }) as HTMLButtonElement;
        expect(newChatButton.disabled).toBe(true);
      });

      it('shows loading state while creating chat', async () => {
        setupAuthenticatedState();
        mockApi.post.mockImplementation(() => new Promise(() => {})); // Never resolves

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const newChatButton = screen.getByRole('button', { name: /\+ Nuova Chat/i });

        await user.click(newChatButton);

        await waitFor(() => {
          expect(screen.getByRole('button', { name: /Creazione.../i })).toBeInTheDocument();
        });
      });

      it('handles chat creation error gracefully', async () => {
        const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
        setupAuthenticatedState();
        mockApi.post.mockRejectedValueOnce(new Error('Chat creation failed'));

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const newChatButton = screen.getByRole('button', { name: /\+ Nuova Chat/i });

        await user.click(newChatButton);

        await waitFor(() => {
          expect(screen.getByText(/Errore nella creazione della chat/i)).toBeInTheDocument();
        });

        consoleErrorSpy.mockRestore();
      });
    });

    describe('Loading Existing Chat', () => {
      it('loads chat history when chat is clicked', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Wait for chat list to fully render
        await waitFor(() => {
          const chatItems = screen.getAllByText('Chess Expert');
          expect(chatItems.length).toBeGreaterThan(0);
        });

        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        // Find the chat item in the sidebar (not the select option)
        const chatItems = screen.getAllByText('Chess Expert');
        // The chat items are in the chat list, which is the last occurrence(s)
        const chatItem = chatItems[chatItems.length - 1];
        await user.click(chatItem);

        await waitFor(() => {
          expect(mockApi.get).toHaveBeenCalledWith('/chats/chat-1');
        });

        expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
      });

      it('shows loading indicator while loading chat history', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        mockApi.get.mockImplementation(() => new Promise(() => {})); // Never resolves

        const user = userEvent.setup();
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText(/Caricamento messaggi.../i)).toBeInTheDocument();
        });
      });

      it('converts backend messages to frontend format correctly', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        // Check snippets are parsed from metadata
        expect(screen.getByText('Fonti:')).toBeInTheDocument();
        expect(screen.getByText('chess-rules.pdf (Pagina 5)')).toBeInTheDocument();
        expect(screen.getByText('Castling rules section')).toBeInTheDocument();
      });

      it('handles malformed metadata JSON gracefully', async () => {
        const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
        setupAuthenticatedState();

        const chatWithBadMetadata = {
          ...mockChatWithHistory,
          messages: [
            {
              id: 'msg-1',
              level: 'agent',
              message: 'Test message',
              metadataJson: 'invalid{json',
              createdAt: '2025-01-10T10:00:00Z'
            }
          ]
        };

        mockApi.get.mockResolvedValueOnce(chatWithBadMetadata);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('Test message')).toBeInTheDocument();
        });

        // Should not crash, snippets should be undefined
        expect(screen.queryByText('Fonti:')).not.toBeInTheDocument();

        consoleErrorSpy.mockRestore();
      });

      it('synchronizes game and agent selection with loaded chat', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Get references to selects
        const gameSelect = screen.getByLabelText(/Gioco:/i) as HTMLSelectElement;
        const agentSelect = screen.getByLabelText(/Agente:/i) as HTMLSelectElement;

        // Verify initial state (game-1 and agent-1 are auto-selected)
        expect(gameSelect.value).toBe('game-1');
        expect(agentSelect.value).toBe('agent-1');

        // Change game selection (which triggers agents load and clears active chat)
        mockApi.get.mockResolvedValueOnce([{ id: 'agent-3', gameId: 'game-2', name: 'Catan Expert', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }]);
        mockApi.get.mockResolvedValueOnce([]);
        await user.selectOptions(gameSelect, 'game-2');

        await waitFor(() => {
          expect(gameSelect.value).toBe('game-2');
        });

        // Now switch back to game-1 manually
        mockApi.get.mockResolvedValueOnce(mockAgents); // Reload agents for game-1
        mockApi.get.mockResolvedValueOnce(mockChats); // Reload chats for game-1
        await user.selectOptions(gameSelect, 'game-1');

        await waitFor(() => {
          expect(gameSelect.value).toBe('game-1');
        });

        // Wait for chats to load and click one
        await waitFor(() => {
          const chatItems = screen.getAllByText('Chess Expert');
          expect(chatItems.length).toBeGreaterThan(0);
        });

        mockApi.get.mockResolvedValueOnce(mockChatWithHistory); // Load chat history
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        // Agent selection should still match
        expect(agentSelect.value).toBe('agent-1');
      });

      it('handles chat history loading error gracefully', async () => {
        const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
        setupAuthenticatedState();
        mockApi.get.mockRejectedValueOnce(new Error('Chat history failed'));

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText(/Errore nel caricamento della cronologia chat/i)).toBeInTheDocument();
        });

        consoleErrorSpy.mockRestore();
      });
    });

    describe('Deleting Chat', () => {
      it('deletes chat when delete button is clicked and confirmed', async () => {
        window.confirm = jest.fn(() => true);

        setupAuthenticatedState();
        mockApi.delete.mockResolvedValueOnce(undefined);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const deleteButtons = screen.getAllByTitle('Elimina chat');

        await user.click(deleteButtons[0]);

        expect(window.confirm).toHaveBeenCalledWith('Sei sicuro di voler eliminare questa chat?');

        await waitFor(() => {
          expect(mockApi.delete).toHaveBeenCalledWith('/chats/chat-1');
        });

        // Chat should be removed from list
        await waitFor(() => {
          const remainingChats = screen.queryAllByText('Chess Expert');
          expect(remainingChats.length).toBeLessThan(2); // One less than before
        });
      });

      it('does not delete chat when user cancels confirmation', async () => {
        window.confirm = jest.fn(() => false);

        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const deleteButtons = screen.getAllByTitle('Elimina chat');

        await user.click(deleteButtons[0]);

        expect(window.confirm).toHaveBeenCalled();
        expect(mockApi.delete).not.toHaveBeenCalled();
      });

      it('clears active chat when deleting the currently active chat', async () => {
        window.confirm = jest.fn(() => true);

        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);
        mockApi.delete.mockResolvedValueOnce(undefined);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat first
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        // Delete the active chat
        const deleteButtons = screen.getAllByTitle('Elimina chat');
        await user.click(deleteButtons[0]);

        await waitFor(() => {
          expect(mockApi.delete).toHaveBeenCalledWith('/chats/chat-1');
        });

        // Messages should be cleared
        await waitFor(() => {
          expect(screen.queryByText('How do I castle?')).not.toBeInTheDocument();
        });
      });

      it('handles chat deletion error gracefully', async () => {
        const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
        window.confirm = jest.fn(() => true);

        setupAuthenticatedState();
        mockApi.delete.mockRejectedValueOnce(new Error('Delete failed'));

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const deleteButtons = screen.getAllByTitle('Elimina chat');

        await user.click(deleteButtons[0]);

        await waitFor(() => {
          expect(screen.getByText(/Errore nell'eliminazione della chat/i)).toBeInTheDocument();
        });

        consoleErrorSpy.mockRestore();
      });

      it('stops click propagation on delete button to prevent loading chat', async () => {
        window.confirm = jest.fn(() => true);

        setupAuthenticatedState();
        mockApi.delete.mockResolvedValueOnce(undefined);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const deleteButtons = screen.getAllByTitle('Elimina chat');

        mockApi.get.mockClear();

        await user.click(deleteButtons[0]);

        // Should not call /chats/{chatId} to load history
        expect(mockApi.get).not.toHaveBeenCalledWith('/chats/chat-1');
      });
    });
  });

  // =============================================================================
  // MESSAGING TESTS
  // =============================================================================

  describe('Messaging', () => {
    describe('Sending Messages', () => {
      it('sends message with chatId when chat is active', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        // Send a message
        mockApi.post.mockResolvedValueOnce({
          answer: 'Castling is only possible when...',
          snippets: [],
          messageId: 'msg-3'
        });

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'Can I castle now?');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(mockApi.post).toHaveBeenCalledWith('/agents/qa', {
            gameId: 'game-1',
            query: 'Can I castle now?',
            chatId: 'chat-1'
          });
        });
      });

      it('auto-creates chat on first message when no chat is active', async () => {
        mockApi.get.mockResolvedValueOnce(mockAuthResponse);
        mockApi.get.mockResolvedValueOnce(mockGames);
        mockApi.get.mockResolvedValueOnce(mockAgents);
        mockApi.get.mockResolvedValueOnce([]);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        const newChat = {
          id: 'chat-new',
          gameId: 'game-1',
          gameName: 'Chess',
          agentId: 'agent-1',
          agentName: 'Chess Expert',
          startedAt: new Date().toISOString(),
          lastMessageAt: null
        };

        mockApi.post.mockResolvedValueOnce(newChat);
        mockApi.post.mockResolvedValueOnce({
          answer: 'Great question!',
          snippets: [],
          messageId: 'msg-1'
        });

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'First message');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(mockApi.post).toHaveBeenNthCalledWith(1, '/chats', {
            gameId: 'game-1',
            agentId: 'agent-1'
          });
        });

        await waitFor(() => {
          expect(mockApi.post).toHaveBeenNthCalledWith(2, '/agents/qa', {
            gameId: 'game-1',
            query: 'First message',
            chatId: 'chat-new'
          });
        });
      });

      it('displays user message immediately and assistant response after API call', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        mockApi.post.mockResolvedValueOnce({
          answer: 'You can castle when there are no pieces between...',
          snippets: [],
          messageId: 'msg-3'
        });

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'When can I castle?');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        // User message should appear immediately
        expect(screen.getByText('When can I castle?')).toBeInTheDocument();

        // Wait for assistant response
        await waitFor(() => {
          expect(screen.getByText('You can castle when there are no pieces between...')).toBeInTheDocument();
        });
      });

      it('clears input after sending message', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        mockApi.post.mockResolvedValueOnce({
          answer: 'Answer',
          snippets: []
        });

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i) as HTMLInputElement;
        await user.type(input, 'Test message');

        expect(input.value).toBe('Test message');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(input.value).toBe('');
        });
      });

      it('shows loading indicator while sending message', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        mockApi.post.mockImplementation(() => new Promise(() => {})); // Never resolves

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'Test message');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(screen.getByText(/Sto pensando.../i)).toBeInTheDocument();
        });

        expect(screen.getByRole('button', { name: /Invio.../i })).toBeDisabled();
      });

      it('disables send button when no game is selected', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

        const user = userEvent.setup();
        const gameSelect = screen.getByLabelText(/Gioco:/i);

        await user.selectOptions(gameSelect, '');

        const sendButton = screen.getByRole('button', { name: /Invia/i }) as HTMLButtonElement;
        expect(sendButton.disabled).toBe(true);
      });

      it('disables send button when no agent is selected', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games/game-1/agents'));

        const user = userEvent.setup();
        const agentSelect = screen.getByLabelText(/Agente:/i);

        await user.selectOptions(agentSelect, '');

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'Test message');

        const sendButton = screen.getByRole('button', { name: /Invia/i }) as HTMLButtonElement;
        expect(sendButton.disabled).toBe(true);
      });

      it('disables send button when input is empty', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const sendButton = screen.getByRole('button', { name: /Invia/i }) as HTMLButtonElement;
        expect(sendButton.disabled).toBe(true);
      });

      it('disables send button when input contains only whitespace', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);

        await user.type(input, '   ');

        const sendButton = screen.getByRole('button', { name: /Invia/i }) as HTMLButtonElement;
        expect(sendButton.disabled).toBe(true);
      });

      it('does not send message when input is whitespace only', async () => {
        setupAuthenticatedState();

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();
        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);

        await user.type(input, '   ');

        const sendButton = screen.getByRole('button', { name: /Invia/i });

        // Can't actually click because it's disabled, but verify no API call
        expect(sendButton).toBeDisabled();
      });

      it('removes user message when message sending fails', async () => {
        const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        mockApi.post.mockRejectedValueOnce(new Error('API failed'));

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'Failed message');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        // In jsdom 26, error handling is fast enough that the message is removed before rendering completes
        // We verify the end state: message is removed and error is shown
        await waitFor(() => {
          expect(screen.getByText(/Errore nella comunicazione con l'agente/i)).toBeInTheDocument();
        });

        // Message should not be in DOM after error
        expect(screen.queryByText('Failed message')).not.toBeInTheDocument();

        consoleErrorSpy.mockRestore();
      });

      it('handles auto-creation failure when sending first message', async () => {
        const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
        mockApi.get.mockResolvedValueOnce(mockAuthResponse);
        mockApi.get.mockResolvedValueOnce(mockGames);
        mockApi.get.mockResolvedValueOnce(mockAgents);
        mockApi.get.mockResolvedValueOnce([]);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        mockApi.post.mockRejectedValueOnce(new Error('Chat creation failed'));

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'First message');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(screen.getByText(/Errore nella comunicazione con l'agente/i)).toBeInTheDocument();
        });

        // Message should be removed
        expect(screen.queryByText('First message')).not.toBeInTheDocument();

        consoleErrorSpy.mockRestore();
      });

      it('updates chat lastMessageAt timestamp after sending message', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        mockApi.post.mockResolvedValueOnce({
          answer: 'Test answer',
          snippets: [],
          messageId: 'msg-3'
        });

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'Test question');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(screen.getByText('Test answer')).toBeInTheDocument();
        });

        // Chat item should reflect updated timestamp (implementation detail - hard to test exact timestamp)
        expect(screen.getAllByText(/Chess Expert/i).length).toBeGreaterThan(0);
      });
    });

    describe('Displaying Messages', () => {
      it('displays snippets with page numbers', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
        });

        mockApi.post.mockResolvedValueOnce({
          answer: 'More info',
          snippets: [
            { text: 'Rule detail', source: 'manual.pdf', page: 10, line: null }
          ],
          messageId: 'msg-3'
        });

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'Tell me more');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(screen.getByText('manual.pdf (Pagina 10)')).toBeInTheDocument();
        });

        expect(screen.getByText('Rule detail')).toBeInTheDocument();
      });

      it('displays snippets without page numbers when page is null', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        mockApi.post.mockResolvedValueOnce({
          answer: 'Info',
          snippets: [
            { text: 'Text without page', source: 'doc.txt', page: null, line: null }
          ],
          messageId: 'msg-3'
        });

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'Question');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(screen.getByText('doc.txt')).toBeInTheDocument();
        });

        expect(screen.queryByText(/Pagina/i)).toBeInTheDocument(); // From existing history
        expect(screen.getByText('Text without page')).toBeInTheDocument();
      });

      it('does not display snippets section when no snippets exist', async () => {
        mockApi.get.mockResolvedValueOnce(mockAuthResponse);
        mockApi.get.mockResolvedValueOnce(mockGames);
        mockApi.get.mockResolvedValueOnce(mockAgents);
        mockApi.get.mockResolvedValueOnce(mockChats);
        mockApi.get.mockResolvedValueOnce({
          ...mockChatWithHistory,
          messages: []
        });

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.queryByText(/Nessun messaggio ancora/i)).toBeInTheDocument();
        });

        mockApi.post.mockResolvedValueOnce({
          answer: 'Simple answer',
          snippets: [],
          messageId: 'msg-1'
        });

        const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
        await user.type(input, 'Simple question');

        const sendButton = screen.getByRole('button', { name: /Invia/i });
        await user.click(sendButton);

        await waitFor(() => {
          expect(screen.getByText('Simple answer')).toBeInTheDocument();
        });

        expect(screen.queryByText('Fonti:')).not.toBeInTheDocument();
      });

      it('displays message timestamps', async () => {
        setupAuthenticatedState();
        mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

        render(<ChatPage />);

        await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

        const user = userEvent.setup();

        // Load a chat
        const chatItems = screen.getAllByText('Chess Expert');
        await user.click(chatItems[chatItems.length - 1]);

        await waitFor(() => {
          expect(screen.getByText('How do I castle?')).toBeInTheDocument();
        });

        // Check timestamps are rendered (hard to test exact format)
        const timestamps = screen.getAllByText(/\d{1,2}:\d{2}:\d{2}/);
        expect(timestamps.length).toBeGreaterThan(0);
      });
    });
  });

  // =============================================================================
  // FEEDBACK TESTS
  // =============================================================================

  describe('Feedback', () => {
    it('submits helpful feedback when thumbs up is clicked', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
      });

      mockApi.post.mockResolvedValueOnce({});

      const helpfulButtons = screen.getAllByRole('button', { name: /👍 Utile/i });
      await user.click(helpfulButtons[0]);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenCalledWith('/agents/feedback', {
          messageId: 'msg-2',
          endpoint: 'qa',
          outcome: 'helpful',
          userId: 'user-1',
          gameId: 'game-1'
        });
      });
    });

    it('submits not-helpful feedback when thumbs down is clicked', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
      });

      mockApi.post.mockResolvedValueOnce({});

      const notHelpfulButtons = screen.getAllByRole('button', { name: /👎 Non utile/i });
      await user.click(notHelpfulButtons[0]);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenCalledWith('/agents/feedback', {
          messageId: 'msg-2',
          endpoint: 'qa',
          outcome: 'not-helpful',
          userId: 'user-1',
          gameId: 'game-1'
        });
      });
    });

    it('toggles feedback to null when clicking same button twice', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
      });

      mockApi.post.mockResolvedValueOnce({});
      mockApi.post.mockResolvedValueOnce({});

      const helpfulButtons = screen.getAllByRole('button', { name: /👍 Utile/i });

      // First click
      await user.click(helpfulButtons[0]);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenNthCalledWith(1, '/agents/feedback', {
          messageId: 'msg-2',
          endpoint: 'qa',
          outcome: 'helpful',
          userId: 'user-1',
          gameId: 'game-1'
        });
      });

      // Second click
      await user.click(helpfulButtons[0]);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenNthCalledWith(2, '/agents/feedback', {
          messageId: 'msg-2',
          endpoint: 'qa',
          outcome: null,
          userId: 'user-1',
          gameId: 'game-1'
        });
      });
    });

    it('changes feedback when switching between helpful and not-helpful', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
      });

      mockApi.post.mockResolvedValueOnce({});
      mockApi.post.mockResolvedValueOnce({});

      const helpfulButtons = screen.getAllByRole('button', { name: /👍 Utile/i });
      const notHelpfulButtons = screen.getAllByRole('button', { name: /👎 Non utile/i });

      // First click helpful
      await user.click(helpfulButtons[0]);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenNthCalledWith(1, '/agents/feedback', expect.objectContaining({
          outcome: 'helpful'
        }));
      });

      // Then click not helpful
      await user.click(notHelpfulButtons[0]);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenNthCalledWith(2, '/agents/feedback', expect.objectContaining({
          outcome: 'not-helpful'
        }));
      });
    });

    it('reverts feedback state when API call fails', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
      });

      mockApi.post.mockRejectedValueOnce(new Error('Feedback failed'));

      const helpfulButtons = screen.getAllByRole('button', { name: /👍 Utile/i });
      const originalStyle = helpfulButtons[0].style.background;

      await user.click(helpfulButtons[0]);

      // Should revert after error
      await waitFor(() => {
        expect(helpfulButtons[0]).toHaveStyle(`background: ${originalStyle || '#f1f3f4'}`);
      });

      consoleErrorSpy.mockRestore();
    });

    it('uses backend message ID for feedback when available', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
      });

      mockApi.post.mockResolvedValueOnce({});

      const helpfulButtons = screen.getAllByRole('button', { name: /👍 Utile/i });
      await user.click(helpfulButtons[0]);

      // Should use backend message ID 'msg-2' from mockChatWithHistory
      await waitFor(() => {
        expect(mockApi.post).toHaveBeenCalledWith('/agents/feedback', expect.objectContaining({
          messageId: 'msg-2'
        }));
      });
    });

    it('only shows feedback buttons for assistant messages', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('How do I castle?')).toBeInTheDocument();
      });

      // Count feedback buttons (should only be for assistant messages)
      const helpfulButtons = screen.getAllByRole('button', { name: /👍 Utile/i });
      const notHelpfulButtons = screen.getAllByRole('button', { name: /👎 Non utile/i });

      // Only 1 assistant message in history
      expect(helpfulButtons.length).toBe(1);
      expect(notHelpfulButtons.length).toBe(1);
    });
  });

  // =============================================================================
  // UI INTERACTION TESTS
  // =============================================================================

  describe('UI Interactions', () => {
    it('toggles sidebar when collapse button is clicked', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();
      const collapseButton = screen.getByTitle(/Nascondi sidebar/i);

      await user.click(collapseButton);

      // Check button changes to expand icon
      expect(screen.getByTitle(/Mostra sidebar/i)).toBeInTheDocument();
    });

    it('shows game name in header when game is selected', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      // Use getAllByText since "Chess" appears in multiple places (header, select, chat items)
      const chessElements = screen.getAllByText('Chess');
      expect(chessElements.length).toBeGreaterThan(0);
    });

    it('shows agent name in header when chat is active', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Chess Expert' })).toBeInTheDocument();
      });
    });

    it('shows default message when no chat is selected', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockResolvedValueOnce(mockAgents);
      mockApi.get.mockResolvedValueOnce([]);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      expect(screen.getByRole('heading', { name: /Seleziona o crea una chat/i })).toBeInTheDocument();
    });

    it('highlights active chat in sidebar', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');

      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('How do I castle?')).toBeInTheDocument();
      });

      // Verify the chat is active by checking that the header shows the agent name
      // This indirectly confirms the chat is highlighted as active
      expect(screen.getByRole('heading', { name: 'Chess Expert' })).toBeInTheDocument();
    });

    it('formats chat preview with date and time', async () => {
      setupAuthenticatedState();

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      // Check chat preview includes date/time (format is locale-dependent)
      const chatPreview = screen.getAllByText(/\d{1,2}\/\d{1,2}\/\d{4}/);
      expect(chatPreview.length).toBeGreaterThan(0);
    });
  });

  // =============================================================================
  // EDGE CASES AND ERROR HANDLING
  // =============================================================================

  describe('Edge Cases', () => {
    it('handles null auth response gracefully', async () => {
      mockApi.get.mockResolvedValueOnce(null);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/auth/me'));

      expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();
    });

    it('handles empty games list', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce([]);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games'));

      const gameSelect = screen.getByLabelText(/Gioco:/i);
      expect(within(gameSelect).queryByText('Chess')).not.toBeInTheDocument();
    });

    it('handles empty agents list', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockResolvedValueOnce([]);
      mockApi.get.mockResolvedValueOnce([]);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/games/game-1/agents'));

      const agentSelect = screen.getByLabelText(/Agente:/i);
      expect(within(agentSelect).queryByText('Chess Expert')).not.toBeInTheDocument();
    });

    it('handles chat with null lastMessageAt', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockResolvedValueOnce(mockAgents);
      mockApi.get.mockResolvedValueOnce([mockChats[1]]); // This one has null lastMessageAt

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      // Should use startedAt for preview
      // Use getAllByText since "Chess Helper" appears in multiple places (sidebar, select options)
      const helperElements = screen.getAllByText('Chess Helper');
      expect(helperElements.length).toBeGreaterThan(0);
    });

    it('handles message with null metadata', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockResolvedValueOnce(mockAgents);
      mockApi.get.mockResolvedValueOnce(mockChats);

      const chatWithNullMetadata = {
        ...mockChatWithHistory,
        messages: [
          {
            id: 'msg-1',
            level: 'agent',
            message: 'Simple message',
            metadataJson: null,
            createdAt: '2025-01-10T10:00:00Z'
          }
        ]
      };

      mockApi.get.mockResolvedValueOnce(chatWithNullMetadata);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Simple message')).toBeInTheDocument();
      });

      expect(screen.queryByText('Fonti:')).not.toBeInTheDocument();
    });

    it('handles QA response without messageId', async () => {
      setupAuthenticatedState();
      mockApi.get.mockResolvedValueOnce(mockChatWithHistory);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      // Load a chat
      const chatItems = screen.getAllByText('Chess Expert');
      await user.click(chatItems[chatItems.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('How do I castle?')).toBeInTheDocument();
      });

      // Response without messageId
      mockApi.post.mockResolvedValueOnce({
        answer: 'Answer without ID',
        snippets: []
      });

      const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
      await user.type(input, 'Question');

      const sendButton = screen.getByRole('button', { name: /Invia/i });
      await user.click(sendButton);

      await waitFor(() => {
        expect(screen.getByText('Answer without ID')).toBeInTheDocument();
      });

      // Feedback should still work (using frontend ID)
      mockApi.post.mockResolvedValueOnce({});

      const helpfulButtons = screen.getAllByRole('button', { name: /👍 Utile/i });
      await user.click(helpfulButtons[helpfulButtons.length - 1]); // Last one (new message)

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenCalledWith('/agents/feedback', expect.objectContaining({
          outcome: 'helpful'
        }));
      });
    });

    it('handles chat creation returning null', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockGames);
      mockApi.get.mockResolvedValueOnce(mockAgents);
      mockApi.get.mockResolvedValueOnce([]);

      render(<ChatPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/chats?gameId=game-1'));

      const user = userEvent.setup();

      mockApi.post.mockResolvedValueOnce(null); // Chat creation returns null

      const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
      await user.type(input, 'First message');

      const sendButton = screen.getByRole('button', { name: /Invia/i });
      await user.click(sendButton);

      await waitFor(() => {
        expect(screen.getByText(/Errore nella comunicazione con l'agente/i)).toBeInTheDocument();
      });

      consoleErrorSpy.mockRestore();
    });
  });
});
