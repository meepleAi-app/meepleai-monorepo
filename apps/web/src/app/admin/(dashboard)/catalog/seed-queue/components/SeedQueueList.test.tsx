import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../lib/catalog-seed-api';
import type { CatalogSeedDraftDto } from '../lib/catalog-seed-types';
import { SeedQueueList } from './SeedQueueList';

vi.mock('../lib/catalog-seed-api');
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }));

function wrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

const ROW: CatalogSeedDraftDto = {
  id: 'd1',
  bggId: 13,
  wikidataQid: null,
  searchTermInput: null,
  status: 'Fetched',
  errorMessage: null,
  resultingSharedGameId: null,
  createdByUserId: 'u1',
  createdAt: '2026-06-04T12:00:00Z',
  fetchedAt: '2026-06-04T12:01:00Z',
  approvedAt: null,
  approvedByUserId: null,
};

describe('SeedQueueList', () => {
  it('renders the loading state on first mount', () => {
    vi.mocked(api.listSeeds).mockImplementation(
      () => new Promise(() => undefined) // never resolves → stays in loading
    );
    render(<SeedQueueList />, { wrapper: wrapper() });
    expect(screen.getByText(/Loading queue/i)).toBeInTheDocument();
  });

  it('renders the empty state when no items are returned', async () => {
    vi.mocked(api.listSeeds).mockResolvedValue({ total: 0, skip: 0, take: 50, items: [] });
    render(<SeedQueueList />, { wrapper: wrapper() });
    await waitFor(() =>
      expect(screen.getByText(/No seed drafts match this filter/i)).toBeInTheDocument()
    );
  });

  it('renders queue rows + total count', async () => {
    vi.mocked(api.listSeeds).mockResolvedValue({ total: 1, skip: 0, take: 50, items: [ROW] });
    render(<SeedQueueList />, { wrapper: wrapper() });
    await waitFor(() => expect(screen.getByTestId('seed-row-d1')).toBeInTheDocument());
    expect(screen.getByText(/1 total/)).toBeInTheDocument();
    expect(screen.getByText(/BGG:13/)).toBeInTheDocument();
  });

  it('exposes Approve action for Fetched rows', async () => {
    vi.mocked(api.listSeeds).mockResolvedValue({ total: 1, skip: 0, take: 50, items: [ROW] });
    vi.mocked(api.approveSeed).mockResolvedValue({ draftId: 'd1', resultingSharedGameId: 'sg-1' });
    render(<SeedQueueList />, { wrapper: wrapper() });
    await waitFor(() => expect(screen.getByLabelText(/Approve d1/i)).toBeInTheDocument());
    await userEvent.click(screen.getByLabelText(/Approve d1/i));
    await waitFor(() => expect(api.approveSeed).toHaveBeenCalledWith('d1'));
  });

  it('filters the list when a status chip is clicked', async () => {
    const spy = vi.mocked(api.listSeeds).mockResolvedValue({
      total: 0,
      skip: 0,
      take: 50,
      items: [],
    });
    render(<SeedQueueList />, { wrapper: wrapper() });
    await waitFor(() => expect(spy).toHaveBeenCalled());
    await userEvent.click(screen.getByRole('button', { name: 'Pending' }));
    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(expect.objectContaining({ status: 'Pending' }))
    );
  });
});
