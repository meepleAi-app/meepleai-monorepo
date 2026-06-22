import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../lib/catalog-seed-api';
import { SeedQueueStatusHero } from './SeedQueueStatusHero';

vi.mock('../lib/catalog-seed-api');

function setup() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('SeedQueueStatusHero', () => {
  it('renders KPI cards for the four statuses', async () => {
    vi.mocked(api.listSeeds).mockResolvedValue({
      total: 5,
      skip: 0,
      take: 1,
      items: [],
    });
    render(<SeedQueueStatusHero />, { wrapper: setup() });

    await waitFor(() => expect(screen.getByText(/Catalog seed queue/i)).toBeInTheDocument());
    expect(screen.getByTestId('seed-status-card-pending')).toBeInTheDocument();
    expect(screen.getByTestId('seed-status-card-fetched')).toBeInTheDocument();
    expect(screen.getByTestId('seed-status-card-approved')).toBeInTheDocument();
    expect(screen.getByTestId('seed-status-card-rejected')).toBeInTheDocument();
  });

  it('renders the error card when list fails', async () => {
    vi.mocked(api.listSeeds).mockRejectedValue(new Error('Network down'));
    render(<SeedQueueStatusHero />, { wrapper: setup() });

    await waitFor(() =>
      expect(screen.getByText(/Catalog seed queue unavailable/i)).toBeInTheDocument()
    );
    expect(screen.getByText(/Network down/i)).toBeInTheDocument();
  });
});
