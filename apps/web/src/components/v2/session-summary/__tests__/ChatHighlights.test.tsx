/**
 * ChatHighlights unit tests — Wave D.3 (Issue #756).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ChatHighlights } from '../ChatHighlights';
import type { ChatHighlight, ChatHighlightsProps } from '../ChatHighlights';

const HIGHLIGHTS: ChatHighlight[] = [
  {
    id: 'h1',
    authorId: 'u1',
    authorName: 'Marco',
    message: 'Quanto valgono i bird con uova multiple?',
    timestamp: '2026-04-23T14:58:00Z',
  },
  {
    id: 'h2',
    authorId: 'agent',
    authorName: 'Agente',
    message: 'I bird con simbolo uovo nell’angolo valgono 1 punto bonus per ogni uovo.',
    timestamp: '2026-04-23T15:02:00Z',
  },
  {
    id: 'h3',
    authorId: 'u1',
    authorName: 'Marco',
    message: 'Posso giocare un bird sopra il limite?',
    timestamp: '2026-04-23T15:08:00Z',
  },
];

const LABELS: ChatHighlightsProps['labels'] = {
  title: 'Chat highlights',
  emptyTitle: 'Nessun highlight',
  emptyDescription: 'I momenti salienti appariranno qui',
};

describe('ChatHighlights', () => {
  it('renders data-slot="chat-highlights"', () => {
    render(<ChatHighlights highlights={HIGHLIGHTS} labels={LABELS} />);
    expect(document.querySelector('[data-slot="chat-highlights"]')).not.toBeNull();
  });

  it('renders empty state when highlights array is empty', () => {
    render(<ChatHighlights highlights={[]} labels={LABELS} />);
    const root = document.querySelector('[data-slot="chat-highlights"]')!;
    expect(root.getAttribute('data-empty')).toBe('true');
    expect(screen.getByText('Nessun highlight')).toBeTruthy();
    expect(screen.getByText('I momenti salienti appariranno qui')).toBeTruthy();
  });

  it('renders one item per highlight', () => {
    render(<ChatHighlights highlights={HIGHLIGHTS} labels={LABELS} />);
    expect(document.querySelectorAll('[data-slot="chat-highlight-item"]').length).toBe(3);
  });

  it('marks agent highlights with data-agent="true"', () => {
    render(<ChatHighlights highlights={HIGHLIGHTS} labels={LABELS} />);
    const items = document.querySelectorAll('[data-slot="chat-highlight-item"]');
    expect(items[0].getAttribute('data-agent')).toBeNull();
    expect(items[1].getAttribute('data-agent')).toBe('true');
    expect(items[2].getAttribute('data-agent')).toBeNull();
  });

  it('renders message text per highlight', () => {
    render(<ChatHighlights highlights={HIGHLIGHTS} labels={LABELS} />);
    expect(screen.getByText('Quanto valgono i bird con uova multiple?')).toBeTruthy();
    expect(screen.getByText('Posso giocare un bird sopra il limite?')).toBeTruthy();
  });

  it('renders authorName per highlight', () => {
    render(<ChatHighlights highlights={HIGHLIGHTS} labels={LABELS} />);
    expect(screen.getAllByText('Marco').length).toBeGreaterThan(0);
    expect(screen.getByText('Agente')).toBeTruthy();
  });

  it('uses role="list" on the highlights ul', () => {
    render(<ChatHighlights highlights={HIGHLIGHTS} labels={LABELS} />);
    const ul = document.querySelector('ul[role="list"]');
    expect(ul).not.toBeNull();
  });

  it('marks data-author per item', () => {
    render(<ChatHighlights highlights={HIGHLIGHTS} labels={LABELS} />);
    const items = document.querySelectorAll('[data-slot="chat-highlight-item"]');
    expect(items[0].getAttribute('data-author')).toBe('u1');
    expect(items[1].getAttribute('data-author')).toBe('agent');
  });

  it('uses isAgent override when provided', () => {
    render(
      <ChatHighlights
        highlights={HIGHLIGHTS}
        labels={LABELS}
        // Mark all u1 as agent (custom predicate)
        isAgent={h => h.authorId === 'u1'}
      />
    );
    const items = document.querySelectorAll('[data-slot="chat-highlight-item"]');
    expect(items[0].getAttribute('data-agent')).toBe('true'); // u1 marked agent
    expect(items[1].getAttribute('data-agent')).toBeNull(); // agent NOT
  });

  it('applies className when provided', () => {
    render(<ChatHighlights highlights={HIGHLIGHTS} labels={LABELS} className="custom-chat" />);
    const root = document.querySelector('[data-slot="chat-highlights"]')!;
    expect(root.classList.contains('custom-chat')).toBe(true);
  });

  it('uses role="status" on empty state', () => {
    render(<ChatHighlights highlights={[]} labels={LABELS} />);
    const root = document.querySelector('[data-slot="chat-highlights"]')!;
    expect(root.getAttribute('role')).toBe('status');
  });
});
