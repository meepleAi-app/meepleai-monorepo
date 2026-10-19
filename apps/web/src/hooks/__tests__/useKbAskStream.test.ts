import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { sseEvent, steppedFetch, type SteppedFetch } from '@/test-utils/sse-stepped-stream';
import { useKbAskStream } from '../useKbAskStream';
import { kbAskClient } from '../../lib/api/clients/kbAskClient';
import { KbAskEventSchema, type KbAskEvent } from '../../lib/api/schemas/kb-ask.schemas';

async function* eventsAsAsyncGen(events: KbAskEvent[]): AsyncGenerator<KbAskEvent> {
  for (const e of events) yield e;
}

// ─── #4510: la rete finta a passi, col client vero ───────────────────────────
// Fra un evento e l'altro l'hook rende, come nel browser. I tipi sono quelli di
// `StreamingEventType` sul filo.

const T = { StateUpdate: 0, Citations: 1, Complete: 4, Error: 5, Token: 7 } as const;

const COMPLETE = {
  totalTokens: 2,
  promptTokens: 5,
  completionTokens: 2,
  estimatedReadingTimeMinutes: 0,
  confidence: null,
};

/** La frase della chat per una risposta interrotta (`qaStreamErrors`, `AI_STREAM_INTERRUPTED`). */
const INTERRUPTED =
  'La risposta si è interrotta prima della fine, quindi non è completa. Riprova a chiedere.';

/** Consegna un passo (un evento, la chiusura o un guasto) e lascia all'hook il tempo di rendere. */
async function deliver(next: () => void) {
  await act(async () => {
    next();
    await new Promise(resolve => setTimeout(resolve, 10));
  });
}

async function runToTheEnd(wire: SteppedFetch) {
  for (let i = 0; i < wire.steps; i++) await deliver(wire.next);
}

describe('useKbAskStream — initial state', () => {
  it('starts in idle with empty citations + no error', () => {
    const { result } = renderHook(() => useKbAskStream());
    expect(result.current.state.status).toBe('idle');
    expect(result.current.state.partialText).toBe('');
    expect(result.current.state.citations).toEqual([]);
    expect(result.current.state.error).toBeNull();
    expect(result.current.state.retryCount).toBe(0);
  });
});

