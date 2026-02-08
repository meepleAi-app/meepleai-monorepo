/**
 * EntityListView Demo Page
 *
 * Interactive demo for testing Phase 1 functionality:
 * - Grid layout with responsive columns
 * - ViewModeSwitcher UI (Grid mode only for Phase 1)
 * - localStorage persistence
 * - Empty/Loading states
 * - Different entity types
 */

'use client';

import { useState, useEffect } from 'react';
import { Users, Clock, Calendar, Star, TrendingUp } from 'lucide-react';
import { EntityListView } from '@/components/ui/data-display/entity-list-view';

// ============================================================================
// Mock Data
// ============================================================================

interface Game {
  id: string;
  title: string;
  publisher: string;
  rating: number;
  minPlayers: number;
  maxPlayers: number;
  playtime: number;
  yearPublished: number;
  imageUrl?: string;
}

const MOCK_GAMES: Game[] = [
  {
    id: '1',
    title: 'Twilight Imperium',
    publisher: 'Fantasy Flight Games',
    rating: 8.7,
    minPlayers: 3,
    maxPlayers: 6,
    playtime: 480,
    yearPublished: 2017,
    imageUrl:
      'https://cf.geekdo-images.com/0jySN1LmpUusSZfWwOLI9g__original/img/0dxeEjHJiuYYsOXC4xS5M8cL08Q=/0x0/filters:format(jpeg)/pic7493297.jpg',
  },
  {
    id: '2',
    title: 'Gloomhaven',
    publisher: 'Cephalofair Games',
    rating: 8.8,
    minPlayers: 1,
    maxPlayers: 4,
    playtime: 120,
    yearPublished: 2017,
    imageUrl:
      'https://cf.geekdo-images.com/sZYp_3BTDGjh2unaZfZmuA__original/img/pBaOL7vJBzDrs99j04-BLvmS4B8=/0x0/filters:format(jpeg)/pic2437871.jpg',
  },
  {
    id: '3',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    rating: 8.1,
    minPlayers: 1,
    maxPlayers: 5,
    playtime: 70,
    yearPublished: 2019,
    imageUrl:
      'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__original/img/uIjeoKgHMcRkEjGOQVGLg2JD75E=/0x0/filters:format(jpeg)/pic4458123.jpg',
  },
  {
    id: '4',
    title: 'Azul',
    publisher: 'Plan B Games',
    rating: 7.9,
    minPlayers: 2,
    maxPlayers: 4,
    playtime: 45,
    yearPublished: 2017,
    imageUrl:
      'https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__original/img/qIncPzl-00XM5c8dnD9c9HTi-XM=/0x0/filters:format(jpeg)/pic6973671.jpg',
  },
  {
    id: '5',
    title: '7 Wonders Duel',
    publisher: 'Repos Production',
    rating: 8.1,
    minPlayers: 2,
    maxPlayers: 2,
    playtime: 30,
    yearPublished: 2015,
    imageUrl:
      'https://cf.geekdo-images.com/WzNs1mA_o22ZXLJ9uS9MOw__original/img/S_1xSDQ82TJ2VuGzf6NfNHJFm_0=/0x0/filters:format(jpeg)/pic2576399.jpg',
  },
  {
    id: '6',
    title: 'Terraforming Mars',
    publisher: 'FryxGames',
    rating: 8.4,
    minPlayers: 1,
    maxPlayers: 5,
    playtime: 120,
    yearPublished: 2016,
    imageUrl:
      'https://cf.geekdo-images.com/wg9oOLcsKvDesSUdZQ4rxw__original/img/FS1RE8Ue6nk1pNbPI3l-OSapQGc=/0x0/filters:format(jpeg)/pic3536616.jpg',
  },
  {
    id: '7',
    title: 'Brass: Birmingham',
    publisher: 'Roxley Games',
    rating: 8.6,
    minPlayers: 2,
    maxPlayers: 4,
    playtime: 120,
    yearPublished: 2018,
    imageUrl:
      'https://cf.geekdo-images.com/x3zxjr-Vw5iU4yDPg70Jgw__original/img/FpyxH41Y6_ROoePAilPNEhXnzO8=/0x0/filters:format(jpeg)/pic3490053.jpg',
  },
  {
    id: '8',
    title: 'Spirit Island',
    publisher: 'Greater Than Games',
    rating: 8.4,
    minPlayers: 1,
    maxPlayers: 4,
    playtime: 120,
    yearPublished: 2017,
    imageUrl:
      'https://cf.geekdo-images.com/0vJwP6NGa7GHPZKC3gplUw__original/img/cI7AHdRqLhx_-o28urDcmXFu3gE=/0x0/filters:format(jpeg)/pic7615963.jpg',
  },
  {
    id: '9',
    title: 'Scythe',
    publisher: 'Stonemaier Games',
    rating: 8.2,
    minPlayers: 1,
    maxPlayers: 5,
    playtime: 115,
    yearPublished: 2016,
    imageUrl:
      'https://cf.geekdo-images.com/7k_nOxpO9OGIjhLq2BUZdA__original/img/jIQ-YUNMF0v48zEVHxqkJvx6vOc=/0x0/filters:format(jpeg)/pic3163924.jpg',
  },
  {
    id: '10',
    title: 'Everdell',
    publisher: 'Starling Games',
    rating: 8.0,
    minPlayers: 1,
    maxPlayers: 4,
    playtime: 80,
    yearPublished: 2018,
    imageUrl:
      'https://cf.geekdo-images.com/fjE7V5LNq31yVEW_yuqI-Q__original/img/ijYTk6KGtxLRdIvLT5nIp4B72rI=/0x0/filters:format(jpeg)/pic3918905.jpg',
  },
];

