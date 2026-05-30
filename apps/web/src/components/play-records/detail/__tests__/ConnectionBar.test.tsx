/**
 * ConnectionBar — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Horizontal row of entity-tinted chips for: game, player count, chat, event date.
 * Renders inside PlayRecordDetailView between the HeroPodium and the KpiGrid.
 *
 * AC-2.3: ConnectionBar con chip entity-tinted (game, player count, chat presence, event date)
 * AC-2.8 EC-2: freeform (gameId===null) → no game link (chip is plain text, not anchor)
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ConnectionBar } from '../ConnectionBar';

describe('ConnectionBar', () => {
  const defaultProps = {
    gameId: 'g-1',
    gameName: 'Wingspan',
    playerCount: 4,
    dateLabel: 'ieri',
    chatCount: 3,
  };

  it('renders data-slot="connection-bar"', () => {
    const { container } = render(<ConnectionBar {...defaultProps} />);
    expect(container.querySelector('[data-slot="connection-bar"]')).not.toBeNull();
  });

  it('shows game name chip', () => {
    render(<ConnectionBar {...defaultProps} />);
    expect(screen.getByText('Wingspan')).toBeTruthy();
  });

  it('shows player count chip', () => {
    render(<ConnectionBar {...defaultProps} />);
    expect(screen.getByText(/4 giocatori/i)).toBeTruthy();
  });

  it('shows event date chip', () => {
    render(<ConnectionBar {...defaultProps} />);
    expect(screen.getByText('ieri')).toBeTruthy();
  });

  it('shows chat chip when chatCount > 0', () => {
    render(<ConnectionBar {...defaultProps} chatCount={5} />);
    expect(screen.getByText(/5 messaggi/i)).toBeTruthy();
  });

  it('shows no-chat chip when chatCount is 0', () => {
    render(<ConnectionBar {...defaultProps} chatCount={0} />);
    expect(screen.getByText(/nessuna chat/i)).toBeTruthy();
  });

  it('EC-2: freeform (gameId=null) — does NOT render an anchor for game', () => {
    const { container } = render(<ConnectionBar {...defaultProps} gameId={null} />);
    const anchors = container.querySelectorAll('a[href*="/games"]');
    expect(anchors.length).toBe(0);
  });

  it('renders game chip with link when gameId is present', () => {
    const { container } = render(<ConnectionBar {...defaultProps} />);
    const gameAnchor = container.querySelector('a[href*="/games/g-1"]');
    expect(gameAnchor).not.toBeNull();
  });
});
