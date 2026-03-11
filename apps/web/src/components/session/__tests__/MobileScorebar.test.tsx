import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MobileScorebar } from '../MobileScorebar';

const players = [
  { id: 'p1', name: 'Alice', score: 10 },
  { id: 'p2', name: 'Bob', score: 15 },
  { id: 'p3', name: 'Carol', score: 8 },
];

describe('MobileScorebar', () => {
  it('renders all player mini-cards', () => {
    render(<MobileScorebar players={players} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Carol')).toBeInTheDocument();
  });

  it('shows scores', () => {
    render(<MobileScorebar players={players} />);
    expect(screen.getByText('10')).toBeInTheDocument();
    expect(screen.getByText('15')).toBeInTheDocument();
  });

  it('is hidden on desktop (lg:hidden)', () => {
    render(<MobileScorebar players={players} />);
    const bar = screen.getByTestId('mobile-scorebar');
    expect(bar.className).toContain('lg:hidden');
  });
});
