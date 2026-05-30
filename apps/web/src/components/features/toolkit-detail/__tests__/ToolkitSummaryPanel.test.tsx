/**
 * ToolkitSummaryPanel - Unit tests (Issue #1479).
 *
 * Hero summary block for `/toolkits/[id]`. Renders cover gradient backdrop,
 * author chip (with avatar image or fallback emoji), optional game chip,
 * title, description, and a 3-stat <dl> (installCount / ratingAverage via
 * Stars / currentVersion). Pure presentational; slots into the `hero` of
 * DetailPageLayout. Source: PR #1163 (closes #1145).
 *
 * Stars is a re-export of the canonical ui/feedback/Stars and is covered by
 * that canonical's own tests; here we assert only that the rating region is
 * present/absent and that the ratingCount suffix is correct.
 *
 * Test matrix (Crispin):
 *   T1. Renders title and description.
 *   T2. Author chip shows authorChipPrefix + authorName.
 *   T3. Author chip uses img tag when authorAvatarUrl is provided.
 *   T4. Author chip uses fallback emoji when authorAvatarUrl is null.
 *   T5. Game chip shown when gameName is a non-empty string.
 *   T6. Game chip absent when gameName is null.
 *   T7. Game chip absent when gameName is undefined.
 *   T8. installCount rendered via toLocaleString().
 *   T9. Rating cell shows Stars region (role=img) when ratingAverage is set.
 *   T10. Rating cell shows noRatings label when ratingAverage is null.
 *   T11. ratingCount suffix "(N)" rendered when ratingCount > 0.
 *   T12. ratingCount suffix absent when ratingCount === 0.
 *   T13. currentVersion rendered.
 *   T14. data-slot attributes present (4 slots).
 *   T15. className composition on the root header.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ToolkitSummaryPanel, type ToolkitSummaryPanelLabels } from '../ToolkitSummaryPanel';

const labels: ToolkitSummaryPanelLabels = {
  authorChipPrefix: 'by',
  gameChipPrefix: 'for',
  statsHeading: 'Statistiche toolkit',
  installCountLabel: 'Installazioni',
  ratingLabel: 'Rating',
  versionLabel: 'Versione',
  noRatings: 'Nessuna valutazione',
};

const baseProps = {
  title: 'My Toolkit',
  description: 'A useful toolkit for board games.',
  authorName: 'Alice',
  authorAvatarUrl: null,
  coverImageUrl: null,
  gameName: null,
  installCount: 1234,
  ratingAverage: 4.5,
  ratingCount: 42,
  currentVersion: '2.1.0',
  labels,
};

describe('ToolkitSummaryPanel (Issue #1479)', () => {
  // T1
  it('renders title and description', () => {
    render(<ToolkitSummaryPanel {...baseProps} />);
    expect(screen.getByText('My Toolkit')).toBeInTheDocument();
    expect(screen.getByText('A useful toolkit for board games.')).toBeInTheDocument();
  });

  // T2
  it('renders author chip with authorChipPrefix and authorName', () => {
    const { container } = render(<ToolkitSummaryPanel {...baseProps} authorName="Alice" />);
    const chip = container.querySelector('[data-slot="toolkit-detail-author-chip"]');
    expect(chip).toBeInTheDocument();
    expect(chip).toHaveTextContent('by Alice');
  });

  // T3
  it('renders an img tag inside the author chip when authorAvatarUrl is provided', () => {
    const { container } = render(
      <ToolkitSummaryPanel {...baseProps} authorAvatarUrl="https://example.com/avatar.png" />
    );
    const chip = container.querySelector('[data-slot="toolkit-detail-author-chip"]');
    expect(chip?.querySelector('img')).toBeInTheDocument();
    expect(chip?.querySelector('img')).toHaveAttribute('src', 'https://example.com/avatar.png');
  });

  // T4
  it('renders the fallback emoji (👤) when authorAvatarUrl is null', () => {
    const { container } = render(<ToolkitSummaryPanel {...baseProps} authorAvatarUrl={null} />);
    const chip = container.querySelector('[data-slot="toolkit-detail-author-chip"]');
    expect(chip?.querySelector('img')).not.toBeInTheDocument();
    expect(chip).toHaveTextContent('👤');
  });

  // T5
  it('shows the game chip when gameName is a non-empty string', () => {
    const { container } = render(<ToolkitSummaryPanel {...baseProps} gameName="Catan" />);
    const chip = container.querySelector('[data-slot="toolkit-detail-game-chip"]');
    expect(chip).toBeInTheDocument();
    expect(chip).toHaveTextContent('for Catan');
  });

  // T6
  it('does not render the game chip when gameName is null', () => {
    const { container } = render(<ToolkitSummaryPanel {...baseProps} gameName={null} />);
    expect(
      container.querySelector('[data-slot="toolkit-detail-game-chip"]')
    ).not.toBeInTheDocument();
  });

  // T7
  it('does not render the game chip when gameName is undefined', () => {
    const { container } = render(<ToolkitSummaryPanel {...baseProps} gameName={undefined} />);
    expect(
      container.querySelector('[data-slot="toolkit-detail-game-chip"]')
    ).not.toBeInTheDocument();
  });

  // T8
  it('renders installCount via toLocaleString()', () => {
    render(<ToolkitSummaryPanel {...baseProps} installCount={1234} />);
    // toLocaleString() output is locale-dependent; assert the label and the number is present
    expect(screen.getByText('Installazioni')).toBeInTheDocument();
    // The dd containing the formatted install count is in the document
    expect(screen.getByText((1234).toLocaleString())).toBeInTheDocument();
  });

  // T9
  it('shows the Stars region (role=img) when ratingAverage is provided', () => {
    render(<ToolkitSummaryPanel {...baseProps} ratingAverage={4.5} ratingCount={10} />);
    // Stars renders with role="img"
    expect(screen.getByRole('img', { name: /Rating:/i })).toBeInTheDocument();
  });

  // T10
  it('shows the noRatings label when ratingAverage is null', () => {
    render(<ToolkitSummaryPanel {...baseProps} ratingAverage={null} ratingCount={0} />);
    expect(screen.getByText('Nessuna valutazione')).toBeInTheDocument();
    expect(screen.queryByRole('img', { name: /Rating:/i })).not.toBeInTheDocument();
  });

  // T11
  it('renders the ratingCount suffix "(N)" when ratingCount > 0', () => {
    render(<ToolkitSummaryPanel {...baseProps} ratingAverage={3.0} ratingCount={7} />);
    expect(screen.getByText('(7)')).toBeInTheDocument();
  });

  // T12
  it('does not render the ratingCount suffix when ratingCount is 0', () => {
    render(<ToolkitSummaryPanel {...baseProps} ratingAverage={3.0} ratingCount={0} />);
    expect(screen.queryByText(/\(\d+\)/)).not.toBeInTheDocument();
  });

  // T13
  it('renders currentVersion', () => {
    render(<ToolkitSummaryPanel {...baseProps} currentVersion="2.1.0" />);
    expect(screen.getByText('2.1.0')).toBeInTheDocument();
  });

  // T14
  it('exposes all 4 data-slot attributes', () => {
    const { container } = render(<ToolkitSummaryPanel {...baseProps} gameName="Catan" />);
    expect(
      container.querySelector('[data-slot="toolkit-detail-summary-panel"]')
    ).toBeInTheDocument();
    expect(container.querySelector('[data-slot="toolkit-detail-author-chip"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="toolkit-detail-game-chip"]')).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="toolkit-detail-summary-stats"]')
    ).toBeInTheDocument();
  });

  // T15
  it('composes custom className with base classes on the root header', () => {
    const { container } = render(<ToolkitSummaryPanel {...baseProps} className="custom-class" />);
    const root = container.querySelector('[data-slot="toolkit-detail-summary-panel"]');
    expect(root).toHaveClass('custom-class');
    expect(root).toHaveClass('rounded-2xl');
  });
});
