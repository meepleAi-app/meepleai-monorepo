import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GameWithKbList } from '@/components/chat/game-with-kb-list';

describe('GameWithKbList', () => {
  const games = [
    {
      gameId: '1',
      title: 'Catan',
      imageUrl: null,
      overallKbStatus: 'ready' as const,
      rulebooks: [],
    },
    {
      gameId: '2',
      title: 'Wingspan',
      imageUrl: null,
      overallKbStatus: 'processing' as const,
      rulebooks: [],
    },
    {
      gameId: '3',
      title: 'Terraforming Mars',
      imageUrl: null,
      overallKbStatus: 'ready' as const,
      rulebooks: [],
    },
  ];

  it('renders all games', () => {
    render(<GameWithKbList games={games} onSelect={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
  });

  it('disables processing games', () => {
    render(<GameWithKbList games={games} onSelect={vi.fn()} />);
    const wingspanButton = screen.getByText('Wingspan').closest('button');
    expect(wingspanButton).toHaveAttribute('aria-disabled', 'true');
  });

  it('calls onSelect for ready games', () => {
    const onSelect = vi.fn();
    render(<GameWithKbList games={games} onSelect={onSelect} />);
    fireEvent.click(screen.getByText('Catan'));
    expect(onSelect).toHaveBeenCalledWith('1');
  });

  it('does not call onSelect for processing games', () => {
    const onSelect = vi.fn();
    render(<GameWithKbList games={games} onSelect={onSelect} />);
    fireEvent.click(screen.getByText('Wingspan'));
    expect(onSelect).not.toHaveBeenCalled();
  });
});
