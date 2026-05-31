/**
 * @vitest-environment jsdom
 *
 * QueryDrillPanel — Issue #1722 PR 2/4.
 *
 * Right-side drill-down panel for a single AiRequest. Shows query +
 * response snippet + metadata grid. Chunks are unavailable until the
 * BE drill endpoint lands; the panel surfaces a "limited drill" badge
 * in that case (degrade gracefully).
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import type { AiRequest } from '@/lib/api/schemas';

import { QueryDrillPanel } from '../QueryDrillPanel';

const sampleRequest: AiRequest = {
  id: '11111111-1111-1111-1111-111111111111',
  userId: '22222222-2222-2222-2222-222222222222',
  gameId: '33333333-3333-3333-3333-333333333333',
  endpoint: '/api/v1/chat',
  query: 'How many cards can a player hold?',
  responseSnippet: 'Up to 5 cards in hand.',
  latencyMs: 842,
  tokenCount: 1240,
  promptTokens: 980,
  completionTokens: 260,
  confidence: 0.84,
  status: 'Success',
  errorMessage: null,
  ipAddress: null,
  userAgent: null,
  createdAt: '2026-05-30T12:00:00Z',
  model: 'gpt-4o-mini',
  finishReason: 'stop',
};

describe('QueryDrillPanel', () => {
  it('renders nothing when query is null', () => {
    const { container } = render(<QueryDrillPanel query={null} onClose={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders the query text and response snippet for a valid query', () => {
    render(<QueryDrillPanel query={sampleRequest} onClose={vi.fn()} />);
    expect(screen.getByText(sampleRequest.query as string)).toBeInTheDocument();
    expect(screen.getByText(sampleRequest.responseSnippet as string)).toBeInTheDocument();
  });

  it('renders endpoint + model + latency in the header', () => {
    render(<QueryDrillPanel query={sampleRequest} onClose={vi.fn()} />);
    expect(screen.getByText(sampleRequest.endpoint)).toBeInTheDocument();
    expect(screen.getByText(/gpt-4o-mini/)).toBeInTheDocument();
    expect(screen.getByText(/842\s*ms/)).toBeInTheDocument();
  });

  it('surfaces a "limited drill" badge when chunks are unavailable', () => {
    render(<QueryDrillPanel query={sampleRequest} onClose={vi.fn()} />);
    expect(screen.getByText(/limited drill/i)).toBeInTheDocument();
  });

  it('renders the panel with the region role and a descriptive label', () => {
    render(<QueryDrillPanel query={sampleRequest} onClose={vi.fn()} />);
    expect(screen.getByRole('region', { name: /query drill-down/i })).toBeInTheDocument();
  });

  it('calls onClose when the close button is clicked', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<QueryDrillPanel query={sampleRequest} onClose={onClose} />);
    await user.click(screen.getByRole('button', { name: /close drill/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
