/**
 * Edit Game Drawer — admin shared-games detail page.
 *
 * Replaces the disabled "Edit Game" button on /admin/shared-games/[id].
 * Pre-populates from current game data, submits via api.sharedGames.update()
 * (PUT /api/v1/admin/shared-games/{id}).
 */

'use client';

import { useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useForm, type Resolver } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { sharedGamesKeys } from '@/hooks/queries';
import { api } from '@/lib/api';

/** Minimal SharedGame shape used by the drawer (works with SharedGame list DTO and SharedGameDetail). */
export interface EditGameTarget {
  readonly id: string;
  readonly title: string;
  readonly description?: string | null;
  readonly yearPublished?: number | null;
  readonly minPlayers?: number | null;
  readonly maxPlayers?: number | null;
  readonly playingTimeMinutes?: number | null;
  readonly minAge?: number | null;
  readonly imageUrl?: string | null;
  readonly thumbnailUrl?: string | null;
}

// Form schema mirrors the editable subset of UpdateSharedGameRequestSchema.
// Optional/nullable rating fields and structured rules content are out of scope
// for this drawer (they have dedicated editors elsewhere).
const EditGameFormSchema = z
  .object({
    title: z.string().min(1, 'Title is required').max(255),
    description: z.string().min(1, 'Description is required'),
    yearPublished: z.coerce
      .number()
      .int()
      .min(1900, 'Year must be >= 1900')
      .max(2100, 'Year must be <= 2100'),
    minPlayers: z.coerce.number().int().min(1).max(100),
    maxPlayers: z.coerce.number().int().min(1).max(100),
    playingTimeMinutes: z.coerce.number().int().min(1).max(10000),
    minAge: z.coerce.number().int().min(0).max(100).default(0),
    imageUrl: z.union([z.string().url('Enter a valid URL'), z.literal('')]).optional(),
    thumbnailUrl: z.union([z.string().url(), z.literal('')]).optional(),
  })
  .refine(d => d.maxPlayers >= d.minPlayers, {
    message: 'Max players must be >= min players',
    path: ['maxPlayers'],
  });

type EditGameFormData = z.infer<typeof EditGameFormSchema>;

export interface EditGameDrawerProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly game: EditGameTarget;
}

