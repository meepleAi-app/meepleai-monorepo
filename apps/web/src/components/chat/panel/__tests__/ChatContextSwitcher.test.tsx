import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { ChatContextSwitcher } from '../ChatContextSwitcher';

describe('ChatContextSwitcher', () => {
  const game = {
    id: 'azul',
    name: 'Azul',
    year: 2017,
    pdfCount: 3,
    kbStatus: 'ready' as const,
  };

  it('renders the game name and meta when context is set', () => {
    render(<ChatContextSwitcher gameContext={game} onPickGame={() => {}} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText(/2017/)).toBeInTheDocument();
    expect(screen.getByText(/3 PDF/)).toBeInTheDocument();
  });

  it('renders a placeholder when no context', () => {
    render(<ChatContextSwitcher gameContext={null} onPickGame={() => {}} />);
    expect(screen.getByText(/Seleziona gioco/i)).toBeInTheDocument();
  });

  it('calls onPickGame when pill clicked', async () => {
    const onPickGame = vi.fn();
    const user = userEvent.setup();
    render(<ChatContextSwitcher gameContext={game} onPickGame={onPickGame} />);
    await user.click(screen.getByRole('button', { name: /azul/i }));
    expect(onPickGame).toHaveBeenCalled();
  });
});
