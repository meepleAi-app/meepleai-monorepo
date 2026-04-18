import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ContinueButton } from '@/components/chat-unified/ContinueButton';

describe('ContinueButton', () => {
  it('renders with correct text', () => {
    render(<ContinueButton onContinue={() => {}} isLoading={false} />);
    expect(screen.getByText('Continua la risposta')).toBeInTheDocument();
  });

  it('calls onContinue when clicked', () => {
    const onContinue = vi.fn();
    render(<ContinueButton onContinue={onContinue} isLoading={false} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onContinue).toHaveBeenCalledOnce();
  });

  it('shows loading state', () => {
    render(<ContinueButton onContinue={() => {}} isLoading={true} />);
    expect(screen.getByText('Continuando...')).toBeInTheDocument();
    expect(screen.getByRole('button')).toBeDisabled();
  });
});
