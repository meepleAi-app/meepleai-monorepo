/**
 * ProgressToast Component Tests (Issue #4210)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ProgressToast } from '../progress-toast';

vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn(() => ({
    status: { state: 'embedding', progress: 70, timestamp: new Date().toISOString() },
    metrics: { progressPercentage: 70 },
  })),
}));

describe('ProgressToast', () => {
  it('should render title and progress', () => {
    render(<ProgressToast documentId="doc-123" title="Game Manual.pdf" />);

    expect(screen.getByText('Game Manual.pdf')).toBeInTheDocument();
    expect(screen.getByText(/Embedding\.\.\. 70%/i)).toBeInTheDocument();
  });

  it('should show progress bar for non-terminal states', () => {
    render(<ProgressToast documentId="doc-123" title="Test.pdf" />);

    const progressBar = screen.getByRole('progressbar');
    expect(progressBar).toBeInTheDocument();
  });

  it('should call onDismiss when dismiss button clicked', async () => {
    const user = userEvent.setup();
    const onDismiss = vi.fn();

    render(<ProgressToast documentId="doc-123" title="Test.pdf" onDismiss={onDismiss} />);

    const dismissBtn = screen.getByLabelText('Dismiss notification');
    await user.click(dismissBtn);

    expect(onDismiss).toHaveBeenCalled();
  });

  it('should show View Details button when callback provided', () => {
    const onViewDetails = vi.fn();

    render(<ProgressToast documentId="doc-123" title="Test.pdf" onViewDetails={onViewDetails} />);

    expect(screen.getByText('View Details')).toBeInTheDocument();
  });
});
