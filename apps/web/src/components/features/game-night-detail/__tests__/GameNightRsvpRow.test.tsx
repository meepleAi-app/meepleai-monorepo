/**
 * Tests for GameNightRsvpRow (Issue #951 commit 2).
 */

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GameNightRsvpRow } from '../GameNightRsvpRow';

const baseProps = {
  userId: '11111111-1111-1111-1111-111111111111',
  userName: 'Marco R.',
  status: 'Accepted' as const,
  statusLabel: 'Confermato',
};

describe('GameNightRsvpRow', () => {
  it('renders user name + status label + avatar', () => {
    render(<GameNightRsvpRow {...baseProps} />);
    const row = screen.getByTestId('game-night-rsvp-row');
    expect(row).toHaveAttribute('data-user-id', baseProps.userId);
    expect(row).toHaveAttribute('data-status', 'Accepted');
    expect(row).toHaveAttribute('data-is-me', 'false');
    expect(within(row).getByText('Marco R.')).toBeInTheDocument();
    expect(within(row).getByText('Confermato')).toBeInTheDocument();
    expect(within(row).getByRole('img', { name: 'Marco R.' })).toBeInTheDocument();
  });

  it('marks isMe rows with entity-player ring + "me" pill', () => {
    render(<GameNightRsvpRow {...baseProps} isMe />);
    const row = screen.getByTestId('game-night-rsvp-row');
    expect(row).toHaveAttribute('data-is-me', 'true');
    expect(row.className).toContain('border-entity-player');
    expect(within(row).getByText('me')).toBeInTheDocument();
  });

  it('renders host pill when isHost + hostLabel provided', () => {
    render(<GameNightRsvpRow {...baseProps} isHost hostLabel="host" />);
    expect(screen.getByText('host')).toBeInTheDocument();
  });

  it('does not render host pill when hostLabel is missing', () => {
    render(<GameNightRsvpRow {...baseProps} isHost />);
    expect(screen.queryByText('host')).toBeNull();
  });

  it('applies dashed border + waiting icon for Pending', () => {
    render(<GameNightRsvpRow {...baseProps} status="Pending" statusLabel="In attesa" />);
    const row = screen.getByTestId('game-night-rsvp-row');
    expect(row.className).toContain('border-dashed');
    expect(within(row).getByText('⏳')).toBeInTheDocument();
  });

  it('strikes through name + dims row when Declined', () => {
    render(<GameNightRsvpRow {...baseProps} status="Declined" statusLabel="Declinato" />);
    const row = screen.getByTestId('game-night-rsvp-row');
    expect(row.className).toContain('opacity-70');
    const nameSpan = within(row).getByText('Marco R.');
    // line-through is applied on the parent flex container, not on the span itself
    const wrapper = nameSpan.closest('.line-through');
    expect(wrapper).not.toBeNull();
  });

  it('uses success icon ✓ for Accepted', () => {
    render(<GameNightRsvpRow {...baseProps} status="Accepted" />);
    expect(screen.getByText('✓')).toBeInTheDocument();
  });

  it('uses warning icon ? for Maybe', () => {
    render(<GameNightRsvpRow {...baseProps} status="Maybe" statusLabel="Forse" />);
    const row = screen.getByTestId('game-night-rsvp-row');
    expect(within(row).getByText('?')).toBeInTheDocument();
  });

  it('uses muted icon × for Declined', () => {
    render(<GameNightRsvpRow {...baseProps} status="Declined" statusLabel="Declinato" />);
    const row = screen.getByTestId('game-night-rsvp-row');
    expect(within(row).getByText('×')).toBeInTheDocument();
  });

  describe('mode prop (issue #1169)', () => {
    it('defaults to data-mode="authenticated" when prop is omitted', () => {
      render(<GameNightRsvpRow {...baseProps} isMe />);
      const row = screen.getByTestId('game-night-rsvp-row');
      expect(row).toHaveAttribute('data-mode', 'authenticated');
    });

    it('exposes data-mode="public" and forces isMe to false in public mode', () => {
      render(<GameNightRsvpRow {...baseProps} isMe mode="public" />);
      const row = screen.getByTestId('game-night-rsvp-row');
      expect(row).toHaveAttribute('data-mode', 'public');
      // isMe is silently coerced to false on public surfaces.
      expect(row).toHaveAttribute('data-is-me', 'false');
      expect(within(row).queryByText('me')).toBeNull();
    });

    it('still renders host pill in public mode', () => {
      render(<GameNightRsvpRow {...baseProps} isHost hostLabel="host" mode="public" />);
      expect(screen.getByText('host')).toBeInTheDocument();
    });
  });
});
