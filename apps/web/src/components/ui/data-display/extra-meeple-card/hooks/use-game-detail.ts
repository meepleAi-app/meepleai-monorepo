import React from 'react';

import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

import { mapProcessingStateToStatus, mapRawToPdfDocumentDto } from '../drawer-helpers';

import type { GameDetailData, KbDocumentPreview, GameAgentPreview } from '../types';

// ============================================================================
// Data Fetching Hook — Game
// ============================================================================

interface UseGameDetailResult {
  data: GameDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function useGameDetail(gameId: string): UseGameDetailResult {
  const [data, setData] = React.useState<GameDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        // Fetch game details, full PDF list (with processing state), and agent config in parallel.
        // KB: /api/v1/games/{id}/pdfs returns { pdfs: PdfDocumentDto[] } with processingState.
        // Agent: /agent-config returns AgentConfigDto? (null when not configured).
        // Both handled gracefully on non-200. (Issue #5029, #5195)
        const [gameRes, kbRes, agentRes] = await Promise.all([
          fetch(`/api/v1/library/games/${gameId}`, { signal }),
          fetch(`/api/v1/games/${gameId}/pdfs`, { signal }),
          fetch(`/api/v1/library/games/${gameId}/agent-config`, { signal }),
        ]);

        if (!gameRes.ok) {
          throw new Error(`Errore ${gameRes.status}: gioco non trovato`);
        }

        const json = (await gameRes.json()) as Record<string, unknown>;
        const kbRawJson = kbRes.ok ? ((await kbRes.json()) as unknown) : null;
        // /api/v1/games/{id}/pdfs returns { pdfs: [...] } shape
        const kbPdfsRaw: unknown[] =
          kbRawJson != null
            ? Array.isArray(kbRawJson)
              ? kbRawJson
              : ((kbRawJson as { pdfs?: unknown[] }).pdfs ?? [])
            : [];
        const agentJson = agentRes.ok ? ((await agentRes.json()) as Record<string, unknown>) : null;

        setData(mapToGameDetailData(json, kbPdfsRaw, agentJson));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : 'Impossibile caricare i dati del gioco');
      } finally {
        setLoading(false);
      }
    },
    [gameId]
  );

  React.useEffect(() => {
    if (!gameId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => {
      controller.abort();
    };
  }, [gameId, fetchData]);

  // retry creates a fresh controller each time
  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToGameDetailData(
  json: Record<string, unknown>,
  kbRaw: unknown[] = [],
  agentRaw: Record<string, unknown> | null = null
): GameDetailData {
  // Map full PdfDocumentDto data (Issue #5195: from /api/v1/games/{gameId}/pdfs)
  const pdfDocuments: PdfDocumentDto[] = kbRaw.map(item =>
    mapRawToPdfDocumentDto(item as Record<string, unknown>)
  );

  // KbDocumentPreview for simpler consumers — derive status from processingState
  const kbDocuments: KbDocumentPreview[] = pdfDocuments.map(pdf => ({
    id: pdf.id,
    fileName: pdf.fileName,
    uploadedAt: pdf.uploadedAt,
    status: mapProcessingStateToStatus(pdf.processingState),
  }));

  // AgentConfigDto shape: { llmModel, temperature, maxTokens, personality, detailLevel, personalNotes }
  // No identity fields (id/name/isActive) — use placeholder values when config exists.
  const agent: GameAgentPreview | undefined =
    agentRaw != null
      ? {
          id: '',
          name: 'Agente AI',
          model: agentRaw.llmModel != null ? String(agentRaw.llmModel) : undefined,
          isActive: true,
        }
      : undefined;

  return {
    id: String(json.id ?? ''),
    title: String(json.title ?? json.name ?? ''),
    imageUrl: json.imageUrl != null ? String(json.imageUrl) : undefined,
    publisher: json.publisher != null ? String(json.publisher) : undefined,
    yearPublished: json.yearPublished != null ? Number(json.yearPublished) : undefined,
    minPlayers: json.minPlayers != null ? Number(json.minPlayers) : undefined,
    maxPlayers: json.maxPlayers != null ? Number(json.maxPlayers) : undefined,
    playTimeMinutes: json.playTimeMinutes != null ? Number(json.playTimeMinutes) : undefined,
    description: json.description != null ? String(json.description) : undefined,
    averageRating: json.averageRating != null ? Number(json.averageRating) : undefined,
    totalPlays: json.totalPlays != null ? Number(json.totalPlays) : undefined,
    faqCount: json.faqCount != null ? Number(json.faqCount) : undefined,
    rulesDocumentCount:
      json.rulesDocumentCount != null ? Number(json.rulesDocumentCount) : undefined,
    kbDocuments: kbDocuments.length > 0 ? kbDocuments : undefined,
    pdfDocuments: pdfDocuments.length > 0 ? pdfDocuments : undefined,
    agent,
  };
}
