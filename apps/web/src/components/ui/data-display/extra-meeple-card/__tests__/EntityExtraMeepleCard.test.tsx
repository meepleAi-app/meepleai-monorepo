/**
 * Tests for Entity variant ExtraMeepleCards
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import {
  GameExtraMeepleCard,
  PlayerExtraMeepleCard,
  CollectionExtraMeepleCard,
} from '../EntityExtraMeepleCard';
import type {
  GameDetailData,
  PlayerDetailData,
  CollectionDetailData,
} from '../types';

// ============================================================================
// Test Data
// ============================================================================

const mockGameData: GameDetailData = {
  id: 'game-1',
  title: 'Catan',
  imageUrl: 'https://example.com/catan.jpg',
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playTimeMinutes: 90,
  description: 'Trade, build, settle.',
  averageRating: 7.8,
  totalPlays: 150,
  faqCount: 12,
  rulesDocumentCount: 3,
};

const mockPlayerData: PlayerDetailData = {
  id: 'player-1',
  displayName: 'Alice',
  avatarUrl: 'https://example.com/alice.jpg',
  gamesPlayed: 42,
  winRate: 0.65,
  totalSessions: 38,
  favoriteGame: 'Catan',
  achievements: [
    { id: 'ach-1', name: 'First Win', icon: '🏆' },
    { id: 'ach-2', name: 'Strategist', icon: '🧠' },
  ],
  recentGames: [
    { name: 'Catan', date: '2026-02-18', result: 'win' },
    { name: 'Ticket to Ride', date: '2026-02-17', result: 'loss' },
    { name: 'Azul', date: '2026-02-16', result: 'draw' },
  ],
};

const mockCollectionData: CollectionDetailData = {
  id: 'coll-1',
  name: 'Family Favorites',
  description: 'Our top family games.',
  imageUrl: 'https://example.com/collection.jpg',
  gameCount: 15,
  ownerName: 'Alice',
  isShared: true,
  games: [
    { id: 'g1', title: 'Catan', imageUrl: 'https://example.com/catan-thumb.jpg' },
    { id: 'g2', title: 'Ticket to Ride' },
  ],
};

// ============================================================================
// GameExtraMeepleCard Tests
// ============================================================================

describe('GameExtraMeepleCard', () => {
  it('renders game title and publisher with year', () => {
    render(<GameExtraMeepleCard data={mockGameData} data-testid="game-card" />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Kosmos (1995)')).toBeInTheDocument();
  });

  it('renders rating badge', () => {
    render(<GameExtraMeepleCard data={mockGameData} />);

    expect(screen.getByText('7.8')).toBeInTheDocument();
  });

  it('renders player count, play time, and total plays stats', () => {
    render(<GameExtraMeepleCard data={mockGameData} />);

    expect(screen.getByText('3-4')).toBeInTheDocument();
    expect(screen.getByText('90m')).toBeInTheDocument();
    expect(screen.getByText('150')).toBeInTheDocument();
  });

  it('renders description', () => {
    render(<GameExtraMeepleCard data={mockGameData} />);

    expect(screen.getByText('Trade, build, settle.')).toBeInTheDocument();
  });

  it('renders all 3 tabs', () => {
    render(<GameExtraMeepleCard data={mockGameData} />);

    expect(screen.getByRole('tab', { name: /details/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /rules/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /stats/i })).toBeInTheDocument();
  });

  it('starts on details tab by default', () => {
    render(<GameExtraMeepleCard data={mockGameData} />);

    expect(screen.getByRole('tab', { name: /details/i })).toHaveAttribute('data-state', 'active');
  });

  it('switches to rules tab', async () => {
    const user = userEvent.setup();
    render(<GameExtraMeepleCard data={mockGameData} />);

    await user.click(screen.getByRole('tab', { name: /rules/i }));

    expect(screen.getByRole('tab', { name: /rules/i })).toHaveAttribute('data-state', 'active');
    expect(screen.getByText('Rules and FAQs will be displayed here')).toBeInTheDocument();
  });

  it('switches to stats tab and shows rating', async () => {
    const user = userEvent.setup();
    render(<GameExtraMeepleCard data={mockGameData} />);

    await user.click(screen.getByRole('tab', { name: /stats/i }));

    expect(screen.getByText('Average Rating')).toBeInTheDocument();
  });

  it('renders loading state', () => {
    render(<GameExtraMeepleCard data={mockGameData} loading data-testid="game-card" />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders error state', () => {
    render(<GameExtraMeepleCard data={mockGameData} error="Game not found" data-testid="game-card" />);

    expect(screen.getByText('Game not found')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<GameExtraMeepleCard data={mockGameData} className="my-class" data-testid="game-card" />);

    expect(screen.getByTestId('game-card')).toHaveClass('my-class');
  });

  it('renders publisher without year when yearPublished is undefined', () => {
    const data = { ...mockGameData, yearPublished: undefined };
    render(<GameExtraMeepleCard data={data} />);

    expect(screen.getByText('Kosmos')).toBeInTheDocument();
  });
});

// ============================================================================
// PlayerExtraMeepleCard Tests
// ============================================================================

describe('PlayerExtraMeepleCard', () => {
  it('renders player name', () => {
    render(<PlayerExtraMeepleCard data={mockPlayerData} />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('renders win rate badge', () => {
    render(<PlayerExtraMeepleCard data={mockPlayerData} />);

    // winRate 0.65 → "65%" appears in badge and stats
    expect(screen.getAllByText('65%').length).toBeGreaterThanOrEqual(1);
  });

  it('renders profile stats', () => {
    render(<PlayerExtraMeepleCard data={mockPlayerData} />);

    expect(screen.getByText('42')).toBeInTheDocument(); // gamesPlayed
    expect(screen.getByText('38')).toBeInTheDocument(); // totalSessions
  });

  it('renders favorite game', () => {
    render(<PlayerExtraMeepleCard data={mockPlayerData} />);

    expect(screen.getByText('Favorite Game')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders all 3 tabs', () => {
    render(<PlayerExtraMeepleCard data={mockPlayerData} />);

    expect(screen.getByRole('tab', { name: /profile/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /achievements/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /recent/i })).toBeInTheDocument();
  });

  it('switches to achievements tab', async () => {
    const user = userEvent.setup();
    render(<PlayerExtraMeepleCard data={mockPlayerData} />);

    await user.click(screen.getByRole('tab', { name: /achievements/i }));

    expect(screen.getByText('First Win')).toBeInTheDocument();
    expect(screen.getByText('Strategist')).toBeInTheDocument();
    expect(screen.getByText('🏆')).toBeInTheDocument();
    expect(screen.getByText('🧠')).toBeInTheDocument();
  });

  it('shows empty achievements state', async () => {
    const user = userEvent.setup();
    const data = { ...mockPlayerData, achievements: [] };
    render(<PlayerExtraMeepleCard data={data} />);

    await user.click(screen.getByRole('tab', { name: /achievements/i }));

    expect(screen.getByText('No achievements yet')).toBeInTheDocument();
  });

  it('switches to recent games tab', async () => {
    const user = userEvent.setup();
    render(<PlayerExtraMeepleCard data={mockPlayerData} />);

    await user.click(screen.getByRole('tab', { name: /recent/i }));

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Win')).toBeInTheDocument();
    expect(screen.getByText('Loss')).toBeInTheDocument();
    expect(screen.getByText('Draw')).toBeInTheDocument();
  });

  it('shows empty recent games state', async () => {
    const user = userEvent.setup();
    const data = { ...mockPlayerData, recentGames: [] };
    render(<PlayerExtraMeepleCard data={data} />);

    await user.click(screen.getByRole('tab', { name: /recent/i }));

    expect(screen.getByText('No recent games')).toBeInTheDocument();
  });

  it('renders loading state', () => {
    render(<PlayerExtraMeepleCard data={mockPlayerData} loading />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders error state', () => {
    render(<PlayerExtraMeepleCard data={mockPlayerData} error="Player not found" />);

    expect(screen.getByText('Player not found')).toBeInTheDocument();
  });
});

// ============================================================================
// CollectionExtraMeepleCard Tests
// ============================================================================

describe('CollectionExtraMeepleCard', () => {
  it('renders collection name and owner', () => {
    render(<CollectionExtraMeepleCard data={mockCollectionData} />);

    expect(screen.getByText('Family Favorites')).toBeInTheDocument();
    expect(screen.getByText('by Alice')).toBeInTheDocument();
  });

  it('renders game count in badge and stats', () => {
    render(<CollectionExtraMeepleCard data={mockCollectionData} />);

    // "15" appears in badge and stat card
    expect(screen.getAllByText('15').length).toBeGreaterThanOrEqual(1);
  });

  it('renders shared status', () => {
    render(<CollectionExtraMeepleCard data={mockCollectionData} />);

    expect(screen.getByText('Yes')).toBeInTheDocument();
  });

  it('renders description', () => {
    render(<CollectionExtraMeepleCard data={mockCollectionData} />);

    expect(screen.getByText('Our top family games.')).toBeInTheDocument();
  });

  it('renders both tabs', () => {
    render(<CollectionExtraMeepleCard data={mockCollectionData} />);

    expect(screen.getByRole('tab', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /games/i })).toBeInTheDocument();
  });

  it('switches to games tab and shows game list', async () => {
    const user = userEvent.setup();
    render(<CollectionExtraMeepleCard data={mockCollectionData} />);

    await user.click(screen.getByRole('tab', { name: /games/i }));

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('shows empty collection state', async () => {
    const user = userEvent.setup();
    const data = { ...mockCollectionData, games: [] };
    render(<CollectionExtraMeepleCard data={data} />);

    await user.click(screen.getByRole('tab', { name: /games/i }));

    expect(screen.getByText('No games in collection')).toBeInTheDocument();
  });

  it('shows shared status as "No" when not shared', () => {
    const data = { ...mockCollectionData, isShared: false };
    render(<CollectionExtraMeepleCard data={data} />);

    expect(screen.getByText('No')).toBeInTheDocument();
  });

  it('renders loading state', () => {
    render(<CollectionExtraMeepleCard data={mockCollectionData} loading />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders error state', () => {
    render(<CollectionExtraMeepleCard data={mockCollectionData} error="Not found" />);

    expect(screen.getByText('Not found')).toBeInTheDocument();
  });
});
