/**
 * New Game Client Component - New Admin Dashboard
 *
 * Minimal form: Title, Description, Year, Min/Max Players, Playing Time, Min Age.
 * Optional: Image URL. Submits via api.sharedGames.create() then redirects to detail page.
 */

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowLeft, Loader2, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useForm, type Resolver } from 'react-hook-form';
import { z } from 'zod';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';

// ─── Form schema ──────────────────────────────────────────────────────────────

const NewGameSchema = z
  .object({
    title: z.string().min(1, 'Title is required').max(255),
    description: z.string().min(1, 'Description is required'),
    yearPublished: z.coerce.number().int().min(1900, 'Year must be ≥ 1900').max(2100, 'Year must be ≤ 2100'),
    minPlayers: z.coerce.number().int().min(1, 'Min 1 player').max(100),
    maxPlayers: z.coerce.number().int().min(1, 'Min 1 player').max(100),
    playingTimeMinutes: z.coerce.number().int().min(1, 'Min 1 minute').max(10000),
    minAge: z.coerce.number().int().min(0).max(100).default(0),
    imageUrl: z.union([z.string().url('Enter a valid URL'), z.literal('')]).optional(),
  })
  .refine((data) => data.maxPlayers >= data.minPlayers, {
    message: 'Max players must be ≥ min players',
    path: ['maxPlayers'],
  });

type NewGameFormData = z.infer<typeof NewGameSchema>;

// ─── Component ────────────────────────────────────────────────────────────────

export function NewGameClient() {
  const router = useRouter();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<NewGameFormData>({
    // Type assertion needed because z.coerce.number() creates unknown input type in Zod v4
    resolver: zodResolver(NewGameSchema) as Resolver<NewGameFormData>,
    defaultValues: {
      yearPublished: new Date().getFullYear(),
      minPlayers: 2,
      maxPlayers: 4,
      playingTimeMinutes: 60,
      minAge: 10,
    },
  });

  const onSubmit = async (data: NewGameFormData) => {
    try {
      const gameId = await api.sharedGames.create({
        title: data.title,
        description: data.description,
        yearPublished: data.yearPublished,
        minPlayers: data.minPlayers,
        maxPlayers: data.maxPlayers,
        playingTimeMinutes: data.playingTimeMinutes,
        minAge: data.minAge,
        imageUrl: data.imageUrl || 'https://placehold.co/300x300?text=No+Image',
        thumbnailUrl: data.imageUrl || 'https://placehold.co/150x150?text=No+Image',
      });
      router.push(`/admin/shared-games/${gameId}`);
    } catch (err) {
      setError('root', {
        message: err instanceof Error ? err.message : 'Failed to create game. Please try again.',
      });
    }
  };

  return (
    <div className="space-y-6 max-w-2xl">
      {/* Header */}
      <div>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => router.push('/admin/shared-games/all')}
          className="mb-4 -ml-2"
        >
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to All Games
        </Button>

        <div className="flex items-center gap-3">
          <div className="h-10 w-10 rounded-lg bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center">
            <Plus className="h-5 w-5 text-amber-700 dark:text-amber-400" />
          </div>
          <div>
            <h1 className="font-quicksand text-2xl font-bold tracking-tight">Add New Game</h1>
            <p className="text-sm text-muted-foreground">
              Create a new entry in the shared game catalog
            </p>
          </div>
        </div>
      </div>

      {/* Form card */}
      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
        <CardHeader>
          <CardTitle className="font-quicksand">Game Details</CardTitle>
          <CardDescription>
            The game will be created in Draft status. You can upload PDF documents and publish it
            after creation.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            {/* Root error */}
            {errors.root && (
              <Alert variant="destructive">
                <AlertDescription>{errors.root.message}</AlertDescription>
              </Alert>
            )}

            {/* Title */}
            <div className="space-y-1.5">
              <Label htmlFor="title">
                Title <span className="text-destructive">*</span>
              </Label>
              <Input
                id="title"
                placeholder="e.g. Catan"
                {...register('title')}
                className={errors.title ? 'border-destructive' : ''}
              />
              {errors.title && (
                <p className="text-xs text-destructive">{errors.title.message}</p>
              )}
            </div>

            {/* Description */}
            <div className="space-y-1.5">
              <Label htmlFor="description">
                Description <span className="text-destructive">*</span>
              </Label>
              <Textarea
                id="description"
                rows={4}
                placeholder="Brief description of the game..."
                {...register('description')}
                className={errors.description ? 'border-destructive' : ''}
              />
              {errors.description && (
                <p className="text-xs text-destructive">{errors.description.message}</p>
              )}
            </div>

            {/* Year + Playing Time */}
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label htmlFor="yearPublished">
                  Year Published <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="yearPublished"
                  type="number"
                  {...register('yearPublished')}
                  className={errors.yearPublished ? 'border-destructive' : ''}
                />
                {errors.yearPublished && (
                  <p className="text-xs text-destructive">{errors.yearPublished.message}</p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="playingTimeMinutes">
                  Playing Time (min) <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="playingTimeMinutes"
                  type="number"
                  {...register('playingTimeMinutes')}
                  className={errors.playingTimeMinutes ? 'border-destructive' : ''}
                />
                {errors.playingTimeMinutes && (
                  <p className="text-xs text-destructive">{errors.playingTimeMinutes.message}</p>
                )}
              </div>
            </div>

            {/* Players */}
            <div className="grid grid-cols-3 gap-4">
              <div className="space-y-1.5">
                <Label htmlFor="minPlayers">
                  Min Players <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="minPlayers"
                  type="number"
                  {...register('minPlayers')}
                  className={errors.minPlayers ? 'border-destructive' : ''}
                />
                {errors.minPlayers && (
                  <p className="text-xs text-destructive">{errors.minPlayers.message}</p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="maxPlayers">
                  Max Players <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="maxPlayers"
                  type="number"
                  {...register('maxPlayers')}
                  className={errors.maxPlayers ? 'border-destructive' : ''}
                />
                {errors.maxPlayers && (
                  <p className="text-xs text-destructive">{errors.maxPlayers.message}</p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="minAge">Min Age</Label>
                <Input
                  id="minAge"
                  type="number"
                  {...register('minAge')}
                />
              </div>
            </div>

            {/* Image URL (optional) */}
            <div className="space-y-1.5">
              <Label htmlFor="imageUrl">
                Image URL <span className="text-muted-foreground text-xs">(optional)</span>
              </Label>
              <Input
                id="imageUrl"
                type="url"
                placeholder="https://example.com/image.jpg"
                {...register('imageUrl')}
                className={errors.imageUrl ? 'border-destructive' : ''}
              />
              {errors.imageUrl && (
                <p className="text-xs text-destructive">{errors.imageUrl.message}</p>
              )}
              <p className="text-xs text-muted-foreground">
                Leave empty to use a placeholder. You can update it later.
              </p>
            </div>

            {/* Actions */}
            <div className="flex gap-3 pt-2">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Creating...
                  </>
                ) : (
                  <>
                    <Plus className="mr-2 h-4 w-4" />
                    Create Game
                  </>
                )}
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => router.push('/admin/shared-games/all')}
                disabled={isSubmitting}
              >
                Cancel
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
