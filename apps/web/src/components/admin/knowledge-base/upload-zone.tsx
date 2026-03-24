'use client';

import { useState, useRef, useCallback, useEffect } from 'react';

import {
  UploadCloudIcon,
  FileTextIcon,
  XIcon,
  CheckCircleIcon,
  AlertCircleIcon,
  LoaderIcon,
  SearchIcon,
  ZapIcon,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import {
  enqueuePdf,
  PRIORITY_NORMAL,
  PRIORITY_URGENT,
} from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useApiClient } from '@/lib/api/context';
import { useGameSearch } from '@/lib/domain-hooks/useGameSearch';

// ── Types ──────────────────────────────────────────────────────────────

interface FileUploadState {
  file: File;
  progress: number;
  status: 'pending' | 'uploading' | 'processing' | 'completed' | 'error';
  error?: string;
  documentId?: string;
}

const MAX_SINGLE_UPLOAD_SIZE = 10 * 1024 * 1024; // 10MB threshold for chunked upload
const ACCEPTED_TYPES = ['application/pdf'];
const MAX_FILE_SIZE = 500 * 1024 * 1024; // 500MB
const MAX_CONCURRENT_UPLOADS = 3;

// ── Component ──────────────────────────────────────────────────────────

export function UploadZone({ initialGameId }: { initialGameId?: string } = {}) {
  const api = useApiClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const preSelectedRef = useRef(false);

  // Game search state
  const [gameQuery, setGameQuery] = useState('');
  const [selectedGame, setSelectedGame] = useState<{ id: string; name: string } | null>(null);
  const [showGameDropdown, setShowGameDropdown] = useState(false);
  const { data: gameResults = [], isLoading: isSearching } = useGameSearch(gameQuery);

  // Upload state
  const [uploads, setUploads] = useState<FileUploadState[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const [urgentPriority, setUrgentPriority] = useState(false);
  const router = useRouter();

  // ── Validation ──────────────────────────────────────────────────────

  const validateFile = useCallback((file: File): string | null => {
    if (!ACCEPTED_TYPES.includes(file.type) && !file.name.toLowerCase().endsWith('.pdf')) {
      return 'Only PDF files are supported';
    }
    if (file.size > MAX_FILE_SIZE) {
      return `File too large (max ${MAX_FILE_SIZE / (1024 * 1024)}MB)`;
    }
    return null;
  }, []);

  // ── Upload Logic ────────────────────────────────────────────────────

  const updateUpload = useCallback((index: number, update: Partial<FileUploadState>) => {
    setUploads(prev => prev.map((u, i) => (i === index ? { ...u, ...update } : u)));
  }, []);

  const handleEnqueueError = useCallback(
    (documentId: string, error: unknown) => {
      toast.error('PDF caricato ma non accodato', {
        description: error instanceof Error ? error.message : "Errore durante l'accodamento",
      });
      setTimeout(() => {
        router.push(`/admin/knowledge-base/documents?highlight=${documentId}`);
      }, 2000);
    },
    [router]
  );

  const uploadSingleFile = useCallback(
    async (file: File, index: number) => {
      if (!selectedGame) return;
      try {
        updateUpload(index, { status: 'uploading', progress: 0 });

        const result = await api.pdf.uploadPdf(selectedGame.id, file, percent =>
          updateUpload(index, { progress: percent })
        );

        updateUpload(index, { status: 'processing', progress: 100, documentId: result.documentId });

        // Auto-enqueue for processing (Urgent=30 puts at head, Normal=10 default)
        try {
          await enqueuePdf(result.documentId, urgentPriority ? PRIORITY_URGENT : PRIORITY_NORMAL);
        } catch (enqueueErr) {
          handleEnqueueError(result.documentId, enqueueErr);
        }

        updateUpload(index, { status: 'completed' });
      } catch (err) {
        updateUpload(index, {
          status: 'error',
          error: err instanceof Error ? err.message : 'Upload failed',
        });
      }
    },
    [api.pdf, selectedGame, updateUpload, urgentPriority, handleEnqueueError]
  );

  const uploadChunkedFile = useCallback(
    async (file: File, index: number) => {
      if (!selectedGame) return;
      try {
        updateUpload(index, { status: 'uploading', progress: 0 });

        // Step 1: Initialize
        const initResult = await api.pdf.initChunkedUpload(selectedGame.id, file.name, file.size);

        if (!initResult.sessionId) {
          throw new Error('Failed to initialize chunked upload');
        }

        const { sessionId, totalChunks, chunkSizeBytes } = initResult;

        // Step 2: Upload chunks
        for (let i = 0; i < totalChunks; i++) {
          const start = i * chunkSizeBytes;
          const end = Math.min(start + chunkSizeBytes, file.size);
          const chunk = file.slice(start, end);

          await api.pdf.uploadChunk(sessionId, i, chunk);

          const progress = Math.round(((i + 1) / totalChunks) * 95); // Reserve 5% for completion
          updateUpload(index, { progress });
        }

        // Step 3: Complete
        updateUpload(index, { status: 'processing', progress: 95 });
        const completeResult = await api.pdf.completeChunkedUpload(sessionId);

        if (!completeResult.success) {
          throw new Error('Failed to assemble chunks');
        }

        updateUpload(index, { progress: 100, documentId: completeResult.documentId });

        // Auto-enqueue (Urgent=30 puts at head, Normal=10 default)
        try {
          await enqueuePdf(
            completeResult.documentId,
            urgentPriority ? PRIORITY_URGENT : PRIORITY_NORMAL
          );
        } catch (enqueueErr) {
          handleEnqueueError(completeResult.documentId, enqueueErr);
        }

        updateUpload(index, { status: 'completed' });
      } catch (err) {
        updateUpload(index, {
          status: 'error',
          error: err instanceof Error ? err.message : 'Upload failed',
        });
      }
    },
    [api.pdf, selectedGame, updateUpload, urgentPriority, handleEnqueueError]
  );

  const startUpload = useCallback(
    async (files: File[]) => {
      if (!selectedGame) return;

      const validFiles: FileUploadState[] = [];
      for (const file of files) {
        const error = validateFile(file);
        if (error) {
          validFiles.push({ file, progress: 0, status: 'error', error });
        } else {
          validFiles.push({ file, progress: 0, status: 'pending' });
        }
      }

      const startIndex = uploads.length;
      setUploads(prev => [...prev, ...validFiles]);

      // Upload files with concurrency limit
      const uploadable = validFiles
        .map((vf, i) => ({ vf, idx: startIndex + i }))
        .filter(({ vf }) => vf.status !== 'error');

      const queue = [...uploadable];
      const runNext = async (): Promise<void> => {
        const item = queue.shift();
        if (!item) return;
        const { vf, idx } = item;
        if (vf.file.size > MAX_SINGLE_UPLOAD_SIZE) {
          await uploadChunkedFile(vf.file, idx);
        } else {
          await uploadSingleFile(vf.file, idx);
        }
        await runNext();
      };

      // Start up to MAX_CONCURRENT_UPLOADS workers
      const workers = Array.from(
        { length: Math.min(MAX_CONCURRENT_UPLOADS, uploadable.length) },
        () => runNext()
      );
      await Promise.all(workers);
    },
    [selectedGame, uploads.length, validateFile, uploadSingleFile, uploadChunkedFile]
  );

  // ── Handlers ────────────────────────────────────────────────────────

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      if (!selectedGame) return;

      const files = Array.from(e.dataTransfer.files);
      if (files.length > 0) startUpload(files);
    },
    [selectedGame, startUpload]
  );

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const files = Array.from(e.target.files ?? []);
      if (files.length > 0) startUpload(files);
      // Reset input so re-selecting the same file works
      e.target.value = '';
    },
    [startUpload]
  );

  const removeUpload = useCallback((index: number) => {
    setUploads(prev => prev.filter((_, i) => i !== index));
  }, []);

  const selectGame = useCallback((game: { id: string; name: string }) => {
    setSelectedGame(game);
    setGameQuery(game.name);
    setShowGameDropdown(false);
  }, []);

  // Pre-select game when navigating from shared game detail page
  useEffect(() => {
    if (!initialGameId || preSelectedRef.current) return;
    preSelectedRef.current = true;
    const baseUrl = process.env.NEXT_PUBLIC_API_BASE ?? 'http://localhost:8080';
    fetch(`${baseUrl}/api/v1/shared-games/${initialGameId}`, { credentials: 'include' })
      .then(r => (r.ok ? r.json() : null))
      .then((data: { id?: unknown; title?: unknown; name?: unknown } | null) => {
        if (data?.id && (data.title ?? data.name)) {
          selectGame({ id: String(data.id), name: String(data.title ?? data.name) });
        }
      })
      .catch(() => {});
  }, [initialGameId, selectGame]);

  // ── Render ──────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      {/* Game Selector */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-4 border border-slate-200/50 dark:border-zinc-700/50">
        <label className="block text-sm font-medium text-slate-700 dark:text-zinc-300 mb-2">
          Seleziona Gioco <span className="text-red-500">*</span>
        </label>
        <div className="relative">
          <div className="relative">
            <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <Input
              placeholder="Cerca giochi..."
              value={gameQuery}
              onChange={e => {
                setGameQuery(e.target.value);
                setSelectedGame(null);
                setShowGameDropdown(true);
              }}
              onFocus={() => setShowGameDropdown(true)}
              className="pl-9"
            />
            {selectedGame && (
              <Badge
                variant="secondary"
                className="absolute right-2 top-1/2 -translate-y-1/2 bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300"
              >
                <CheckCircleIcon className="w-3 h-3 mr-1" />
                Selezionato
              </Badge>
            )}
          </div>

          {showGameDropdown && gameQuery.length >= 2 && !selectedGame && (
            <div className="absolute z-50 w-full mt-1 bg-white dark:bg-zinc-800 border border-slate-200 dark:border-zinc-700 rounded-lg shadow-lg max-h-48 overflow-y-auto">
              {isSearching ? (
                <div className="p-3 text-sm text-slate-500 text-center">
                  <LoaderIcon className="w-4 h-4 animate-spin inline mr-2" />
                  Ricerca...
                </div>
              ) : gameResults.length === 0 ? (
                <div className="p-3 text-sm text-slate-500 text-center">Nessun gioco trovato</div>
              ) : (
                gameResults.map(game => (
                  <button
                    key={game.id}
                    type="button"
                    onClick={() => selectGame(game)}
                    className="w-full px-3 py-2 text-left text-sm hover:bg-slate-100 dark:hover:bg-zinc-700 flex items-center gap-2 transition-colors"
                  >
                    <FileTextIcon className="w-4 h-4 text-slate-400 shrink-0" />
                    <span className="truncate">{game.name}</span>
                    <Badge variant="outline" className="ml-auto text-xs shrink-0">
                      {game.source}
                    </Badge>
                  </button>
                ))
              )}
            </div>
          )}
        </div>
      </div>

      {/* Priority Toggle */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-3 border border-slate-200/50 dark:border-zinc-700/50 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-slate-700 dark:text-zinc-300">
            Priorità Elaborazione
          </span>
          {urgentPriority && (
            <span className="text-xs text-amber-600 dark:text-amber-400">— in testa alla coda</span>
          )}
        </div>
        <button
          type="button"
          data-testid="priority-toggle"
          onClick={() => setUrgentPriority(prev => !prev)}
          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${
            urgentPriority
              ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300 ring-1 ring-amber-300 dark:ring-amber-700'
              : 'bg-slate-100 text-slate-700 dark:bg-zinc-700 dark:text-zinc-300'
          }`}
        >
          {urgentPriority && <ZapIcon className="w-3.5 h-3.5" />}
          {urgentPriority ? 'Urgente' : 'Normale'}
        </button>
      </div>

      {/* Drop Zone */}
      <div
        role="button"
        tabIndex={0}
        onClick={() => selectedGame && fileInputRef.current?.click()}
        onKeyDown={e => {
          if ((e.key === 'Enter' || e.key === ' ') && selectedGame) fileInputRef.current?.click();
        }}
        onDrop={handleDrop}
        onDragOver={e => {
          e.preventDefault();
          if (selectedGame) setIsDragOver(true);
        }}
        onDragLeave={() => setIsDragOver(false)}
        className={`rounded-xl p-8 border-2 border-dashed transition-all ${
          !selectedGame
            ? 'border-slate-300/40 dark:border-zinc-600/40 bg-slate-50/50 dark:bg-zinc-800/30 cursor-not-allowed opacity-60'
            : isDragOver
              ? 'border-amber-500 bg-amber-50/30 dark:bg-amber-900/20 scale-[1.01]'
              : 'border-amber-400/40 dark:border-amber-600/40 hover:border-amber-400/60 hover:bg-amber-50/10 dark:hover:bg-amber-900/10 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md cursor-pointer'
        }`}
      >
        <input
          ref={fileInputRef}
          type="file"
          accept=".pdf"
          multiple
          onChange={handleFileChange}
          className="hidden"
          aria-label="Seleziona file PDF da caricare"
        />
        <div className="flex flex-col items-center justify-center text-center">
          <div className="w-16 h-16 bg-amber-100 dark:bg-amber-900/30 rounded-full flex items-center justify-center mb-4">
            <UploadCloudIcon className="w-8 h-8 text-amber-700 dark:text-amber-400" />
          </div>
          <h3 className="font-quicksand text-lg font-bold text-slate-900 dark:text-zinc-100 mb-2">
            {selectedGame
              ? 'Trascina i PDF qui o clicca per sfogliare'
              : 'Seleziona un gioco sopra per iniziare il caricamento'}
          </h3>
          <p className="text-sm text-slate-600 dark:text-zinc-400 mb-4">
            {selectedGame
              ? `Caricamento per: ${selectedGame.name} — File > 10MB usano upload chunked automaticamente`
              : 'Devi selezionare un gioco prima di caricare documenti'}
          </p>
          <div className="flex items-center gap-4 text-xs text-slate-500 dark:text-zinc-500">
            <div className="flex items-center gap-1">
              <FileTextIcon className="w-4 h-4" />
              <span>Solo PDF</span>
            </div>
            <span>Max 500MB per file</span>
          </div>
        </div>
      </div>

      {/* Upload Progress List */}
      {uploads.length > 0 && (
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-4 border border-slate-200/50 dark:border-zinc-700/50 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="font-quicksand font-bold text-sm text-slate-900 dark:text-zinc-100">
              Caricamenti ({uploads.filter(u => u.status === 'completed').length}/{uploads.length})
            </h3>
            {uploads.every(u => u.status === 'completed' || u.status === 'error') && (
              <Button variant="ghost" size="sm" onClick={() => setUploads([])}>
                Cancella tutto
              </Button>
            )}
          </div>

          {uploads.map((upload, index) => (
            <div
              key={`${upload.file.name}-${index}`}
              className="p-3 bg-slate-50/50 dark:bg-zinc-900/50 rounded-lg border border-slate-200/50 dark:border-zinc-700/50"
            >
              <div className="flex items-center justify-between mb-1">
                <div className="flex items-center gap-2 min-w-0">
                  <FileTextIcon className="w-4 h-4 text-slate-500 shrink-0" />
                  <span className="text-sm font-medium text-slate-900 dark:text-zinc-100 truncate">
                    {upload.file.name}
                  </span>
                  <span className="text-xs text-slate-500 shrink-0">
                    {(upload.file.size / (1024 * 1024)).toFixed(1)} MB
                  </span>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  {upload.status === 'uploading' && (
                    <LoaderIcon className="w-4 h-4 text-blue-500 animate-spin" />
                  )}
                  {upload.status === 'processing' && (
                    <LoaderIcon className="w-4 h-4 text-amber-500 animate-spin" />
                  )}
                  {upload.status === 'completed' && (
                    <CheckCircleIcon className="w-4 h-4 text-green-500" />
                  )}
                  {upload.status === 'error' && (
                    <AlertCircleIcon className="w-4 h-4 text-red-500" />
                  )}
                  {(upload.status === 'completed' || upload.status === 'error') && (
                    <button
                      type="button"
                      onClick={() => removeUpload(index)}
                      className="p-0.5 hover:bg-slate-200 dark:hover:bg-zinc-700 rounded"
                    >
                      <XIcon className="w-3 h-3 text-slate-400" />
                    </button>
                  )}
                </div>
              </div>

              {(upload.status === 'uploading' || upload.status === 'processing') && (
                <div className="space-y-1">
                  <div className="h-1.5 bg-gray-200 dark:bg-zinc-700 rounded-full overflow-hidden">
                    <div
                      className={`h-full transition-all duration-300 rounded-full ${
                        upload.status === 'processing'
                          ? 'bg-gradient-to-r from-amber-500 to-amber-400'
                          : 'bg-gradient-to-r from-blue-500 to-blue-400'
                      }`}
                      style={{ width: `${upload.progress}%` }}
                    />
                  </div>
                  <div className="text-xs text-slate-500 text-right">
                    {upload.status === 'processing' ? 'Elaborazione...' : `${upload.progress}%`}
                  </div>
                </div>
              )}

              {upload.status === 'error' && upload.error && (
                <p className="text-xs text-red-500 mt-1">{upload.error}</p>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
