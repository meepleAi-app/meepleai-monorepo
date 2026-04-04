/**
 * Edit Game Night Page
 * Issue #33 — P3 Game Night Frontend
 */

'use client';

import { use } from 'react';

import { useRouter } from 'next/navigation';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useGameNight, useUpdateGameNight } from '@/hooks/queries/useGameNights';
import { useToast } from '@/hooks/useToast';
import type { CreateGameNightInput } from '@/lib/api/schemas/game-nights.schemas';

import { GameNightForm } from '../../_components/GameNightForm';

export default function EditGameNightPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const { toast } = useToast();

  const { data: event, isLoading } = useGameNight(id);
  const updateMutation = useUpdateGameNight();

  function handleSubmit(data: CreateGameNightInput) {
    updateMutation.mutate(
      { id, input: data },
      {
        onSuccess: () => {
          toast({ title: 'Serata aggiornata' });
          router.push(`/game-nights/${id}`);
        },
        onError: () => {
          toast({
            title: 'Errore',
            description: 'Impossibile aggiornare la serata.',
            variant: 'destructive',
          });
        },
      }
    );
  }

  if (isLoading || !event) {
    return (
      <div className="p-4 max-w-2xl mx-auto space-y-4">
        <Skeleton className="h-8 w-3/4" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  return (
    <div className="p-4">
      <GameNightForm
        defaultValues={{
          title: event.title,
          description: event.description ?? undefined,
          scheduledAt: event.scheduledAt,
          location: event.location ?? undefined,
          maxPlayers: event.maxPlayers ?? undefined,
        }}
        onSubmit={handleSubmit}
        isSubmitting={updateMutation.isPending}
        submitLabel="Salva Modifiche"
      />
    </div>
  );
}
