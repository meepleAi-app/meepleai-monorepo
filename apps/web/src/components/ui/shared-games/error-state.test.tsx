import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ErrorState } from './error-state';

describe('ErrorState (v2)', () => {
  const defaults = {
    title: 'Impossibile caricare il catalogo',
    description: 'Riprova tra qualche secondo.',
    retryLabel: 'Riprova',
    onRetry: vi.fn(),
  };

  it('renders title and description', () => {
    render(<ErrorState {...defaults} />);
    expect(screen.getByText('Impossibile caricare il catalogo')).toBeInTheDocument();
    expect(screen.getByText('Riprova tra qualche secondo.')).toBeInTheDocument();
  });

  it('exposes role=alert for live announcement', () => {
    const { container } = render(<ErrorState {...defaults} />);
    expect(container.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('emits data-slot=shared-games-error-state', () => {
    const { container } = render(<ErrorState {...defaults} />);
    expect(container.querySelector('[data-slot="shared-games-error-state"]')).not.toBeNull();
  });

  it('renders retry button with provided label', () => {
    render(<ErrorState {...defaults} retryLabel="Try again" />);
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('calls onRetry when button clicked', () => {
    const onRetry = vi.fn();
    render(<ErrorState {...defaults} onRetry={onRetry} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('renders the 🛑 icon as aria-hidden decoration', () => {
    render(<ErrorState {...defaults} />);
    expect(screen.getByText('🛑')).toHaveAttribute('aria-hidden', 'true');
  });
});
