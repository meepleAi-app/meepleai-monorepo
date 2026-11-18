/**
 * #5072: the title cell renders the card image through SafeImg. A BGG image (#2123, ADR-059 §5) never reaches the
 * DOM: the entity tile shows instead, as with no image.
 */

import { render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { EntityTable } from '../EntityTable';

const reportBggAttempt = vi.hoisted(() => vi.fn());
vi.mock('@/lib/images/bgg-attempt-beacon', () => ({ reportBggAttempt }));

const BGG = 'https://cf.geekdo-images.com/abc/pic1.jpg';
const ALLOWED = 'https://covers.meepleai.app/abc/cover.webp';

beforeEach(() => {
  reportBggAttempt.mockClear();
});

describe('EntityTable title cell (#5072)', () => {
  it('shows the entity tile instead of a BGG image, and an allowed image as an <img> (#5072)', () => {
    const { container } = render(
      <EntityTable
        cards={[
          { entity: 'game', title: 'Azul', imageUrl: BGG },
          { entity: 'game', title: 'Brass', imageUrl: ALLOWED },
        ]}
      />
    );

    const srcs = Array.from(container.querySelectorAll('img'), img => img.getAttribute('src'));
    expect(srcs).toEqual([ALLOWED]);
    expect(reportBggAttempt).toHaveBeenCalledWith(BGG);

    const azulRow = screen.getByText('Azul').closest('tr')!;
    expect(within(azulRow).getByText('🎲')).toBeInTheDocument();
  });
});
