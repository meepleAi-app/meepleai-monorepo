/**
 * RatingBreakdown - Unit tests (Issue #1479).
 *
 * Pure presentational ratings summary. Maps from the mockup ReviewsTab
 * (sp4-toolkit-detail.jsx:847-935): average + Stars + count, a 5→1 histogram,
 * and the reviews list. Source data: ToolkitRatingsResponse (averageStars,
 * totalCount, breakdown.star1..5, items: ToolkitRating[]).
 *
 * Test matrix (Crispin):
 *   T1. Average (toFixed 1) + reviews count label.
 *   T2. Five histogram rows, one per star bucket, with counts.
 *   T3. Bar width % is count/totalCount (5★ = 7/10 → 70%).
 *   T4. EDGE division-by-zero: totalCount=0 → empty state, no NaN, no rows.
 *   T5. Reviews list: one row per review with name + comment.
 *   T6. Avatar: img when raterAvatarUrl set; neutral initials otherwise.
 *   T7. comment null → no comment paragraph.
 *   T8. data-slot attributes (card + rows).
 *   T9. DS-15 tokens.
 *   T10. className composition.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { RatingBreakdown, type RatingBreakdownReview } from '../RatingBreakdown';

const labels = {
  reviewsCountLabel: 'recensioni',
  empty: 'Ancora nessuna recensione',
};

function review(over: Partial<RatingBreakdownReview> & { id: string }): RatingBreakdownReview {
  return {
    id: over.id,
    raterDisplayName: over.raterDisplayName ?? 'Marco Rossi',
    raterAvatarUrl: over.raterAvatarUrl ?? null,
    stars: over.stars ?? 5,
    comment: 'comment' in over ? (over.comment ?? null) : 'Ottimo toolkit',
    createdAtLabel: over.createdAtLabel ?? '20 mag 2026',
  };
}

const breakdown = { star1: 0, star2: 0, star3: 1, star4: 2, star5: 7 };

const twoReviews: ReadonlyArray<RatingBreakdownReview> = [
  review({ id: 'r1', raterDisplayName: 'Marco Rossi', stars: 5, comment: 'Ottimo toolkit' }),
  review({ id: 'r2', raterDisplayName: 'Lucia Bianchi', stars: 4, comment: 'Molto utile' }),
];

describe('RatingBreakdown (Issue #1479)', () => {
  // T1
  it('renders the average and the reviews count label', () => {
    render(
      <RatingBreakdown
        averageStars={4.3}
        totalCount={10}
        breakdown={breakdown}
        reviews={twoReviews}
        labels={labels}
      />
    );
    expect(screen.getByText('4.3')).toBeInTheDocument();
    expect(screen.getByText('10 recensioni')).toBeInTheDocument();
  });

  // T2
  it('renders five histogram rows with their counts', () => {
    const { container } = render(
      <RatingBreakdown
        averageStars={4.3}
        totalCount={10}
        breakdown={breakdown}
        reviews={twoReviews}
        labels={labels}
      />
    );
    expect(container.querySelectorAll('[data-slot="toolkit-detail-rating-row"]')).toHaveLength(5);
  });

  // T3
  it('sets bar width to count/totalCount percent (5★ = 70%)', () => {
    const { container } = render(
      <RatingBreakdown
        averageStars={4.3}
        totalCount={10}
        breakdown={breakdown}
        reviews={twoReviews}
        labels={labels}
      />
    );
    // Rows render 5★ → 1★, so the first bar is the 5★ bucket (7/10 = 70%).
    const bars = container.querySelectorAll('[data-slot="toolkit-detail-rating-bar"]');
    expect((bars[0] as HTMLElement).style.width).toBe('70%');
  });

  // T4 — EDGE: division-by-zero guard
  it('shows the empty state with no NaN when totalCount is 0', () => {
    const { container } = render(
      <RatingBreakdown
        averageStars={0}
        totalCount={0}
        breakdown={{ star1: 0, star2: 0, star3: 0, star4: 0, star5: 0 }}
        reviews={[]}
        labels={labels}
      />
    );
    expect(screen.getByText('Ancora nessuna recensione')).toBeInTheDocument();
    expect(container.querySelectorAll('[data-slot="toolkit-detail-rating-row"]')).toHaveLength(0);
    expect(container.textContent ?? '').not.toContain('NaN');
  });

  // T5
  it('renders one row per review with name and comment', () => {
    render(
      <RatingBreakdown
        averageStars={4.3}
        totalCount={10}
        breakdown={breakdown}
        reviews={twoReviews}
        labels={labels}
      />
    );
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
    expect(screen.getByText('Marco Rossi')).toBeInTheDocument();
    expect(screen.getByText('Ottimo toolkit')).toBeInTheDocument();
    expect(screen.getByText('Lucia Bianchi')).toBeInTheDocument();
  });

  // T6
  it('renders an avatar image when raterAvatarUrl is set', () => {
    const { container } = render(
      <RatingBreakdown
        averageStars={5}
        totalCount={1}
        breakdown={{ star1: 0, star2: 0, star3: 0, star4: 0, star5: 1 }}
        reviews={[review({ id: 'r1', raterAvatarUrl: 'https://x/a.png' })]}
        labels={labels}
      />
    );
    expect(container.querySelector('img')).toBeInTheDocument();
  });

  it('renders neutral initials when raterAvatarUrl is null', () => {
    const { container } = render(
      <RatingBreakdown
        averageStars={5}
        totalCount={1}
        breakdown={{ star1: 0, star2: 0, star3: 0, star4: 0, star5: 1 }}
        reviews={[review({ id: 'r1', raterDisplayName: 'Marco Rossi', raterAvatarUrl: null })]}
        labels={labels}
      />
    );
    expect(container.querySelector('img')).not.toBeInTheDocument();
    expect(screen.getByText('MR')).toBeInTheDocument();
  });

  it('derives two-char initials for a single-word name', () => {
    render(
      <RatingBreakdown
        averageStars={5}
        totalCount={1}
        breakdown={{ star1: 0, star2: 0, star3: 0, star4: 0, star5: 1 }}
        reviews={[review({ id: 'r1', raterDisplayName: 'Mario', raterAvatarUrl: null })]}
        labels={labels}
      />
    );
    expect(screen.getByText('MA')).toBeInTheDocument();
  });

  // T7
  it('omits the comment paragraph when comment is null', () => {
    render(
      <RatingBreakdown
        averageStars={5}
        totalCount={1}
        breakdown={{ star1: 0, star2: 0, star3: 0, star4: 0, star5: 1 }}
        reviews={[review({ id: 'r1', raterDisplayName: 'No Comment', comment: null })]}
        labels={labels}
      />
    );
    expect(screen.getByText('No Comment')).toBeInTheDocument();
    expect(screen.queryByText('Ottimo toolkit')).not.toBeInTheDocument();
  });

  // T8
  it('exposes data-slot on the root card', () => {
    const { container } = render(
      <RatingBreakdown
        averageStars={4.3}
        totalCount={10}
        breakdown={breakdown}
        reviews={twoReviews}
        labels={labels}
      />
    );
    expect(
      container.querySelector('[data-slot="toolkit-detail-rating-breakdown"]')
    ).toBeInTheDocument();
  });

  // T9
  it('uses DS-15 tokens on the breakdown card', () => {
    const { container } = render(
      <RatingBreakdown
        averageStars={4.3}
        totalCount={10}
        breakdown={breakdown}
        reviews={twoReviews}
        labels={labels}
      />
    );
    const card = container.querySelector('[data-slot="toolkit-detail-rating-summary"]');
    expect(card).toHaveClass('bg-card');
    expect(card).toHaveClass('border-border');
  });

  // T10
  it('composes custom className with base classes', () => {
    const { container } = render(
      <RatingBreakdown
        averageStars={4.3}
        totalCount={10}
        breakdown={breakdown}
        reviews={twoReviews}
        labels={labels}
        className="extra"
      />
    );
    const root = container.querySelector('[data-slot="toolkit-detail-rating-breakdown"]');
    expect(root).toHaveClass('extra');
  });
});
