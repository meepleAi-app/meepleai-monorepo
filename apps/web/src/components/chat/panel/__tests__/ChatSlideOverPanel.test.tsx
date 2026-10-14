import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { useChatPanelStore } from '@/lib/stores/chat-panel-store';

// Mock the data hooks so the panel renders synchronously with empty lists
// #4187: spie sugli argomenti, per verificare che a pannello chiuso le query non partano.
const hookArgs = vi.hoisted(() => ({
  recent: [] as unknown[][],
  library: [] as unknown[][],
}));

vi.mock('@/hooks/queries/useChatSessions', () => ({
  useRecentChatSessions: (...args: unknown[]) => (
    hookArgs.recent.push(args),
    {
      data: { sessions: [], totalCount: 0 },
      isLoading: false,
      error: null,
    }
  ),
}));

// #4187: la tendina del contesto offre la libreria dell'utente, cercata sul server.
const LIBRARY_ITEMS = [
  {
    gameId: 'wingspan',
    gameTitle: 'Wingspan',
    gameYearPublished: 2019,
    gameImageUrl: null,
    gameIconUrl: null,
    hasKb: true,
  },
  {
    gameId: 'senza-kb',
    gameTitle: 'Gioco senza KB',
    gameYearPublished: null,
    gameImageUrl: null,
    gameIconUrl: null,
    hasKb: false,
  },
];
let libraryTotal = LIBRARY_ITEMS.length;
// #4227: con `hasKb: true` il server filtra, e il totale e' quello dei giochi con KB.
let kbTotal = LIBRARY_ITEMS.filter(item => item.hasKb).length;
vi.mock('@/hooks/queries/useLibrarySearch', () => ({
  useLibrarySearch: (...args: unknown[]) => {
    hookArgs.library.push(args);
    const onlyKb = (args[1] as { hasKb?: boolean } | undefined)?.hasKb === true;
    return {
      items: onlyKb ? LIBRARY_ITEMS.filter(item => item.hasKb) : LIBRARY_ITEMS,
      totalCount: onlyKb ? kbTotal : libraryTotal,
      isLoading: false,
      resultsMatchQuery: true,
      term: String(args[0] ?? ''),
    };
  },
}));

// Mock the streaming hook to surface a stable, inert state
const streamResetMock = vi.fn();
const streamSendMock = vi.fn();
// #4168 (5g-b): ciò che lo stream dice di una domanda fermata. Vuoto, finché un test non lo riempie.
const streamOutcome = vi.hoisted(() => ({
  error: null as string | null,
  tierLimit: null as { current?: number; max?: number; upgradeUrl: string } | null,
}));
vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: () => ({
    state: {
      statusMessage: null,
      currentAnswer: '',
      followUpQuestions: [],
      isStreaming: false,
      error: streamOutcome.error,
      tierLimit: streamOutcome.tierLimit,
      chatThreadId: null,
      totalTokens: 0,
      debugSteps: [],
      modelDowngrade: null,
      strategyTier: null,
      executionId: null,
      connectionStatus: 'idle',
      retryCount: 0,
    },
    sendMessage: streamSendMock,
    reset: streamResetMock,
    stopStreaming: vi.fn(),
  }),
}));

// Stub the chat client — the panel only calls getThreadById directly from
// inside handleSelectChat, which none of these tests exercise.
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadById: vi.fn().mockResolvedValue(null),
    },
  },
}));

import { ChatSlideOverPanel } from '../ChatSlideOverPanel';

