/**
 * ChatExtraMeepleCard Tests
 * Issue #5027 - ChatExtraMeepleCard — detail card con tab
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ChatExtraMeepleCard } from '../EntityExtraMeepleCard';
import type { ChatDetailData } from '../types';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(
      (
        { children, style, ...props }: React.PropsWithChildren<Record<string, unknown>>,
        ref: React.Ref<HTMLDivElement>
      ) => (
        <div ref={ref} style={style as React.CSSProperties} {...props}>
          {children}
        </div>
      )
    ),
  },
}));

vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// ============================================================================
// Helpers
// ============================================================================

const MOCK_DATE = '2026-01-15T10:00:00Z';

function makeData(overrides: Partial<ChatDetailData> = {}): ChatDetailData {
  return {
    id: 'thread-1',
    status: 'active',
    agentId: 'agent-1',
    agentName: 'Catan Expert',
    agentModel: 'gpt-4o',
    gameId: 'game-1',
    gameName: 'Catan',
    startedAt: MOCK_DATE,
    messageCount: 5,
    messages: [
      {
        id: 'msg-1',
        role: 'user',
        content: 'Come funziona il commercio?',
        createdAt: MOCK_DATE,
      },
      {
        id: 'msg-2',
        role: 'assistant',
        content: 'Il commercio in Catan funziona così...',
        createdAt: MOCK_DATE,
      },
    ],
    ...overrides,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('ChatExtraMeepleCard', () => {

  // --------------------------------------------------------------------------
  // Loading / Error states
  // --------------------------------------------------------------------------

  describe('loading and error states', () => {
    it('renders loading state when loading is true', () => {
      render(<ChatExtraMeepleCard data={makeData()} loading />);
      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('renders error state when error is provided', () => {
      render(<ChatExtraMeepleCard data={makeData()} error="Thread non trovato" />);
      expect(screen.getByText('Thread non trovato')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Overview tab (default)
  // --------------------------------------------------------------------------

  describe('Overview tab', () => {
    it('renders the agent name in the header', () => {
      render(<ChatExtraMeepleCard data={makeData()} />);
      expect(screen.getByText('Catan Expert')).toBeInTheDocument();
    });

    it('renders ChatStatusBadge with correct status', () => {
      render(<ChatExtraMeepleCard data={makeData({ status: 'active' })} />);
      expect(screen.getByTestId('chat-status-active')).toBeInTheDocument();
    });

    it('renders agent model badge', () => {
      render(<ChatExtraMeepleCard data={makeData()} />);
      expect(screen.getByText('gpt-4o')).toBeInTheDocument();
    });

    it('renders game context chip when gameName is set', () => {
      render(<ChatExtraMeepleCard data={makeData()} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Gioco di contesto')).toBeInTheDocument();
    });

    it('does not render game chip when gameName is absent', () => {
      render(<ChatExtraMeepleCard data={makeData({ gameName: undefined })} />);
      expect(screen.queryByText('Gioco di contesto')).not.toBeInTheDocument();
    });

    it('renders "Riprendi Chat" CTA linking to thread', () => {
      render(<ChatExtraMeepleCard data={makeData()} />);
      const cta = screen.getByTestId('chat-action-resume');
      expect(cta).toHaveAttribute('href', '/chat/thread-1');
      expect(cta).toHaveTextContent('Riprendi Chat');
    });

    it('renders duration stat card when durationMinutes is set', () => {
      render(<ChatExtraMeepleCard data={makeData({ durationMinutes: 30 })} />);
      expect(screen.getByText('30m')).toBeInTheDocument();
    });

    it('does not render duration stat card when durationMinutes is absent', () => {
      render(<ChatExtraMeepleCard data={makeData({ durationMinutes: undefined })} />);
      expect(screen.queryByText(/m$/)).not.toBeInTheDocument();
    });

    it('renders message count stat card', () => {
      render(<ChatExtraMeepleCard data={makeData({ messageCount: 7 })} />);
      expect(screen.getByText('7')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Messages tab
  // --------------------------------------------------------------------------

  describe('Messages tab', () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('switches to messages tab on click', async () => {
      const user = userEvent.setup();
      render(<ChatExtraMeepleCard data={makeData()} />);

      await user.click(screen.getByRole('tab', { name: /messaggi/i }));

      expect(screen.getByTestId('chat-action-view-full')).toBeInTheDocument();
    });

    it('renders chat bubbles for each message', async () => {
      const user = userEvent.setup();
      render(<ChatExtraMeepleCard data={makeData()} />);

      await user.click(screen.getByRole('tab', { name: /messaggi/i }));

      expect(screen.getByText('Come funziona il commercio?')).toBeInTheDocument();
      expect(screen.getByText('Il commercio in Catan funziona così...')).toBeInTheDocument();
    });

    it('truncates long message content to 200 chars', async () => {
      const user = userEvent.setup();
      const longContent = 'A'.repeat(250);
      const data = makeData({
        messages: [{ id: 'm1', role: 'user', content: longContent, createdAt: MOCK_DATE }],
      });

      render(<ChatExtraMeepleCard data={data} />);
      await user.click(screen.getByRole('tab', { name: /messaggi/i }));

      // Should see 200 chars + ellipsis, not the full 250
      const expected = `${'A'.repeat(200)}…`;
      expect(screen.getByText(expected)).toBeInTheDocument();
    });

    it('shows empty state when no messages', async () => {
      const user = userEvent.setup();
      render(<ChatExtraMeepleCard data={makeData({ messages: [] })} />);

      await user.click(screen.getByRole('tab', { name: /messaggi/i }));

      expect(screen.getByText('Nessun messaggio in questo thread')).toBeInTheDocument();
    });

    it('renders "Vedi conversazione completa" CTA with correct href', async () => {
      const user = userEvent.setup();
      render(<ChatExtraMeepleCard data={makeData()} />);

      await user.click(screen.getByRole('tab', { name: /messaggi/i }));

      const cta = screen.getByTestId('chat-action-view-full');
      expect(cta).toHaveAttribute('href', '/chat/thread-1');
    });
  });

  // --------------------------------------------------------------------------
  // Context tab
  // --------------------------------------------------------------------------

  describe('Context tab', () => {
    it('switches to context tab on click', async () => {
      const user = userEvent.setup();
      render(<ChatExtraMeepleCard data={makeData()} />);

      await user.click(screen.getByRole('tab', { name: /contesto/i }));

      // Agent section should be visible
      expect(screen.getByText('Agente')).toBeInTheDocument();
    });

    it('renders compact agent card in context tab', async () => {
      const user = userEvent.setup();
      render(<ChatExtraMeepleCard data={makeData()} />);

      await user.click(screen.getByRole('tab', { name: /contesto/i }));

      expect(screen.getAllByText('Catan Expert').length).toBeGreaterThan(0);
      expect(screen.getAllByText('gpt-4o').length).toBeGreaterThan(0);
    });

    it('renders compact game card in context tab', async () => {
      const user = userEvent.setup();
      render(<ChatExtraMeepleCard data={makeData()} />);

      await user.click(screen.getByRole('tab', { name: /contesto/i }));

      expect(screen.getByText('Gioco')).toBeInTheDocument();
      expect(screen.getAllByText('Catan').length).toBeGreaterThan(0);
    });

    it('renders session parameters when present', async () => {
      const user = userEvent.setup();
      const data = makeData({
        temperature: 0.7,
        maxTokens: 1024,
        systemPrompt: 'Sei un esperto di giochi.',
      });

      render(<ChatExtraMeepleCard data={data} />);
      await user.click(screen.getByRole('tab', { name: /contesto/i }));

      expect(screen.getByText('0.7')).toBeInTheDocument();
      expect(screen.getByText('1024')).toBeInTheDocument();
      expect(screen.getByText('Sei un esperto di giochi.')).toBeInTheDocument();
    });

    it('truncates system prompt longer than 150 chars', async () => {
      const user = userEvent.setup();
      const longPrompt = 'B'.repeat(200);
      const data = makeData({ systemPrompt: longPrompt });

      render(<ChatExtraMeepleCard data={data} />);
      await user.click(screen.getByRole('tab', { name: /contesto/i }));

      const expected = `${'B'.repeat(150)}…`;
      expect(screen.getByText(expected)).toBeInTheDocument();
    });

    it('shows empty state when no context is available', async () => {
      const user = userEvent.setup();
      const data = makeData({
        gameName: undefined,
        agentName: undefined,
        temperature: undefined,
        maxTokens: undefined,
        systemPrompt: undefined,
      });

      render(<ChatExtraMeepleCard data={data} />);
      await user.click(screen.getByRole('tab', { name: /contesto/i }));

      expect(screen.getByText('Nessun contesto disponibile')).toBeInTheDocument();
    });

    it('does not render session params section when all params absent', async () => {
      const user = userEvent.setup();
      render(<ChatExtraMeepleCard data={makeData({ temperature: undefined, maxTokens: undefined, systemPrompt: undefined })} />);

      await user.click(screen.getByRole('tab', { name: /contesto/i }));

      expect(screen.queryByText('Parametri sessione')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Status variants
  // --------------------------------------------------------------------------

  describe('status badge variants', () => {
    it.each([
      ['active', 'chat-status-active'],
      ['waiting', 'chat-status-waiting'],
      ['archived', 'chat-status-archived'],
      ['closed', 'chat-status-closed'],
    ] as const)('renders %s status badge', (status, testId) => {
      render(<ChatExtraMeepleCard data={makeData({ status })} />);
      expect(screen.getByTestId(testId)).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // data-testid passthrough
  // --------------------------------------------------------------------------

  it('forwards data-testid to root element', () => {
    render(<ChatExtraMeepleCard data={makeData()} data-testid="my-chat-card" />);
    expect(screen.getByTestId('my-chat-card')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Fallback title when agentName is absent
  // --------------------------------------------------------------------------

  it('uses "Chat" as header title when agentName is absent', () => {
    render(<ChatExtraMeepleCard data={makeData({ agentName: undefined })} />);
    expect(screen.getByText('Chat')).toBeInTheDocument();
  });
});
