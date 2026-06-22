import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../lib/catalog-seed-api';
import CatalogSeedQueuePage from '../page';

vi.mock('../lib/catalog-seed-api');
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }));
vi.mock('../hooks/use-catalog-seed-stream', () => ({
  useCatalogSeedStream: () => ({ events: [], isConnected: true, clear: vi.fn() }),
}));

function wrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('CatalogSeedQueuePage', () => {
  it('renders the header, KPI hero, queue list, and live stream sections', async () => {
    vi.mocked(api.listSeeds).mockResolvedValue({ total: 0, skip: 0, take: 1, items: [] });
    render(<CatalogSeedQueuePage />, { wrapper: wrapper() });

    await waitFor(() =>
      // H1 (page header) + H2 (hero) both contain "Catalog seed queue" — assert at least one
      expect(
        screen.getAllByRole('heading', { name: /Catalog seed queue/i }).length
      ).toBeGreaterThan(0)
    );
    expect(screen.getByText(/Admin · Catalog · Seed pipeline/i)).toBeInTheDocument();
    // KPI cards
    expect(screen.getByTestId('seed-status-card-pending')).toBeInTheDocument();
    expect(screen.getByTestId('seed-status-card-fetched')).toBeInTheDocument();
    expect(screen.getByTestId('seed-status-card-approved')).toBeInTheDocument();
    expect(screen.getByTestId('seed-status-card-rejected')).toBeInTheDocument();
    // Bulk paste + single add + search forms — assert via region role
    expect(screen.getByRole('region', { name: /Bulk paste BGG IDs/i })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /Single seed add/i })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /Wikidata search/i })).toBeInTheDocument();
    // Queue + log stream — assert via region role
    expect(screen.getByRole('region', { name: /Catalog seed queue list/i })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /Catalog seed live stream/i })).toBeInTheDocument();
  });
});
