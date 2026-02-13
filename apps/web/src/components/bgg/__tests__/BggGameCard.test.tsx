/**
 * BggGameCard Tests - Issue #4141
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { BggGameCard } from '../BggGameCard';
import type { BggSearchResult } from '@/types/bgg';

const mockGame: BggSearchResult = {
  id: 13,
  name: 'Catan',
  yearPublished: 1995,
  thumbnail: 'https://example.com/catan.jpg',
};

const mockGameNoThumbnail: BggSearchResult = {
  id: 822,
  name: 'Carcassonne',
  yearPublished: 2000,
  thumbnail: null,
};

describe('BggGameCard', () => {
  it('should render game name and year', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Published: 1995')).toBeInTheDocument();
  });

  it('should render thumbnail when available', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    const img = screen.getByAltText('Catan');
    expect(img).toHaveAttribute('src', 'https://example.com/catan.jpg');
  });

  it('should show placeholder when thumbnail is null', () => {
    render(<BggGameCard game={mockGameNoThumbnail} onSelect={vi.fn()} />);

    expect(screen.getByText('No image')).toBeInTheDocument();
  });

  it('should call onSelect with game ID when clicked', () => {
    const mockOnSelect = vi.fn();
    render(<BggGameCard game={mockGame} onSelect={mockOnSelect} />);

    const button = screen.getByRole('button');
    button.click();

    expect(mockOnSelect).toHaveBeenCalledWith(13);
  });

  it('should show selected state with check icon', () => {
    render(<BggGameCard game={mockGame} selected={true} onSelect={vi.fn()} />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-pressed', 'true');
    expect(button).toHaveClass('border-amber-500');
  });

  it('should not show check icon when not selected', () => {
    render(<BggGameCard game={mockGame} selected={false} onSelect={vi.fn()} />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-pressed', 'false');
  });

  it('should have accessible label', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    expect(screen.getByLabelText('Select Catan (1995)')).toBeInTheDocument();
  });

  it('should apply glassmorphism styling', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    const button = screen.getByRole('button');
    expect(button).toHaveClass('bg-white/70', 'backdrop-blur-md');
  });

  it('should have hover state', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    const button = screen.getByRole('button');
    expect(button).toHaveClass('hover:bg-white/90');
  });

  it('should have focus ring for keyboard navigation', () => {
    render(<BggGameCard game={mockGame} onSelect={vi.fn()} />);

    const button = screen.getByRole('button');
    expect(button).toHaveClass('focus:ring-2', 'focus:ring-amber-500');
  });

  it('should truncate long game names', () => {
    const longNameGame: BggSearchResult = {
      id: 999,
      name: 'Very Long Game Name That Should Be Truncated In The UI',
      yearPublished: 2024,
      thumbnail: null,
    };

    const { container } = render(<BggGameCard game={longNameGame} onSelect={vi.fn()} />);

    const title = container.querySelector('.truncate');
    expect(title).toBeInTheDocument();
  });
});