describe('useKbAskStream — transitions', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /**
   * #4510: lo stream resta aperto. Prima questo test usava uno stream che finiva dopo il primo
   * evento, e fissava come «in corso» una risposta che si era già chiusa senza esito: proprio il
   * difetto. Quella chiusura ora è un'interruzione (sotto, «a stream that closes…»).
   */
  it('idle → streaming on ask()', async () => {
    const wire = steppedFetch([sseEvent(T.StateUpdate, { message: 'Ricerca…' })]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('scout abilities');
    });
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await deliver(wire.next);

    expect(result.current.state.status).toBe('streaming');
    expect(result.current.state.error).toBeNull();
  });

  it('accumulates tokens into partialText', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'La ' } },
        { type: 7, data: { token: 'classe.' } },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.partialText).toBe('La classe.'));
  });

  it('stores citations from Citations event (D-E: page-level)', async () => {
    const cite = { docId: 'd', source: 'd', page: 14, snippet: 't', score: 0.9 };
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([{ type: 1, data: { citations: [cite] } }])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.citations).toEqual([cite]));
  });

  it('streaming → completed on Complete event with tokens', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'a' } },
        {
          type: 4,
          data: {
            totalTokens: 1,
            promptTokens: 5,
            completionTokens: 1,
            estimatedReadingTimeMinutes: 0,
            confidence: null,
          },
        },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.status).toBe('completed'));
    expect(result.current.state.totalTokens).toBe(1);
  });

  it('streaming → completed-empty when Complete arrives with totalTokens=0 and no prior Citations (D-L Adzic)', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 0, data: { message: 'Ricerca nella tua libreria...' } },
        {
          type: 4,
          data: {
            totalTokens: 0,
            promptTokens: 0,
            completionTokens: 0,
            estimatedReadingTimeMinutes: 0,
            confidence: null,
          },
        },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.status).toBe('completed-empty'));
  });

  it('Error event with server-side code maps to kind=server (D-L Nygard)', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 5, data: { message: 'RBAC failed', code: 'RBAC_RESOLUTION_FAILED' } },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.status).toBe('error'));
    expect(result.current.state.error?.kind).toBe('server');
    expect(result.current.state.error?.code).toBe('RBAC_RESOLUTION_FAILED');
  });

  /**
   * #4168 (5g-b): la domanda fermata dalla quota del piano. L'errore porta i numeri e il link,
   * così il pannello può dire che cosa è successo invece di «errore del server».
   */
  it('Error event for the plan quota carries the numbers of the plan', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        {
          type: 5,
          data: {
            message: 'Tier limit exceeded: AgentQuery (3/3)',
            code: 'TIER_LIMIT_EXCEEDED',
            tierLimit: { current: 3, max: 3, upgradeUrl: '/pricing' },
          },
        },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.status).toBe('error'));
    expect(result.current.state.error).toEqual({
      kind: 'server',
      message: 'Tier limit exceeded: AgentQuery (3/3)',
      code: 'TIER_LIMIT_EXCEEDED',
      tierLimit: { current: 3, max: 3, upgradeUrl: '/pricing' },
    });
  });

  /**
   * La giunzione fra lo schema e l'hook: l'evento com'è sul filo, passato dallo schema come fa il
   * client, arriva allo stato con i numeri. Se lo schema smettesse di conservarli, qui si vede.
   */
  it('the plan quota survives from the wire to the state', async () => {
    const onTheWire = {
      type: 5,
      data: {
        errorMessage: 'Tier limit exceeded: AgentQuery (3/3)',
        errorCode: 'TIER_LIMIT_EXCEEDED',
        limitType: 'AgentQuery',
        current: 3,
        max: 3,
        upgradeUrl: '/pricing',
      },
    };
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([KbAskEventSchema.parse(onTheWire)])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.status).toBe('error'));
    expect(result.current.state.error?.code).toBe('TIER_LIMIT_EXCEEDED');
    expect(result.current.state.error?.tierLimit).toEqual({
      current: 3,
      max: 3,
      upgradeUrl: '/pricing',
    });
  });

  it('Error event with LLM_STREAMING_FAILED + accumulated partial text maps to kind=partial', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'La classe Scout inizia…' } },
        { type: 5, data: { message: 'LLM crashed', code: 'LLM_STREAMING_FAILED' } },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.error?.kind).toBe('partial'));
    expect(result.current.state.partialText).toBe('La classe Scout inizia…');
  });

  it('Network throw maps to kind=connection', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() => {
      throw new TypeError('Failed to fetch');
    });
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.error?.kind).toBe('connection'));
  });

  /** #4510: lo stream resta aperto finché l'utente lo ferma; fermarlo non è un'interruzione. */
  it('stop() aborts mid-stream and transitions to idle', async () => {
    const wire = steppedFetch([
      sseEvent(T.Token, { token: 'a' }),
      sseEvent(T.Token, { token: 'b' }),
    ]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await deliver(wire.next);
    expect(result.current.state.status).toBe('streaming');

    act(() => {
      result.current.stop();
    });
    // Anche dopo che il trasporto ha visto la fine, resta ferma: niente errore.
    await deliver(wire.next);
    expect(wire.signal()?.aborted).toBe(true);
    expect(result.current.state.status).toBe('idle');
    expect(result.current.state.error).toBeNull();
  });

  it('reset() returns to idle from completed', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        {
          type: 4,
          data: {
            totalTokens: 1,
            promptTokens: 1,
            completionTokens: 1,
            estimatedReadingTimeMinutes: 0,
            confidence: null,
          },
        },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });
    await waitFor(() => expect(result.current.state.status).toBe('completed'));

    act(() => {
      result.current.reset();
    });
    expect(result.current.state.status).toBe('idle');
    expect(result.current.state.partialText).toBe('');
    expect(result.current.state.citations).toEqual([]);
  });

  it('ask() with topK and language forwards options to client', async () => {
    const spy = vi
      .spyOn(kbAskClient, 'askGlobal')
      .mockImplementation(() => eventsAsAsyncGen([{ type: 0, data: { message: 'ok' } }]));
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q', { language: 'en', topK: 12 });
    });

    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        { query: 'q', language: 'en', topK: 12 },
        expect.any(AbortSignal)
      )
    );
  });

  it('retries on connection error with exponential backoff (1s, 3s, 9s)', async () => {
    vi.useFakeTimers();
    let attempt = 0;
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() => {
      attempt++;
      if (attempt < 3) throw new TypeError('Failed to fetch');
      return eventsAsAsyncGen([
        {
          type: 4,
          data: {
            totalTokens: 1,
            promptTokens: 1,
            completionTokens: 1,
            estimatedReadingTimeMinutes: 0,
            confidence: null,
          },
        },
      ]);
    });
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });
    await vi.runAllTimersAsync();

    expect(result.current.state.retryCount).toBeGreaterThanOrEqual(2);
    // #4510: il tentativo è riuscito, quindi la risposta è completa e l'errore dei tentativi
    // falliti non c'è più. Prima restava `status: 'error'`.
    expect(result.current.state.status).toBe('completed');
    expect(result.current.state.error).toBeNull();
    vi.useRealTimers();
  });

  it('retries exactly 3 times then surfaces final error on persistent connection failure', async () => {
    vi.useFakeTimers();
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() => {
      throw new TypeError('Failed to fetch');
    });

    const { result } = renderHook(() => useKbAskStream());
    act(() => {
      result.current.ask('q');
    });
    await vi.runAllTimersAsync();

    expect(result.current.state.retryCount).toBe(3);
    expect(result.current.state.error?.kind).toBe('connection');
    vi.useRealTimers();
  });
});

