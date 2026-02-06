/**
 * Dashboard - Main Navigation Hub
 * Issue #3286 - User Dashboard Layout System
 *
 * The primary navigation hub for MeepleAI featuring:
 * - 6 reorderable sections (drag-to-reorder)
 * - Grid/List view toggle per section
 * - Sticky ActionBar per focused section
 * - Global search with filters
 * - Warm Tabletop aesthetic
 *
 * @example
 * ```tsx
 * <Dashboard />
 * ```
 */

'use client';

import { useState, useCallback, useRef, useEffect, useMemo } from 'react';

import { motion, AnimatePresence, Reorder } from 'framer-motion';
import {
  Zap,
  Clock,
  Gamepad2,
  Users,
  Heart,
  Bell,
  Plus,
  FileText,
  Bot,
  Dices,
  Search,
  Filter,
  type LucideIcon,
} from 'lucide-react';

import { CollectionFilters } from '@/components/collection/CollectionFilters';
import { CollectionStatsBar } from '@/components/collection/CollectionStatsBar';
import { useLayout } from '@/components/layout/LayoutProvider';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import type { CollectionHeroStats, CollectionFilters as CollectionFiltersType } from '@/types/collection';

import { ActiveSessionsWidget } from './ActiveSessionsWidget';
import { ActivityFeed } from './ActivityFeed';
import { ActivityItem, type ActivityData } from './ActivityItem';
import { ChatHistorySection } from './ChatHistorySection';
import { DashboardHeader } from './DashboardHeader';
import { DashboardSection, type ViewMode } from './DashboardSection';
import { GameCard, type GameData } from './GameCard';
import { HeroStats } from './HeroStats';
import { LibrarySnapshot } from './LibrarySnapshot';
import { NotificationItem, type NotificationData } from './NotificationItem';
import { QuickActionCard } from './QuickActionCard';
import { QuickActionsGrid } from './QuickActionsGrid';
import { WishlistCard, type WishlistItemData } from './WishlistCard';

// Collection Components - Issue #3649

// ============================================================================
// Types
// ============================================================================

/** Section configuration for ordering and display */
export interface SectionConfig {
  id: string;
  title: string;
  icon: LucideIcon;
  viewMode: ViewMode;
  collapsed: boolean;
  order: number;
}

/** Quick action data */
interface QuickActionData {
  icon: LucideIcon;
  title: string;
  description: string;
  onClick: () => void;
}

// ============================================================================
// Mock Data (to be replaced with API calls)
// ============================================================================

const MOCK_QUICK_ACTIONS: QuickActionData[] = [
  {
    icon: Plus,
    title: 'Aggiungi Gioco',
    description: 'Cerca o crea un nuovo gioco',
    onClick: () => {
      /* TODO: Navigate to add game */
    },
  },
  {
    icon: FileText,
    title: 'Carica Regolamento',
    description: 'Upload PDF o documento',
    onClick: () => {
      /* TODO: Open upload rules dialog */
    },
  },
  {
    icon: Bot,
    title: "Chiedi all'AI",
    description: 'Assistente regole e strategie',
    onClick: () => {
      /* TODO: Open AI assistant */
    },
  },
  {
    icon: Dices,
    title: 'Nuova Partita',
    description: 'Registra una sessione di gioco',
    onClick: () => {
      /* TODO: Start new session */
    },
  },
];

const MOCK_ACTIVITIES: ActivityData[] = [
  {
    id: '1',
    type: 'game_added',
    title: 'Wingspan aggiunto alla collezione',
    timestamp: new Date(Date.now() - 1000 * 60 * 30),
    game: { id: 'g1', name: 'Wingspan' },
  },
  {
    id: '2',
    type: 'session_played',
    title: 'Partita a Catan completata',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2),
    game: { id: 'g2', name: 'Catan' },
    details: 'Vincitore: Mario • 4 giocatori • 90 min',
  },
  {
    id: '3',
    type: 'ai_interaction',
    title: "Domanda all'AI su Gloomhaven",
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 5),
    game: { id: 'g3', name: 'Gloomhaven' },
  },
  {
    id: '4',
    type: 'share_received',
    title: 'Luigi ti ha condiviso Terraforming Mars',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24),
    game: { id: 'g4', name: 'Terraforming Mars' },
    user: { id: 'u1', name: 'Luigi' },
  },
];

