import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SessionBottomNav } from './SessionBottomNav';

describe('SessionBottomNav', () => {
  it('renders 4 session tabs', () => {
    render(<SessionBottomNav activeTab="game" onTabChange={() => {}} />);
    expect(screen.getByText('Gioco')).toBeInTheDocument();
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
    expect(screen.getByText('Chiedi')).toBeInTheDocument();
    expect(screen.getByText('Giocatori')).toBeInTheDocument();
  });

  it('highlights active tab', () => {
    render(<SessionBottomNav activeTab="scores" onTabChange={() => {}} />);
    const scoresTab = screen.getByText('Punteggi').closest('button');
    expect(scoresTab).toHaveAttribute('aria-selected', 'true');
  });

  it('calls onTabChange when tab is clicked', () => {
    const onTabChange = vi.fn();
    render(<SessionBottomNav activeTab="game" onTabChange={onTabChange} />);
    fireEvent.click(screen.getByText('Chiedi'));
    expect(onTabChange).toHaveBeenCalledWith('chat');
  });

  it('has fixed bottom positioning', () => {
    const { container } = render(
      <SessionBottomNav activeTab="game" onTabChange={() => {}} />
    );
    expect(container.firstChild).toHaveClass('fixed');
    expect(container.firstChild).toHaveClass('bottom-0');
  });
});
