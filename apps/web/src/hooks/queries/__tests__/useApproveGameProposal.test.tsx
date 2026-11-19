/**
 * #5076: the admin approves a proposal through approve-game-proposal. A failed request shows the message for its
 * status: the hooks read `error.status`, which ApiError does not have (it carries `statusCode`), so the 409 and 403
 * messages never showed.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { ApiError } from '@/lib/api/core/errors';

const approveGameProposal = vi.hoisted(() => vi.fn());
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('@/lib/api', () => ({ api: { adminShareRequests: { approveGameProposal } } }));
vi.mock('sonner', () => ({ toast }));

import { adminShareRequestsKeys, useApproveGameProposal } from '../useAdminShareRequests';

function setup() {
  const queryClient = createTestQueryClient();
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const { result } = renderHook(() => useApproveGameProposal(), { wrapper });
  return { result, invalidate };
}

describe('useApproveGameProposal (#5076)', () => {
  it('posts the approval and refreshes the list and the request', async () => {
    approveGameProposal.mockResolvedValue({
      shareRequestId: 'req-1',
      status: 'Approved',
      targetSharedGameId: 'game-1',
      resolvedAt: '2026-11-19T11:00:00Z',
    });
    const { result, invalidate } = setup();

    await act(async () => {
      await result.current.mutateAsync({
        shareRequestId: 'req-1',
        approvalAction: 'ApproveAsNew',
        targetSharedGameId: null,
      });
    });

    expect(approveGameProposal).toHaveBeenCalledWith('req-1', {
      approvalAction: 'ApproveAsNew',
      targetSharedGameId: null,
    });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: adminShareRequestsKeys.lists() });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: adminShareRequestsKeys.detail('req-1') });
    expect(toast.success).toHaveBeenCalledWith('Proposta approvata.');
  });

  it.each([
    [409, 'Non approvabile: la revisione non è tua, è scaduta, o lo stato è cambiato.'],
    [404, 'Il gioco proposto o il gioco di destinazione non esiste più.'],
    // The validators refuse an admin who no longer holds the review: "try again" cannot fix it.
    [422, 'La revisione non è più tua: la pagina mostra lo stato aggiornato.'],
    [400, 'La revisione non è più tua: la pagina mostra lo stato aggiornato.'],
  ])('shows the message for a %i, and refreshes the request', async (statusCode, message) => {
    approveGameProposal.mockRejectedValue(new ApiError({ message: 'refused', statusCode }));
    const { result, invalidate } = setup();

    await act(async () => {
      await result.current
        .mutateAsync({ shareRequestId: 'req-1', approvalAction: 'ApproveAsNew' })
        .catch(() => undefined);
    });

    expect(toast.error).toHaveBeenCalledWith(message);
    // The panel then shows the request's real state instead of the refused form.
    expect(invalidate).toHaveBeenCalledWith({ queryKey: adminShareRequestsKeys.detail('req-1') });
  });
});
