/**
 * Cost estimation for the embedding step of an ingestion job.
 * Pure FE heuristic — the backend doesn't currently persist real token counts
 * in `ProcessingStep.MetadataJson` for the Embed step (verified 2026-05-29).
 *
 * Formula: chunkCount × AVG_TOKENS_PER_CHUNK × pricePerToken(model)
 *
 * `AVG_TOKENS_PER_CHUNK` is a configured constant (512), matching the
 * sentence-based chunker default. When the backend starts populating real
 * token counts (follow-up #1653), swap this for the real metadata field.
 * Issue #1650.
 */

const AVG_TOKENS_PER_CHUNK = 512;

interface ModelPricing {
  readonly pricePerToken: number; // 0 for self-hosted
  readonly note: string; // human-readable formula descriptor
}

const KNOWN_MODELS: Record<string, ModelPricing> = {
  'bge-base-en-v1.5': { pricePerToken: 0, note: 'self-hosted' },
  'text-embedding-3-small': {
    pricePerToken: 2e-8, // $0.02 / 1M tokens (OpenAI 2024-12 list)
    note: '×',
  },
};

export interface CostEstimate {
  readonly value: number;
  readonly model: string;
  readonly formula: string;
}

export function estimateCost(chunkCount: number, model: string): CostEstimate | null {
  const pricing = KNOWN_MODELS[model];
  if (!pricing) return null;

  if (pricing.pricePerToken === 0) {
    return {
      value: 0,
      model,
      formula: 'self-hosted',
    };
  }

  const value = chunkCount * AVG_TOKENS_PER_CHUNK * pricing.pricePerToken;
  return {
    value,
    model,
    formula: `${chunkCount} × ${AVG_TOKENS_PER_CHUNK} ${pricing.note} $${pricing.pricePerToken.toExponential(2)}`,
  };
}
