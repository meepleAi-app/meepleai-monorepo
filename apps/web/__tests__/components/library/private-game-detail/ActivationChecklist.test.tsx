import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ActivationChecklist } from '@/components/library/private-game-detail/ActivationChecklist';

describe('ActivationChecklist', () => {
  const defaultProps = {
    gameAdded: true,
    pdfStatus: 'none' as const,
    agentStatus: 'none' as const,
    onUploadPdf: vi.fn(),
    onCreateAgent: vi.fn(),
    onStartGame: vi.fn(),
  };

  it('renders all 3 steps', () => {
    render(<ActivationChecklist {...defaultProps} />);
    expect(screen.getByTestId('step-game-added')).toBeInTheDocument();
    expect(screen.getByTestId('step-pdf')).toBeInTheDocument();
    expect(screen.getByTestId('step-agent')).toBeInTheDocument();
  });

  it('shows step 1 as completed when gameAdded is true', () => {
    render(<ActivationChecklist {...defaultProps} />);
    const step1 = screen.getByTestId('step-game-added');
    expect(step1).toHaveAttribute('data-completed', 'true');
  });

  it('disables Start Game button when PDF not ready', () => {
    render(<ActivationChecklist {...defaultProps} />);
    const button = screen.getByRole('button', { name: /inizia partita/i });
    expect(button).toBeDisabled();
  });

  it('enables Start Game button when PDF is ready', () => {
    render(<ActivationChecklist {...defaultProps} pdfStatus="ready" />);
    const button = screen.getByRole('button', { name: /inizia partita/i });
    expect(button).toBeEnabled();
  });

  it('calls onUploadPdf when upload button clicked', () => {
    render(<ActivationChecklist {...defaultProps} />);
    fireEvent.click(screen.getByRole('button', { name: /carica regolamento/i }));
    expect(defaultProps.onUploadPdf).toHaveBeenCalledOnce();
  });

  it('shows upload zone when PDF step is active', () => {
    render(<ActivationChecklist {...defaultProps} />);
    expect(screen.getByRole('button', { name: /carica regolamento/i })).toBeInTheDocument();
  });

  it('collapses completed steps', () => {
    render(<ActivationChecklist {...defaultProps} pdfStatus="ready" agentStatus="ready" />);
    const step2 = screen.getByTestId('step-pdf');
    expect(step2).toHaveAttribute('data-collapsed', 'true');
  });
});