const MOCK_GAMES: GameData[] = [
  {
    id: 'g1',
    name: 'Wingspan',
    imageUrl: '/images/games/wingspan.jpg',
    rating: 9,
    playCount: 12,
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 3),
    isFavorite: true,
    ownershipStatus: 'OWNED',
    hasPdf: true,
    hasActiveChat: true,
  },
  {
    id: 'g2',
    name: 'Catan',
    imageUrl: '/images/games/catan.jpg',
    rating: 8,
    playCount: 45,
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 2),
    isFavorite: false,
    ownershipStatus: 'OWNED',
    hasPdf: false,
    hasActiveChat: true,
  },
  {
    id: 'g3',
    name: 'Gloomhaven',
    imageUrl: '/images/games/gloomhaven.jpg',
    rating: 10,
    playCount: 8,
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7),
    isFavorite: true,
    ownershipStatus: 'OWNED',
    location: 'Scaffale B',
    hasPdf: true,
    hasActiveChat: false,
  },
  {
    id: 'g4',
    name: 'Terraforming Mars',
    imageUrl: '/images/games/terraforming.jpg',
    rating: 9,
    playCount: 15,
    isFavorite: false,
    ownershipStatus: 'LENT_OUT',
    location: 'Da Mario',
    hasPdf: true,
    hasActiveChat: true,
  },
  {
    id: 'g5',
    name: 'Azul',
    imageUrl: '/images/games/azul.jpg',
    rating: 8,
    playCount: 20,
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2),
    isFavorite: false,
    ownershipStatus: 'OWNED',
    hasPdf: false,
    hasActiveChat: false,
  },
  {
    id: 'g6',
    name: 'Scythe',
    imageUrl: '/images/games/scythe.jpg',
    rating: 9,
    playCount: 6,
    isFavorite: true,
    ownershipStatus: 'OWNED',
    hasPdf: true,
    hasActiveChat: false,
  },
];

// Mock Hero Stats for Collection Section - Issue #3649
const MOCK_COLLECTION_HERO_STATS: CollectionHeroStats = {
  totalGames: 6,
  privatePdfsCount: 4,
  totalSessions: 106,
  gamesPlayedThisMonth: 3,
};

const MOCK_SHARED_GAMES = {
  sharedByMe: MOCK_GAMES.slice(0, 2),
  sharedWithMe: MOCK_GAMES.slice(2, 4),
  pending: MOCK_GAMES.slice(4, 5),
};

const MOCK_WISHLIST: WishlistItemData[] = [
  {
    id: 'w1',
    game: { id: 'wg1', name: 'Ark Nova', imageUrl: '/images/games/arknova.jpg' },
    priority: 'HIGH',
    targetPrice: 55,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 10),
    visibility: 'FRIENDS',
  },
  {
    id: 'w2',
    game: { id: 'wg2', name: 'Spirit Island', imageUrl: '/images/games/spirit.jpg' },
    priority: 'MEDIUM',
    notes: 'Espansione Jagged Earth inclusa',
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 30),
    visibility: 'PRIVATE',
  },
  {
    id: 'w3',
    game: { id: 'wg3', name: 'Brass Birmingham', imageUrl: '/images/games/brass.jpg' },
    priority: 'LOW',
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 60),
    visibility: 'PUBLIC',
  },
];

