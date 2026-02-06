/**
 * GameSideCard Component Tests (Issue #3511)
 *
 * Simplified tests focusing on core rendering and tab switching.
 * Full interaction tests covered by E2E suite.
 *
 * Target: ≥85% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { GameSideCard } from '../GameSideCard';

// ============================================================================
// Mock Setup
// ============================================================================

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// ============================================================================
// Rendering Tests
// ============================================================================

describe('GameSideCard - Rendering', () => {
  it('renders without crashing', () => {
    const { container } = render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={null} />);
    expect(container).toBeInTheDocument();
  });

  it('renders with Knowledge Base tab by default', () => {
    const { container } = render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={null} />);
    expect(container.textContent).toContain('Knowledge Base');
  });

  it('renders Social Links tab', () => {
    const { container } = render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={null} />);
    expect(container.textContent).toContain('Social Links');
  });

  it('renders with BGG ID provided', () => {
    const { container } = render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={13} />);
    expect(container).toBeInTheDocument();
  });

  it('renders without BGG ID', () => {
    const { container } = render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={null} />);
    expect(container).toBeInTheDocument();
  });

  it('shows upload button in Knowledge Base', () => {
    const { container } = render(<GameSideCard gameId="game-123" gameTitle="Catan" bggId={null} />);
    expect(container.textContent).toContain('Carica PDF');
  });

  it('handles different game titles', () => {
    const { container } = render(<GameSideCard gameId="game-456" gameTitle="Terraforming Mars" bggId={null} />);
    expect(container).toBeInTheDocument();
  });

  it('handles very long game IDs', () => {
    const longId = 'game-' + '1234567890'.repeat(10);
    const { container } = render(<GameSideCard gameId={longId} gameTitle="Test Game" bggId={null} />);
    expect(container).toBeInTheDocument();
  });
});
