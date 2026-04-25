/**
 * @vitest-environment jsdom
 *
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 15)
 *
 * Tests for `ThresholdsConfigForm`. Surface contract:
 *  - Save button is disabled when no field has changed (form is pristine).
 *  - Inputs reject out-of-range values via Zod (matches the backend validator
 *    bounds tested in `UpdateCertificationThresholdsValidatorTests` —
 *    0..100 for percentages, >= 0 integer for `maxPageTolerance`).
 *  - On submit the hook is called with the current form state; on success a
 *    toast is shown (delegated to the hook, asserted via the mocked hook
 *    being invoked with the expected variables).
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { CertificationThresholdsDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

const mockMutate = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/admin/useUpdateThresholds', () => ({
  useUpdateThresholds: () => ({
    mutate: mockMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
  }),
}));

import { ThresholdsConfigForm } from '../ThresholdsConfigForm';

const INITIAL: CertificationThresholdsDto = {
  minCoveragePct: 70,
  maxPageTolerance: 10,
  minBggMatchPct: 80,
  minOverallScore: 60,
};

function renderWithClient(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('ThresholdsConfigForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders all four numeric fields prefilled from the initial DTO', () => {
    renderWithClient(<ThresholdsConfigForm initial={INITIAL} />);

    expect(screen.getByLabelText(/minimum coverage/i)).toHaveValue(INITIAL.minCoveragePct);
    expect(screen.getByLabelText(/maximum page tolerance/i)).toHaveValue(INITIAL.maxPageTolerance);
    expect(screen.getByLabelText(/minimum bgg match/i)).toHaveValue(INITIAL.minBggMatchPct);
    expect(screen.getByLabelText(/minimum overall score/i)).toHaveValue(INITIAL.minOverallScore);
  });

  it('disables save when no field changed', () => {
    renderWithClient(<ThresholdsConfigForm initial={INITIAL} />);

    expect(screen.getByRole('button', { name: /save thresholds/i })).toBeDisabled();
  });

  it('blocks save when minCoveragePct > 100', async () => {
    const user = userEvent.setup();
    renderWithClient(<ThresholdsConfigForm initial={INITIAL} />);

    const input = screen.getByLabelText(/minimum coverage/i);
    await user.clear(input);
    await user.type(input, '150');

    await user.click(screen.getByRole('button', { name: /save thresholds/i }));

    expect(await screen.findByText(/must be between 0 and 100/i)).toBeInTheDocument();
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('blocks save when maxPageTolerance is negative', async () => {
    const user = userEvent.setup();
    renderWithClient(<ThresholdsConfigForm initial={INITIAL} />);

    const input = screen.getByLabelText(/maximum page tolerance/i);
    await user.clear(input);
    await user.type(input, '-1');

    await user.click(screen.getByRole('button', { name: /save thresholds/i }));

    expect(await screen.findByText(/must be at least 0/i)).toBeInTheDocument();
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('calls updateThresholds on save with the dirty form state', async () => {
    const user = userEvent.setup();
    renderWithClient(<ThresholdsConfigForm initial={INITIAL} />);

    const coverageInput = screen.getByLabelText(/minimum coverage/i);
    await user.clear(coverageInput);
    await user.type(coverageInput, '85');

    await user.click(screen.getByRole('button', { name: /save thresholds/i }));

    expect(mockMutate).toHaveBeenCalledTimes(1);
    const [variables] = mockMutate.mock.calls[0];
    expect(variables).toMatchObject({
      minCoveragePct: 85,
      maxPageTolerance: INITIAL.maxPageTolerance,
      minBggMatchPct: INITIAL.minBggMatchPct,
      minOverallScore: INITIAL.minOverallScore,
    });
  });

  it('accepts boundary values 0 and 100 for percentage fields', async () => {
    const user = userEvent.setup();
    renderWithClient(<ThresholdsConfigForm initial={INITIAL} />);

    const coverageInput = screen.getByLabelText(/minimum coverage/i);
    await user.clear(coverageInput);
    await user.type(coverageInput, '0');

    const overallInput = screen.getByLabelText(/minimum overall score/i);
    await user.clear(overallInput);
    await user.type(overallInput, '100');

    await user.click(screen.getByRole('button', { name: /save thresholds/i }));

    expect(mockMutate).toHaveBeenCalledTimes(1);
    const [variables] = mockMutate.mock.calls[0];
    expect(variables).toMatchObject({
      minCoveragePct: 0,
      minOverallScore: 100,
    });
  });
});
