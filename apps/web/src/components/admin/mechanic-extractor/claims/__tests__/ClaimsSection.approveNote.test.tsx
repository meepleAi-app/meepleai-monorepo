/** @vitest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetClaims = vi.hoisted(() => vi.fn());
const mockApprove = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getMechanicAnalysisClaims: mockGetClaims,
    approveMechanicClaim: mockApprove,
  }),
}));
const MockHttpClient = vi.hoisted(() => class MockHttpClient {});
vi.mock('@/lib/api/core/httpClient', () => ({ HttpClient: MockHttpClient }));

import { ClaimsSection } from '../ClaimsSection';

const claim = {
  id: 'd1',
  analysisId: 'a',
  section: 1,
  text: 't',
  displayOrder: 0,
  status: 0,
  reviewedBy: null,
  reviewedAt: null,
  rejectionNote: null,
  reviewNote: null,
  validations: [],
  citations: [],
};

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('ClaimsSection approve with note', () => {
  it('sends the optional note on approve', async () => {
    mockGetClaims.mockResolvedValue([claim]);
    mockApprove.mockResolvedValue({ ...claim, status: 1, reviewNote: 'matches p.4' });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    fireEvent.click(await screen.findByTestId('claim-approve-d1'));
    fireEvent.change(screen.getByTestId('approve-claim-note-input'), {
      target: { value: 'matches p.4' },
    });
    fireEvent.click(screen.getByTestId('approve-claim-confirm'));
    await waitFor(() => expect(mockApprove).toHaveBeenCalledWith('a', 'd1', 'matches p.4'));
  });

  it('allows confirming with no note (note is optional)', async () => {
    mockGetClaims.mockResolvedValue([claim]);
    mockApprove.mockResolvedValue({ ...claim, status: 1, reviewNote: null });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    fireEvent.click(await screen.findByTestId('claim-approve-d1'));
    expect(screen.getByTestId('approve-claim-confirm')).not.toBeDisabled();
    fireEvent.click(screen.getByTestId('approve-claim-confirm'));
    await waitFor(() => expect(mockApprove).toHaveBeenCalledWith('a', 'd1', undefined));
  });

  it('renders the reviewNote in a green block after refetch', async () => {
    mockGetClaims.mockResolvedValue([{ ...claim, status: 1, reviewNote: 'matches p.4' }]);
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    expect(await screen.findByTestId('claim-review-note-d1')).toHaveTextContent('matches p.4');
  });
});
