/**
 * JourneyProgress Tests
 * Issue #4949: Collapsible journey progress banner for new players
 *
 * Test Coverage:
 * - Banner renders when journey incomplete
 * - Banner hidden when all steps completed
 * - Banner hidden after dismiss click (localStorage set)
 * - Banner respects prior localStorage dismissal
 * - Collapse/expand toggle
 * - Step states: completed (teal), active (orange), pending (muted)
 * - Active KB step shows progress detail text
 * - Step 1 complete when gameId provided
 * - Step 1 complete when privateGamesData has totalCount > 0
 * - Steps 2-5 driven by query data
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { JourneyProgress } from '../JourneyProgress';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/usePdfProcessingStatus', () => ({
  usePdfProcessingStatus: vi.fn(),
}));

vi.mock('@/hooks/queries/useGameAgents', () => ({
  useGameAgents: vi.fn(),
}));

vi.mock('@/hooks/queries/useChatSessions', () => ({
  useRecentChatSessions: vi.fn(),
}));

vi.mock('@tanstack/react-query', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@tanstack/react-query')>();
  return {
    ...actual,
    useQuery: vi.fn(),
  };
});

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getPrivateGames: vi.fn(),
    },
  },
}));

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { usePdfProcessingStatus } = await import('@/hooks/queries/usePdfProcessingStatus') as any;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { useGameAgents } = await import('@/hooks/queries/useGameAgents') as any;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { useRecentChatSessions } = await import('@/hooks/queries/useChatSessions') as any;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { useQuery } = await import('@tanstack/react-query') as any;

// ============================================================================
// Helpers
// ============================================================================

function mockAllHooks({
  privateGamesCount = 0,
  pdfStatus = undefined as { status: string; progress?: number | null } | undefined,
  pdfError = false,
  pdfLoading = false,
  agentsCount = 0,
  chatGameIds = [] as string[],
} = {}) {
  useQuery.mockReturnValue({
    data: privateGamesCount > 0 ? { totalCount: privateGamesCount } : undefined,
    isLoading: false,
  });

  usePdfProcessingStatus.mockReturnValue({
    data: pdfStatus,
    isLoading: pdfLoading,
    isError: pdfError,
  });

  useGameAgents.mockReturnValue({
    data: Array.from({ length: agentsCount }, (_, i) => ({ id: `agent-${i}` })),
  });

  useRecentChatSessions.mockReturnValue({
    data: {
      sessions: chatGameIds.map((gameId, i) => ({
        id: `session-${i}`,
        gameId,
        title: `Chat ${i}`,
        lastMessageAt: null,
        lastMessagePreview: null,
        messageCount: 0,
        isArchived: false,
        createdAt: '2026-01-01T00:00:00Z',
        userId: 'user-1',
      })),
    },
  });
}

// ============================================================================
// Tests
// ============================================================================

describe('JourneyProgress', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Clear all localStorage entries (component uses per-game keys like
    // 'journey-progress-dismissed-game-1' so we must clear all, not just base key)
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  describe('Visibility', () => {
    it('renders when journey is incomplete', () => {
      mockAllHooks();

      render(<JourneyProgress />);

      expect(screen.getByTestId('journey-progress')).toBeInTheDocument();
    });

    it('shows all 5 steps', () => {
      mockAllHooks();

      render(<JourneyProgress />);

      expect(screen.getByTestId('journey-step-create-game')).toBeInTheDocument();
      expect(screen.getByTestId('journey-step-upload-pdf')).toBeInTheDocument();
      expect(screen.getByTestId('journey-step-kb-ready')).toBeInTheDocument();
      expect(screen.getByTestId('journey-step-create-agent')).toBeInTheDocument();
      expect(screen.getByTestId('journey-step-chat')).toBeInTheDocument();
    });

    it('hides when already dismissed (localStorage=true)', () => {
      localStorage.setItem('journey-progress-dismissed', 'true');
      mockAllHooks();

      render(<JourneyProgress />);

      expect(screen.queryByTestId('journey-progress')).not.toBeInTheDocument();
    });

    it('hides after user clicks dismiss button', async () => {
      const user = userEvent.setup();
      mockAllHooks();

      render(<JourneyProgress />);
      expect(screen.getByTestId('journey-progress')).toBeInTheDocument();

      await user.click(screen.getByTestId('journey-dismiss-btn'));

      expect(screen.queryByTestId('journey-progress')).not.toBeInTheDocument();
    });

    it('sets localStorage to true after dismiss', async () => {
      const user = userEvent.setup();
      mockAllHooks();

      render(<JourneyProgress />);
      await user.click(screen.getByTestId('journey-dismiss-btn'));

      expect(localStorage.getItem('journey-progress-dismissed')).toBe('true');
    });
  });

  describe('Collapse / Expand', () => {
    it('shows steps by default (not collapsed)', () => {
      mockAllHooks();

      render(<JourneyProgress />);

      expect(screen.getByTestId('journey-steps')).toBeInTheDocument();
    });

    it('hides steps when collapsed', async () => {
      const user = userEvent.setup();
      mockAllHooks();

      render(<JourneyProgress />);

      await user.click(screen.getByTestId('journey-collapse-btn'));

      expect(screen.queryByTestId('journey-steps')).not.toBeInTheDocument();
    });

    it('shows steps again after re-expanding', async () => {
      const user = userEvent.setup();
      mockAllHooks();

      render(<JourneyProgress />);

      await user.click(screen.getByTestId('journey-collapse-btn'));
      await user.click(screen.getByTestId('journey-collapse-btn'));

      expect(screen.getByTestId('journey-steps')).toBeInTheDocument();
    });
  });

  describe('Step 1: Crea gioco', () => {
    it('step 1 is "active" when no private games and no gameId', () => {
      mockAllHooks({ privateGamesCount: 0 });

      render(<JourneyProgress />);

      expect(screen.getByTestId('journey-step-create-game')).toHaveAttribute(
        'data-status',
        'active'
      );
    });

    it('step 1 is "completed" when private games exist', () => {
      mockAllHooks({ privateGamesCount: 1 });

      render(<JourneyProgress />);

      expect(screen.getByTestId('journey-step-create-game')).toHaveAttribute(
        'data-status',
        'completed'
      );
    });

    it('step 1 is "completed" when gameId prop is provided', () => {
      mockAllHooks();

      render(<JourneyProgress gameId="game-1" />);

      expect(screen.getByTestId('journey-step-create-game')).toHaveAttribute(
        'data-status',
        'completed'
      );
    });
  });

  describe('Step 2: Carica PDF', () => {
    it('step 2 is "pending" when no pdf and step 1 incomplete', () => {
      mockAllHooks({ privateGamesCount: 0 });

      render(<JourneyProgress />);

      expect(screen.getByTestId('journey-step-upload-pdf')).toHaveAttribute(
        'data-status',
        'pending'
      );
    });

    it('step 2 is "active" when game exists but no PDF', () => {
      mockAllHooks({ privateGamesCount: 1 });

      render(<JourneyProgress gameId="game-1" />);

      // No pdf status (undefined) with no error → step 2 active
      expect(screen.getByTestId('journey-step-upload-pdf')).toHaveAttribute(
        'data-status',
        'active'
      );
    });

    it('step 2 is "completed" when PDF status exists', () => {
      mockAllHooks({
        pdfStatus: { status: 'pending' },
      });

      render(<JourneyProgress gameId="game-1" />);

      expect(screen.getByTestId('journey-step-upload-pdf')).toHaveAttribute(
        'data-status',
        'completed'
      );
    });

    it('step 2 is "active" when PDF query returns 404 error (no PDF uploaded)', () => {
      // When API returns 404, the hook sets isError=true and data=undefined
      // hasPdf = !!gameId && !pdfLoading && !pdfError && pdfStatus !== undefined
      // With pdfError=true, hasPdf is false → step 2 is active (game exists)
      mockAllHooks({ pdfError: true });

      render(<JourneyProgress gameId="game-1" />);

      expect(screen.getByTestId('journey-step-upload-pdf')).toHaveAttribute(
        'data-status',
        'active'
      );
    });
  });

  describe('Step 3: KB pronta', () => {
    it('step 3 is "active" with progress detail when processing', () => {
      mockAllHooks({
        pdfStatus: { status: 'processing', progress: 68 },
      });

      render(<JourneyProgress gameId="game-1" />);

      expect(screen.getByTestId('journey-step-kb-ready')).toHaveAttribute(
        'data-status',
        'active'
      );

      expect(
        screen.getByTestId('journey-step-detail-kb-ready')
      ).toHaveTextContent('68%');
    });

    it('step 3 is "active" when processing with null progress', () => {
      mockAllHooks({
        pdfStatus: { status: 'processing', progress: null },
      });

      render(<JourneyProgress gameId="game-1" />);

      const detail = screen.getByTestId('journey-step-detail-kb-ready');
      expect(detail).toHaveTextContent('Indicizzazione in corso');
    });

    it('step 3 is "completed" when indexed', () => {
      mockAllHooks({
        pdfStatus: { status: 'indexed' },
      });

      render(<JourneyProgress gameId="game-1" />);

      expect(screen.getByTestId('journey-step-kb-ready')).toHaveAttribute(
        'data-status',
        'completed'
      );
    });

    it('step 3 shows no detail when indexed', () => {
      mockAllHooks({
        pdfStatus: { status: 'indexed' },
      });

      render(<JourneyProgress gameId="game-1" />);

      expect(
        screen.queryByTestId('journey-step-detail-kb-ready')
      ).not.toBeInTheDocument();
    });
  });

  describe('Step 4: Crea agente', () => {
    it('step 4 is "active" when KB is indexed but no agents', () => {
      mockAllHooks({
        pdfStatus: { status: 'indexed' },
        agentsCount: 0,
      });

      render(<JourneyProgress gameId="game-1" />);

      expect(screen.getByTestId('journey-step-create-agent')).toHaveAttribute(
        'data-status',
        'active'
      );
    });

    it('step 4 is "completed" when agent exists', () => {
      mockAllHooks({
        pdfStatus: { status: 'indexed' },
        agentsCount: 1,
      });

      render(<JourneyProgress gameId="game-1" />);

      expect(screen.getByTestId('journey-step-create-agent')).toHaveAttribute(
        'data-status',
        'completed'
      );
    });
  });

  describe('Step 5: Chat', () => {
    it('step 5 is "active" when agent exists but no chats', () => {
      mockAllHooks({
        pdfStatus: { status: 'indexed' },
        agentsCount: 1,
        chatGameIds: [],
      });

      render(<JourneyProgress gameId="game-1" />);

      expect(screen.getByTestId('journey-step-chat')).toHaveAttribute(
        'data-status',
        'active'
      );
    });

    it('auto-dismisses when step 5 (chat) is also completed (all steps done)', async () => {
      // When all 5 steps are complete, component auto-dismisses
      mockAllHooks({
        pdfStatus: { status: 'indexed' },
        agentsCount: 1,
        chatGameIds: ['game-1'],
      });

      render(<JourneyProgress gameId="game-1" />);

      await waitFor(() => {
        expect(screen.queryByTestId('journey-progress')).not.toBeInTheDocument();
      });
    });

    it('step 5 is not completed by chat sessions for OTHER games', () => {
      mockAllHooks({
        pdfStatus: { status: 'indexed' },
        agentsCount: 1,
        chatGameIds: ['other-game-id'],
      });

      render(<JourneyProgress gameId="game-1" />);

      // step 5 should be active (agent exists) not completed
      expect(screen.getByTestId('journey-step-chat')).toHaveAttribute(
        'data-status',
        'active'
      );
    });
  });

  describe('Complete journey auto-dismiss', () => {
    it('hides banner when all 5 steps are completed', async () => {
      mockAllHooks({
        privateGamesCount: 1,
        pdfStatus: { status: 'indexed' },
        agentsCount: 1,
        chatGameIds: ['game-1'],
      });

      render(<JourneyProgress gameId="game-1" />);

      await waitFor(() => {
        expect(screen.queryByTestId('journey-progress')).not.toBeInTheDocument();
      });
    });

    it('sets localStorage when all steps complete', async () => {
      mockAllHooks({
        privateGamesCount: 1,
        pdfStatus: { status: 'indexed' },
        agentsCount: 1,
        chatGameIds: ['game-1'],
      });

      render(<JourneyProgress gameId="game-1" />);

      await waitFor(() => {
        // Component uses per-game key when gameId is provided
        expect(localStorage.getItem('journey-progress-dismissed-game-1')).toBe('true');
      });
    });
  });

  describe('Accessibility', () => {
    it('has region role with label', () => {
      mockAllHooks();

      render(<JourneyProgress />);

      expect(
        screen.getByRole('region', { name: /Percorso di configurazione/i })
      ).toBeInTheDocument();
    });

    it('has list role for steps container', () => {
      mockAllHooks();

      render(<JourneyProgress />);

      expect(screen.getByRole('list')).toBeInTheDocument();
    });

    it('dismiss button has accessible label', () => {
      mockAllHooks();

      render(<JourneyProgress />);

      expect(
        screen.getByRole('button', { name: /Chiudi percorso/i })
      ).toBeInTheDocument();
    });
  });
});
