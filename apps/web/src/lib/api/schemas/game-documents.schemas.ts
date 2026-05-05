import { z } from 'zod';

export const DocumentCategorySchema = z.enum([
  'Rulebook',
  'Expansion',
  'Errata',
  'QuickStart',
  'Reference',
  'PlayerAid',
  'Other',
]);

export type DocumentCategory = z.infer<typeof DocumentCategorySchema>;

export const GameDocumentSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  status: z.enum(['indexed', 'processing', 'failed']),
  pageCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime({ offset: true }),
  category: DocumentCategorySchema,
  versionLabel: z.string().nullable(),
});

export type GameDocument = z.infer<typeof GameDocumentSchema>;

export const RULEBOOK_CATEGORIES: DocumentCategory[] = ['Rulebook'];

export function isRulebook(doc: GameDocument): boolean {
  return RULEBOOK_CATEGORIES.includes(doc.category);
}
