/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 36)
 *
 * Curator form for creating/editing a golden claim attached to a SharedGame.
 * Wraps `useCreateGoldenClaim` (mode='create') and `useUpdateGoldenClaim` (mode='edit').
 *
 * Backend authoritative source for field constraints:
 *   apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/GoldenClaim*.cs
 */

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2Icon } from 'lucide-react';
import { useForm, Controller, type Resolver } from 'react-hook-form';
import { z } from 'zod';

import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/forms/form';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useCreateGoldenClaim } from '@/hooks/admin/useCreateGoldenClaim';
import { useUpdateGoldenClaim } from '@/hooks/admin/useUpdateGoldenClaim';
import {
  MechanicSectionSchema,
  type MechanicGoldenClaimDto,
  type MechanicSection,
} from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

// ──────────────────────────────────────────────────────────────────────────
// Form schema (UI-only — wider than backend DTO so we can carry displayOrder
// even though the API contract does not currently expose it on create/update).
// ──────────────────────────────────────────────────────────────────────────

export const GoldenClaimFormSchema = z.object({
  section: MechanicSectionSchema,
  statement: z
    .string()
    .min(10, 'Statement must be at least 10 characters')
    .max(1000, 'Statement must be at most 1000 characters'),
  expectedPage: z.coerce
    .number()
    .int('Expected page must be an integer')
    .min(1, 'Expected page must be at least 1'),
  sourceQuote: z
    .string()
    .min(10, 'Source quote must be at least 10 characters')
    .max(2000, 'Source quote must be at most 2000 characters'),
  displayOrder: z.coerce.number().int('Display order must be an integer').optional().default(0),
});

export type GoldenClaimFormData = z.infer<typeof GoldenClaimFormSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Section options (mirror MechanicSectionSchema enum)
// ──────────────────────────────────────────────────────────────────────────

const SECTION_OPTIONS: ReadonlyArray<{ value: MechanicSection; label: string }> = [
  { value: 'Summary', label: 'Summary' },
  { value: 'Mechanics', label: 'Mechanics' },
  { value: 'Victory', label: 'Victory' },
  { value: 'Resources', label: 'Resources' },
  { value: 'Phases', label: 'Phases' },
  { value: 'Faq', label: 'FAQ' },
];

// ──────────────────────────────────────────────────────────────────────────
// Component
// ──────────────────────────────────────────────────────────────────────────

export interface GoldenClaimFormProps {
  /** Shared game the claim is attached to (always required, including edit). */
  sharedGameId: string;
  /** Form mode — `create` clears state on success, `edit` calls `onClose`. */
  mode: 'create' | 'edit';
  /** When `mode === 'edit'`, the original DTO to prefill the form from. */
  initialClaim?: MechanicGoldenClaimDto;
  /** When `mode === 'edit'`, the ID of the claim being edited (PUT target). */
  claimId?: string;
  /** Optional callback invoked after a successful submit (used by edit dialog). */
  onClose?: () => void;
}

