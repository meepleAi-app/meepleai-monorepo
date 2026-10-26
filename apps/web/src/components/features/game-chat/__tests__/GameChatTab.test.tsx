import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock CitationPdfTab to avoid cascading imports of react-pdf + api.knowledgeBase
// (CitationModal lazy-mounts it only when user clicks PDF tab — not exercised here)
vi.mock('../CitationPdfTab', () => ({
  CitationPdfTab: () => null,
}));

vi.mock('@/lib/api/clients/chatClient', () => ({
  qaStream: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn().mockResolvedValue([]),
    },
  },
}));

import {
  QA_TOO_MANY_REQUESTS_MESSAGE,
  QaTooManyRequestsError,
} from '@/components/chat-unified/qaStreamErrors';
import { qaStream } from '@/lib/api/clients/chatClient';
import { api } from '@/lib/api';
import { GameChatTab } from '../GameChatTab';

async function* mockStream(events: Array<{ type: number; data: unknown }>) {
  for (const e of events) yield e;
}

const sampleCitation = {
  documentId: 'd1',
  pageNumber: 12,
  snippet: 'ogni potere…',
  relevanceScore: 0.95,
  copyrightTier: 'full' as const,
};

const happyEvents = [
  { type: 7, data: 'Sì, ' },
  { type: 7, data: 'ogni potere si attiva ogni volta.' },
  { type: 4, data: { confidence: 0.92, Citations: [sampleCitation] } },
];

const lowConfEvents = [
  { type: 7, data: 'Non sono certo, probabilmente si rimescolano gli scarti.' },
  {
    type: 4,
    data: {
      confidence: 0.42,
      Citations: [{ ...sampleCitation, pageNumber: 6, snippet: 'fine mazzo', documentId: 'd2' }],
    },
  },
];

const oocEvents = [
  { type: 7, data: 'Non ho informazioni su Tainted Grail.' },
  { type: 4, data: { confidence: 0.0, Citations: [] } },
];

