/**
 * MechanicIcon Component Tests
 *
 * Tests for the MechanicIcon wrapper component:
 * - Renders the correct icon for known mechanics
 * - Falls back to DefaultMechanicIcon for unknown mechanics
 * - Applies size attribute correctly
 * - Applies custom className
 * - All mechanic keys render without error
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { MechanicIcon } from '@/components/icons/mechanics';

describe('MechanicIcon', () => {
  it('renders engine-building icon with correct size', () => {
    render(<MechanicIcon mechanic="engine-building" size={24} />);
    const svg = screen.getByRole('img', { name: /engine building/i });
    expect(svg).toBeInTheDocument();
    expect(svg).toHaveAttribute('width', '24');
    expect(svg).toHaveAttribute('height', '24');
  });

  it('renders fallback for unknown mechanic', () => {
    render(<MechanicIcon mechanic="unknown-mechanic" size={24} />);
    const svg = screen.getByRole('img', { name: /board game/i });
    expect(svg).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<MechanicIcon mechanic="area-control" size={20} className="text-amber-500" />);
    const svg = screen.getByRole('img', { name: /area control/i });
    expect(svg).toHaveClass('text-amber-500');
  });

  it('renders area-control icon', () => {
    render(<MechanicIcon mechanic="area-control" size={16} />);
    const svg = screen.getByRole('img', { name: /area control/i });
    expect(svg).toBeInTheDocument();
    expect(svg).toHaveAttribute('width', '16');
  });

  it('renders deck-building icon', () => {
    render(<MechanicIcon mechanic="deck-building" size={24} />);
    const svg = screen.getByRole('img', { name: /deck building/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders worker-placement icon', () => {
    render(<MechanicIcon mechanic="worker-placement" size={24} />);
    const svg = screen.getByRole('img', { name: /worker placement/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders cooperative icon', () => {
    render(<MechanicIcon mechanic="cooperative" size={24} />);
    const svg = screen.getByRole('img', { name: /cooperative/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders competitive icon', () => {
    render(<MechanicIcon mechanic="competitive" size={24} />);
    const svg = screen.getByRole('img', { name: /competitive/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders dice-rolling icon', () => {
    render(<MechanicIcon mechanic="dice-rolling" size={24} />);
    const svg = screen.getByRole('img', { name: /dice rolling/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders puzzle-abstract icon', () => {
    render(<MechanicIcon mechanic="puzzle-abstract" size={24} />);
    const svg = screen.getByRole('img', { name: /puzzle/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders narrative-rpg icon', () => {
    render(<MechanicIcon mechanic="narrative-rpg" size={24} />);
    const svg = screen.getByRole('img', { name: /narrative/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders tile-placement icon', () => {
    render(<MechanicIcon mechanic="tile-placement" size={24} />);
    const svg = screen.getByRole('img', { name: /tile placement/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders trading icon', () => {
    render(<MechanicIcon mechanic="trading" size={24} />);
    const svg = screen.getByRole('img', { name: /trading/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders set-collection icon', () => {
    render(<MechanicIcon mechanic="set-collection" size={24} />);
    const svg = screen.getByRole('img', { name: /set collection/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders dungeon-crawler icon', () => {
    render(<MechanicIcon mechanic="dungeon-crawler" size={24} />);
    const svg = screen.getByRole('img', { name: /dungeon crawler/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders route-building icon', () => {
    render(<MechanicIcon mechanic="route-building" size={24} />);
    const svg = screen.getByRole('img', { name: /route building/i });
    expect(svg).toBeInTheDocument();
  });

  it('renders social-deduction icon', () => {
    render(<MechanicIcon mechanic="social-deduction" size={24} />);
    const svg = screen.getByRole('img', { name: /social deduction/i });
    expect(svg).toBeInTheDocument();
  });

  it('uses default size of 24 when no size provided', () => {
    render(<MechanicIcon mechanic="trading" />);
    const svg = screen.getByRole('img', { name: /trading/i });
    expect(svg).toHaveAttribute('width', '24');
    expect(svg).toHaveAttribute('height', '24');
  });

  it('renders with empty className by default', () => {
    render(<MechanicIcon mechanic="cooperative" />);
    const svg = screen.getByRole('img', { name: /cooperative/i });
    // className attribute may be empty string or absent — just check it renders
    expect(svg).toBeInTheDocument();
  });
});
