/**
 * ProssimiSection — priority #1 dashboard slot (Asse C plan v2 WP2 T2).
 *
 * Covers:
 *   1. Default state renders 2 cards
 *   2. Cards sorted ASC by date
 *   3. "IN CORSO" badge appears only on InProgress status
 *   4. RSVP summary: confirmed-only when pending=0
 *   5. RSVP summary: "X/Y pending" when pending>0
 *   6. Click on card calls openDrawer('gameNightEvent', id) [#1929 WP4]
 *   7. Empty state renders EmptySection CTA
 *   8. Loading state renders twin skeletons
 *   9. Error state renders ErrorBanner with retry
 *  10. "+ Nuova" CTA in header points to /game-nights/new
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const openDrawerMock = vi.fn();

vi.mock('@/lib/stores/cascade-navigation-store', () => ({
  useCascadeNavigationStore: (selector: (s: { openDrawer: typeof openDrawerMock }) => unknown) =>
    selector({ openDrawer: openDrawerMock }),
}));

vi.mock('next/link', () => ({
  __esModule: true,
  default: ({
    href,
    children,
    ...props
  }: React.PropsWithChildren<{ href: string } & Record<string, unknown>>) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

import { ProssimiSection, type ProssimiGameNightCard } from '../ProssimiSection';

const TOMORROW = new Date(Date.now() + 86_400_000).toISOString();
const DAY_AFTER = new Date(Date.now() + 172_800_000).toISOString();

const baseCards: readonly ProssimiGameNightCard[] = [
  {
    id: 'gn-1',
    title: 'Casa Marco',
    date: TOMORROW,
    status: 'Published',
    rsvpConfirmedCount: 4,
    rsvpPendingCount: 0,
    rsvpTotalCount: 4,
  },
  {
    id: 'gn-2',
    title: 'Casa Anna',
    date: DAY_AFTER,
    status: 'InProgress',
    rsvpConfirmedCount: 2,
    rsvpPendingCount: 2,
    rsvpTotalCount: 4,
  },
];

describe('ProssimiSection', () => {
  beforeEach(() => {
    openDrawerMock.mockClear();
  });

  it('renders 2 cards in default state', () => {
    render(<ProssimiSection state="default" gameNights={baseCards} />);
    expect(screen.getByTestId('prossimi-cards')).toBeInTheDocument();
    expect(screen.getByTestId('prossimi-card-gn-1')).toBeInTheDocument();
    expect(screen.getByTestId('prossimi-card-gn-2')).toBeInTheDocument();
  });

  it('sorts cards by date ascending', () => {
    const reversed: ProssimiGameNightCard[] = [baseCards[1], baseCards[0]];
    render(<ProssimiSection state="default" gameNights={reversed} />);
    const cards = screen
      .getAllByRole('button')
      .filter(b => b.getAttribute('data-testid')?.startsWith('prossimi-card-'));
    expect(cards[0].getAttribute('data-testid')).toBe('prossimi-card-gn-1');
    expect(cards[1].getAttribute('data-testid')).toBe('prossimi-card-gn-2');
  });

  it('shows IN CORSO badge for InProgress status only', () => {
    render(<ProssimiSection state="default" gameNights={baseCards} />);
    expect(screen.getByTestId('prossimi-badge-gn-2')).toHaveTextContent('IN CORSO');
    expect(screen.queryByTestId('prossimi-badge-gn-1')).not.toBeInTheDocument();
  });

  it('renders RSVP counter (confirmed only) when pending=0', () => {
    render(<ProssimiSection state="default" gameNights={baseCards} />);
    expect(screen.getByText('4 ✓')).toBeInTheDocument();
  });

  it('renders RSVP counter as confirmed/total + "pending" suffix when pending>0', () => {
    render(<ProssimiSection state="default" gameNights={baseCards} />);
    expect(screen.getByText('2/4 pending')).toBeInTheDocument();
  });

  it('opens drawer with event entity on card click', async () => {
    const userEvent = (await import('@testing-library/user-event')).default;
    const user = userEvent.setup();
    render(<ProssimiSection state="default" gameNights={baseCards} />);
    await user.click(screen.getByTestId('prossimi-card-gn-1'));
    expect(openDrawerMock).toHaveBeenCalledWith('gameNightEvent', 'gn-1');
  });

  it('renders empty state with CTA pointing to /game-nights/new', () => {
    render(<ProssimiSection state="empty" />);
    expect(screen.getByTestId('prossimi-empty')).toBeInTheDocument();
    expect(screen.getByText(/Crea la tua prima Game Night/i)).toBeInTheDocument();
    const cta = screen.getByRole('link', {
      name: /Crea la tua prima Game Night/i,
    });
    expect(cta).toHaveAttribute('href', '/game-nights/new');
  });

  it('renders loading state with twin skeletons', () => {
    const { container } = render(<ProssimiSection state="loading" />);
    expect(screen.getByTestId('prossimi-skeleton')).toBeInTheDocument();
    const skeletons = container.querySelectorAll('[data-slot="dashboard-section-skeleton"]');
    expect(skeletons.length).toBe(2);
  });

  it('renders error state with retry button', () => {
    const retry = vi.fn();
    render(<ProssimiSection state="error" onRetry={retry} />);
    expect(screen.getByTestId('prossimi-error')).toBeInTheDocument();
    expect(screen.getByText('Impossibile caricare le prossime Game Night.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Riprova/i })).toBeInTheDocument();
  });

  it('renders "+ Nuova" CTA in header pointing to /game-nights/new', () => {
    render(<ProssimiSection state="default" gameNights={baseCards} />);
    const cta = screen.getByTestId('prossimi-cta-new');
    expect(cta).toHaveAttribute('href', '/game-nights/new');
    expect(cta).toHaveTextContent('+ Nuova');
  });
});
