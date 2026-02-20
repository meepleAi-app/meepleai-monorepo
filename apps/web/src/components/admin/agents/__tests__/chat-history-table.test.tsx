import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { ChatHistoryTable } from '../chat-history-table';

describe('ChatHistoryTable', () => {
  it('renders chat history table', () => {
    render(<ChatHistoryTable />);

    expect(screen.getByText('Session ID')).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByText('Agent')).toBeInTheDocument();
  });

  it('displays mock chat sessions', () => {
    render(<ChatHistoryTable />);

    expect(screen.getByText('Sarah Chen')).toBeInTheDocument();
    expect(screen.getByText('chat-001')).toBeInTheDocument();
  });

  it('expands chat preview on row click', () => {
    render(<ChatHistoryTable />);

    const firstRow = screen.getByText('chat-001').closest('tr')!;

    // Initially collapsed
    expect(screen.queryByText('Chat Preview')).not.toBeInTheDocument();

    // Click to expand
    fireEvent.click(firstRow);
    expect(screen.getByText('Chat Preview')).toBeInTheDocument();

    // Click again to collapse
    fireEvent.click(firstRow);
    expect(screen.queryByText('Chat Preview')).not.toBeInTheDocument();
  });

  it('handles keyboard navigation (Enter key)', () => {
    render(<ChatHistoryTable />);

    const firstRow = screen.getByText('chat-001').closest('tr')!;

    // Press Enter to expand
    fireEvent.keyDown(firstRow, { key: 'Enter' });
    expect(screen.getByText('Chat Preview')).toBeInTheDocument();
  });

  it('handles keyboard navigation (Space key)', () => {
    render(<ChatHistoryTable />);

    const firstRow = screen.getByText('chat-001').closest('tr')!;

    // Press Space to expand
    fireEvent.keyDown(firstRow, { key: ' ' });
    expect(screen.getByText('Chat Preview')).toBeInTheDocument();
  });

  it('renders star ratings', () => {
    const { container } = render(<ChatHistoryTable />);

    // Check for star icons
    const stars = container.querySelectorAll('svg');
    expect(stars.length).toBeGreaterThan(0);
  });

  it('shows chat preview messages', () => {
    render(<ChatHistoryTable />);

    const firstRow = screen.getByText('chat-001').closest('tr')!;
    fireEvent.click(firstRow);

    // Check for preview message content
    expect(screen.getByText(/What happens when you roll doubles in Catan/)).toBeInTheDocument();
  });
});
