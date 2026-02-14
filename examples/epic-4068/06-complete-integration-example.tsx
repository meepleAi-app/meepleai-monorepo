/**
 * Example 6: Complete Integration - All Epic #4068 Features
 *
 * Comprehensive example showing:
 * - Permission system (tier/role checks)
 * - Tag system (vertical strips with overflow)
 * - Smart tooltips (auto-positioning, accessibility)
 * - Agent metadata (status, model, stats)
 * - Collection limits (progress indicators)
 *
 * This example demonstrates a real-world game catalog with all enhancements.
 */

import React, { useState } from 'react';
import { usePermissions } from '@/contexts/PermissionContext';
import { PermissionGate, TierGate } from '@/components/auth/PermissionGate';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { CollectionLimitIndicator } from '@/components/dashboard/CollectionLimitIndicator';
import { createTagsFromKeys, sortTagsByPriority } from '@/lib/tags/utils';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Lock,
  Sparkles,
  Users,
  Clock,
  Calendar,
  HelpCircle,
  Settings,
  BarChart3,
  Trash2,
  Edit,
  Eye
} from 'lucide-react';
import { MOCK_AGENTS } from './data/mock-data';
import type { Tag } from '@/types/tags';

// Mock game data
const MOCK_GAMES = [
  {
    id: '1',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    year: 2019,
    imageUrl: '/games/wingspan.jpg',
    rating: 8.2,
    players: '1-5',
    playTime: '40-70m',
    tagKeys: ['new', 'owned']
  },
  {
    id: '2',
    title: 'Azul',
    publisher: 'Plan B Games',
    year: 2017,
    imageUrl: '/games/azul.jpg',
    rating: 7.8,
    players: '2-4',
    playTime: '30-45m',
    tagKeys: ['sale', 'wishlisted']
  },
  {
    id: '3',
    title: 'Gloomhaven',
    publisher: 'Cephalofair Games',
    year: 2017,
    imageUrl: '/games/gloomhaven.jpg',
    rating: 8.9,
    players: '1-4',
    playTime: '60-120m',
    tagKeys: ['new', 'sale', 'exclusive', 'wishlisted', 'owned']
  },
  // ... more games
];

