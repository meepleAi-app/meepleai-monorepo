/**
 * RecentiSection — priority #2 dashboard slot (Asse C plan v2 WP3 T3).
 *
 * Covers:
 *   1. Default state renders 2 cards
 *   2. Cards sorted DESC by date (most recent first)
 *   3. Session count singular/plural ("1 partita" vs "N partite")
 *   4. Invariante #7: 1 card = 1 GameNight wrapper (NOT N cards per N sessions)
 *   5. MVP rendered when present, omitted when undefined
 *   6. Thumbnails truncated to MAX 3
 *   7. Click on card calls openDrawer('gameNightEvent', id)
 *   8. Empty state hidden (null render — spec MAJ-6 matrix)
 *   9. Loading state renders twin skeletons
 *  10. Error state renders ErrorBanner with retry handler
 *  11. MAJ-7 "Vedi tutti i completati" footer link to /game-nights?status=completed
 *  12. Date formatting: "oggi" for today
 *  13. Date formatting: "ieri" for yesterday
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

import { RecentiSection, type RecentiGameNightCard } from '../RecentiSection';

const TWO_DAYS_AGO = new Date(Date.now() - 2 * 86_400_000).toISOString();
const FIVE_DAYS_AGO = new Date(Date.now() - 5 * 86_400_000).toISOString();

const baseCards: readonly RecentiGameNightCard[] = [
  {
    id: 'gn-1',
    title: 'Sabato a casa Marco',
    date: TWO_DAYS_AGO,
    sessionCount: 3,
    mvpDisplayName: 'Marco',
    gamePreviewThumbnails: [
      'https://test/wingspan.png',
      'https://test/catan.png',
      'https://test/azul.png',
    ],
  },
  {
    id: 'gn-2',
    title: 'Venerdì pizza & gioco',
    date: FIVE_DAYS_AGO,
    sessionCount: 1,
    mvpDisplayName: undefined,
    gamePreviewThumbnails: ['https://test/codenames.png'],
  },
];

describe('RecentiSection', () => {
  beforeEach(() => {
    openDrawerMock.mockClear();
  });

  it('renders 2 cards in default state', () => {
    render(<RecentiSection state="default" gameNights={baseCards} />);
    expect(screen.getByTestId('recenti-cards')).toBeInTheDocument();
    expect(screen.getByTestId('recenti-card-gn-1')).toBeInTheDocument();
    expect(screen.getByTestId('recenti-card-gn-2')).toBeInTheDocument();
  });

  it('sorts cards DESC by date (most recent first) regardless of input order', () => {
    const reversed: readonly RecentiGameNightCard[] = [baseCards[1], baseCards[0]];
    render(<RecentiSection state="default" gameNights={reversed} />);
    const cards = screen
      .getAllByRole('button')
      .filter(b => (b as HTMLElement).dataset.testid?.startsWith('recenti-card-'));
    expect((cards[0] as HTMLElement).dataset.testid).toBe('recenti-card-gn-1'); // 2 days ago, most recent
    expect((cards[1] as HTMLElement).dataset.testid).toBe('recenti-card-gn-2'); // 5 days ago
  });

  it('renders session count with correct singular/plural', () => {
    render(<RecentiSection state="default" gameNights={baseCards} />);
    expect(screen.getByText('3 partite')).toBeInTheDocument();
    expect(screen.getByText('1 partita')).toBeInTheDocument();
  });

  it('Invariante #7: 1 card = 1 GameNight wrapper (NOT N cards per N sessions)', () => {
    // gn-1 has sessionCount=3, but renders as 1 card (not 3).
    // gn-2 has sessionCount=1. Total cards expected: 2 (the GameNight wrappers).
    render(<RecentiSection state="default" gameNights={baseCards} />);
    const cards = screen
      .getAllByRole('button')
      .filter(b => (b as HTMLElement).dataset.testid?.startsWith('recenti-card-'));
    expect(cards).toHaveLength(2); // 2 GameNight wrappers, NOT 4 (3+1 sessions)
  });

  it('renders MVP display name when present (gn-1)', () => {
    render(<RecentiSection state="default" gameNights={baseCards} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getAllByText('MVP')).toHaveLength(1); // only gn-1 has MVP
  });

  it('omits MVP rendering when mvpDisplayName is undefined (gn-2)', () => {
    render(<RecentiSection state="default" gameNights={[baseCards[1]]} />);
    expect(screen.queryByText('MVP')).not.toBeInTheDocument();
  });

  it('truncates thumbnails to MAX 3 when input has more', () => {
    const withMany: RecentiGameNightCard = {
      ...baseCards[0],
      gamePreviewThumbnails: ['a.png', 'b.png', 'c.png', 'd.png', 'e.png'],
    };
    render(<RecentiSection state="default" gameNights={[withMany]} />);
    const thumbnails = screen.getByTestId('recenti-thumbnails-gn-1');
    expect(thumbnails.children).toHaveLength(3);
  });

  it('renders single thumbnail for gn-2 (1 game)', () => {
    render(<RecentiSection state="default" gameNights={[baseCards[1]]} />);
    const thumbnails = screen.getByTestId('recenti-thumbnails-gn-2');
    expect(thumbnails.children).toHaveLength(1);
  });

  it('omits thumbnail list when gamePreviewThumbnails is empty', () => {
    const noThumbs: RecentiGameNightCard = {
      ...baseCards[0],
      gamePreviewThumbnails: [],
    };
    render(<RecentiSection state="default" gameNights={[noThumbs]} />);
    expect(screen.queryByTestId('recenti-thumbnails-gn-1')).not.toBeInTheDocument();
  });

  it('opens drawer with ("event", id) on card click', async () => {
    const user = userEvent.setup();
    render(<RecentiSection state="default" gameNights={baseCards} />);
    await user.click(screen.getByTestId('recenti-card-gn-1'));
    expect(openDrawerMock).toHaveBeenCalledWith('gameNightEvent', 'gn-1');
  });

  it('renders empty state as null (hidden — spec MAJ-6 matrix)', () => {
    const { container } = render(<RecentiSection state="empty" />);
    expect(container.firstChild).toBeNull();
  });

  it('renders loading state with twin skeletons', () => {
    render(<RecentiSection state="loading" />);
    expect(screen.getByTestId('recenti-skeleton')).toBeInTheDocument();
  });

  it('renders error state with ErrorBanner + retry handler', async () => {
    const onRetry = vi.fn();
    const user = userEvent.setup();
    render(<RecentiSection state="error" onRetry={onRetry} />);
    expect(screen.getByTestId('recenti-error')).toBeInTheDocument();
    // ErrorBanner exposes the retry button when onRetry is wired.
    const retryBtn = screen.getByRole('button', { name: /Riprova/i });
    await user.click(retryBtn);
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('MAJ-7: renders "Vedi tutti i completati" footer link', () => {
    render(<RecentiSection state="default" gameNights={baseCards} />);
    const link = screen.getByTestId('recenti-vedi-tutti');
    expect(link).toHaveAttribute('href', '/game-nights?status=completed');
    expect(link).toHaveTextContent('Vedi tutti i completati');
  });

  it('formats "oggi" for today', () => {
    const today: RecentiGameNightCard = {
      ...baseCards[0],
      id: 'gn-today',
      date: new Date().toISOString(),
    };
    render(<RecentiSection state="default" gameNights={[today]} />);
    expect(screen.getByText('oggi')).toBeInTheDocument();
  });

  it('formats "ieri" for yesterday', () => {
    const yesterday: RecentiGameNightCard = {
      ...baseCards[0],
      id: 'gn-yesterday',
      date: new Date(Date.now() - 86_400_000).toISOString(),
    };
    render(<RecentiSection state="default" gameNights={[yesterday]} />);
    expect(screen.getByText('ieri')).toBeInTheDocument();
  });
});
