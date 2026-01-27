/**
 * Collection-Centric Dashboard - Option A
 *
 * Editorial Gaming aesthetic: Sophisticated yet playful, organized but not sterile
 *
 * Design Direction:
 * - Typography: Display caratteriale (Playfair Display) + sans serif leggibile (Geist Sans)
 * - Color: Earthy palette (burnt orange #D97706, olive green #65A30D, deep blue #1E40AF)
 * - Motion: Fluid micro-interactions, card flip animations, glassmorphic depth
 * - Layout: Asymmetric grid with grid-breaking elements
 *
 * Features:
 * - Flip-on-hover game cards revealing detailed stats
 * - Collapsible filter sidebar (desktop) / bottom sheet (mobile)
 * - Prominent search with live suggestions
 * - Collection stats with glassmorphic cards
 * - Floating Action Button for quick add
 *
 * @see Issue #[TBD] - Collection Dashboard Redesign
 */

'use client';

import React, { useState } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { Search, Filter, Plus, Grid3x3, List, TrendingUp, Star, Gamepad2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { cn } from '@/lib/utils';

// Types
interface CollectionStats {
  totalGames: number;
  playedLast30Days: number;
  currentStreak: number;
  wishlistCount: number;
  trendingThisMonth: number;
}

interface GameCardData {
  id: string;
  title: string;
  coverUrl: string;
  rating: number;
  complexity: 1 | 2 | 3;
  playCount: number;
  avgDuration: number;
  lastPlayed?: Date;
  isInWishlist: boolean;
}

interface FilterState {
  categories: string[];
  complexity: number[];
  playerCount: string;
  status: 'all' | 'owned' | 'played' | 'wishlist';
  sortBy: 'recent' | 'alphabetical' | 'rating' | 'duration';
}

// Mock data (replace with real API calls)
const MOCK_STATS: CollectionStats = {
  totalGames: 127,
  playedLast30Days: 23,
  currentStreak: 7,
  wishlistCount: 15,
  trendingThisMonth: 3,
};

const MOCK_GAMES: GameCardData[] = Array.from({ length: 12 }, (_, i) => ({
  id: `game-${i}`,
  title: `Board Game Title ${i + 1}`,
  coverUrl: `https://picsum.photos/seed/${i}/300/400`,
  rating: Math.floor(Math.random() * 5) + 1,
  complexity: (Math.floor(Math.random() * 3) + 1) as 1 | 2 | 3,
  playCount: Math.floor(Math.random() * 50),
  avgDuration: Math.floor(Math.random() * 120) + 30,
  lastPlayed: Math.random() > 0.5 ? new Date(Date.now() - Math.random() * 30 * 24 * 60 * 60 * 1000) : undefined,
  isInWishlist: Math.random() > 0.7,
}));

export function CollectionDashboard() {
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [filters, setFilters] = useState<FilterState>({
    categories: [],
    complexity: [],
    playerCount: 'all',
    status: 'all',
    sortBy: 'recent',
  });

  return (
    <div className="min-h-screen bg-gradient-to-br from-stone-50 via-amber-50/30 to-emerald-50/20">
      {/* Header con profilo e navigazione */}
      <header className="sticky top-0 z-50 backdrop-blur-[16px] backdrop-saturate-[180%] bg-background/95 dark:bg-card dark:backdrop-blur-none border-b border-border/50 dark:border-border/30 shadow-sm">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between gap-4">
            {/* Logo / Brand */}
            <div className="flex items-center gap-2">
              <Gamepad2 className="h-6 w-6 text-amber-600" />
              <span className="font-playfair text-xl font-bold bg-gradient-to-r from-amber-700 to-emerald-700 bg-clip-text text-transparent">
                MeepleAI
              </span>
            </div>

            {/* Search Bar (Desktop) */}
            <div className="hidden md:flex flex-1 max-w-2xl">
              <div className="relative w-full">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-stone-400" />
                <Input
                  type="search"
                  placeholder="Cerca giochi, editori, designer..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-10 pr-4 py-6 rounded-full border-2 border-stone-200 focus:border-amber-500 focus:ring-4 focus:ring-amber-500/10 transition-all duration-300 shadow-sm hover:shadow-md"
                />
              </div>
            </div>

            {/* User Profile */}
            <div className="flex items-center gap-3">
              <Button variant="ghost" size="icon" className="rounded-full">
                <span className="sr-only">Notifiche</span>
                <div className="relative">
                  <div className="h-2 w-2 bg-amber-500 rounded-full absolute -top-0.5 -right-0.5 animate-pulse" />
                  <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                  </svg>
                </div>
              </Button>
              <div className="h-10 w-10 rounded-full bg-gradient-to-br from-amber-400 to-emerald-500 flex items-center justify-center text-white font-bold shadow-md">
                MU
              </div>
            </div>
          </div>

          {/* Search Bar (Mobile) */}
          <div className="md:hidden mt-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-stone-400" />
              <Input
                type="search"
                placeholder="Cerca..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pl-9 pr-4 py-2 rounded-full border-2 border-stone-200 focus:border-amber-500 text-sm"
              />
            </div>
          </div>
        </div>
      </header>

      {/* Stats Overview con Glassmorphism */}
      <section className="container mx-auto px-4 py-8">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <StatsCard
            icon={<Gamepad2 className="h-6 w-6" />}
            label="Totale Giochi"
            value={MOCK_STATS.totalGames}
            gradient="from-amber-500/20 to-orange-500/20"
            borderColor="border-amber-500/30"
          />
          <StatsCard
            icon={<TrendingUp className="h-6 w-6" />}
            label="Giocati (30gg)"
            value={MOCK_STATS.playedLast30Days}
            subtext={`🔥 Streak: ${MOCK_STATS.currentStreak} giorni`}
            gradient="from-emerald-500/20 to-teal-500/20"
            borderColor="border-emerald-500/30"
          />
          <StatsCard
            icon={<Star className="h-6 w-6" />}
            label="Wishlist"
            value={MOCK_STATS.wishlistCount}
            subtext={`+${MOCK_STATS.trendingThisMonth} questo mese`}
            gradient="from-blue-500/20 to-indigo-500/20"
            borderColor="border-blue-500/30"
          />
          <StatsCard
            icon={<Grid3x3 className="h-6 w-6" />}
            label="Trend"
            value={`+${MOCK_STATS.trendingThisMonth}`}
            subtext="giochi in crescita"
            gradient="from-purple-500/20 to-pink-500/20"
            borderColor="border-purple-500/30"
          />
        </div>
      </section>

      {/* Main Content: Sidebar + Grid */}
      <div className="container mx-auto px-4 pb-8">
        <div className="flex flex-col md:flex-row gap-6">
          {/* Filter Sidebar */}
          <AnimatePresence mode="wait">
            {sidebarOpen && (
              <motion.aside
                initial={{ width: 0, opacity: 0 }}
                animate={{ width: 'auto', opacity: 1 }}
                exit={{ width: 0, opacity: 0 }}
                transition={{ duration: 0.3, ease: 'easeInOut' }}
                className="md:w-72 flex-shrink-0"
              >
                <FilterSidebar filters={filters} onChange={setFilters} />
              </motion.aside>
            )}
          </AnimatePresence>

          {/* Games Grid */}
          <main className="flex-1 space-y-6">
            {/* Toolbar */}
            <div className="flex items-center justify-between gap-4 flex-wrap">
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setSidebarOpen(!sidebarOpen)}
                  className="gap-2"
                >
                  <Filter className="h-4 w-4" />
                  Filtri
                </Button>
                <Badge variant="secondary" className="text-xs">
                  {MOCK_GAMES.length} giochi
                </Badge>
              </div>

              <div className="flex items-center gap-2">
                <Button
                  variant={viewMode === 'grid' ? 'default' : 'ghost'}
                  size="icon"
                  onClick={() => setViewMode('grid')}
                >
                  <Grid3x3 className="h-4 w-4" />
                </Button>
                <Button
                  variant={viewMode === 'list' ? 'default' : 'ghost'}
                  size="icon"
                  onClick={() => setViewMode('list')}
                >
                  <List className="h-4 w-4" />
                </Button>
              </div>
            </div>

            {/* Games Grid/List */}
            <div
              className={cn(
                'grid gap-6',
                viewMode === 'grid'
                  ? 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4'
                  : 'grid-cols-1'
              )}
            >
              {MOCK_GAMES.map((game) => (
                <GameCardFlip key={game.id} game={game} viewMode={viewMode} />
              ))}
            </div>
          </main>
        </div>
      </div>

      {/* Floating Action Button */}
      <motion.button
        whileHover={{ scale: 1.1 }}
        whileTap={{ scale: 0.95 }}
        className="fixed bottom-6 right-6 h-14 w-14 rounded-full bg-gradient-to-br from-amber-500 to-orange-600 text-white shadow-2xl shadow-amber-500/50 flex items-center justify-center hover:shadow-amber-500/70 transition-shadow duration-300 z-40"
      >
        <Plus className="h-6 w-6" />
      </motion.button>
    </div>
  );
}

