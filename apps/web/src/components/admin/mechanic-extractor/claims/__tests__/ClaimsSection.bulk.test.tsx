/** @vitest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, within, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetClaims = vi.hoisted(() => vi.fn());
const mockBulkApprove = vi.hoisted(() => vi.fn());
const mockBulkReject = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getMechanicAnalysisClaims: mockGetClaims,
    bulkApproveMechanicClaims: mockBulkApprove,
    bulkRejectMechanicClaims: mockBulkReject,
  }),
}));
const MockHttpClient = vi.hoisted(() => class MockHttpClient {});
vi.mock('@/lib/api/core/httpClient', () => ({ HttpClient: MockHttpClient }));

import { ClaimsSection } from '../ClaimsSection';

const longQuote = Array.from({ length: 25 }, (_, i) => `w${i}`).join(' ');
const claims = [
  {
    id: 'd1',
    analysisId: 'a',
    section: 1,
    text: 't1',
    displayOrder: 0,
    status: 0,
    reviewedBy: null,
    reviewedAt: null,
    rejectionNote: null,
    reviewNote: null,
    validations: [],
    citations: [{ id: 'c1', pdfPage: 1, quote: longQuote, displayOrder: 0 }],
  },
  {
    id: 'd2',
    analysisId: 'a',
    section: 1,
    text: 't2',
    displayOrder: 1,
    status: 0,
    reviewedBy: null,
    reviewedAt: null,
    rejectionNote: null,
    reviewNote: null,
    validations: [],
    citations: [{ id: 'c2', pdfPage: 1, quote: 'short quote', displayOrder: 0 }],
  },
];

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('ClaimsSection bulk reject by quote length', () => {
  it('rejects only the >20-word-quote claim after count-confirm', async () => {
    mockGetClaims.mockResolvedValue(claims);
    mockBulkReject.mockResolvedValue({ rejectedCount: 1, skippedAlreadyRejectedCount: 0, claims });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });

    const select = await screen.findByTestId('bulk-action-select');
    fireEvent.click(select);
    const listbox = await screen.findByRole('listbox');
    fireEvent.click(within(listbox).getByText(/Reject all with quote >20 words/));

    expect(screen.getByTestId('bulk-action-count')).toHaveTextContent('1');
    fireEvent.click(screen.getByTestId('bulk-action-confirm'));

    await waitFor(() =>
      expect(mockBulkReject).toHaveBeenCalledWith(
        'a',
        expect.objectContaining({ claimIds: ['d1'] })
      )
    );
  });

  it('shows amber warning when bulk-reject skips already-rejected claims', async () => {
    mockGetClaims.mockResolvedValue(claims);
    mockBulkReject.mockResolvedValue({
      rejectedCount: 1,
      skippedAlreadyRejectedCount: 1,
      claims,
    });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });

    const select = await screen.findByTestId('bulk-action-select');
    fireEvent.click(select);
    const listbox = await screen.findByRole('listbox');
    fireEvent.click(within(listbox).getByText(/Reject all with quote >20 words/));
    fireEvent.click(screen.getByTestId('bulk-action-confirm'));

    expect(await screen.findByTestId('claims-action-warning')).toHaveTextContent(/1/);
  });

  it('approve-pending option calls the existing bulk-approve mutation', async () => {
    mockGetClaims.mockResolvedValue(claims);
    mockBulkApprove.mockResolvedValue({ approvedCount: 2, skippedRejectedCount: 0, claims });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });

    const select = await screen.findByTestId('bulk-action-select');
    fireEvent.click(select);
    const listbox = await screen.findByRole('listbox');
    fireEvent.click(within(listbox).getByText(/Approve all pending/));

    expect(screen.getByTestId('bulk-action-count')).toHaveTextContent('2');
    fireEvent.click(screen.getByTestId('bulk-action-confirm'));

    await waitFor(() => expect(mockBulkApprove).toHaveBeenCalledWith('a'));
  });
});
