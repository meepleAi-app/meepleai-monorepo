import { useCallback, useEffect, useState } from 'react';

interface PdfDocument {
  id: string;
  fileName: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string;
  language?: string | null;
  status?: string | null;
  logUrl?: string | null;
}

interface PdfListResponse {
  pdfs?: PdfDocument[];
}

/**
 * usePdfs - PDF documents fetching and management
 *
 * Features:
 * - Fetch PDFs for a game
 * - Loading and error states
 * - Automatic refresh on game change
 * - Manual refetch capability
 */
export function usePdfs(gameId: string | null) {
  const [pdfs, setPdfs] = useState<PdfDocument[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';

  const fetchPdfs = useCallback(async () => {
    if (!gameId) {
      setPdfs([]);
      setError(null);
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await fetch(`${API_BASE}/api/v1/games/${gameId}/pdfs`, {
        credentials: 'include'
      });

      if (!response.ok) {
        console.error('Failed to load PDFs:', response.statusText);
        setError('Unable to load uploaded PDFs. Please try again.');
        return;
      }

      const data: PdfListResponse = await response.json();
      setPdfs(data.pdfs ?? []);
    } catch (err) {
      console.error('Failed to load PDFs:', err);
      setError('Unable to load uploaded PDFs. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [gameId, API_BASE]);

  useEffect(() => {
    void fetchPdfs();
  }, [fetchPdfs]);

  return {
    pdfs,
    loading,
    error,
    refetch: fetchPdfs
  };
}
