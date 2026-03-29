/**
 * Agent Documents Schemas
 *
 * Zod schemas for user-scoped agent document selection endpoints.
 * Used by the library game agent document panel to show available
 * documents and allow per-user selection.
 */

import { z } from 'zod';

export const DocumentSelectionItemSchema = z.object({
  documentId: z.string(),
  fileName: z.string(),
  documentType: z.string(),
  processingState: z.string(),
  isPrivate: z.boolean(),
  isSelected: z.boolean(),
  pageCount: z.number().nullable(),
});

export const AvailableDocumentsSchema = z.object({
  agentId: z.string().nullable(),
  baseDocuments: z.array(DocumentSelectionItemSchema),
  additionalDocuments: z.array(DocumentSelectionItemSchema),
});

export type DocumentSelectionItem = z.infer<typeof DocumentSelectionItemSchema>;
export type AvailableDocuments = z.infer<typeof AvailableDocumentsSchema>;