// Stats Card Component con Glassmorphism
function StatsCard({
  icon,
  label,
  value,
  subtext,
  gradient,
  borderColor,
}: {
  icon: React.ReactNode;
  label: string;
  value: number | string;
  subtext?: string;
  gradient: string;
  borderColor: string;
}) {
  return (
    <motion.div
      whileHover={{ y: -4, scale: 1.02 }}
      transition={{ duration: 0.2 }}
      className={cn(
        'relative overflow-hidden rounded-2xl p-4 backdrop-blur-xl bg-gradient-to-br',
        gradient,
        'border-2',
        borderColor,
        'shadow-lg hover:shadow-xl transition-shadow duration-300'
      )}
    >
      {/* Decorative gradient orb */}
      <div className="absolute -top-8 -right-8 h-24 w-24 rounded-full bg-background/20 blur-2xl" />

      <div className="relative space-y-2">
        <div className="flex items-center justify-between">
          <div className="text-stone-600">{icon}</div>
        </div>
        <div>
          <div className="text-3xl font-bold font-playfair text-stone-900">{value}</div>
          <div className="text-xs font-medium text-stone-600 uppercase tracking-wider">{label}</div>
          {subtext && <div className="text-xs text-stone-500 mt-1">{subtext}</div>}
        </div>
      </div>
    </motion.div>
  );
}

