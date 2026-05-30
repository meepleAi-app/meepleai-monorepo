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
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-state-error-server')).toBeInTheDocument();
    expect(screen.getByText(/RBAC_RESOLUTION_FAILED/)).toBeInTheDocument();
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
});
