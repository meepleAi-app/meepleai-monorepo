/**
 * ChatInfoPanel — F11 #1974 polish regression tests.
 *
 * Verifies the i18n cleanup (chat.infoPanel.* keys) + the new optional
 * agent meta block introduced by the audit follow-on.
 */

import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { describe, it, expect, vi } from 'vitest';

import { ChatInfoPanel } from '../ChatInfoPanel';

// Mock the citation badge so this suite doesn't have to feed it a full
// citation contract — its only purpose here is to verify the panel slot
// renders when citations are non-empty.
vi.mock('../CitationBadge', () => ({
  CitationBadge: ({ citation }: { citation: { documentId: string; pageNumber: number } }) => (
    <span data-testid={`citation-${citation.documentId}-${citation.pageNumber}`}>citation</span>
  ),
}));

const MESSAGES: Record<string, string> = {
  'chat.infoPanel.linkedGameLabel': 'Linked game',
  'chat.infoPanel.citationsHeading': 'Citations ({count})',
  'chat.infoPanel.suggestedQuestionsHeading': 'Suggested questions',
  'chat.infoPanel.agentSectionHeading': 'Agent',
  'chat.infoPanel.agentTypeLabel': 'Type',
};

function renderPanel(props: Partial<React.ComponentProps<typeof ChatInfoPanel>> = {}) {
  const defaultProps: React.ComponentProps<typeof ChatInfoPanel> = {
    game: null,
    citations: [],
    suggestedQuestions: [],
    onQuestionClick: vi.fn(),
  };
  return render(
    <IntlProvider locale="en" messages={MESSAGES}>
      <ChatInfoPanel {...defaultProps} {...props} />
    </IntlProvider>
  );
}

describe('ChatInfoPanel — F11 #1974 polish', () => {
  it('renders the root slot wrapper', () => {
    const { container } = renderPanel();
    expect(container.querySelector('[data-slot="chat-info-panel"]')).toBeInTheDocument();
  });

  it('does NOT render the agent block when no agent prop is provided', () => {
    const { container } = renderPanel();
    expect(container.querySelector('[data-slot="chat-info-panel-agent"]')).not.toBeInTheDocument();
  });

  it('renders the agent block (name + heading) when agent prop is provided', () => {
    renderPanel({ agent: { name: 'Tutor' } });
    expect(screen.getByText('Agent')).toBeInTheDocument();
    expect(screen.getByText('Tutor')).toBeInTheDocument();
  });

  it('renders the agent typology label when provided', () => {
    renderPanel({ agent: { name: 'Tutor', typology: 'tutor' } });
    expect(screen.getByText(/Type: tutor/)).toBeInTheDocument();
  });

  it('does NOT render the typology line when agent.typology is omitted', () => {
    renderPanel({ agent: { name: 'Auto (Orchestrator)' } });
    expect(screen.queryByText(/Type:/)).not.toBeInTheDocument();
  });

  it('renders the linked-game label from i18n when game prop is provided', () => {
    renderPanel({ game: { id: 'g1', title: 'Catan' } });
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Linked game')).toBeInTheDocument();
  });

  it('renders the citations heading with the count baked in', () => {
    renderPanel({
      citations: [
        { documentId: 'd1', pageNumber: 1 },
        { documentId: 'd2', pageNumber: 2 },
        { documentId: 'd3', pageNumber: 3 },
        // @ts-expect-error — test fixture, only the fields the badge mock reads matter.
      ] as Citation[],
    });
    expect(screen.getByText('Citations (3)')).toBeInTheDocument();
  });

  it('renders the suggested-questions heading + buttons from i18n', () => {
    renderPanel({ suggestedQuestions: ['How do I win?', 'Best opening?'] });
    expect(screen.getByText('Suggested questions')).toBeInTheDocument();
    expect(screen.getByText('How do I win?')).toBeInTheDocument();
    expect(screen.getByText('Best opening?')).toBeInTheDocument();
  });
});