export function EditGameDrawer({ open, onOpenChange, game }: EditGameDrawerProps) {
  const queryClient = useQueryClient();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<EditGameFormData>({
    resolver: zodResolver(EditGameFormSchema) as Resolver<EditGameFormData>,
    defaultValues: {
      title: game.title,
      description: game.description ?? '',
      yearPublished: game.yearPublished ?? new Date().getFullYear(),
      minPlayers: game.minPlayers ?? 1,
      maxPlayers: game.maxPlayers ?? 4,
      playingTimeMinutes: game.playingTimeMinutes ?? 60,
      minAge: game.minAge ?? 0,
      imageUrl: game.imageUrl ?? '',
      thumbnailUrl: game.thumbnailUrl ?? '',
    },
  });

  // Reset form when target game changes (drawer reopened on different game).
  useEffect(() => {
    if (open) {
      reset({
        title: game.title,
        description: game.description ?? '',
        yearPublished: game.yearPublished ?? new Date().getFullYear(),
        minPlayers: game.minPlayers ?? 1,
        maxPlayers: game.maxPlayers ?? 4,
        playingTimeMinutes: game.playingTimeMinutes ?? 60,
        minAge: game.minAge ?? 0,
        imageUrl: game.imageUrl ?? '',
        thumbnailUrl: game.thumbnailUrl ?? '',
      });
    }
  }, [open, game, reset]);

  const updateMutation = useMutation({
    mutationFn: async (data: EditGameFormData) => {
      // Backend requires non-null URL fields; coerce '' to existing or placeholder.
      // Schema upstream enforces .url() so empty string would fail server-side validation.
      const payload = {
        title: data.title,
        description: data.description,
        yearPublished: data.yearPublished,
        minPlayers: data.minPlayers,
        maxPlayers: data.maxPlayers,
        playingTimeMinutes: data.playingTimeMinutes,
        minAge: data.minAge,
        imageUrl: data.imageUrl || game.imageUrl || 'https://placeholder.example/cover.png',
        thumbnailUrl:
          data.thumbnailUrl ||
          game.thumbnailUrl ||
          data.imageUrl ||
          game.imageUrl ||
          'https://placeholder.example/thumb.png',
      };
      await api.sharedGames.update(game.id, payload);
    },
    onSuccess: () => {
      toast.success('Gioco aggiornato');
      void queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', game.id] });
      void queryClient.invalidateQueries({ queryKey: sharedGamesKeys.all });
      onOpenChange(false);
    },
    onError: (err: unknown) => {
      const msg = err instanceof Error ? err.message : 'Errore durante il salvataggio';
      toast.error(msg);
    },
  });

  const onSubmit = handleSubmit(data => updateMutation.mutate(data));

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="right"
        className="w-full max-w-xl overflow-y-auto"
        data-testid="edit-game-drawer"
      >
        <SheetHeader>
          <SheetTitle>Modifica gioco</SheetTitle>
          <SheetDescription>
            Aggiorna metadati di {game.title}. Le modifiche sono visibili immediatamente
            agli utenti del catalogo.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={onSubmit} className="mt-6 space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="edit-title">Titolo *</Label>
            <Input id="edit-title" {...register('title')} aria-invalid={!!errors.title} />
            {errors.title && (
              <p className="text-xs text-destructive">{errors.title.message}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="edit-description">Descrizione *</Label>
            <Textarea
              id="edit-description"
              rows={4}
              {...register('description')}
              aria-invalid={!!errors.description}
            />
            {errors.description && (
              <p className="text-xs text-destructive">{errors.description.message}</p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="edit-year">Anno *</Label>
              <Input
                id="edit-year"
                type="number"
                {...register('yearPublished')}
                aria-invalid={!!errors.yearPublished}
              />
              {errors.yearPublished && (
                <p className="text-xs text-destructive">{errors.yearPublished.message}</p>
              )}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="edit-time">Durata (min) *</Label>
              <Input
                id="edit-time"
                type="number"
                {...register('playingTimeMinutes')}
                aria-invalid={!!errors.playingTimeMinutes}
              />
              {errors.playingTimeMinutes && (
                <p className="text-xs text-destructive">{errors.playingTimeMinutes.message}</p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="edit-min-players">Min giocatori *</Label>
              <Input
                id="edit-min-players"
                type="number"
                {...register('minPlayers')}
                aria-invalid={!!errors.minPlayers}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="edit-max-players">Max giocatori *</Label>
              <Input
                id="edit-max-players"
                type="number"
                {...register('maxPlayers')}
                aria-invalid={!!errors.maxPlayers}
              />
              {errors.maxPlayers && (
                <p className="text-xs text-destructive">{errors.maxPlayers.message}</p>
              )}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="edit-min-age">Età minima</Label>
              <Input id="edit-min-age" type="number" {...register('minAge')} />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="edit-image">Image URL</Label>
            <Input
              id="edit-image"
              type="url"
              placeholder="https://..."
              {...register('imageUrl')}
              aria-invalid={!!errors.imageUrl}
            />
            {errors.imageUrl && (
              <p className="text-xs text-destructive">{errors.imageUrl.message}</p>
            )}
          </div>

          {updateMutation.isError && (
            <Alert variant="destructive">
              <AlertDescription>
                {updateMutation.error instanceof Error
                  ? updateMutation.error.message
                  : 'Errore durante il salvataggio'}
              </AlertDescription>
            </Alert>
          )}

          <div className="sticky bottom-0 -mx-6 flex justify-end gap-2 border-t bg-background px-6 py-4">
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              Annulla
            </Button>
            <Button
              type="submit"
              disabled={!isDirty || isSubmitting || updateMutation.isPending}
              data-testid="edit-game-submit"
            >
              {(isSubmitting || updateMutation.isPending) && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Salva modifiche
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
