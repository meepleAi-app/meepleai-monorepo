/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { GameTableDrawer } from '../GameTableDrawer';
import type { DrawerContent } from '@/lib/stores/gameTableDrawerStore';

vi.mock('@/components/library/game-table/GameStatsPanel', () => ({
  GameStatsPanel: ({ gameId }: { gameId: string }) => (
    <div data-testid="game-stats-panel">Stats for {gameId}</div>
  ),
}));

vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentKbDocs: () => ({ data: [], isLoading: false }),
  useAgentThreads: () => ({ data: [], isLoading: false }),
}));

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

describe('GameTableDrawer', () => {
  it('renders header with title and close button', () => {
    const content: DrawerContent = { type: 'stats', gameId: 'g1' };
    render(<GameTableDrawer content={content} onClose={vi.fn()} />, { wrapper: createWrapper() });
    expect(screen.getByText('Statistiche')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /chiudi/i })).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    const onClose = vi.fn();
    render(<GameTableDrawer content={{ type: 'kb', gameId: 'g1' }} onClose={onClose} />, {
      wrapper: createWrapper(),
    });
    fireEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('renders correct title for each content type', () => {
    const cases: [DrawerContent, string][] = [
      [{ type: 'chat', agentId: 'a1' }, 'Chat AI'],
      [{ type: 'stats', gameId: 'g1' }, 'Statistiche'],
      [{ type: 'kb', gameId: 'g1' }, 'Knowledge Base'],
      [{ type: 'toolkit', gameId: 'g1' }, 'Toolkit'],
      [{ type: 'document', documentId: 'd1' }, 'Documento'],
      [{ type: 'session', sessionId: 's1' }, 'Sessione'],
    ];
    for (const [content, title] of cases) {
      const { unmount } = render(<GameTableDrawer content={content} onClose={vi.fn()} />, {
        wrapper: createWrapper(),
      });
      expect(screen.getByText(title)).toBeInTheDocument();
      unmount();
    }
  });

  it('renders icon for content type', () => {
    render(<GameTableDrawer content={{ type: 'chat', agentId: 'a1' }} onClose={vi.fn()} />, {
      wrapper: createWrapper(),
    });
    expect(screen.getByText('💬')).toBeInTheDocument();
  });
});
