/**
 * Tests for ActionLogTimeline citation rendering (#2564 AC-SSE-4).
 *
 * Covers:
 *   - A chat entry with citations[] renders read-only citation chips (page + source)
 *   - A chat entry without citations renders no citation chips
 *   - Non-chat entries never render citations
 */

import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import type { ActionLogEntry, ActionLogTimelineLabels } from '../ActionLogTimeline';
import { ActionLogTimeline } from '../ActionLogTimeline';

const LABELS: ActionLogTimelineLabels = {
  title: 'Registro',
  emptyLabel: 'Nessuna azione',
  typeScore: 'Punteggio',
  typeTool: 'Strumento',
  typeAgent: 'Agente',
  typeChat: 'Chat',
  typePhoto: 'Foto',
  typeEvent: 'Evento',
  timestampAriaLabel: 'Cronologia azioni',
};

function renderTimeline(entries: ReadonlyArray<ActionLogEntry>) {
  return render(<ActionLogTimeline entries={entries} labels={LABELS} />);
}

describe('ActionLogTimeline — citations (#2564 AC-SSE-4)', () => {
  it('renders read-only citation chips for a chat entry with citations', () => {
    const { container } = renderTimeline([
      {
        id: 'msg-1',
        type: 'chat',
        authorName: 'agent',
        content: 'Vedi le regole.',
        timestamp: '2026-06-29T10:00:00Z',
        citations: [
          { page: 3, source: 'Setup', snippet: 'Place the board...' },
          { page: 7, source: 'Combat' },
        ],
      },
    ]);

    const chips = container.querySelectorAll('[data-slot="action-log-citation"]');
    expect(chips).toHaveLength(2);
    expect(screen.getByText('Setup')).toBeInTheDocument();
    expect(screen.getByText('Combat')).toBeInTheDocument();
    expect(screen.getByText('p. 3')).toBeInTheDocument();
    expect(screen.getByText('p. 7')).toBeInTheDocument();
  });

  it('renders no citation chips for a chat entry without citations', () => {
    const { container } = renderTimeline([
      {
        id: 'msg-2',
        type: 'chat',
        authorName: 'p-alice',
        content: 'Ciao',
        timestamp: '2026-06-29T10:00:00Z',
      },
    ]);

    expect(container.querySelectorAll('[data-slot="action-log-citation"]')).toHaveLength(0);
  });

  it('never renders citations for non-chat entries', () => {
    const { container } = renderTimeline([
      {
        id: 'sc-1',
        type: 'score',
        authorName: 'p-bob',
        content: '+5',
        timestamp: '2026-06-29T10:00:00Z',
      },
    ]);

    expect(container.querySelectorAll('[data-slot="action-log-citation"]')).toHaveLength(0);
  });
});
