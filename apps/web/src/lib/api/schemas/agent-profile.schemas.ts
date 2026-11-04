/**
 * #4169 (ADR-095 D8, fetta 6c): il profilo dell'agente per la pagina admin «Agente».
 *
 * Rispecchia `AgentProfileAdminDto` del backend (`GET /api/v1/admin/agent-profile`). Gli enum
 * arrivano come stringhe (JsonStringEnumConverter). Stati, stili di citazione e categorie restano
 * stringhe aperte: un valore nuovo del backend non deve far fallire la lettura dell'intera pagina.
 */

import { z } from 'zod';

export const AgentProfileSessionAssistantSchema = z.object({
  enabled: z.boolean(),
  useReserveModel: z.boolean(),
  promptLanguages: z.array(z.string()),
  temperature: z.number(),
  maxResponseTokens: z.number().int(),
});

export const AgentProfileContentSchema = z.object({
  // Identità
  name: z.string(),
  description: z.string(),
  persona: z.string(),
  // Modello
  primaryModelId: z.string(),
  fallbackModelId: z.string().nullable(),
  // Generazione
  temperature: z.number(),
  maxResponseTokens: z.number().int(),
  citationStyle: z.string(),
  // Prompt
  systemPrompts: z.record(z.string(), z.string()),
  fallbackLanguage: z.string(),
  // Recupero (in sola lettura fino alla fetta 7)
  candidatePoolSize: z.number().int(),
  finalTopK: z.number().int(),
  minScore: z.number(),
  vectorWeight: z.number(),
  keywordWeight: z.number(),
  rerankerEnabled: z.boolean(),
  // Fonti (in sola lettura fino alla fetta 7)
  allowedCategories: z.array(z.string()),
  // Assistente in partita (in sola lettura)
  sessionAssistant: AgentProfileSessionAssistantSchema,
});

export type AgentProfileContent = z.infer<typeof AgentProfileContentSchema>;

export const AgentProfileVersionSchema = z.object({
  id: z.string(),
  versionNumber: z.number().int(),
  status: z.string(),
  createdAt: z.string(),
  publishedAt: z.string().nullable(),
  archivedAt: z.string().nullable(),
  content: AgentProfileContentSchema,
});

export type AgentProfileVersion = z.infer<typeof AgentProfileVersionSchema>;

export const AgentProfileVersionSummarySchema = z.object({
  id: z.string(),
  versionNumber: z.number().int(),
  status: z.string(),
  createdAt: z.string(),
  publishedAt: z.string().nullable(),
  archivedAt: z.string().nullable(),
});

export type AgentProfileVersionSummary = z.infer<typeof AgentProfileVersionSummarySchema>;

export const AgentProfileAdminSchema = z.object({
  published: AgentProfileVersionSchema,
  draft: AgentProfileVersionSchema.nullable(),
  versions: z.array(AgentProfileVersionSummarySchema),
});

export type AgentProfileAdmin = z.infer<typeof AgentProfileAdminSchema>;

/**
 * Il corpo di `PUT /api/v1/admin/agent-profile/draft`: solo le sezioni che la pagina modifica.
 * Recupero, Fonti e l'assistente in partita non ci sono: il backend li conserva.
 */
export interface SaveAgentProfileDraftRequest {
  name: string;
  description: string;
  persona: string;
  primaryModelId: string;
  fallbackModelId: string | null;
  temperature: number;
  maxResponseTokens: number;
  citationStyle: string;
  systemPrompts: Record<string, string>;
  fallbackLanguage: string;
}

/** Il codice del 409 quando un modello del profilo non ha un prezzo nel catalogo (ADR-095 D5). */
export const MODEL_WITHOUT_PRICE_CODE = 'model_without_price';
