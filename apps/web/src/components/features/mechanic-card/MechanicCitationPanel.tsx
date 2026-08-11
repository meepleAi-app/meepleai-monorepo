'use client';

/**
 * MechanicCitationPanel — ME-M2.1 (Issue #530).
 *
 * The responsive source-of-truth panel a reader opens from a citation badge. It
 * shows the cited rulebook page with the quote highlighted and offers a
 * one-click APA-style "copy citation".
 *
 * Responsive surface (spec C):
 *   - Desktop (≥768px): a right-hand {@link Sheet} (~480px).
 *   - Mobile  (<768px): a bottom {@link Drawer} (full-height).
 *   We detect the breakpoint via `useMediaQuery` and render EXACTLY ONE of them
 *   (never both), so the PDF viewer mounts once.
 *
 * PDF rendering reuses the shared {@link PdfQuoteViewer} (extracted from the
 * admin {@link PdfQuoteHighlighter}), which lazily pulls pdfjs via
 * `PdfInlineViewer`. Admin behaviour is unchanged.
 *
 * Copy behaviour (spec C footer):
 *   - `navigator.clipboard` present → write the APA string + a `sonner` success
 *     toast.
 *   - absent → open a fallback modal with a pre-selected read-only <textarea>
 *     and a "Premi Ctrl+C per copiare" hint.
 *
 * a11y: the Sheet/Drawer primitives provide the focus trap + ESC-to-close. We
 * additionally restore focus to the badge that opened the panel and announce the
 * open via an `aria-live="polite"` region.
 */

import { useCallback, useEffect, useRef, useState, type ReactElement } from 'react';

import { toast } from 'sonner';

import { PdfQuoteViewer } from '@/components/pdf/PdfQuoteViewer';
import { Badge } from '@/components/ui/data-display/badge';
import { Drawer, DrawerContent, DrawerHeader, DrawerTitle } from '@/components/ui/drawer/drawer';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { useMediaQuery } from '@/hooks/useMediaQuery';
import type {
  PublishedMechanicCardDto,
  PublishedMechanicCitationDto,
} from '@/lib/api/schemas/mechanic-card.schemas';
import { DOCUMENT_NAME_FALLBACK, formatApaCitation } from '@/lib/mechanic-card/citation-format';

const DESKTOP_QUERY = '(min-width: 768px)';

