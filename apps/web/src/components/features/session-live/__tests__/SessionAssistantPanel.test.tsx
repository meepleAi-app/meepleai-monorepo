/**
 * SessionAssistantPanel — #4238 A4 (#4373).
 *
 * The rules under test come from the reviews of A3 (#4297, #4301):
 *   - after a reload, the conversation and the open cards come back from the panel read;
 *   - proposals are confirmed one at a time;
 *   - the first `status` of a turn expires the cards still Pending from earlier turns;
 *   - a closed card (Stale, Failed, …) says why and cannot be confirmed.
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApiError } from '@/lib/api/core/errors';
import type {
  AssistantProposalDto,
  AssistantTurnEvent,
  SessionAssistantPanelDto,
} from '@/lib/api/schemas/live-sessions.schemas';
import itMessages from '@/locales/it.json';

import { SessionAssistantPanel } from '../assistant/SessionAssistantPanel';

// ─── Mocks ────────────────────────────────────────────────────────────────────

const getAssistant =
  vi.fn<(sessionId: string, language?: string) => Promise<SessionAssistantPanelDto>>();
const confirmAssistantProposal =
  vi.fn<
    (sessionId: string, proposalId: string, points?: number) => Promise<AssistantProposalDto>
  >();
const rejectAssistantProposal =
  vi.fn<(sessionId: string, proposalId: string) => Promise<AssistantProposalDto>>();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      getAssistant: (sessionId: string, language?: string) => getAssistant(sessionId, language),
      confirmAssistantProposal: (sessionId: string, proposalId: string, points?: number) =>
        confirmAssistantProposal(sessionId, proposalId, points),
      rejectAssistantProposal: (sessionId: string, proposalId: string) =>
        rejectAssistantProposal(sessionId, proposalId),
    },
  },
}));

const askSessionAssistant =
  vi.fn<
    (sessionId: string, question: string, language?: string) => AsyncGenerator<AssistantTurnEvent>
  >();

vi.mock('@/lib/session-live/assistant-stream', () => ({
  askSessionAssistant: (sessionId: string, question: string, language?: string) =>
    askSessionAssistant(sessionId, question, language),
}));

// ─── Helpers ──────────────────────────────────────────────────────────────────

function flattenMessages(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
  return Object.entries(obj).reduce<Record<string, string>>((acc, [key, val]) => {
    const fullKey = prefix ? `${prefix}.${key}` : key;
    if (typeof val === 'string') acc[fullKey] = val;
    else if (typeof val === 'object' && val !== null)
      Object.assign(acc, flattenMessages(val as Record<string, unknown>, fullKey));
    return acc;
  }, {});
}

const FLAT_IT = flattenMessages(itMessages as Record<string, unknown>);

function renderPanel() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <IntlProvider locale="it" messages={FLAT_IT}>
          {children}
        </IntlProvider>
      </QueryClientProvider>
    );
  }
  return render(<SessionAssistantPanel sessionId="live-1" />, { wrapper: Wrapper });
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function proposal(overrides: Partial<AssistantProposalDto> = {}): AssistantProposalDto {
  return {
    id: 'p1',
    kind: 'Score',
    status: 'Pending',
    messageId: 'm2',
    playerId: 'marco',
    playerName: 'Marco',
    dimension: 'punti',
    round: 1,
    points: 5,
    cellValueBefore: 0,
    resultingValue: 5,
    expectedTurnIndex: null,
    confirmedPoints: null,
    failureCode: null,
    summary: '+5 punti a Marco, round 1',
    ...overrides,
  };
}

function panel(overrides: Partial<SessionAssistantPanelDto> = {}): SessionAssistantPanelDto {
  return {
    availability: { enabled: true, code: null },
    messages: [
      { id: 'm1', role: 'user', content: 'Marco fa 5 punti', createdAt: '2026-10-14T10:00:00Z' },
      {
        id: 'm2',
        role: 'assistant',
        content: 'Ecco la proposta.',
        createdAt: '2026-10-14T10:00:01Z',
      },
    ],
    proposals: [proposal()],
    ...overrides,
  };
}

const cards = () => screen.queryAllByRole('article');
const confirmButtonOf = (card: HTMLElement) =>
  within(card).queryByRole('button', { name: 'Conferma' });

/** A turn that yields the given events, then waits for `release` before ending. */
function heldTurn(events: AssistantTurnEvent[], release: Promise<void>) {
  return async function* turn(): AsyncGenerator<AssistantTurnEvent> {
    for (const event of events) yield event;
    await release;
  };
}

