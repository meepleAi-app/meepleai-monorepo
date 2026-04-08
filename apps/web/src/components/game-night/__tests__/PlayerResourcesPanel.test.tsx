import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { PlayerResourcesPanel } from '../PlayerResourcesPanel';
import type { PlayerResource } from '@/stores/game-night/types';

const mockResources: PlayerResource[] = [
  { participantId: 'p1', playerName: 'Marco', resources: { wood: 3, ore: 2 } },
  { participantId: 'p2', playerName: 'Sara', resources: { wood: 1, ore: 4 } },
];

describe('PlayerResourcesPanel', () => {
  it('renders all players with their resources', () => {
    render(<PlayerResourcesPanel resources={mockResources} onUpdate={vi.fn()} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('Sara')).toBeInTheDocument();
  });

  it('calls onUpdate when increment button is clicked', () => {
    const onUpdate = vi.fn();
    render(<PlayerResourcesPanel resources={mockResources} onUpdate={onUpdate} />);
    const incrementButtons = screen.getAllByLabelText(/Incrementa wood/);
    fireEvent.click(incrementButtons[0]); // Marco's wood +
    expect(onUpdate).toHaveBeenCalledWith('p1', 'wood', 4);
  });

  it('calls onUpdate with min 0 when decrement button is clicked', () => {
    const onUpdate = vi.fn();
    const resources: PlayerResource[] = [
      { participantId: 'p1', playerName: 'Marco', resources: { gold: 0 } },
    ];
    render(<PlayerResourcesPanel resources={resources} onUpdate={onUpdate} />);
    fireEvent.click(screen.getByLabelText(/Decrementa gold/));
    expect(onUpdate).toHaveBeenCalledWith('p1', 'gold', 0);
  });
});
