/**
 * Profile Landing Page - /profile
 *
 * Overview landing page con tre tab:
 *   - Overview: library stats + ultime partite + quick action links
 *   - Achievements: achievements inline
 *   - Activity: activity feed
 */

'use client';

import { useEffect, useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';
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
} from 'lucide-react';
import Link from 'next/link';

import { AchievementsGrid } from '@/components/profile/AchievementsGrid';
import { ActivityFeed } from '@/components/profile/ActivityFeed';
import { AvatarUpload } from '@/components/profile/AvatarUpload';
import { EditProfileSheet } from '@/components/profile/EditProfileSheet';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { userKeys } from '@/hooks/queries/useCurrentUser';
import { libraryKeys } from '@/hooks/queries/useLibrary';
import { useAuth } from '@/hooks/useAuth';
import { useRecentSessions } from '@/hooks/useRecentSessions';
import { api } from '@/lib/api';
import type { UserLibraryStats } from '@/lib/api/schemas/library.schemas';
import { useCardHand } from '@/stores/use-card-hand';

// ─── Types ────────────────────────────────────────────────────────────────────

type Tab = 'overview' | 'achievements' | 'activity';

// ─── TabBar ───────────────────────────────────────────────────────────────────

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

// ─── QuickActionLink ──────────────────────────────────────────────────────────

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

// ─── OverviewTab ──────────────────────────────────────────────────────────────

function OverviewTab() {
  const { sessions, isLoading: sessionsLoading } = useRecentSessions(3);

  const {
    data: stats,
    isLoading,
    error,
  } = useQuery<UserLibraryStats>({
    queryKey: libraryKeys.stats(),
    queryFn: () => api.library.getStats(),
    staleTime: 2 * 60 * 1000,
  });

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
              <AlertDescription className="font-nunito text-sm">
                {error instanceof Error ? error.message : 'Errore nel caricamento stats.'}
              </AlertDescription>
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
              description="Vedi badge e traguardi"
            />
            <QuickActionLink
              href="/library"
              icon={BookOpen}
              label="My Library"
              description="Sfoglia la tua collezione"
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

// ─── AchievementsTab ──────────────────────────────────────────────────────────

function AchievementsTab() {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground font-nunito">Badge guadagnati e progressi</p>
        <Button asChild variant="ghost" size="sm" className="font-nunito gap-1">
          <Link href="/profile/achievements">
            Tutti <ChevronRight className="h-3 w-3" />
          </Link>
        </Button>
      </div>
      <AchievementsGrid />
    </div>
  );
}

// ─── ActivityTab ──────────────────────────────────────────────────────────────

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
  const queryClient = useQueryClient();
  const { drawCard } = useCardHand();
  const [activeTab, setActiveTab] = useState<Tab>('overview');

  const { data: profile } = useQuery({
    queryKey: [...userKeys.current(), 'profile'],
    queryFn: () => api.auth.getProfile(),
    staleTime: 5 * 60 * 1000,
  });

  useEffect(() => {
    drawCard({
      id: 'section-profile',
      entity: 'player',
      title: 'Profile',
      href: '/profile',
    });
  }, [drawCard]);

  const displayName = profile?.displayName ?? user?.displayName ?? user?.email ?? 'Player';
  const avatarUrl = profile?.avatarUrl ?? null;

  async function handleAvatarUpload(file: File, previewUrl: string): Promise<void> {
    // Optimistic update immediato con blob URL
    queryClient.setQueryData([...userKeys.current(), 'profile'], (prev: typeof profile) =>
      prev ? { ...prev, avatarUrl: previewUrl } : prev
    );
    const result = await api.auth.uploadAvatar(file);
    if (result?.avatarUrl) {
      queryClient.setQueryData([...userKeys.current(), 'profile'], (prev: typeof profile) =>
        prev ? { ...prev, avatarUrl: result.avatarUrl } : prev
      );
    }
    await queryClient.invalidateQueries({ queryKey: userKeys.all });
  }

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-3xl">
        {/* Profile Header */}
        <div className="flex items-center gap-4 mb-8 p-6 rounded-2xl bg-white/70 backdrop-blur-md border border-border/50 shadow-sm">
          <AvatarUpload
            currentAvatarUrl={avatarUrl}
            displayName={displayName}
            onUpload={handleAvatarUpload}
            size={64}
          />
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
          <EditProfileSheet currentDisplayName={displayName} />
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
