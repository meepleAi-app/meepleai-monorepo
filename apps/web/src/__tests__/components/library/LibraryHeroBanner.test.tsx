/**
 * LibraryHeroBanner Component Tests
 *
 * Test Coverage:
 * - Session variant renders session name, player count, games
 * - Discovery variant renders "Scopri nuovi giochi" when no session
 * - Loading state shows skeleton
 * - hide prop returns null
 *
 * Target: >= 90% coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LibraryHeroBanner } from '@/components/library/LibraryHeroBanner';
import type { NextSessionData } from '@/hooks/queries/useNextSession';

// ============================================================================
// Mock Setup
// ============================================================================

let mockSessionData: NextSessionData | null;
let mockIsLoading: boolean;

vi.mock('@/hooks/queries/useNextSession', () => ({
  useNextSession: () => ({
    data: mockSessionData,
    isLoading: mockIsLoading,
  }),
}));

// Mock next/link to render a plain anchor
vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('LibraryHeroBanner', () => {
  beforeEach(() => {
    mockSessionData = null;
    mockIsLoading = false;
  });

  // ==========================================================================
  // hide prop
  // ==========================================================================

  it('returns null when hide is true', () => {
    const { container } = render(<LibraryHeroBanner hide />);
    expect(container.innerHTML).toBe('');
  });

  // ==========================================================================
  // Loading state
  // ==========================================================================

  it('renders skeleton when loading', () => {
    mockIsLoading = true;
    const { container } = render(<LibraryHeroBanner />);
    const skeleton = container.firstElementChild;
    expect(skeleton).toBeTruthy();
    expect(skeleton!.className).toContain('animate-pulse');
  });

  // ==========================================================================
  // Discovery variant (no session)
  // ==========================================================================

  it('renders discovery variant when no session exists', () => {
    render(<LibraryHeroBanner />);

    expect(screen.getByText('Scopri nuovi giochi')).toBeInTheDocument();
    expect(
      screen.getByText('Esplora il catalogo e arricchisci la tua collezione')
    ).toBeInTheDocument();

    const link = screen.getByRole('link', { name: /Esplora Catalogo/i });
    expect(link).toHaveAttribute('href', '/games');
  });

  it('discovery variant has correct aria-label', () => {
    render(<LibraryHeroBanner />);
    expect(screen.getByRole('banner', { name: 'Esplora il catalogo' })).toBeInTheDocument();
  });

  // ==========================================================================
  // Session variant
  // ==========================================================================

  it('renders session variant with session name and player count', () => {
    mockSessionData = {
      id: 'session-1',
      name: 'Serata Catan',
      scheduledAt: '2026-04-01T20:00:00Z',
      playerCount: 4,
      games: ['Catan', 'Ticket to Ride'],
    };

    render(<LibraryHeroBanner />);

    expect(screen.getByText('Serata Catan')).toBeInTheDocument();
    expect(screen.getByText(/4 giocatori/)).toBeInTheDocument();
    expect(screen.getByText(/Catan, Ticket to Ride/)).toBeInTheDocument();
  });

  it('session variant links to session detail page', () => {
    mockSessionData = {
      id: 'session-42',
      name: 'Game Night',
      scheduledAt: '2026-04-01T20:00:00Z',
      playerCount: 3,
      games: [],
    };

    render(<LibraryHeroBanner />);

    const link = screen.getByRole('link', { name: /Vedi Dettagli/i });
    expect(link).toHaveAttribute('href', '/sessions/session-42');
  });

  it('session variant has correct aria-label', () => {
    mockSessionData = {
      id: 's1',
      name: 'Test',
      scheduledAt: '2026-04-01T20:00:00Z',
      playerCount: 2,
      games: [],
    };

    render(<LibraryHeroBanner />);
    expect(screen.getByRole('banner', { name: 'Prossima sessione' })).toBeInTheDocument();
  });

  it('session variant omits game text when games array is empty', () => {
    mockSessionData = {
      id: 's1',
      name: 'Quick Session',
      scheduledAt: '2026-04-01T20:00:00Z',
      playerCount: 2,
      games: [],
    };

    render(<LibraryHeroBanner />);

    // Should show player count without the dot separator
    const playerText = screen.getByText('2 giocatori');
    expect(playerText).toBeInTheDocument();
    expect(playerText.textContent).not.toContain('·');
  });

  // ==========================================================================
  // className forwarding
  // ==========================================================================

  it('forwards className to discovery variant', () => {
    const { container } = render(<LibraryHeroBanner className="mt-4" />);
    const banner = container.firstElementChild;
    expect(banner!.className).toContain('mt-4');
  });

  it('forwards className to loading skeleton', () => {
    mockIsLoading = true;
    const { container } = render(<LibraryHeroBanner className="mt-4" />);
    const skeleton = container.firstElementChild;
    expect(skeleton!.className).toContain('mt-4');
  });
});
