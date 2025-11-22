/**
 * PDF & Document Processing API Schemas (FE-IMP-005)
 *
 * Zod schemas for validating DocumentProcessing bounded context responses.
 * Covers: PDF upload, processing progress, document metadata
 */

import { z } from 'zod';

// ========== PDF Document ==========

export const PdfDocumentDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  fileName: z.string().min(1),
  filePath: z.string().min(1),
  fileSizeBytes: z.number().int().nonnegative(),
  processingStatus: z.string().min(1),
  uploadedAt: z.string().datetime(),
  processedAt: z.string().datetime().nullable(),
  pageCount: z.number().int().positive().nullable(),
});

export type PdfDocumentDto = z.infer<typeof PdfDocumentDtoSchema>;

// ========== Processing Progress ==========

export const ProcessingProgressSchema = z.object({
  status: z.enum(['Pending', 'Processing', 'Completed', 'Failed']),
  currentPage: z.number().int().nonnegative().optional(),
  totalPages: z.number().int().positive().optional(),
  percentComplete: z.number().min(0).max(100),
  message: z.string().optional(),
  error: z.string().nullable().optional(),
});

export type ProcessingProgress = z.infer<typeof ProcessingProgressSchema>;
