/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeepleDealtGameCard } from '../MeepleDealtGameCard';

describe('MeepleDealtGameCard', () => {
  const mockGame = {
    id: 'game-1',
    title: 'Catan',
    thumbnailUrl: '/catan.jpg',
  };

  it('renders wrapper with dealt-card testid', () => {
    render(<MeepleDealtGameCard game={mockGame} onRemove={vi.fn()} rotation={3} />);
    const wrapper = screen.getByTestId('dealt-card');
    expect(wrapper).toBeInTheDocument();
  });

  it('displays game title', () => {
    render(<MeepleDealtGameCard game={mockGame} onRemove={vi.fn()} rotation={3} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });
});
