'use client';

/**
 * MechanicCitationBadge — ME-M2.1 (Issue #530).
 *
 * The inline `[p.N]` citation control on the public mechanic card. Replaces the
 * plain badge/button from #528: it now previews the backing rulebook quote in a
 * Radix tooltip and opens the responsive citation panel on activation.
 *
 * Tooltip behaviour (spec B):
 *   - Hover: shows after ~300ms (Radix `delayDuration`), the truncated quote.
 *   - Keyboard focus: shows IMMEDIATELY (we drive `open` so focus bypasses the
 *     hover delay), for keyboard users who can't "hover".
 *   - Mouse-leave: closes after ~100ms.
 *   - `role="tooltip"`, positioned above with auto-flip + 8px offset.
 *
 * Activation: click / Enter / Space → `onOpen()` (Enter/Space are native for a
 * <button>). `aria-label="Citazione regolamento, pagina {N}"`.
 *
 * Edge cases:
 *   - `pdfPage == null` → disabled, non-interactive, ~60% opacity, tooltip
 *     "Pagina non disponibile".
 *   - Missing quote → tooltip shows `[Citazione regolamento p.{N}]`.
 */

import { useCallback, useRef, useState, type ReactElement } from 'react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import type { PublishedMechanicCitationDto } from '@/lib/api/schemas/mechanic-card.schemas';
import { truncateAtWordBoundary } from '@/lib/mechanic-card/citation-format';
import { cn } from '@/lib/utils';

const HOVER_OPEN_DELAY_MS = 300;
const HOVER_CLOSE_DELAY_MS = 100;
const TOOLTIP_OFFSET_PX = 8;

export interface MechanicCitationBadgeProps {
  readonly citation: PublishedMechanicCitationDto;
  /** Called when the badge is activated (click / Enter / Space). */
  readonly onOpen: () => void;
}

/**
 * Pick the tooltip preview text: the truncated quote, or a defensive label when
 * the quote is missing / blank.
 */
function tooltipPreview(citation: MechanicCitationBadgeProps['citation']): string {
  const quote = citation.quote?.trim();
  if (!quote) {
    return `[Citazione regolamento p.${citation.pdfPage}]`;
  }
  return truncateAtWordBoundary(quote);
}

export function MechanicCitationBadge({
  citation,
  onOpen,
}: MechanicCitationBadgeProps): ReactElement {
  const hasPage = typeof citation.pdfPage === 'number' && Number.isFinite(citation.pdfPage);
  // Controlled open so keyboard focus can bypass the hover delay (focus =
  // immediate) while hover keeps the 300ms Radix delay.
  const [open, setOpen] = useState(false);
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearCloseTimer = useCallback(() => {
    if (closeTimer.current) {
      clearTimeout(closeTimer.current);
      closeTimer.current = null;
    }
  }, []);

  const handleFocus = useCallback(() => {
    clearCloseTimer();
    setOpen(true); // focus → immediate
  }, [clearCloseTimer]);

  const handleBlur = useCallback(() => {
    clearCloseTimer();
    setOpen(false);
  }, [clearCloseTimer]);

  const handleMouseLeave = useCallback(() => {
    clearCloseTimer();
    closeTimer.current = setTimeout(() => setOpen(false), HOVER_CLOSE_DELAY_MS);
  }, [clearCloseTimer]);

  // ── Disabled variant: unknown page ─────────────────────────────────────────
  if (!hasPage) {
    return (
      <TooltipProvider delayDuration={HOVER_OPEN_DELAY_MS}>
        <Tooltip>
          <TooltipTrigger asChild>
            <span
              data-slot="mechanic-card-citation"
              data-testid="mechanic-card-citation-disabled"
              aria-disabled="true"
              className="inline-flex cursor-not-allowed items-center rounded-md border border-border bg-muted px-1.5 py-0.5 align-middle text-[11px] font-semibold text-muted-foreground opacity-60"
            >
              [p.?]
            </span>
          </TooltipTrigger>
          <TooltipContent side="top" sideOffset={TOOLTIP_OFFSET_PX} role="tooltip">
            Pagina non disponibile
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  const label = `p.${citation.pdfPage}`;

  return (
    <TooltipProvider delayDuration={HOVER_OPEN_DELAY_MS}>
      <Tooltip open={open} onOpenChange={setOpen}>
        <TooltipTrigger asChild>
          <button
            type="button"
            data-slot="mechanic-card-citation"
            data-testid={`mechanic-card-citation-${citation.pdfId}-${citation.pdfPage}`}
            onClick={onOpen}
            onFocus={handleFocus}
            onBlur={handleBlur}
            onMouseEnter={clearCloseTimer}
            onMouseLeave={handleMouseLeave}
            aria-label={`Citazione regolamento, pagina ${citation.pdfPage}`}
            className={cn(
              'inline-flex items-center rounded-md border border-entity-game/40 bg-entity-game/10 px-1.5 py-0.5 align-middle text-[11px] font-semibold text-entity-game-text transition-colors',
              'hover:bg-entity-game/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              'print:border-border print:bg-transparent print:text-foreground'
            )}
          >
            {label}
          </button>
        </TooltipTrigger>
        <TooltipContent
          side="top"
          sideOffset={TOOLTIP_OFFSET_PX}
          role="tooltip"
          className="max-w-xs text-left"
        >
          {tooltipPreview(citation)}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
