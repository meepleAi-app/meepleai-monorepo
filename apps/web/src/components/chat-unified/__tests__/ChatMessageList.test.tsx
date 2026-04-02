/**
 * ChatMessageList — Windowed Slice Tests
 * Verifica che solo gli ultimi WINDOW_SIZE=50 messaggi siano renderizzati
 * e che il bottone "messaggi precedenti" carichi quelli nascosti.
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import {
  ChatMessageList,
  type ChatMessageItem,
  type StreamStateForMessages,
} from '../ChatMessageList';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('../TtsSpeakerButton', () => ({
  TtsSpeakerButton: () => null,
}));
vi.mock('../RuleSourceCard', () => ({
  RuleSourceCard: () => null,
}));
vi.mock('../ResponseMetaBadge', () => ({
  ResponseMetaBadge: () => null,
}));
vi.mock('../TechnicalDetailsPanel', () => ({
  TechnicalDetailsPanel: () => null,
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

const baseStream: StreamStateForMessages = {
  isStreaming: false,
  currentAnswer: '',
  statusMessage: null,
  strategyTier: null,
  executionId: null,
  debugSteps: [],
  modelDowngrade: null,
};

function makeMessages(count: number): ChatMessageItem[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `msg-${i}`,
    role: (i % 2 === 0 ? 'user' : 'assistant') as 'user' | 'assistant',
    content: `Messaggio numero ${i}`,
  }));
}

const defaultProps = {
  streamState: baseStream,
  isEditor: false,
  isAdmin: false,
  isTtsSupported: false,
  ttsEnabled: false,
  isSpeaking: false,
  onSpeak: vi.fn(),
  onStopSpeaking: vi.fn(),
  messagesEndRef: { current: null } as React.RefObject<HTMLDivElement | null>,
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ChatMessageList — windowed slice', () => {
  it('renders all messages when count is ≤ 50', () => {
    const messages = makeMessages(10);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    expect(screen.getAllByTestId(/^message-/).length).toBe(10);
    expect(screen.queryByRole('button', { name: /messaggi precedenti/ })).toBeNull();
  });

  it('renders only last 50 messages when count is 80', () => {
    const messages = makeMessages(80);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    // Deve renderizzare esattamente 50 nodi
    expect(screen.getAllByTestId(/^message-/).length).toBe(50);
    // L'ultimo messaggio è visibile
    expect(screen.getByText('Messaggio numero 79')).toBeInTheDocument();
    // Il primo messaggio NON è visibile
    expect(screen.queryByText('Messaggio numero 0')).toBeNull();
  });

  it('shows "30 messaggi precedenti" button when 80 messages and window=50', () => {
    const messages = makeMessages(80);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    expect(screen.getByRole('button', { name: /30 messaggi precedenti/ })).toBeInTheDocument();
  });

  it('renders all 80 messages after clicking the button', () => {
    const messages = makeMessages(80);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    fireEvent.click(screen.getByRole('button', { name: /30 messaggi precedenti/ }));
    // Ora tutti 80 sono visibili
    expect(screen.getAllByTestId(/^message-/).length).toBe(80);
    // Il bottone scompare
    expect(screen.queryByRole('button', { name: /messaggi precedenti/ })).toBeNull();
  });

  it('shows empty state when messages array is empty', () => {
    render(<ChatMessageList {...defaultProps} messages={[]} />);
    expect(screen.getByText(/Inizia la conversazione/)).toBeInTheDocument();
  });

  it('shows streaming bubble when isStreaming is true', () => {
    render(
      <ChatMessageList
        {...defaultProps}
        messages={[]}
        streamState={{ ...baseStream, isStreaming: true, currentAnswer: 'risposta...' }}
      />
    );
    expect(screen.getByTestId('message-streaming')).toBeInTheDocument();
  });
});
