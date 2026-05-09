import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GameChatSidebar } from '../GameChatSidebar';

describe('GameChatSidebar', () => {
  const baseProps = {
    gameTitle: 'Wingspan',
    gameIcon: '🦤',
    currentAgent: 'tutor' as const,
    history: [],
    onAgentChange: vi.fn(),
    onHistorySelect: vi.fn(),
  };

  it('renders game-mini header', () => {
    render(<GameChatSidebar {...baseProps} />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('🦤')).toBeInTheDocument();
  });

  it('renders agent switch with current pressed', () => {
    render(<GameChatSidebar {...baseProps} currentAgent="arbitro" />);
    const arbitroBtn = screen.getByRole('button', { name: /Arbitro/ });
    expect(arbitroBtn).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /Tutor/ })).toHaveAttribute('aria-pressed', 'false');
  });

  it('calls onAgentChange when other agent clicked', () => {
    const onAgentChange = vi.fn();
    render(<GameChatSidebar {...baseProps} onAgentChange={onAgentChange} currentAgent="tutor" />);
    fireEvent.click(screen.getByRole('button', { name: /Arbitro/ }));
    expect(onAgentChange).toHaveBeenCalledWith('arbitro');
  });

  it('renders history items + active highlight', () => {
    render(
      <GameChatSidebar
        {...baseProps}
        history={[
          { id: 'q1', question: 'Cumulo poteri?', when: 'ora', active: true },
          { id: 'q2', question: 'Setup 4', when: '5m fa' },
        ]}
      />
    );
    expect(screen.getByText('Cumulo poteri?')).toBeInTheDocument();
    expect(screen.getByText('Setup 4')).toBeInTheDocument();
  });

  it('calls onHistorySelect when item clicked', () => {
    const onHistorySelect = vi.fn();
    render(
      <GameChatSidebar
        {...baseProps}
        onHistorySelect={onHistorySelect}
        history={[{ id: 'q1', question: 'X', when: 'ora' }]}
      />
    );
    fireEvent.click(screen.getByText('X').closest('button')!);
    expect(onHistorySelect).toHaveBeenCalledWith('q1');
  });

  it('renders empty history gracefully', () => {
    render(<GameChatSidebar {...baseProps} />);
    expect(screen.queryByText(/conversazioni/i)).toBeInTheDocument();
  });
});
