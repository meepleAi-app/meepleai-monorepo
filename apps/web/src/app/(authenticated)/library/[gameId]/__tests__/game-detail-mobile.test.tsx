/**
 * #4992: below lg the game page had no way into the chat — no button, and the `?tab=aiChat` deep
 * link (the /agent redirect, chat notifications) landed on the hero with the drawer closed.
 */
import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import GameDetailMobile from '../game-detail-mobile';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: () => ({
    data: { gameId: 'game-1', gameTitle: 'Jaipur', customCoverR2Key: null },
    isLoading: false,
    error: null,
  }),
}));

vi.mock('@/components/ui/data-display/meeple-card/MeepleCard', () => ({
  MeepleCard: ({ title }: { title?: string }) => <div>{title}</div>,
}));

vi.mock('@/components/game-detail/mobile/GameDetailsDrawer', () => ({
  GameDetailsDrawer: ({ open, initialTab }: { open: boolean; initialTab?: string }) => (
    <div data-testid="drawer-stub" data-open={String(open)} data-tab={initialTab} />
  ),
}));

vi.mock('@/components/session/StartSessionSheet', () => ({ StartSessionSheet: () => null }));
vi.mock('@/components/features/library/custom-cover/CustomCoverDialog', () => ({
  CustomCoverDialog: () => null,
}));
vi.mock('@/components/features/library/custom-cover/EditCoverOverlay', () => ({
  EditCoverOverlay: () => null,
}));

function viewportIsDesktop(matches: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockImplementation((query: string) => ({
      matches: query === '(min-width: 1024px)' ? matches : false,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }))
  );
}

function drawer() {
  const stub = screen.getByTestId('drawer-stub');
  return { open: stub.dataset.open, tab: stub.dataset.tab };
}

describe('GameDetailMobile — way into the chat (#4992)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('opens the drawer on the Agente tab from the Chat button', () => {
    viewportIsDesktop(false);
    render(<GameDetailMobile gameId="game-1" />);

    fireEvent.click(screen.getByTestId('focused-game-chat-button'));

    expect(drawer()).toEqual({ open: 'true', tab: 'aiChat' });
  });

  it('keeps Dettagli on the Info tab', () => {
    viewportIsDesktop(false);
    render(<GameDetailMobile gameId="game-1" />);

    fireEvent.click(screen.getByTestId('focused-game-chat-button'));
    fireEvent.click(screen.getByTestId('focused-game-details-button'));

    expect(drawer()).toEqual({ open: 'true', tab: 'info' });
  });

  it('opens the drawer on the tab of a deep link below lg', () => {
    viewportIsDesktop(false);
    render(<GameDetailMobile gameId="game-1" deepLinkTab="aiChat" />);

    expect(drawer()).toEqual({ open: 'true', tab: 'aiChat' });
  });

  it('leaves the drawer closed for a deep link at desktop width', () => {
    // Both layouts are mounted and CSS hides one; the drawer is a portal, so opening it here would
    // put it over the desktop page.
    viewportIsDesktop(true);
    render(<GameDetailMobile gameId="game-1" deepLinkTab="aiChat" />);

    expect(drawer().open).toBe('false');
  });

  it('leaves the drawer closed without a deep link', () => {
    viewportIsDesktop(false);
    render(<GameDetailMobile gameId="game-1" />);

    expect(drawer().open).toBe('false');
  });
});
