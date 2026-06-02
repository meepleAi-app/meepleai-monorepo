import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { EvaluationTriggerButton } from '../EvaluationTriggerButton';

const startEvaluationMock = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    kbQuality: {
      startEvaluation: (...args: unknown[]) => startEvaluationMock(...args),
    },
  },
}));

const DOC_ID = '22222222-2222-2222-2222-222222222222';

function renderWithClient(ui: ReactElement) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

afterEach(() => {
  startEvaluationMock.mockReset();
});

describe('EvaluationTriggerButton', () => {
  it('triggers the start mutation when clicked', async () => {
    startEvaluationMock.mockResolvedValue({
      evaluationId: '11111111-1111-1111-1111-111111111111',
      locationCreatedAt: '2026-06-02T10:00:00Z',
      rateLimitRemaining: 0,
      rateLimitReset: '2026-06-02T10:10:00Z',
      costCapRemaining: 50,
      costCapEstimate: 0.05,
    });

    renderWithClient(
      <EvaluationTriggerButton docId={DOC_ID} hasOverrideCostCapPermission={false} />
    );

    fireEvent.click(screen.getByTestId('eval-trigger-button'));

    await waitFor(() => {
      expect(startEvaluationMock).toHaveBeenCalledWith(DOC_ID, { overrideCostCap: false });
    });
  });

  it('shows the override toggle when admin permission is granted', () => {
    renderWithClient(
      <EvaluationTriggerButton docId={DOC_ID} hasOverrideCostCapPermission={true} />
    );
    expect(screen.getByTestId('eval-override-toggle')).toBeInTheDocument();
  });

  it('hides the override toggle without permission', () => {
    renderWithClient(
      <EvaluationTriggerButton docId={DOC_ID} hasOverrideCostCapPermission={false} />
    );
    expect(screen.queryByTestId('eval-override-toggle')).not.toBeInTheDocument();
  });

  it('passes overrideCostCap=true when the toggle is enabled', async () => {
    startEvaluationMock.mockResolvedValue({
      evaluationId: '11111111-1111-1111-1111-111111111111',
      locationCreatedAt: '2026-06-02T10:00:00Z',
      rateLimitRemaining: 0,
      rateLimitReset: '2026-06-02T10:10:00Z',
      costCapRemaining: 50,
      costCapEstimate: 0.05,
    });

    renderWithClient(
      <EvaluationTriggerButton docId={DOC_ID} hasOverrideCostCapPermission={true} />
    );

    fireEvent.click(screen.getByTestId('eval-override-toggle'));
    fireEvent.click(screen.getByTestId('eval-trigger-button'));

    await waitFor(() => {
      expect(startEvaluationMock).toHaveBeenCalledWith(DOC_ID, { overrideCostCap: true });
    });
  });

  it('surfaces the mutation error message inline', async () => {
    startEvaluationMock.mockRejectedValue(new Error('Cost cap exceeded'));

    renderWithClient(
      <EvaluationTriggerButton docId={DOC_ID} hasOverrideCostCapPermission={false} />
    );
    fireEvent.click(screen.getByTestId('eval-trigger-button'));

    expect(await screen.findByTestId('eval-error')).toHaveTextContent('Cost cap exceeded');
  });
});
