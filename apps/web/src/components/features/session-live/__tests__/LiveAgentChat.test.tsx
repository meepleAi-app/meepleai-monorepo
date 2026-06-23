/**
 * LiveAgentChat unit tests — Wave D.2 Interactions sub-PR (Issue #750)
 * Extended by Issue #2375 G3 — draft persistence + smart scroll
 *
 * Coverage:
 * - Render shape (data-slot, messages, empty state)
 * - Role variant matrix (visibility toggle Spectator vs Player/Host)
 * - onSendMessage fires with correct content + visibility
 * - Spectator forced to 'shared' visibility
 * - Input form state (disabled submit when empty)
 * - aria attributes
 * - #2375 G3: draft persistence (sessionStorage read/write/clear)
 * - #2375 G3: data-at-bottom attribute reflecting scroll anchor state
 */

import { act, render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { IntlProvider } from 'react-intl';

import type { LiveAgentChatLabels, LiveAgentChatProps, ChatMessage } from '../LiveAgentChat';
import { LiveAgentChat } from '../LiveAgentChat';
import { CHAT_DRAFT_KEY_PREFIX } from '@/lib/session-live/use-chat-draft';
import { mapCitationToChatCitation } from '@/lib/session-live/map-citation-to-chat-citation';

// ─── i18n messages used by the component ──────────────────────────────────────

const INTL_MESSAGES = {
  'pages.sessionLive.chat.newMessagesToast':
    '{count, plural, one {# nuovo messaggio} other {# nuovi messaggi}}',
  'pages.sessionLive.chatAgent.nonGroundedDisclaimer': 'Risposta non basata sul regolamento',
};

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: LiveAgentChatLabels = {
  title: 'Chat AI',
  inputAriaLabel: 'Scrivi un messaggio',
  sendAriaLabel: 'Invia',
  visibilityPrivate: 'Privato',
  visibilityShared: 'Condiviso',
  emptyMessage: 'Nessun messaggio',
  newMessagesToastAriaLabel: 'Nuovi messaggi — clic per scorrere',
};

const MESSAGES: ReadonlyArray<ChatMessage> = [
  {
    id: 'msg-1',
    senderId: 'user-a',
    senderName: 'Alice',
    content: 'Ciao!',
    visibility: 'shared',
    timestamp: '2026-05-06T10:00:00Z',
  },
  {
    id: 'msg-2',
    senderId: 'viewer-id',
    senderName: 'Me',
    content: 'Messaggio mio',
    visibility: 'private',
    timestamp: '2026-05-06T10:01:00Z',
  },
];

// ─── Render helper (wraps with IntlProvider for useIntl compatibility) ────────

function renderWithIntl(ui: React.ReactElement) {
  return render(
    <IntlProvider locale="it" messages={INTL_MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

function renderChat(overrides: Partial<LiveAgentChatProps> = {}) {
  const onSendMessage = vi.fn();
  const props: LiveAgentChatProps = {
    sessionId: 'sess-test',
    messages: [],
    viewerRole: 'Player',
    viewerId: 'viewer-id',
    onSendMessage,
    labels: LABELS,
    ...overrides,
  };
  const result = renderWithIntl(<LiveAgentChat {...props} />);
  return { ...result, onSendMessage };
}

// ─── Shared IntersectionObserver stub ─────────────────────────────────────────

beforeEach(() => {
  vi.stubGlobal(
    'IntersectionObserver',
    vi.fn(function (this: unknown, cb: IntersectionObserverCallback) {
      return {
        callback: cb,
        observe: vi.fn(),
        disconnect: vi.fn(),
      } as unknown as IntersectionObserver;
    })
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  sessionStorage.clear();
});

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('LiveAgentChat — render shape', () => {
  it('renders data-slot="live-agent-chat"', () => {
    renderChat();
    expect(screen.getByRole('region', { name: 'Chat AI' })).toHaveAttribute(
      'data-slot',
      'live-agent-chat'
    );
  });

  it('renders data-viewer-role attribute', () => {
    renderChat({ viewerRole: 'Host' });
    expect(screen.getByRole('region', { name: 'Chat AI' })).toHaveAttribute(
      'data-viewer-role',
      'Host'
    );
  });

  it('renders empty message when no messages', () => {
    renderChat({ messages: [] });
    expect(screen.getByText('Nessun messaggio')).toBeInTheDocument();
  });

  it('renders messages list', () => {
    renderChat({ messages: MESSAGES });
    expect(screen.getByText('Ciao!')).toBeInTheDocument();
    expect(screen.getByText('Messaggio mio')).toBeInTheDocument();
  });

  it('renders sender name for non-own messages', () => {
    renderChat({ messages: MESSAGES, viewerId: 'viewer-id' });
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('renders input field', () => {
    renderChat();
    expect(screen.getByRole('textbox', { name: 'Scrivi un messaggio' })).toBeInTheDocument();
  });

  it('renders send button', () => {
    renderChat();
    expect(screen.getByRole('button', { name: 'Invia' })).toBeInTheDocument();
  });
});

describe('LiveAgentChat — role variant matrix', () => {
  it('shows visibility toggle for Player', () => {
    renderChat({ viewerRole: 'Player' });
    expect(screen.getByRole('button', { name: /Privato/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Condiviso/i })).toBeInTheDocument();
  });

  it('shows visibility toggle for Host', () => {
    renderChat({ viewerRole: 'Host' });
    expect(screen.getByRole('button', { name: /Privato/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Condiviso/i })).toBeInTheDocument();
  });

  it('hides visibility toggle for Spectator', () => {
    renderChat({ viewerRole: 'Spectator' });
    // Visibility group button should not be shown (Spectator forced to shared)
    const privateButtons = screen.queryAllByRole('button', { name: /Privato/i });
    // Only send button should exist for Spectator — no private toggle
    expect(privateButtons).toHaveLength(0);
  });
});

describe('LiveAgentChat — onSendMessage', () => {
  it('calls onSendMessage with content and "shared" for Player', async () => {
    const user = userEvent.setup();
    const { onSendMessage } = renderChat({ viewerRole: 'Player' });

    await user.type(screen.getByRole('textbox', { name: 'Scrivi un messaggio' }), 'Test msg');
    await user.click(screen.getByRole('button', { name: 'Invia' }));

    expect(onSendMessage).toHaveBeenCalledOnce();
    expect(onSendMessage).toHaveBeenCalledWith('Test msg', 'shared');
  });

  it('calls onSendMessage with "private" when private is selected (Player)', async () => {
    const user = userEvent.setup();
    const { onSendMessage } = renderChat({ viewerRole: 'Player' });

    // Switch to private
    await user.click(screen.getByRole('button', { name: /Privato/i }));
    await user.type(screen.getByRole('textbox', { name: 'Scrivi un messaggio' }), 'Private msg');
    await user.click(screen.getByRole('button', { name: 'Invia' }));

    expect(onSendMessage).toHaveBeenCalledWith('Private msg', 'private');
  });

  it('forces Spectator to "shared" visibility', async () => {
    const user = userEvent.setup();
    const { onSendMessage } = renderChat({ viewerRole: 'Spectator' });

    await user.type(screen.getByRole('textbox', { name: 'Scrivi un messaggio' }), 'Spectator msg');
    await user.click(screen.getByRole('button', { name: 'Invia' }));

    expect(onSendMessage).toHaveBeenCalledWith('Spectator msg', 'shared');
  });

  it('does not call onSendMessage when input is empty', async () => {
    const user = userEvent.setup();
    const { onSendMessage } = renderChat({ viewerRole: 'Player' });

    await user.click(screen.getByRole('button', { name: 'Invia' }));
    expect(onSendMessage).not.toHaveBeenCalled();
  });

  it('clears input after send', async () => {
    const user = userEvent.setup();
    renderChat({ viewerRole: 'Player' });

    const input = screen.getByRole('textbox', { name: 'Scrivi un messaggio' });
    await user.type(input, 'Hello');
    await user.click(screen.getByRole('button', { name: 'Invia' }));

    expect(input).toHaveValue('');
  });
});

describe('LiveAgentChat — aria attributes', () => {
  it('has aria-pressed on visibility toggle buttons', () => {
    renderChat({ viewerRole: 'Player' });
    const sharedBtn = screen.getByRole('button', { name: /Condiviso/i });
    // Default is shared → aria-pressed=true
    expect(sharedBtn).toHaveAttribute('aria-pressed', 'true');
  });

  it('updates aria-pressed when switching visibility', async () => {
    const user = userEvent.setup();
    renderChat({ viewerRole: 'Player' });

    const privateBtn = screen.getByRole('button', { name: /Privato/i });
    await user.click(privateBtn);
    expect(privateBtn).toHaveAttribute('aria-pressed', 'true');
  });
});

// ─── #2375 G3 — draft persistence + smart scroll ──────────────────────────────

// ─── #2500 — ChatCitationCard rendering ───────────────────────────────────────

describe('#2500 — CitationCard rendering', () => {
  it('renderizza ChatCitationCard quando il messaggio assistant ha citazioni', () => {
    const msgWithCitations: ChatMessage = {
      id: 'msg-cite-1',
      senderId: 'agent',
      senderName: 'Agent',
      content: 'Ecco il regolamento.',
      visibility: 'shared',
      timestamp: '2026-06-23T10:00:00Z',
      citations: [{ documentName: 'Reg Azul', pages: [7], excerpt: 'Posiziona' }],
    };
    renderChat({ messages: [msgWithCitations] });
    expect(screen.getByText(/Reg Azul/)).toBeInTheDocument();
    expect(screen.getByText(/pag\. 7/i)).toBeInTheDocument();
  });

  it('NON renderizza citazioni quando assenti (AC-CHAT-3)', () => {
    const msgWithoutCitations: ChatMessage = {
      id: 'msg-no-cite',
      senderId: 'agent',
      senderName: 'Agent',
      content: 'Risposta senza citazioni.',
      visibility: 'shared',
      timestamp: '2026-06-23T10:01:00Z',
    };
    renderChat({ messages: [msgWithoutCitations] });
    expect(screen.queryByText(/pag\./i)).toBeNull();
  });
});

// ─── AC-CHAT-2: DOM test "mai verbatim" (Parte A, solo test — nessuna modifica di produzione) ───

describe('#2500 AC-CHAT-2 — protected verbatim never in DOM', () => {
  it('mostra la parafrasi ma mai il verbatim nel DOM per citazione protected', () => {
    // Costruisce la ChatCitation passando per mapCitationToChatCitation su una citazione protected
    // con un campo verbatim distintivo — questo è il regression hook per il round-trip
    // protected → mapper → ChatCitationCard → DOM.
    const protectedCitation = {
      source: 'Regolamento Azul',
      pageNumber: 7,
      copyrightTier: 'protected' as const,
      snippet: 'VERBATIM_SECRET',
      paraphrasedSnippet: 'sintesi regola',
    };

    const chatCitation = mapCitationToChatCitation(protectedCitation);
    expect(chatCitation).not.toBeNull();
    // Sanity: la citazione mappata usa paraphrase, non verbatim
    expect(chatCitation!.excerpt).toBe('sintesi regola');

    const msgWithProtectedCitation: ChatMessage = {
      id: 'msg-protected-1',
      senderId: 'agent',
      senderName: 'MeepleAI',
      content: 'Ecco la regola.',
      visibility: 'shared',
      timestamp: '2026-06-23T10:00:00Z',
      citations: [chatCitation!],
    };

    const { container } = renderChat({ messages: [msgWithProtectedCitation] });

    // La parafrasi è presente nel DOM
    expect(screen.getByText(/sintesi regola/)).toBeInTheDocument();
    // Il verbatim NON è mai nel DOM
    expect(container.textContent).not.toContain('VERBATIM_SECRET');
  });
});

// ─── AC-CHAT-3: disclaimer risposta non-grounded ──────────────────────────────

describe('#2500 AC-CHAT-3 — non-grounded disclaimer', () => {
  it('messaggio assistant isNonGrounded:true → disclaimer data-slot="chat-nongrounded-disclaimer" nel DOM', () => {
    const msgNonGrounded: ChatMessage = {
      id: 'msg-ng-1',
      senderId: 'agent',
      senderName: 'MeepleAI',
      content: 'Risposta senza fonti.',
      visibility: 'shared',
      timestamp: '2026-06-23T10:00:00Z',
      isNonGrounded: true,
    };
    const { container } = renderChat({ messages: [msgNonGrounded] });
    expect(
      container.querySelector('[data-slot="chat-nongrounded-disclaimer"]')
    ).toBeInTheDocument();
    // Testo localizzato presente
    expect(container.textContent).toContain('Risposta non basata sul regolamento');
  });

  it('messaggio assistant con citazioni → disclaimer assente', () => {
    const msgWithCitations: ChatMessage = {
      id: 'msg-cite-ng',
      senderId: 'agent',
      senderName: 'MeepleAI',
      content: 'Risposta con fonti.',
      visibility: 'shared',
      timestamp: '2026-06-23T10:00:00Z',
      citations: [{ documentName: 'Reg', pages: [1], excerpt: 'testo' }],
    };
    const { container } = renderChat({ messages: [msgWithCitations] });
    expect(
      container.querySelector('[data-slot="chat-nongrounded-disclaimer"]')
    ).not.toBeInTheDocument();
  });

  it('messaggio user → disclaimer assente anche se isNonGrounded accidentalmente truthy', () => {
    // Nota: per design i messaggi user non hanno mai isNonGrounded, ma anche se fosse
    // settato accidentalmente il disclaimer non deve apparire su messaggi user
    const msgUser: ChatMessage = {
      id: 'msg-user-ng',
      senderId: 'viewer-id',
      senderName: 'Player',
      content: 'Domanda utente.',
      visibility: 'shared',
      timestamp: '2026-06-23T10:00:00Z',
    };
    const { container } = renderChat({ messages: [msgUser], viewerId: 'viewer-id' });
    expect(
      container.querySelector('[data-slot="chat-nongrounded-disclaimer"]')
    ).not.toBeInTheDocument();
  });

  it('messaggio assistant isNonGrounded:false/undefined → disclaimer assente', () => {
    const msgAssistant: ChatMessage = {
      id: 'msg-a-nodisc',
      senderId: 'agent',
      senderName: 'MeepleAI',
      content: 'Risposta normale senza flag.',
      visibility: 'shared',
      timestamp: '2026-06-23T10:00:00Z',
    };
    const { container } = renderChat({ messages: [msgAssistant] });
    expect(
      container.querySelector('[data-slot="chat-nongrounded-disclaimer"]')
    ).not.toBeInTheDocument();
  });
});

describe('#2375 G3 — draft + smart scroll', () => {
  const baseLabels: LiveAgentChatLabels = {
    title: 'Chat',
    inputAriaLabel: 'Scrivi',
    sendAriaLabel: 'Invia',
    visibilityPrivate: 'Privato',
    visibilityShared: 'Condiviso',
    emptyMessage: 'Vuoto',
    newMessagesToastAriaLabel: 'Nuovi messaggi — clic per scorrere',
  };

  it('loads draft from sessionStorage on mount', () => {
    sessionStorage.setItem(`${CHAT_DRAFT_KEY_PREFIX}sess-1`, 'in-progress draft');

    renderWithIntl(
      <LiveAgentChat
        sessionId="sess-1"
        messages={[]}
        viewerRole="Player"
        viewerId="me"
        onSendMessage={() => {}}
        labels={baseLabels}
      />
    );

    const input = screen.getByLabelText('Scrivi') as HTMLInputElement;
    expect(input.value).toBe('in-progress draft');
  });

  it('writes input changes to sessionStorage as draft', () => {
    renderWithIntl(
      <LiveAgentChat
        sessionId="sess-1"
        messages={[]}
        viewerRole="Player"
        viewerId="me"
        onSendMessage={() => {}}
        labels={baseLabels}
      />
    );

    const input = screen.getByLabelText('Scrivi') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'typing' } });

    expect(sessionStorage.getItem(`${CHAT_DRAFT_KEY_PREFIX}sess-1`)).toBe('typing');
  });

  it('clears draft from sessionStorage after successful send', () => {
    const onSend = vi.fn();
    renderWithIntl(
      <LiveAgentChat
        sessionId="sess-1"
        messages={[]}
        viewerRole="Player"
        viewerId="me"
        onSendMessage={onSend}
        labels={baseLabels}
      />
    );

    const input = screen.getByLabelText('Scrivi') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'hi' } });
    fireEvent.click(screen.getByLabelText('Invia'));

    expect(onSend).toHaveBeenCalledWith('hi', 'shared');
    expect(sessionStorage.getItem(`${CHAT_DRAFT_KEY_PREFIX}sess-1`)).toBe(null);
    expect(input.value).toBe('');
  });

  it('renders data-at-bottom attribute reflecting scroll anchor state', () => {
    const { container } = renderWithIntl(
      <LiveAgentChat
        sessionId="sess-1"
        messages={[]}
        viewerRole="Player"
        viewerId="me"
        onSendMessage={() => {}}
        labels={baseLabels}
      />
    );

    const root = container.querySelector('[data-slot="live-agent-chat"]');
    expect(root).not.toBeNull();
    expect(root?.getAttribute('data-at-bottom')).toBe('true');
  });

  it('resets newMessageCount when isAtBottom flips to true (manual scroll)', () => {
    // Capture IntersectionObserver callbacks for manual firing
    const observers: IntersectionObserverCallback[] = [];
    vi.stubGlobal(
      'IntersectionObserver',
      vi.fn(function (this: unknown, cb: IntersectionObserverCallback) {
        observers.push(cb);
        return { observe: vi.fn(), disconnect: vi.fn() } as unknown as IntersectionObserver;
      })
    );

    function fire(isIntersecting: boolean) {
      act(() => {
        for (const cb of observers) {
          cb([{ isIntersecting } as IntersectionObserverEntry], {} as IntersectionObserver);
        }
      });
    }

    const { rerender, queryByLabelText } = renderWithIntl(
      <LiveAgentChat
        sessionId="sess-1"
        messages={[]}
        viewerRole="Player"
        viewerId="me"
        onSendMessage={() => {}}
        labels={baseLabels}
      />
    );

    // Step 1: simulate user scrolled up (not at bottom)
    fire(false);

    // Step 2: new message arrives while scrolled up → counter increments → toast visible
    rerender(
      <IntlProvider locale="it" messages={INTL_MESSAGES}>
        <LiveAgentChat
          sessionId="sess-1"
          messages={[
            {
              id: 'm1',
              senderId: 'other',
              senderName: 'Other',
              content: 'hi',
              visibility: 'shared',
              timestamp: '2026-01-01T00:00:00Z',
            },
          ]}
          viewerRole="Player"
          viewerId="me"
          onSendMessage={() => {}}
          labels={baseLabels}
        />
      </IntlProvider>
    );

    expect(queryByLabelText(baseLabels.newMessagesToastAriaLabel)).not.toBeNull();

    // Step 3: user manually scrolls to bottom (isAtBottom flips true, no new messages)
    fire(true);

    // Toast should disappear (guarded by !isAtBottom) AND counter reset to 0
    expect(queryByLabelText(baseLabels.newMessagesToastAriaLabel)).toBeNull();
  });
});