async function ask(question: string) {
  const input = screen.getByRole('textbox', { name: "Domanda all'assistente" });
  fireEvent.change(input, { target: { value: question } });
  fireEvent.click(screen.getByRole('button', { name: 'Invia' }));
}

beforeEach(() => {
  getAssistant.mockReset();
  confirmAssistantProposal.mockReset();
  rejectAssistantProposal.mockReset();
  askSessionAssistant.mockReset();
});

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('SessionAssistantPanel (#4373)', () => {
  it('after a reload, shows the conversation and the open card under its answer', async () => {
    getAssistant.mockResolvedValue(panel());

    renderPanel();

    expect(await screen.findByText('Marco fa 5 punti')).toBeInTheDocument();
    const answer = screen.getByText('Ecco la proposta.').closest('li')!;
    const card = within(answer).getByRole('article');
    expect(card).toHaveAttribute('data-status', 'Pending');
    expect(within(card).getByText('+5 punti a Marco, round 1')).toBeInTheDocument();
    expect(within(card).getByText('Totale dopo la conferma: 5')).toBeInTheDocument();
    expect(confirmButtonOf(card)).toBeEnabled();
    expect(getAssistant).toHaveBeenCalledWith('live-1', 'it');
  });

  it('confirms one card at a time: a second confirmation does not start while the first is in flight', async () => {
    getAssistant.mockResolvedValue(
      panel({
        proposals: [
          proposal({ id: 'p1' }),
          proposal({ id: 'p2', kind: 'AdvanceTurn', playerName: null }),
        ],
      })
    );
    const first = deferred<AssistantProposalDto>();
    confirmAssistantProposal.mockReturnValueOnce(first.promise);

    renderPanel();
    await waitFor(() => expect(cards()).toHaveLength(2));
    const [scoreCard, turnCard] = cards();

    fireEvent.click(confirmButtonOf(scoreCard)!);
    await waitFor(() => expect(confirmButtonOf(turnCard)).toBeDisabled());
    fireEvent.click(confirmButtonOf(turnCard)!);

    expect(confirmAssistantProposal).toHaveBeenCalledTimes(1);
    expect(confirmAssistantProposal).toHaveBeenCalledWith('live-1', 'p1', undefined);
    expect(within(turnCard).getByText("Un'altra conferma è in corso.")).toBeInTheDocument();

    await act(async () => first.resolve(proposal({ id: 'p1', status: 'Accepted' })));

    await waitFor(() => expect(confirmButtonOf(cards()[1])).toBeEnabled());
    expect(cards()[0]).toHaveAttribute('data-status', 'Accepted');
  });

  it('the first status of a turn expires the cards still Pending from earlier turns', async () => {
    getAssistant.mockResolvedValue(panel());
    const release = deferred<void>();
    askSessionAssistant.mockImplementation(heldTurn([{ type: 'status' }], release.promise));

    renderPanel();
    await waitFor(() => expect(cards()).toHaveLength(1));

    await act(async () => ask('a chi tocca?'));

    await waitFor(() => expect(cards()[0]).toHaveAttribute('data-status', 'Expired'));
    expect(within(cards()[0]).getByText('Scaduta')).toBeInTheDocument();
    expect(confirmButtonOf(cards()[0])).toBeNull();
    expect(screen.getByRole('textbox', { name: "Domanda all'assistente" })).toBeDisabled();

    getAssistant.mockResolvedValue(panel({ proposals: [proposal({ status: 'Expired' })] }));
    await act(async () => release.resolve());
  });

  it('a turn streams its card under the answer that produced it', async () => {
    getAssistant.mockResolvedValue(panel({ messages: [], proposals: [] }));
    const release = deferred<void>();
    askSessionAssistant.mockImplementation(
      heldTurn(
        [
          { type: 'status' },
          { type: 'status', tool: 'proponi_punteggio' },
          { type: 'proposal', proposal: proposal({ id: 'p9', messageId: 'm9' }) },
          { type: 'answer', text: 'Confermala dalla card.' },
        ],
        release.promise
      )
    );

    renderPanel();
    await screen.findByText(/io propongo, tu confermi/);
    await act(async () => ask('Marco fa 5 punti'));

    const answer = (await screen.findByText('Confermala dalla card.')).closest('li')!;
    expect(within(answer).getByRole('article')).toHaveAttribute('data-status', 'Pending');
    expect(screen.getByText('Preparo un punteggio…')).toBeInTheDocument();

    await act(async () => release.resolve());
  });

  it('a Stale or Failed card says why and cannot be confirmed', async () => {
    getAssistant.mockResolvedValue(
      panel({
        proposals: [
          proposal({ id: 'p1', status: 'Stale' }),
          proposal({ id: 'p2', status: 'Failed', failureCode: 'proposal_not_applicable' }),
        ],
      })
    );

    renderPanel();
    await waitFor(() => expect(cards()).toHaveLength(2));
    const [stale, failed] = cards();

    expect(within(stale).getByText('Superata: la partita è cambiata')).toBeInTheDocument();
    expect(within(failed).getByText('Non applicata')).toBeInTheDocument();
    expect(
      within(failed).getByText('La proposta non vale più per questa partita.')
    ).toBeInTheDocument();
    expect(confirmButtonOf(stale)).toBeNull();
    expect(confirmButtonOf(failed)).toBeNull();
  });

  it('a confirmation refused with proposal_stale says so, and the card is read again', async () => {
    getAssistant.mockResolvedValueOnce(panel());
    getAssistant.mockResolvedValue(panel({ proposals: [proposal({ status: 'Stale' })] }));
    confirmAssistantProposal.mockRejectedValue(
      new ApiError({ message: 'stale', statusCode: 409, code: 'proposal_stale' })
    );

    renderPanel();
    await waitFor(() => expect(cards()).toHaveLength(1));
    fireEvent.click(confirmButtonOf(cards()[0])!);

    expect(
      await screen.findByText('La partita è cambiata dalla proposta: non è stato scritto nulla.')
    ).toBeInTheDocument();
    await waitFor(() => expect(cards()[0]).toHaveAttribute('data-status', 'Stale'));
  });

  it('a correction confirms the corrected points', async () => {
    getAssistant.mockResolvedValue(panel());
    confirmAssistantProposal.mockResolvedValue(proposal({ status: 'Edited', confirmedPoints: 6 }));

    renderPanel();
    await waitFor(() => expect(cards()).toHaveLength(1));
    fireEvent.click(within(cards()[0]).getByRole('button', { name: 'Correggi' }));
    fireEvent.change(within(cards()[0]).getByLabelText('Punti corretti'), {
      target: { value: '6' },
    });
    fireEvent.click(within(cards()[0]).getByRole('button', { name: 'Conferma con correzione' }));

    await waitFor(() => expect(confirmAssistantProposal).toHaveBeenCalledWith('live-1', 'p1', 6));
  });

  it('with the assistant off, says why and disables the input', async () => {
    getAssistant.mockResolvedValue(
      panel({
        availability: { enabled: false, code: 'assistant_disabled' },
        messages: [],
        proposals: [],
      })
    );

    renderPanel();

    expect(await screen.findByText("L'assistente non è attivo.")).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: "Domanda all'assistente" })).toBeDisabled();
  });

  it('a turn refused before it starts says why and drops the unsaved question', async () => {
    getAssistant.mockResolvedValue(panel({ messages: [], proposals: [] }));
    askSessionAssistant.mockImplementation(
      async function* refused(): AsyncGenerator<AssistantTurnEvent> {
        throw new ApiError({ message: 'over', statusCode: 409, code: 'session_state_invalid' });
      }
    );

    renderPanel();
    await screen.findByText(/io propongo, tu confermi/);
    await act(async () => ask('a chi tocca?'));

    expect(
      await screen.findByText("La partita è conclusa: l'assistente non risponde più.")
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('a chi tocca?')).not.toBeInTheDocument());
  });
});
