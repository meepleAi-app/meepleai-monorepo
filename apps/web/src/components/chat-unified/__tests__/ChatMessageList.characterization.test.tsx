/**
 * ChatMessageList Characterization Tests (Issue #292)
 *
 * Separate from ChatMessageList.test.tsx to avoid coupling with the
 * global mocks used by windowed slice tests. Added as safety net before
 * the ChatMessageList refactor (Option ε per spike report).
 *
 * See docs/development/chat-message-api-compatibility.md for the refactor
 * strategy and docs/development/chat-message-list-behavior-baseline.md for
 * the full test priority list from G0.1 audit.
 *
 * Tests included (HIGH + MEDIUM priority from G0.1):
 * - model_downgrade_local_fallback_banner
 * - model_downgrade_upgrade_message
 * - tts_speaker_button_conditional_render
 * - citations_multiple_per_message
 * - last_assistant_message_strategy_tier_badge
 * - strategy_badge_hidden_during_streaming
 * - window_slide_exact_boundary_51_messages
 * - technical_details_panel_visibility
 * - empty_citations_list_no_render
 * - undefined_citations_no_render
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import {
  ChatMessageList,
  type ChatMessageItem,
  type StreamStateForMessages,
} from '../ChatMessageList';

// ── Observable mocks (different from ChatMessageList.test.tsx) ────────────────

vi.mock('../TtsSpeakerButton', () => ({
  TtsSpeakerButton: ({ text }: { text: string }) => (
    <button type="button" data-testid="tts-speaker" data-text={text}>
      speak
    </button>
  ),
}));

vi.mock('../RuleSourceCard', () => ({
  RuleSourceCard: ({
    citations,
  }: {
    citations: Array<{ documentId: string; pageNumber: number }>;
  }) => (
    <div data-testid="rule-source-card" data-count={citations.length}>
      {citations.map((c, i) => (
        <span key={`${c.documentId}-${i}`} data-doc={c.documentId}>
          {c.documentId}
        </span>
      ))}
    </div>
  ),
}));

vi.mock('../ResponseMetaBadge', () => ({
  ResponseMetaBadge: ({ strategyTier }: { strategyTier: string | null }) => (
    <span data-testid="strategy-badge" data-tier={strategyTier ?? 'null'} />
  ),
}));

vi.mock('../TechnicalDetailsPanel', () => ({
  TechnicalDetailsPanel: ({
    debugSteps,
    showDebugLink,
  }: {
    debugSteps: unknown[];
    showDebugLink: boolean;
  }) => (
    <div
      data-testid="tech-details"
      data-steps={debugSteps.length}
      data-admin={String(showDebugLink)}
    />
  ),
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

// ── Tests: Model downgrade ────────────────────────────────────────────────────

describe('ChatMessageList — model downgrade characterization', () => {
  it('renders model downgrade banner when stream has local fallback', () => {
    render(
      <ChatMessageList
        {...defaultProps}
        messages={[]}
        streamState={{
          ...baseStream,
          modelDowngrade: {
            isLocalFallback: true,
            originalModel: 'gpt-4o',
            fallbackModel: 'llama3:8b',
            upgradeMessage: false,
            fallbackReason: 'rate_limited',
          },
        }}
      />
    );
    const banner = screen.getByTestId('model-downgrade-banner');
    expect(banner).toHaveAttribute('role', 'alert');
    expect(banner).toHaveTextContent(/llama3:8b/);
  });

  it('renders premium upgrade link when upgradeMessage=true', () => {
    render(
      <ChatMessageList
        {...defaultProps}
        messages={[]}
        streamState={{
          ...baseStream,
          modelDowngrade: {
            isLocalFallback: false,
            originalModel: 'gpt-4o',
            fallbackModel: 'gpt-3.5-turbo',
            upgradeMessage: true,
            fallbackReason: 'rate_limited',
          },
        }}
      />
    );
    const link = screen.getByRole('link', { name: /premium|pricing|upgrade/i });
    expect(link).toHaveAttribute('href', '/pricing');
  });
});

// ── Tests: TTS ────────────────────────────────────────────────────────────────

describe('ChatMessageList — TTS characterization', () => {
  it('renders TTS speaker button only when isTtsSupported + ttsEnabled both true', () => {
    const msg: ChatMessageItem = { id: 'a', role: 'assistant', content: 'Ciao' };

    const { rerender } = render(
      <ChatMessageList
        {...defaultProps}
        messages={[msg]}
        isTtsSupported={false}
        ttsEnabled={false}
      />
    );
    expect(screen.queryByTestId('tts-speaker')).toBeNull();

    rerender(
      <ChatMessageList
        {...defaultProps}
        messages={[msg]}
        isTtsSupported={true}
        ttsEnabled={false}
      />
    );
    expect(screen.queryByTestId('tts-speaker')).toBeNull();

    rerender(
      <ChatMessageList {...defaultProps} messages={[msg]} isTtsSupported={true} ttsEnabled={true} />
    );
    expect(screen.getByTestId('tts-speaker')).toHaveAttribute('data-text', 'Ciao');
  });
});

// ── Tests: Citations ──────────────────────────────────────────────────────────

describe('ChatMessageList — citations characterization', () => {
  it('passes all citations to RuleSourceCard (not just first)', () => {
    const msg: ChatMessageItem = {
      id: 'a',
      role: 'assistant',
      content: 'Risposta con fonti',
      citations: [
        {
          documentId: 'doc-1',
          pageNumber: 1,
          snippet: 'Regolamento §1',
          relevanceScore: 0.9,
          copyrightTier: 'full',
        },
        {
          documentId: 'doc-2',
          pageNumber: 2,
          snippet: 'Regolamento §2',
          relevanceScore: 0.85,
          copyrightTier: 'full',
        },
        {
          documentId: 'doc-3',
          pageNumber: 3,
          snippet: 'Regolamento §3',
          relevanceScore: 0.8,
          copyrightTier: 'protected',
        },
      ],
    };
    render(<ChatMessageList {...defaultProps} messages={[msg]} gameTitle="Catan" />);

    const card = screen.getByTestId('rule-source-card');
    expect(card).toHaveAttribute('data-count', '3');
    expect(card).toHaveTextContent('doc-1');
    expect(card).toHaveTextContent('doc-2');
    expect(card).toHaveTextContent('doc-3');
  });

  it('does NOT render RuleSourceCard when citations array is empty', () => {
    const msg: ChatMessageItem = {
      id: 'a',
      role: 'assistant',
      content: 'No sources',
      citations: [],
    };
    render(<ChatMessageList {...defaultProps} messages={[msg]} />);
    expect(screen.queryByTestId('rule-source-card')).toBeNull();
  });

  it('does NOT render RuleSourceCard when citations is undefined', () => {
    const msg: ChatMessageItem = {
      id: 'a',
      role: 'assistant',
      content: 'No sources',
    };
    render(<ChatMessageList {...defaultProps} messages={[msg]} />);
    expect(screen.queryByTestId('rule-source-card')).toBeNull();
  });
});

// ── Tests: Strategy tier badge ────────────────────────────────────────────────

describe('ChatMessageList — strategy tier badge characterization', () => {
  it('renders ResponseMetaBadge only on last assistant message when not streaming', () => {
    const messages: ChatMessageItem[] = [
      { id: 'a1', role: 'assistant', content: 'First' },
      { id: 'u1', role: 'user', content: 'Question' },
      { id: 'a2', role: 'assistant', content: 'Second (last)' },
    ];
    render(
      <ChatMessageList
        {...defaultProps}
        messages={messages}
        streamState={{ ...baseStream, strategyTier: 'Balanced' }}
      />
    );

    const badges = screen.getAllByTestId('strategy-badge');
    expect(badges).toHaveLength(1);
    expect(badges[0]).toHaveAttribute('data-tier', 'Balanced');
  });

  it('does NOT render ResponseMetaBadge during streaming', () => {
    const messages: ChatMessageItem[] = [{ id: 'a1', role: 'assistant', content: 'Response' }];
    render(
      <ChatMessageList
        {...defaultProps}
        messages={messages}
        streamState={{ ...baseStream, strategyTier: 'Balanced', isStreaming: true }}
      />
    );
    expect(screen.queryByTestId('strategy-badge')).toBeNull();
  });
});

// ── Tests: Window boundary ────────────────────────────────────────────────────

describe('ChatMessageList — window boundary characterization', () => {
  it('handles exact WINDOW_SIZE+1 boundary (51 messages)', () => {
    const messages = makeMessages(51);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    expect(screen.getAllByTestId(/^message-/).length).toBe(50);
    expect(screen.getByText('Messaggio numero 50')).toBeInTheDocument();
    expect(screen.queryByText('Messaggio numero 0')).toBeNull();
    expect(screen.getByRole('button', { name: /1 messaggi? precedenti/ })).toBeInTheDocument();
  });

  it('loads hidden messages when clicking "1 messaggi precedenti" (from 51)', () => {
    const messages = makeMessages(51);
    render(<ChatMessageList {...defaultProps} messages={messages} />);
    fireEvent.click(screen.getByRole('button', { name: /1 messaggi? precedenti/ }));
    expect(screen.getAllByTestId(/^message-/).length).toBe(51);
    expect(screen.getByText('Messaggio numero 0')).toBeInTheDocument();
  });
});

// ── Tests: Technical details panel ────────────────────────────────────────────

describe('ChatMessageList — technical details panel characterization', () => {
  it('renders TechnicalDetailsPanel only when editor + isLastAssistant + debugSteps.length > 0', () => {
    const msg: ChatMessageItem = { id: 'a', role: 'assistant', content: 'Response' };

    // Case 1: not editor → hidden
    const { rerender } = render(
      <ChatMessageList
        {...defaultProps}
        messages={[msg]}
        isEditor={false}
        streamState={{ ...baseStream, debugSteps: [{ step: 'mock' } as never] }}
      />
    );
    expect(screen.queryByTestId('tech-details')).toBeNull();

    // Case 2: editor + empty debugSteps → hidden
    rerender(
      <ChatMessageList
        {...defaultProps}
        messages={[msg]}
        isEditor={true}
        streamState={{ ...baseStream, debugSteps: [] }}
      />
    );
    expect(screen.queryByTestId('tech-details')).toBeNull();

    // Case 3: editor + debugSteps → visible with isAdmin propagated
    rerender(
      <ChatMessageList
        {...defaultProps}
        messages={[msg]}
        isEditor={true}
        isAdmin={true}
        streamState={{ ...baseStream, debugSteps: [{ step: 'mock' } as never] }}
      />
    );
    const panel = screen.getByTestId('tech-details');
    expect(panel).toHaveAttribute('data-steps', '1');
    expect(panel).toHaveAttribute('data-admin', 'true');
  });
});
