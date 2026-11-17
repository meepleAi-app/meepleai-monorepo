import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GameChatSidebar } from '../GameChatSidebar';

describe('GameChatSidebar', () => {
  const baseProps = {
    gameTitle: 'Wingspan',
    gameIcon: '🦤',
    history: [],
    onHistorySelect: vi.fn(),
  };

  it('renders game-mini header', () => {
    render(<GameChatSidebar {...baseProps} />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('🦤')).toBeInTheDocument();
  });

  // #4999: the tutor/arbitro switch changed the label and nothing else. The intent is read in the header.
  it('offers no agent or intent switch', () => {
    render(<GameChatSidebar {...baseProps} />);
    expect(screen.queryByRole('group')).not.toBeInTheDocument();
    expect(screen.queryAllByRole('button')).toHaveLength(0);
    expect(screen.queryByText(/tutor|arbitro/i)).not.toBeInTheDocument();
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
