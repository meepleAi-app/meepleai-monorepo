/**
 * ChatMessageList Tests
 *
 * Windowed slice tests (pre-existing) + characterization tests (#292 prereq).
 *
 * The characterization tests pin behavior of:
 * - Feedback round-trip (helpful / not-helpful / error silent-fail)
 * - Streaming token accumulation
 * - Per-message feedback state isolation
 * - Feedback button visibility based on gameId/threadId
 *
 * Added as safety net before ChatMessageList is refactored (Issue #292).
 * See docs/development/chat-message-list-behavior-baseline.md for rationale.
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

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

// FeedbackButtons stub — exposes minimal API for tests to drive feedback flow
vi.mock('@/components/ui/meeple/feedback-buttons', () => ({
  FeedbackButtons: ({
    value,
    onFeedbackChange,
    isLoading,
  }: {
    value: 'helpful' | 'not-helpful' | null;
    onFeedbackChange: (v: 'helpful' | 'not-helpful' | null, comment?: string) => void;
    isLoading: boolean;
    showCommentOnNegative?: boolean;
    size?: string;
  }) => (
    <div
      data-testid="feedback-buttons"
      data-value={value ?? 'null'}
      data-loading={String(isLoading)}
    >
      <button type="button" onClick={() => onFeedbackChange('helpful')}>
        Helpful
      </button>
      <button type="button" onClick={() => onFeedbackChange('not-helpful', 'Bad response')}>
        Not helpful
      </button>
    </div>
  ),
}));

// api mock — shared across tests
const submitKbFeedbackMock = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      submitKbFeedback: (...args: unknown[]) => submitKbFeedbackMock(...args),
    },
  },
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

// ── Tests: Windowed slice (pre-existing) ──────────────────────────────────────

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
    expect(screen.getAllByTestId(/^message-/).length).toBe(50);
    expect(screen.getByText('Messaggio numero 79')).toBeInTheDocument();
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
    expect(screen.getAllByTestId(/^message-/).length).toBe(80);
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

// ── Tests: Characterization — Feedback flow (#292 safety net) ─────────────────

describe('ChatMessageList — feedback flow characterization', () => {
  beforeEach(() => {
    submitKbFeedbackMock.mockReset();
  });

  const assistantMessage: ChatMessageItem = {
    id: 'msg-assistant-1',
    role: 'assistant',
    content: 'Puoi giocare 3 carte.',
  };

  it('submits helpful feedback and calls api.knowledgeBase.submitKbFeedback with correct payload', async () => {
    submitKbFeedbackMock.mockResolvedValue(undefined);

    render(
      <ChatMessageList
        {...defaultProps}
        messages={[assistantMessage]}
        gameId="game-1"
        threadId="thread-42"
      />
    );

    const helpfulButton = screen.getByRole('button', { name: 'Helpful' });
    fireEvent.click(helpfulButton);

    await waitFor(() => {
      expect(submitKbFeedbackMock).toHaveBeenCalledWith('game-1', {
        chatSessionId: 'thread-42',
        messageId: 'msg-assistant-1',
        outcome: 'helpful',
        comment: undefined,
      });
    });
  });

  it('submits not-helpful feedback with comment and transforms outcome to snake_case', async () => {
    submitKbFeedbackMock.mockResolvedValue(undefined);

    render(
      <ChatMessageList
        {...defaultProps}
        messages={[assistantMessage]}
        gameId="game-1"
        threadId="thread-42"
      />
    );

    fireEvent.click(screen.getByRole('button', { name: 'Not helpful' }));

    await waitFor(() => {
      expect(submitKbFeedbackMock).toHaveBeenCalledWith('game-1', {
        chatSessionId: 'thread-42',
        messageId: 'msg-assistant-1',
        outcome: 'not_helpful',
        comment: 'Bad response',
      });
    });
  });

  it('silently swallows API errors on feedback submission (no error bubbles to user)', async () => {
    submitKbFeedbackMock.mockRejectedValue(new Error('Network down'));

    // If error bubbled, React would log or throw — capture console.error to ensure none
    const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    render(
      <ChatMessageList
        {...defaultProps}
        messages={[assistantMessage]}
        gameId="game-1"
        threadId="thread-42"
      />
    );

    fireEvent.click(screen.getByRole('button', { name: 'Helpful' }));

    await waitFor(() => {
      expect(submitKbFeedbackMock).toHaveBeenCalled();
    });

    // Key assertion: error did NOT re-throw or show toast (silent catch block at line 124-125)
    expect(consoleErrorSpy).not.toHaveBeenCalledWith(
      expect.stringContaining('Network down'),
      expect.anything()
    );

    consoleErrorSpy.mockRestore();
  });

  it('isolates feedback state per message — setting feedback on msg-1 does not affect msg-2', async () => {
    submitKbFeedbackMock.mockResolvedValue(undefined);

    const messages: ChatMessageItem[] = [
      { id: 'msg-a', role: 'assistant', content: 'Risposta A' },
      { id: 'msg-b', role: 'assistant', content: 'Risposta B' },
    ];

    render(
      <ChatMessageList {...defaultProps} messages={messages} gameId="game-1" threadId="thread-42" />
    );

    const feedbackStubs = screen.getAllByTestId('feedback-buttons');
    expect(feedbackStubs).toHaveLength(2);
    expect(feedbackStubs[0]).toHaveAttribute('data-value', 'null');
    expect(feedbackStubs[1]).toHaveAttribute('data-value', 'null');

    // Click helpful inside the first feedback stub
    const firstHelpful = feedbackStubs[0].querySelector('button') as HTMLButtonElement;
    fireEvent.click(firstHelpful);

    await waitFor(() => {
      // After the round-trip, msg-a should be 'helpful'; msg-b unchanged
      const updatedStubs = screen.getAllByTestId('feedback-buttons');
      expect(updatedStubs[0]).toHaveAttribute('data-value', 'helpful');
      expect(updatedStubs[1]).toHaveAttribute('data-value', 'null');
    });
  });

  it('hides feedback buttons when gameId is missing', () => {
    render(
      <ChatMessageList {...defaultProps} messages={[assistantMessage]} threadId="thread-42" />
    );
    expect(screen.queryByTestId('feedback-buttons')).toBeNull();
  });

  it('hides feedback buttons when threadId is missing', () => {
    render(<ChatMessageList {...defaultProps} messages={[assistantMessage]} gameId="game-1" />);
    expect(screen.queryByTestId('feedback-buttons')).toBeNull();
  });

  it('does NOT render feedback buttons on user messages', () => {
    const userMessage: ChatMessageItem = {
      id: 'msg-user-1',
      role: 'user',
      content: 'Quante carte posso giocare?',
    };
    render(
      <ChatMessageList
        {...defaultProps}
        messages={[userMessage]}
        gameId="game-1"
        threadId="thread-42"
      />
    );
    expect(screen.queryByTestId('feedback-buttons')).toBeNull();
  });
});

// ── Tests: Characterization — Streaming (#292 safety net) ─────────────────────

describe('ChatMessageList — streaming characterization', () => {
  it('updates streaming bubble content when currentAnswer changes across rerenders', () => {
    const { rerender } = render(
      <ChatMessageList
        {...defaultProps}
        messages={[]}
        streamState={{ ...baseStream, isStreaming: true, currentAnswer: 'Ciao' }}
      />
    );

    expect(screen.getByTestId('message-streaming')).toHaveTextContent('Ciao');

    rerender(
      <ChatMessageList
        {...defaultProps}
        messages={[]}
        streamState={{
          ...baseStream,
          isStreaming: true,
          currentAnswer: 'Ciao, come posso aiutarti?',
        }}
      />
    );

    expect(screen.getByTestId('message-streaming')).toHaveTextContent('Ciao, come posso aiutarti?');
  });

  it('shows status message with role="status" and aria-live="polite"', () => {
    render(
      <ChatMessageList
        {...defaultProps}
        messages={[]}
        streamState={{ ...baseStream, statusMessage: 'Collegamento in corso...' }}
      />
    );

    const status = screen.getByTestId('stream-status');
    expect(status).toHaveAttribute('role', 'status');
    expect(status).toHaveAttribute('aria-live', 'polite');
    expect(status).toHaveTextContent('Collegamento in corso...');
  });
});
