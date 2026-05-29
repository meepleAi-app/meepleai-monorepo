/**
 * RecordsHero Component Tests (Minimal - Task 1)
 *
 * Basic smoke tests for RecordsHero rendering.
 * Issue #1488: Play Records Index Reskin (Task 1)
 */

import { describe, it, expect, vi } from 'vitest';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { RecordsHero } from '../RecordsHero';

// RecordsHero consumes usePlayerStatistics (TanStack Query) — needs provider wrap.
function renderWithQuery(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe('RecordsHero', () => {
  it('renders without crashing with isLoading=false', () => {
    const { container } = renderWithQuery(<RecordsHero isLoading={false} />);
    expect(container).toBeInTheDocument();
  });

  it('renders without crashing with isLoading=true', () => {
    const { container } = renderWithQuery(<RecordsHero isLoading={true} />);

    // Should display 4 skeleton boxes
    const skeletons = container.querySelectorAll('[class*="animate-pulse"]');
    expect(skeletons.length).toBeGreaterThanOrEqual(4);
  });

  it('contains a link to /play-records/new', () => {
    const { container } = renderWithQuery(<RecordsHero isLoading={false} />);

    const link = container.querySelector('a[href="/play-records/new"]');
    expect(link).toBeInTheDocument();
  });

  it('applies semantic styling', () => {
    const { container } = renderWithQuery(<RecordsHero isLoading={false} />);

    const heroDiv = container.firstChild;
    // Component uses relative + overflow-hidden + border + gradient (not flex-col on root).
    expect(heroDiv).toHaveClass('relative', 'overflow-hidden');
  });
});
