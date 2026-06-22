/**
 * Tests for PlayerAvatars (SP4 #1170 commit 2).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PlayerAvatars, type AvatarPlayer } from '../PlayerAvatars';

function makePlayers(n: number): AvatarPlayer[] {
  return Array.from({ length: n }, (_, i) => ({
    id: `p-${i}`,
    initials: `P${i}`,
    name: `Player ${i}`,
  }));
}

describe('PlayerAvatars', () => {
  it('renders empty when players array is empty', () => {
    render(<PlayerAvatars players={[]} />);
    const container = screen.getByTestId('game-nights-player-avatars');
    expect(container.children.length).toBe(0);
  });

  it('renders all players when under max', () => {
    render(<PlayerAvatars players={makePlayers(3)} max={5} />);
    expect(screen.getByText('P0')).toBeInTheDocument();
    expect(screen.getByText('P1')).toBeInTheDocument();
    expect(screen.getByText('P2')).toBeInTheDocument();
  });

  it('renders exactly max with no overflow chip', () => {
    render(<PlayerAvatars players={makePlayers(5)} max={5} />);
    expect(screen.getByText('P4')).toBeInTheDocument();
    expect(screen.queryByText(/^\+/)).not.toBeInTheDocument();
  });

  it('renders +N overflow chip when over max', () => {
    render(<PlayerAvatars players={makePlayers(8)} max={5} />);
    expect(screen.getByText('+3')).toBeInTheDocument();
    expect(screen.queryByText('P5')).not.toBeInTheDocument();
  });

  it('applies a deterministic inline background style', () => {
    // jsdom normalizes hsl() to rgb() in inline style, so assert against
    // the (non-empty) background property rather than the HSL literal.
    const { container } = render(<PlayerAvatars players={makePlayers(1)} />);
    const avatar = container.querySelector('[aria-label="Player 0"]') as HTMLElement;
    expect(avatar).not.toBeNull();
    expect(avatar.style.background).not.toBe('');
  });
});
