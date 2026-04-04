/**
 * Create Game Night Page
 * Issue #33 — P3 Game Night Frontend
 */

'use client';

import { useRouter } from 'next/navigation';

import { useCreateGameNight } from '@/hooks/queries/useGameNights';
import { useToast } from '@/hooks/useToast';
import type { CreateGameNightInput } from '@/lib/api/schemas/game-nights.schemas';

import { GameNightForm } from '../_components/GameNightForm';

export default function NewGameNightPage() {
  const router = useRouter();
  const { toast } = useToast();
  const createMutation = useCreateGameNight();

  function handleSubmit(data: CreateGameNightInput) {
    createMutation.mutate(data, {
      onSuccess: id => {
        toast({ title: 'Serata creata', description: 'La serata è stata creata come bozza.' });
        router.push(`/game-nights/${id}`);
      },
      onError: () => {
        toast({
          title: 'Errore',
          description: 'Impossibile creare la serata.',
          variant: 'destructive',
        });
      },
    });
  }

  return (
    <div className="p-4">
      <GameNightForm onSubmit={handleSubmit} isSubmitting={createMutation.isPending} />
    </div>
  );
}
