import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { GameTabsPanel } from '../GameTabsPanel';

// Mock the data-fetching hook so the tab panels render their placeholder states
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: () => ({ data: null, isLoading: false, isError: false }),
}));

describe('GameTabsPanel', () => {
  it('renders a vertical tablist labelled "Dettagli gioco"', () => {
    render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
    const tablist = screen.getByRole('tablist', { name: /dettagli gioco/i });
    expect(tablist).toBeInTheDocument();
    expect(tablist).toHaveAttribute('aria-orientation', 'vertical');
  });

  it('renders exactly 5 tabs', () => {
    render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
    expect(screen.getAllByRole('tab')).toHaveLength(5);
  });

  it('defaults to the Info tab as selected', () => {
    render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
    const infoTab = screen.getByRole('tab', { name: /info/i });
    expect(infoTab).toHaveAttribute('aria-selected', 'true');
  });

  it('respects the initialTab prop', () => {
    render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" initialTab="toolbox" />);
    expect(screen.getByRole('tab', { name: /toolbox/i })).toHaveAttribute('aria-selected', 'true');
  });

  it('switches the selected tab on click', () => {
    render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
    const aiChatTab = screen.getByRole('tab', { name: /ai chat/i });
    fireEvent.click(aiChatTab);
    expect(aiChatTab).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: /info/i })).toHaveAttribute('aria-selected', 'false');
  });

  it('invokes onTabChange when switching tabs', () => {
    const onTabChange = vi.fn();
    render(
      <GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" onTabChange={onTabChange} />
    );
    fireEvent.click(screen.getByRole('tab', { name: /partite/i }));
    expect(onTabChange).toHaveBeenCalledWith('partite');
  });

  it('does not invoke onTabChange when clicking the already-active tab', () => {
    const onTabChange = vi.fn();
    render(
      <GameTabsPanel
        gameId="00000000-0000-4000-8000-000000000001"
        initialTab="info"
        onTabChange={onTabChange}
      />
    );
    fireEvent.click(screen.getByRole('tab', { name: /info/i }));
    expect(onTabChange).not.toHaveBeenCalled();
  });

  it('shows "not in library" placeholder for Info tab when isNotInLibrary is true', () => {
    render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" isNotInLibrary />);
    expect(screen.getByText(/aggiungi questo gioco alla tua libreria/i)).toBeInTheDocument();
  });
});
