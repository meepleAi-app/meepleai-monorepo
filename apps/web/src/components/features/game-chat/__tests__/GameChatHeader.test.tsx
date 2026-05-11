import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { GameChatHeader } from '../GameChatHeader';

describe('GameChatHeader', () => {
  it('renders game title + icon + tutor badge', () => {
    render(<GameChatHeader gameTitle="Wingspan" gameIcon="🦤" agent="tutor" />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('🦤')).toBeInTheDocument();
    expect(screen.getByText(/Tutor/i)).toBeInTheDocument();
  });
  it('renders arbitro badge when agent=arbitro', () => {
    render(<GameChatHeader gameTitle="Wingspan" gameIcon="🦤" agent="arbitro" />);
    expect(screen.getByText(/Arbitro/i)).toBeInTheDocument();
  });
  it('renders subtitle when provided', () => {
    render(
      <GameChatHeader gameTitle="Wingspan" gameIcon="🦤" agent="tutor" subtitle="Edge case" />
    );
    expect(screen.getByText(/Edge case/)).toBeInTheDocument();
  });
  it('exposes agent via data attribute', () => {
    render(<GameChatHeader gameTitle="X" gameIcon="🎲" agent="arbitro" />);
    expect(screen.getByTestId('game-chat-header')).toHaveAttribute('data-agent', 'arbitro');
  });
});
