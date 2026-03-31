/**
 * Profile Landing Page - /profile
 *
 * Overview landing page with three tabs:
 *   - Overview: library stats + quick action links
 *   - Achievements: embedded achievements content (mirrors /profile/achievements)
 *   - Activity: coming soon placeholder
 *
 * @see Issue #4893
 */

'use client';

import { useEffect, useState } from 'react';

import {
  Activity,
  BookOpen,
  ChevronRight,
  FileText,
  Gamepad2,
  Heart,
  LayoutDashboard,
  Package,
  Trophy,
  User,
} from 'lucide-react';
import Link from 'next/link';

import { ActivityFeed } from '@/components/profile/ActivityFeed';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useAuth } from '@/hooks/useAuth';
import { useRecentSessions } from '@/hooks/useRecentSessions';
import { api } from '@/lib/api';
import type { UserLibraryStats } from '@/lib/api/schemas/library.schemas';
import { useCardHand } from '@/stores/use-card-hand';

// ─── Types ────────────────────────────────────────────────────────────────────

type Tab = 'overview' | 'achievements' | 'activity';

// ─── Sub-components ───────────────────────────────────────────────────────────

function TabBar({ active, onChange }: { active: Tab; onChange: (t: Tab) => void }) {
  const tabs: { id: Tab; label: string; icon: React.ElementType }[] = [
    { id: 'overview', label: 'Overview', icon: LayoutDashboard },
    { id: 'achievements', label: 'Achievements', icon: Trophy },
    { id: 'activity', label: 'Activity', icon: Activity },
  ];

  return (
    <div className="flex border-b border-border mb-6" role="tablist">
      {tabs.map(tab => {
        const Icon = tab.icon;
        const isActive = active === tab.id;
        return (
          <button
            key={tab.id}
            role="tab"
            aria-selected={isActive}
            onClick={() => onChange(tab.id)}
            className={`flex items-center gap-2 px-4 py-3 text-sm font-nunito font-medium border-b-2 transition-colors ${
              isActive
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            }`}
          >
            <Icon className="h-4 w-4" />
            {tab.label}
          </button>
        );
      })}
    </div>
  );
}

function QuickActionLink({
  href,
  icon: Icon,
  label,
  description,
}: {
  href: string;
  icon: React.ElementType;
  label: string;
  description: string;
}) {
  return (
    <Link
      href={href}
      className="flex items-center gap-3 p-3 rounded-lg border border-border/50 hover:bg-muted/40 transition-colors group"
    >
      <div className="rounded-md bg-primary/10 p-2 shrink-0">
        <Icon className="h-4 w-4 text-primary" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="font-medium text-sm font-nunito">{label}</p>
        <p className="text-xs text-muted-foreground font-nunito">{description}</p>
      </div>
      <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0 group-hover:translate-x-0.5 transition-transform" />
    </Link>
  );
}

// ─── StatTile ─────────────────────────────────────────────────────────────────

function StatTile({
  icon: Icon,
  label,
  value,
  color,
}: {
  icon: React.ElementType;
  label: string;
  value: number;
  color: string;
}): React.ReactElement {
  return (
    <div className="flex items-center gap-2 p-3 rounded-lg bg-muted/30">
      <Icon className={`h-4 w-4 shrink-0 ${color}`} />
      <div>
        <p className="text-xs text-muted-foreground font-nunito">{label}</p>
        <p className="text-lg font-bold font-quicksand">{value}</p>
      </div>
    </div>
  );
}

// ─── Overview Tab ─────────────────────────────────────────────────────────────

