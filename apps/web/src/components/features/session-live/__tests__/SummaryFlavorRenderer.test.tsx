import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { SummaryFlavorRenderer, hasSummaryFlavor } from '../SummaryFlavorRenderer';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

// Replace the lazy Catan flavor with a synchronous marker so the dispatcher's
// dynamic-import mapping resolves deterministically in the test.
vi.mock('../flavors/catan/CatanSummaryFlavor', () => ({
  CatanSummaryFlavor: () => <div data-testid="catan-flavor-marker" />,
}));

const session = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-01-01T00:00:00Z',
  completedAt: '2026-01-01T00:47:00Z',
  playerCount: 1,
  players: [],
  winnerName: null,
  notes: null,
  durationMinutes: 10,
} as unknown as GameSessionDto;

describe('hasSummaryFlavor', () => {
  it('is true for catan', () => {
    expect(hasSummaryFlavor('catan')).toBe(true);
  });

  it('is false for unknown slug / null / undefined', () => {
    expect(hasSummaryFlavor('wingspan')).toBe(false);
    expect(hasSummaryFlavor(null)).toBe(false);
    expect(hasSummaryFlavor(undefined)).toBe(false);
  });
});

describe('SummaryFlavorRenderer dispatch', () => {
  it('lazy-loads and renders the Catan flavor for gameSlug=catan', async () => {
    render(<SummaryFlavorRenderer gameSlug="catan" session={session} />);
    expect(await screen.findByTestId('catan-flavor-marker')).toBeInTheDocument();
  });

  it('renders nothing for an unknown slug', () => {
    const { container } = render(<SummaryFlavorRenderer gameSlug="wingspan" session={session} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing for a null slug', () => {
    const { container } = render(<SummaryFlavorRenderer gameSlug={null} session={session} />);
    expect(container).toBeEmptyDOMElement();
  });
});
