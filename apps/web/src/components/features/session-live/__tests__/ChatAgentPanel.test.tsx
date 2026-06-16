/**
 * ChatAgentPanel unit tests — Issue #2374 G1 (T3).
 *
 * Coverage (per plan §4 T3):
 * 1. data-slot="chat-agent-panel" regression hook
 * 2. header shows agent name + emoji + Online pip + latency
 * 3. header has "ChatAgent" pill (mockup magnet semantic)
 * 4. body renders LiveAgentChat (data-slot="live-agent-chat")
 * 5. collapsed=true unmounts body + aria-expanded=false
 * 6. clicking header fires onHeaderClick (when provided)
 * 7. header is <div> (not <button>) when onHeaderClick undefined
 * 8. compact prop pass-through smoke (renders without error)
 * 9. token discipline: no raw HSL; uses bg-entity-agent classes
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { ChatAgentPanelLabels, ChatAgentPanelProps } from '../ChatAgentPanel';
import { ChatAgentPanel } from '../ChatAgentPanel';
import type { ChatMessage, LiveAgentChatLabels } from '../LiveAgentChat';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const CHAT_PANEL_LABELS: LiveAgentChatLabels = {
  title: 'Chat AI',
  inputAriaLabel: 'Scrivi un messaggio',
  sendAriaLabel: 'Invia',
  visibilityPrivate: 'Privato',
  visibilityShared: 'Condiviso',
  emptyMessage: 'Nessun messaggio',
};

const LABELS: ChatAgentPanelLabels = {
  title: 'ChatAgent',
  agentNameAriaLabel: 'Nome agente Meeple',
  onlineLabel: 'Online',
  latencyAriaLabel: 'Latenza 42ms',
  chatPanelLabels: CHAT_PANEL_LABELS,
};

const MESSAGES: ReadonlyArray<ChatMessage> = [
  {
    id: 'msg-1',
    senderId: 'agent',
    senderName: 'Meeple',
    content: 'Benvenuto!',
    visibility: 'shared',
    timestamp: '2026-06-15T10:00:00Z',
  },
];

function renderPanel(overrides: Partial<ChatAgentPanelProps> = {}) {
  const onSendMessage = vi.fn();
  const onHeaderClick = vi.fn();
  const props: ChatAgentPanelProps = {
    messages: [],
    viewerRole: 'Player',
    viewerId: 'viewer-id',
    onSendMessage,
    agentName: 'Meeple',
    latencyMs: 42,
    labels: LABELS,
    onHeaderClick,
    ...overrides,
  };
  const result = render(<ChatAgentPanel {...props} />);
  return { ...result, onSendMessage, onHeaderClick };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ChatAgentPanel — render shape', () => {
  it('renders data-slot="chat-agent-panel" (regression hook)', () => {
    const { container } = renderPanel();
    const root = container.querySelector('[data-slot="chat-agent-panel"]');
    expect(root).not.toBeNull();
  });

  it('header shows agent name + emoji + Online pip + latency', () => {
    renderPanel({ agentName: 'Meeple', agentEmoji: '🤖', latencyMs: 42 });
    // Agent name visible
    expect(screen.getByText('Meeple')).toBeInTheDocument();
    // Emoji avatar present (default 🤖)
    expect(screen.getByText('🤖')).toBeInTheDocument();
    // Online pip — aria-label matches labels.onlineLabel
    expect(screen.getByLabelText(LABELS.onlineLabel)).toBeInTheDocument();
    // Latency indicator — aria-label matches labels.latencyAriaLabel + text shows ms
    expect(screen.getByLabelText(LABELS.latencyAriaLabel)).toBeInTheDocument();
    expect(screen.getByText(/42ms/)).toBeInTheDocument();
  });

  it('header has "ChatAgent" pill (mockup magnet semantic)', () => {
    renderPanel();
    // The pill carries labels.title text ("ChatAgent"); use getAllByText because
    // the aria-label on agent name uses a similar word — match the visible pill.
    const pill = screen.getByText(LABELS.title);
    expect(pill).toBeInTheDocument();
  });

  it('body renders LiveAgentChat (data-slot="live-agent-chat")', () => {
    const { container } = renderPanel({ messages: MESSAGES });
    const chat = container.querySelector('[data-slot="live-agent-chat"]');
    expect(chat).not.toBeNull();
  });
});

describe('ChatAgentPanel — collapsed semantics', () => {
  it('collapsed=true hides body + sets aria-expanded=false on header button', () => {
    const { container } = renderPanel({ collapsed: true });
    // Body unmounted: data-slot="live-agent-chat" must not exist
    const chat = container.querySelector('[data-slot="live-agent-chat"]');
    expect(chat).toBeNull();
    // Header button has aria-expanded="false"
    const headerButton = container.querySelector('[data-slot="chat-agent-panel"] > button');
    expect(headerButton).not.toBeNull();
    expect(headerButton).toHaveAttribute('aria-expanded', 'false');
  });

  it('collapsed=true also reflects on root via data-collapsed="true"', () => {
    const { container } = renderPanel({ collapsed: true });
    const root = container.querySelector('[data-slot="chat-agent-panel"]');
    expect(root).toHaveAttribute('data-collapsed', 'true');
  });
});

describe('ChatAgentPanel — header interactions', () => {
  it('clicking header fires onHeaderClick (when provided)', () => {
    const { container, onHeaderClick } = renderPanel();
    const headerButton = container.querySelector('[data-slot="chat-agent-panel"] > button');
    expect(headerButton).not.toBeNull();
    fireEvent.click(headerButton!);
    expect(onHeaderClick).toHaveBeenCalledOnce();
  });

  it('header is a <div> (not <button>) when onHeaderClick is undefined', () => {
    // Override default fixture by explicitly passing onHeaderClick=undefined
    const { container } = render(
      <ChatAgentPanel
        messages={[]}
        viewerRole="Player"
        viewerId="viewer-id"
        onSendMessage={vi.fn()}
        agentName="Meeple"
        latencyMs={42}
        labels={LABELS}
      />
    );
    // Header is the first child of root, should NOT be a <button>
    const root = container.querySelector('[data-slot="chat-agent-panel"]');
    const headerEl = root?.firstElementChild;
    expect(headerEl?.tagName.toLowerCase()).not.toBe('button');
  });
});

describe('ChatAgentPanel — compact pass-through', () => {
  it('renders without error when compact=true (smoke)', () => {
    const { container } = renderPanel({ compact: true, messages: MESSAGES });
    // Chat panel still renders
    const chat = container.querySelector('[data-slot="live-agent-chat"]');
    expect(chat).not.toBeNull();
  });
});

describe('ChatAgentPanel — token discipline', () => {
  it('uses bg-entity-agent classes and contains no raw HSL', () => {
    const { container } = renderPanel();
    const root = container.querySelector('[data-slot="chat-agent-panel"]');
    expect(root).not.toBeNull();
    // The rendered DOM must contain at least one bg-entity-agent class somewhere
    const html = (root as HTMLElement).outerHTML;
    expect(html).toMatch(/bg-entity-agent/);
    // No raw HSL literals — common shapes: `bg-[hsl(`, `[hsl(`, `hsl(140` etc.
    expect(html).not.toMatch(/\[hsl\(/);
  });
});