describe('ChatSlideOverPanel', () => {
  beforeEach(() => {
    libraryTotal = LIBRARY_ITEMS.length;
    kbTotal = LIBRARY_ITEMS.filter(item => item.hasKb).length;
    useChatPanelStore.getState().close();
    useChatPanelStore.getState().clearGameContext();
    streamResetMock.mockClear();
    streamSendMock.mockClear();
    streamOutcome.error = null;
    streamOutcome.tierLimit = null;
  });

  /**
   * #4168 (5g-b): da questo pannello passa anche la chat dei giochi privati. Una domanda fermata
   * dalla quota del piano mostra l'avviso con il link ai piani, non il riquadro d'errore.
   */
  it('mostra la quota del piano come un avviso con il link ai piani, non come un errore', () => {
    streamOutcome.error =
      'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3). Il conteggio riparte alle 02:00 (mezzanotte UTC).';
    streamOutcome.tierLimit = { current: 3, max: 3, upgradeUrl: '/pricing' };
    useChatPanelStore.getState().open();

    render(<ChatSlideOverPanel />);

    expect(screen.getByTestId('tier-limit-exceeded')).toHaveTextContent('3 su 3');
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
    expect(screen.queryByTestId('chat-stream-error')).not.toBeInTheDocument();
  });

  it('renders nothing when panel is closed', () => {
    const { container } = render(<ChatSlideOverPanel />);
    expect(container).toBeEmptyDOMElement();
  });

  // #4187: il pannello è montato su ogni pagina autenticata (DesktopShell). A pannello chiuso le
  // sue query non devono partire: prima scaricavano 50 giochi e 50 sessioni a ogni pagina.
  it('does not run its queries while closed, and runs them once opened', () => {
    hookArgs.recent.length = 0;
    hookArgs.library.length = 0;
    const { rerender } = render(<ChatSlideOverPanel />);
    expect(hookArgs.recent.at(-1)?.[1]).toBe(false);
    expect((hookArgs.library.at(-1)?.[1] as { enabled?: boolean })?.enabled).toBe(false);

    act(() => useChatPanelStore.getState().open());
    rerender(<ChatSlideOverPanel />);
    expect(hookArgs.recent.at(-1)?.[1]).toBe(true);
    expect((hookArgs.library.at(-1)?.[1] as { enabled?: boolean })?.enabled).toBe(true);
  });

  it('renders panel when open', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);
    expect(screen.getByText(/Chat con l'agente/i)).toBeInTheDocument();
  });

  it('renders game context in switcher when set', () => {
    useChatPanelStore.getState().open({
      id: 'azul',
      name: 'Azul',
      year: 2017,
      pdfCount: 3,
      kbStatus: 'ready',
    });
    render(<ChatSlideOverPanel />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('sceglie un gioco della libreria dalla tendina del contesto (#4187)', async () => {
    const user = userEvent.setup();
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    await user.click(screen.getByRole('button', { name: /seleziona gioco/i }));
    await user.click(screen.getByRole('option', { name: 'Wingspan' }));

    const context = useChatPanelStore.getState().gameContext;
    expect(context?.id).toBe('wingspan');
    expect(context?.name).toBe('Wingspan');
    expect(context?.year).toBe(2019);
  });

  it('la barra laterale «Giochi con KB» elenca solo i giochi della libreria con la KB pronta', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    // La tendina e' chiusa: l'unico «Gioco senza KB» possibile sarebbe nella barra laterale.
    expect(screen.getByRole('button', { name: /wingspan/i })).toBeInTheDocument();
    expect(screen.queryByText('Gioco senza KB')).not.toBeInTheDocument();
  });

  it('«Aggiungi dal catalogo» chiude il pannello, che altrimenti coprirebbe /games', async () => {
    const user = userEvent.setup();
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    await user.click(screen.getByRole('button', { name: /seleziona gioco/i }));
    await user.click(screen.getByRole('link', { name: /aggiungi dal catalogo/i }));

    expect(useChatPanelStore.getState().isOpen).toBe(false);
  });

  it('chiede alla libreria la pagina massima, la stessa per tendina e barra laterale', () => {
    hookArgs.library.length = 0;
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    const sizes = hookArgs.library.map(args => (args[1] as { pageSize?: number }).pageSize);
    expect(new Set(sizes)).toEqual(new Set([100]));
  });

  it('la barra laterale chiede al server i soli giochi con KB, la tendina tutta la libreria (#4227)', () => {
    hookArgs.library.length = 0;
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    const filters = hookArgs.library.map(args => (args[1] as { hasKb?: boolean }).hasKb);
    expect(filters).toContain(true);
    expect(filters).toContain(undefined);
  });

  it('una libreria grande con pochi giochi con KB non si dice parziale (#4227)', () => {
    // Prima la barra filtrava la prima pagina della libreria: oltre 100 giochi diceva di
    // essere parziale anche se i giochi con KB stavano tutti in una pagina.
    libraryTotal = 250;
    kbTotal = 1;
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    expect(screen.getByRole('button', { name: /wingspan/i })).toBeInTheDocument();
    expect(screen.queryByText(/cerca nella tendina/i)).not.toBeInTheDocument();
  });

  it('se i giochi con KB superano la pagina, la barra lo dice e rimanda alla ricerca', () => {
    kbTotal = 250;
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    expect(screen.getByText(/cerca nella tendina/i)).toBeInTheDocument();
  });

  it('closes when the ✕ header button is clicked', async () => {
    useChatPanelStore.getState().open();
    const user = userEvent.setup();
    render(<ChatSlideOverPanel />);
    await user.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(useChatPanelStore.getState().isOpen).toBe(false);
  });

  it('closes on Esc keypress', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true });
    window.dispatchEvent(event);
    expect(useChatPanelStore.getState().isOpen).toBe(false);
  });

  /**
   * #4341: il velo stava SOPRA il pannello. I due livelli venivano da due scale: il velo da
   * `design-tokens.css` (1040), il pannello da `--z-modal`, che `design-tokens-canonical.css`
   * ridefinisce a 60. Nel browser un clic nel campo colpiva il velo, che chiude la chat. jsdom non
   * carica il CSS: qui i due livelli si risolvono come fa il cascade, leggendo i file dei token.
   */
  it('keeps the panel above its own backdrop (#4341)', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    const backdrop = effectiveZIndex(screen.getByTestId('chat-panel-backdrop'));
    const panel = effectiveZIndex(screen.getByTestId('chat-slide-over-panel'));

    expect(panel).toBeGreaterThan(backdrop);
  });
});

