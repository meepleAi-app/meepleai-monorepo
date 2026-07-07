/**
 * AgentConfigModal test — locks the "modal unchanged" invariant after the
 * Issue #2732 refactor that extracted the edit-mode fields into the shared
 * AgentConfigFields component. The modal must still render the same fields and
 * still persist the seeded config through the update mutation on Save.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mutateAsync = vi.fn();
const useAgentConfigMock = vi.fn();
const useUpdateAgentConfigMock = vi.fn(() => ({ mutateAsync, isPending: false }));

// Partial mock: keep agentConfigKeys (and everything else) real; override only
// the two hooks the modal consumes.
vi.mock('@/hooks/queries', async importOriginal => {
  const actual = await importOriginal<typeof import('@/hooks/queries')>();
  return {
    ...actual,
    useAgentConfig: (...args: unknown[]) => useAgentConfigMock(...args),
    useUpdateAgentConfig: () => useUpdateAgentConfigMock(),
  };
});

vi.mock('@/components/layout/Toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() },
}));

import { AgentConfigModal } from '../AgentConfigModal';

const SEEDED = {
  llmModel: 'deepseek-chat' as const,
  temperature: 0.9,
  maxTokens: 2048,
  personality: 'Professionale' as const,
  detailLevel: 'Dettagliato' as const,
  personalNotes: 'nota',
};

function renderModal() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <AgentConfigModal isOpen onClose={vi.fn()} gameId="game-1" gameTitle="Catan" />
    </QueryClientProvider>
  );
}

describe('AgentConfigModal (invariant lock — Issue #2732)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mutateAsync.mockResolvedValue({});
    useAgentConfigMock.mockReturnValue({ data: SEEDED, isLoading: false });
    useUpdateAgentConfigMock.mockReturnValue({ mutateAsync, isPending: false });
  });

  it('renders the shared config fields in edit mode', async () => {
    renderModal();
    expect(await screen.findByRole('spinbutton')).toBeInTheDocument(); // maxTokens
    expect(screen.getByRole('combobox')).toBeInTheDocument(); // model select
    expect(screen.getByRole('textbox')).toBeInTheDocument(); // notes textarea
  });

  it('Salva Configurazione persists the seeded config via mutateAsync', async () => {
    renderModal();

    // Wait for the seeding effect to hydrate the fields from currentConfig.
    const input = await screen.findByRole('spinbutton');
    await waitFor(() => expect(input).toHaveValue(2048));

    fireEvent.click(screen.getByRole('button', { name: /salva configurazione/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1));
    expect(mutateAsync).toHaveBeenCalledWith({ gameId: 'game-1', request: SEEDED });
  });
});
