import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../lib/catalog-seed-api';
import { WikidataSearchForm } from './WikidataSearchForm';

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

const PROVENANCE_TITLE = {
  provider: 'wikidata',
  sourceUrl: 'https://www.wikidata.org/wiki/Q170416',
  sourceField: 'P1476',
  fetchedAt: '2026-06-04T12:00:00Z',
  value: 'Catan',
};

const PROVENANCE_DESIGNER = {
  provider: 'wikidata',
  sourceUrl: 'https://www.wikidata.org/wiki/Q170416',
  sourceField: 'P178',
  fetchedAt: '2026-06-04T12:00:00Z',
  value: 'Klaus Teuber',
};

describe('WikidataSearchForm', () => {
  it('disables the search button when input is too short', () => {
    render(<WikidataSearchForm />, { wrapper: wrapper() });
    expect(screen.getByRole('button', { name: /Search/i })).toBeDisabled();
  });

  it('invokes wikidataSearch on submit', async () => {
    vi.mocked(api.wikidataSearch).mockResolvedValue({
      fields: { title: PROVENANCE_TITLE },
      errorMessage: null,
    });
    render(<WikidataSearchForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByLabelText(/Wikidata search term/i), 'catan');
    await userEvent.click(screen.getByRole('button', { name: /Search/i }));
    await waitFor(() => expect(api.wikidataSearch).toHaveBeenCalledWith('catan'));
  });

  it('renders the result fields with provenance', async () => {
    vi.mocked(api.wikidataSearch).mockResolvedValue({
      fields: { title: PROVENANCE_TITLE, designer: PROVENANCE_DESIGNER },
      errorMessage: null,
    });
    render(<WikidataSearchForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByLabelText(/Wikidata search term/i), 'catan');
    await userEvent.click(screen.getByRole('button', { name: /Search/i }));
    await waitFor(() => expect(screen.getByTestId('wikidata-search-result')).toBeInTheDocument());
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
    expect(screen.getByText('Q170416')).toBeInTheDocument();
  });

  it('shows error when search reports a provider error', async () => {
    vi.mocked(api.wikidataSearch).mockResolvedValue({
      fields: {},
      errorMessage: 'SPARQL endpoint timeout',
    });
    render(<WikidataSearchForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByLabelText(/Wikidata search term/i), 'foo');
    await userEvent.click(screen.getByRole('button', { name: /Search/i }));
    expect(await screen.findByText(/SPARQL endpoint timeout/i)).toBeInTheDocument();
  });

  it('add-to-queue forwards the QID to enqueueSeed', async () => {
    vi.mocked(api.wikidataSearch).mockResolvedValue({
      fields: { title: PROVENANCE_TITLE },
      errorMessage: null,
    });
    vi.mocked(api.enqueueSeed).mockResolvedValue({
      id: 'd1',
      bggId: null,
      status: 'Pending',
      isDuplicate: false,
    });
    render(<WikidataSearchForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByLabelText(/Wikidata search term/i), 'catan');
    await userEvent.click(screen.getByRole('button', { name: /Search/i }));
    await waitFor(() => expect(screen.getByText('Q170416')).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: /Add Q170416 to queue/i }));
    await waitFor(() => expect(api.enqueueSeed).toHaveBeenCalledWith({ wikidataQid: 'Q170416' }));
  });
});
