/**
 * AddCreditsModal Tests
 * Issue #3692 - Token Management System
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { AddCreditsModal } from '../AddCreditsModal';

const defaultProps = {
  open: true,
  onClose: vi.fn(),
  onSubmit: vi.fn().mockResolvedValue(undefined),
};

describe('AddCreditsModal', () => {
  it('renders when open', () => {
    render(<AddCreditsModal {...defaultProps} />);
    expect(screen.getByTestId('add-credits-modal')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(<AddCreditsModal {...defaultProps} open={false} />);
    expect(screen.queryByTestId('add-credits-modal')).not.toBeInTheDocument();
  });

  it('renders header', () => {
    render(<AddCreditsModal {...defaultProps} />);
    expect(screen.getByText('Add Credits')).toBeInTheDocument();
  });

  it('renders quick amount buttons', () => {
    render(<AddCreditsModal {...defaultProps} />);
    expect(screen.getByTestId('quick-amount-50')).toBeInTheDocument();
    expect(screen.getByTestId('quick-amount-100')).toBeInTheDocument();
    expect(screen.getByTestId('quick-amount-250')).toBeInTheDocument();
    expect(screen.getByTestId('quick-amount-500')).toBeInTheDocument();
    expect(screen.getByTestId('quick-amount-1000')).toBeInTheDocument();
  });

  it('sets amount on quick button click', () => {
    render(<AddCreditsModal {...defaultProps} />);
    fireEvent.click(screen.getByTestId('quick-amount-250'));
    expect(screen.getByTestId('credits-amount-input')).toHaveValue(250);
  });

  it('renders amount input', () => {
    render(<AddCreditsModal {...defaultProps} />);
    expect(screen.getByTestId('credits-amount-input')).toBeInTheDocument();
  });

  it('renders note input', () => {
    render(<AddCreditsModal {...defaultProps} />);
    expect(screen.getByTestId('credits-note-input')).toBeInTheDocument();
  });

  it('renders close button', () => {
    render(<AddCreditsModal {...defaultProps} />);
    expect(screen.getByTestId('close-credits-modal')).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    const onClose = vi.fn();
    render(<AddCreditsModal {...defaultProps} onClose={onClose} />);
    fireEvent.click(screen.getByTestId('close-credits-modal'));
    expect(onClose).toHaveBeenCalled();
  });

  it('calls onClose when cancel clicked', () => {
    const onClose = vi.fn();
    render(<AddCreditsModal {...defaultProps} onClose={onClose} />);
    fireEvent.click(screen.getByText('Cancel'));
    expect(onClose).toHaveBeenCalled();
  });

  it('shows validation error for zero amount', async () => {
    render(<AddCreditsModal {...defaultProps} />);
    fireEvent.change(screen.getByTestId('credits-amount-input'), { target: { value: '0' } });
    fireEvent.click(screen.getByTestId('submit-credits'));
    await waitFor(() => {
      expect(screen.getByTestId('credits-error')).toHaveTextContent('Please enter a valid amount greater than 0');
    });
  });

  it('calls onSubmit with correct values', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<AddCreditsModal {...defaultProps} onSubmit={onSubmit} />);
    fireEvent.click(screen.getByTestId('quick-amount-100'));
    fireEvent.change(screen.getByTestId('credits-note-input'), { target: { value: 'Top-up' } });
    fireEvent.click(screen.getByTestId('submit-credits'));
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(100, 'Top-up');
    });
  });

  it('shows error on submit failure', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new Error('Network error'));
    render(<AddCreditsModal {...defaultProps} onSubmit={onSubmit} />);
    fireEvent.click(screen.getByTestId('quick-amount-50'));
    fireEvent.click(screen.getByTestId('submit-credits'));
    await waitFor(() => {
      expect(screen.getByTestId('credits-error')).toHaveTextContent('Network error');
    });
  });
});