// Filter Sidebar Component
function FilterSidebar({
  filters: _filters,
  onChange: _onChange,
}: {
  filters: FilterState;
  onChange: (filters: FilterState) => void;
}) {
  return (
    <div className="bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none rounded-2xl border border-border/50 dark:border-border/30 p-6 space-y-6 shadow-lg sticky top-24">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-playfair font-bold text-stone-900">Filtri</h2>
        <Button variant="ghost" size="sm" className="text-xs text-amber-600 hover:text-amber-700">
          Reset
        </Button>
      </div>

      {/* Categories */}
      <div className="space-y-3">
        <h3 className="text-sm font-semibold text-stone-700 uppercase tracking-wider">Categoria</h3>
        <div className="flex flex-wrap gap-2">
          {['Strategia', 'Party', 'Famiglia', 'Astratto', 'Cooperativo'].map((cat) => (
            <Badge
              key={cat}
              variant="outline"
              className="cursor-pointer hover:bg-amber-100 hover:border-amber-500 transition-colors"
            >
              {cat}
            </Badge>
          ))}
        </div>
      </div>

      {/* Complexity */}
      <div className="space-y-3">
        <h3 className="text-sm font-semibold text-stone-700 uppercase tracking-wider">Difficoltà</h3>
        <div className="space-y-2">
          {[
            { label: 'Facile', value: 1 },
            { label: 'Medio', value: 2 },
            { label: 'Difficile', value: 3 },
          ].map((level) => (
            <label key={level.value} className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" className="rounded border-stone-300 text-amber-600 focus:ring-amber-500" />
              <span className="text-sm text-stone-700">{level.label}</span>
              <span className="text-amber-600">{'●'.repeat(level.value)}{'○'.repeat(3 - level.value)}</span>
            </label>
          ))}
        </div>
      </div>

      {/* Player Count */}
      <div className="space-y-3">
        <h3 className="text-sm font-semibold text-stone-700 uppercase tracking-wider">Giocatori</h3>
        <div className="flex flex-wrap gap-2">
          {['1-2', '3-4', '5+'].map((range) => (
            <Badge
              key={range}
              variant="outline"
              className="cursor-pointer hover:bg-emerald-100 hover:border-emerald-500 transition-colors"
            >
              {range}
            </Badge>
          ))}
        </div>
      </div>
    </div>
  );
}

