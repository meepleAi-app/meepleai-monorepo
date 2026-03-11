/**
 * Game Night Detail Page
 * Issue #33 — P3 Game Night Frontend
 *
 * Shows game night info, RSVP buttons, and participant list.
 */

'use client';

import { use } from 'react';

import { Calendar, Check, Edit, HelpCircle, MapPin, Send, Users, X, XCircle } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  useGameNight,
  useGameNightRsvps,
  usePublishGameNight,
  useCancelGameNight,
  useRsvpGameNight,
} from '@/hooks/queries/useGameNights';
import { useToast } from '@/hooks/use-toast';
import type { RsvpStatus } from '@/lib/api/schemas/game-nights.schemas';

export default function GameNightDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { toast } = useToast();

  const { data: event, isLoading, error } = useGameNight(id);
  const { data: rsvps } = useGameNightRsvps(id);
  const publishMutation = usePublishGameNight();
  const cancelMutation = useCancelGameNight();
  const rsvpMutation = useRsvpGameNight();

  function handleRsvp(response: RsvpStatus) {
    rsvpMutation.mutate(
      { id, response },
      {
        onSuccess: () => {
          const labels: Record<string, string> = {
            Accepted: 'Partecipazione confermata',
            Declined: 'Hai declinato',
            Maybe: 'Risposta registrata',
          };
          toast({ title: labels[response] ?? 'Risposta inviata' });
        },
        onError: () => {
          toast({
            title: 'Errore',
            description: 'Impossibile inviare la risposta.',
            variant: 'destructive',
          });
        },
      }
    );
  }

  function handlePublish() {
    publishMutation.mutate(id, {
      onSuccess: () =>
        toast({
          title: 'Serata pubblicata',
          description: 'Gli invitati riceveranno una notifica.',
        }),
      onError: () =>
        toast({ title: 'Errore', description: 'Impossibile pubblicare.', variant: 'destructive' }),
    });
  }

  function handleCancel() {
    cancelMutation.mutate(id, {
      onSuccess: () => toast({ title: 'Serata annullata' }),
      onError: () =>
        toast({ title: 'Errore', description: 'Impossibile annullare.', variant: 'destructive' }),
    });
  }

  if (isLoading) {
    return (
      <div className="p-4 max-w-2xl mx-auto space-y-4">
        <Skeleton className="h-8 w-3/4" />
        <Skeleton className="h-4 w-1/2" />
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  if (error || !event) {
    return (
      <div className="text-center py-12 text-destructive font-nunito">
        <p>Serata non trovata.</p>
      </div>
    );
  }

  const scheduledDate = new Date(event.scheduledAt);
  const isDraft = event.status === 'Draft';
  const isCancelled = event.status === 'Cancelled';

  return (
    <div className="p-4 max-w-2xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold font-quicksand">{event.title}</h1>
          <p className="text-muted-foreground font-nunito">Organizzata da {event.organizerName}</p>
        </div>
        <div className="flex gap-2">
          {isDraft && (
            <>
              <Button size="sm" variant="outline" asChild>
                <Link href={`/game-nights/${id}/edit`}>
                  <Edit className="h-4 w-4 mr-1" />
                  Modifica
                </Link>
              </Button>
              <Button size="sm" onClick={handlePublish} disabled={publishMutation.isPending}>
                <Send className="h-4 w-4 mr-1" />
                Pubblica
              </Button>
            </>
          )}
          {!isCancelled && !isDraft && (
            <Button
              size="sm"
              variant="destructive"
              onClick={handleCancel}
              disabled={cancelMutation.isPending}
            >
              <XCircle className="h-4 w-4 mr-1" />
              Annulla
            </Button>
          )}
        </div>
      </div>

      {/* Info Card */}
      <Card>
        <CardContent className="pt-6 space-y-3 font-nunito">
          <div className="flex items-center gap-2">
            <Calendar className="h-4 w-4 text-muted-foreground" />
            <span>
              {scheduledDate.toLocaleDateString('it-IT', {
                weekday: 'long',
                day: 'numeric',
                month: 'long',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              })}
            </span>
          </div>
          {event.location && (
            <div className="flex items-center gap-2">
              <MapPin className="h-4 w-4 text-muted-foreground" />
              <span>{event.location}</span>
            </div>
          )}
          <div className="flex items-center gap-2">
            <Users className="h-4 w-4 text-muted-foreground" />
            <span>
              {event.acceptedCount} confermati
              {event.pendingCount > 0 && ` · ${event.pendingCount} in attesa`}
              {event.maxPlayers && ` / ${event.maxPlayers} max`}
            </span>
          </div>
          {event.description && (
            <p className="text-sm text-muted-foreground pt-2 border-t">{event.description}</p>
          )}
        </CardContent>
      </Card>

      {/* RSVP Buttons */}
      {!isDraft && !isCancelled && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base font-quicksand">La tua risposta</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex gap-2 flex-wrap">
              <Button
                variant="outline"
                className="border-green-300 hover:bg-green-50"
                onClick={() => handleRsvp('Accepted')}
                disabled={rsvpMutation.isPending}
              >
                <Check className="h-4 w-4 mr-1 text-green-600" />
                Partecipo
              </Button>
              <Button
                variant="outline"
                className="border-amber-300 hover:bg-amber-50"
                onClick={() => handleRsvp('Maybe')}
                disabled={rsvpMutation.isPending}
              >
                <HelpCircle className="h-4 w-4 mr-1 text-amber-600" />
                Forse
              </Button>
              <Button
                variant="outline"
                className="border-red-300 hover:bg-red-50"
                onClick={() => handleRsvp('Declined')}
                disabled={rsvpMutation.isPending}
              >
                <X className="h-4 w-4 mr-1 text-red-600" />
                Non partecipo
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Participants */}
      {rsvps && rsvps.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base font-quicksand">
              Partecipanti ({rsvps.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="space-y-2">
              {rsvps.map(rsvp => (
                <li key={rsvp.id} className="flex items-center justify-between text-sm font-nunito">
                  <span>{rsvp.userName}</span>
                  <RsvpBadge status={rsvp.status} />
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function RsvpBadge({ status }: { status: string }) {
  switch (status) {
    case 'Accepted':
      return <Badge className="bg-green-100 text-green-800 border-green-200">Confermato</Badge>;
    case 'Declined':
      return <Badge variant="destructive">Declinato</Badge>;
    case 'Maybe':
      return <Badge className="bg-amber-100 text-amber-800 border-amber-200">Forse</Badge>;
    default:
      return <Badge variant="secondary">In attesa</Badge>;
  }
}
