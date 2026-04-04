import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

// Import the canonical KbStatusBadge export.
// File is named DocumentStatusBadge.tsx for historical reasons — the component was renamed.
import { KbStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';

describe('KbStatusBadge', () => {
  it('renders processing state with animated icon and Italian label', () => {
    render(<KbStatusBadge status="processing" />);
    const badge = screen.getByTestId('kb-status-processing');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveTextContent('In elaborazione');
    expect(badge.className).toContain('bg-blue-50');
    const icon = badge.querySelector('svg');
    expect(icon?.classList.contains('animate-spin')).toBe(true);
  });

  it('renders indexed state with green check', () => {
    render(<KbStatusBadge status="indexed" />);
    const badge = screen.getByTestId('kb-status-indexed');
    expect(badge).toHaveTextContent('Indicizzata');
    expect(badge.className).toContain('bg-green-50');
  });

  it('renders failed state with red X', () => {
    render(<KbStatusBadge status="failed" />);
    const badge = screen.getByTestId('kb-status-failed');
    expect(badge).toHaveTextContent('Errore');
    expect(badge.className).toContain('bg-red-50');
  });

  it('renders none state with muted style', () => {
    render(<KbStatusBadge status="none" />);
    const badge = screen.getByTestId('kb-status-none');
    expect(badge).toHaveTextContent('Non indicizzata');
  });

  it('supports md size variant', () => {
    render(<KbStatusBadge status="indexed" size="md" />);
    const badge = screen.getByTestId('kb-status-indexed');
    expect(badge.className).toContain('px-2');
  });

  it('has accessible aria-label', () => {
    render(<KbStatusBadge status="processing" />);
    expect(screen.getByLabelText('Stato KB: In elaborazione')).toBeInTheDocument();
  });
});
