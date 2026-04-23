import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { FocusCard } from '../FocusCard';
import { MeepleCard, __resetDeprecationDedup } from '../../MeepleCard';

describe('FocusCard connections path', () => {
  beforeEach(() => {
    __resetDeprecationDedup();
  });

  it('S2: connections=[] renders no nav DOM, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<FocusCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(screen.queryByTestId('nav-footer')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(
      <FocusCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });

  it('S3: navItems path emits one warn per instance (deduped)', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const props = {
      entity: 'game' as const,
      title: 'X',
      variant: 'focus' as const,
      navItems: [{ label: 'L', entity: 'session' as const, icon: null, href: '/x' }],
    };
    const { rerender } = render(<MeepleCard {...props} />);
    rerender(<MeepleCard {...props} />);
    expect(warn.mock.calls.filter(c => /navItems.*deprecated/.test(c[0]))).toHaveLength(1);
  });
});

describe('FocusCard adapter path', () => {
  beforeEach(() => {
    __resetDeprecationDedup();
  });

  it('S9: FocusCard with adapter flag keeps 3 interactive elements, aria-labels preserved', () => {
    render(
      <FocusCard
        entity="game"
        title="X"
        __useConnectionsForNavItems
        navItems={[
          { label: '3 sessioni', entity: 'session', count: 3, href: '/s/1', icon: <i /> },
          { label: '2 KB', entity: 'kb', count: 2, href: '/k/1', icon: <i /> },
          {
            label: 'Nuovo',
            entity: 'player',
            count: 0,
            showPlus: true,
            onPlusClick: () => {},
            icon: <i />,
          },
        ]}
      />
    );
    const interactive = [...screen.queryAllByRole('link'), ...screen.queryAllByRole('button')];
    expect(interactive).toHaveLength(3);
    const labels = interactive.map(el => el.getAttribute('aria-label')?.toLowerCase());
    expect(labels.filter(Boolean)).toHaveLength(3);
  });
});
