/**
 * Document Collection Schemas
 *
 * Issue #2051: Multi-document upload API schemas
 *
 * Zod validation schemas for document collection operations
 */

import { z } from 'zod';

/**
 * Document types supported in the system
 */
export const documentTypeSchema = z.enum(['base', 'expansion', 'errata', 'homerule']);
export type DocumentType = z.infer<typeof documentTypeSchema>;

/**
 * Document metadata within a collection
 */
export const documentMetadataSchema = z.object({
  pdfDocumentId: z.string().uuid(),
  documentType: documentTypeSchema,
  sortOrder: z.number().int().min(0),
});
export type DocumentMetadata = z.infer<typeof documentMetadataSchema>;

/**
 * Create document collection request
 */
export const createDocumentCollectionRequestSchema = z.object({
  name: z.string().min(1).max(200),
  description: z.string().max(1000).optional(),
  initialDocuments: z.array(documentMetadataSchema).max(5),
});
export type CreateDocumentCollectionRequest = z.infer<typeof createDocumentCollectionRequestSchema>;

/**
 * Document collection response
 */
export const documentCollectionSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  name: z.string(),
  description: z.string().nullable(),
  documentCount: z.number().int().min(0),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});
export type DocumentCollection = z.infer<typeof documentCollectionSchema>;

/**
 * Document within a collection (detailed response)
 */
export const collectionDocumentSchema = z.object({
  id: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  documentType: documentTypeSchema,
  sortOrder: z.number().int().min(0),
  fileName: z.string(),
  uploadedAt: z.string().datetime(),
});
export type CollectionDocument = z.infer<typeof collectionDocumentSchema>;

/**
 * Add document to collection request
 */
export const addDocumentToCollectionRequestSchema = z.object({
  pdfDocumentId: z.string().uuid(),
  documentType: documentTypeSchema,
  sortOrder: z.number().int().min(0).optional(),
});
export type AddDocumentToCollectionRequest = z.infer<typeof addDocumentToCollectionRequestSchema>;

/**
 * Reorder document request
 */
export const reorderDocumentRequestSchema = z.object({
  newSortOrder: z.number().int().min(0),
});
export type ReorderDocumentRequest = z.infer<typeof reorderDocumentRequestSchema>;

/**
 * Update collection request
 */
export const updateDocumentCollectionRequestSchema = z.object({
  name: z.string().min(1).max(200).optional(),
  description: z.string().max(1000).optional().nullable(),
});
export type UpdateDocumentCollectionRequest = z.infer<typeof updateDocumentCollectionRequestSchema>;
