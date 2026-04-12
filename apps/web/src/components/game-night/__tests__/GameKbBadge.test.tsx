import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { GameKbBadge } from '../GameKbBadge';

describe('GameKbBadge', () => {
  it('renders "AI pronto" when isIndexed=true', () => {
    render(<GameKbBadge isIndexed={true} />);
    expect(screen.getByText('AI pronto')).toBeInTheDocument();
    expect(screen.getByTestId('game-kb-badge')).toHaveAttribute('data-indexed', 'true');
  });

  it('renders "Solo manuale" when isIndexed=false', () => {
    render(<GameKbBadge isIndexed={false} />);
    expect(screen.getByText('Solo manuale')).toBeInTheDocument();
    expect(screen.getByTestId('game-kb-badge')).toHaveAttribute('data-indexed', 'false');
  });

  it('renders nothing while loading', () => {
    const { container } = render(<GameKbBadge isIndexed={false} isLoading={true} />);
    expect(container.firstChild).toBeNull();
  });
});
