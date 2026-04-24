import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { FeaturedCard } from '../FeaturedCard';
import { MeepleCard, __resetDeprecationDedup } from '../../MeepleCard';

describe('FeaturedCard connections path', () => {
  beforeEach(() => {
    __resetDeprecationDedup();
  });

  it('S2: connections=[] renders no nav DOM, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<FeaturedCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(screen.queryByTestId('nav-footer')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(
      <FeaturedCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });

  it('S3: navItems path emits one warn per instance (deduped)', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const props = {
      entity: 'game' as const,
      title: 'X',
      variant: 'featured' as const,
      navItems: [{ label: 'L', entity: 'session' as const, icon: null, href: '/x' }],
    };
    const { rerender } = render(<MeepleCard {...props} />);
    rerender(<MeepleCard {...props} />);
    expect(warn.mock.calls.filter(c => /navItems.*deprecated/.test(c[0]))).toHaveLength(1);
  });
});
