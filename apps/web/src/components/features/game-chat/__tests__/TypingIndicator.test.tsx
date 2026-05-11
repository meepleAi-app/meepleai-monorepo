import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { TypingIndicator } from '../TypingIndicator';

describe('TypingIndicator', () => {
  it('renders 3 animated dots', () => {
    render(<TypingIndicator />);
    expect(screen.getAllByTestId('typing-dot')).toHaveLength(3);
  });

  it('shows elapsed and budget meta when provided', () => {
    render(<TypingIndicator elapsedMs={2400} budgetMs={10000} />);
    expect(screen.getByText(/2\.4s.*10s/)).toBeInTheDocument();
  });

  it('shows hint when provided', () => {
    render(<TypingIndicator hint="Cerco in 3 KB" />);
    expect(screen.getByText(/Cerco in 3 KB/)).toBeInTheDocument();
  });

  it('renders only dots when no meta provided', () => {
    render(<TypingIndicator />);
    expect(screen.queryByText(/elapsed/i)).not.toBeInTheDocument();
  });
});