const MOCK_NOTIFICATIONS: NotificationData[] = [
  {
    id: 'n1',
    type: 'SHARE',
    title: 'Mario ti ha condiviso Catan',
    body: 'Puoi ora accedere alle regole e ai documenti.',
    status: 'UNREAD',
    isActionable: true,
    actions: [
      { id: 'accept', label: 'Accetta', actionType: 'ACCEPT' },
      { id: 'reject', label: 'Rifiuta', actionType: 'REJECT' },
    ],
    createdAt: new Date(Date.now() - 1000 * 60 * 15),
  },
  {
    id: 'n2',
    type: 'LOAN',
    title: 'Promemoria prestito',
    body: 'Gloomhaven è in prestito a Luigi da 30 giorni.',
    status: 'UNREAD',
    isActionable: true,
    actions: [
      { id: 'remind', label: 'Invia promemoria', actionType: 'REMIND' },
      { id: 'dismiss', label: 'Ignora', actionType: 'DISMISS' },
    ],
    createdAt: new Date(Date.now() - 1000 * 60 * 60 * 2),
  },
  {
    id: 'n3',
    type: 'WISHLIST',
    title: 'Offerta Ark Nova!',
    body: 'Ark Nova è in offerta a 45€ su Amazon.',
    status: 'READ',
    isActionable: true,
    deepLink: 'https://amazon.it/...',
    actions: [{ id: 'view', label: 'Vai', actionType: 'EXTERNAL_LINK' }],
    createdAt: new Date(Date.now() - 1000 * 60 * 60 * 24),
  },
  {
    id: 'n4',
    type: 'AI',
    title: 'Nuove FAQ per Wingspan',
    body: "L'AI ha generato nuove risposte basate sulle tue domande.",
    status: 'READ',
    isActionable: false,
    createdAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2),
  },
];

// ============================================================================
// Default Section Configuration
// ============================================================================

const DEFAULT_SECTIONS: SectionConfig[] = [
  { id: 'quick-actions', title: 'Azioni Rapide', icon: Zap, viewMode: 'grid', collapsed: false, order: 0 },
  { id: 'recent-activity', title: 'Attività Recente', icon: Clock, viewMode: 'list', collapsed: false, order: 1 },
  { id: 'collection', title: 'La Mia Collezione', icon: Gamepad2, viewMode: 'grid', collapsed: false, order: 2 },
  { id: 'shared-games', title: 'Giochi Condivisi', icon: Users, viewMode: 'grid', collapsed: false, order: 3 },
  { id: 'wishlist', title: 'Lista Desideri', icon: Heart, viewMode: 'grid', collapsed: false, order: 4 },
  { id: 'notifications', title: 'Notifiche', icon: Bell, viewMode: 'list', collapsed: false, order: 5 },
];

// ============================================================================
// Dashboard Component
// ============================================================================