/**
 * #4510: la risposta si chiude senza `Complete` né `Error` — il backend o un proxy chiudono la
 * connessione in modo pulito a metà. Prima lo stato restava `streaming`, senza errore: il pannello
 * della knowledge base mostrava «in corso» per sempre. Stessa regola della chat (#4335, PR #4509).
 */
describe('useKbAskStream — a stream that closes without Complete or Error (#4510)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('ends the answer as interrupted, and keeps what arrived', async () => {
    const cite = { docId: 'd1', source: 'd1', page: 4, snippet: 'Cinque tessere.', score: 0.9 };
    const wire = steppedFetch([
      sseEvent(T.Citations, { citations: [cite] }),
      sseEvent(T.Token, { token: 'La ' }),
    ]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await runToTheEnd(wire);

    expect(result.current.state.status).toBe('error');
    expect(result.current.state.error).toEqual({ kind: 'interrupted', message: INTERRUPTED });
    // La parte arrivata resta: la vista la mostra segnata come incompleta.
    expect(result.current.state.partialText).toBe('La ');
    expect(result.current.state.citations).toEqual([cite]);
    // E non riparte da sola: la domanda la rifà l'utente, se vuole.
    expect(wire.calls()).toBe(1);
  });

  it('says the same when it closes before any word of the answer', async () => {
    const wire = steppedFetch([sseEvent(T.StateUpdate, { message: 'Ricerca…' })]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await runToTheEnd(wire);

    expect(result.current.state.status).toBe('error');
    expect(result.current.state.error).toEqual({ kind: 'interrupted', message: INTERRUPTED });
    expect(result.current.state.partialText).toBe('');
    expect(wire.calls()).toBe(1);
  });

  /** Il controllo: la chiusura che segue `Complete` è la fine normale. */
  it('treats the close that follows Complete as the normal end', async () => {
    const wire = steppedFetch([
      sseEvent(T.Token, { token: 'Cinque tessere.' }),
      sseEvent(T.Complete, COMPLETE),
    ]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await runToTheEnd(wire);

    expect(result.current.state.status).toBe('completed');
    expect(result.current.state.error).toBeNull();
    expect(result.current.state.partialText).toBe('Cinque tessere.');
  });

  /** Review di #4509: dopo l'evento conclusivo la risposta è chiusa. Un guasto lì non la rompe. */
  it('leaves a complete answer alone when the connection drops after Complete', async () => {
    const wire = steppedFetch([
      sseEvent(T.Token, { token: 'Cinque tessere.' }),
      sseEvent(T.Complete, COMPLETE),
      new TypeError('network error'),
    ]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await runToTheEnd(wire);

    expect(result.current.state.status).toBe('completed');
    expect(result.current.state.error).toBeNull();
    expect(result.current.state.retryCount).toBe(0);
    expect(result.current.state.partialText).toBe('Cinque tessere.');
    expect(wire.calls()).toBe(1);
  });

  it('leaves a complete answer alone when an Error event follows Complete', async () => {
    const wire = steppedFetch([
      sseEvent(T.Token, { token: 'Cinque tessere.' }),
      sseEvent(T.Complete, COMPLETE),
      sseEvent(T.Error, {
        errorMessage: 'An internal error occurred.',
        errorCode: 'INTERNAL_ERROR',
      }),
    ]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await runToTheEnd(wire);

    expect(result.current.state.status).toBe('completed');
    expect(result.current.state.error).toBeNull();
  });

  /**
   * Il backend dice da sé che la risposta si è interrotta (`AI_STREAM_INTERRUPTED`, dal gateway
   * LLM dopo il primo token): per chi legge è la stessa cosa, con la stessa frase della chat.
   */
  it("reads the backend's own AI_STREAM_INTERRUPTED as the same interruption", async () => {
    const wire = steppedFetch([
      sseEvent(T.Token, { token: 'La ' }),
      sseEvent(T.Error, {
        errorMessage: 'The answer stopped before its end.',
        errorCode: 'AI_STREAM_INTERRUPTED',
      }),
    ]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await runToTheEnd(wire);

    expect(result.current.state.status).toBe('error');
    expect(result.current.state.error).toEqual({
      kind: 'interrupted',
      message: INTERRUPTED,
      code: 'AI_STREAM_INTERRUPTED',
    });
    expect(result.current.state.partialText).toBe('La ');
  });
});

/**
 * #4510: il nuovo tentativo riparte dallo stato accumulato dell'automa, non da zero. Misurato in
 * PR #4509 con una sonda: un tentativo dopo un token raddoppiava il testo (`"La La "`), e a
 * tentativo riuscito lo stato restava `status: 'error'`.
 */
describe('useKbAskStream — retries (#4510)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('does not retry once words have arrived, so the text is not doubled', async () => {
    const wire = steppedFetch([
      sseEvent(T.Token, { token: 'La ' }),
      new TypeError('network error'),
    ]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await runToTheEnd(wire);
    // Oltre il primo intervallo di attesa (1 s): se un tentativo fosse in coda, partirebbe qui.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1500);
    });
    // E, se fosse partito, gli diamo modo di consegnare.
    await runToTheEnd(wire);

    expect(result.current.state.partialText).toBe('La ');
    expect(wire.calls()).toBe(1);
    expect(result.current.state.status).toBe('error');
    expect(result.current.state.error).toEqual({ kind: 'interrupted', message: INTERRUPTED });
    expect(result.current.state.retryCount).toBe(0);
  });

  /** An `Error` event concludes the answer as much as `Complete`: nothing after it is retried. */
  it('does not retry when the connection drops after an Error event', async () => {
    const wire = steppedFetch([
      sseEvent(T.Error, { errorMessage: 'RBAC failed', errorCode: 'RBAC_RESOLUTION_FAILED' }),
      new TypeError('network error'),
    ]);
    vi.spyOn(globalThis, 'fetch').mockImplementation(wire.fetch);
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    await runToTheEnd(wire);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1500);
    });

    expect(wire.calls()).toBe(1);
    expect(result.current.state.retryCount).toBe(0);
    expect(result.current.state.error?.code).toBe('RBAC_RESOLUTION_FAILED');
  });

  it('shows the connection error while the retry waits, and drops it once the retry answers', async () => {
    const wire = steppedFetch([
      sseEvent(T.StateUpdate, { message: 'Ricerca…' }),
      sseEvent(T.Token, { token: 'Cinque tessere.' }),
      sseEvent(T.Complete, COMPLETE),
    ]);
    let fetchCalls = 0;
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      fetchCalls++;
      if (fetchCalls === 1) return Promise.reject(new TypeError('Failed to fetch'));
      return wire.fetch(input, init);
    });
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?'));
    // Il primo tentativo non si connette: il tentativo dopo è in attesa, e lo si dice.
    await vi.waitFor(() => expect(result.current.state.retryCount).toBe(1));
    expect(result.current.state.status).toBe('error');
    expect(result.current.state.error?.kind).toBe('connection');

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    await vi.waitFor(() => expect(wire.calls()).toBe(1));
    // Il nuovo tentativo risponde: l'errore di prima non vale più.
    await deliver(wire.next);
    expect(result.current.state.status).toBe('streaming');
    expect(result.current.state.error).toBeNull();

    await deliver(wire.next);
    await deliver(wire.next);
    await deliver(wire.next);

    expect(result.current.state.status).toBe('completed');
    expect(result.current.state.error).toBeNull();
    expect(result.current.state.partialText).toBe('Cinque tessere.');
    expect(result.current.state.retryCount).toBe(1);
  });
});

