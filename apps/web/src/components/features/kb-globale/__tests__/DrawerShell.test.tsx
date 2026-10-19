import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DrawerShell } from '../DrawerShell';
import type { KbAskStreamState } from '../../../../hooks/useKbAskStream';

const baseState: KbAskStreamState = {
  status: 'idle',
  partialText: '',
  citations: [],
  totalTokens: 0,
  elapsedMs: 0,
  error: null,
  retryCount: 0,
};

const labels = {
  title: 'Ask the Meeple',
  subtitle: 'Knowledge Base',
  closeLabel: 'Chiudi',
  idle: {
    welcomeTitle: 'Chiedimi qualsiasi cosa',
    welcomeBody: 'Cerco nei tuoi PDF.',
    suggestionsLabel: 'Suggerimenti',
    placeholder: 'Chiedi al Meeple…',
    sendLabel: 'Invia',
  },
  streaming: { statusLabel: 'STREAMING', stopLabel: 'Stop streaming' },
  completed: { completedLabel: 'COMPLETED', copyLabel: 'Copy', regenerateLabel: 'Regenerate' },
  empty: {
    title: 'Nessun documento',
    body: 'Carica un PDF per iniziare.',
    cta: 'Vai alla libreria',
  },
  error: {
    connection: { title: 'Connessione persa', body: 'Retry automatico…', action: 'Riprova ora' },
    timeout: { title: 'Risposta lenta', body: '>30s', action: 'Continua attesa', alt: 'Cancella' },
    partial: { title: 'Risposta incompleta', body: 'Stream interrotto', action: 'Ripeti query' },
    server: { title: 'Errore del server', body: 'Riprova', action: 'Riprova' },
    interrupted: {
      title: 'Risposta interrotta',
      body: 'La risposta si è interrotta prima della fine, quindi non è completa. Riprova a chiedere.',
      action: 'Chiedi di nuovo',
      alt: "Fai un'altra domanda",
    },
  },
};

