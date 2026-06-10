import { render as rtlRender, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { GameTabsPanel } from '../GameTabsPanel';
import type { GameTabId } from '../tabs';
import { useEffect, useState } from 'react';

// Mock the data-fetching hook so the tab panels render their placeholder states
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: () => ({ data: null, isLoading: false, isError: false }),
}));

function render(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return rtlRender(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('GameTabsPanel', () => {
  it('renders a horizontal tablist labelled "Dettagli gioco" (#2105 M7 layout restructure)', () => {
    render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
    const tablist = screen.getByRole('tablist', { name: /dettagli gioco/i });
    expect(tablist).toBeInTheDocument();
    expect(tablist).toHaveAttribute('aria-orientation', 'horizontal');
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
    // Label is "Toolkit" (Newman Strategy 1 — #2010); the underlying tab id is `toolbox`.
    expect(screen.getByRole('tab', { name: /toolkit/i })).toHaveAttribute('aria-selected', 'true');
  });

  it('switches the selected tab on click', () => {
    render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
    // Label is "Agente" (Newman Strategy 1 — #2010); the underlying tab id is `aiChat`.
    const agentTab = screen.getByRole('tab', { name: /agente/i });
    fireEvent.click(agentTab);
    expect(agentTab).toHaveAttribute('aria-selected', 'true');
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

  // #2105 M7 review fix — WCAG 2.1 §4.1.2 / APG Tabs keyboard navigation
  describe('keyboard navigation (#2105 M7)', () => {
    it('ArrowRight moves to next tab and updates selection', () => {
      render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
      const infoTab = screen.getByRole('tab', { name: /info/i });
      fireEvent.keyDown(infoTab, { key: 'ArrowRight' });
      expect(screen.getByRole('tab', { name: /agente/i })).toHaveAttribute('aria-selected', 'true');
    });

    it('ArrowLeft wraps from first to last tab', () => {
      render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
      const infoTab = screen.getByRole('tab', { name: /info/i });
      fireEvent.keyDown(infoTab, { key: 'ArrowLeft' });
      // Last tab is "Partite"
      expect(screen.getByRole('tab', { name: /partite/i })).toHaveAttribute(
        'aria-selected',
        'true'
      );
    });

    it('Home jumps to the first tab', () => {
      render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" initialTab="partite" />);
      const partiteTab = screen.getByRole('tab', { name: /partite/i });
      fireEvent.keyDown(partiteTab, { key: 'Home' });
      expect(screen.getByRole('tab', { name: /info/i })).toHaveAttribute('aria-selected', 'true');
    });

    it('End jumps to the last tab', () => {
      render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
      const infoTab = screen.getByRole('tab', { name: /info/i });
      fireEvent.keyDown(infoTab, { key: 'End' });
      expect(screen.getByRole('tab', { name: /partite/i })).toHaveAttribute(
        'aria-selected',
        'true'
      );
    });

    it('unhandled keys do not change the selection', () => {
      render(<GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" />);
      const infoTab = screen.getByRole('tab', { name: /info/i });
      fireEvent.keyDown(infoTab, { key: 'Enter' });
      expect(infoTab).toHaveAttribute('aria-selected', 'true');
    });
  });

  // #2105 M7 review fix — initialTab prop sync on URL searchParam change
  describe('initialTab prop sync (#2105 M7)', () => {
    function Harness({ initial }: { initial: GameTabId }) {
      const [tab, setTab] = useState<GameTabId>(initial);
      useEffect(() => setTab(initial), [initial]);
      return <GameTabsPanel gameId="00000000-0000-4000-8000-000000000001" initialTab={tab} />;
    }

    it('updates the selected tab when initialTab prop changes after first render', () => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false, gcTime: 0 } },
      });
      const { rerender } = rtlRender(
        <QueryClientProvider client={queryClient}>
          <Harness initial="info" />
        </QueryClientProvider>
      );
      expect(screen.getByRole('tab', { name: /info/i })).toHaveAttribute('aria-selected', 'true');
      rerender(
        <QueryClientProvider client={queryClient}>
          <Harness initial="aiChat" />
        </QueryClientProvider>
      );
      expect(screen.getByRole('tab', { name: /agente/i })).toHaveAttribute('aria-selected', 'true');
    });
  });
});