export interface MechanicCitationPanelProps {
  /** The citation being previewed. `null` renders nothing. */
  readonly citation: PublishedMechanicCitationDto | null;
  /** Card-level provenance used to build the APA string. */
  readonly card: Pick<
    PublishedMechanicCardDto,
    'gameName' | 'publisher' | 'publicationYear' | 'documentName' | 'sourceAnalysisId'
  >;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

/**
 * Build the APA citation string for the current citation + card. Extracted so
 * both the clipboard write and the fallback textarea use the identical text.
 */
function buildApaString(
  citation: PublishedMechanicCitationDto,
  card: MechanicCitationPanelProps['card']
): string {
  return formatApaCitation({
    publisher: card.publisher,
    publicationYear: card.publicationYear,
    gameTitle: card.gameName,
    documentName: card.documentName,
    pdfPage: citation.pdfPage,
    quote: citation.quote,
    analysisId: card.sourceAnalysisId,
  });
}

/** The panel body — identical inside the Sheet and the Drawer. */
function PanelBody({
  citation,
  card,
}: {
  citation: PublishedMechanicCitationDto;
  card: MechanicCitationPanelProps['card'];
}): ReactElement {
  const [fallbackOpen, setFallbackOpen] = useState(false);
  const fallbackText = buildApaString(citation, card);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const docLabel = card.documentName?.trim() || DOCUMENT_NAME_FALLBACK;

  const handleCopy = useCallback(async () => {
    const text = buildApaString(citation, card);
    const clipboard = typeof navigator !== 'undefined' ? navigator.clipboard : undefined;
    if (!clipboard || typeof clipboard.writeText !== 'function') {
      setFallbackOpen(true);
      return;
    }
    try {
      await clipboard.writeText(text);
      toast.success('Citazione copiata');
    } catch {
      // Permission denied / not-allowed → offer the manual fallback.
      setFallbackOpen(true);
    }
  }, [citation, card]);

  // Pre-select the fallback textarea so the user can immediately press Ctrl+C.
  useEffect(() => {
    if (fallbackOpen) {
      const el = textareaRef.current;
      if (el) {
        el.focus();
        el.select();
      }
    }
  }, [fallbackOpen]);

  return (
    <div
      className="flex min-h-0 flex-1 flex-col"
      data-slot="mechanic-citation-panel-body"
      data-testid="mechanic-citation-panel-body"
    >
      {/* Header meta */}
      <div className="flex flex-col gap-2 border-b border-border px-4 pb-3">
        <div className="flex flex-wrap items-center gap-2">
          <Badge
            variant="outline"
            className="border-entity-game/40 bg-entity-game/10 text-entity-game-text"
          >
            AI-generated, human-reviewed
          </Badge>
          <span className="text-xs uppercase tracking-wide text-muted-foreground">
            pagina {citation.pdfPage}
          </span>
        </div>
        <p className="text-sm font-semibold text-foreground">{docLabel}</p>
      </div>

      {/* PDF + highlight (shared with admin) */}
      <div className="min-h-0 flex-1 overflow-auto px-4 py-3">
        <PdfQuoteViewer
          documentId={citation.pdfId}
          page={citation.pdfPage}
          quote={citation.quote}
          resetKey={`${citation.pdfId}:${citation.pdfPage}`}
        />
      </div>

      {/* Footer actions */}
      <div className="flex items-center justify-between gap-2 border-t border-border px-4 py-3">
        <Button type="button" size="sm" onClick={handleCopy} data-testid="mechanic-citation-copy">
          Copia citazione
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled
          title="Segnalazione errori disponibile a breve"
          data-testid="mechanic-citation-report"
        >
          Segnala errore
        </Button>
      </div>

      {/* Clipboard fallback modal (no navigator.clipboard) */}
      <Dialog open={fallbackOpen} onOpenChange={setFallbackOpen}>
        <DialogContent className="max-w-md" data-testid="mechanic-citation-copy-fallback">
          <DialogHeader>
            <DialogTitle>Copia manuale</DialogTitle>
            <DialogDescription>Premi Ctrl+C per copiare la citazione.</DialogDescription>
          </DialogHeader>
          <textarea
            ref={textareaRef}
            readOnly
            value={fallbackText}
            rows={6}
            aria-label="Testo della citazione"
            className="w-full resize-none rounded-md border border-border bg-muted p-2 font-mono text-xs text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
          <div className="flex justify-end">
            <DialogClose asChild>
              <Button type="button" variant="outline" size="sm">
                Chiudi
              </Button>
            </DialogClose>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}

export function MechanicCitationPanel({
  citation,
  card,
  isOpen,
  onClose,
}: MechanicCitationPanelProps): ReactElement | null {
  const isDesktop = useMediaQuery(DESKTOP_QUERY);

  // Track the element that had focus when the panel opened, to restore it on
  // close (spec: ESC restores focus to the badge that opened the panel). The
  // Radix/vaul primitives restore focus too, but tracking makes the AD-2
  // "swap content" case deterministic.
  const triggerRef = useRef<HTMLElement | null>(null);
  useEffect(() => {
    if (isOpen) {
      triggerRef.current = (document.activeElement as HTMLElement) ?? null;
    }
  }, [isOpen]);

  const handleOpenChange = useCallback(
    (next: boolean) => {
      if (!next) {
        onClose();
        // Defer to after the primitive tears down its own focus management.
        const trigger = triggerRef.current;
        if (trigger && typeof trigger.focus === 'function') {
          requestAnimationFrame(() => trigger.focus());
        }
      }
    },
    [onClose]
  );

  if (!citation) return null;

  const announcement = isOpen
    ? `Citazione aperta: ${card.documentName?.trim() || DOCUMENT_NAME_FALLBACK}, pagina ${citation.pdfPage}`
    : '';

  const liveRegion = (
    <span aria-live="polite" className="sr-only" data-testid="mechanic-citation-live">
      {announcement}
    </span>
  );

  if (isDesktop) {
    return (
      <Sheet open={isOpen} onOpenChange={handleOpenChange}>
        <SheetContent
          className="flex w-full flex-col gap-0 p-0 sm:max-w-[480px]"
          data-testid="mechanic-citation-panel-sheet"
        >
          <SheetHeader className="px-4 pb-2 pt-4 text-left">
            <SheetTitle>Fonte della citazione</SheetTitle>
          </SheetHeader>
          {liveRegion}
          <PanelBody citation={citation} card={card} />
        </SheetContent>
      </Sheet>
    );
  }

  return (
    <Drawer open={isOpen} onOpenChange={handleOpenChange} side="mobile-bottom" entity="game">
      <DrawerContent className="h-[90vh] gap-0 p-0" data-testid="mechanic-citation-panel-drawer">
        <DrawerHeader className="px-4 pb-2 pt-2 text-left">
          <DrawerTitle>Fonte della citazione</DrawerTitle>
        </DrawerHeader>
        {liveRegion}
        <PanelBody citation={citation} card={card} />
      </DrawerContent>
    </Drawer>
  );
}
