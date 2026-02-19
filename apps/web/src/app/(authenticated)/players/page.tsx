/**
 * Players List Page - /players
 *
 * Displays players the user has played with, aggregated from play records.
 * Uses EntityListView with MeepleCard entity=player and navigation footer.
 *
 * @see Issue #4692
 */

'use client';

import { useMemo } from 'react';

import { Gamepad2, Trophy, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { EntityListView } from '@/components/ui/data-display/entity-list-view';
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { getNavigationLinks } from '@/config/entity-navigation';
import { usePlayerStatistics } from '@/hooks/queries/usePlayersFromRecords';

interface PlayerListItem {
  id: string;
  displayName: string;
  gameName: string;
  playCount: number;
}

function renderPlayerCard(
  player: PlayerListItem,
): Omit<MeepleCardProps, 'entity' | 'variant'> {
  const navLinks = getNavigationLinks('player', { id: player.id });

  return {
    title: player.displayName,
    subtitle: player.gameName,
    metadata: [
      { icon: Gamepad2, value: `${player.playCount} partite` },
    ],
    navigateTo: navLinks,
  };
}

export default function PlayersListPage() {
  const router = useRouter();
  const { data: stats, isLoading, error } = usePlayerStatistics();

  // Transform game play counts into list items for display
  const players = useMemo(() => {
    if (!stats) return [];
    const items: PlayerListItem[] = [];

    // gamePlayCounts is Record<gameName, count>
    for (const [gameName, count] of Object.entries(stats.gamePlayCounts)) {
      items.push({
        id: gameName.toLowerCase().replace(/\s+/g, '-'),
        displayName: gameName,
        gameName: `${count} sessioni`,
        playCount: count,
      });
    }

    return items.sort((a, b) => b.playCount - a.playCount);
  }, [stats]);

  if (error) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <div className="container mx-auto max-w-7xl">
          <Alert variant="destructive">
            <AlertDescription>
              Errore nel caricamento dei giocatori.
            </AlertDescription>
          </Alert>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-7xl">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-quicksand font-bold text-foreground">
            Giocatori
          </h1>
          <p className="text-muted-foreground font-nunito mt-1">
            I giocatori con cui hai condiviso partite
          </p>
          {stats && (
            <div className="flex gap-6 mt-4">
              <div className="flex items-center gap-2 text-sm font-nunito text-muted-foreground">
                <Gamepad2 className="h-4 w-4" />
                <span>{stats.totalSessions} sessioni totali</span>
              </div>
              <div className="flex items-center gap-2 text-sm font-nunito text-muted-foreground">
                <Trophy className="h-4 w-4" />
                <span>{stats.totalWins} vittorie</span>
              </div>
            </div>
          )}
        </div>

        {/* Player List */}
        <EntityListView<PlayerListItem>
          items={players}
          entity="player"
          persistenceKey="players-list"
          loading={isLoading}
          renderItem={renderPlayerCard}
          onItemClick={(player) => router.push(`/players/${player.id}`)}
          searchable
          searchPlaceholder="Cerca giocatore..."
          searchFields={['displayName', 'gameName']}
          emptyMessage="Nessun giocatore trovato. Registra la tua prima partita!"
          title="I tuoi Giocatori"
          showViewSwitcher
          gridColumns={{ default: 1, sm: 2, lg: 3, xl: 4 }}
        />
      </div>
    </div>
  );
}
