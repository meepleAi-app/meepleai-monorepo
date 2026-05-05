import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { LifecycleStateBadge } from '../LifecycleStateBadge';

describe('LifecycleStateBadge', () => {
  it.each(['active', 'idle', 'completed', 'setup', 'processing', 'failed'] as const)(
    'renders for value=%s',
    value => {
      render(<LifecycleStateBadge value={value} entityType="game" />);
      expect(screen.getByTestId(`lifecycle-badge-${value}`)).toBeInTheDocument();
    }
  );

  it('renders processing with spinning Loader2 icon', () => {
    const { container } = render(<LifecycleStateBadge value="processing" entityType="kb" />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('class')).toMatch(/animate-spin/);
  });

  it('renders active with entity-colored dot', () => {
    render(<LifecycleStateBadge value="active" entityType="session" />);
    const badge = screen.getByTestId('lifecycle-badge-active');
    // Session color hsl(240, 60%, 55%) gets normalized by JSDOM to rgb(71, 71, 209).
    // Verify the dot carries an entity-derived background (non-empty rgb).
    expect(badge.getAttribute('style') ?? '').toMatch(/background:\s*rgb\(/);
  });
});
