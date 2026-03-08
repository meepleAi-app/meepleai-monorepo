import { z } from 'zod';

// ========== Mechanic Draft Schemas ==========

export const MechanicDraftDtoSchema = z.object({
  id: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  createdBy: z.string().uuid(),
  gameTitle: z.string(),
  summaryNotes: z.string(),
  mechanicsNotes: z.string(),
  victoryNotes: z.string(),
  resourcesNotes: z.string(),
  phasesNotes: z.string(),
  questionsNotes: z.string(),
  summaryDraft: z.string(),
  mechanicsDraft: z.string(),
  victoryDraft: z.string(),
  resourcesDraft: z.string(),
  phasesDraft: z.string(),
  questionsDraft: z.string(),
  createdAt: z.string(),
  lastModified: z.string(),
  status: z.string(),
});

export type MechanicDraftDto = z.infer<typeof MechanicDraftDtoSchema>;

export const AiAssistResultDtoSchema = z.object({
  section: z.string(),
  generatedDraft: z.string(),
});

export type AiAssistResultDto = z.infer<typeof AiAssistResultDtoSchema>;

// ========== Request Types ==========

export interface SaveMechanicDraftRequest {
  sharedGameId: string;
  pdfDocumentId: string;
  gameTitle: string;
  userId: string;
  summaryNotes: string;
  mechanicsNotes: string;
  victoryNotes: string;
  resourcesNotes: string;
  phasesNotes: string;
  questionsNotes: string;
}

export interface AiAssistRequest {
  draftId: string;
  section: string;
  humanNotes: string;
  gameTitle: string;
}

export interface AcceptDraftRequest {
  draftId: string;
  section: string;
  acceptedDraft: string;
}

export interface FinalizeRequest {
  draftId: string;
  userId: string;
}

// ========== Route Constants ==========

export const MECHANIC_EXTRACTOR_ROUTES = {
  draft: '/api/v1/admin/mechanic-extractor/draft',
  aiAssist: '/api/v1/admin/mechanic-extractor/ai-assist',
  acceptDraft: '/api/v1/admin/mechanic-extractor/accept-draft',
  finalize: '/api/v1/admin/mechanic-extractor/finalize',
} as const;
