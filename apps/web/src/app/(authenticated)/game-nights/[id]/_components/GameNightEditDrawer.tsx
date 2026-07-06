'use client';

import { useRouter, useSearchParams } from 'next/navigation';

import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer/drawer';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useUpdateGameNight } from '@/hooks/queries/useGameNights';
import { useToast } from '@/hooks/useToast';
import { useTranslation } from '@/hooks/useTranslation';
import { ConflictError } from '@/lib/api/core/errors';
import type { CreateGameNightInput } from '@/lib/api/schemas/game-nights.schemas';

import { GameNightForm } from '../../_components/GameNightForm';

export interface GameNightEditDrawerProps {
  readonly gameNightId: string;
  readonly organizerId: string;
  readonly defaultValues: {
    title: string;
    description?: string;
    scheduledAt: string;
    location?: string;
    maxPlayers?: number;
  };
}

/**
 * Edit-overlay drawer for a game night (ADR-079). Opened by the `?action=edit`
 * deep-link on `/game-nights/[id]`; closing strips the param so browser
 * back/forward and shareable links stay coherent.
 *
 * IDOR-guard: the drawer is only mounted for the organiser. A non-organiser
 * who forges `?action=edit` never sees the form — and the BE `UpdateGameNight`
 * organiser-guard rejects the mutation regardless (defence in depth).
 *
 * Optimistic-concurrency (#2703): a 409 `concurrent_edit` surfaces a
 * reload-and-retry toast instead of the generic error, so a host whose form
 * went stale (concurrent edit / RSVP write) is told to refresh.
 */
export function GameNightEditDrawer({
  gameNightId,
  organizerId,
  defaultValues,
}: GameNightEditDrawerProps): React.JSX.Element | null {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t } = useTranslation();
  const { toast } = useToast();
  const { data: viewer } = useCurrentUser();
  const updateMutation = useUpdateGameNight();

  const isOrganizer = !!viewer && viewer.id === organizerId;

  // IDOR-guard: never mount edit UI for a non-organiser, even with the param set.
  if (!isOrganizer) {
    return null;
  }

  const open = searchParams.get('action') === 'edit';

  function handleClose(): void {
    router.replace(`/game-nights/${gameNightId}`);
  }

  function handleSubmit(input: CreateGameNightInput): void {
    updateMutation.mutate(
      { id: gameNightId, input },
      {
        onSuccess: () => {
          toast({ title: t('gameNightDetail.editDrawer.savedToast') });
          handleClose();
        },
        onError: (error: unknown) => {
          const isConflict =
            error instanceof ConflictError ||
            (error as { statusCode?: number })?.statusCode === 409;
          if (isConflict) {
            toast({
              title: t('gameNightDetail.errors.concurrentEdit.title'),
              description: t('gameNightDetail.errors.concurrentEdit.body'),
              variant: 'destructive',
            });
            return;
          }
          toast({
            title: t('gameNightDetail.editDrawer.errorGeneric'),
            variant: 'destructive',
          });
        },
      }
    );
  }

  return (
    <Drawer
      open={open}
      onOpenChange={next => {
        if (!next) handleClose();
      }}
      entity="event"
    >
      <DrawerContent>
        <DrawerHeader>
          <DrawerTitle>{t('gameNightDetail.editDrawer.title')}</DrawerTitle>
          <DrawerDescription>{t('gameNightDetail.editDrawer.description')}</DrawerDescription>
        </DrawerHeader>
        <div className="overflow-y-auto p-4">
          <GameNightForm
            defaultValues={defaultValues}
            onSubmit={handleSubmit}
            isSubmitting={updateMutation.isPending}
            submitLabel={t('gameNightDetail.editDrawer.submit')}
          />
        </div>
      </DrawerContent>
    </Drawer>
  );
}
