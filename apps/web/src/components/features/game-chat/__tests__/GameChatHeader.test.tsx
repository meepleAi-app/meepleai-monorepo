import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { GameChatHeader } from '../GameChatHeader';

describe('GameChatHeader', () => {
  it('renders game title + icon + the intent label', () => {
    render(<GameChatHeader gameTitle="Wingspan" gameIcon="🦤" intent="tutor" />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('🦤')).toBeInTheDocument();
    expect(screen.getByTestId('game-chat-header')).toHaveTextContent('Tutor');
  });
  // #4999: the label of each intent comes from lib/chat/answer-intent.ts, not from a list of its own.
  it.each([
    ['auto', 'Auto'],
    ['tutor', 'Tutor'],
    ['secca', 'Risposta secca'],
  ] as const)('shows %s as «%s», read-only', (intent, label) => {
    render(<GameChatHeader gameTitle="Wingspan" gameIcon="🦤" intent={intent} />);
    const header = screen.getByTestId('game-chat-header');
    expect(header).toHaveTextContent(label);
    expect(screen.queryAllByRole('button')).toHaveLength(0);
  });
  it('names the badge for a screen reader', () => {
    render(<GameChatHeader gameTitle="Wingspan" gameIcon="🦤" intent="secca" />);
    expect(screen.getByTestId('game-chat-header')).toHaveTextContent(
      'Intento della risposta: Risposta secca'
    );
  });
  it('renders subtitle when provided', () => {
    render(
      <GameChatHeader gameTitle="Wingspan" gameIcon="🦤" intent="tutor" subtitle="Edge case" />
    );
    expect(screen.getByText(/Edge case/)).toBeInTheDocument();
  });
  it('exposes the intent via data attribute', () => {
    render(<GameChatHeader gameTitle="X" gameIcon="🎲" intent="secca" />);
    expect(screen.getByTestId('game-chat-header')).toHaveAttribute('data-intent', 'secca');
  });
});
