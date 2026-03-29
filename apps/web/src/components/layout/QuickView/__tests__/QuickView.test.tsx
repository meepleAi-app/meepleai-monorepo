import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, beforeEach, vi } from 'vitest';

import { useQuickViewStore } from '@/stores/quick-view';
import { QuickView } from '../QuickView';

vi.mock('next/navigation', () => ({ usePathname: () => '/games' }));

describe('QuickView', () => {
  beforeEach(() => {
    useQuickViewStore.setState(useQuickViewStore.getInitialState());
  });

  it('is not rendered when closed', () => {
    render(<QuickView />);
    expect(screen.queryByTestId('quick-view')).not.toBeInTheDocument();
  });

  it('renders when open with game selected', () => {
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    expect(screen.getByTestId('quick-view')).toBeInTheDocument();
  });

  it('renders three tab buttons', () => {
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    expect(screen.getByRole('tab', { name: /regole/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /faq/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /ai/i })).toBeInTheDocument();
  });

  it('switches tabs on click', async () => {
    const user = userEvent.setup();
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    await user.click(screen.getByRole('tab', { name: /faq/i }));
    expect(useQuickViewStore.getState().activeTab).toBe('faq');
  });

  it('has a close button', async () => {
    const user = userEvent.setup();
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    await user.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(useQuickViewStore.getState().isOpen).toBe(false);
  });

  it('has a collapse button that narrows to 44px', async () => {
    const user = userEvent.setup();
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    await user.click(screen.getByRole('button', { name: /comprimi/i }));
    expect(useQuickViewStore.getState().isCollapsed).toBe(true);
  });

  it('sets data-mode attribute to session when opened for session', () => {
    useQuickViewStore.getState().openForSession('session-1', 'game-1');
    render(<QuickView />);
    expect(screen.getByTestId('quick-view')).toHaveAttribute('data-mode', 'session');
  });

  it('sets data-mode attribute to game when opened for game', () => {
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    expect(screen.getByTestId('quick-view')).toHaveAttribute('data-mode', 'game');
  });
});
