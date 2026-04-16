'use client';

import { ArrowRightIcon, Upload } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import type { GameWithoutKbDto } from '@/lib/api/kb-games-without-kb-api';

interface UploadForGameDrawerProps {
  game: GameWithoutKbDto | null;
  open: boolean;
  onClose: () => void;
}

/**
 * Slide-over drawer that previews the target game and hands the admin off to
 * the dedicated upload page (`/admin/knowledge-base/upload?gameId=...`).
 *
 * Previously this drawer also accepted files via drag-and-drop, but the files
 * were silently dropped on navigation (the upload page owns its own selection
 * state). The simplified flow avoids misleading UX: one clear CTA that lands
 * the admin on the real upload form pre-filtered by game.
 *
 * See: docs/superpowers/plans/2026-04-11-rag-admin-onboarding.md (Task 6)
 */
export function UploadForGameDrawer({ game, open, onClose }: UploadForGameDrawerProps) {
  const uploadHref = game ? `/admin/knowledge-base/upload?gameId=${game.gameId}` : undefined;

  if (!game) return null;

  return (
    <Sheet open={open} onOpenChange={next => !next && onClose()}>
      <SheetContent
        side="right"
        className="flex w-[420px] flex-col gap-0 p-0 sm:w-[480px] sm:max-w-[480px]"
      >
        <SheetHeader className="border-b border-slate-200 px-6 py-4 dark:border-zinc-700">
          <SheetTitle className="text-base font-semibold">Upload PDF — {game.title}</SheetTitle>
        </SheetHeader>

        <div className="flex-1 space-y-4 overflow-y-auto px-6 py-4">
          <MeepleCard
            entity="game"
            variant="list"
            title={game.title}
            subtitle={game.publisher ?? undefined}
            imageUrl={game.imageUrl ?? undefined}
            status="idle"
            metadata={[
              { label: 'Giocatori', value: game.playerCountLabel },
              ...(game.pdfCount > 0
                ? [{ label: 'PDF caricati', value: String(game.pdfCount) }]
                : []),
              ...(game.failedPdfCount > 0
                ? [{ label: 'PDF falliti', value: String(game.failedPdfCount) }]
                : []),
            ]}
          />

          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700 dark:border-zinc-700 dark:bg-zinc-800/50 dark:text-zinc-300">
            <p className="font-medium">Come procedere</p>
            <p className="mt-1 text-xs text-slate-500 dark:text-zinc-400">
              Aprirai il flusso di upload principale con il gioco già selezionato. Carica uno o più
              PDF: l&apos;estrazione e l&apos;indicizzazione RAG partono automaticamente una volta
              completato l&apos;upload.
            </p>
          </div>
        </div>

        <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-4 dark:border-zinc-700">
          <Button variant="outline" onClick={onClose}>
            Annulla
          </Button>
          <Button asChild>
            <a href={uploadHref}>
              <Upload className="mr-2 h-4 w-4" aria-hidden="true" />
              Apri flusso di upload
              <ArrowRightIcon className="ml-2 h-4 w-4" aria-hidden="true" />
            </a>
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}
