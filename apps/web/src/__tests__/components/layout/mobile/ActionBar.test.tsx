import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useCardHand } from '@/lib/stores/card-hand-store';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { ActionBar } from '@/components/layout/mobile/ActionBar';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn().mockReturnValue('/dashboard'),
  useRouter: vi.fn().mockReturnValue({ back: vi.fn() }),
}));

vi.mock('@/components/dashboard', () => ({
  useDashboardMode: vi.fn().mockReturnValue({ isGameMode: false, activeSessionId: null }),
}));

const CLOSED_STATE = {
  state: 'closed' as const,
  activeEntityType: null,
  activeEntityId: null,
  activeTabId: null,
  sourceEntityId: null,
  anchorRect: null,
  deckStackSkipped: false,
  drawerStack: [],
};

const GAME_CARD = (n: number) => ({
  id: `game:${n}`,
  entityType: 'game' as const,
  entityId: String(n),
  label: `Game ${n}`,
  href: `/games/${n}`,
  pinned: false,
  addedAt: n,
});

describe('ActionBar', () => {
  beforeEach(() => {
    useCardHand.setState({ cards: [] });
    useCascadeNavigationStore.setState(CLOSED_STATE);
  });

  it('renders the action bar', () => {
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar')).toBeDefined();
  });

  it('renders no chips when hand is empty', () => {
    render(<ActionBar />);
    expect(screen.queryByTestId(/action-bar-chip/)).toBeNull();
  });

  it('renders one chip per card (max 3 visible)', () => {
    useCardHand.setState({ cards: [GAME_CARD(1), GAME_CARD(2)] });
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar-chip-game:1')).toBeDefined();
    expect(screen.getByTestId('action-bar-chip-game:2')).toBeDefined();
  });

  it('shows max 3 chips with overflow badge when cards > 3', () => {
    useCardHand.setState({ cards: [GAME_CARD(1), GAME_CARD(2), GAME_CARD(3), GAME_CARD(4)] });
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar-chip-game:1')).toBeDefined();
    expect(screen.getByTestId('action-bar-chip-game:2')).toBeDefined();
    expect(screen.getByTestId('action-bar-chip-game:3')).toBeDefined();
    expect(screen.queryByTestId('action-bar-chip-game:4')).toBeNull();
    const overflow = screen.getByTestId('action-bar-overflow');
    expect(overflow.textContent).toContain('+1');
  });

  it('opens drawer when chip is tapped', () => {
    useCardHand.setState({ cards: [GAME_CARD(1)] });
    render(<ActionBar />);
    fireEvent.click(screen.getByTestId('action-bar-chip-game:1'));
    const storeState = useCascadeNavigationStore.getState();
    expect(storeState.state).toBe('drawer');
    expect(storeState.activeEntityType).toBe('game');
    expect(storeState.activeEntityId).toBe('1');
  });

  it('opens drawer when overflow is tapped', () => {
    useCardHand.setState({ cards: [GAME_CARD(1), GAME_CARD(2), GAME_CARD(3), GAME_CARD(4)] });
    render(<ActionBar />);
    fireEvent.click(screen.getByTestId('action-bar-overflow'));
    expect(useCascadeNavigationStore.getState().state).toBe('drawer');
  });

  it('shows CTA for /dashboard route', () => {
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar-cta')).toBeDefined();
    expect(screen.getByTestId('action-bar-cta').textContent).toContain('Aggiungi');
  });

  it('renders session mode when in session', async () => {
    const { useDashboardMode } = await import('@/components/dashboard');
    vi.mocked(useDashboardMode).mockReturnValueOnce({ isGameMode: true, activeSessionId: 'sess1' });
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar-session')).toBeDefined();
    expect(screen.queryByTestId('action-bar')).toBeNull();
  });
});
