/**
 * Add Private Game Form Component
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.2)
 *
 * Manual entry form for adding private games to library.
 * Features:
 * - Full Zod validation with react-hook-form
 * - Player count validation (max >= min)
 * - Optional fields for metadata
 * - Loading states and error handling
 */

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';

// Form schema with validation
const AddPrivateGameFormSchema = z.object({
  title: z.string().min(1, 'Title is required').max(200, 'Title too long'),
  minPlayers: z.number().int().min(1, 'Min 1 player').max(99, 'Max 99 players'),
  maxPlayers: z.number().int().min(1, 'Min 1 player').max(99, 'Max 99 players'),
  yearPublished: z.number().int().min(1900).max(2100).nullable().optional(),
  playingTimeMinutes: z.number().int().min(1).max(10000).nullable().optional(),
  minAge: z.number().int().min(0).max(99).nullable().optional(),
  complexityRating: z.number().min(0).max(5).nullable().optional(),
  description: z.string().max(5000, 'Description too long').nullable().optional(),
  imageUrl: z.string().url('Invalid URL').nullable().optional().or(z.literal('')),
}).refine((data) => data.maxPlayers >= data.minPlayers, {
  message: 'Max players must be >= min players',
  path: ['maxPlayers'],
});

export type AddPrivateGameFormData = z.infer<typeof AddPrivateGameFormSchema>;

export interface AddPrivateGameFormProps {
  /** Callback on successful form submission */
  onSubmit: (data: AddPrivateGameFormData) => Promise<void>;
  /** Callback to cancel form */
  onCancel: () => void;
  /** Whether form is submitting */
  isSubmitting?: boolean;
}

export function AddPrivateGameForm({ onSubmit, onCancel, isSubmitting = false }: AddPrivateGameFormProps) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<AddPrivateGameFormData>({
    resolver: zodResolver(AddPrivateGameFormSchema),
    defaultValues: {
      minPlayers: 1,
      maxPlayers: 4,
      yearPublished: new Date().getFullYear(),
    },
  });

  const onSubmitForm = async (data: AddPrivateGameFormData) => {
    try {
      await onSubmit(data);
    } catch (error) {
      console.error('Form submission error:', error);
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmitForm)} className="space-y-6">
      {/* Title */}
      <div className="space-y-2">
        <Label htmlFor="title">
          Title <span className="text-destructive">*</span>
        </Label>
        <Input
          id="title"
          {...register('title')}
          placeholder="e.g., Catan"
          disabled={isSubmitting}
        />
        {errors.title && (
          <p className="text-sm text-destructive">{errors.title.message}</p>
        )}
      </div>

      {/* Player Count */}
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="minPlayers">
            Min Players <span className="text-destructive">*</span>
          </Label>
          <Input
            id="minPlayers"
            type="number"
            min="1"
            max="99"
            {...register('minPlayers', { valueAsNumber: true })}
            disabled={isSubmitting}
          />
          {errors.minPlayers && (
            <p className="text-sm text-destructive">{errors.minPlayers.message}</p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="maxPlayers">
            Max Players <span className="text-destructive">*</span>
          </Label>
          <Input
            id="maxPlayers"
            type="number"
            min="1"
            max="99"
            {...register('maxPlayers', { valueAsNumber: true })}
            disabled={isSubmitting}
          />
          {errors.maxPlayers && (
            <p className="text-sm text-destructive">{errors.maxPlayers.message}</p>
          )}
        </div>
      </div>

      {/* Year & Playing Time */}
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="yearPublished">Year Published</Label>
          <Input
            id="yearPublished"
            type="number"
            min="1900"
            max="2100"
            {...register('yearPublished', { valueAsNumber: true })}
            disabled={isSubmitting}
          />
          {errors.yearPublished && (
            <p className="text-sm text-destructive">{errors.yearPublished.message}</p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="playingTimeMinutes">Playing Time (min)</Label>
          <Input
            id="playingTimeMinutes"
            type="number"
            min="1"
            max="10000"
            {...register('playingTimeMinutes', { valueAsNumber: true })}
            placeholder="e.g., 60"
            disabled={isSubmitting}
          />
          {errors.playingTimeMinutes && (
            <p className="text-sm text-destructive">{errors.playingTimeMinutes.message}</p>
          )}
        </div>
      </div>

      {/* Min Age & Complexity */}
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="minAge">Min Age</Label>
          <Input
            id="minAge"
            type="number"
            min="0"
            max="99"
            {...register('minAge', { valueAsNumber: true })}
            placeholder="e.g., 10"
            disabled={isSubmitting}
          />
          {errors.minAge && (
            <p className="text-sm text-destructive">{errors.minAge.message}</p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="complexityRating">Complexity (0-5)</Label>
          <Input
            id="complexityRating"
            type="number"
            min="0"
            max="5"
            step="0.1"
            {...register('complexityRating', { valueAsNumber: true })}
            placeholder="e.g., 2.5"
            disabled={isSubmitting}
          />
          {errors.complexityRating && (
            <p className="text-sm text-destructive">{errors.complexityRating.message}</p>
          )}
        </div>
      </div>

      {/* Description */}
      <div className="space-y-2">
        <Label htmlFor="description">Description</Label>
        <Textarea
          id="description"
          {...register('description')}
          placeholder="Game description..."
          rows={4}
          disabled={isSubmitting}
        />
        {errors.description && (
          <p className="text-sm text-destructive">{errors.description.message}</p>
        )}
      </div>

      {/* Image URL */}
      <div className="space-y-2">
        <Label htmlFor="imageUrl">Image URL</Label>
        <Input
          id="imageUrl"
          type="url"
          {...register('imageUrl')}
          placeholder="https://example.com/image.jpg"
          disabled={isSubmitting}
        />
        {errors.imageUrl && (
          <p className="text-sm text-destructive">{errors.imageUrl.message}</p>
        )}
      </div>

      {/* Form Actions */}
      <div className="flex justify-end gap-3 pt-4">
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Adding...' : 'Add Private Game'}
        </Button>
      </div>
    </form>
  );
}
