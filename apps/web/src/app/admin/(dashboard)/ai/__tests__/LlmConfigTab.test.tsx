/**
 * #4169 (ADR-095 D8, fetta 6a): la pagina «Budget e quote».
 *
 * Il runtime applica tetti, soglia di degrado e quote (LlmBudgetGate), ma prima di questa fetta la
 * pagina salvava solo i tetti, insieme a soglie del circuit breaker e catena di fallback che il
 * runtime non legge. Questi test fissano il contratto: si salva ciò che il gate applica, e solo quello.
 */
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { LlmConfigTab } from '../LlmConfigTab';

const mockGetConfig = vi.hoisted(() => vi.fn());
const mockUpdateConfig = vi.hoisted(() => vi.fn());
const mockGetUsage = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getLlmSystemConfig: mockGetConfig,
      updateLlmSystemConfig: mockUpdateConfig,
      getLlmBudgetUsage: mockGetUsage,
    },
  },
}));

const CONFIG = {
  circuitBreakerFailureThreshold: 5,
  circuitBreakerOpenDurationSeconds: 30,
  circuitBreakerSuccessThreshold: 2,
  dailyBudgetUsd: 1,
  monthlyBudgetUsd: 20,
  degradeThresholdPercent: 80,
  backgroundQuotaPercent: 30,
  evaluationQuotaPercent: 20,
  fallbackChainJson: '[]',
  source: 'database' as const,
  lastUpdatedAt: '2026-10-11T12:00:00Z',
  lastUpdatedByUserId: null,
};

const USAGE = {
  available: true,
  day: '2026-10-11',
  today: { totalUsd: 0.85, answersUsd: 0.7, backgroundUsd: 0.1, evaluationsUsd: 0.05 },
  month: { totalUsd: 4.5, answersUsd: 4, backgroundUsd: 0.3, evaluationsUsd: 0.2 },
  isDegraded: true,
};

describe('LlmConfigTab — Budget e quote (#4169)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetConfig.mockResolvedValue(CONFIG);
    mockUpdateConfig.mockImplementation(async (request: object) => ({ ...CONFIG, ...request }));
    mockGetUsage.mockResolvedValue(USAGE);
  });

  it('saves the degrade threshold and the quotas the gateway applies', async () => {
    const user = userEvent.setup();
    renderWithQuery(<LlmConfigTab />);

    const threshold = await screen.findByLabelText(/degrade threshold/i);
    await user.clear(threshold);
    await user.type(threshold, '70');
    const background = screen.getByLabelText(/background quota/i);
    await user.clear(background);
    await user.type(background, '25');
    await user.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(mockUpdateConfig).toHaveBeenCalledTimes(1));
    expect(mockUpdateConfig).toHaveBeenCalledWith(
      expect.objectContaining({
        dailyBudgetUsd: 1,
        monthlyBudgetUsd: 20,
        degradeThresholdPercent: 70,
        backgroundQuotaPercent: 25,
        evaluationQuotaPercent: 20,
      })
    );
  });

  it('does not offer settings the runtime does not read', async () => {
    const user = userEvent.setup();
    renderWithQuery(<LlmConfigTab />);
    const daily = await screen.findByLabelText(/daily cap/i);

    expect(screen.queryByText(/fallback chain/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/failure threshold/i)).not.toBeInTheDocument();

    await user.clear(daily);
    await user.type(daily, '2');
    await user.click(screen.getByRole('button', { name: /save/i }));
    await waitFor(() => expect(mockUpdateConfig).toHaveBeenCalledTimes(1));
    const request = mockUpdateConfig.mock.calls[0][0] as Record<string, unknown>;
    expect(request).not.toHaveProperty('fallbackChainJson');
    expect(request).not.toHaveProperty('circuitBreakerFailureThreshold');
  });

  it("shows today's and this month's spend per class, and that the degrade is active", async () => {
    renderWithQuery(<LlmConfigTab />);

    expect(await screen.findByText(/degrade active/i)).toBeInTheDocument();
    const today = screen.getByRole('region', { name: /today/i });
    expect(today).toHaveTextContent('$0.85');
    expect(today).toHaveTextContent('$1.00');
    expect(today).toHaveTextContent('$0.70');
    expect(today).toHaveTextContent('$0.10');
    const month = screen.getByRole('region', { name: /this month/i });
    expect(month).toHaveTextContent('$4.50');
    expect(month).toHaveTextContent('$20.00');
  });

  it('says so when the spend cannot be read', async () => {
    mockGetUsage.mockResolvedValue({
      available: false,
      day: null,
      today: null,
      month: null,
      isDegraded: false,
    });
    renderWithQuery(<LlmConfigTab />);

    expect(await screen.findByText(/spend cannot be read/i)).toBeInTheDocument();
  });

  it('refuses quotas that together exceed 100% without saving', async () => {
    const user = userEvent.setup();
    renderWithQuery(<LlmConfigTab />);

    const evaluation = await screen.findByLabelText(/evaluation quota/i);
    await user.clear(evaluation);
    await user.type(evaluation, '80');
    await user.click(screen.getByRole('button', { name: /save/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/100/);
    expect(mockUpdateConfig).not.toHaveBeenCalled();
  });
});