export function Dashboard() {
  const { responsive, registerActions, clearActions } = useLayout();
  const { isMobile } = responsive;

  // Section configuration state
  const [sections, setSections] = useState<SectionConfig[]>(DEFAULT_SECTIONS);
  const [activeSectionId, setActiveSectionId] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);

  // Section refs for intersection observer
  const sectionRefs = useRef<Map<string, HTMLElement>>(new Map());

  // Collection filter state
  const [collectionFilter, setCollectionFilter] = useState<'all' | 'mine' | 'accessible' | 'lent'>('all');

  // Collection inline filters state - Issue #3649
  const [collectionInlineFilters, setCollectionInlineFilters] = useState<CollectionFiltersType>({
    hasPdf: null,
    hasActiveChat: null,
    category: null,
  });

  // Shared games tab state
  const [sharedTab, setSharedTab] = useState<'byMe' | 'withMe' | 'pending'>('byMe');

  // Section handlers
  const handleViewModeChange = useCallback((sectionId: string, mode: ViewMode) => {
    setSections((prev) =>
      prev.map((s) => (s.id === sectionId ? { ...s, viewMode: mode } : s))
    );
  }, []);

  const handleCollapseToggle = useCallback((sectionId: string) => {
    setSections((prev) =>
      prev.map((s) => (s.id === sectionId ? { ...s, collapsed: !s.collapsed } : s))
    );
  }, []);

  const handleSectionReorder = useCallback((newSections: SectionConfig[]) => {
    setSections(newSections.map((s, i) => ({ ...s, order: i })));
  }, []);

  // Intersection Observer for active section detection
  useEffect(() => {
    const observers = new Map<string, IntersectionObserver>();

    sections.forEach((section) => {
      const element = sectionRefs.current.get(section.id);
      if (!element) return;

      const observer = new IntersectionObserver(
        (entries) => {
          entries.forEach((entry) => {
            if (entry.isIntersecting && entry.intersectionRatio >= 0.3) {
              setActiveSectionId(section.id);
            }
          });
        },
        { threshold: [0.3, 0.5, 0.7], rootMargin: '-100px 0px -40% 0px' }
      );

      observer.observe(element);
      observers.set(section.id, observer);
    });

    return () => {
      observers.forEach((observer) => observer.disconnect());
    };
  }, [sections]);

  // Register context-aware actions based on active section
  useEffect(() => {
    const activeSection = sections.find((s) => s.id === activeSectionId);
    if (!activeSection) {
      clearActions();
      return;
    }

    const sectionActions = getSectionActions(activeSection.id);
    registerActions(sectionActions);

    return () => clearActions();
  }, [activeSectionId, sections, registerActions, clearActions]);

  // Get filtered games based on collection filter and inline filters - Issue #3649
  const filteredGames = useMemo(() => {
    let games = MOCK_GAMES;

    // Apply ownership filter
    switch (collectionFilter) {
      case 'mine':
        games = games.filter((g) => g.ownershipStatus === 'OWNED');
        break;
      case 'accessible':
        // Would filter by shared access
        break;
      case 'lent':
        games = games.filter((g) => g.ownershipStatus === 'LENT_OUT');
        break;
    }

    // Apply inline filters - Issue #3649
    if (collectionInlineFilters.hasPdf !== null) {
      games = games.filter((g) => g.hasPdf === collectionInlineFilters.hasPdf);
    }
    if (collectionInlineFilters.hasActiveChat !== null) {
      games = games.filter((g) => g.hasActiveChat === collectionInlineFilters.hasActiveChat);
    }
    // Category filter would be applied here when games have category field

    return games;
  }, [collectionFilter, collectionInlineFilters]);

  // Get shared games based on tab
  const sharedGames = useMemo(() => {
    switch (sharedTab) {
      case 'byMe':
        return MOCK_SHARED_GAMES.sharedByMe;
      case 'withMe':
        return MOCK_SHARED_GAMES.sharedWithMe;
      case 'pending':
        return MOCK_SHARED_GAMES.pending;
      default:
        return [];
    }
  }, [sharedTab]);

  // Render section content based on ID
  const renderSectionContent = useCallback(
    (section: SectionConfig) => {
      switch (section.id) {
        case 'quick-actions':
          return (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {MOCK_QUICK_ACTIONS.map((action) => (
                <QuickActionCard
                  key={action.title}
                  icon={action.icon}
                  title={action.title}
                  description={action.description}
                  onClick={action.onClick}
                />
              ))}
            </div>
          );

        case 'recent-activity':
          return (
            <div className="space-y-2">
              <AnimatePresence mode="popLayout">
                {MOCK_ACTIVITIES.map((activity, index) => (
                  <motion.div
                    key={activity.id}
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, x: -20 }}
                    transition={{ delay: index * 0.05 }}
                  >
                    <ActivityItem data={activity} />
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          );

        case 'collection':
          return (
            <>
              {/* Hero Stats Bar - Issue #3649 */}
              <CollectionStatsBar
                stats={MOCK_COLLECTION_HERO_STATS}
                className="mb-4"
              />

              {/* Inline Filters - Issue #3649 */}
              <CollectionFilters
                filters={collectionInlineFilters}
                onFilterChange={setCollectionInlineFilters}
                className="mb-4"
              />

              {/* Ownership Filter tabs */}
              <div className="mb-4 flex gap-2 overflow-x-auto pb-2">
                {(['all', 'mine', 'accessible', 'lent'] as const).map((filter) => (
                  <Button
                    key={filter}
                    variant={collectionFilter === filter ? 'default' : 'outline'}
                    size="sm"
                    onClick={() => setCollectionFilter(filter)}
                    className="shrink-0"
                  >
                    {filter === 'all' && 'Tutti'}
                    {filter === 'mine' && 'Miei'}
                    {filter === 'accessible' && 'Accessibili'}
                    {filter === 'lent' && 'Prestati a me'}
                  </Button>
                ))}
              </div>

              {/* Games grid/list */}
              <div
                className={cn(
                  section.viewMode === 'grid'
                    ? 'grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5'
                    : 'space-y-3'
                )}
              >
                <AnimatePresence mode="popLayout">
                  {filteredGames.map((game, index) => (
                    <motion.div
                      key={game.id}
                      layout
                      initial={{ opacity: 0, scale: 0.95 }}
                      animate={{ opacity: 1, scale: 1 }}
                      exit={{ opacity: 0, scale: 0.95 }}
                      transition={{ delay: index * 0.03 }}
                    >
                      <GameCard
                        data={game}
                        viewMode={section.viewMode}
                        onAskAI={() => void game.name /* TODO: Ask AI */}
                        onClick={() => void game.id /* TODO: View game */}
                      />
                    </motion.div>
                  ))}
                </AnimatePresence>
              </div>
            </>
          );

        case 'shared-games':
          return (
            <>
              {/* Tab navigation */}
              <div className="mb-4 flex gap-2 border-b border-border">
                {([
                  { key: 'byMe', label: 'Condivisi da me', count: MOCK_SHARED_GAMES.sharedByMe.length },
                  { key: 'withMe', label: 'Condivisi con me', count: MOCK_SHARED_GAMES.sharedWithMe.length },
                  { key: 'pending', label: 'In attesa', count: MOCK_SHARED_GAMES.pending.length },
                ] as const).map((tab) => (
                  <button
                    key={tab.key}
                    onClick={() => setSharedTab(tab.key)}
                    className={cn(
                      'relative px-4 py-2 text-sm font-medium transition-colors',
                      sharedTab === tab.key
                        ? 'text-primary'
                        : 'text-muted-foreground hover:text-foreground'
                    )}
                  >
                    {tab.label} ({tab.count})
                    {sharedTab === tab.key && (
                      <motion.div
                        layoutId="shared-tab-indicator"
                        className="absolute inset-x-0 -bottom-px h-0.5 bg-primary"
                      />
                    )}
                  </button>
                ))}
              </div>

              {/* Games grid/list */}
              <div
                className={cn(
                  section.viewMode === 'grid'
                    ? 'grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4'
                    : 'space-y-3'
                )}
              >
                <AnimatePresence mode="popLayout">
                  {sharedGames.map((game, index) => (
                    <motion.div
                      key={game.id}
                      layout
                      initial={{ opacity: 0, scale: 0.95 }}
                      animate={{ opacity: 1, scale: 1 }}
                      exit={{ opacity: 0, scale: 0.95 }}
                      transition={{ delay: index * 0.03 }}
                    >
                      <GameCard
                        data={game}
                        viewMode={section.viewMode}
                        onAskAI={() => void game.name /* TODO: Ask AI */}
                        onClick={() => void game.id /* TODO: View game */}
                      />
                    </motion.div>
                  ))}
                </AnimatePresence>
              </div>
            </>
          );

        case 'wishlist':
          return (
            <div
              className={cn(
                section.viewMode === 'grid'
                  ? 'grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3'
                  : 'space-y-3'
              )}
            >
              <AnimatePresence mode="popLayout">
                {MOCK_WISHLIST.map((item, index) => (
                  <motion.div
                    key={item.id}
                    layout
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, x: -20 }}
                    transition={{ delay: index * 0.05 }}
                  >
                    <WishlistCard
                      data={item}
                      viewMode={section.viewMode}
                      onAcquire={() => void item.game.name /* TODO: Acquire */}
                      onClick={() => void item.id /* TODO: View wishlist item */}
                    />
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          );

        case 'notifications':
          return (
            <div className="space-y-2">
              <AnimatePresence mode="popLayout">
                {MOCK_NOTIFICATIONS.map((notification, index) => (
                  <motion.div
                    key={notification.id}
                    initial={{ opacity: 0, x: -20 }}
                    animate={{ opacity: 1, x: 0 }}
                    exit={{ opacity: 0, x: 20 }}
                    transition={{ delay: index * 0.05 }}
                  >
                    <NotificationItem
                      data={notification}
                      onAction={(actionId) => void [notification.id, actionId] /* TODO: Handle action */}
                      onDismiss={() => void notification.id /* TODO: Dismiss */}
                    />
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          );

        default:
          return null;
      }
    },
    [collectionFilter, filteredGames, sharedTab, sharedGames]
  );

  return (
    <div className="min-h-screen pb-24">
      {/* Dashboard Header with Global Search */}
      <DashboardHeader />

      {/* Hero Stats Section - Issue #3308 */}
      <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        <HeroStats />
      </div>

      {/* Dashboard Widgets - Issue #3309, #3310, #3311, #3312, #3313 */}
      <div className="mx-auto max-w-7xl px-4 pb-6 sm:px-6 lg:px-8 space-y-4">
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <ActiveSessionsWidget />
          <LibrarySnapshot />
        </div>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <ActivityFeed />
          <ChatHistorySection />
        </div>
        <QuickActionsGrid />
      </div>

      {/* Reorderable Sections */}
      <Reorder.Group
        axis="y"
        values={sections}
        onReorder={handleSectionReorder}
        className="space-y-6 px-4 py-6 sm:px-6 lg:px-8"
      >
        <AnimatePresence>
          {sections.map((section) => (
            <Reorder.Item
              key={section.id}
              value={section}
              dragListener={!isMobile}
              onDragStart={() => setIsDragging(true)}
              onDragEnd={() => setIsDragging(false)}
              whileDrag={{ scale: 1.02, boxShadow: '0 10px 30px rgba(139, 90, 60, 0.15)' }}
            >
              <motion.div
                ref={(el) => {
                  if (el) sectionRefs.current.set(section.id, el);
                }}
                layout
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -20 }}
              >
                <DashboardSection
                  id={section.id}
                  title={section.title}
                  icon={section.icon}
                  viewMode={section.viewMode}
                  collapsed={section.collapsed}
                  isActive={activeSectionId === section.id}
                  isDragging={isDragging}
                  onViewModeChange={(mode) => handleViewModeChange(section.id, mode)}
                  onCollapseToggle={() => handleCollapseToggle(section.id)}
                  showViewToggle={!['quick-actions', 'recent-activity', 'notifications'].includes(section.id)}
                >
                  {renderSectionContent(section)}
                </DashboardSection>
              </motion.div>
            </Reorder.Item>
          ))}
        </AnimatePresence>
      </Reorder.Group>
    </div>
  );
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Get context-aware actions for ActionBar based on active section
 */
function getSectionActions(sectionId: string) {
  const baseActions = [
    { id: 'search', label: 'Cerca', icon: Search, priority: 1, onClick: () => void 0 /* TODO: Search */ },
    { id: 'filter', label: 'Filtra', icon: Filter, priority: 2, onClick: () => void 0 /* TODO: Filter */ },
  ];

  switch (sectionId) {
    case 'collection':
      return [
        { id: 'add', label: 'Aggiungi', icon: Plus, priority: 0, variant: 'primary' as const, onClick: () => void 0 /* TODO: Add game */ },
        ...baseActions,
      ];
    case 'shared-games':
      return [
        { id: 'share', label: 'Condividi', icon: Users, priority: 0, variant: 'primary' as const, onClick: () => void 0 /* TODO: Share */ },
        ...baseActions,
      ];
    case 'wishlist':
      return [
        { id: 'add', label: 'Aggiungi', icon: Plus, priority: 0, variant: 'primary' as const, onClick: () => void 0 /* TODO: Add to wishlist */ },
        ...baseActions,
      ];
    default:
      return baseActions;
  }
}

export default Dashboard;
