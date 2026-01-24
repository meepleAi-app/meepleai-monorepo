/**
 * GameChatTab Component Tests
 * Issue #2401: QuickQuestion AI Generation
 *
 * Minimal tests for the GameChatTab component with React Query mock
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GameChatTab } from '../GameChatTab';
import * as apiModule from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

// Mock the ChatContent component to simplify testing
vi.mock('@/components/chat/ChatContent', () => ({
  ChatContent: () => <div data-testid="chat-content">Chat Interface</div>,
}));

// Mock the ChatStoreProvider
vi.mock('@/store/chat/ChatStoreProvider', () => ({
  ChatStoreProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

// Mock the Zustand store
vi.mock('@/store/chat/store', () => ({
  useChatStore: () => ({
    selectGame: vi.fn(),
    sendMessage: vi.fn(),
  }),
}));

// ============================================================================
// Test Suite
// ============================================================================

describe('GameChatTab', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
        },
      },
    });
    vi.clearAllMocks();
  });

  const renderComponent = (gameId: string = 'test-game-id', gameTitle: string = 'Test Game') => {
    return render(
      <QueryClientProvider client={queryClient}>
        <GameChatTab gameId={gameId} gameTitle={gameTitle} />
      </QueryClientProvider>
    );
  };

  it('should render quick questions section', async () => {
    // Mock empty quick questions (will use fallback)
    vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockResolvedValue([]);

    renderComponent();

    expect(screen.getByText('Domande Rapide')).toBeInTheDocument();
    expect(screen.getByTestId('chat-interface')).toBeInTheDocument();
  });

  it('should display loading spinner while fetching questions', () => {
    // Mock a pending promise to keep loading state
    vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockReturnValue(
      new Promise(() => {}) // Never resolves
    );

    renderComponent();

    // The spinner should be visible while loading
    expect(screen.getByText('Domande Rapide')).toBeInTheDocument();
  });

  it('should display fallback questions when API returns empty', async () => {
    vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockResolvedValue([]);

    renderComponent();

    // Wait for loading to complete
    await waitFor(() => {
      expect(screen.queryByText('Come si gioca?')).toBeInTheDocument();
    });

    // Should have all 5 fallback questions
    expect(screen.getByText('Come si gioca?')).toBeInTheDocument();
    expect(screen.getByText('Quali sono le regole principali?')).toBeInTheDocument();
    expect(screen.getByText('Come si vince?')).toBeInTheDocument();
    expect(screen.getByText('Quanto dura una partita?')).toBeInTheDocument();
    expect(screen.getByText('È adatto ai principianti?')).toBeInTheDocument();
  });

  it('should display AI-generated questions from API', async () => {
    const mockQuestions = [
      {
        id: 'q1',
        sharedGameId: 'game-1',
        text: 'Qual è la strategia migliore?',
        emoji: '🧠',
        category: 0,
        displayOrder: 1,
        isGenerated: true,
        createdAt: '2024-01-01T00:00:00Z',
        isActive: true,
      },
      {
        id: 'q2',
        sharedGameId: 'game-1',
        text: 'Come si gioca in modalità difficile?',
        emoji: '🎯',
        category: 1,
        displayOrder: 2,
        isGenerated: true,
        createdAt: '2024-01-01T00:00:00Z',
        isActive: true,
      },
    ];

    vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockResolvedValue(mockQuestions);

    renderComponent();

    // Wait for questions to load
    await waitFor(() => {
      expect(screen.getByText('Qual è la strategia migliore?')).toBeInTheDocument();
    });

    // Should display API questions (not fallback)
    expect(screen.getByText('Qual è la strategia migliore?')).toBeInTheDocument();
    expect(screen.getByText('Come si gioca in modalità difficile?')).toBeInTheDocument();

    // Should NOT display fallback questions
    expect(screen.queryByText('Come si gioca?')).not.toBeInTheDocument();
  });

  it('should have clickable question badges', async () => {
    vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockResolvedValue([]);

    renderComponent();

    // Wait for fallback questions to appear
    await waitFor(() => {
      expect(screen.getByText('Come si gioca?')).toBeInTheDocument();
    });

    // Questions should be buttons/interactive
    const questionBadge = screen.getByText('Come si gioca?').closest('div');
    expect(questionBadge).toHaveAttribute('role', 'button');
  });

  it('should have proper aria-labels for accessibility', async () => {
    vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockResolvedValue([]);

    renderComponent();

    // Wait for fallback questions
    await waitFor(() => {
      expect(screen.getByText('Come si gioca?')).toBeInTheDocument();
    });

    const questionBadge = screen.getByLabelText('Quick question: Come si gioca?');
    expect(questionBadge).toBeInTheDocument();
  });

  it('should display emojis for questions', async () => {
    vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockResolvedValue([]);

    renderComponent();

    // Wait for fallback questions
    await waitFor(() => {
      expect(screen.getByText('Come si gioca?')).toBeInTheDocument();
    });

    // Check for emoji presence
    expect(screen.getByText('🎮')).toBeInTheDocument(); // From fallback q1
    expect(screen.getByText('📖')).toBeInTheDocument(); // From fallback q2
    expect(screen.getByText('🏆')).toBeInTheDocument(); // From fallback q3
    expect(screen.getByText('⏱️')).toBeInTheDocument(); // From fallback q4
    expect(screen.getByText('🎯')).toBeInTheDocument(); // From fallback q5
  });

  it('should render chat interface', async () => {
    vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockResolvedValue([]);

    renderComponent();

    expect(screen.getByTestId('chat-interface')).toBeInTheDocument();
    expect(screen.getByTestId('chat-content')).toBeInTheDocument();
  });

  it('should call API with correct game ID', async () => {
    const spy = vi.spyOn(apiModule.api.games, 'getQuickQuestions').mockResolvedValue([]);
    const gameId = 'specific-game-123';

    renderComponent(gameId);

    await waitFor(() => {
      expect(spy).toHaveBeenCalledWith(gameId);
    });
  });
});