function OverviewTab() {
  const [stats, setStats] = useState<UserLibraryStats | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { sessions, isLoading: sessionsLoading } = useRecentSessions(3);

  useEffect(() => {
    api.library
      .getStats()
      .then(data => setStats(data))
      .catch(err => setError(err instanceof Error ? err.message : 'Failed to load stats'))
      .finally(() => setIsLoading(false));
  }, []);

  return (
    <div className="space-y-6">
      {/* Library Stats */}
      <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-sm">
        <CardHeader className="pb-2">
          <CardTitle className="font-quicksand text-lg">Library Stats</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading && (
            <div className="grid grid-cols-3 gap-3">
              {[...Array(6)].map((_, i) => (
                <Skeleton key={i} className="h-14 w-full rounded-lg" />
              ))}
            </div>
          )}
          {error && (
            <Alert>
              <AlertDescription className="font-nunito text-sm">{error}</AlertDescription>
            </Alert>
          )}
          {!isLoading && !error && stats && (
            <div className="grid grid-cols-3 gap-3">
              <StatTile
                icon={Gamepad2}
                label="Giochi"
                value={stats.totalGames}
                color="text-primary"
              />
              <StatTile
                icon={Heart}
                label="Preferiti"
                value={stats.favoriteGames}
                color="text-red-400"
              />
              <StatTile
                icon={BookOpen}
                label="Posseduti"
                value={stats.ownedCount ?? 0}
                color="text-green-500"
              />
              <StatTile
                icon={Trophy}
                label="Wishlist"
                value={stats.wishlistCount ?? 0}
                color="text-amber-500"
              />
              <StatTile
                icon={FileText}
                label="PDF caricati"
                value={stats.privatePdfs ?? 0}
                color="text-blue-500"
              />
              <StatTile
                icon={Package}
                label="In prestito"
                value={stats.inPrestitoCount ?? 0}
                color="text-orange-400"
              />
            </div>
          )}
        </CardContent>
      </Card>

      {/* Ultime partite */}
      <Card className="border-l-4 border-l-green-400 shadow-sm">
        <CardHeader className="pb-2 flex flex-row items-center justify-between">
          <CardTitle className="font-quicksand text-lg">Ultime partite</CardTitle>
          <Button asChild variant="ghost" size="sm" className="font-nunito gap-1">
            <Link href="/play-records">
              Tutte <ChevronRight className="h-3 w-3" />
            </Link>
          </Button>
        </CardHeader>
        <CardContent>
          {sessionsLoading && (
            <div className="space-y-2">
              {[...Array(3)].map((_, i) => (
                <Skeleton key={i} className="h-10 w-full rounded-lg" />
              ))}
            </div>
          )}
          {!sessionsLoading && sessions.length === 0 && (
            <p className="text-sm text-muted-foreground font-nunito">
              Nessuna partita ancora.{' '}
              <Link href="/sessions" className="underline">
                Inizia una sessione
              </Link>
            </p>
          )}
          {!sessionsLoading &&
            sessions.map(s => (
              <div
                key={s.id}
                className="flex items-center justify-between py-2 border-b border-border/30 last:border-0"
              >
                <span className="font-nunito text-sm font-medium">{s.gameName}</span>
                <span className="text-xs text-muted-foreground font-nunito">
                  {new Date(s.sessionDate).toLocaleDateString('it-IT', {
                    day: '2-digit',
                    month: 'short',
                  })}
                </span>
              </div>
            ))}
        </CardContent>
      </Card>

      {/* Quick Actions */}
      <Card className="border-l-4 border-l-amber-400 shadow-sm">
        <CardHeader className="pb-2">
          <CardTitle className="font-quicksand text-lg">Quick Actions</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-2">
            <QuickActionLink
              href="/profile/achievements"
              icon={Trophy}
              label="Achievements"
              description="View your earned badges and milestones"
            />
            <QuickActionLink
              href="/library"
              icon={BookOpen}
              label="My Library"
              description="Browse and manage your game collection"
            />
            <QuickActionLink
              href="/play-records"
              icon={Gamepad2}
              label="Storia di gioco"
              description="Tutte le partite giocate"
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

// ─── Achievements Tab ──────────────────────────────────────────────────────────

function AchievementsTab() {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground font-nunito">
          Your earned badges and progress milestones
        </p>
        <Button asChild variant="ghost" size="sm" className="font-nunito gap-1">
          <Link href="/profile/achievements">
            View All <ChevronRight className="h-3 w-3" />
          </Link>
        </Button>
      </div>
      {/* Iframe-like embed via redirect to achievements page content */}
      <Card className="border-l-4 border-l-amber-400">
        <CardContent className="py-8 text-center">
          <Trophy className="h-10 w-10 text-amber-500 mx-auto mb-3" />
          <p className="font-medium font-quicksand mb-1">Your Achievements</p>
          <p className="text-sm text-muted-foreground font-nunito mb-4">
            View your full achievements gallery
          </p>
          <Button asChild variant="outline" className="font-nunito">
            <Link href="/profile/achievements">Open Achievements</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

// ─── Activity Tab ─────────────────────────────────────────────────────────────

function ActivityTab(): React.ReactElement {
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground font-nunito">
        Le tue ultime partite, achievement e aggiornamenti
      </p>
      <ActivityFeed />
    </div>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export default function ProfilePage() {
  const { user } = useAuth();
  const { drawCard } = useCardHand();
  const [activeTab, setActiveTab] = useState<Tab>('overview');

  useEffect(() => {
    drawCard({
      id: 'section-profile',
      entity: 'custom',
      title: 'Profile',
      href: '/profile',
    });
  }, [drawCard]);

  const displayName = user?.displayName ?? user?.email ?? 'Player';
  const initials = displayName
    .split(' ')
    .map(w => w[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-3xl">
        {/* Profile Header */}
        <div className="flex items-center gap-4 mb-8 p-6 rounded-2xl bg-white/70 backdrop-blur-md border border-border/50 shadow-sm">
          {/* Avatar */}
          <div className="h-16 w-16 rounded-full bg-primary/10 border-2 border-primary/20 flex items-center justify-center shrink-0">
            {user ? (
              <span className="text-xl font-bold font-quicksand text-primary">{initials}</span>
            ) : (
              <User className="h-7 w-7 text-muted-foreground" />
            )}
          </div>
          {/* Info */}
          <div className="flex-1 min-w-0">
            <h1 className="text-2xl font-bold font-quicksand truncate">{displayName}</h1>
            {user?.email && (
              <p className="text-sm text-muted-foreground font-nunito truncate">{user.email}</p>
            )}
            {user?.role && (
              <span className="inline-block mt-1 text-xs px-2 py-0.5 rounded bg-amber-100 text-amber-900 font-nunito font-medium">
                {user.role}
              </span>
            )}
          </div>
        </div>

        {/* Tabs */}
        <TabBar active={activeTab} onChange={setActiveTab} />

        {/* Tab Content */}
        {activeTab === 'overview' && <OverviewTab />}
        {activeTab === 'achievements' && <AchievementsTab />}
        {activeTab === 'activity' && <ActivityTab />}
      </div>
    </div>
  );
}
