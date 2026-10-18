/**
 * @vitest-environment jsdom
 *
 * /admin/knowledge-base/reference-questions — #4470 (ADR-095 fetta 7b, D8). Le domande di riferimento
 * con cui si valuta l'agente, in sola lettura: il set lo scrive l'utente e lo carica il seed.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockGetQuestions = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getAdminReferenceQuestions: (...args: unknown[]) => mockGetQuestions(...args),
    },
  },
}));

import ReferenceQuestionsPage from '../page';

const CATAN = '11111111-1111-1111-1111-111111111111';
const EUROPE = '22222222-2222-2222-2222-222222222222';

const question = (
  code: string,
  gameId: string,
  gameName: string | null,
  overrides: Partial<{ outOfCorpus: boolean; inGate: boolean }> = {}
) => ({
  code,
  gameId,
  gameName,
  question: `Domanda ${code}?`,
  expectedAnswer: `Risposta attesa ${code}.`,
  category: 'Commercio',
  difficulty: 'Medium',
  questionType: 'Procedurale',
  sourceReference: `Manuale - ${code}`,
  outOfCorpus: false,
  inGate: false,
  ...overrides,
});

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

describe('ReferenceQuestionsPage', () => {
  beforeEach(() => {
    mockGetQuestions.mockReset();
    mockGetQuestions.mockResolvedValue({
      items: [
        question('CAT-01', CATAN, 'Catan', { outOfCorpus: true }),
        question('CAT-02', CATAN, 'Catan', { inGate: true }),
        question('TTR-18', EUROPE, 'Ticket to Ride: Europe'),
      ],
      games: [
        { gameId: CATAN, gameName: 'Catan', questionCount: 2 },
        { gameId: EUROPE, gameName: 'Ticket to Ride: Europe', questionCount: 1 },
      ],
    });
  });

  it('lists every question with its game, its expected answer and its flags', async () => {
    render(<ReferenceQuestionsPage />, { wrapper: makeWrapper() });

    const first = await screen.findByRole('article', { name: 'CAT-01' });
    expect(first).toHaveTextContent('Catan');
    expect(first).toHaveTextContent('Domanda CAT-01?');
    expect(first).toHaveTextContent('Risposta attesa CAT-01.');
    expect(first).toHaveTextContent('Manuale - CAT-01');
    expect(first).toHaveTextContent(/fuori dal corpus/i);
    expect(first).not.toHaveTextContent(/nel gate/i);
    expect(screen.getByRole('article', { name: 'CAT-02' })).toHaveTextContent(/nel gate/i);
    expect(screen.getAllByRole('article')).toHaveLength(3);
    expect(mockGetQuestions.mock.calls.at(-1)?.[0]).toEqual({
      gameId: undefined,
      inGate: undefined,
    });
  });

  it('says so when the questions cannot be loaded, instead of a blank page', async () => {
    mockGetQuestions.mockRejectedValue(new Error('500'));

    render(<ReferenceQuestionsPage />, { wrapper: makeWrapper() });

    expect(await screen.findByRole('alert')).toHaveTextContent(/non è stato possibile/i);
  });

  it('filters by a game chosen by name, with its number of questions', async () => {
    const user = userEvent.setup();
    render(<ReferenceQuestionsPage />, { wrapper: makeWrapper() });

    await user.click(await screen.findByRole('combobox', { name: 'Gioco' }));
    await user.click(await screen.findByRole('option', { name: 'Ticket to Ride: Europe (1)' }));

    await waitFor(() =>
      expect(mockGetQuestions.mock.calls.at(-1)?.[0]).toMatchObject({ gameId: EUROPE })
    );
  });

  it('filters by the publication gate, in both directions', async () => {
    const user = userEvent.setup();
    render(<ReferenceQuestionsPage />, { wrapper: makeWrapper() });

    await user.click(await screen.findByRole('combobox', { name: 'Gate' }));
    await user.click(await screen.findByRole('option', { name: 'Nel gate' }));
    await waitFor(() =>
      expect(mockGetQuestions.mock.calls.at(-1)?.[0]).toMatchObject({ inGate: true })
    );

    await user.click(screen.getByRole('combobox', { name: 'Gate' }));
    await user.click(await screen.findByRole('option', { name: 'Fuori dal gate' }));
    await waitFor(() =>
      expect(mockGetQuestions.mock.calls.at(-1)?.[0]).toMatchObject({ inGate: false })
    );
  });

  it('says that the seed has loaded nothing yet, when there are no questions at all', async () => {
    mockGetQuestions.mockResolvedValue({ items: [], games: [] });

    render(<ReferenceQuestionsPage />, { wrapper: makeWrapper() });

    expect(await screen.findByText(/nessuna domanda di riferimento caricata/i)).toBeInTheDocument();
  });
});
