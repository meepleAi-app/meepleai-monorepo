/**
 * Game Nights Content (client component)
 * Issue #33 — P3 Game Night Frontend
 *
 * Renders upcoming / my game nights based on ?tab query param.
 */

'use client';

import { useEffect } from 'react';

import { Calendar, MapPin, Users } from 'lucide-react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useMyGameNights, useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import type { GameNightDto } from '@/lib/api/schemas/game-nights.schemas';
import { useCardHand } from '@/stores/use-card-hand';

function GameNightCard({ event }: { event: GameNightDto }) {
  const scheduledDate = new Date(event.scheduledAt);
  const isPast = scheduledDate < new Date();

  return (
    <Link href={`/game-nights/${event.id}`}>
      <Card className="hover:border-amber-300 transition-colors cursor-pointer">
        <CardHeader className="pb-2">
          <div className="flex items-start justify-between gap-2">
            <CardTitle className="text-lg font-quicksand">{event.title}</CardTitle>
            <StatusBadge status={event.status} isPast={isPast} />
          </div>
          <CardDescription className="font-nunito">
            Organizzata da {event.organizerName}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-2 text-sm text-muted-foreground font-nunito">
          <div className="flex items-center gap-2">
            <Calendar className="h-4 w-4" />
            <span>
              {scheduledDate.toLocaleDateString('it-IT', {
                weekday: 'long',
                day: 'numeric',
                month: 'long',
                hour: '2-digit',
                minute: '2-digit',
              })}
            </span>
          </div>
          {event.location && (
            <div className="flex items-center gap-2">
              <MapPin className="h-4 w-4" />
              <span>{event.location}</span>
            </div>
          )}
          <div className="flex items-center gap-2">
            <Users className="h-4 w-4" />
            <span>
              {event.acceptedCount} confermati
              {event.pendingCount > 0 && ` · ${event.pendingCount} in attesa`}
              {event.maxPlayers && ` / ${event.maxPlayers} max`}
            </span>
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}

function StatusBadge({ status, isPast }: { status: string; isPast: boolean }) {
  if (status === 'Cancelled') return <Badge variant="destructive">Annullata</Badge>;
  if (status === 'Draft') return <Badge variant="secondary">Bozza</Badge>;
  if (isPast) return <Badge variant="outline">Conclusa</Badge>;
  return <Badge className="bg-amber-100 text-amber-900 border-amber-200">Pubblicata</Badge>;
}

function EmptyState({ tab }: { tab: string }) {
  return (
    <div className="text-center py-12 text-muted-foreground font-nunito">
      <Calendar className="h-12 w-12 mx-auto mb-4 opacity-40" />
      <p className="text-lg font-medium">
        {tab === 'mine' ? 'Non hai ancora organizzato serate' : 'Nessuna serata in programma'}
      </p>
      <p className="text-sm mt-1">
        {tab === 'mine'
          ? 'Crea la tua prima serata giochi!'
          : 'Le prossime serate appariranno qui.'}
      </p>
    </div>
  );
}

export function GameNightsContent() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab') ?? 'upcoming';
  const { drawCard } = useCardHand();

  useEffect(() => {
    drawCard({
      id: 'section-game-nights',
      entity: 'event',
      title: 'Game Nights',
      href: '/game-nights',
    });
  }, [drawCard]);

  const upcomingQuery = useUpcomingGameNights();
  const mineQuery = useMyGameNights();

  const isUpcoming = tab !== 'mine';
  const { data, isLoading, error } = isUpcoming ? upcomingQuery : mineQuery;

  if (isLoading) return <GameNightsLoadingSkeleton />;

  if (error) {
    return (
      <div className="text-center py-12 text-destructive font-nunito">
        <p>Errore nel caricamento delle serate.</p>
      </div>
    );
  }

  if (!data || data.length === 0) return <EmptyState tab={tab} />;

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 p-4">
      {data.map(event => (
        <GameNightCard key={event.id} event={event} />
      ))}
    </div>
  );
}

export function GameNightsLoadingSkeleton() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 p-4">
      {Array.from({ length: 6 }).map((_, i) => (
        <Card key={i}>
          <CardHeader className="pb-2">
            <Skeleton className="h-5 w-3/4" />
            <Skeleton className="h-4 w-1/2 mt-1" />
          </CardHeader>
          <CardContent className="space-y-2">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-2/3" />
            <Skeleton className="h-4 w-1/2" />
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
