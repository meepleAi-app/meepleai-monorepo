import React from 'react';

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { CardBackComposer } from '../CardBackComposer';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: React.PropsWithChildren<{ href: string }>) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('CardBackComposer', () => {
  it('renders blocks for game entity with provided data', () => {
    render(
      <CardBackComposer
        entity="game"
        blockData={{
          stats: { type: 'stats', entries: [{ label: 'Plays', value: 12 }] },
          actions: { type: 'actions', actions: [{ label: 'Play', onClick: vi.fn() }] },
        }}
      />
    );
    expect(screen.getByText('Plays')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('→ Play')).toBeInTheDocument();
  });

  it('renders empty state for blocks with no data', () => {
    render(<CardBackComposer entity="location" blockData={{}} />);
    expect(screen.getAllByText('No data yet').length).toBeGreaterThan(0);
  });

  it('renders blocks in registry order', () => {
    const { container } = render(
      <CardBackComposer
        entity="session"
        blockData={{
          ranking: { type: 'ranking', players: [{ name: 'Marco', score: 87, position: 1 }] },
          timeline: { type: 'timeline', events: [{ time: '20:30', label: 'Start' }] },
          actions: { type: 'actions', actions: [{ label: 'Score', onClick: vi.fn() }] },
        }}
      />
    );
    const titles = container.querySelectorAll('h4');
    expect(titles[0]?.textContent).toBe('Classifica');
    expect(titles[1]?.textContent).toBe('Timeline');
    expect(titles[2]?.textContent).toBe('Azioni');
  });

  it('skips blocks not in the registry for that entity', () => {
    render(
      <CardBackComposer
        entity="game"
        blockData={{
          stats: { type: 'stats', entries: [{ label: 'X', value: 1 }] },
          ranking: { type: 'ranking', players: [] },
        }}
      />
    );
    expect(screen.queryByText('Classifica')).not.toBeInTheDocument();
  });
});
