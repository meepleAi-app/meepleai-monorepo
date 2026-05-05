/**
 * LiveAgentChat unit tests — Wave D.2 Interactions sub-PR (Issue #750)
 *
 * Coverage:
 * - Render shape (data-slot, messages, empty state)
 * - Role variant matrix (visibility toggle Spectator vs Player/Host)
 * - onSendMessage fires with correct content + visibility
 * - Spectator forced to 'shared' visibility
 * - Input form state (disabled submit when empty)
 * - aria attributes
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { LiveAgentChatLabels, LiveAgentChatProps, ChatMessage } from '../LiveAgentChat';
import { LiveAgentChat } from '../LiveAgentChat';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: LiveAgentChatLabels = {
  title: 'Chat AI',
  inputAriaLabel: 'Scrivi un messaggio',
  sendAriaLabel: 'Invia',
  visibilityPrivate: 'Privato',
  visibilityShared: 'Condiviso',
  emptyMessage: 'Nessun messaggio',
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

function renderChat(overrides: Partial<LiveAgentChatProps> = {}) {
  const onSendMessage = vi.fn();
  const props: LiveAgentChatProps = {
    messages: [],
    viewerRole: 'Player',
    viewerId: 'viewer-id',
    onSendMessage,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<LiveAgentChat {...props} />);
  return { ...result, onSendMessage };
}

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
