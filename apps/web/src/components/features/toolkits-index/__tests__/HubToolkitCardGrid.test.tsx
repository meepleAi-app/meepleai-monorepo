/**
 * HubToolkitCardGrid - Unit tests (Issue #1480).
 *
 * Pure presentational card for a single toolkit in the `/toolkits` hub grid.
 * Maps from sp4-hub-toolkits.jsx:104-228 (function HubToolkitCardGrid).
 * Note: despite the name, this is the CARD (per-item), NOT a grid container.
 * The orchestrator renders many of these inside a CSS grid.
 *
 * Props derived from `RecommendedToolkit` (BE Zod schema). Several mockup
 * fields are P83 deferred (no BE backing yet): version, toolCount, useCount,
 * gameName, badge. The component gracefully hides them when undefined.
 *
 * Test matrix (Crispin):
 *   T1. data-slot on root.
 *   T2. Renders title + authorName + installCount.
 *   T3. ratingAverage non-null → Stars + numeric value rendered; null → no rating row.
 *   T4. P83 deferred fields (version/toolCount/useCount): present → rendered, absent → hidden.
 *   T5. gameName present → rendered; null/undefined → fallback "Universale" label.
 *   T6. badge present → rendered; absent → no badge.
 *   T7. Install button click → onInstall callback with toolkit id.
 *   T8. className composition + axe a11y scan.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { HubToolkitCardGrid, type HubToolkitCardItem } from '../HubToolkitCardGrid';

const labels = {
  gameRefFallback: 'Universale (multi-game)',
  installCta: '+ Installa toolkit',
  installAriaLabel: 'Installa {title}',
  toolsLabel: 'Strumenti:',
  usesLabel: 'Uses:',
};

function makeToolkit(over?: Partial<HubToolkitCardItem>): HubToolkitCardItem {
  return {
    id: over?.id ?? 'tk-1',
    title: over?.title ?? 'Azul Tools',
    authorName: over?.authorName ?? 'Marco Rossi',
    installCount: over?.installCount ?? 42,
    ratingAverage: over && 'ratingAverage' in over ? (over.ratingAverage as number | null) : 4.5,
    ratingCount: over?.ratingCount ?? 18,
    coverImageUrl: over?.coverImageUrl ?? null,
    coverEmoji: over?.coverEmoji ?? '🧰',
    ...(over?.version !== undefined ? { version: over.version } : {}),
    ...(over?.toolCount !== undefined ? { toolCount: over.toolCount } : {}),
    ...(over?.useCount !== undefined ? { useCount: over.useCount } : {}),
    ...(over?.gameName !== undefined ? { gameName: over.gameName } : {}),
    ...(over?.badge !== undefined ? { badge: over.badge } : {}),
  };
}

describe('HubToolkitCardGrid (Issue #1480)', () => {
  // T1
  it('exposes a data-slot on the root', () => {
    const { container } = render(<HubToolkitCardGrid toolkit={makeToolkit()} labels={labels} />);
    expect(container.querySelector('[data-slot="toolkits-index-card"]')).toBeInTheDocument();
  });

  // T2
  it('renders title, authorName, and installCount', () => {
    render(<HubToolkitCardGrid toolkit={makeToolkit()} labels={labels} />);
    expect(screen.getByText('Azul Tools')).toBeInTheDocument();
    expect(screen.getByText('Marco Rossi')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
  });

  // T3
  it('renders rating row when ratingAverage is non-null', () => {
    const { container } = render(
      <HubToolkitCardGrid toolkit={makeToolkit({ ratingAverage: 4.3 })} labels={labels} />
    );
    expect(container.querySelector('[data-slot="toolkits-index-card-rating"]')).toBeInTheDocument();
    expect(screen.getByText('4.3')).toBeInTheDocument();
  });

  it('hides rating row when ratingAverage is null (P83)', () => {
    const { container } = render(
      <HubToolkitCardGrid
        toolkit={makeToolkit({ ratingAverage: null, ratingCount: 0 })}
        labels={labels}
      />
    );
    expect(
      container.querySelector('[data-slot="toolkits-index-card-rating"]')
    ).not.toBeInTheDocument();
  });

  // T4
  it('renders version + toolCount + useCount when present (P83)', () => {
    render(
      <HubToolkitCardGrid
        toolkit={makeToolkit({ version: 2, toolCount: 5, useCount: 137 })}
        labels={labels}
      />
    );
    expect(screen.getByText('v2.0')).toBeInTheDocument();
    expect(screen.getByText(/Strumenti:/)).toBeInTheDocument();
    expect(screen.getByText(/Uses:/)).toBeInTheDocument();
  });

  it('hides version/toolCount/useCount when undefined (P83 graceful hide)', () => {
    render(<HubToolkitCardGrid toolkit={makeToolkit()} labels={labels} />);
    expect(screen.queryByText(/v\d+\.\d/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Strumenti:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Uses:/)).not.toBeInTheDocument();
  });

  // T5
  it('renders gameName when present', () => {
    render(<HubToolkitCardGrid toolkit={makeToolkit({ gameName: 'Azul' })} labels={labels} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('renders gameRefFallback when gameName is null', () => {
    render(<HubToolkitCardGrid toolkit={makeToolkit({ gameName: null })} labels={labels} />);
    expect(screen.getByText(labels.gameRefFallback)).toBeInTheDocument();
  });

  // T6
  it('renders badge when present', () => {
    render(<HubToolkitCardGrid toolkit={makeToolkit({ badge: 'Featured' })} labels={labels} />);
    expect(screen.getByText('Featured')).toBeInTheDocument();
  });

  // T7
  it('fires onInstall with toolkit id when install button clicked', () => {
    const onInstall = vi.fn();
    render(<HubToolkitCardGrid toolkit={makeToolkit()} labels={labels} onInstall={onInstall} />);
    fireEvent.click(screen.getByRole('button', { name: 'Installa Azul Tools' }));
    expect(onInstall).toHaveBeenCalledWith('tk-1');
  });

  it('fires onClick on Enter key when card has focus (keyboard a11y)', () => {
    const onClick = vi.fn();
    render(<HubToolkitCardGrid toolkit={makeToolkit()} labels={labels} onClick={onClick} />);
    const card = screen.getByRole('article');
    fireEvent.keyDown(card, { key: 'Enter' });
    expect(onClick).toHaveBeenCalledWith('tk-1');
  });

  it('fires onClick on Space key when card has focus (keyboard a11y)', () => {
    const onClick = vi.fn();
    render(<HubToolkitCardGrid toolkit={makeToolkit()} labels={labels} onClick={onClick} />);
    const card = screen.getByRole('article');
    fireEvent.keyDown(card, { key: ' ' });
    expect(onClick).toHaveBeenCalledWith('tk-1');
  });

  // T8
  it('composes className and passes axe a11y scan', async () => {
    const { container } = render(
      <HubToolkitCardGrid
        toolkit={makeToolkit({ ratingAverage: 4.3, gameName: 'Azul', version: 2 })}
        labels={labels}
        className="extra"
      />
    );
    const root = container.querySelector('[data-slot="toolkits-index-card"]');
    expect(root).toHaveClass('extra');
    expect(await axe(container)).toHaveNoViolations();
  });
});
