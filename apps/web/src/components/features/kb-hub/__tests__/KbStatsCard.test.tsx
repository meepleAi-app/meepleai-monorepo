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
});
