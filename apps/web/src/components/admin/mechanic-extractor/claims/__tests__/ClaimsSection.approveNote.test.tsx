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
  kind: 'Rule',
  priority: 'Base',
  overrides: [],
  trigger: null,
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
    expect(screen.getByTestId('approve-claim-note-input')).toHaveAttribute('maxLength', '2000');
    fireEvent.change(screen.getByTestId('approve-claim-note-input'), {
      target: { value: 'matches p.4' },
    });
    fireEvent.click(screen.getByTestId('approve-claim-confirm'));
    await waitFor(() =>
      expect(mockApprove).toHaveBeenCalledWith('a', 'd1', 'matches p.4', {
        kind: 'Rule',
        priority: 'Base',
        overrides: [],
        trigger: null,
      })
    );
  });

  it('allows confirming with no note (note is optional)', async () => {
    mockGetClaims.mockResolvedValue([claim]);
    mockApprove.mockResolvedValue({ ...claim, status: 1, reviewNote: null });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    fireEvent.click(await screen.findByTestId('claim-approve-d1'));
    expect(screen.getByTestId('approve-claim-confirm')).not.toBeDisabled();
    fireEvent.click(screen.getByTestId('approve-claim-confirm'));
    await waitFor(() =>
      expect(mockApprove).toHaveBeenCalledWith('a', 'd1', undefined, {
        kind: 'Rule',
        priority: 'Base',
        overrides: [],
        trigger: null,
      })
    );
  });

  it('renders the reviewNote in a green block after refetch', async () => {
    mockGetClaims.mockResolvedValue([{ ...claim, status: 1, reviewNote: 'matches p.4' }]);
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    expect(await screen.findByTestId('claim-review-note-d1')).toHaveTextContent('matches p.4');
  });

  it('sends an edited kind on approve', async () => {
    mockGetClaims.mockResolvedValue([claim]);
    mockApprove.mockResolvedValue({ ...claim, status: 1 });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    fireEvent.click(await screen.findByTestId('claim-approve-d1'));
    fireEvent.change(screen.getByLabelText('Kind'), { target: { value: 'Exception' } });
    fireEvent.click(screen.getByTestId('approve-claim-confirm'));
    await waitFor(() =>
      expect(mockApprove).toHaveBeenCalledWith(
        'a',
        'd1',
        undefined,
        expect.objectContaining({ kind: 'Exception' })
      )
    );
  });

  it('shows the second claim own kind after cancelling an edit on the first', async () => {
    const claimB = { ...claim, id: 'd2', text: 'u', displayOrder: 1, kind: 'Clarification' };
    mockGetClaims.mockResolvedValue([claim, claimB]);
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    fireEvent.click(await screen.findByTestId('claim-approve-d1'));
    fireEvent.change(screen.getByLabelText('Kind'), { target: { value: 'Example' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByLabelText('Kind')).not.toBeInTheDocument());
    fireEvent.click(screen.getByTestId('claim-approve-d2'));
    expect(screen.getByLabelText('Kind')).toHaveValue('Clarification');
  });
});