const STYLES = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../styles');

/** Il valore di una variabile `--z-*` com'è nel browser. */
function resolveZToken(token: string): number | undefined {
  const read = (file: string) => fs.readFileSync(path.join(STYLES, file), 'utf8');
  const canonical = read('design-tokens-canonical.css');
  // `design-tokens.css` dichiara le variabili dentro `@layer tokens`, il file canonico fuori da
  // ogni layer: una dichiarazione fuori dai layer vince sempre, qualunque siano il selettore e
  // l'ordine dei file. Se un giorno il canonico entrasse in un layer, questa regola non varrebbe
  // più e il test darebbe una risposta falsa: meglio fermarsi.
  if (canonical.includes('@layer')) {
    throw new Error(
      'design-tokens-canonical.css ora usa @layer: la risoluzione dei livelli di questo test va rivista'
    );
  }
  const find = (css: string) => new RegExp(`--${token}:\\s*(-?\\d+)\\s*;`).exec(css)?.[1];
  const value = find(canonical) ?? find(read('design-tokens.css'));
  return value === undefined ? undefined : Number(value);
}

/** Il z-index che l'elemento avrà nel browser, dalla sua classe `z-[var(--z-…,fallback)]`. */
function effectiveZIndex(element: HTMLElement): number {
  const match = /z-\[var\(--([a-z-]+),(\d+)\)\]/.exec(element.className);
  if (!match) {
    throw new Error(
      `${element.dataset.testid}: questo test sa leggere solo un livello dichiarato come ` +
        'z-[var(--z-…,N)]. Se il livello ora si dichiara in un altro modo, aggiorna la lettura qui: ' +
        'non è detto che i livelli siano sbagliati.'
    );
  }
  const [, token, fallback] = match;
  return resolveZToken(token) ?? Number(fallback);
}
