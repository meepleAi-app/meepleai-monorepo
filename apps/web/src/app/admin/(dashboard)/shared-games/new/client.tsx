/**
 * New Game Client Component - New Admin Dashboard
 *
 * Form with BGG search integration for auto-filling game data.
 * Fields: Title, Description, Year, Min/Max Players, Playing Time, Min Age,
 * Image URL, Categories, Mechanics, Designers, Publishers, Ratings.
 * Submits via api.sharedGames.create() then redirects to detail page.
 */

'use client';

import { useState } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, FileUp, Loader2, Plus } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useForm, type Resolver } from 'react-hook-form';
import { z } from 'zod';

import { BggSearchPanel } from '@/components/admin/shared-games/BggSearchPanel';
import { MetadataTagInput } from '@/components/admin/shared-games/MetadataTagInput';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { BggFullGameData } from '@/types/bgg';

// ─── Form schema ──────────────────────────────────────────────────────────────

const NewGameSchema = z
  .object({
    title: z.string().min(1, 'Title is required').max(255),
    description: z.string().min(1, 'Description is required'),
    yearPublished: z.coerce
      .number()
      .int()
      .min(1900, 'Year must be >= 1900')
      .max(2100, 'Year must be <= 2100'),
    minPlayers: z.coerce.number().int().min(1, 'Min 1 player').max(100),
    maxPlayers: z.coerce.number().int().min(1, 'Min 1 player').max(100),
    playingTimeMinutes: z.coerce.number().int().min(1, 'Min 1 minute').max(10000),
    minAge: z.coerce.number().int().min(0).max(100).default(0),
    imageUrl: z.union([z.string().url('Enter a valid URL'), z.literal('')]).optional(),
    thumbnailUrl: z.union([z.string().url(), z.literal('')]).optional(),
    bggId: z.coerce.number().int().positive().optional(),
    complexityRating: z.coerce.number().min(0).max(5).optional(),
    averageRating: z.coerce.number().min(0).max(10).optional(),
    categories: z.array(z.string()).default([]),
    mechanics: z.array(z.string()).default([]),
    designers: z.array(z.string()).default([]),
    publishers: z.array(z.string()).default([]),
  })
  .refine(data => data.maxPlayers >= data.minPlayers, {
    message: 'Max players must be >= min players',
    path: ['maxPlayers'],
  });

type NewGameFormData = z.infer<typeof NewGameSchema>;

// ─── Component ────────────────────────────────────────────────────────────────

