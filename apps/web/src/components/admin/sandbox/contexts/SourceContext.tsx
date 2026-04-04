'use client';

import {
  createContext,
  useContext,
  useState,
  useCallback,
  useEffect,
  useRef,
  type ReactNode,
} from 'react';

import { api } from '@/lib/api';

export interface SharedGameSummary {
  id: string;
  title: string;
  publisher?: string;
  thumbnailUrl?: string;
  pdfCount: number;
  chunkCount: number;
  vectorCount: number;
}

export interface PdfDocumentSummary {
  id: string;
  fileName: string;
  fileSize: number;
  status: 'Pending' | 'Extracting' | 'Chunking' | 'Embedding' | 'Completed' | 'Failed';
  chunkCount: number;
  createdAt: string;
  updatedAt: string;
}

interface SourceContextValue {
  selectedGame: SharedGameSummary | null;
  setSelectedGame: (game: SharedGameSummary | null) => void;
  documents: PdfDocumentSummary[];
  setDocuments: (docs: PdfDocumentSummary[]) => void;
  refreshDocuments: () => void;
  isLoadingDocuments: boolean;
  setIsLoadingDocuments: (loading: boolean) => void;
  deletePdf: (pdfId: string) => Promise<boolean>;
}

const SourceContext = createContext<SourceContextValue | null>(null);

export function SourceProvider({ children }: { children: ReactNode }) {
  const [selectedGame, setSelectedGame] = useState<SharedGameSummary | null>(null);
  const [documents, setDocuments] = useState<PdfDocumentSummary[]>([]);
  const [isLoadingDocuments, setIsLoadingDocuments] = useState(false);
  const refreshCounterRef = useRef(0);

  // Fetch documents when game changes or refresh is triggered
  const fetchDocuments = useCallback(async (gameId: string) => {
    setIsLoadingDocuments(true);
    try {
      const docs = await api.sandbox.getDocumentsByGame(gameId);
      setDocuments(
        docs.map(d => ({
          id: d.id,
          fileName: d.originalFileName,
          fileSize: d.fileSizeBytes,
          status: mapProcessingState(d.processingState),
          chunkCount: d.chunkCount,
          createdAt: d.uploadedAt,
          updatedAt: d.processedAt || d.uploadedAt,
        }))
      );
    } catch {
      setDocuments([]);
    } finally {
      setIsLoadingDocuments(false);
    }
  }, []);

  // Auto-fetch documents when game selection changes
  useEffect(() => {
    if (selectedGame) {
      fetchDocuments(selectedGame.id);
    } else {
      setDocuments([]);
    }
  }, [selectedGame, fetchDocuments]);

  const refreshDocuments = useCallback(() => {
    if (selectedGame) {
      refreshCounterRef.current += 1;
      fetchDocuments(selectedGame.id);
    }
  }, [selectedGame, fetchDocuments]);

  const deletePdf = useCallback(async (pdfId: string): Promise<boolean> => {
    try {
      await api.sandbox.deletePdf(pdfId);
      setDocuments(prev => prev.filter(d => d.id !== pdfId));
      return true;
    } catch {
      return false;
    }
  }, []);

  return (
    <SourceContext.Provider
      value={{
        selectedGame,
        setSelectedGame,
        documents,
        setDocuments,
        refreshDocuments,
        isLoadingDocuments,
        setIsLoadingDocuments,
        deletePdf,
      }}
    >
      {children}
    </SourceContext.Provider>
  );
}

export function useSource() {
  const ctx = useContext(SourceContext);
  if (!ctx) throw new Error('useSource must be used within SourceProvider');
  return ctx;
}

function mapProcessingState(
  state: string
): 'Pending' | 'Extracting' | 'Chunking' | 'Embedding' | 'Completed' | 'Failed' {
  switch (state) {
    case 'Ready':
      return 'Completed';
    case 'Uploading':
    case 'Queued':
      return 'Pending';
    case 'Extracting':
      return 'Extracting';
    case 'Chunking':
      return 'Chunking';
    case 'Embedding':
      return 'Embedding';
    case 'Failed':
      return 'Failed';
    default:
      return 'Pending';
  }
}