/**
 * #4510: «Riprova a chiedere». La vista rifà la stessa domanda con `retry()`: prima il pannello
 * mandava come domanda il testo parziale della risposta (o una domanda vuota).
 */
describe('useKbAskStream — retry() (#4510)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('asks the same question again, from scratch', async () => {
    const first = steppedFetch([sseEvent(T.Token, { token: 'La ' })]);
    const second = steppedFetch([
      sseEvent(T.Token, { token: 'Cinque tessere.' }),
      sseEvent(T.Complete, COMPLETE),
    ]);
    const bodies: unknown[] = [];
    let current: SteppedFetch = first;
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      bodies.push(JSON.parse(String(init?.body)));
      return current.fetch(input, init);
    });
    const { result } = renderHook(() => useKbAskStream());

    act(() => result.current.ask('Quante tessere?', { language: 'it' }));
    await vi.waitFor(() => expect(first.calls()).toBe(1));
    await runToTheEnd(first);
    expect(result.current.state.error?.kind).toBe('interrupted');

    current = second;
    act(() => result.current.retry());
    await vi.waitFor(() => expect(second.calls()).toBe(1));
    // Riparte pulita: l'avviso era della risposta di prima.
    expect(result.current.state.error).toBeNull();
    await runToTheEnd(second);

    expect(bodies).toEqual([
      { query: 'Quante tessere?', language: 'it' },
      { query: 'Quante tessere?', language: 'it' },
    ]);
    expect(result.current.state.status).toBe('completed');
    expect(result.current.state.partialText).toBe('Cinque tessere.');
  });
});
