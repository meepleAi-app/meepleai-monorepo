/**
 * SuggestedSection — priority #3 dashboard slot (Asse C plan v2 WP4 T4).
 *
 * Covers:
 *   1. Default state renders cards in suggested grid
 *   2. Empty state returns null (silent fallback — spec MAJ-6 matrix)
 *   3. Error state returns null (silent fallback — spec MAJ-6 matrix)
 *   4. Loading state renders twin skeletons
 *   5. Cards include cover image when coverImageUrl present
 *   6. Cards fall back to emoji placeholder when coverImageUrl absent
 *   7. Cards link to /library/[id]
 *   8. Meta label combines playerCount + durationMin
 *   9. Section uses entity="game" tint (icon + count pill)
 *  10. Default state with empty array also returns null (defensive)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { vi } from 'vitest';

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

import { SuggestedSection, type SuggestedGameCard } from '../SuggestedSection';

const baseGames: readonly SuggestedGameCard[] = [
  {
    id: 'g-1',
    title: 'Wingspan',
    coverImageUrl: 'https://test/wingspan.png',
    playerCount: '1-5',
    durationMin: 70,
  },
  {
    id: 'g-2',
    title: 'Azul',
    coverImageUrl: undefined,
    playerCount: '2-4',
    durationMin: 45,
  },
];

describe('SuggestedSection', () => {
  it('renders cards in default state', () => {
    render(<SuggestedSection state="default" games={baseGames} />);
    expect(screen.getByTestId('suggested-cards')).toBeInTheDocument();
    expect(screen.getByTestId('suggested-card-g-1')).toBeInTheDocument();
    expect(screen.getByTestId('suggested-card-g-2')).toBeInTheDocument();
  });

  it('renders empty state as null (silent fallback — spec MAJ-6)', () => {
    const { container } = render(<SuggestedSection state="empty" />);
    expect(container.firstChild).toBeNull();
  });

  it('renders error state as null (silent fallback — spec MAJ-6)', () => {
    const onRetry = vi.fn();
    const { container } = render(<SuggestedSection state="error" onRetry={onRetry} />);
    expect(container.firstChild).toBeNull();
    // onRetry intentionally NOT exposed in DOM (no retry button rendered).
    expect(onRetry).not.toHaveBeenCalled();
  });

  it('renders loading state with twin skeletons', () => {
    const { container } = render(<SuggestedSection state="loading" />);
    expect(screen.getByTestId('suggested-skeleton')).toBeInTheDocument();
    const skeletons = container.querySelectorAll('[data-slot="dashboard-section-skeleton"]');
    expect(skeletons.length).toBe(2);
  });

  it('renders cover image when coverImageUrl is present (g-1)', () => {
    render(<SuggestedSection state="default" games={[baseGames[0]]} />);
    const cover = screen.getByTestId('suggested-cover-g-1');
    const img = cover.querySelector('img');
    expect(img).not.toBeNull();
    expect(img?.getAttribute('src')).toBe('https://test/wingspan.png');
  });

  it('falls back to emoji placeholder when coverImageUrl is absent (g-2)', () => {
    render(<SuggestedSection state="default" games={[baseGames[1]]} />);
    const cover = screen.getByTestId('suggested-cover-g-2');
    expect(cover.querySelector('img')).toBeNull();
    // The placeholder is the dice emoji `🎲` rendered inside the cover.
    expect(cover.textContent).toContain('🎲');
  });

  it('links each card to /library/[id]', () => {
    render(<SuggestedSection state="default" games={baseGames} />);
    expect(screen.getByTestId('suggested-card-g-1')).toHaveAttribute('href', '/library/g-1');
    expect(screen.getByTestId('suggested-card-g-2')).toHaveAttribute('href', '/library/g-2');
  });

  it('renders meta label combining playerCount + durationMin', () => {
    render(<SuggestedSection state="default" games={baseGames} />);
    expect(screen.getByText('1-5 giocatori · 70 min')).toBeInTheDocument();
    expect(screen.getByText('2-4 giocatori · 45 min')).toBeInTheDocument();
  });

  it('uses entity="game" tint (icon + count pill)', () => {
    const { container } = render(<SuggestedSection state="default" games={baseGames} />);
    const section = container.querySelector('[data-slot="dashboard-section"]');
    expect(section).not.toBeNull();
    expect(section?.getAttribute('data-section-id')).toBe('suggested');
    // DashboardSection applies entity tint via Tailwind utility class hsl(var(--c-game)).
    expect(section?.innerHTML).toContain('hsl(var(--c-game)');
    // The section title and dice icon are rendered.
    expect(screen.getByText('Potresti giocare')).toBeInTheDocument();
    // Count pill reflects games.length (2).
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('returns null when default state receives an empty array (defensive)', () => {
    const { container } = render(<SuggestedSection state="default" games={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
