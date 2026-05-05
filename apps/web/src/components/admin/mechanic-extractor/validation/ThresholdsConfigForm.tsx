/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 15)
 *
 * Operator form for the singleton `CertificationThresholds` value object.
 * Mirrors the four-field surface returned by `GET /api/v1/admin/mechanic-extractor/thresholds`
 * and consumed by `PUT /thresholds`.
 *
 * Backend authoritative source for field bounds:
 *   apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CertificationThresholds.cs
 *   apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/Validation/UpdateCertificationThresholdsValidator.cs
 *
 * Scope notes:
 *  - Audit display ("Last updated by … on …") deferred — `GetCertificationThresholdsQueryHandler`
 *    currently returns only the VO, not the aggregate's `UpdatedByUserId` /
 *    `UpdatedAt`. Surfacing those would require a new DTO + handler change and
 *    is intentionally out of scope for Task 15.
 */

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2Icon } from 'lucide-react';
import { useForm, type Resolver } from 'react-hook-form';
import { z } from 'zod';

import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/forms/form';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useUpdateThresholds } from '@/hooks/admin/useUpdateThresholds';
import type { CertificationThresholdsDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

// ──────────────────────────────────────────────────────────────────────────
// Form schema — bounds mirror `CertificationThresholds.Create` factory and
// `UpdateCertificationThresholdsValidator`. Defense-in-depth: the same
// validation runs server-side; this just keeps invalid input from making the
// network round-trip and surfaces FluentValidation-style messages locally.
// ──────────────────────────────────────────────────────────────────────────

export const ThresholdsConfigFormSchema = z.object({
  minCoveragePct: z.coerce
    .number()
    .min(0, 'Minimum coverage must be between 0 and 100')
    .max(100, 'Minimum coverage must be between 0 and 100'),
  maxPageTolerance: z.coerce
    .number()
    .int('Maximum page tolerance must be an integer')
    .min(0, 'Maximum page tolerance must be at least 0'),
  minBggMatchPct: z.coerce
    .number()
    .min(0, 'Minimum BGG match must be between 0 and 100')
    .max(100, 'Minimum BGG match must be between 0 and 100'),
  minOverallScore: z.coerce
    .number()
    .min(0, 'Minimum overall score must be between 0 and 100')
    .max(100, 'Minimum overall score must be between 0 and 100'),
});

export type ThresholdsConfigFormData = z.infer<typeof ThresholdsConfigFormSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Component
// ──────────────────────────────────────────────────────────────────────────

export interface ThresholdsConfigFormProps {
  /** Current persisted thresholds — used to prefill the form and detect dirty state. */
  initial: CertificationThresholdsDto;
}

export function ThresholdsConfigForm({ initial }: ThresholdsConfigFormProps) {
  const updateMutation = useUpdateThresholds();

  const form = useForm<ThresholdsConfigFormData>({
    resolver: zodResolver(ThresholdsConfigFormSchema) as Resolver<ThresholdsConfigFormData>,
    defaultValues: {
      minCoveragePct: initial.minCoveragePct,
      maxPageTolerance: initial.maxPageTolerance,
      minBggMatchPct: initial.minBggMatchPct,
      minOverallScore: initial.minOverallScore,
    },
  });

  const isSubmitting = updateMutation.isPending;
  const isDirty = form.formState.isDirty;

  const onSubmit = form.handleSubmit(data => {
    updateMutation.mutate(data, {
      onSuccess: () => {
        // Reset the baseline so subsequent edits start a fresh dirty cycle.
        form.reset(data);
      },
    });
  });

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        <FormField
          control={form.control}
          name="minCoveragePct"
          render={({ field }) => (
            <FormItem>
              <FormLabel htmlFor="thresholds-min-coverage">Minimum coverage (%)</FormLabel>
              <FormControl>
                <Input
                  id="thresholds-min-coverage"
                  type="number"
                  min={0}
                  max={100}
                  step="any"
                  {...field}
                />
              </FormControl>
              <FormDescription>
                Minimum % of golden claims that must be matched for certification (0–100).
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="maxPageTolerance"
          render={({ field }) => (
            <FormItem>
              <FormLabel htmlFor="thresholds-max-page-tolerance">
                Maximum page tolerance (pages)
              </FormLabel>
              <FormControl>
                <Input
                  id="thresholds-max-page-tolerance"
                  type="number"
                  min={0}
                  step={1}
                  {...field}
                />
              </FormControl>
              <FormDescription>
                Allowed deviation between expected and observed page numbers (≥ 0). Zero means
                exact-page strict mode.
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="minBggMatchPct"
          render={({ field }) => (
            <FormItem>
              <FormLabel htmlFor="thresholds-min-bgg-match">Minimum BGG match (%)</FormLabel>
              <FormControl>
                <Input
                  id="thresholds-min-bgg-match"
                  type="number"
                  min={0}
                  max={100}
                  step="any"
                  {...field}
                />
              </FormControl>
              <FormDescription>
                Minimum % of expected BGG mechanic tags surfaced by the analysis (0–100).
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="minOverallScore"
          render={({ field }) => (
            <FormItem>
              <FormLabel htmlFor="thresholds-min-overall-score">
                Minimum overall score (%)
              </FormLabel>
              <FormControl>
                <Input
                  id="thresholds-min-overall-score"
                  type="number"
                  min={0}
                  max={100}
                  step="any"
                  {...field}
                />
              </FormControl>
              <FormDescription>
                Minimum aggregate score across coverage, page accuracy, and BGG match (0–100).
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="flex items-center justify-end gap-2 pt-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => form.reset()}
            disabled={isSubmitting || !isDirty}
          >
            Reset
          </Button>
          <Button type="submit" disabled={isSubmitting || !isDirty}>
            {isSubmitting && <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />}
            Save thresholds
          </Button>
        </div>
      </form>
    </Form>
  );
}
