/**
 * Wave A.4 (Issue #603) — EmptyState rendering tests.
 *
 * Verifies the empty-tab placeholder contract from spec §3.7:
 *  - KIND_ICON map renders correct emoji per kind
 *  - data-slot + data-kind attributes for query stability
 *  - Title (h3) + description (p) text rendering
 *  - Optional className passthrough
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EmptyState, type EmptyStateKind } from './empty-state';

const baseLabels = {
  title: 'No items yet',
  description: 'There is nothing here for now.',
};

describe('EmptyState (Wave A.4)', () => {
  it('renders title and description text', () => {
    render(<EmptyState kind="no-toolkits" labels={baseLabels} />);
    expect(screen.getByRole('heading', { level: 3, name: 'No items yet' })).toBeInTheDocument();
    expect(screen.getByText('There is nothing here for now.')).toBeInTheDocument();
  });

  it.each<[EmptyStateKind, string]>([
    ['no-toolkits', '🧰'],
    ['no-agents', '🤖'],
    ['no-kbs', '📚'],
  ])('renders correct icon for kind=%s', (kind, expectedIcon) => {
    render(<EmptyState kind={kind} labels={baseLabels} />);
    expect(screen.getByText(expectedIcon)).toBeInTheDocument();
  });

  it('exposes data-slot and data-kind attributes', () => {
    const { container } = render(<EmptyState kind="no-agents" labels={baseLabels} />);
    const root = container.querySelector('[data-slot="shared-game-detail-empty-state"]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('data-kind', 'no-agents');
  });

  it('passes through optional className', () => {
    const { container } = render(
      <EmptyState kind="no-kbs" labels={baseLabels} className="my-custom-class" />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-empty-state"]');
    expect(root?.className).toContain('my-custom-class');
  });
});
