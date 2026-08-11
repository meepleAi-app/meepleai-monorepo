/**
 * #2564 — axe AA test for ActionLogTimeline broadcast citation chips.
 *
 * Renders the timeline with a chat entry carrying citations[] and runs axe-core.
 * Structural a11y (roles, labels, aria) is enforced here; the color-contrast rule is
 * exercised by the E2E `Frontend - A11y E2E` job (jsdom cannot compute contrast). The
 * chip color reuses the knowledge-base token (--c-kb, same as CitationChip) which is
 * AA-compliant in both themes.
 */

import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { axe } from 'jest-axe';

import { ActionLogTimeline } from '@/components/features/session-live/ActionLogTimeline';
import type {
  ActionLogEntry,
  ActionLogTimelineLabels,
} from '@/components/features/session-live/ActionLogTimeline';

// toHaveNoViolations is extended globally in vitest.setup.tsx

const LABELS: ActionLogTimelineLabels = {
  title: 'Registro azioni',
  emptyLabel: 'Nessuna azione',
  typeScore: 'Punteggio',
  typeTool: 'Strumento',
  typeAgent: 'Agente',
  typeChat: 'Chat',
  typePhoto: 'Foto',
  typeEvent: 'Evento',
  timestampAriaLabel: 'Cronologia azioni',
};

describe('ActionLogTimeline citations — axe AA (#2564)', () => {
  it('has no axe violations with broadcast citation chips', async () => {
    const entries: ActionLogEntry[] = [
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
    ];

    const { container } = render(<ActionLogTimeline entries={entries} labels={LABELS} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