describe('DrawerShell — FSM state rendering', () => {
  it('renders idle: welcome + suggestions + input', () => {
    render(
      <DrawerShell
        state={baseState}
        labels={labels}
        suggestions={['Q1', 'Q2', 'Q3']}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-state-idle')).toBeInTheDocument();
    expect(screen.getByText(/chiedimi qualsiasi cosa/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Q[123]/)).toHaveLength(3);
  });

  it('renders streaming: partial text + stop button', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'streaming',
      partialText: 'La classe Scout…',
      citations: [],
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-state-streaming')).toBeInTheDocument();
    expect(screen.getByText(/la classe scout/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /stop streaming/i })).toBeInTheDocument();
  });

  it('renders completed: full text + citation list (D-F: NUMBERED LIST below answer)', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'completed',
      partialText: 'Risposta completa.',
      citations: [
        { docId: 'd1', source: 'd1', page: 14, snippet: 'cite uno', score: 0.9 },
        { docId: 'd1', source: 'd1', page: 21, snippet: 'cite due', score: 0.8 },
      ],
      totalTokens: 412,
      elapsedMs: 2100,
    };
    const { container } = render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-state-completed')).toBeInTheDocument();
    expect(screen.getByText(/risposta completa/i)).toBeInTheDocument();
    // D-F: 2 citation items in a list, each with index + page + snippet visible
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(2);
    // Index 1 + 2 visible via data-attr selector
    expect(container.querySelector('[data-citation-index="1"]')).toBeInTheDocument();
    expect(container.querySelector('[data-citation-index="2"]')).toBeInTheDocument();
    // Page refs visible
    expect(screen.getByText(/p\.14/)).toBeInTheDocument();
    expect(screen.getByText(/p\.21/)).toBeInTheDocument();
    // Snippets visible
    expect(screen.getByText(/cite uno/i)).toBeInTheDocument();
    expect(screen.getByText(/cite due/i)).toBeInTheDocument();
    // Completion metadata visible
    expect(screen.getByText(/412 tokens/)).toBeInTheDocument();
    expect(screen.getByText(/2\.1s/)).toBeInTheDocument();
    expect(screen.getByText(/2 citations/)).toBeInTheDocument();
  });

  it('renders completed-empty: dedicated empty state with CTA', async () => {
    const state: KbAskStreamState = { ...baseState, status: 'completed-empty' };
    const onEmptyCta = vi.fn();
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={onEmptyCta}
      />
    );
    expect(screen.getByTestId('drawer-state-empty')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /vai alla libreria/i }));
    expect(onEmptyCta).toHaveBeenCalledOnce();
  });

  it('renders error[connection]: countdown + auto-retry hint', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      error: { kind: 'connection', message: 'lost' },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-state-error-connection')).toBeInTheDocument();
    expect(screen.getByText(/connessione persa/i)).toBeInTheDocument();
  });

  it('renders error[timeout]: continue + cancel actions', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      error: { kind: 'timeout', message: 'slow' },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-state-error-timeout')).toBeInTheDocument();
    expect(screen.getByText(/cancella/i)).toBeInTheDocument();
  });

  it('renders error[partial]: shows accumulated partial text + [stream interrotto]', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      partialText: 'La classe Scout inizia con',
      error: { kind: 'partial', message: 'LLM crashed' },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-state-error-partial')).toBeInTheDocument();
    expect(screen.getByText(/la classe scout inizia con/i)).toBeInTheDocument();
  });

  it('renders error[server]: shows code', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      error: { kind: 'server', message: 'RBAC failed', code: 'RBAC_RESOLUTION_FAILED' },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-state-error-server')).toBeInTheDocument();
    expect(screen.getByText(/RBAC_RESOLUTION_FAILED/)).toBeInTheDocument();
    // Un guasto si può riprovare.
    expect(screen.getByRole('button', { name: /Riprova/ })).toBeInTheDocument();
  });

  /**
   * #4510: la risposta si è interrotta prima della fine. La parte arrivata resta, segnata come
   * incompleta, e il riquadro dice perché, con la frase della chat. Testo piccolo su `bg-muted`:
   * `text-foreground`, non `text-muted-foreground` (4,43:1 in tema scuro, sotto AA).
   */
  it('renders error[interrupted]: keeps the words that arrived, marked as incomplete, and says why', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      partialText: 'La classe Scout inizia con',
      error: { kind: 'interrupted', message: labels.error.interrupted.body },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    expect(screen.getByTestId('drawer-state-error-interrupted')).toBeInTheDocument();
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Risposta interrotta');
    expect(alert).toHaveTextContent(
      'La risposta si è interrotta prima della fine, quindi non è completa. Riprova a chiedere.'
    );

    const partial = screen.getByTestId('drawer-error-partial-text');
    expect(partial).toHaveTextContent('La classe Scout inizia con');
    expect(partial).toHaveClass('text-foreground');
    expect(partial).not.toHaveClass('text-muted-foreground');
    const mark = screen.getByText('Risposta interrotta: non è completa.');
    expect(mark).toHaveClass('text-foreground');
  });

  it('renders error[interrupted] without words: only the reason', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      error: { kind: 'interrupted', message: labels.error.interrupted.body },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    expect(screen.getByRole('alert')).toHaveTextContent('Risposta interrotta');
    expect(screen.queryByTestId('drawer-error-partial-text')).not.toBeInTheDocument();
  });

  /**
   * #4510: «Chiedi di nuovo» rifà la stessa domanda (`onRetry`). Prima il pulsante chiamava
   * `onAsk(state.partialText)`: mandava come domanda il pezzo di risposta arrivato, o una domanda
   * vuota, che il backend rifiuta.
   */
  it('asks the same question again through onRetry, not the partial answer through onAsk', async () => {
    const onAsk = vi.fn();
    const onRetry = vi.fn();
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      partialText: 'La classe Scout inizia con',
      error: { kind: 'interrupted', message: labels.error.interrupted.body },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={onAsk}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={onRetry}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    await userEvent.click(screen.getByRole('button', { name: /Chiedi di nuovo/ }));

    expect(onRetry).toHaveBeenCalledTimes(1);
    expect(onAsk).not.toHaveBeenCalled();
  });

  it('lets the user leave an interrupted answer and ask another question', async () => {
    const onReset = vi.fn();
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      partialText: 'La classe Scout inizia con',
      error: { kind: 'interrupted', message: labels.error.interrupted.body },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={onReset}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    await userEvent.click(screen.getByRole('button', { name: "Fai un'altra domanda" }));

    expect(onReset).toHaveBeenCalledTimes(1);
  });

  /** Il riquadro d'errore, per ogni tipo: testo leggibile (AA) in entrambi i temi. */
  it('writes the error title and body with text-foreground', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      error: { kind: 'server', message: 'RBAC failed', code: 'RBAC_RESOLUTION_FAILED' },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    expect(screen.getByText('Errore del server')).toHaveClass('text-foreground');
    expect(screen.getByText('Riprova', { selector: 'div' })).toHaveClass('text-foreground');
    expect(screen.getByRole('button', { name: /Riprova/ })).toHaveClass(
      'bg-primary',
      'text-primary-foreground'
    );
  });

  /**
   * #4168 (5g-b): la quota del piano non è un guasto. Niente riquadro d'errore, niente codice in
   * chiaro, e soprattutto niente «Riprova»: riproverebbe la stessa domanda, fermata allo stesso
   * modo fino a domani. Al suo posto l'avviso della chat, con i numeri del piano e il link ai piani.
   */
  it('renders the plan quota as a notice with the way to the plans, not as a server error', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      error: {
        kind: 'server',
        message: 'Tier limit exceeded: AgentQuery (3/3)',
        code: 'TIER_LIMIT_EXCEEDED',
        tierLimit: { current: 3, max: 3, upgradeUrl: '/pricing' },
      },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    const notice = screen.getByTestId('tier-limit-exceeded');
    expect(notice).toHaveTextContent(
      'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3)'
    );
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
    expect(screen.queryByTestId('drawer-state-error-server')).not.toBeInTheDocument();
    expect(screen.queryByText(/TIER_LIMIT_EXCEEDED/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Tier limit exceeded/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Riprova/ })).not.toBeInTheDocument();
  });

  /** Senza i numeri (un backend che non li manda) l'avviso c'è lo stesso, e porta ai piani. */
  it('renders the plan quota notice even when the numbers are missing', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      error: { kind: 'server', message: 'Tier limit exceeded', code: 'TIER_LIMIT_EXCEEDED' },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    expect(screen.getByTestId('tier-limit-exceeded')).toHaveTextContent(
      'Hai usato tutte le domande di oggi previste dal tuo piano.'
    );
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
  });

  /**
   * Il pannello resta montato quando lo si chiude: senza un modo di lasciare l'avviso, riaprirlo
   * lo rimostrerebbe, anche il giorno dopo. «Ho capito» riporta il pannello alla domanda.
   */
  it('lets the user leave the plan quota notice and go back to the question', async () => {
    const onReset = vi.fn();
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      error: {
        kind: 'server',
        message: 'Tier limit exceeded: AgentQuery (3/3)',
        code: 'TIER_LIMIT_EXCEEDED',
        tierLimit: { current: 3, max: 3, upgradeUrl: '/pricing' },
      },
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={onReset}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    await userEvent.click(screen.getByRole('button', { name: 'Ho capito' }));

    expect(onReset).toHaveBeenCalledTimes(1);
  });

  /**
   * Il posto dell'avviso è nel pannello da prima, vuoto: l'avviso arriva dopo l'invio senza che il
   * fuoco si sposti, e una regione viva montata già piena non viene annunciata da tutti gli screen
   * reader.
   */
  it('keeps the place for the plan quota notice in the drawer, empty', () => {
    render(
      <DrawerShell
        state={baseState}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    const region = screen.getByTestId('tier-limit-region');
    expect(region).toHaveAttribute('role', 'status');
    expect(region).toBeEmptyDOMElement();
    expect(screen.queryByRole('button', { name: 'Ho capito' })).not.toBeInTheDocument();
  });

  it('calls onClose when header close pressed (all states)', async () => {
    const onClose = vi.fn();
    render(
      <DrawerShell
        state={baseState}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={onClose}
        onEmptyCta={vi.fn()}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onAsk(query) from idle when suggestion clicked', async () => {
    const onAsk = vi.fn();
    render(
      <DrawerShell
        state={baseState}
        labels={labels}
        suggestions={['How to play?']}
        onAsk={onAsk}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: /how to play/i }));
    expect(onAsk).toHaveBeenCalledWith('How to play?');
  });

  it('calls onStop from streaming state', async () => {
    const onStop = vi.fn();
    const state: KbAskStreamState = { ...baseState, status: 'streaming', partialText: 'a' };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={onStop}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: /stop streaming/i }));
    expect(onStop).toHaveBeenCalledOnce();
  });

  it('renders completed: inline pills when text contains [N] markers (D-1703-D inline path)', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'completed',
      partialText: 'La classe Scout ha 3 abilità [1][2]. Perks parte da [3].',
      citations: [
        { docId: 'd1', source: 'd1', page: 14, snippet: 'cite uno', score: 0.9 },
        { docId: 'd1', source: 'd1', page: 14, snippet: 'cite due', score: 0.8 },
        { docId: 'd2', source: 'd2', page: 21, snippet: 'cite tre', score: 0.7 },
      ],
      totalTokens: 412,
      elapsedMs: 2100,
    };
    const { container } = render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    expect(screen.getByTestId('drawer-state-completed')).toBeInTheDocument();
    expect(screen.getByTestId('drawer-completed-answer')).toHaveAttribute(
      'data-render-mode',
      'inline'
    );
    const pills = container.querySelectorAll('[data-slot="kb-globale-citation-pill"]');
    expect(pills).toHaveLength(3);
    // Fallback list MUST NOT be present (UX safety invariant)
    expect(screen.queryByTestId('citation-list')).toBeNull();
  });

  it('renders completed: fallback list when text has no [N] markers (D-F fallback preserved)', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'completed',
      partialText: 'Risposta semplice senza marker.',
      citations: [{ docId: 'd1', source: 'd1', page: 14, snippet: 'cite uno', score: 0.9 }],
      totalTokens: 100,
      elapsedMs: 500,
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );

    expect(screen.getByTestId('drawer-completed-answer')).toHaveAttribute(
      'data-render-mode',
      'fallback'
    );
    // The numbered list IS present in the fallback path
    expect(screen.getByTestId('citation-list')).toBeInTheDocument();
    expect(screen.getByText(/cite uno/i)).toBeInTheDocument();
  });

  it('forwards onCitationClick from DrawerCompleted props to inline pills (#1702 wiring fix)', async () => {
    const onCitationClick = vi.fn();
    const state: KbAskStreamState = {
      ...baseState,
      status: 'completed',
      partialText: 'La classe Scout [1].',
      citations: [
        {
          docId: 'doc-1',
          source: 'doc-1',
          page: 14,
          snippet: 'cite uno',
          score: 0.9,
          chunkId: 'doc-1_3',
        },
      ],
      totalTokens: 100,
      elapsedMs: 500,
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onRetry={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
        onCitationClick={onCitationClick}
      />
    );

    const pill = screen.getByRole('button', { name: /citation 1/i });
    await userEvent.click(pill);
    expect(onCitationClick).toHaveBeenCalledWith({
      docId: 'doc-1',
      page: 14,
      chunkId: 'doc-1_3',
    });
  });
});
