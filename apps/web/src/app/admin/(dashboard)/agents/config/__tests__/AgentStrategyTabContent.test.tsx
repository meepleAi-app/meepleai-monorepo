/**
 * #4169 (ADR-095 D8, fetta 6d): il tab Strategy non offre impostazioni che il runtime non legge.
 *
 * Prima di questa fetta il tab mostrava Retrieval, Generation e Confidence che il salvataggio non
 * mandava mai (e il toast diceva comunque «Changes saved»), tre schede con numeri scritti a mano e un
 * «Edit» senza handler sui mapping. Resta la matrice tier×strategia, che il runtime legge.
 */
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { AgentStrategyTabContent } from '../AgentStrategyTabContent';

const client = vi.hoisted(() => ({
  getTierStrategyMatrix: vi.fn(),
  getStrategyModelMappings: vi.fn(),
  updateTierStrategyAccess: vi.fn(),
  updateStrategyModelMapping: vi.fn(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => client,
}));

vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

vi.mock('@/hooks/useAdminConfig', () => ({
  useAdminConfig: () => ({ data: undefined }),
  parseConfigValue: () => undefined,
}));

const MATRIX = {
  tiers: ['User', 'Admin'],
  strategies: [
    { name: 'Balanced', displayName: 'Balanced' },
    { name: 'Precise', displayName: 'Precise' },
  ],
  accessMatrix: [
    { tier: 'User', strategy: 'Balanced', isEnabled: true },
    { tier: 'User', strategy: 'Precise', isEnabled: false },
    { tier: 'Admin', strategy: 'Balanced', isEnabled: true },
    { tier: 'Admin', strategy: 'Precise', isEnabled: true },
  ],
};

const MAPPINGS = [
  {
    strategy: 'Balanced',
    provider: 'DeepSeek',
    primaryModel: 'deepseek-flash',
    fallbackModels: ['meta-llama/llama-3.3-70b-instruct'],
  },
];

describe('AgentStrategyTabContent (#4169)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    client.getTierStrategyMatrix.mockResolvedValue(MATRIX);
    client.getStrategyModelMappings.mockResolvedValue(MAPPINGS);
    client.updateTierStrategyAccess.mockResolvedValue(undefined);
  });

  it('offers no retrieval, generation or confidence setting the save never sent', async () => {
    renderWithQuery(<AgentStrategyTabContent />);
    await screen.findByText('deepseek-flash');

    expect(screen.queryByText(/confidence thresholds/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/retrieval configuration/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/generation configuration/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('slider')).not.toBeInTheDocument();
  });

  it('shows no hard-coded numbers as if they were measured', async () => {
    renderWithQuery(<AgentStrategyTabContent />);
    await screen.findByText('deepseek-flash');

    expect(screen.queryByText('743ms')).not.toBeInTheDocument();
    expect(screen.queryByText('Claude 3.5')).not.toBeInTheDocument();
  });

  it('shows the model mappings read-only', async () => {
    renderWithQuery(<AgentStrategyTabContent />);
    await screen.findByText('deepseek-flash');

    expect(screen.queryByRole('button', { name: /^edit$/i })).not.toBeInTheDocument();
  });

  it('saves a change of the tier access matrix', async () => {
    const user = userEvent.setup();
    renderWithQuery(<AgentStrategyTabContent />);

    await user.click(await screen.findByRole('checkbox', { name: /user can use precise/i }));
    await user.click(screen.getByRole('button', { name: /save tier access/i }));

    await waitFor(() =>
      expect(client.updateTierStrategyAccess).toHaveBeenCalledWith({
        tier: 'User',
        strategy: 'Precise',
        isEnabled: true,
      })
    );
  });
});
