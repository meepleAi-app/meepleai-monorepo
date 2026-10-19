/**
 * Uno stream SSE che consegna un evento alla volta, quando il test lo dice.
 *
 * Serve a chi prova una vista insieme all'hook vero dello stream: fra un evento e l'altro la vista
 * rende, ed è lì che un effetto mal legato può fermare lo stream (#4333). Uno stream che consegna
 * tutto in un colpo non lascia quello spazio.
 */

/** Un evento com'è sul filo: `data: {json}` con il tipo numerico di `StreamingEventType`. */
export function sseEvent(type: number, data: unknown): string {
  return `data: ${JSON.stringify({ type, data, timestamp: new Date().toISOString() })}\n\n`;
}

/**
 * Un passo dello stream: un evento com'è sul filo, oppure un errore. Un errore fa fallire la
 * lettura in corso, come la rete che cade a metà (`reader.read()` lancia), e chiude lo stream:
 * i passi dopo non arrivano più.
 */
export type SseStep = string | Error;

export interface SteppedStream {
  readonly body: ReadableStream<Uint8Array>;
  /**
   * Autorizza la prossima consegna: un evento, oppure — dopo l'ultimo — la chiusura dello stream.
   * Le autorizzazioni si accumulano: chiamarla prima che lo stream sia pronto, o due volte di
   * fila, non ne perde nessuna.
   */
  next(): void;
}

export function steppedStream(events: SseStep[], signal?: AbortSignal | null): SteppedStream {
  const encoder = new TextEncoder();
  let permits = 0;
  let index = 0;
  let wake: (() => void) | null = null;

  const waitForPermit = async (): Promise<void> => {
    while (permits === 0 && !signal?.aborted) {
      await new Promise<void>(resolve => {
        wake = resolve;
      });
    }
    permits = Math.max(0, permits - 1);
  };

  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      // Come la `fetch` vera: fermata la richiesta, la lettura in corso fallisce.
      signal?.addEventListener('abort', () => {
        try {
          controller.error(new DOMException('Aborted', 'AbortError'));
        } catch {
          /* lo stream era già chiuso */
        }
        wake?.();
      });
    },
    async pull(controller) {
      await waitForPermit();
      if (signal?.aborted) return;
      if (index >= events.length) {
        controller.close();
        return;
      }
      const step = events[index];
      index++;
      if (step instanceof Error) {
        controller.error(step);
        return;
      }
      controller.enqueue(encoder.encode(step));
    },
  });

  return {
    body,
    next: () => {
      permits++;
      wake?.();
    },
  };
}

export interface SteppedFetch {
  /** La `fetch` finta da assegnare a `globalThis.fetch`. */
  readonly fetch: typeof fetch;
  /** Quante volte è stata chiamata. */
  calls(): number;
  /** Il segnale passato all'ultima chiamata: dice se lo stream è stato fermato. */
  signal(): AbortSignal | null;
  /** Autorizza la prossima consegna dell'ultima chiamata: un evento, o la chiusura. */
  next(): void;
  /**
   * Quante consegne servono per arrivare in fondo: gli eventi, più la chiusura. Dopo un errore la
   * chiusura non c'è, e le consegne in più non fanno niente.
   */
  readonly steps: number;
}

/** Una `fetch` che risponde con uno stream a passi, e ricorda il segnale di interruzione. */
export function steppedFetch(events: SseStep[]): SteppedFetch {
  let count = 0;
  let lastSignal: AbortSignal | null = null;
  let stream: SteppedStream | null = null;

  const fake = async (_url: unknown, init?: RequestInit): Promise<Response> => {
    count++;
    lastSignal = init?.signal ?? null;
    stream = steppedStream(events, lastSignal);
    return { ok: true, status: 200, body: stream.body } as unknown as Response;
  };

  return {
    fetch: fake as unknown as typeof fetch,
    calls: () => count,
    signal: () => lastSignal,
    next: () => stream?.next(),
    steps: events.length + 1,
  };
}
