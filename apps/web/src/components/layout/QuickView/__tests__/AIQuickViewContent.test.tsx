import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AIQuickViewContent } from '../AIQuickViewContent';

describe('AIQuickViewContent', () => {
  it('renders quick prompt buttons', () => {
    render(<AIQuickViewContent gameId="g1" gameName="Catan" />);
    expect(screen.getByText(/spiega le regole/i)).toBeInTheDocument();
    expect(screen.getByText(/strategia/i)).toBeInTheDocument();
  });

  it('renders message input', () => {
    render(<AIQuickViewContent gameId="g1" gameName="Catan" />);
    expect(screen.getByPlaceholderText(/chiedi/i)).toBeInTheDocument();
  });

  it('sends message on submit', async () => {
    const user = userEvent.setup();
    render(<AIQuickViewContent gameId="g1" gameName="Catan" />);
    const input = screen.getByPlaceholderText(/chiedi/i);
    await user.type(input, 'Come si vince?');
    await user.click(screen.getByRole('button', { name: /invia/i }));
    expect(input).toHaveValue('');
  });

  it('shows game name in context label', () => {
    render(<AIQuickViewContent gameId="g1" gameName="Catan" />);
    const matches = screen.getAllByText(/catan/i);
    expect(matches.length).toBeGreaterThan(0);
  });
});
