import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ManaPips } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import type { ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

const pips: ManaPip[] = [
  { entityType: 'session', count: 5 },
  { entityType: 'kb', count: 2 },
  { entityType: 'agent', count: 1 },
];

describe('ManaPips', () => {
  it('renders up to 3 pips', () => {
    const { container } = render(<ManaPips pips={pips} />);
    const dots = container.querySelectorAll('[data-pip]');
    expect(dots).toHaveLength(3);
  });

  it('shows +N overflow when more than 3 pips', () => {
    const fourPips: ManaPip[] = [
      { entityType: 'session', count: 5 },
      { entityType: 'kb', count: 2 },
      { entityType: 'agent', count: 1 },
      { entityType: 'toolkit', count: 3 },
    ];
    render(<ManaPips pips={fourPips} />);
    expect(screen.getByText('+1')).toBeInTheDocument();
  });

  it('shows count badge when size=md and count > 0', () => {
    render(<ManaPips pips={[{ entityType: 'session', count: 5 }]} size="md" />);
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('does not show badge when size=sm', () => {
    render(<ManaPips pips={[{ entityType: 'session', count: 5 }]} size="sm" />);
    expect(screen.queryByText('5')).not.toBeInTheDocument();
  });

  it('renders nothing when pips array is empty', () => {
    const { container } = render(<ManaPips pips={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
