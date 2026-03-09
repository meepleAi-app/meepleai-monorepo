import React from 'react';

import type { KbDetailData } from '../types';

// ============================================================================
// KB Detail Hook (Issue #5028)
// ============================================================================

interface UseKbDetailResult {
  data: KbDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function useKbDetail(pdfId: string): UseKbDetailResult {
  const [data, setData] = React.useState<KbDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const loadKbDetail = React.useCallback(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);

    const load = async () => {
      try {
        const res = await fetch(`/api/v1/pdfs/${pdfId}/text`, {
          signal: controller.signal,
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const doc = (await res.json()) as Record<string, unknown>;
        setData(mapToKbDetailData(doc));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : 'Errore caricamento documento');
      } finally {
        setLoading(false);
      }
    };

    void load();
    return () => controller.abort();
  }, [pdfId]);

  React.useEffect(() => {
    const cleanup = loadKbDetail();
    return cleanup;
  }, [loadKbDetail]);

  return { data, loading, error, retry: loadKbDetail };
}

function mapToKbDetailData(doc: Record<string, unknown>): KbDetailData {
  const rawStatus = String(doc.processingStatus ?? 'none').toLowerCase();
  const statusMap: Record<string, KbDetailData['status']> = {
    uploaded: 'processing',
    extracting: 'processing',
    indexing: 'processing',
    indexed: 'indexed',
    failed: 'failed',
    none: 'none',
  };
  const status = statusMap[rawStatus] ?? 'none';

  const extractedText = doc.extractedText != null ? String(doc.extractedText) : undefined;
  const MAX_CHARS = 2000; // ~500 words
  const extractedContent = extractedText ? extractedText.slice(0, MAX_CHARS) : undefined;
  const hasMoreContent = extractedText ? extractedText.length > MAX_CHARS : false;

  return {
    id: String(doc.id ?? ''),
    fileName: String(doc.fileName ?? 'Documento'),
    fileSize: doc.fileSize != null ? Number(doc.fileSize) : undefined,
    pageCount: doc.pageCount != null ? Number(doc.pageCount) : undefined,
    characterCount: doc.characterCount != null ? Number(doc.characterCount) : undefined,
    uploadedAt: doc.uploadedAt != null ? String(doc.uploadedAt) : undefined,
    processedAt: doc.processedAt != null ? String(doc.processedAt) : undefined,
    status,
    errorMessage: doc.processingError != null ? String(doc.processingError) : undefined,
    extractedContent,
    hasMoreContent,
  };
}
