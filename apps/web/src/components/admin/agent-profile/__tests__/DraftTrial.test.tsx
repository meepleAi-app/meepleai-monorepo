/**
 * #4169 (6c-3, ADR-095 D8): l'admin prova la bozza prima di pubblicarla, con la stessa pipeline delle
 * risposte agli utenti.
 */
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { DraftTrial } from '../DraftTrial';

const mockGetGameKbStatuses = vi.hoisted(() => vi.fn());
const mockTry = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: { admin: { getGameKbStatuses: mockGetGameKbStatuses } },
}));

vi.mock('@/lib/api/clients/agentProfileTrialStream', async importOriginal => ({
  ...(await importOriginal<typeof import('@/lib/api/clients/agentProfileTrialStream')>()),
  tryAgentProfileDraft: mockTry,
}));

const GAMES = {
  items: [
    {
      gameId: 'g-catan',
      gameName: 'Catan',
      kbStatus: 'complete',
      documentCount: 1,
      totalChunks: 90,
      latestIndexedAt: null,
      hasAutoBackup: false,
    },
    {
      gameId: 'g-empty',
      gameName: 'No rules yet',
      kbStatus: 'none',
      documentCount: 0,
      totalChunks: 0,
      latestIndexedAt: null,
      hasAutoBackup: false,
    },
  ],
};

async function* stream(events: { type: number; data: unknown }[]) {
  for (const e of events) yield e;
}

beforeEach(() => {
  vi.clearAllMocks();
  mockGetGameKbStatuses.mockResolvedValue(GAMES);
});

async function ask(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Question'), 'How many cards at setup?');
  await user.click(screen.getByRole('button', { name: /ask the draft/i }));
}

describe('DraftTrial', () => {
  it('offers only the games with a knowledge base', async () => {
    render(<DraftTrial draftVersion={3} dirty={false} />);

    const select = await screen.findByLabelText('Game');
    await waitFor(() => expect(within(select).getAllByRole('option')).toHaveLength(1));
    expect(within(select).getByRole('option', { name: 'Catan' })).toBeInTheDocument();
  });

  it('streams the answer of the draft and says which version answered', async () => {
    const user = userEvent.setup();
    mockTry.mockReturnValue(
      stream([
        { type: 1, data: { citations: [{ source: 'PDF:x', page: 3 }] } },
        { type: 7, data: { token: 'Draw' } },
        { type: 7, data: { token: ' two cards.' } },
        { type: 4, data: { profileVersionId: 'v3' } },
      ])
    );
    render(<DraftTrial draftVersion={3} dirty={false} />);
    await screen.findByRole('option', { name: 'Catan' });

    await ask(user);

    expect(await screen.findByText('Draw two cards.')).toBeInTheDocument();
    expect(screen.getByText(/answered by draft version 3/i)).toBeInTheDocument();
    expect(screen.getByText(/1 source/i)).toBeInTheDocument();
    expect(mockTry).toHaveBeenCalledWith(
      { gameId: 'g-catan', question: 'How many cards at setup?' },
      expect.anything()
    );
  });

  it('shows why a trial was refused', async () => {
    const user = userEvent.setup();
    mockTry.mockImplementation(() => {
      throw new Error('The agent profile has no draft to try: save one first.');
    });
    render(<DraftTrial draftVersion={3} dirty={false} />);
    await screen.findByRole('option', { name: 'Catan' });

    await ask(user);

    expect(await screen.findByRole('alert')).toHaveTextContent(/no draft to try/i);
  });

  it('shows an error event from the stream', async () => {
    const user = userEvent.setup();
    mockTry.mockReturnValue(
      stream([{ type: 5, data: { message: 'AI budget paused', code: 'AI_BUDGET' } }])
    );
    render(<DraftTrial draftVersion={3} dirty={false} />);
    await screen.findByRole('option', { name: 'Catan' });

    await ask(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('AI budget paused');
  });

  it('waits for a saved draft', async () => {
    render(<DraftTrial draftVersion={3} dirty />);
    await screen.findByRole('option', { name: 'Catan' });

    expect(screen.getByRole('button', { name: /ask the draft/i })).toBeDisabled();
    expect(screen.getByText(/save the draft to try it/i)).toBeInTheDocument();
  });

  it('needs a draft', async () => {
    render(<DraftTrial draftVersion={null} dirty={false} />);
    await screen.findByRole('option', { name: 'Catan' });

    expect(screen.getByRole('button', { name: /ask the draft/i })).toBeDisabled();
  });
});
