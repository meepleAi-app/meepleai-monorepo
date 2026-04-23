import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GridCard } from '../GridCard';
import { MeepleCard, __resetDeprecationDedup } from '../../MeepleCard';

describe('GridCard connections path', () => {
  beforeEach(() => {
    __resetDeprecationDedup();
  });

  it('S2: connections=[] renders no nav DOM, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<GridCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(screen.queryByTestId('nav-footer')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(
      <GridCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });

  it('S3: navItems path emits one warn per instance (deduped)', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const props = {
      entity: 'game' as const,
      title: 'X',
      variant: 'grid' as const,
      navItems: [{ label: 'L', entity: 'session' as const, icon: null, href: '/x' }],
    };
    const { rerender } = render(<MeepleCard {...props} />);
    rerender(<MeepleCard {...props} />);
    expect(warn.mock.calls.filter(c => /navItems.*deprecated/.test(c[0]))).toHaveLength(1);
  });
});

describe('GridCard adapter path', () => {
  beforeEach(() => {
    __resetDeprecationDedup();
  });

  it('S4: navItems adapter path renders ConnectionChipStrip with equivalent DOM', () => {
    render(
      <GridCard
        entity="game"
        title="X"
        navItems={[
          {
            label: '3 sessioni',
            entity: 'session',
            count: 3,
            href: '/s',
            icon: <i data-testid="i1" />,
          },
        ]}
        __useConnectionsForNavItems
      />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
    expect(screen.getByTestId('i1')).toBeInTheDocument();
    expect(screen.queryByTestId('nav-footer')).toBeNull();
    expect(screen.getByRole('link', { name: /3.*session/i })).toBeInTheDocument();
  });
});
