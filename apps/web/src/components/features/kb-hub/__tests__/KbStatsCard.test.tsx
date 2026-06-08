import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { KbStatsCard } from '../KbStatsCard';

const baseLabels = {
  cardTitle: 'KB Coverage Stats',
  cardSubtitle: 'Metriche indicizzazione',
  docsLabel: 'Documenti',
  chunksLabel: 'Chunks',
  embeddingsLabel: 'Embeddings',
  lastReindexLabel: 'Ultima idx.',
  raptorLabel: 'RAPTOR last',
  coverageLabel: 'Copertura KB',
  coverage: {
    None: 'Nessuna',
    Basic: 'Base',
    Standard: 'Standard',
    Complete: 'Completa',
  },
  lifetimeCostLabel: 'Costo lifetime token',
  sparklineLabel: 'Consumo token · ultimi 7 gg',
  sparklineStart: '-7gg',
  sparklineEnd: 'oggi',
};

describe('KbStatsCard (Issue #1481)', () => {
  // #1816 P3-7 Phase 2 — indexing-pending badge.
  describe('indexingPending badge (#1816 P3-7)', () => {
    const labelsWithBadge = {
      ...baseLabels,
      indexingBadge: '⏳ Indexing in progress',
      indexingDescription: 'The document is uploaded but not yet searchable from chat.',
    };

    it('does NOT render the indexing badge by default (prop omitted)', () => {
      const { container } = render(
        <KbStatsCard
          documentCount={0}
          coverageLevel="None"
          coverageScore={0}
          labels={labelsWithBadge}
        />
      );
      expect(
        container.querySelector('[data-slot="kb-hub-stats-indexing-badge"]')
      ).not.toBeInTheDocument();
    });

    it('does NOT render the indexing badge when indexingPending=false', () => {
      const { container } = render(
        <KbStatsCard
          documentCount={3}
          coverageLevel="Standard"
          coverageScore={70}
          labels={labelsWithBadge}
          indexingPending={false}
        />
      );
      expect(
        container.querySelector('[data-slot="kb-hub-stats-indexing-badge"]')
      ).not.toBeInTheDocument();
    });

    it('renders the indexing badge + description when indexingPending=true and labels provided', () => {
      const { container } = render(
        <KbStatsCard
          documentCount={0}
          coverageLevel="None"
          coverageScore={0}
          labels={labelsWithBadge}
          indexingPending={true}
        />
      );
      const badge = container.querySelector('[data-slot="kb-hub-stats-indexing-badge"]');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveAttribute('role', 'status');
      expect(badge).toHaveAttribute('aria-live', 'polite');
      expect(screen.getByText('⏳ Indexing in progress')).toBeInTheDocument();
      expect(
        screen.getByText('The document is uploaded but not yet searchable from chat.')
      ).toBeInTheDocument();
    });

    it('does NOT render the badge when indexingPending=true but labels.indexingBadge is missing', () => {
      const { container } = render(
        <KbStatsCard
          documentCount={0}
          coverageLevel="None"
          coverageScore={0}
          labels={baseLabels}
          indexingPending={true}
        />
      );
      // The component guards on BOTH the flag AND the presence of the label —
      // an i18n-key resolution miss must not render an empty badge slot.
      expect(
        container.querySelector('[data-slot="kb-hub-stats-indexing-badge"]')
      ).not.toBeInTheDocument();
    });
  });

  it('renders required fields: documentCount, coverageLevel, coverageScore', () => {
    render(
      <KbStatsCard
        documentCount={12}
        coverageLevel="Standard"
        coverageScore={73}
        labels={baseLabels}
      />
    );
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Documenti')).toBeInTheDocument();
    expect(screen.getByText(/Standard · 73%/)).toBeInTheDocument();
  });

  it('hides deferred metric tiles when corresponding props undefined (P83)', () => {
    const { container } = render(
      <KbStatsCard documentCount={12} coverageLevel="None" coverageScore={0} labels={baseLabels} />
    );
    expect(container.querySelector('[data-slot="kb-hub-stats-metric-docs"]')).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="kb-hub-stats-metric-chunks"]')
    ).not.toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="kb-hub-stats-metric-embeddings"]')
    ).not.toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="kb-hub-stats-metric-lastReindex"]')
    ).not.toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="kb-hub-stats-metric-raptor"]')
    ).not.toBeInTheDocument();
  });

  it('renders deferred metric tiles when corresponding props provided', () => {
    const { container } = render(
      <KbStatsCard
        documentCount={12}
        coverageLevel="Complete"
        coverageScore={100}
        chunks={1247}
        embeddings={4891}
        lastReindexRelative="3 gg fa"
        raptorLastRebuildRelative="12 gg fa"
        labels={baseLabels}
      />
    );
    expect(container.querySelector('[data-slot="kb-hub-stats-metric-chunks"]')).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="kb-hub-stats-metric-embeddings"]')
    ).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="kb-hub-stats-metric-lastReindex"]')
    ).toBeInTheDocument();
    expect(container.querySelector('[data-slot="kb-hub-stats-metric-raptor"]')).toBeInTheDocument();
    // Locale-tolerant: jsdom may fall back to en-US (no thousand separator) even when
    // toLocaleString('it-IT') is requested. Match either "1.247" or "1,247" or "1247".
    expect(screen.getByText(/1[.,]?247/)).toBeInTheDocument();
    expect(screen.getByText(/4[.,]?891/)).toBeInTheDocument();
    expect(screen.getByText('3 gg fa')).toBeInTheDocument();
  });

  it('renders lifetime cost and sparkline only when both data + non-compact', () => {
    const { container, rerender } = render(
      <KbStatsCard
        documentCount={12}
        coverageLevel="Standard"
        coverageScore={73}
        lifetimeCost="$2.84"
        costHistory={[0.12, 0.38, 0.22, 0.45, 0.19, 0.84, 0.64]}
        labels={baseLabels}
      />
    );
    expect(container.querySelector('[data-slot="kb-hub-stats-lifetime-cost"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="kb-hub-stats-sparkline"]')).toBeInTheDocument();
    expect(screen.getByText('$2.84')).toBeInTheDocument();

    rerender(
      <KbStatsCard
        documentCount={12}
        coverageLevel="Standard"
        coverageScore={73}
        lifetimeCost="$2.84"
        costHistory={[0.12, 0.38, 0.22, 0.45, 0.19, 0.84, 0.64]}
        compact
        labels={baseLabels}
      />
    );
    // Compact mode hides lifetime cost + sparkline + header
    expect(
      container.querySelector('[data-slot="kb-hub-stats-lifetime-cost"]')
    ).not.toBeInTheDocument();
    expect(container.querySelector('[data-slot="kb-hub-stats-sparkline"]')).not.toBeInTheDocument();
  });

  it('hides the sparkline when costHistory contains only zeros (F9 #1974)', () => {
    // F9 regression guard: the BE seeds an empty 7-day cost window for KBs
    // that have never been used. Pre-fix the sparkline rendered 7 flat
    // zero-height bars — pure UI noise. Now the chart is hidden unless at
    // least one datapoint is non-zero.
    const { container } = render(
      <KbStatsCard
        documentCount={12}
        coverageLevel="Standard"
        coverageScore={73}
        lifetimeCost="$0.00"
        costHistory={[0, 0, 0, 0, 0, 0, 0]}
        labels={baseLabels}
      />
    );
    expect(container.querySelector('[data-slot="kb-hub-stats-sparkline"]')).not.toBeInTheDocument();
    // Lifetime cost row is independent of sparkline signal — it still renders
    // (the card still wants to surface the explicit "$0.00 lifetime" state).
    expect(container.querySelector('[data-slot="kb-hub-stats-lifetime-cost"]')).toBeInTheDocument();
  });

  it('renders the sparkline when at least one datapoint is non-zero (F9 #1974)', () => {
    const { container } = render(
      <KbStatsCard
        documentCount={12}
        coverageLevel="Standard"
        coverageScore={73}
        lifetimeCost="$0.04"
        // 6 zeros + 1 spike — first real activity within the window.
        costHistory={[0, 0, 0, 0, 0, 0, 0.04]}
        labels={baseLabels}
      />
    );
    expect(container.querySelector('[data-slot="kb-hub-stats-sparkline"]')).toBeInTheDocument();
  });
});
