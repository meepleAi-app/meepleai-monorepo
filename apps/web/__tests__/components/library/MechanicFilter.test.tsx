/**
 * MechanicFilter Component Tests
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { MechanicFilter } from '@/components/library/MechanicFilter';

describe('MechanicFilter', () => {
  const mechanics = ['engine-building', 'area-control', 'deck-building'];

  it('renders filter chips for each mechanic', () => {
    render(<MechanicFilter mechanics={mechanics} selected={[]} onSelect={vi.fn()} />);
    expect(screen.getByText(/engine building/i)).toBeInTheDocument();
    expect(screen.getByText(/area control/i)).toBeInTheDocument();
    expect(screen.getByText(/deck building/i)).toBeInTheDocument();
  });

  it('renders no chips when mechanics array is empty', () => {
    const { container } = render(
      <MechanicFilter mechanics={[]} selected={[]} onSelect={vi.fn()} />
    );
    expect(container.querySelectorAll('button')).toHaveLength(0);
  });

  it('highlights selected mechanic', () => {
    render(<MechanicFilter mechanics={mechanics} selected={['area-control']} onSelect={vi.fn()} />);
    const chip = screen.getByText(/area control/i).closest('button');
    expect(chip).toHaveClass('bg-[#f0a030]');
  });

  it('does not highlight unselected mechanics', () => {
    render(<MechanicFilter mechanics={mechanics} selected={['area-control']} onSelect={vi.fn()} />);
    const chip = screen.getByText(/engine building/i).closest('button');
    expect(chip).toHaveClass('bg-[#21262d]');
    expect(chip).not.toHaveClass('bg-[#f0a030]');
  });

  it('calls onSelect when chip clicked', () => {
    const onSelect = vi.fn();
    render(<MechanicFilter mechanics={mechanics} selected={[]} onSelect={onSelect} />);
    fireEvent.click(screen.getByText(/engine building/i));
    expect(onSelect).toHaveBeenCalledWith('engine-building');
  });

  it('calls onSelect with correct mechanic key for each chip', () => {
    const onSelect = vi.fn();
    render(<MechanicFilter mechanics={mechanics} selected={[]} onSelect={onSelect} />);

    fireEvent.click(screen.getByText(/deck building/i));
    expect(onSelect).toHaveBeenCalledWith('deck-building');
  });

  it('highlights multiple selected mechanics', () => {
    render(
      <MechanicFilter
        mechanics={mechanics}
        selected={['engine-building', 'deck-building']}
        onSelect={vi.fn()}
      />
    );
    const engineChip = screen.getByText(/engine building/i).closest('button');
    const deckChip = screen.getByText(/deck building/i).closest('button');
    const areaChip = screen.getByText(/area control/i).closest('button');

    expect(engineChip).toHaveClass('bg-[#f0a030]');
    expect(deckChip).toHaveClass('bg-[#f0a030]');
    expect(areaChip).not.toHaveClass('bg-[#f0a030]');
  });

  it('falls back to raw mechanic key when label not found', () => {
    render(<MechanicFilter mechanics={['unknown-mechanic']} selected={[]} onSelect={vi.fn()} />);
    expect(screen.getByText('unknown-mechanic')).toBeInTheDocument();
  });

  it('renders all 15 known mechanics', () => {
    const allMechanics = [
      'engine-building',
      'area-control',
      'deck-building',
      'worker-placement',
      'cooperative',
      'competitive',
      'dice-rolling',
      'puzzle-abstract',
      'narrative-rpg',
      'tile-placement',
      'trading',
      'set-collection',
      'dungeon-crawler',
      'route-building',
      'social-deduction',
    ];
    render(<MechanicFilter mechanics={allMechanics} selected={[]} onSelect={vi.fn()} />);
    expect(screen.getAllByRole('button')).toHaveLength(15);
  });
});