// Game Card with Flip Animation
function GameCardFlip({ game, viewMode }: { game: GameCardData; viewMode: 'grid' | 'list' }) {
  const [isFlipped, setIsFlipped] = useState(false);

  if (viewMode === 'list') {
    // List view - no flip, horizontal layout
    return (
      <div className="bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none rounded-xl border border-border/50 dark:border-border/30 p-4 hover:shadow-lg transition-shadow duration-300 flex gap-4">
        <img src={game.coverUrl} alt={game.title} className="w-24 h-32 object-cover rounded-lg" />
        <div className="flex-1">
          <h3 className="font-playfair font-bold text-lg text-stone-900">{game.title}</h3>
          <div className="flex items-center gap-2 mt-2">
            <div className="text-amber-500">{'★'.repeat(game.rating)}{'☆'.repeat(5 - game.rating)}</div>
            <span className="text-xs text-stone-500">({game.playCount} partite)</span>
          </div>
        </div>
      </div>
    );
  }

  // Grid view - flip card
  return (
    <motion.div
      className="relative h-80 perspective-1000"
      onHoverStart={() => setIsFlipped(true)}
      onHoverEnd={() => setIsFlipped(false)}
    >
      <motion.div
        className="relative w-full h-full preserve-3d"
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.6, type: 'spring' }}
      >
        {/* Front */}
        <div className="absolute inset-0 backface-hidden">
          <div className="relative h-full rounded-2xl overflow-hidden shadow-xl group">
            <img src={game.coverUrl} alt={game.title} className="w-full h-full object-cover" />
            <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent" />
            <div className="absolute bottom-0 left-0 right-0 p-4 space-y-2">
              <h3 className="font-playfair font-bold text-white text-lg">{game.title}</h3>
              <div className="flex items-center justify-between">
                <div className="text-amber-400 text-sm">{'★'.repeat(game.rating)}{'☆'.repeat(5 - game.rating)}</div>
                <Badge variant="secondary" className="text-xs">
                  {'●'.repeat(game.complexity)}{'○'.repeat(3 - game.complexity)}
                </Badge>
              </div>
            </div>
          </div>
        </div>

        {/* Back */}
        <div className="absolute inset-0 backface-hidden rotate-y-180">
          <div className="h-full rounded-2xl bg-gradient-to-br from-amber-50 to-orange-50 border-2 border-amber-200 p-6 flex flex-col justify-between shadow-xl backdrop-blur-xl">
            <div className="space-y-4">
              <h3 className="font-playfair font-bold text-stone-900 text-lg">{game.title}</h3>

              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-stone-600">Partite giocate:</span>
                  <span className="font-bold text-stone-900">{game.playCount}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-stone-600">Durata media:</span>
                  <span className="font-bold text-stone-900">{game.avgDuration} min</span>
                </div>
                {game.lastPlayed && (
                  <div className="flex justify-between">
                    <span className="text-stone-600">Ultimo gioco:</span>
                    <span className="font-bold text-stone-900">
                      {new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'short' }).format(game.lastPlayed)}
                    </span>
                  </div>
                )}
              </div>
            </div>

            <div className="space-y-2">
              <Button size="sm" className="w-full bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700">
                ✓ Segna Giocato
              </Button>
              <Button size="sm" variant="outline" className="w-full">
                {game.isInWishlist ? '⭐ Rimuovi Wishlist' : '☆ Aggiungi Wishlist'}
              </Button>
            </div>
          </div>
        </div>
      </motion.div>
    </motion.div>
  );
}

