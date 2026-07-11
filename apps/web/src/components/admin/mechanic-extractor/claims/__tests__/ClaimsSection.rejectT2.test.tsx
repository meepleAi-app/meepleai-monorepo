/** @vitest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, within, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetClaims = vi.hoisted(() => vi.fn());
const mockBulkReject = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getMechanicAnalysisClaims: mockGetClaims,
    bulkApproveMechanicClaims: vi.fn(),
    bulkRejectMechanicClaims: mockBulkReject,
  }),
}));
const MockHttpClient = vi.hoisted(() => class MockHttpClient {});
vi.mock('@/lib/api/core/httpClient', () => ({ HttpClient: MockHttpClient }));

import { ClaimsSection } from '../ClaimsSection';

const base = {
  analysisId: 'a',
  section: 1,
  text: 't',
  displayOrder: 0,
  status: 0,
  reviewedBy: null,
  reviewedAt: null,
  rejectionNote: null,
  reviewNote: null,
  citations: [{ id: 'c', pdfPage: 1, quote: 'q', displayOrder: 0 }],
};
const claims = [
  {
    ...base,
    id: 'd1',
    validations: [{ rule: 'T2', outcome: 'fail', message: 'verbatim', score: null }],
  },
  { ...base, id: 'd2', validations: [{ rule: 'T2', outcome: 'pass', message: null, score: null }] },
];

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('ClaimsSection reject-all-failing-T2', () => {
  it('rejects only claims carrying a T2 fail validation', async () => {
    mockGetClaims.mockResolvedValue(claims);
    mockBulkReject.mockResolvedValue({ rejectedCount: 1, skippedAlreadyRejectedCount: 0, claims });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });

    // Radix Select: click the trigger, wait for the listbox, click the option by its visible text.
    const select = await screen.findByTestId('bulk-action-select');
    fireEvent.click(select);
    const listbox = await screen.findByRole('listbox');
    fireEvent.click(within(listbox).getByText(/Reject all failing T2/));

    fireEvent.click(screen.getByTestId('bulk-action-confirm'));

    await waitFor(() =>
      expect(mockBulkReject).toHaveBeenCalledWith(
        'a',
        expect.objectContaining({ claimIds: ['d1'] })
      )
    );
  });
});
