/**
 * GameSideCard Component Tests (Issue #3511)
 * Issue #4858: Updated for MeepleInfoCard delegation
 *
 * Verifies that the deprecated GameSideCard wrapper correctly
 * delegates to MeepleInfoCard with the expected props.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameSideCard } from '../GameSideCard';

// Mock MeepleInfoCard to inspect props
const mockMeepleInfoCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-info-card', () => ({
  MeepleInfoCard: (props: Record<string, unknown>) => {
    mockMeepleInfoCard(props);
    return <div data-testid={props['data-testid'] as string}>MeepleInfoCard</div>;
  },
}));

describe('GameSideCard', () => {
  beforeEach(() => {
    mockMeepleInfoCard.mockClear();
  });

  it('renders without crashing', () => {
    render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={null} />);
    expect(screen.getByTestId('game-side-card')).toBeInTheDocument();
  });

  it('delegates to MeepleInfoCard with correct props', () => {
    render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={13} />);

    expect(mockMeepleInfoCard).toHaveBeenCalledWith(
      expect.objectContaining({
        gameId: 'game-123',
        gameTitle: 'Catan',
        bggId: 13,
        showKnowledgeBase: true,
        showSocialLinks: true,
      })
    );
  });

  it('passes null bggId correctly', () => {
    render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={null} />);

    expect(mockMeepleInfoCard).toHaveBeenCalledWith(
      expect.objectContaining({ bggId: null })
    );
  });

  it('handles different game titles', () => {
    render(<GameSideCard gameId="game-456" gameTitle="Terraforming Mars" bggId={null} />);

    expect(mockMeepleInfoCard).toHaveBeenCalledWith(
      expect.objectContaining({ gameTitle: 'Terraforming Mars' })
    );
  });

  it('handles long game IDs', () => {
    const longId = 'game-' + '1234567890'.repeat(10);
    render(<GameSideCard gameId={longId} gameTitle="Test Game" bggId={null} />);

    expect(mockMeepleInfoCard).toHaveBeenCalledWith(
      expect.objectContaining({ gameId: longId })
    );
  });
});