export function NewGameClient() {
  const router = useRouter();
  const [selectedBggId, setSelectedBggId] = useState<number | null>(null);
  const [bggFilledFields, setBggFilledFields] = useState<Set<string>>(new Set());

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
    setValue,
    watch,
  } = useForm<NewGameFormData>({
    // Type assertion needed because z.coerce.number() creates unknown input type in Zod v4
    resolver: zodResolver(NewGameSchema) as Resolver<NewGameFormData>,
    defaultValues: {
      yearPublished: new Date().getFullYear(),
      minPlayers: 2,
      maxPlayers: 4,
      playingTimeMinutes: 60,
      minAge: 10,
      categories: [],
      mechanics: [],
      designers: [],
      publishers: [],
    },
  });

  // Fetch autocomplete suggestions for metadata tag inputs
  const { data: distinctMetadata } = useQuery({
    queryKey: ['shared-games', 'metadata', 'distinct'],
    queryFn: () => api.sharedGames.getDistinctMetadata(),
    staleTime: 5 * 60 * 1000,
  });

  const onBggSelect = (data: BggFullGameData) => {
    const fieldsToFill: Record<string, unknown> = {
      title: data.name,
      description: data.description ?? '',
      yearPublished: data.yearPublished,
      minPlayers: data.minPlayers,
      maxPlayers: data.maxPlayers,
      playingTimeMinutes: data.playingTime,
      minAge: data.minAge ?? 0,
      imageUrl: data.imageUrl ?? '',
      thumbnailUrl: data.thumbnailUrl ?? '',
      bggId: data.id,
      complexityRating: data.complexityRating,
      averageRating: data.averageRating,
      categories: data.categories,
      mechanics: data.mechanics,
      designers: data.designers,
      publishers: data.publishers,
    };

    const filled = new Set<string>();
    for (const [key, value] of Object.entries(fieldsToFill)) {
      if (value !== undefined && value !== null) {
        setValue(key as keyof NewGameFormData, value as never, { shouldValidate: true });
        filled.add(key);
      }
    }
    setBggFilledFields(filled);
    setSelectedBggId(data.id);
  };

  const bggFieldClass = (field: string) => (bggFilledFields.has(field) ? 'bg-orange-50/50' : '');

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
        thumbnailUrl:
          data.thumbnailUrl || data.imageUrl || 'https://placehold.co/150x150?text=No+Image',
        bggId: data.bggId,
        complexityRating: data.complexityRating,
        averageRating: data.averageRating,
        categories: data.categories,
        mechanics: data.mechanics,
        designers: data.designers,
        publishers: data.publishers,
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

      {/* Cross-link to Import flow (#255) */}
      <div className="rounded-lg border border-dashed border-slate-300 dark:border-zinc-600 bg-slate-50/50 dark:bg-zinc-800/30 px-4 py-3 flex items-center gap-3">
        <FileUp className="h-4 w-4 text-muted-foreground shrink-0" />
        <p className="text-sm text-muted-foreground">
          Have a PDF rulebook?{' '}
          <Link
            href="/admin/shared-games/import"
            className="text-primary font-medium underline underline-offset-2 hover:text-primary/80"
          >
            Import from PDF
          </Link>{' '}
          to auto-extract game metadata.
        </p>
      </div>

      {/* BGG Search Section */}
      <div className="mb-6">
        <BggSearchPanel onSelect={onBggSelect} />
        {selectedBggId && (
          <div className="mt-2 flex items-center gap-2">
            <span className="inline-flex items-center rounded-full bg-blue-100 px-3 py-1 text-sm font-medium text-blue-800">
              Linked to BGG #{selectedBggId}
            </span>
          </div>
        )}
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
                className={`${errors.title ? 'border-destructive' : ''} ${bggFieldClass('title')}`}
              />
              {errors.title && <p className="text-xs text-destructive">{errors.title.message}</p>}
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
                className={`${errors.description ? 'border-destructive' : ''} ${bggFieldClass('description')}`}
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
                  className={`${errors.yearPublished ? 'border-destructive' : ''} ${bggFieldClass('yearPublished')}`}
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
                  className={`${errors.playingTimeMinutes ? 'border-destructive' : ''} ${bggFieldClass('playingTimeMinutes')}`}
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
                  className={`${errors.minPlayers ? 'border-destructive' : ''} ${bggFieldClass('minPlayers')}`}
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
                  className={`${errors.maxPlayers ? 'border-destructive' : ''} ${bggFieldClass('maxPlayers')}`}
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
                  className={bggFieldClass('minAge')}
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
                className={`${errors.imageUrl ? 'border-destructive' : ''} ${bggFieldClass('imageUrl')}`}
              />
              {errors.imageUrl && (
                <p className="text-xs text-destructive">{errors.imageUrl.message}</p>
              )}
              <p className="text-xs text-muted-foreground">
                Leave empty to use a placeholder. You can update it later.
              </p>
            </div>

            {/* BGG Metadata */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <MetadataTagInput
                label="Categories"
                tags={watch('categories') ?? []}
                onChange={tags => setValue('categories', tags)}
                maxTags={50}
                colorClass="bg-purple-100 text-purple-800"
                suggestions={distinctMetadata?.categories ?? []}
              />
              <MetadataTagInput
                label="Mechanics"
                tags={watch('mechanics') ?? []}
                onChange={tags => setValue('mechanics', tags)}
                maxTags={50}
                colorClass="bg-rose-100 text-rose-800"
                suggestions={distinctMetadata?.mechanics ?? []}
              />
              <MetadataTagInput
                label="Designers"
                tags={watch('designers') ?? []}
                onChange={tags => setValue('designers', tags)}
                maxTags={20}
                colorClass="bg-green-100 text-green-800"
                suggestions={distinctMetadata?.designers ?? []}
              />
              <MetadataTagInput
                label="Publishers"
                tags={watch('publishers') ?? []}
                onChange={tags => setValue('publishers', tags)}
                maxTags={20}
                colorClass="bg-amber-100 text-amber-800"
                suggestions={distinctMetadata?.publishers ?? []}
              />
            </div>

            {/* Ratings (only show when BGG data is loaded) */}
            {selectedBggId && (
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label>Complexity Rating (0-5)</Label>
                  <Input
                    type="number"
                    step="0.01"
                    min="0"
                    max="5"
                    {...register('complexityRating', { valueAsNumber: true })}
                    className={bggFieldClass('complexityRating')}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label>Average Rating (0-10)</Label>
                  <Input
                    type="number"
                    step="0.01"
                    min="0"
                    max="10"
                    {...register('averageRating', { valueAsNumber: true })}
                    className={bggFieldClass('averageRating')}
                  />
                </div>
              </div>
            )}

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