interface Collection {
  id: string;
  name: string;
  description: string;
  gameCount: number;
}

const MOCK_COLLECTIONS: Collection[] = [
  {
    id: '1',
    name: 'Strategy Masterpieces',
    description: 'Deep strategy games for experienced players',
    gameCount: 24,
  },
  {
    id: '2',
    name: 'Party Favorites',
    description: 'Fun games for social gatherings',
    gameCount: 12,
  },
  {
    id: '3',
    name: 'Two Player Duels',
    description: 'Perfect for couples and competitive friends',
    gameCount: 18,
  },
  {
    id: '4',
    name: 'Family Classics',
    description: 'Games everyone can enjoy',
    gameCount: 15,
  },
];

// ============================================================================
// Main Component
// ============================================================================

export default function EntityListViewDemoPage() {
  const [loadingGames, setLoadingGames] = useState(false);
  const [loadingCollections, setLoadingCollections] = useState(false);
  const [showGames, setShowGames] = useState(true);
  const [showCollections, setShowCollections] = useState(true);
  const [clickedGame, setClickedGame] = useState<string | null>(null);

  // Client-only localStorage reading (avoid hydration mismatch)
  const [localStorageValues, setLocalStorageValues] = useState<Record<string, string>>({});

  useEffect(() => {
    // Read all localStorage keys on client-side only
    setLocalStorageValues({
      'view-mode:demo-games': localStorage.getItem('view-mode:demo-games') || '(not set)',
      'view-mode:demo-collections': localStorage.getItem('view-mode:demo-collections') || '(not set)',
      'view-mode:demo-custom-grid': localStorage.getItem('view-mode:demo-custom-grid') || '(not set)',
      'view-mode:demo-players': localStorage.getItem('view-mode:demo-players') || '(not set)',
    });
  }, []);

  return (
    <div className="container mx-auto py-8 space-y-16">
      {/* Page Header */}
      <header className="space-y-4">
        <h1 className="font-quicksand font-bold text-4xl md:text-5xl">
          EntityListView - Phase 1 Demo
        </h1>
        <p className="text-muted-foreground text-lg">
          Interactive demo for testing Grid mode, view switcher, and localStorage persistence
        </p>

        {/* Demo Controls */}
        <div className="flex flex-wrap gap-3 p-4 bg-accent/20 rounded-lg">
          <button
            onClick={() => setLoadingGames(!loadingGames)}
            className="px-4 py-2 rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 transition-colors"
          >
            {loadingGames ? 'Hide' : 'Show'} Games Loading State
          </button>
          <button
            onClick={() => setLoadingCollections(!loadingCollections)}
            className="px-4 py-2 rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 transition-colors"
          >
            {loadingCollections ? 'Hide' : 'Show'} Collections Loading State
          </button>
          <button
            onClick={() => setShowGames(!showGames)}
            className="px-4 py-2 rounded-lg bg-secondary text-secondary-foreground hover:bg-secondary/80 transition-colors"
          >
            {showGames ? 'Hide' : 'Show'} Games Section
          </button>
          <button
            onClick={() => setShowCollections(!showCollections)}
            className="px-4 py-2 rounded-lg bg-secondary text-secondary-foreground hover:bg-secondary/80 transition-colors"
          >
            {showCollections ? 'Hide' : 'Show'} Collections Section
          </button>
          <button
            onClick={() => {
              localStorage.clear();
              window.location.reload();
            }}
            className="px-4 py-2 rounded-lg bg-destructive text-destructive-foreground hover:bg-destructive/90 transition-colors"
          >
            Clear localStorage & Reload
          </button>
        </div>

        {clickedGame && (
          <div className="p-4 bg-primary/10 border border-primary/20 rounded-lg">
            <p className="text-sm">
              <strong>Last clicked:</strong> {clickedGame}
            </p>
          </div>
        )}
      </header>

      {/* Games Section (Orange) */}
      {showGames && (
        <section>
          <EntityListView
            items={MOCK_GAMES}
            entity="game"
            persistenceKey="demo-games"
            defaultViewMode="grid"
            renderItem={(game) => ({
              id: game.id,
              title: game.title,
              subtitle: game.publisher,
              imageUrl: game.imageUrl,
              rating: game.rating,
              ratingMax: 10,
              metadata: [
                { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
                { icon: Clock, value: `${game.playtime}m` },
                { icon: Calendar, value: game.yearPublished.toString() },
              ],
              badge: `${game.rating.toFixed(1)}`,
            })}
            onItemClick={(game) => {
              setClickedGame(game.title);
              console.log('Game clicked:', game);
            }}
            title="Featured Games"
            subtitle="10 top-rated board games - Phase 1: Grid mode only"
            gridColumns={{ default: 1, sm: 2, lg: 3, xl: 4 }}
            gridGap={4}
            loading={loadingGames}
            emptyMessage="No games available. Try adding some games!"
          />
        </section>
      )}

      {/* Collections Section (Teal) */}
      {showCollections && (
        <section>
          <EntityListView
            items={MOCK_COLLECTIONS}
            entity="collection"
            persistenceKey="demo-collections"
            defaultViewMode="grid"
            renderItem={(collection) => ({
              id: collection.id,
              title: collection.name,
              subtitle: collection.description,
              metadata: [{ icon: Star, value: `${collection.gameCount} games` }],
            })}
            onItemClick={(collection) => {
              setClickedGame(collection.name);
              console.log('Collection clicked:', collection);
            }}
            title="My Collections"
            subtitle="Organized game collections - Phase 1: Grid mode only"
            gridColumns={{ default: 1, sm: 2, lg: 2, xl: 3 }}
            gridGap={6}
            loading={loadingCollections}
            emptyMessage="No collections yet. Create your first collection!"
          />
        </section>
      )}

      {/* Empty State Demo */}
      <section>
        <EntityListView
          items={[]}
          entity="event"
          persistenceKey="demo-events"
          renderItem={(event: any) => ({
            id: event.id,
            title: event.title,
          })}
          title="Events (Empty State Demo)"
          subtitle="This section demonstrates the empty state"
          emptyMessage="No upcoming events. Check back soon!"
        />
      </section>

      {/* Custom Grid Layout Demo */}
      <section>
        <EntityListView
          items={MOCK_GAMES.slice(0, 6)}
          entity="game"
          persistenceKey="demo-custom-grid"
          renderItem={(game) => ({
            id: game.id,
            title: game.title,
            subtitle: game.publisher,
            imageUrl: game.imageUrl,
            rating: game.rating,
            ratingMax: 10,
            metadata: [
              { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
              { icon: Clock, value: `${game.playtime}m` },
            ],
          })}
          onItemClick={(game) => {
            setClickedGame(`Custom Grid: ${game.title}`);
          }}
          title="Custom Grid Layout"
          subtitle="5 columns on 2XL screens, larger gap"
          gridColumns={{ default: 1, sm: 2, md: 3, lg: 4, xl: 5, '2xl': 5 }}
          gridGap={8}
        />
      </section>

      {/* Player Entity Demo (Purple) */}
      <section>
        <EntityListView
          items={[
            { id: '1', name: 'Marco Rossi', username: '@marco_games', plays: 142 },
            { id: '2', name: 'Anna Bianchi', username: '@anna_board', plays: 89 },
            { id: '3', name: 'Luca Verdi', username: '@luca_tabletop', plays: 201 },
            { id: '4', name: 'Sofia Neri', username: '@sofia_strategy', plays: 156 },
          ]}
          entity="player"
          persistenceKey="demo-players"
          renderItem={(player: any) => ({
            id: player.id,
            title: player.name,
            subtitle: player.username,
            metadata: [
              { icon: TrendingUp, value: `${player.plays} plays` },
              { icon: Star, value: 'Top 5%' },
            ],
          })}
          onItemClick={(player: any) => {
            setClickedGame(`Player: ${player.name}`);
          }}
          title="Top Players"
          subtitle="Purple entity type with player-specific metadata"
          gridColumns={{ default: 1, sm: 2, lg: 4 }}
        />
      </section>

      {/* Feature Testing Info */}
      <section className="p-6 bg-accent/10 border border-border rounded-xl space-y-4">
        <h2 className="font-quicksand font-bold text-2xl">Phase 1 Features to Test</h2>

        <div className="grid md:grid-cols-2 gap-6">
          {/* Feature Checklist */}
          <div className="space-y-2">
            <h3 className="font-semibold text-lg mb-3">✅ Implemented Features</h3>
            <ul className="space-y-2 text-sm">
              <li className="flex items-start gap-2">
                <span className="text-green-500 mt-0.5">✓</span>
                <span>
                  <strong>Grid Layout:</strong> Responsive columns (1→2→3→4)
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-500 mt-0.5">✓</span>
                <span>
                  <strong>ViewModeSwitcher:</strong> UI control (Grid only for Phase 1)
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-500 mt-0.5">✓</span>
                <span>
                  <strong>localStorage Persistence:</strong> Preferences saved per section
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-500 mt-0.5">✓</span>
                <span>
                  <strong>Empty State:</strong> Graceful handling of empty lists
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-500 mt-0.5">✓</span>
                <span>
                  <strong>Loading State:</strong> Skeleton loaders (toggle with buttons)
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-500 mt-0.5">✓</span>
                <span>
                  <strong>Entity Types:</strong> Game (orange), Collection (teal), Player (purple)
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="text-green-500 mt-0.5">✓</span>
                <span>
                  <strong>Click Handling:</strong> onItemClick callback (see banner above)
                </span>
              </li>
            </ul>
          </div>

          {/* Testing Instructions */}
          <div className="space-y-2">
            <h3 className="font-semibold text-lg mb-3">🧪 Manual Testing</h3>
            <ol className="space-y-2 text-sm list-decimal list-inside">
              <li>
                <strong>Resize browser:</strong> Watch responsive columns adapt
              </li>
              <li>
                <strong>Click cards:</strong> See clicked game name in banner
              </li>
              <li>
                <strong>Toggle loading:</strong> Test skeleton states
              </li>
              <li>
                <strong>Hide sections:</strong> Test conditional rendering
              </li>
              <li>
                <strong>Reload page:</strong> Verify localStorage persistence
              </li>
              <li>
                <strong>Open DevTools:</strong> Check localStorage keys:
                <code className="ml-2 px-2 py-0.5 bg-muted rounded text-xs">
                  view-mode:demo-games
                </code>
              </li>
              <li>
                <strong>Keyboard nav:</strong> Tab to ViewModeSwitcher, test focus
              </li>
              <li>
                <strong>Accessibility:</strong> Test with screen reader (NVDA/JAWS)
              </li>
            </ol>
          </div>
        </div>

        {/* Coming in Phase 2 */}
        <div className="mt-6 p-4 bg-amber-500/10 border border-amber-500/20 rounded-lg">
          <h3 className="font-semibold text-lg mb-2">📅 Coming in Phase 2</h3>
          <ul className="text-sm space-y-1 text-muted-foreground">
            <li>• List view mode (compact vertical layout)</li>
            <li>• Carousel view mode (3D horizontal scroll)</li>
            <li>• Full 3-mode switching with ViewModeSwitcher</li>
          </ul>
        </div>
      </section>

      {/* localStorage Viewer */}
      <section className="p-6 bg-muted/50 rounded-xl">
        <h2 className="font-quicksand font-bold text-xl mb-4">localStorage Inspector</h2>
        <div className="space-y-2 font-mono text-xs">
          {Object.entries(localStorageValues).map(([key, value]) => (
            <div key={key} className="flex justify-between p-2 bg-background rounded">
              <span className="text-muted-foreground">{key}</span>
              <span className="text-foreground">{value}</span>
            </div>
          ))}
        </div>
        <p className="mt-4 text-xs text-muted-foreground">
          Open DevTools → Application → Local Storage to see full state
        </p>
      </section>
    </div>
  );
}
