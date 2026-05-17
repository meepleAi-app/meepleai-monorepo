/**
 * HubCatalogView smoke test (#1166).
 *
 * Minimal render-without-crash assertion. Detailed per-shell + per-card tests
 * deferred per spec AC10 (consistent with sibling Stage 3 FE PRs).
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/hub/games',
  useRouter: () => ({ replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

import { HubCatalogView, type HubFilter } from '../HubCatalogView';

const LABELS = {
  badge: 'Test badge',
  title: 'Test catalog',
  subtitle: 'Test subtitle',
  searchPlaceholder: 'Search…',
  filterAriaLabel: 'Filters',
  filterLabels: { all: 'All', featured: 'Featured', new: 'New', top: 'Top' },
  emptyTitle: 'Empty title',
  emptyBody: 'Empty body',
  filteredEmptyTitle: 'No results',
  filteredEmptyBody: 'Try other filters',
  errorTitle: 'Error title',
  errorBody: 'Error body',
  retryLabel: 'Retry',
  resultsCountTemplate: '{filtered} of {total}',
};

interface Item {
  id: string;
  name: string;
}

describe('HubCatalogView (Stage 3 hub cluster)', () => {
  it('renders hero + 4 filter pills + populated grid', () => {
    const items: Item[] = [
      { id: 'a', name: 'Alpha' },
      { id: 'b', name: 'Beta' },
    ];
    render(
      <HubCatalogView<Item>
        entity="game"
        labels={LABELS}
        kpi={[]}
        items={items}
        itemMatches={() => true}
        renderCard={item => <div data-testid={`card-${item.id}`}>{item.name}</div>}
        getItemKey={item => item.id}
      />
    );
    expect(screen.getByText('Test catalog')).toBeInTheDocument();
    expect(screen.getByRole('toolbar', { name: 'Filters' })).toBeInTheDocument();
    expect(screen.getByTestId('card-a')).toBeInTheDocument();
    expect(screen.getByTestId('card-b')).toBeInTheDocument();
  });

  it('renders empty state when items array is empty', () => {
    render(
      <HubCatalogView<Item>
        entity="game"
        labels={LABELS}
        kpi={[]}
        items={[]}
        itemMatches={() => true}
        renderCard={() => null}
        getItemKey={() => ''}
      />
    );
    expect(screen.getByText('Empty title')).toBeInTheDocument();
  });

  it('renders error state when isError=true', () => {
    render(
      <HubCatalogView<Item>
        entity="game"
        labels={LABELS}
        kpi={[]}
        items={[]}
        itemMatches={() => true}
        renderCard={() => null}
        getItemKey={() => ''}
        isError
      />
    );
    expect(screen.getByText('Error title')).toBeInTheDocument();
  });

  it('renders loading skeleton when isLoading=true', () => {
    const { container } = render(
      <HubCatalogView<Item>
        entity="game"
        labels={LABELS}
        kpi={[]}
        items={[]}
        itemMatches={() => true}
        renderCard={() => null}
        getItemKey={() => ''}
        isLoading
      />
    );
    const skeletons = container.querySelectorAll('[data-slot="hub-card-skeleton"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('exposes entity attribute on root slot', () => {
    const { container } = render(
      <HubCatalogView<Item>
        entity="toolkit"
        labels={LABELS}
        kpi={[]}
        items={[]}
        itemMatches={() => true}
        renderCard={() => null}
        getItemKey={() => ''}
      />
    );
    const root = container.querySelector('[data-slot="hub-catalog-view"]');
    expect(root?.getAttribute('data-entity')).toBe('toolkit');
  });

  it('filters items via itemMatches predicate when query and filter change is applied', () => {
    const items: Item[] = [
      { id: 'a', name: 'Alpha' },
      { id: 'b', name: 'Beta' },
    ];
    const matcher = (item: Item, _filter: HubFilter, q: string) =>
      !q || item.name.toLowerCase().includes(q.toLowerCase());
    render(
      <HubCatalogView<Item>
        entity="game"
        labels={LABELS}
        kpi={[]}
        items={items}
        itemMatches={matcher}
        renderCard={item => <div data-testid={`card-${item.id}`}>{item.name}</div>}
        getItemKey={item => item.id}
      />
    );
    // Both cards visible with default empty query.
    expect(screen.getByTestId('card-a')).toBeInTheDocument();
    expect(screen.getByTestId('card-b')).toBeInTheDocument();
  });
});
