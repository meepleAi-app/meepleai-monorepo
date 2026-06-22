/**
 * ChatHistoryTimeline unit tests — Wave C.2 Task 2
 *
 * 4 tests: loading/error/empty/success states.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ChatHistoryTimeline } from '../ChatHistoryTimeline';

const LABELS = {
  title: 'Storico chat',
  loadingLabel: 'Caricamento storico...',
  errorLabel: 'Impossibile caricare lo storico.',
  retryLabel: 'Riprova',
  empty: 'Nessuna conversazione registrata.',
  emptySubtitle: 'Le conversazioni con questo agente appariranno qui.',
  threadCount: '{count} conversazioni',
  resolvedLabel: 'Risolta',
  unresolvedLabel: 'In corso',
  messagesLabel: '{count} messaggi',
};

const SAMPLE_THREADS = [
  {
    id: 'c1',
    firstMessage: 'Come funziona il bonus?',
    when: '5m fa',
    messageCount: 6,
    resolved: true,
  },
];

describe('ChatHistoryTimeline', () => {
  it('renders data-slot attribute', () => {
    render(<ChatHistoryTimeline state={{ kind: 'loading' }} labels={LABELS} />);
    expect(document.querySelector('[data-slot="agent-detail-chat-history-timeline"]')).toBeTruthy();
  });

  it('error kind: renders retry button', () => {
    const retry = vi.fn();
    render(<ChatHistoryTimeline state={{ kind: 'error', retry }} labels={LABELS} />);
    expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
  });

  it('empty kind: renders empty message', () => {
    render(<ChatHistoryTimeline state={{ kind: 'empty' }} labels={LABELS} />);
    expect(screen.getByText(/nessuna conversazione/i)).toBeInTheDocument();
  });

  it('success kind: renders thread list', () => {
    render(
      <ChatHistoryTimeline state={{ kind: 'success', threads: SAMPLE_THREADS }} labels={LABELS} />
    );
    expect(screen.getByText(/come funziona il bonus/i)).toBeInTheDocument();
  });
});
