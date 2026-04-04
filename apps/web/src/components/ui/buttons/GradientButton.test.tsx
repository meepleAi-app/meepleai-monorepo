import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GradientButton } from './GradientButton';

describe('GradientButton', () => {
  it('renders children', () => {
    render(<GradientButton>Click me</GradientButton>);
    expect(screen.getByRole('button', { name: 'Click me' })).toBeInTheDocument();
  });

  it('applies gradient-primary class', () => {
    render(<GradientButton>Action</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('gradient-primary');
  });

  it('handles click events', () => {
    const onClick = vi.fn();
    render(<GradientButton onClick={onClick}>Action</GradientButton>);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('renders disabled state', () => {
    render(<GradientButton disabled>Action</GradientButton>);
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('shows loading spinner and disables button', () => {
    render(<GradientButton loading>Action</GradientButton>);
    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-busy', 'true');
  });

  it('renders full width when fullWidth is true', () => {
    render(<GradientButton fullWidth>Action</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('w-full');
  });

  it('supports size variants', () => {
    const { rerender } = render(<GradientButton size="sm">Small</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('h-9');

    rerender(<GradientButton size="lg">Large</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('h-12');
  });

  it('merges custom className', () => {
    render(<GradientButton className="mt-4">Action</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('gradient-primary');
    expect(screen.getByRole('button')).toHaveClass('mt-4');
  });
});
