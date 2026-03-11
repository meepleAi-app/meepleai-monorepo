/**
 * DiscoverPage routing tests
 * Verifies that ?tab=bgg renders BggSearchTab (and other tab branches are correct).
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

// Mock tab components so the async server component is trivially renderable
vi.mock('../BggSearchTab', () => ({
  BggSearchTab: () => <div data-testid="bgg-search-tab" />,
}));
vi.mock('@/app/(authenticated)/library/proposals/MyProposalsClient', () => ({
  default: () => <div data-testid="my-proposals-client" />,
}));
vi.mock('@/app/(public)/games/catalog/_content', () => ({
  CatalogContent: () => <div data-testid="catalog-content" />,
}));
vi.mock('@/components/catalog/MeepleGameCatalogCard', () => ({
  MeepleGameCatalogCardSkeleton: () => <div data-testid="catalog-skeleton" />,
}));

import DiscoverPage from '../page';

describe('DiscoverPage tab routing', () => {
  it('renders BggSearchTab when tab=bgg', async () => {
    const page = await DiscoverPage({ searchParams: Promise.resolve({ tab: 'bgg' }) });
    render(page);
    expect(screen.getByTestId('bgg-search-tab')).toBeInTheDocument();
  });

  it('renders catalog by default (no tab param)', async () => {
    const page = await DiscoverPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByTestId('catalog-content')).toBeInTheDocument();
  });

  it('renders proposals tab when tab=proposals', async () => {
    const page = await DiscoverPage({ searchParams: Promise.resolve({ tab: 'proposals' }) });
    render(page);
    expect(screen.getByTestId('my-proposals-client')).toBeInTheDocument();
  });

  it('renders community placeholder when tab=community', async () => {
    const page = await DiscoverPage({ searchParams: Promise.resolve({ tab: 'community' }) });
    render(page);
    expect(screen.getByText('Community')).toBeInTheDocument();
  });
});
