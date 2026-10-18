/**
 * #4168 (5g-b) — la chat incorporata davanti alla quota del piano.
 *
 * Riceve lo stesso stream della chat (`/agents/qa/stream`) attraverso `useAgentChatStream`. Una
 * domanda fermata dalla quota non è un guasto: niente riquadro rosso, ma l'avviso con i numeri del
 * piano e il link ai piani, come nelle altre viste.
 */

import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { EmbeddedChatView } from '../EmbeddedChatView';

const stream = vi.hoisted(() => ({
  state: {} as Record<string, unknown>,
}));

const idle = {
  statusMessage: null,
  currentAnswer: '',
  followUpQuestions: [],
  isStreaming: false,
  error: null,
  tierLimit: null,
  chatThreadId: null,
  totalTokens: 0,
  debugSteps: [],
  modelDowngrade: null,
  strategyTier: null,
  executionId: null,
  connectionStatus: 'idle',
  retryCount: 0,
};

vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: () => ({
    state: stream.state,
    sendMessage: vi.fn(),
    stopStreaming: vi.fn(),
    reset: vi.fn(),
  }),
}));

const QUOTA_SENTENCE =
  'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3). Il conteggio riparte alle 02:00 (mezzanotte UTC).';

describe('EmbeddedChatView — la quota del piano (#4168, 5g-b)', () => {
  beforeEach(() => {
    stream.state = { ...idle };
  });

  it('shows the plan quota as a notice with the way to the plans, not as an error', () => {
    stream.state = {
      ...idle,
      error: QUOTA_SENTENCE,
      tierLimit: { current: 3, max: 3, upgradeUrl: '/pricing' },
    };

    render(<EmbeddedChatView threadId="thread-1" gameId="game-1" />);

    const notice = screen.getByTestId('tier-limit-exceeded');
    expect(notice).toHaveTextContent(
      'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3)'
    );
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
    // Non è un guasto: il riquadro d'errore non c'è.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  /** Il posto dell'avviso è nella pagina da prima, vuoto, come nelle altre viste. */
  it('keeps the place for the quota notice in the page, empty', () => {
    render(<EmbeddedChatView threadId="thread-1" gameId="game-1" />);

    const region = screen.getByTestId('tier-limit-region');
    expect(region).toHaveAttribute('role', 'status');
    expect(region).toBeEmptyDOMElement();
  });

  /** Un guasto vero resta un errore, nel suo riquadro. */
  it('still shows another error as an error', () => {
    stream.state = { ...idle, error: 'Il servizio AI è temporaneamente non disponibile.' };

    render(<EmbeddedChatView threadId="thread-1" gameId="game-1" />);

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Il servizio AI è temporaneamente non disponibile.'
    );
    expect(screen.queryByTestId('tier-limit-exceeded')).not.toBeInTheDocument();
  });
});
