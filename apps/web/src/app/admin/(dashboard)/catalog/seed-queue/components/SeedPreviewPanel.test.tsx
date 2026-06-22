import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../lib/catalog-seed-api';
import type { CatalogSeedDraftDto } from '../lib/catalog-seed-types';
import { SeedPreviewPanel } from './SeedPreviewPanel';

vi.mock('../lib/catalog-seed-api');

function wrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

const SAMPLE: CatalogSeedDraftDto = {
  id: 'd1',
  bggId: 13,
  wikidataQid: 'Q170416',
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

describe('SeedPreviewPanel', () => {
  it('renders the status pill + metadata grid', () => {
    vi.mocked(api.getSeedById).mockResolvedValue(SAMPLE);
    render(<SeedPreviewPanel draft={SAMPLE} />, { wrapper: wrapper() });
    // The word "Fetched" appears both as the status pill value AND as a metadata
    // grid label ("Fetched: <timestamp>"). At least one element must exist.
    expect(screen.getAllByText('Fetched').length).toBeGreaterThan(0);
    expect(screen.getByText('13')).toBeInTheDocument();
    expect(screen.getByText('Q170416')).toBeInTheDocument();
  });

  it('renders the error card on FetchFailed', () => {
    vi.mocked(api.getSeedById).mockResolvedValue(SAMPLE);
    render(
      <SeedPreviewPanel
        draft={{ ...SAMPLE, status: 'FetchFailed', errorMessage: 'BGG returned 404' }}
      />,
      { wrapper: wrapper() }
    );
    expect(screen.getByText(/Fetch error/i)).toBeInTheDocument();
    expect(screen.getByText('BGG returned 404')).toBeInTheDocument();
  });

  it('toggles the raw JSON viewer', async () => {
    vi.mocked(api.getSeedById).mockResolvedValue(SAMPLE);
    render(<SeedPreviewPanel draft={SAMPLE} />, { wrapper: wrapper() });
    await userEvent.click(screen.getByRole('button', { name: /View raw DTO/i }));
    expect(screen.getByRole('button', { name: /Hide raw JSON/i })).toBeInTheDocument();
    expect(screen.getByText(/"id": "d1"/)).toBeInTheDocument();
  });
});
