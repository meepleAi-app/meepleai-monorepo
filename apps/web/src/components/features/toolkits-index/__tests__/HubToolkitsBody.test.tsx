/**
 * HubToolkitsBody - Unit tests (Issue #1480).
 *
 * Pure presentational orchestrator wrapper for `/toolkits` hub.
 * Maps from sp4-hub-toolkits.jsx:500-567 (function HubToolkitsBody).
 * Composes Hero + Filters + (Grid | SkeletonGrid | EmptyFiltered | ErrorState)
 * based on the `state` prop. Filter state is controlled (parent owns).
 *
 * Test matrix (Crispin):
 *   T1. data-slot on root.
 *   T2. Hero always rendered.
 *   T3. state="error" → ErrorState; no Filters; no grid; no toolkits cards.
 *   T4. state="loading" → SkeletonCard grid; no toolkits cards; no empty state.
 *   T5. state="default" + toolkits empty → HubEmptyFiltered.
 *   T6. state="default" + toolkits non-empty → HubToolkitCardGrid per item.
 *   T7. HubEmptyFiltered onReset → onQueryChange('') + onStatusChange('all').
 *   T8. ErrorState onRetry → onRetry callback.
 *   T9. className + axe scan on default state.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { HubToolkitsBody } from '../HubToolkitsBody';
import type { HubToolkitCardItem } from '../HubToolkitCardGrid';

const heroLabels = {
  eyebrow: 'Hub · /toolkits',
  title: 'Catalogo toolkit community',
  subtitle: 'Bundle pronti.',
};

const filterLabels = {
  searchPlaceholder: 'Cerca…',
  searchClearAriaLabel: 'Pulisci',
  statusTablistAriaLabel: 'Filtra',
  statusOptions: { all: 'Tutti', featured: 'Featured', new: 'Nuovi', top: 'Top 100' },
  sortLabel: 'Ordina',
  sortOptions: { popular: 'Popolarità', rating: 'Rating', title: 'Titolo', uses: 'Uses' },
  countTemplate: '{count} toolkit',
};

const cardLabels = {
  gameRefFallback: 'Universale',
  installCta: '+ Installa',
  installAriaLabel: 'Installa {title}',
  toolsLabel: 'Strumenti:',
  usesLabel: 'Uses:',
};

const emptyLabels = {
  title: 'Nessun toolkit',
  body: 'Nessun risultato.',
  reset: 'Azzera',
  resetAriaLabel: 'Azzera filtri',
};

const errorLabels = {
  title: 'Errore catalogo',
  body: 'Verifica connessione.',
  retry: '↻ Riprova',
  retryAriaLabel: 'Riprova',
};

const heroStats = [
  { label: 'Toolkit', value: 24 },
  { label: 'Installazioni', value: '12k' },
];

const sampleToolkits: readonly HubToolkitCardItem[] = [
  {
    id: 'tk-1',
    title: 'Azul Tools',
    authorName: 'Marco',
    installCount: 42,
    ratingAverage: 4.3,
    ratingCount: 18,
    coverImageUrl: null,
  },
  {
    id: 'tk-2',
    title: 'Wingspan Helper',
    authorName: 'Lucia',
    installCount: 99,
    ratingAverage: 4.7,
    ratingCount: 25,
    coverImageUrl: null,
  },
];

function renderBody(overrides?: {
  state?: 'default' | 'loading' | 'error';
  toolkits?: readonly HubToolkitCardItem[];
  onQueryChange?: (q: string) => void;
  onStatusChange?: (s: 'all' | 'featured' | 'new' | 'top') => void;
  onRetry?: () => void;
}) {
  return render(
    <HubToolkitsBody
      state={overrides?.state ?? 'default'}
      toolkits={overrides?.toolkits ?? sampleToolkits}
      heroStats={heroStats}
      query=""
      onQueryChange={overrides?.onQueryChange ?? (() => {})}
      status="all"
      onStatusChange={overrides?.onStatusChange ?? (() => {})}
      sort="popular"
      onSortChange={() => {}}
      onRetry={overrides?.onRetry ?? (() => {})}
      heroLabels={heroLabels}
      filterLabels={filterLabels}
      cardLabels={cardLabels}
      emptyLabels={emptyLabels}
      errorLabels={errorLabels}
    />
  );
}

describe('HubToolkitsBody (Issue #1480)', () => {
  // T1
  it('exposes a data-slot on the root', () => {
    const { container } = renderBody();
    expect(container.querySelector('[data-slot="toolkits-index-body"]')).toBeInTheDocument();
  });

  // T2
  it('always renders the Hero', () => {
    renderBody({ state: 'error' });
    expect(screen.getByRole('heading', { level: 1, name: heroLabels.title })).toBeInTheDocument();
  });

  // T3
  it('state=error → ErrorState; no Filters; no toolkit cards', () => {
    const { container } = renderBody({ state: 'error' });
    expect(container.querySelector('[data-slot="toolkits-index-error-state"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="toolkits-index-filters"]')).not.toBeInTheDocument();
    expect(container.querySelectorAll('[data-slot="toolkits-index-card"]')).toHaveLength(0);
  });

  // T4
  it('state=loading → SkeletonCard grid; no toolkit cards; no empty state', () => {
    const { container } = renderBody({ state: 'loading' });
    const skeletons = container.querySelectorAll('[data-slot="toolkits-index-skeleton-card"]');
    expect(skeletons.length).toBeGreaterThan(0);
    expect(container.querySelectorAll('[data-slot="toolkits-index-card"]')).toHaveLength(0);
    expect(
      container.querySelector('[data-slot="toolkits-index-empty-filtered"]')
    ).not.toBeInTheDocument();
  });

  // T5
  it('state=default + toolkits empty → HubEmptyFiltered', () => {
    const { container } = renderBody({ state: 'default', toolkits: [] });
    expect(
      container.querySelector('[data-slot="toolkits-index-empty-filtered"]')
    ).toBeInTheDocument();
    expect(container.querySelectorAll('[data-slot="toolkits-index-card"]')).toHaveLength(0);
  });

  // T6
  it('state=default + toolkits non-empty → renders one card per item', () => {
    const { container } = renderBody({ state: 'default', toolkits: sampleToolkits });
    expect(container.querySelectorAll('[data-slot="toolkits-index-card"]')).toHaveLength(2);
    expect(screen.getByText('Azul Tools')).toBeInTheDocument();
    expect(screen.getByText('Wingspan Helper')).toBeInTheDocument();
  });

  // T7
  it('HubEmptyFiltered onReset triggers full reset (query + status + sort)', () => {
    const onQueryChange = vi.fn();
    const onStatusChange = vi.fn();
    const onSortChange = vi.fn();
    render(
      <HubToolkitsBody
        state="default"
        toolkits={[]}
        heroStats={heroStats}
        query=""
        onQueryChange={onQueryChange}
        status="all"
        onStatusChange={onStatusChange}
        sort="popular"
        onSortChange={onSortChange}
        onRetry={() => {}}
        heroLabels={heroLabels}
        filterLabels={filterLabels}
        cardLabels={cardLabels}
        emptyLabels={emptyLabels}
        errorLabels={errorLabels}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: emptyLabels.resetAriaLabel }));
    expect(onQueryChange).toHaveBeenCalledWith('');
    expect(onStatusChange).toHaveBeenCalledWith('all');
    expect(onSortChange).toHaveBeenCalledWith('popular');
  });

  // T8
  it('ErrorState onRetry triggers onRetry callback', () => {
    const onRetry = vi.fn();
    renderBody({ state: 'error', onRetry });
    fireEvent.click(screen.getByRole('button', { name: errorLabels.retryAriaLabel }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  // T9
  it('passes axe a11y scan on default state', async () => {
    const { container } = renderBody({ state: 'default', toolkits: sampleToolkits });
    expect(await axe(container)).toHaveNoViolations();
  });
});
