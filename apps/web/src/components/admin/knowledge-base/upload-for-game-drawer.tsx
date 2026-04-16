'use client';

import { useCallback, useState } from 'react';

import { FileText, Upload, X } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/useToast';
import type { GameWithoutKbDto } from '@/lib/api/kb-games-without-kb-api';

const MAX_FILES = 5;
const MAX_SIZE_MB = 500;

interface UploadForGameDrawerProps {
  game: GameWithoutKbDto | null;
  open: boolean;
  onClose: () => void;
}

interface PendingFile {
  file: File;
  id: string;
}

/**
 * Slide-over drawer that lets an admin upload PDFs targeting a specific
 * SharedGame that currently has no Knowledge Base.
 *
 * On submit we navigate to the existing /admin/knowledge-base/upload page
 * with `gameId` pre-filled — the existing UploadZone handles the actual
 * ingestion pipeline.
 *
 * See: docs/superpowers/plans/2026-04-11-rag-admin-onboarding.md (Task 6)
 */
export function UploadForGameDrawer({ game, open, onClose }: UploadForGameDrawerProps) {
  const { toast } = useToast();
  const [pendingFiles, setPendingFiles] = useState<PendingFile[]>([]);
  const [isDragging, setIsDragging] = useState(false);

  const addFiles = useCallback(
    (files: File[]) => {
      const valid = files.filter(f => {
        const sizeMb = f.size / (1024 * 1024);
        if (sizeMb > MAX_SIZE_MB) {
          toast({
            title: `${f.name} supera ${MAX_SIZE_MB}MB`,
            variant: 'destructive',
          });
          return false;
        }
        return true;
      });

      setPendingFiles(prev => {
        const available = MAX_FILES - prev.length;
        if (available <= 0) {
          toast({
            title: `Massimo ${MAX_FILES} file per upload`,
            variant: 'destructive',
          });
          return prev;
        }
        const toAdd = valid.slice(0, available).map(f => ({
          file: f,
          id: crypto.randomUUID(),
        }));
        return [...prev, ...toAdd];
      });
    },
    [toast]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragging(false);
      const dropped = Array.from(e.dataTransfer.files).filter(f => f.type === 'application/pdf');
      addFiles(dropped);
    },
    [addFiles]
  );

  const handleFileInput = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const selected = Array.from(e.target.files ?? []);
      addFiles(selected);
      e.target.value = '';
    },
    [addFiles]
  );

  function removeFile(id: string) {
    setPendingFiles(prev => prev.filter(f => f.id !== id));
  }

  function handleClose() {
    setPendingFiles([]);
    onClose();
  }

  const handleUpload = useCallback(() => {
    if (!game || pendingFiles.length === 0) return;
    const params = new URLSearchParams({ gameId: game.gameId });
    window.location.href = `/admin/knowledge-base/upload?${params.toString()}`;
  }, [game, pendingFiles]);

  if (!game) return null;

  return (
    <Sheet open={open} onOpenChange={next => !next && handleClose()}>
      <SheetContent
        side="right"
        className="flex w-[420px] flex-col gap-0 p-0 sm:w-[480px] sm:max-w-[480px]"
      >
        <SheetHeader className="border-b border-slate-200 px-6 py-4 dark:border-zinc-700">
          <SheetTitle className="text-base font-semibold">Upload PDF — {game.title}</SheetTitle>
        </SheetHeader>

        <div className="flex-1 space-y-4 overflow-y-auto px-6 py-4">
          <MeepleCard
            entity="kb"
            variant="list"
            title={game.title}
            subtitle={game.publisher ?? undefined}
            imageUrl={game.imageUrl ?? undefined}
            status="idle"
            metadata={[{ label: 'Giocatori', value: game.playerCountLabel }]}
          />

          <div
            onDragOver={e => {
              e.preventDefault();
              setIsDragging(true);
            }}
            onDragLeave={() => setIsDragging(false)}
            onDrop={handleDrop}
            className={`relative cursor-pointer rounded-xl border-2 border-dashed p-8 text-center transition-colors ${
              isDragging
                ? 'border-blue-400 bg-blue-50 dark:bg-blue-900/20'
                : 'border-slate-300 hover:border-slate-400 dark:border-zinc-600 dark:hover:border-zinc-500'
            }`}
          >
            <input
              type="file"
              accept=".pdf,application/pdf"
              multiple
              onChange={handleFileInput}
              className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
              aria-label="Seleziona file PDF"
            />
            <Upload
              className="mx-auto mb-2 h-8 w-8 text-slate-400 dark:text-zinc-500"
              aria-hidden="true"
            />
            <p className="text-sm font-medium text-slate-700 dark:text-zinc-300">
              Trascina i PDF qui
            </p>
            <p className="mt-1 text-xs text-slate-400 dark:text-zinc-500">
              Max {MAX_FILES} file · Max {MAX_SIZE_MB}MB ciascuno
            </p>
          </div>

          {pendingFiles.length > 0 && (
            <ul className="space-y-2">
              {pendingFiles.map(({ file, id }) => (
                <li
                  key={id}
                  className="flex items-center gap-3 rounded-lg bg-slate-50 px-3 py-2 dark:bg-zinc-800"
                >
                  <FileText
                    className="h-4 w-4 shrink-0 text-slate-400 dark:text-zinc-500"
                    aria-hidden="true"
                  />
                  <span className="flex-1 truncate text-sm text-slate-700 dark:text-zinc-300">
                    {file.name}
                  </span>
                  <span className="shrink-0 text-xs text-slate-400 dark:text-zinc-500">
                    {(file.size / (1024 * 1024)).toFixed(1)} MB
                  </span>
                  <button
                    type="button"
                    onClick={() => removeFile(id)}
                    className="text-slate-400 transition-colors hover:text-red-500 dark:text-zinc-500 dark:hover:text-red-400"
                    aria-label={`Rimuovi ${file.name}`}
                  >
                    <X className="h-4 w-4" aria-hidden="true" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-4 dark:border-zinc-700">
          <Button variant="outline" onClick={handleClose}>
            Annulla
          </Button>
          <Button onClick={handleUpload} disabled={pendingFiles.length === 0}>
            <Upload className="mr-2 h-4 w-4" aria-hidden="true" />
            Avvia upload ({pendingFiles.length} PDF)
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}
