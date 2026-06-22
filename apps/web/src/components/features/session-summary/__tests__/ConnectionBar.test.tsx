/**
 * ConnectionBar unit tests — Wave D.3 (Issue #756).
 */

import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ConnectionBar } from '../ConnectionBar';
import type { ConnectionBarProps } from '../ConnectionBar';

const PIPS: ConnectionBarProps['pips'] = [
  { entity: 'game', emoji: '🎲', label: 'Gioco', count: 1, href: '#section-game' },
  { entity: 'player', emoji: '👤', label: 'Giocatori', count: 4, href: '#section-player' },
  { entity: 'agent', emoji: '🤖', label: 'Agente', count: 1 },
  { entity: 'chat', emoji: '💬', label: 'Chat', count: 8 },
  { entity: 'kb', emoji: '📄', label: 'Foto', count: 3 },
  { entity: 'event', emoji: '📅', label: 'Serata', count: 0 },
];

const LABELS: ConnectionBarProps['labels'] = {
  title: 'Andamento partita',
  emptyEvent: 'Nessun evento registrato',
};

describe('ConnectionBar', () => {
  it('renders data-slot="connection-bar"', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    expect(document.querySelector('[data-slot="connection-bar"]')).not.toBeNull();
  });

  it('renders one pip per entry', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    expect(document.querySelectorAll('[data-slot="connection-bar-pip"]').length).toBe(6);
  });

  it('uses role="status" on the strip container', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    const strip = document.querySelector('[data-slot="connection-bar"]')!;
    expect(strip.getAttribute('role')).toBe('status');
  });

  it('uses aria-label from labels.title', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    const strip = document.querySelector('[data-slot="connection-bar"]')!;
    expect(strip.getAttribute('aria-label')).toBe('Andamento partita');
  });

  it('marks empty pips with data-empty="true"', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    const eventPip = document.querySelector('[data-entity="event"]')!;
    expect(eventPip.getAttribute('data-empty')).toBe('true');
  });

  it('non-empty pips do NOT have data-empty', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    const gamePip = document.querySelector('[data-entity="game"]')!;
    expect(gamePip.getAttribute('data-empty')).toBeNull();
  });

  it('renders <a> tags when href is provided', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    const gamePip = document.querySelector('[data-entity="game"]')! as HTMLElement;
    expect(gamePip.tagName).toBe('A');
    expect(gamePip.getAttribute('href')).toBe('#section-game');
  });

  it('renders <span> tags when href is omitted', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    const agentPip = document.querySelector('[data-entity="agent"]')! as HTMLElement;
    expect(agentPip.tagName).toBe('SPAN');
  });

  it('shows count badge for non-empty pips', () => {
    const { container } = render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    expect(container.textContent).toContain('1');
    expect(container.textContent).toContain('4');
    expect(container.textContent).toContain('8');
  });

  it('sets title=emptyEvent on empty pips', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    const eventPip = document.querySelector('[data-entity="event"]')!;
    expect(eventPip.getAttribute('title')).toBe('Nessun evento registrato');
  });

  it('handles empty pip array', () => {
    render(<ConnectionBar pips={[]} labels={LABELS} />);
    expect(document.querySelectorAll('[data-slot="connection-bar-pip"]').length).toBe(0);
  });

  // #1094 Real-C-misc regression guard: opacity-0.6 must be scoped to the
  // emoji <span aria-hidden="true"> only, NOT the wrapper. Wrapper-level
  // opacity dims the label text → catastrophic AA fail (~2.94:1).
  it('scopes opacity dim to emoji span only on empty pips (not wrapper) — #1094 Real-C-misc', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    // Find any empty pip (count === 0)
    const emptyPip = document.querySelector(
      '[data-slot="connection-bar-pip"][data-empty="true"]'
    ) as HTMLElement;
    expect(emptyPip).not.toBeNull();
    // Wrapper MUST NOT carry opacity (would fail AA on label text)
    expect(emptyPip.style.opacity).toBe('');
    // Emoji span (first aria-hidden child) MUST carry opacity 0.6
    const emojiSpan = emptyPip.querySelector('[aria-hidden="true"]') as HTMLElement;
    expect(emojiSpan).not.toBeNull();
    expect(emojiSpan.style.opacity).toBe('0.6');
  });

  it('label span on empty pip has no opacity dim (AA compliance) — #1094 Real-C-misc', () => {
    render(<ConnectionBar pips={PIPS} labels={LABELS} />);
    const emptyPip = document.querySelector(
      '[data-slot="connection-bar-pip"][data-empty="true"]'
    ) as HTMLElement;
    expect(emptyPip).not.toBeNull();
    // Last <span> is the label; it must NOT carry opacity
    const spans = emptyPip.querySelectorAll('span');
    const labelSpan = spans[spans.length - 1] as HTMLElement;
    expect(labelSpan.style.opacity).toBe('');
  });
});
