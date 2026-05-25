/**
 * VersionTimeline - Unit tests (Issue #1479).
 *
 * Pure presentational version history rail. Maps from the mockup VersionsTab
 * (sp4-toolkit-detail.jsx:743-841). Source data: ToolkitVersion[] (version,
 * publishedAt, changelog, isCurrent). The backend has NO `kind` field and
 * gives `changelog` as a single string (not notes[]) — so `kind` is optional
 * and the changelog is split on newlines into bullet lines.
 *
 * Owner actions (edit notes / yank) are deferred: the v1 backend has no yank
 * workflow, so they are out of scope for this shelf-ready component.
 *
 * Test matrix (Crispin):
 *   T1. One item per version, labelled "v{version}".
 *   T2. data-slot attributes (root + items).
 *   T3. isCurrent → current badge shown exactly once.
 *   T4. kind present → kind badge; kind absent → no kind badge.
 *   T5. changelog split on newlines into bullet lines.
 *   T6. publishedAtLabel rendered.
 *   T7. Empty state (no versions) → labels.empty, no items.
 *   T8. Rail connector between items: N-1 for N items.
 *   T9. DS-15 tokens on the item card.
 *   T10. className composition.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { VersionTimeline, type VersionTimelineItem } from '../VersionTimeline';

const labels = {
  currentBadge: 'Corrente',
  empty: 'Nessuna versione pubblicata',
};

const threeVersions: ReadonlyArray<VersionTimelineItem> = [
  {
    version: '1.2.0',
    publishedAtLabel: '20 mag 2026',
    changelog: 'Aggiunto supporto X\nMigliorato Y',
    isCurrent: true,
    kind: 'minor',
  },
  {
    version: '1.1.0',
    publishedAtLabel: '10 mag 2026',
    changelog: 'Fix bug Z',
    isCurrent: false,
    kind: 'patch',
  },
  {
    version: '1.0.0',
    publishedAtLabel: '1 mag 2026',
    changelog: 'Release iniziale',
    isCurrent: false,
    kind: 'major',
  },
];

describe('VersionTimeline (Issue #1479)', () => {
  // T1
  it('renders one item per version labelled v{version}', () => {
    render(<VersionTimeline versions={threeVersions} labels={labels} />);
    expect(screen.getByText('v1.2.0')).toBeInTheDocument();
    expect(screen.getByText('v1.1.0')).toBeInTheDocument();
    expect(screen.getByText('v1.0.0')).toBeInTheDocument();
  });

  // T2
  it('exposes data-slot on the root and items', () => {
    const { container } = render(<VersionTimeline versions={threeVersions} labels={labels} />);
    expect(
      container.querySelector('[data-slot="toolkit-detail-version-timeline"]')
    ).toBeInTheDocument();
    expect(container.querySelectorAll('[data-slot="toolkit-detail-version-item"]')).toHaveLength(3);
  });

  // T3
  it('shows the current badge exactly once for the current version', () => {
    render(<VersionTimeline versions={threeVersions} labels={labels} />);
    expect(screen.getAllByText(/Corrente/)).toHaveLength(1);
  });

  // T4
  it('renders the kind badge when kind is provided', () => {
    const { container } = render(<VersionTimeline versions={threeVersions} labels={labels} />);
    const kinds = container.querySelectorAll('[data-slot="toolkit-detail-version-kind"]');
    expect(kinds).toHaveLength(3);
    expect(screen.getByText('minor')).toBeInTheDocument();
    expect(screen.getByText('patch')).toBeInTheDocument();
    expect(screen.getByText('major')).toBeInTheDocument();
  });

  it('omits the kind badge when kind is undefined', () => {
    const { container } = render(
      <VersionTimeline
        versions={[
          { version: '1.0.0', publishedAtLabel: '1 mag 2026', changelog: 'x', isCurrent: true },
        ]}
        labels={labels}
      />
    );
    expect(
      container.querySelector('[data-slot="toolkit-detail-version-kind"]')
    ).not.toBeInTheDocument();
  });

  // T5
  it('splits the changelog on newlines into bullet lines', () => {
    render(<VersionTimeline versions={threeVersions} labels={labels} />);
    expect(screen.getByText('Aggiunto supporto X')).toBeInTheDocument();
    expect(screen.getByText('Migliorato Y')).toBeInTheDocument();
    expect(screen.getByText('Fix bug Z')).toBeInTheDocument();
  });

  // T6
  it('renders the published-at label', () => {
    render(<VersionTimeline versions={threeVersions} labels={labels} />);
    expect(screen.getByText('20 mag 2026')).toBeInTheDocument();
  });

  // T7
  it('renders the empty state and no items when versions is empty', () => {
    const { container } = render(<VersionTimeline versions={[]} labels={labels} />);
    expect(screen.getByText('Nessuna versione pubblicata')).toBeInTheDocument();
    expect(container.querySelectorAll('[data-slot="toolkit-detail-version-item"]')).toHaveLength(0);
  });

  // T8
  it('renders N-1 rail connectors for N items', () => {
    const { container } = render(<VersionTimeline versions={threeVersions} labels={labels} />);
    expect(
      container.querySelectorAll('[data-slot="toolkit-detail-version-connector"]')
    ).toHaveLength(2);
  });

  it('renders 0 connectors for a single version', () => {
    const { container } = render(
      <VersionTimeline versions={[threeVersions[0]!]} labels={labels} />
    );
    expect(
      container.querySelectorAll('[data-slot="toolkit-detail-version-connector"]')
    ).toHaveLength(0);
  });

  it('renders no changelog list when the changelog is empty', () => {
    const { container } = render(
      <VersionTimeline
        versions={[
          { version: '1.0.0', publishedAtLabel: '1 mag 2026', changelog: '', isCurrent: true },
        ]}
        labels={labels}
      />
    );
    expect(container.querySelector('ul')).not.toBeInTheDocument();
  });

  // T9
  it('uses DS-15 tokens on the item card', () => {
    const { container } = render(<VersionTimeline versions={threeVersions} labels={labels} />);
    const item = container.querySelector('[data-slot="toolkit-detail-version-item-card"]');
    expect(item).toHaveClass('bg-card');
    expect(item).toHaveClass('border-border');
  });

  // T10
  it('composes custom className with base classes on the root', () => {
    const { container } = render(
      <VersionTimeline versions={threeVersions} labels={labels} className="extra" />
    );
    const root = container.querySelector('[data-slot="toolkit-detail-version-timeline"]');
    expect(root).toHaveClass('extra');
  });
});
