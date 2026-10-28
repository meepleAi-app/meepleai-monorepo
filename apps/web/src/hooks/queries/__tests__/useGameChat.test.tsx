/**
 * useGameChat — unit tests
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.4
 */
import { renderHook, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api/clients/chatClient', () => ({
  qaStream: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn(),
    },
  },
}));

import {
  QA_TOO_MANY_REQUESTS_MESSAGE,
  QaTooManyRequestsError,
} from '@/components/chat-unified/qaStreamErrors';
import { qaStream } from '@/lib/api/clients/chatClient';
import { api } from '@/lib/api';
import { logger } from '@/lib/logger';
import { useGameChat } from '../useGameChat';

async function* mockStream(events: Array<{ type: number; data: unknown }>) {
  for (const e of events) yield e;
}

const sampleCitation = {
  documentId: 'd1',
  pageNumber: 12,
  snippet: 'Ogni potere…',
  relevanceScore: 0.95,
  copyrightTier: 'full' as const,
};

const happyEvents = [
  { type: 7, data: 'Sì, ' },
  { type: 7, data: 'ogni potere si attiva ogni volta.' },
  { type: 4, data: { confidence: 0.92, Citations: [sampleCitation] } },
];

describe('useGameChat', () => {
  beforeEach(() => {
    vi.mocked(qaStream).mockReset();
    vi.mocked(api.chat.getThreadsByGame).mockReset();
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValue([]);
  });

  it('starts with empty messages and tutor agent', () => {
    const { result } = renderHook(() => useGameChat('wingspan'));
    expect(result.current.messages).toEqual([]);
    expect(result.current.currentAgent).toBe('tutor');
    expect(result.current.isLoading).toBe(false);
  });

  it('ask appends user + agent messages on success', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('posso usare potere?');
    });
    expect(result.current.messages).toHaveLength(2);
    expect(result.current.messages[0].role).toBe('user');
    expect(result.current.messages[0].content).toBe('posso usare potere?');
    expect(result.current.messages[1].role).toBe('agent');
    expect(result.current.messages[1].content).toBe('Sì, ogni potere si attiva ogni volta.');
    expect(result.current.messages[1].overallConfidence).toBe(0.92);
    expect(result.current.messages[1].citations).toHaveLength(1);
    expect(result.current.messages[1].isLowQuality).toBe(false);
  });

  it('derives isLowQuality=true when confidence < 0.70 AND no citations', async () => {
    const lowConfEvents = [
      { type: 7, data: 'Non sono certo.' },
      { type: 4, data: { confidence: 0.42, Citations: [] } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(lowConfEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('edge?');
    });
    expect(result.current.messages[1].isLowQuality).toBe(true);
    expect(result.current.messages[1].outOfContext).toBe(false);
  });

  it('does NOT mark isLowQuality when citations are present even if confidence < 0.70 (Issue #2712)', async () => {
    // A grounded answer with valid citations must be shown, not hidden behind the "Non sono certo" card.
    const groundedLowConf = [
      { type: 7, data: 'Il punteggio si calcola contando le carte agente rimaste.' },
      { type: 4, data: { confidence: 0.53, Citations: [sampleCitation] } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(groundedLowConf) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('come si calcola il punteggio?');
    });
    expect(result.current.messages[1].isLowQuality).toBe(false);
    expect(result.current.messages[1].content).toContain('Il punteggio si calcola');
    expect(result.current.messages[1].citations).toHaveLength(1);
  });

  it('accumulates answer text from { token } event shape (Issue #2712)', async () => {
    // Production SSE sends type-7 events as { token: "..." } (chatClient.qaStream yields raw
    // event.data), NOT plain strings. The hook must accumulate that shape or the bubble is empty.
    const tokenObjectEvents = [
      { type: 7, data: { token: 'Il punteggio ' } },
      { type: 7, data: { token: 'si calcola alla fine della partita.' } },
      { type: 4, data: { confidence: 0.79, Citations: [sampleCitation] } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(tokenObjectEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('come si calcola il punteggio?');
    });
    expect(result.current.messages[1].content).toBe(
      'Il punteggio si calcola alla fine della partita.'
    );
    expect(result.current.messages[1].isLowQuality).toBe(false);
  });

  it('surfaces citations from the type-1 Citations-early SSE event when COMPLETE has citations:null (#V)', async () => {
    // Real backend contract (verified on live SSE): citations arrive in the
    // type-1 (CITATIONS) event as { text, source:"PDF:{docId}", page, score },
    // while the type-4 COMPLETE payload carries citations:null. The hook MUST
    // surface the type-1 citations (mapped to the FE Citation shape) or the chat
    // renders zero CitationChip and the source PDF is unreachable.
    const sseRealEvents = [
      { type: 7, data: { token: 'Alla fine ' } },
      { type: 7, data: { token: 'della partita si contano i punti.' } },
      {
        type: 1,
        data: {
          citations: [
            {
              text: 'Alice loses a total of 8 points',
              source: 'PDF:853f6dcc-aaaa',
              page: 6,
              line: 0,
              score: 0.85,
            },
            {
              text: 'tiles directly adjacent',
              source: 'PDF:853f6dcc-aaaa',
              page: 5,
              line: 0,
              score: 0.83,
            },
          ],
        },
      },
      { type: 4, data: { confidence: 0.8, citations: null } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(sseRealEvents) as any);
    const { result } = renderHook(() => useGameChat('azul'));
    await act(async () => {
      await result.current.ask('come si calcolano i punti a fine partita?');
    });
    const agent = result.current.messages[1];
    expect(agent.citations).toHaveLength(2);
    expect(agent.citations?.[0]).toEqual(
      expect.objectContaining({
        documentId: '853f6dcc-aaaa',
        pageNumber: 6,
        snippet: 'Alice loses a total of 8 points',
        relevanceScore: 0.85,
        copyrightTier: 'full',
      })
    );
    expect(agent.isLowQuality).toBe(false);
    expect(agent.outOfContext).toBe(false);
  });

  it('surfaces SP-C region grounding fields (regions/charStart/charEnd) from the type-1 event (#3407)', async () => {
    // The BE Snippet now Full-gates + emits regions[]/charStart/charEnd on the type-1 CITATIONS
    // event; mapSseCitation must forward them to the FE Citation so SP-D can draw the overlay.
    const sseEvents = [
      { type: 7, data: { token: 'Answer.' } },
      {
        type: 1,
        data: {
          citations: [
            {
              text: 'verbatim rule text',
              source: 'PDF:doc-1',
              page: 2,
              line: 0,
              score: 0.9,
              regions: [{ page: 2, x: 0.1, y: 0.2, width: 0.3, height: 0.4 }],
              charStart: 100,
              charEnd: 250,
            },
          ],
        },
      },
      { type: 4, data: { confidence: 0.8, citations: null } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(sseEvents) as any);
    const { result } = renderHook(() => useGameChat('azul'));
    await act(async () => {
      await result.current.ask('come si calcolano i punti?');
    });
    const cite = result.current.messages[1].citations?.[0];
    expect(cite?.regions).toEqual([{ page: 2, x: 0.1, y: 0.2, width: 0.3, height: 0.4 }]);
    expect(cite?.charStart).toBe(100);
    expect(cite?.charEnd).toBe(250);
  });

  it('derives outOfContext=true when no citations + confidence < 0.30', async () => {
    const oocEvents = [
      { type: 7, data: 'Non ho informazioni.' },
      { type: 4, data: { confidence: 0.0, Citations: [] } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(oocEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('tg?');
    });
    expect(result.current.messages[1].outOfContext).toBe(true);
    expect(result.current.messages[1].citations).toBeUndefined();
  });

  it('isLoading transitions correctly during ask', async () => {
    let releaseStream: () => void = () => {};
    const slowStream = async function* () {
      await new Promise<void>(r => {
        releaseStream = r;
      });
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(slowStream() as any);
    const { result } = renderHook(() => useGameChat('wingspan'));

    act(() => {
      void result.current.ask('q');
    });
    await waitFor(() => expect(result.current.isLoading).toBe(true));

    await act(async () => {
      releaseStream();
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it('isError true when stream throws', async () => {
    const errorStream = async function* () {
      throw new Error('500');
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(errorStream() as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      try {
        await result.current.ask('q');
      } catch {
        /* expected */
      }
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  /**
   * #4168 (5g-b): la domanda fermata dalla quota del piano non è un guasto. `ask` non rifiuta —
   * chi lo chiama non gestisce i rifiuti, e l'utente vedrebbe la sua domanda senza risposta e
   * senza un perché — e l'hook dà i numeri del piano, da mostrare nell'avviso.
   */
  it('reports the plan quota instead of failing when the stream stops for the quota', async () => {
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
    const { result } = renderHook(() => useGameChat('wingspan'));

    let rejection: unknown = null;
    await act(async () => {
      try {
        await result.current.ask('q');
      } catch (e) {
        rejection = e;
      }
    });

    expect(rejection).toBeNull();
    expect(result.current.tierLimit).toEqual({ current: 3, max: 3, upgradeUrl: '/pricing' });
    expect(result.current.isError).toBe(false);
    expect(result.current.isLoading).toBe(false);
    // La domanda resta; una risposta dell'agente non c'è.
    expect(result.current.messages.map(m => m.role)).toEqual(['user']);
  });

  /**
   * #4388: il backend tiene aperte al massimo due risposte per utente e rifiuta la terza con 429.
   * Come per la quota, non è un guasto che chi chiama sappia gestire: `ask` non rifiuta, e l'hook
   * dà la frase da mostrare. Prima `isError` diventava vero e il rifiuto risaliva fino a una
   * `void chat.ask(q)` senza catch: la domanda restava senza risposta e senza un perché.
   */
  it('reports a refusal for too many open answers instead of failing', async () => {
    const refused = async function* () {
      throw new QaTooManyRequestsError();
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(refused() as any);
    const { result } = renderHook(() => useGameChat('wingspan'));

    let rejection: unknown = null;
    await act(async () => {
      try {
        await result.current.ask('q');
      } catch (e) {
        rejection = e;
      }
    });

    expect(rejection).toBeNull();
    expect(result.current.refusal).toBe(QA_TOO_MANY_REQUESTS_MESSAGE);
    expect(result.current.isError).toBe(false);
    expect(result.current.isLoading).toBe(false);
    // La domanda resta; una risposta dell'agente non c'è.
    expect(result.current.messages.map(m => m.role)).toEqual(['user']);
  });

  it('forgets the refusal with the next question', async () => {
    const refused = async function* () {
      throw new QaTooManyRequestsError();
      yield happyEvents[2];
    };
    vi.mocked(qaStream)
      .mockReturnValueOnce(refused() as any)
      .mockReturnValueOnce(mockStream(happyEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('prima');
    });
    expect(result.current.refusal).not.toBeNull();

    await act(async () => {
      await result.current.ask('seconda');
    });

    expect(result.current.refusal).toBeNull();
    expect(result.current.messages.at(-1)?.role).toBe('agent');
  });

  it('still fails for another error', async () => {
    const broken = async function* () {
      throw new Error('QA stream failed: HTTP 500');
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(broken() as any);
    const { result } = renderHook(() => useGameChat('wingspan'));

    await act(async () => {
      await expect(result.current.ask('q')).rejects.toThrow('HTTP 500');
    });

    expect(result.current.isError).toBe(true);
    expect(result.current.refusal).toBeNull();
  });

  /**
   * #4332: `ask` rifiuta per un errore vero (sopra), e l'hook dà anche la frase da mostrare:
   * quella tradotta del codice se il backend ne manda uno, altrimenti una generica. Il testo
   * grezzo di un'eccezione di trasporto (`HTTP 500`) non è per l'utente.
   */
  /**
   * Uno stream che si chiude pulito senza `Complete` né `Error` — un proxy che taglia a metà, un backend
   * che cade dopo qualche token. `ask` terminava senza errore e senza messaggio: l'indicatore di scrittura
   * spariva e all'utente non restava niente. È la stessa classe di difetto di #4335, su un altro hook.
   */
  describe('stream closed without a result', () => {
    it('an empty stream is reported as interrupted, not as silence', async () => {
      vi.mocked(qaStream).mockReturnValueOnce(mockStream([]) as any);
      const { result } = renderHook(() => useGameChat('wingspan'));

      await act(async () => {
        await expect(result.current.ask('q')).rejects.toThrow();
      });

      expect(result.current.isError).toBe(true);
      expect(result.current.errorMessage).toBe(
        'La risposta si è interrotta prima della fine, quindi non è completa. Riprova a chiedere.'
      );
    });

    it('tokens without a Complete are reported as interrupted, and no half answer is kept', async () => {
      vi.mocked(qaStream).mockReturnValueOnce(
        mockStream([
          { type: 7, data: 'Cinque ' },
          { type: 7, data: 'tessere ' },
        ]) as any
      );
      const { result } = renderHook(() => useGameChat('wingspan'));

      await act(async () => {
        await expect(result.current.ask('q')).rejects.toThrow();
      });

      expect(result.current.isError).toBe(true);
      expect(result.current.isLoading).toBe(false);
      expect(result.current.messages.map(m => m.role)).toEqual(['user']);
    });

    it('a stream that ends with a Complete is not an error', async () => {
      vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
      const { result } = renderHook(() => useGameChat('wingspan'));

      await act(async () => {
        await result.current.ask('q');
      });

      expect(result.current.isError).toBe(false);
      expect(result.current.errorMessage).toBeNull();
    });

    it('a stream stopped by the plan quota is not reported as interrupted', async () => {
      vi.mocked(qaStream).mockReturnValueOnce(
        mockStream([
          { type: 5, data: { errorCode: 'TIER_LIMIT_EXCEEDED', current: 3, max: 3 } },
        ]) as any
      );
      const { result } = renderHook(() => useGameChat('wingspan'));

      await act(async () => {
        await result.current.ask('q');
      });

      expect(result.current.isError).toBe(false);
      expect(result.current.errorMessage).toBeNull();
    });
  });

  describe('errorMessage (#4332)', () => {
    it('is null before any error', () => {
      const { result } = renderHook(() => useGameChat('wingspan'));

      expect(result.current.errorMessage).toBeNull();
    });

    it('translates the code of an error event', async () => {
      vi.mocked(qaStream).mockReturnValueOnce(
        mockStream([
          { type: 5, data: { errorCode: 'AI_BUDGET_DAILY_EXHAUSTED', message: 'daily budget' } },
        ]) as any
      );
      const { result } = renderHook(() => useGameChat('wingspan'));

      await act(async () => {
        await expect(result.current.ask('q')).rejects.toThrow();
      });

      expect(result.current.isError).toBe(true);
      expect(result.current.errorMessage).toBe(
        'Il servizio AI è in pausa fino a domani: è stato raggiunto il budget di spesa del giorno.'
      );
    });

    it('gives a generic sentence for a transport failure', async () => {
      const broken = async function* () {
        throw new Error('QA stream failed: HTTP 500');
        yield happyEvents[2];
      };
      vi.mocked(qaStream).mockReturnValueOnce(broken() as any);
      const { result } = renderHook(() => useGameChat('wingspan'));

      await act(async () => {
        await expect(result.current.ask('q')).rejects.toThrow('HTTP 500');
      });

      expect(result.current.errorMessage).toBe('La risposta non è arrivata. Riprova tra poco.');
    });

    // Il catch-all degli endpoint di risposta manda `An error occurred: {ex.Message}` con codice
    // INTERNAL_ERROR: il messaggio di un'eccezione interna non è per l'utente.
    it('never exposes the raw backend text of a code it does not know', async () => {
      vi.mocked(qaStream).mockReturnValueOnce(
        mockStream([
          {
            type: 5,
            data: {
              errorCode: 'INTERNAL_ERROR',
              message: 'An error occurred: Npgsql.PostgresException 42P01',
            },
          },
        ]) as any
      );
      const { result } = renderHook(() => useGameChat('wingspan'));

      await act(async () => {
        await expect(result.current.ask('q')).rejects.toThrow();
      });

      expect(result.current.errorMessage).toBe('La risposta non è arrivata. Riprova tra poco.');
    });

    it('logs the real cause, since the user only sees a sentence', async () => {
      const spy = vi.spyOn(logger, 'error').mockImplementation(() => undefined);
      const broken = async function* () {
        throw new Error('QA stream failed: HTTP 500');
        yield happyEvents[2];
      };
      vi.mocked(qaStream).mockReturnValueOnce(broken() as any);
      const { result } = renderHook(() => useGameChat('wingspan'));

      await act(async () => {
        await expect(result.current.ask('q')).rejects.toThrow('HTTP 500');
      });

      expect(spy).toHaveBeenCalledWith(
        expect.stringContaining('la risposta non è arrivata'),
        expect.objectContaining({ message: 'QA stream failed: HTTP 500' })
      );
      spy.mockRestore();
    });

    it('forgets the sentence with the next question', async () => {
      const broken = async function* () {
        throw new Error('boom');
        yield happyEvents[2];
      };
      vi.mocked(qaStream).mockReturnValueOnce(broken() as any);
      const { result } = renderHook(() => useGameChat('wingspan'));
      await act(async () => {
        await expect(result.current.ask('q')).rejects.toThrow('boom');
      });
      expect(result.current.errorMessage).not.toBeNull();

      vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
      await act(async () => {
        await result.current.ask('q2');
      });

      expect(result.current.errorMessage).toBeNull();
      expect(result.current.isError).toBe(false);
    });

    it('is not set by the plan quota or by too many open answers', async () => {
      vi.mocked(qaStream).mockReturnValueOnce(
        mockStream([
          { type: 5, data: { errorCode: 'TIER_LIMIT_EXCEEDED', current: 3, max: 3 } },
        ]) as any
      );
      const { result } = renderHook(() => useGameChat('wingspan'));
      await act(async () => {
        await result.current.ask('q');
      });

      expect(result.current.errorMessage).toBeNull();
    });
  });

  it('forgets the plan quota with the next question', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(
      mockStream([
        { type: 5, data: { errorCode: 'TIER_LIMIT_EXCEEDED', current: 3, max: 3 } },
      ]) as any
    );
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('q');
    });
    expect(result.current.tierLimit).not.toBeNull();

    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    await act(async () => {
      await result.current.ask('q2');
    });

    expect(result.current.tierLimit).toBeNull();
  });

  it('switchAgent updates currentAgent', () => {
    const { result } = renderHook(() => useGameChat('wingspan'));
    act(() => {
      result.current.switchAgent('arbitro');
    });
    expect(result.current.currentAgent).toBe('arbitro');
  });

  it('switchAgent does NOT clear message history', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await act(async () => {
      await result.current.ask('q');
    });
    expect(result.current.messages).toHaveLength(2);
    act(() => {
      result.current.switchAgent('arbitro');
    });
    expect(result.current.messages).toHaveLength(2);
  });

  it('initialAgent overrides default tutor', () => {
    const { result } = renderHook(() => useGameChat('wingspan', 'arbitro'));
    expect(result.current.currentAgent).toBe('arbitro');
  });

  // ─── G2 hydrate tests ───────────────────────────────

  const sampleThreadMessage = (role: string, content: string, ts: string) => ({
    content,
    role,
    timestamp: ts,
  });

  const sampleThread = (id: string, lastMessageAt: string | null, messages: any[] = []) => ({
    id,
    gameId: 'wingspan',
    agentId: null,
    agentType: null,
    title: 'Test thread',
    createdAt: '2026-05-10T00:00:00Z',
    lastMessageAt,
    messageCount: messages.length,
    messages,
  });

  it('hydrates messages from latest thread on mount', async () => {
    const thread = sampleThread('thread-1', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'old question', '2026-05-10T09:00:00Z'),
      sampleThreadMessage('agent', 'old answer', '2026-05-10T09:01:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toHaveLength(2);
    expect(result.current.messages[0].content).toBe('old question');
    expect(result.current.messages[0].isHistorical).toBe(true);
    expect(result.current.chatThreadId).toBe('thread-1');
    expect(result.current.hasHistoricalMessages).toBe(true);
  });

  it('selects most recent thread when multiple exist (lastMessageAt desc)', async () => {
    const older = sampleThread('thread-old', '2026-05-09T10:00:00Z', [
      sampleThreadMessage('user', 'older', '2026-05-09T10:00:00Z'),
    ]);
    const newer = sampleThread('thread-new', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'newer', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([older, newer]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.chatThreadId).toBe('thread-new');
    expect(result.current.messages[0].content).toBe('newer');
  });

  it('starts empty when no threads exist (no error)', async () => {
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toEqual([]);
    expect(result.current.chatThreadId).toBeNull();
    expect(result.current.hasHistoricalMessages).toBe(false);
    expect(result.current.isError).toBe(false);
  });

  it('silent fail when getThreadsByGame rejects', async () => {
    vi.mocked(api.chat.getThreadsByGame).mockRejectedValueOnce(new Error('500'));
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toEqual([]);
    expect(result.current.isError).toBe(false);
  });

  it('passes chatId in subsequent qaStream calls after hydrate', async () => {
    const thread = sampleThread('thread-existing', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'old', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread]);
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);

    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.chatThreadId).toBe('thread-existing'));

    await act(async () => {
      await result.current.ask('new question');
    });

    expect(qaStream).toHaveBeenCalledWith(
      expect.objectContaining({
        chatId: 'thread-existing',
      })
    );
  });

  it('preserves messages across remount (audit trigger A)', async () => {
    const thread = sampleThread('thread-1', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'cached', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValue([thread]);

    const { result, unmount } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.messages.length).toBeGreaterThan(0));

    unmount();

    const { result: result2 } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result2.current.messages.length).toBeGreaterThan(0));
    expect(result2.current.messages[0].content).toBe('cached');
  });
});