describe('GameChatTab', () => {
  beforeEach(() => {
    vi.mocked(qaStream).mockReset();
    vi.mocked(api.chat.getThreadsByGame).mockReset();
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValue([]);
  });

  it('renders empty state with input bar and suggested prompts', () => {
    render(<GameChatTab gameId="wingspan" />);
    expect(screen.getByPlaceholderText(/scrivi/i)).toBeInTheDocument();
  });

  it('happy path: ask → user bubble + agent bubble + citation chip + alta badge', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    render(<GameChatTab gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByText(/Sì, ogni potere/)).toBeInTheDocument());
    expect(screen.getByText('q?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /p\. ?12/ })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'alta');
  });

  it('low confidence path: shows disclaimer + bassa badge', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(lowConfEvents) as any);
    render(<GameChatTab gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'edge?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByText(/non sono certo/i)).toBeInTheDocument());
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'bassa');
  });

  it('out-of-context path: shows action pills', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(oocEvents) as any);
    render(<GameChatTab gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'tg?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /cambia gioco/i })).toBeInTheDocument()
    );
    expect(screen.getByRole('button', { name: /cerca un agente/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /resta/i })).toBeInTheDocument();
  });

  it('citation chip click opens CitationModal', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    render(<GameChatTab gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => screen.getByRole('button', { name: /p\. ?12/ }));
    fireEvent.click(screen.getByRole('button', { name: /p\. ?12/ }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    // KB button hidden until G4 → onOpenInKb undefined
    expect(screen.queryByRole('button', { name: /apri nella kb/i })).not.toBeInTheDocument();
  });

  // ─── G2 fast resume tests ───────────────────────────────

  it('shows skeleton bubbles while hydrating', () => {
    // Make getThreadsByGame never resolve → isHydrating stays true
    vi.mocked(api.chat.getThreadsByGame).mockReturnValueOnce(new Promise(() => {}));
    render(<GameChatTab gameId="wingspan" />);
    expect(screen.getAllByTestId('chat-bubble-skeleton').length).toBeGreaterThan(0);
  });

  it('shows ChatHistoryBanner when historical messages + new messages exist', async () => {
    const thread = {
      id: 'thread-1',
      gameId: 'wingspan',
      agentId: null,
      agentType: null,
      title: 'Test',
      createdAt: '2026-05-10T00:00:00Z',
      lastMessageAt: '2026-05-10T10:00:00Z',
      messageCount: 1,
      messages: [{ content: 'old historical', role: 'user', timestamp: '2026-05-10T10:00:00Z' }],
    };
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread] as any);
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);

    render(<GameChatTab gameId="wingspan" />);
    await waitFor(() => expect(screen.getByText('old historical')).toBeInTheDocument());

    // No banner yet (only historical, no new)
    expect(screen.queryByRole('note')).not.toBeInTheDocument();

    // User asks new question
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'new q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByText(/Sì, ogni potere/)).toBeInTheDocument());

    // Banner now visible
    expect(screen.getByRole('note')).toBeInTheDocument();
  });

  it('does NOT show ChatHistoryBanner when only historical messages (no new yet)', async () => {
    const thread = {
      id: 'thread-1',
      gameId: 'wingspan',
      agentId: null,
      agentType: null,
      title: 'Test',
      createdAt: '2026-05-10T00:00:00Z',
      lastMessageAt: '2026-05-10T10:00:00Z',
      messageCount: 1,
      messages: [{ content: 'old only', role: 'user', timestamp: '2026-05-10T10:00:00Z' }],
    };
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread] as any);

    render(<GameChatTab gameId="wingspan" />);
    await waitFor(() => expect(screen.getByText('old only')).toBeInTheDocument());
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });

  /**
   * #4168 (5g-b): la domanda fermata dalla quota del piano. Prima di questo test la chat in partita
   * non mostrava niente quando lo stream finiva con un errore: la domanda restava lì, senza
   * risposta e senza un perché. Per la quota mostra l'avviso, con i numeri del piano e il link.
   */
  it('plan quota: shows the notice with the way to the plans, and keeps the question', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(
      mockStream([
        {
          type: 5,
          data: {
            errorMessage: 'Tier limit exceeded: AgentQuery (3/3)',
            errorCode: 'TIER_LIMIT_EXCEEDED',
            current: 3,
            max: 3,
            upgradeUrl: '/pricing',
          },
        },
      ]) as any
    );
    render(<GameChatTab gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));

    const notice = await screen.findByTestId('tier-limit-exceeded');
    expect(notice).toHaveTextContent(
      'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3)'
    );
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
    expect(screen.getByText('q?')).toBeInTheDocument();
    expect(screen.queryByText(/Tier limit exceeded/)).not.toBeInTheDocument();
  });

  it('plan quota: the notice goes away with the next question', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(
      mockStream([
        { type: 5, data: { errorCode: 'TIER_LIMIT_EXCEEDED', current: 3, max: 3 } },
      ]) as any
    );
    render(<GameChatTab gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await screen.findByTestId('tier-limit-exceeded');

    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q2?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));

    await waitFor(() => expect(screen.getByText(/Sì, ogni potere/)).toBeInTheDocument());
    expect(screen.queryByTestId('tier-limit-exceeded')).not.toBeInTheDocument();
  });

  /**
   * #4388: il backend tiene aperte al massimo due risposte per utente e rifiuta la terza con 429.
   * La domanda resta e compare la frase che dice di aspettare; prima non compariva niente e la
   * richiesta rifiutata risaliva come promessa rifiutata, senza chi la gestisse.
   */
  it('too many open answers: keeps the question and says to wait', async () => {
    const refused = async function* () {
      throw new QaTooManyRequestsError();
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(refused() as any);
    render(<GameChatTab gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));

    expect(await screen.findByTestId('answer-refusal')).toHaveTextContent(
      QA_TOO_MANY_REQUESTS_MESSAGE
    );
    expect(screen.getByText('q?')).toBeInTheDocument();
  });

  it('too many open answers: the notice goes away with the next question', async () => {
    const refused = async function* () {
      throw new QaTooManyRequestsError();
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(refused() as any);
    render(<GameChatTab gameId="wingspan" />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await screen.findByTestId('answer-refusal');

    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q2?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));

    await waitFor(() => expect(screen.getByText(/Sì, ogni potere/)).toBeInTheDocument());
    expect(screen.queryByTestId('answer-refusal')).not.toBeInTheDocument();
  });
});