export function GoldenClaimForm({
  sharedGameId,
  mode,
  initialClaim,
  claimId,
  onClose,
}: GoldenClaimFormProps) {
  const createMutation = useCreateGoldenClaim();
  const updateMutation = useUpdateGoldenClaim();

  const form = useForm<GoldenClaimFormData>({
    resolver: zodResolver(GoldenClaimFormSchema) as Resolver<GoldenClaimFormData>,
    defaultValues: initialClaim
      ? {
          section: initialClaim.section,
          statement: initialClaim.statement,
          expectedPage: initialClaim.expectedPage,
          sourceQuote: initialClaim.sourceQuote,
          displayOrder: 0,
        }
      : {
          section: 'Mechanics' as MechanicSection,
          statement: '',
          expectedPage: 1,
          sourceQuote: '',
          displayOrder: 0,
        },
  });

  const isSubmitting = createMutation.isPending || updateMutation.isPending;

  const onSubmit = form.handleSubmit(data => {
    if (mode === 'edit') {
      if (!claimId) return;
      updateMutation.mutate(
        {
          sharedGameId,
          claimId,
          request: {
            statement: data.statement,
            expectedPage: data.expectedPage,
            sourceQuote: data.sourceQuote,
          },
        },
        {
          onSuccess: () => {
            onClose?.();
          },
        }
      );
      return;
    }

    createMutation.mutate(
      {
        sharedGameId,
        request: {
          sharedGameId,
          section: data.section,
          statement: data.statement,
          expectedPage: data.expectedPage,
          sourceQuote: data.sourceQuote,
        },
      },
      {
        onSuccess: () => {
          form.reset({
            section: data.section,
            statement: '',
            expectedPage: 1,
            sourceQuote: '',
            displayOrder: 0,
          });
        },
      }
    );
  });

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {/* Section (immutable in edit mode) */}
        <FormField
          control={form.control}
          name="section"
          render={({ field }) => (
            <FormItem>
              <FormLabel htmlFor="golden-claim-section">Section *</FormLabel>
              <FormControl>
                <Controller
                  control={form.control}
                  name="section"
                  render={({ field: ctrl }) => (
                    <Select
                      value={ctrl.value}
                      onValueChange={ctrl.onChange}
                      disabled={mode === 'edit'}
                    >
                      <SelectTrigger id="golden-claim-section">
                        <SelectValue placeholder="Select a section" />
                      </SelectTrigger>
                      <SelectContent>
                        {SECTION_OPTIONS.map(opt => (
                          <SelectItem key={opt.value} value={opt.value}>
                            {opt.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
              </FormControl>
              <FormMessage />
              {mode === 'edit' && (
                <p className="text-xs text-muted-foreground">
                  Section is immutable after creation.
                </p>
              )}
              {/* keep field.name referenced so RHF wires aria-* correctly */}
              <input type="hidden" name={field.name} value={field.value} />
            </FormItem>
          )}
        />

        {/* Statement */}
        <FormField
          control={form.control}
          name="statement"
          render={({ field }) => (
            <FormItem>
              <FormLabel htmlFor="golden-claim-statement">Statement *</FormLabel>
              <FormControl>
                <Textarea
                  id="golden-claim-statement"
                  rows={3}
                  placeholder="A factual claim derived from the rulebook (10–1000 chars)…"
                  {...field}
                />
              </FormControl>
              <FormMessage />
              <p className="text-xs text-muted-foreground">{field.value?.length ?? 0} / 1000</p>
            </FormItem>
          )}
        />

        {/* Expected page */}
        <FormField
          control={form.control}
          name="expectedPage"
          render={({ field }) => (
            <FormItem>
              <FormLabel htmlFor="golden-claim-expected-page">Expected page *</FormLabel>
              <FormControl>
                <Input id="golden-claim-expected-page" type="number" min={1} step={1} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        {/* Source quote */}
        <FormField
          control={form.control}
          name="sourceQuote"
          render={({ field }) => (
            <FormItem>
              <FormLabel htmlFor="golden-claim-source-quote">Source quote *</FormLabel>
              <FormControl>
                <Textarea
                  id="golden-claim-source-quote"
                  rows={3}
                  placeholder="Verbatim excerpt from the rulebook (10–2000 chars)…"
                  {...field}
                />
              </FormControl>
              <FormMessage />
              <p className="text-xs text-muted-foreground">{field.value?.length ?? 0} / 2000</p>
            </FormItem>
          )}
        />

        {/* Display order (create mode only — edit endpoint does not accept it) */}
        {mode === 'create' && (
          <FormField
            control={form.control}
            name="displayOrder"
            render={({ field }) => (
              <FormItem>
                <FormLabel htmlFor="golden-claim-display-order">Display order</FormLabel>
                <FormControl>
                  <Input
                    id="golden-claim-display-order"
                    type="number"
                    step={1}
                    {...field}
                    value={field.value ?? 0}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        )}

        {/* Actions */}
        <div className="flex items-center justify-end gap-2 pt-2">
          {mode === 'edit' && onClose && (
            <Button type="button" variant="outline" onClick={onClose} disabled={isSubmitting}>
              Cancel
            </Button>
          )}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting && <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />}
            {mode === 'edit' ? 'Save changes' : 'Create claim'}
          </Button>
        </div>
      </form>
    </Form>
  );
}
