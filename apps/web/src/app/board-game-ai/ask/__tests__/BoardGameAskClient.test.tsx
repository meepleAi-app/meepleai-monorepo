/**
 * Tests for BoardGameAskClient component
 * Issue #1005: Jest tests for Q&A components (20 tests)
 *
 * Coverage:
 * - Rendering and UI structure
 * - Game selection (loading, selection, errors)
 * - Question input (text entry, keyboard shortcuts, validation)
 * - Submit button (states, loading, disabled logic)
 * - Conversation history (messages, citations, animations)
 * - Error handling (games fetch, query errors)
 * - Integration flow (E2E user journey)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BoardGameAskClient from '../BoardGameAskClient';
import { useChatQuery } from '@/lib/hooks/useChatQuery';

// Mock dependencies
vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getAll: vi.fn(),
    },
  },
}));
vi.mock('@/lib/hooks/useChatQuery');
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
  },
}));

// Mock data
const mockUuid = (index: number) => `770e8400-e29b-41d4-a716-00000000000${index}`;

const mockGames = [
  {
    id: mockUuid(1),
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    bggId: 13,
    createdAt: new Date().toISOString(),
    description: 'Settle the island of Catan',
  },
  {
    id: mockUuid(2),
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 5,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 60,
    bggId: 9209,
    createdAt: new Date().toISOString(),
    description: 'Build railway routes',
  },
  {
    id: mockUuid(3),
    title: 'Azul',
    publisher: 'Next Move Games',
    yearPublished: 2017,
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 45,
    bggId: 230802,
    createdAt: new Date().toISOString(),
    description: 'Tile placement game',
  },
];

const firstGameId = mockGames[0].id;

const mockQueryState = {
  isLoading: false,
  error: null,
  state: null,
};

const mockQueryControls = {
  askQuestion: vi.fn().mockResolvedValue(undefined),
};

describe('BoardGameAskClient', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { api } = await import('@/lib/api');
    vi.mocked(api.games.getAll).mockResolvedValue(mockGames);
    (useChatQuery as any).mockReturnValue([mockQueryState, mockQueryControls]);
  });

  // ============================================================================
  // GROUP 1: RENDERING (3 tests)
  // ============================================================================

  describe('Rendering', () => {
    it('should render without crashing with all main sections', () => {
      render(<BoardGameAskClient />);

      // Header
      expect(screen.getByText('Board Game AI')).toBeInTheDocument();

      // Main content
      expect(screen.getByText('Ask a Question')).toBeInTheDocument();
      expect(screen.getByLabelText(/select game/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/your question/i)).toBeInTheDocument();
    });

    it('should display empty state initially', () => {
      render(<BoardGameAskClient />);

      expect(screen.getByText('Ready to Answer Your Questions')).toBeInTheDocument();
      expect(
        screen.getByText(/select a game above and ask any rule question/i)
      ).toBeInTheDocument();
    });

    it('should render navigation links in header', () => {
      render(<BoardGameAskClient />);

      expect(screen.getByRole('link', { name: /chat/i })).toHaveAttribute('href', '/chat');
      expect(screen.getByRole('link', { name: /upload/i })).toHaveAttribute('href', '/upload');
      expect(screen.getByRole('link', { name: /home/i })).toHaveAttribute('href', '/');
    });
  });

  // ============================================================================
  // GROUP 2: GAME SELECTION (4 tests)
  // ============================================================================

  describe('Game Selection', () => {
    it('should load games on mount and auto-select first game', async () => {
      const { api } = await import('@/lib/api');
      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(api.games.getAll).toHaveBeenCalled();
      });

      // Auto-selection happens via useEffect, verify select has value
      const select = screen.getByLabelText(/select game/i);
      await waitFor(() => {
        expect(select).not.toHaveTextContent('Loading games...');
      });
    });

    it('should display games in select dropdown', async () => {
      render(<BoardGameAskClient />);

      // Wait for games to load and first game to be auto-selected
      const selectTrigger = screen.getByLabelText(/select game/i);
      await waitFor(() => {
        expect(selectTrigger).not.toHaveTextContent('Loading games...');
      });

      // Verify the selected game is displayed in the trigger (auto-selected first game)
      await waitFor(() => {
        expect(selectTrigger).toHaveTextContent('Catan');
      });
    });

    it('should show loading state while fetching games', async () => {
      const { api } = await import('@/lib/api');
      vi.mocked(api.games.getAll).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(mockGames), 100))
      );

      render(<BoardGameAskClient />);

      expect(screen.getByText(/loading games/i)).toBeInTheDocument();
    });

    it('should display error when games fetch fails', async () => {
      const { api } = await import('@/lib/api');
      vi.mocked(api.games.getAll).mockRejectedValue(new Error('Network error'));

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByText(/failed to load games/i)).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // GROUP 3: QUESTION INPUT (4 tests)
  // ============================================================================

  describe('Question Input', () => {
    it('should accept text input in question textarea', async () => {
      const user = userEvent.setup();
      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Can I play a development card?');

      expect(textarea).toHaveValue('Can I play a development card?');
    });

    it('should submit question on Ctrl+Enter', async () => {
      const user = userEvent.setup();
      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Test question');
      await user.keyboard('{Control>}{Enter}{/Control}');

      await waitFor(() => {
        expect(mockQueryControls.askQuestion).toHaveBeenCalledWith(firstGameId, 'Test question');
      });
    });

    it('should be disabled when no game is selected', async () => {
      const { api } = await import('@/lib/api');
      vi.mocked(api.games.getAll).mockResolvedValue([]);
      render(<BoardGameAskClient />);

      await waitFor(() => {
        const textarea = screen.getByLabelText(/your question/i);
        expect(textarea).toBeDisabled();
      });
    });

    it('should be disabled during query loading', async () => {
      (useChatQuery as any).mockReturnValue([
        { ...mockQueryState, isLoading: true },
        mockQueryControls,
      ]);

      render(<BoardGameAskClient />);

      await waitFor(() => {
        const textarea = screen.getByLabelText(/your question/i);
        expect(textarea).toBeDisabled();
      });
    });
  });

  // ============================================================================
  // GROUP 4: SUBMIT BUTTON (3 tests)
  // ============================================================================

  describe('Submit Button', () => {
    it('should be disabled when no game selected, empty question, or loading', async () => {
      render(<BoardGameAskClient />);

      await waitFor(() => {
        const button = screen.getByRole('button', { name: /ask question/i });
        expect(button).toBeDisabled();
      });
    });

    it('should show loading state during query', async () => {
      (useChatQuery as any).mockReturnValue([
        { ...mockQueryState, isLoading: true },
        mockQueryControls,
      ]);

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByText(/thinking/i)).toBeInTheDocument();
      });
    });

    it('should trigger question submission on click', async () => {
      const user = userEvent.setup();
      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Test question');

      const button = screen.getByRole('button', { name: /ask question/i });
      await user.click(button);

      await waitFor(() => {
        expect(mockQueryControls.askQuestion).toHaveBeenCalledWith(firstGameId, 'Test question');
      });
    });
  });

  // ============================================================================
  // GROUP 5: CONVERSATION HISTORY (4 tests)
  // ============================================================================

  describe('Conversation History', () => {
    it('should render user messages correctly', async () => {
      const user = userEvent.setup();
      const onCompleteMock = vi.fn();

      (useChatQuery as any).mockImplementation(callbacks => {
        onCompleteMock.mockImplementation(() => {
          callbacks.onComplete('Answer text', [], {});
        });
        return [mockQueryState, { askQuestion: onCompleteMock }];
      });

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'My test question');

      const button = screen.getByRole('button', { name: /ask question/i });
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByText('My test question')).toBeInTheDocument();
      });
    });

    it('should render assistant messages with content', async () => {
      const user = userEvent.setup();
      let capturedOnComplete: any;

      (useChatQuery as any).mockImplementation(callbacks => {
        capturedOnComplete = callbacks.onComplete;
        return [
          mockQueryState,
          {
            askQuestion: vi.fn().mockImplementation(() => {
              capturedOnComplete('This is the answer', [], {});
            }),
          },
        ];
      });

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Question');

      const button = screen.getByRole('button', { name: /ask question/i });
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByText('This is the answer')).toBeInTheDocument();
      });
    });

    it('should display citations when present in assistant message', async () => {
      const user = userEvent.setup();
      let capturedOnComplete: any;

      const mockCitations = [
        {
          documentId: 'doc-123',
          pageNumber: 5,
          snippet: 'Players start with 10 credits',
          relevanceScore: 0.95,
        },
      ];

      (useChatQuery as any).mockImplementation(callbacks => {
        capturedOnComplete = callbacks.onComplete;
        return [
          mockQueryState,
          {
            askQuestion: vi.fn().mockImplementation(() => {
              capturedOnComplete('Answer with citations', mockCitations, {});
            }),
          },
        ];
      });

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Question');

      const button = screen.getByRole('button', { name: /ask question/i });
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByText(/sources:/i)).toBeInTheDocument();
        expect(screen.getByText('Document: doc-123')).toBeInTheDocument();
        expect(screen.getByText(/page 5/i)).toBeInTheDocument();
        expect(screen.getByText(/players start with 10 credits/i)).toBeInTheDocument();
      });
    });

    it('should show conversation section after first exchange', async () => {
      const user = userEvent.setup();
      let capturedOnComplete: any;

      (useChatQuery as any).mockImplementation(callbacks => {
        capturedOnComplete = callbacks.onComplete;
        return [
          mockQueryState,
          {
            askQuestion: vi.fn().mockImplementation(() => {
              capturedOnComplete('Answer', [], {});
            }),
          },
        ];
      });

      render(<BoardGameAskClient />);

      // Initially no conversation
      expect(screen.queryByText('Conversation')).not.toBeInTheDocument();

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Question');

      const button = screen.getByRole('button', { name: /ask question/i });
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByText('Conversation')).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // GROUP 6: ERROR HANDLING (2 tests)
  // ============================================================================

  describe('Error Handling', () => {
    it('should display query error from API', async () => {
      (useChatQuery as any).mockReturnValue([
        { ...mockQueryState, error: 'Failed to process question' },
        mockQueryControls,
      ]);

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByText('Failed to process question')).toBeInTheDocument();
      });
    });

    it('should display loading state indicator during query', async () => {
      (useChatQuery as any).mockReturnValue([
        { ...mockQueryState, isLoading: true, state: 'Processing your question...' },
        mockQueryControls,
      ]);

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByText('Processing your question...')).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // GROUP 7: INTEGRATION (1 test)
  // ============================================================================

  describe('Integration Flow', () => {
    it('should complete full user journey: select game → type question → submit → see response', async () => {
      const user = userEvent.setup();
      let capturedOnComplete: any;

      const mockCitations = [
        {
          documentId: 'catan-rulebook',
          pageNumber: 3,
          snippet: 'Development cards cannot be played on the turn they are purchased',
          relevanceScore: 0.92,
        },
      ];

      (useChatQuery as any).mockImplementation(callbacks => {
        capturedOnComplete = callbacks.onComplete;
        return [
          mockQueryState,
          {
            askQuestion: vi.fn().mockImplementation(() => {
              capturedOnComplete(
                'No, you cannot play a development card on the same turn you bought it.',
                mockCitations,
                {}
              );
            }),
          },
        ];
      });

      const { api } = await import('@/lib/api');
      render(<BoardGameAskClient />);

      // Step 1: Wait for games to load and verify game selected
      await waitFor(() => {
        expect(api.games.getAll).toHaveBeenCalled();
      });

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      // Step 2: Type question
      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Can I play a development card on the same turn I bought it?');

      expect(textarea).toHaveValue('Can I play a development card on the same turn I bought it?');

      // Step 3: Submit question
      const submitButton = screen.getByRole('button', { name: /ask question/i });
      await user.click(submitButton);

      // Step 4: Verify user message appears
      await waitFor(() => {
        expect(
          screen.getByText('Can I play a development card on the same turn I bought it?')
        ).toBeInTheDocument();
      });

      // Step 5: Verify assistant response with citations
      await waitFor(() => {
        expect(
          screen.getByText(/no, you cannot play a development card on the same turn/i)
        ).toBeInTheDocument();
        expect(screen.getByText(/sources:/i)).toBeInTheDocument();
        expect(screen.getByText('Document: catan-rulebook')).toBeInTheDocument();
        expect(
          screen.getByText(/development cards cannot be played on the turn they are purchased/i)
        ).toBeInTheDocument();
      });

      // Step 6: Verify conversation section appeared
      expect(screen.getByText('Conversation')).toBeInTheDocument();

      // Step 7: Verify empty state is gone
      expect(screen.queryByText('Ready to Answer Your Questions')).not.toBeInTheDocument();
    });
  });
});
