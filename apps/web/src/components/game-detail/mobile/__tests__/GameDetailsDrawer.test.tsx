import { render as rtlRender, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { GameDetailsDrawer } from '../GameDetailsDrawer';

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: vi.fn(),
    push: vi.fn(),
    refresh: vi.fn(),
  }),
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: () => ({ data: null, isLoading: false, isError: false }),
}));

function render(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return rtlRender(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

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
    // Label is "Agente" (Newman Strategy 1 — #2010); the underlying tab id is `aiChat`.
    const agentTab = screen.getByRole('tab', { name: /agente/i });
    fireEvent.click(agentTab);
    expect(agentTab).toHaveAttribute('aria-selected', 'true');
  });

  it('respects initialTab prop', () => {
    render(
      <GameDetailsDrawer open={true} onOpenChange={() => {}} gameId="game-1" initialTab="partite" />
    );
    expect(screen.getByRole('tab', { name: /partite/i })).toHaveAttribute('aria-selected', 'true');
  });

  it('closes on Escape when its parent controls it', async () => {
    const { useState } = await import('react');
    function Host() {
      const [open, setOpen] = useState(true);
      return (
        <GameDetailsDrawer open={open} onOpenChange={setOpen} gameId="game-1" initialTab="aiChat" />
      );
    }
    render(<Host />);
    expect(screen.getByTestId('game-details-drawer')).toBeInTheDocument();

    fireEvent.keyDown(document.activeElement ?? document.body, { key: 'Escape' });

    await waitFor(() =>
      expect(screen.queryByTestId('game-details-drawer')).not.toBeInTheDocument()
    );
  });

  // #4992: the drawer stays mounted while closed, and read initialTab only at its first mount, so
  // reopening it on another tab (the mobile «Chat» button → Agente) still showed the old one.
  it('reopens on the tab it is given, not on the last one shown', () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const ui = (open: boolean, initialTab: 'info' | 'aiChat') => (
      <QueryClientProvider client={queryClient}>
        <GameDetailsDrawer
          open={open}
          onOpenChange={() => {}}
          gameId="game-1"
          initialTab={initialTab}
        />
      </QueryClientProvider>
    );
    const { rerender } = rtlRender(ui(true, 'info'));
    fireEvent.click(screen.getByRole('tab', { name: /partite/i }));

    rerender(ui(false, 'info'));
    rerender(ui(true, 'aiChat'));
    expect(screen.getByRole('tab', { name: /agente/i })).toHaveAttribute('aria-selected', 'true');

    rerender(ui(false, 'aiChat'));
    rerender(ui(true, 'info'));
    expect(screen.getByRole('tab', { name: /info/i })).toHaveAttribute('aria-selected', 'true');
  });
});
