/**
 * Wave A.4 follow-up (Issue #615) — ErrorState rendering tests.
 *
 * Verifies the dedicated error surface contract:
 *  - role="alert" + aria-live="assertive" for assertive announcement
 *  - h2 title + description + retry button (NOT anchor — retry is action)
 *  - onRetry invocation on click
 *  - disabled + aria-busy when isRetrying=true; click is no-op
 *  - data-slot for query stability
 *  - Optional className passthrough
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ErrorState } from './error-state';

const baseLabels = {
  title: 'Errore di caricamento',
  description: 'Si è verificato un problema. Riprova fra un momento.',
  retryLabel: 'Riprova',
};

describe('ErrorState (Wave A.4 follow-up)', () => {
  it('renders title and description text', () => {
    render(<ErrorState labels={baseLabels} onRetry={vi.fn()} />);
    expect(
      screen.getByRole('heading', { level: 2, name: 'Errore di caricamento' })
    ).toBeInTheDocument();
    expect(
      screen.getByText('Si è verificato un problema. Riprova fra un momento.')
    ).toBeInTheDocument();
  });

  it('exposes role="alert" with aria-live="assertive"', () => {
    render(<ErrorState labels={baseLabels} onRetry={vi.fn()} />);
    const root = screen.getByRole('alert');
    expect(root).toBeInTheDocument();
    expect(root).toHaveAttribute('aria-live', 'assertive');
  });

  it('renders retry CTA as button (not anchor — retry is an action)', () => {
    render(<ErrorState labels={baseLabels} onRetry={vi.fn()} />);
    const button = screen.getByRole('button', { name: 'Riprova' });
    expect(button).toBeInTheDocument();
    expect(button.tagName).toBe('BUTTON');
  });

  it('invokes onRetry when retry button is clicked', () => {
    const onRetry = vi.fn();
    render(<ErrorState labels={baseLabels} onRetry={onRetry} />);
    fireEvent.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('disables retry button and sets aria-busy when isRetrying=true', () => {
    render(<ErrorState labels={baseLabels} onRetry={vi.fn()} isRetrying={true} />);
    const button = screen.getByRole('button', { name: 'Riprova' });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-busy', 'true');
  });

  it('does not invoke onRetry when click fires on disabled button', () => {
    const onRetry = vi.fn();
    render(<ErrorState labels={baseLabels} onRetry={onRetry} isRetrying={true} />);
    fireEvent.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onRetry).not.toHaveBeenCalled();
  });

  it('omits aria-busy when not retrying (defaults to undefined, not "false")', () => {
    render(<ErrorState labels={baseLabels} onRetry={vi.fn()} />);
    const button = screen.getByRole('button', { name: 'Riprova' });
    expect(button).not.toHaveAttribute('aria-busy');
    expect(button).not.toBeDisabled();
  });

  it('exposes data-slot attribute', () => {
    const { container } = render(<ErrorState labels={baseLabels} onRetry={vi.fn()} />);
    const root = container.querySelector('[data-slot="shared-game-detail-error-state"]');
    expect(root).not.toBeNull();
  });

  it('passes through optional className', () => {
    const { container } = render(
      <ErrorState labels={baseLabels} onRetry={vi.fn()} className="my-custom-class" />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-error-state"]');
    expect(root?.className).toContain('my-custom-class');
  });
});