export function CompleteIntegrationExample() {
  const {
    tier,
    role,
    canAccess,
    hasTier,
    isAdmin,
    limits,
    loading
  } = usePermissions();

  const [selectedGames, setSelectedGames] = useState<string[]>([]);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');

  // Collection stats (mock)
  const collectionStats = {
    gameCount: tier === 'free' ? 25 : tier === 'normal' ? 75 : tier === 'pro' ? 420 : 1200,
    storageMB: tier === 'free' ? 45 : tier === 'normal' ? 325 : tier === 'pro' ? 3500 : 12000
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background">
      {/* Header with Tier/Role Display */}
      <header className="border-b bg-card sticky top-0 z-50">
        <div className="container mx-auto px-4 py-4">
          <div className="flex justify-between items-center">
            <div>
              <h1 className="text-2xl font-bold">MeepleAI Game Catalog</h1>
              <p className="text-sm text-muted-foreground">
                Epic #4068: Complete Integration Example
              </p>
            </div>

            <div className="flex items-center gap-4">
              <div className="flex gap-2">
                <Badge variant="outline" className="capitalize">
                  {tier} Tier
                </Badge>
                <Badge variant="secondary" className="capitalize">
                  {role}
                </Badge>
              </div>

              {isAdmin() && (
                <Button variant="destructive" size="sm" asChild>
                  <a href="/admin">
                    <Settings className="mr-2 h-4 w-4" />
                    Admin Panel
                  </a>
                </Button>
              )}
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="container mx-auto px-4 py-8">
        {/* Collection Limits Section */}
        <section className="mb-8">
          <div className="flex justify-between items-start mb-4">
            <div>
              <h2 className="text-xl font-semibold">Your Collection</h2>
              <p className="text-sm text-muted-foreground">
                {collectionStats.gameCount} games • {collectionStats.storageMB}MB storage used
              </p>
            </div>

            <TierGate
              minimum="normal"
              fallback={
                <Alert>
                  <Lock className="h-4 w-4" />
                  <AlertDescription>
                    Upgrade to Normal for collection management
                  </AlertDescription>
                </Alert>
              }
            >
              <Button>
                <Settings className="mr-2 h-4 w-4" />
                Manage Collection
              </Button>
            </TierGate>
          </div>

          <CollectionLimitIndicator
            gameCount={collectionStats.gameCount}
            storageMB={collectionStats.storageMB}
          />
        </section>

        {/* Bulk Selection Toolbar */}
        <PermissionGate
          feature="bulk-select"
          fallback={
            <Alert className="mb-6">
              <Lock className="h-4 w-4" />
              <AlertTitle>Bulk Selection Locked</AlertTitle>
              <AlertDescription>
                Upgrade to Pro or become an Editor to unlock bulk operations
                <Button className="mt-2" size="sm" onClick={() => alert('Navigate to /upgrade')}>
                  Upgrade to Pro
                </Button>
              </AlertDescription>
            </Alert>
          }
        >
          <div className="mb-6 p-4 bg-card border rounded-lg">
            <div className="flex justify-between items-center">
              <p className="text-sm font-medium">
                {selectedGames.length} game{selectedGames.length !== 1 && 's'} selected
              </p>

              {selectedGames.length > 0 && (
                <div className="flex gap-2">
                  <Button size="sm" variant="outline">
                    Add to Collection
                  </Button>
                  <Button size="sm" variant="outline">
                    Export
                  </Button>
                  {isAdmin() && (
                    <Button size="sm" variant="destructive">
                      <Trash2 className="mr-2 h-4 w-4" />
                      Delete
                    </Button>
                  )}
                </div>
              )}
            </div>
          </div>
        </PermissionGate>

        {/* View Mode Toggle */}
        <div className="flex justify-between items-center mb-6">
          <Tabs value={viewMode} onValueChange={(v) => setViewMode(v as 'grid' | 'list')}>
            <TabsList>
              <TabsTrigger value="grid">Grid</TabsTrigger>
              <TabsTrigger value="list">List</TabsTrigger>
            </TabsList>
          </Tabs>

          {canAccess('filters.advanced') && (
            <Button variant="outline" size="sm">
              <BarChart3 className="mr-2 h-4 w-4" />
              Advanced Filters
            </Button>
          )}
        </div>

        {/* Game Grid/List */}
        <div className={
          viewMode === 'grid'
            ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6'
            : 'space-y-4'
        }>
          {MOCK_GAMES.map(game => {
            const tags = createTagsFromKeys('game', game.tagKeys);
            const sortedTags = sortTagsByPriority(tags, 'game');
            const isSelected = selectedGames.includes(game.id);

            return (
              <div key={game.id} className="relative">
                {/* Bulk select checkbox (Pro tier or Editor role) */}
                {canAccess('bulk-select') && (
                  <div className="absolute top-2 right-2 z-20">
                    <Checkbox
                      checked={isSelected}
                      onCheckedChange={(checked) => {
                        setSelectedGames(prev =>
                          checked
                            ? [...prev, game.id]
                            : prev.filter(id => id !== game.id)
                        );
                      }}
                      className="bg-white/90 backdrop-blur-sm"
                    />
                  </div>
                )}

                <MeepleCard
                  entity="game"
                  variant={viewMode}
                  title={game.title}
                  subtitle={`${game.publisher} · ${game.year}`}
                  imageUrl={game.imageUrl}
                  rating={game.rating}
                  ratingMax={10}
                  metadata={[
                    { icon: Users, value: game.players },
                    { icon: Clock, value: game.playTime },
                    { icon: Calendar, value: String(game.year) }
                  ]}
                  tags={sortedTags}
                  maxVisibleTags={3}
                  showWishlist={canAccess('wishlist')}
                  draggable={canAccess('drag-drop')}
                  quickActions={[
                    { icon: Eye, label: 'View', onClick: () => alert(`View ${game.title}`) },
                    canAccess('quick-action.edit') && {
                      icon: Edit,
                      label: 'Edit',
                      onClick: () => alert(`Edit ${game.title}`)
                    },
                    isAdmin() && {
                      icon: Trash2,
                      label: 'Delete',
                      onClick: () => alert(`Delete ${game.title}`),
                      destructive: true,
                      adminOnly: true
                    }
                  ].filter(Boolean)}
                />
              </div>
            );
          })}
        </div>

        {/* Agent Dashboard Section (Pro tier) */}
        <TierGate
          minimum="pro"
          fallback={
            <Alert className="mt-12">
              <Lock className="h-4 w-4" />
              <AlertTitle>AI Agents (Pro Feature)</AlertTitle>
              <AlertDescription>
                Create and manage AI agents for game rules, strategy, and more.
                <Button className="mt-2" size="sm">Upgrade to Pro</Button>
              </AlertDescription>
            </Alert>
          }
        >
          <section className="mt-12">
            <h2 className="text-xl font-semibold mb-6">AI Agents</h2>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {MOCK_AGENTS.map(agent => (
                <MeepleCard
                  key={agent.id}
                  entity="agent"
                  variant="grid"
                  title={agent.name}
                  subtitle={agent.strategy}
                  agentMetadata={agent.metadata}
                  tags={createTagsFromKeys('agent', agent.metadata.capabilities.map(c => c.toLowerCase()))}
                  maxVisibleTags={3}
                  quickActions={[
                    { icon: MessageSquare, label: 'Chat', onClick: () => alert('Open chat') },
                    { icon: BarChart3, label: 'Stats', onClick: () => alert('View stats') },
                    isAdmin() && {
                      icon: Settings,
                      label: 'Configure',
                      onClick: () => alert('Configure agent'),
                      adminOnly: true
                    }
                  ].filter(Boolean)}
                />
              ))}
            </div>
          </section>
        </TierGate>

        {/* Analytics Section (Pro tier or Admin role) */}
        <PermissionGate
          feature="analytics.view"
          fallback={
            <Alert className="mt-12">
              <Lock className="h-4 w-4" />
              <AlertTitle>Analytics Dashboard (Pro Feature)</AlertTitle>
              <AlertDescription>
                View detailed analytics about your collection and gameplay.
                {!hasTier('pro') && <Button className="mt-2" size="sm">Upgrade to Pro</Button>}
                {hasTier('pro') && <p className="text-xs mt-2">Contact admin for Analytics role</p>}
              </AlertDescription>
            </Alert>
          }
        >
          <section className="mt-12 p-6 bg-card border rounded-lg">
            <h2 className="text-xl font-semibold mb-4">
              <BarChart3 className="inline mr-2" />
              Analytics Dashboard
            </h2>

            <div className="grid grid-cols-3 gap-4">
              <div className="p-4 bg-muted rounded">
                <p className="text-sm text-muted-foreground">Total Games</p>
                <p className="text-3xl font-bold">{collectionStats.gameCount}</p>
              </div>

              <div className="p-4 bg-muted rounded">
                <p className="text-sm text-muted-foreground">Avg Rating</p>
                <p className="text-3xl font-bold">8.3</p>
              </div>

              <div className="p-4 bg-muted rounded">
                <p className="text-sm text-muted-foreground">Play Sessions</p>
                <p className="text-3xl font-bold">142</p>
              </div>
            </div>
          </section>
        </PermissionGate>

        {/* Admin Panel (Admin role only) */}
        {isAdmin() && (
          <section className="mt-12 p-6 bg-destructive/10 border-destructive border rounded-lg">
            <h2 className="text-xl font-semibold mb-4 text-destructive">
              <Settings className="inline mr-2" />
              Admin Panel
            </h2>

            <div className="space-y-4">
              <div className="flex justify-between items-center p-3 bg-background rounded">
                <span className="font-medium">User Management</span>
                <Button size="sm" variant="outline">Manage Users</Button>
              </div>

              <div className="flex justify-between items-center p-3 bg-background rounded">
                <span className="font-medium">Game Catalog Administration</span>
                <Button size="sm" variant="outline">Manage Catalog</Button>
              </div>

              <div className="flex justify-between items-center p-3 bg-background rounded">
                <span className="font-medium">Permission Configuration</span>
                <Button size="sm" variant="outline">Edit Permissions</Button>
              </div>
            </div>
          </section>
        )}

        {/* Feature Showcase */}
        <section className="mt-12 p-6 bg-muted/50 rounded-lg">
          <h2 className="text-xl font-semibold mb-4">Epic #4068 Feature Showcase</h2>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <h3 className="font-medium">Permission System</h3>
              <ul className="text-sm space-y-1 text-muted-foreground">
                <li>✓ Tier: {tier.toUpperCase()}</li>
                <li>✓ Role: {role.toUpperCase()}</li>
                <li>✓ Conditional feature rendering</li>
                <li>✓ Hierarchical tier/role checks</li>
                <li>✓ Permission-aware quick actions</li>
              </ul>
            </div>

            <div className="space-y-2">
              <h3 className="font-medium">Tag System</h3>
              <ul className="text-sm space-y-1 text-muted-foreground">
                <li>✓ Vertical left-edge tag strips</li>
                <li>✓ Max 3 visible + overflow counter</li>
                <li>✓ Entity-specific presets (game/agent)</li>
                <li>✓ Responsive (32px → 24px mobile)</li>
                <li>✓ Priority-based sorting</li>
              </ul>
            </div>

            <div className="space-y-2">
              <h3 className="font-medium">Smart Tooltips</h3>
              <ul className="text-sm space-y-1 text-muted-foreground">
                <li>✓ Auto-flip at viewport edges</li>
                <li>✓ Keyboard navigation (Tab/Enter/Esc)</li>
                <li>✓ Mobile touch support</li>
                <li>✓ WCAG 2.1 AA compliant</li>
                <li>✓ Performance: &lt;16ms positioning</li>
              </ul>
            </div>

            <div className="space-y-2">
              <h3 className="font-medium">Agent Metadata</h3>
              <ul className="text-sm space-y-1 text-muted-foreground">
                <li>✓ Status badges (Active/Idle/Training/Error)</li>
                <li>✓ Model info with parameter tooltips</li>
                <li>✓ Invocation stats (formatted counts)</li>
                <li>✓ Capability tags (RAG, Vision, Code)</li>
                <li>✓ Last executed (relative time)</li>
              </ul>
            </div>
          </div>
        </section>

        {/* User Journey by Tier */}
        <section className="mt-12">
          <h2 className="text-xl font-semibold mb-6">User Journey by Tier</h2>

          <Tabs defaultValue={tier} className="w-full">
            <TabsList className="grid w-full grid-cols-4">
              <TabsTrigger value="free">Free</TabsTrigger>
              <TabsTrigger value="normal">Normal</TabsTrigger>
              <TabsTrigger value="pro">Pro</TabsTrigger>
              <TabsTrigger value="enterprise">Enterprise</TabsTrigger>
            </TabsList>

            <TabsContent value="free" className="space-y-4">
              <Alert>
                <AlertTitle>Free Tier Experience</AlertTitle>
                <AlertDescription>
                  <ul className="list-disc list-inside space-y-1 mt-2">
                    <li>✅ Wishlist: Add games to wishlist</li>
                    <li>❌ Bulk Select: Upgrade to Pro</li>
                    <li>❌ Drag & Drop: Upgrade to Normal</li>
                    <li>❌ Agent Creation: Upgrade to Pro</li>
                    <li>❌ Analytics: Upgrade to Pro</li>
                  </ul>
                  <p className="mt-4 font-semibold">Limits: 50 games, 100MB storage</p>
                </AlertDescription>
              </Alert>
            </TabsContent>

            <TabsContent value="normal" className="space-y-4">
              <Alert>
                <AlertTitle>Normal Tier Experience</AlertTitle>
                <AlertDescription>
                  <ul className="list-disc list-inside space-y-1 mt-2">
                    <li>✅ Wishlist</li>
                    <li>✅ Drag & Drop: Reorder games</li>
                    <li>✅ Collection Management</li>
                    <li>✅ Advanced Filters</li>
                    <li>❌ Bulk Select: Upgrade to Pro</li>
                    <li>❌ Agent Creation: Upgrade to Pro</li>
                  </ul>
                  <p className="mt-4 font-semibold">Limits: 100 games, 500MB storage</p>
                </AlertDescription>
              </Alert>
            </TabsContent>

            <TabsContent value="pro" className="space-y-4">
              <Alert>
                <AlertTitle>Pro Tier Experience</AlertTitle>
                <AlertDescription>
                  <ul className="list-disc list-inside space-y-1 mt-2">
                    <li>✅ All Free & Normal features</li>
                    <li>✅ Bulk Selection: Multi-select operations</li>
                    <li>✅ Agent Creation: Create custom AI agents</li>
                    <li>✅ Analytics Dashboard</li>
                    <li>✅ Advanced features unlocked</li>
                  </ul>
                  <p className="mt-4 font-semibold">Limits: 500 games, 5GB storage</p>
                </AlertDescription>
              </Alert>
            </TabsContent>

            <TabsContent value="enterprise" className="space-y-4">
              <Alert>
                <AlertTitle>Enterprise Tier Experience</AlertTitle>
                <AlertDescription>
                  <ul className="list-disc list-inside space-y-1 mt-2">
                    <li>✅ All Pro features</li>
                    <li>✅ Unlimited games</li>
                    <li>✅ Unlimited storage</li>
                    <li>✅ Priority support</li>
                    <li>✅ Custom integrations</li>
                  </ul>
                  <p className="mt-4 font-semibold">Limits: ∞ Unlimited</p>
                </AlertDescription>
              </Alert>
            </TabsContent>
          </Tabs>
        </section>

        {/* Implementation Notes */}
        <section className="mt-12 p-6 bg-card border rounded-lg">
          <h2 className="text-xl font-semibold mb-4">Implementation Notes</h2>

          <div className="space-y-4 text-sm">
            <div>
              <h3 className="font-medium mb-2">Key Components Used</h3>
              <ul className="list-disc list-inside space-y-1 text-muted-foreground">
                <li><code>usePermissions()</code>: Hook for tier/role/feature checks</li>
                <li><code>&lt;PermissionGate&gt;</code>: Conditional rendering wrapper</li>
                <li><code>&lt;TierGate&gt;</code>: Tier-based rendering</li>
                <li><code>&lt;MeepleCard&gt;</code>: Universal card with all features</li>
                <li><code>&lt;CollectionLimitIndicator&gt;</code>: Progress bars</li>
              </ul>
            </div>

            <div>
              <h3 className="font-medium mb-2">Permission Logic</h3>
              <pre className="bg-muted p-3 rounded text-xs overflow-auto">
{`// OR Logic (default): Tier OR Role
bulk-select: Pro tier OR Editor role
agent.create: Pro tier OR Creator role

// AND Logic: Both required
quick-action.delete: Admin role (tier irrelevant)

// State-based: Varies by resource state
view-game: Published (all) OR Draft (creator only)`}
              </pre>
            </div>

            <div>
              <h3 className="font-medium mb-2">Tag System</h3>
              <pre className="bg-muted p-3 rounded text-xs overflow-auto">
{`// Create tags from keys
const tags = createTagsFromKeys('game', ['new', 'sale', 'owned']);

// Sort by priority
const sorted = sortTagsByPriority(tags, 'game');

// Render in card
<MeepleCard tags={sorted} maxVisibleTags={3} />`}
              </pre>
            </div>
          </div>
        </section>
      </main>

      {/* Footer */}
      <footer className="mt-12 border-t py-8 bg-card">
        <div className="container mx-auto px-4 text-center text-sm text-muted-foreground">
          <p>Epic #4068: MeepleCard Enhancements - Complete Integration Example</p>
          <p className="mt-2">
            Your tier: <strong className="capitalize">{tier}</strong> •
            Your role: <strong className="capitalize">{role}</strong> •
            Collection: <strong>{collectionStats.gameCount} / {limits.maxGames} games</strong>
          </p>

          <div className="mt-4 flex justify-center gap-4">
            <Button variant="link" size="sm" onClick={() => alert('View all features')}>
              Feature Documentation
            </Button>
            <Button variant="link" size="sm" onClick={() => alert('Navigate to /upgrade')}>
              Upgrade Tier
            </Button>
            {!isAdmin() && (
              <Button variant="link" size="sm" onClick={() => alert('Contact admin')}>
                Request Role Change
              </Button>
            )}
          </div>
        </div>
      </footer>
    </div>
  );
}

export default CompleteIntegrationExample;
