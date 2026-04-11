/**
 * PhotoButton tests — QuickActions optional photo prop
 * GAP-005: Photo capture button in LiveSession
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { QuickActions } from '../QuickActions';

describe('QuickActions — Foto button', () => {
  const baseProps = {
    isPaused: false,
    onOpenRules: vi.fn(),
    onAskArbiter: vi.fn(),
    onTogglePause: vi.fn(),
    onOpenScores: vi.fn(),
  };

  it('should not render Foto button when onOpenPhoto is not provided', () => {
    render(<QuickActions {...baseProps} />);
    expect(screen.queryByTestId('quick-action-photo')).not.toBeInTheDocument();
  });

  it('should render the Foto button when onOpenPhoto is provided', () => {
    render(<QuickActions {...baseProps} onOpenPhoto={vi.fn()} />);
    expect(screen.getByTestId('quick-action-photo')).toBeInTheDocument();
  });

  it('should display "Foto" label', () => {
    render(<QuickActions {...baseProps} onOpenPhoto={vi.fn()} />);
    expect(screen.getByText('Foto')).toBeInTheDocument();
  });

  it('should call onOpenPhoto when Foto button is clicked', () => {
    const onOpenPhoto = vi.fn();
    render(<QuickActions {...baseProps} onOpenPhoto={onOpenPhoto} />);
    fireEvent.click(screen.getByTestId('quick-action-photo'));
    expect(onOpenPhoto).toHaveBeenCalledTimes(1);
  });

  it('should disable Foto button when isLoading is true', () => {
    render(<QuickActions {...baseProps} onOpenPhoto={vi.fn()} isLoading={true} />);
    expect(screen.getByTestId('quick-action-photo')).toBeDisabled();
  });
});
