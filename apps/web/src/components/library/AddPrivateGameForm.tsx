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
import { useTranslation } from '@/hooks/useTranslation';
import { logger } from '@/lib/logger';

// Form schema with validation
const AddPrivateGameFormSchema = z
  .object({
    title: z.string().min(1, 'Title is required').max(200, 'Title too long'),
    minPlayers: z.number().int().min(1, 'Min 1 player').max(99, 'Max 99 players'),
    maxPlayers: z.number().int().min(1, 'Min 1 player').max(99, 'Max 99 players'),
    yearPublished: z.number().int().min(1900).max(2100).nullable().optional(),
    playingTimeMinutes: z.number().int().min(1).max(10000).nullable().optional(),
    minAge: z.number().int().min(0).max(99).nullable().optional(),
    complexityRating: z.number().min(0).max(5).nullable().optional(),
    description: z.string().max(5000, 'Description too long').nullable().optional(),
    imageUrl: z.string().url('Invalid URL').nullable().optional().or(z.literal('')),
  })
  .refine(data => data.maxPlayers >= data.minPlayers, {
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
  /** Optional initial values to pre-populate the form (e.g., from BGG data) */
  initialValues?: Partial<AddPrivateGameFormData>;
  /** Submit button label override */
  submitLabel?: string;
}

export function AddPrivateGameForm({
  onSubmit,
  onCancel,
  isSubmitting = false,
  initialValues,
  submitLabel,
}: AddPrivateGameFormProps) {
  const { t } = useTranslation();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<AddPrivateGameFormData>({
    resolver: zodResolver(AddPrivateGameFormSchema),
    defaultValues: {
      minPlayers: initialValues?.minPlayers ?? 1,
      maxPlayers: initialValues?.maxPlayers ?? 4,
      yearPublished: initialValues?.yearPublished ?? new Date().getFullYear(),
      title: initialValues?.title ?? '',
      playingTimeMinutes: initialValues?.playingTimeMinutes ?? undefined,
      minAge: initialValues?.minAge ?? undefined,
      complexityRating: initialValues?.complexityRating ?? undefined,
      description: initialValues?.description ?? undefined,
      imageUrl: initialValues?.imageUrl ?? undefined,
    },
  });

  const onSubmitForm = async (data: AddPrivateGameFormData) => {
    try {
      await onSubmit(data);
    } catch (error) {
      logger.error('Form submission error:', error);
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmitForm)} className="space-y-6">
      {/* Title */}
      <div className="space-y-2">
        <Label htmlFor="title">
          {t('privateGameForm.title')} <span className="text-destructive">*</span>
        </Label>
        <Input
          id="title"
          {...register('title')}
          placeholder={t('privateGameForm.titlePlaceholder')}
          disabled={isSubmitting}
        />
        {errors.title && <p className="text-sm text-destructive">{errors.title.message}</p>}
      </div>

      {/* Player Count */}
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="minPlayers">
            {t('privateGameForm.minPlayers')} <span className="text-destructive">*</span>
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
            {t('privateGameForm.maxPlayers')} <span className="text-destructive">*</span>
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
          <Label htmlFor="yearPublished">{t('privateGameForm.yearPublished')}</Label>
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
          <Label htmlFor="playingTimeMinutes">{t('privateGameForm.playingTime')}</Label>
          <Input
            id="playingTimeMinutes"
            type="number"
            min="1"
            max="10000"
            {...register('playingTimeMinutes', { valueAsNumber: true })}
            placeholder={t('privateGameForm.playingTimePlaceholder')}
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
          <Label htmlFor="minAge">{t('privateGameForm.minAge')}</Label>
          <Input
            id="minAge"
            type="number"
            min="0"
            max="99"
            {...register('minAge', { valueAsNumber: true })}
            placeholder={t('privateGameForm.minAgePlaceholder')}
            disabled={isSubmitting}
          />
          {errors.minAge && <p className="text-sm text-destructive">{errors.minAge.message}</p>}
        </div>

        <div className="space-y-2">
          <Label htmlFor="complexityRating">{t('privateGameForm.complexity')}</Label>
          <Input
            id="complexityRating"
            type="number"
            min="0"
            max="5"
            step="0.1"
            {...register('complexityRating', { valueAsNumber: true })}
            placeholder={t('privateGameForm.complexityPlaceholder')}
            disabled={isSubmitting}
          />
          {errors.complexityRating && (
            <p className="text-sm text-destructive">{errors.complexityRating.message}</p>
          )}
        </div>
      </div>

      {/* Description */}
      <div className="space-y-2">
        <Label htmlFor="description">{t('privateGameForm.description')}</Label>
        <Textarea
          id="description"
          {...register('description')}
          placeholder={t('privateGameForm.descriptionPlaceholder')}
          rows={4}
          disabled={isSubmitting}
        />
        {errors.description && (
          <p className="text-sm text-destructive">{errors.description.message}</p>
        )}
      </div>

      {/* Image URL */}
      <div className="space-y-2">
        <Label htmlFor="imageUrl">{t('privateGameForm.imageUrl')}</Label>
        <Input
          id="imageUrl"
          type="url"
          {...register('imageUrl')}
          placeholder={t('privateGameForm.imageUrlPlaceholder')}
          disabled={isSubmitting}
        />
        {errors.imageUrl && <p className="text-sm text-destructive">{errors.imageUrl.message}</p>}
      </div>

      {/* Form Actions */}
      <div className="flex justify-end gap-3 pt-4">
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting
            ? t('privateGameForm.adding')
            : (submitLabel ?? t('privateGameForm.addPrivateGame'))}
        </Button>
      </div>
    </form>
  );
}
