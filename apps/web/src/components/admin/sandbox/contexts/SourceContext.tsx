'use client';

import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';

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
}

const SourceContext = createContext<SourceContextValue | null>(null);

export function SourceProvider({ children }: { children: ReactNode }) {
  const [selectedGame, setSelectedGame] = useState<SharedGameSummary | null>(null);
  const [documents, setDocuments] = useState<PdfDocumentSummary[]>([]);
  const [isLoadingDocuments, setIsLoadingDocuments] = useState(false);

  const refreshDocuments = useCallback(() => {
    setIsLoadingDocuments(true);
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
