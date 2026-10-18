/**
 * useSessionAssistant — #4238 A4 (#4373).
 *
 * The panel disables the other confirm buttons while one confirmation is in flight, but two taps
 * can land in the same tick, before the re-render that disables them. The hook is the guard that
 * holds then: these tests call it directly, with no button in between.
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type {
  AssistantProposalDto,
  SessionAssistantPanelDto,
} from '@/lib/api/schemas/live-sessions.schemas';

import { useSessionAssistant } from '../useSessionAssistant';

const getAssistant =
  vi.fn<(sessionId: string, language?: string) => Promise<SessionAssistantPanelDto>>();
const confirmAssistantProposal =
  vi.fn<
    (sessionId: string, proposalId: string, points?: number) => Promise<AssistantProposalDto>
  >();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      getAssistant: (sessionId: string, language?: string) => getAssistant(sessionId, language),
      confirmAssistantProposal: (sessionId: string, proposalId: string, points?: number) =>
        confirmAssistantProposal(sessionId, proposalId, points),
      rejectAssistantProposal: vi.fn(),
    },
  },
}));

vi.mock('@/lib/session-live/assistant-stream', () => ({ askSessionAssistant: vi.fn() }));

function proposal(id: string, kind = 'Score'): AssistantProposalDto {
  return {
    id,
    kind,
    status: 'Pending',
    messageId: 'm2',
    summary: id,
  };
}

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

beforeEach(() => {
  getAssistant.mockReset();
  confirmAssistantProposal.mockReset();
  getAssistant.mockResolvedValue({
    availability: { enabled: true, code: null },
    messages: [],
    proposals: [proposal('p1'), proposal('p2', 'AdvanceTurn')],
  });
});

describe('useSessionAssistant (#4373)', () => {
  it('two confirmations in the same tick start only the first', async () => {
    let finish!: (value: AssistantProposalDto) => void;
    confirmAssistantProposal.mockReturnValue(new Promise(resolve => (finish = resolve)));

    const { result } = renderHook(() => useSessionAssistant('live-1', 'it'), { wrapper });
    await waitFor(() => expect(result.current.proposals).toHaveLength(2));

    act(() => {
      void result.current.confirm('p1');
      void result.current.confirm('p2');
    });

    expect(confirmAssistantProposal).toHaveBeenCalledTimes(1);
    expect(confirmAssistantProposal).toHaveBeenCalledWith('live-1', 'p1', undefined);
    expect(result.current.confirmingId).toBe('p1');

    await act(async () => finish({ ...proposal('p1'), status: 'Accepted' }));
    await waitFor(() => expect(result.current.confirmingId).toBeNull());

    // Once the first one is settled, the next confirmation starts.
    confirmAssistantProposal.mockResolvedValue({
      ...proposal('p2', 'AdvanceTurn'),
      status: 'Accepted',
    });
    await act(async () => result.current.confirm('p2'));
    expect(confirmAssistantProposal).toHaveBeenCalledTimes(2);
  });
});
