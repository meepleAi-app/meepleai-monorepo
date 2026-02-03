/**
 * TypingIndicator Tests (Issue #3243)
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { TypingIndicator } from '../TypingIndicator';

describe('TypingIndicator', () => {
  it('renders with correct ARIA label', () => {
    render(<TypingIndicator />);

    expect(screen.getByLabelText('Agent is typing')).toBeInTheDocument();
  });

  it('renders three animated dots', () => {
    const { container } = render(<TypingIndicator />);

    const dots = container.querySelectorAll('.animate-bounce');
    expect(dots).toHaveLength(3);
  });

  it('renders blinking cursor', () => {
    const { container } = render(<TypingIndicator />);

    const cursor = container.querySelector('.animate-pulse');
    expect(cursor).toBeInTheDocument();
    expect(cursor?.textContent).toBe('▊');
  });

  it('has dark background styling', () => {
    const { container } = render(<TypingIndicator />);

    const bubble = container.querySelector('.bg-gray-800');
    expect(bubble).toBeInTheDocument();
  });

  it('renders with proper spacing between dots and cursor', () => {
    const { container } = render(<TypingIndicator />);

    const cursor = container.querySelector('.animate-pulse');
    expect(cursor).toHaveClass('ml-1'); // Margin left for spacing
  });
});
