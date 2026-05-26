import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { RaptorPanel } from '../RaptorPanel';

const baseLabels = {
  title: 'RAPTOR Rebuild',
  description: 'Recursive Abstractive Processing Tree Of Retrieval',
  lockedBadge: 'PRO',
  activeBadge: 'PRO ✓',
  lockedNote: 'RAPTOR richiede piano Pro.',
  upgradeCta: 'Upgrade to Pro',
  upgradeLink: 'Scopri piano Pro',
  rebuildCta: 'Rebuild RAPTOR',
  metrics: {
    lastRebuild: 'Ultimo rebuild',
    summaries: 'Summaries gen.',
  },
  estimateLabel: 'Stima operazione',
  estimateDescription: '~{chunks} chunks → summaries gerarchici',
};

describe('RaptorPanel (Issue #1481)', () => {
  it('renders locked state for free tier with disabled CTA', () => {
    const { container } = render(<RaptorPanel tier="free" labels={baseLabels} />);
    expect(screen.getByText('RAPTOR Rebuild')).toBeInTheDocument();
    expect(screen.getByText('PRO')).toBeInTheDocument(); // locked badge
    expect(screen.getByText('RAPTOR richiede piano Pro.')).toBeInTheDocument();
    const upgradeBtn = container.querySelector(
      '[data-slot="kb-hub-raptor-upgrade-cta"]'
    ) as HTMLButtonElement;
    expect(upgradeBtn).toBeInTheDocument();
    expect(upgradeBtn).toBeDisabled();
  });

  it('renders active state for pro tier with enabled rebuild CTA', () => {
    const onRebuild = vi.fn();
    render(<RaptorPanel tier="pro" labels={baseLabels} onRebuild={onRebuild} />);
    expect(screen.getByText('PRO ✓')).toBeInTheDocument();
    const rebuildBtn = screen.getByRole('button', { name: 'Rebuild RAPTOR' });
    expect(rebuildBtn).toBeInTheDocument();
    fireEvent.click(rebuildBtn);
    expect(onRebuild).toHaveBeenCalledTimes(1);
  });

  it('hides metrics block when no metrics props provided (P83)', () => {
    const { container } = render(<RaptorPanel tier="free" labels={baseLabels} />);
    expect(container.querySelector('[data-slot="kb-hub-raptor-metrics"]')).not.toBeInTheDocument();
  });

  it('renders metrics block when lastRebuild + summaries provided', () => {
    const { container } = render(
      <RaptorPanel
        tier="pro"
        labels={baseLabels}
        lastRebuildRelative="12 gg fa"
        summariesCount={147}
      />
    );
    expect(container.querySelector('[data-slot="kb-hub-raptor-metrics"]')).toBeInTheDocument();
    expect(screen.getByText('12 gg fa')).toBeInTheDocument();
    expect(screen.getByText('147')).toBeInTheDocument();
  });

  it('renders estimate block only when pro tier + estimateChunks + (cost or duration)', () => {
    const { container, rerender } = render(
      <RaptorPanel tier="pro" labels={baseLabels} estimateChunks={1247} estimatedCost="$1.20" />
    );
    expect(container.querySelector('[data-slot="kb-hub-raptor-estimate"]')).toBeInTheDocument();
    expect(screen.getByText('$1.20')).toBeInTheDocument();
    // Locale-tolerant: jsdom may render "1247" instead of "1.247" (it-IT thousand sep).
    expect(
      screen.getByText(text => /^~1[.,]?247 chunks → summaries gerarchici$/.test(text))
    ).toBeInTheDocument();

    // Free tier: estimate hidden even if data provided
    rerender(
      <RaptorPanel tier="free" labels={baseLabels} estimateChunks={1247} estimatedCost="$1.20" />
    );
    expect(container.querySelector('[data-slot="kb-hub-raptor-estimate"]')).not.toBeInTheDocument();
  });

  it('invokes onUpgrade when upgrade link clicked (free tier)', () => {
    const onUpgrade = vi.fn();
    render(<RaptorPanel tier="free" labels={baseLabels} onUpgrade={onUpgrade} />);
    fireEvent.click(screen.getByRole('button', { name: /Scopri piano Pro/ }));
    expect(onUpgrade).toHaveBeenCalledTimes(1);
  });
});
