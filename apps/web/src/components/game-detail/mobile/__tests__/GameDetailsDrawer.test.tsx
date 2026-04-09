import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { GameDetailsDrawer } from '../GameDetailsDrawer';

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: () => ({ data: null, isLoading: false, isError: false }),
}));

describe('GameDetailsDrawer', () => {
  it('does not render tab content when closed', () => {
    render(<GameDetailsDrawer open={false} onOpenChange={() => {}} gameId="game-1" />);
    expect(screen.queryByTestId('game-details-drawer')).not.toBeInTheDocument();
  });

  it('renders with 5 tabs when open', () => {
    render(<GameDetailsDrawer open={true} onOpenChange={() => {}} gameId="game-1" />);
    expect(screen.getByTestId('game-details-drawer')).toBeInTheDocument();
    expect(screen.getByRole('tablist', { name: /dettagli gioco/i })).toBeInTheDocument();
    expect(screen.getAllByRole('tab')).toHaveLength(5);
  });

  it('defaults to Info tab', () => {
    render(<GameDetailsDrawer open={true} onOpenChange={() => {}} gameId="game-1" />);
    expect(screen.getByRole('tab', { name: /info/i })).toHaveAttribute('aria-selected', 'true');
  });

  it('switches tabs when clicked', () => {
    render(<GameDetailsDrawer open={true} onOpenChange={() => {}} gameId="game-1" />);
    const aiChatTab = screen.getByRole('tab', { name: /ai chat/i });
    fireEvent.click(aiChatTab);
    expect(aiChatTab).toHaveAttribute('aria-selected', 'true');
  });

  it('respects initialTab prop', () => {
    render(
      <GameDetailsDrawer open={true} onOpenChange={() => {}} gameId="game-1" initialTab="partite" />
    );
    expect(screen.getByRole('tab', { name: /partite/i })).toHaveAttribute('aria-selected', 'true');
  });
});
