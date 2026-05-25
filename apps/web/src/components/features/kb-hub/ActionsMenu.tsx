/**
 * ActionsMenu — popover with 5 PDF row actions (open/reindex/cost/move/delete).
 *
 * Pure presentational. Issue #1481.
 * Mapped from `admin-mockups/design_files/sp4-kb-hub.jsx` ActionsMenu.
 *
 * Wraps Radix DropdownMenu primitive for keyboard nav + ESC close + focus return.
 * Caller injects trigger via `children` slot.
 */

'use client';

import type { ReactElement, ReactNode } from 'react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';

export type PdfAction = 'open' | 'reindex' | 'cost' | 'move' | 'delete';

export interface ActionsMenuItemLabel {
  readonly label: string;
  readonly description: string;
  readonly icon: string;
}

export interface ActionsMenuLabels {
  readonly headerSubtitle: string; // e.g. "{size} · {date}"
  readonly actions: Record<PdfAction, ActionsMenuItemLabel>;
}

export interface ActionsMenuPdfHeader {
  readonly name: string;
  readonly sizeFormatted: string;
  readonly uploadedAtRelative: string;
}

export interface ActionsMenuProps {
  readonly pdf: ActionsMenuPdfHeader;
  readonly labels: ActionsMenuLabels;
  readonly onSelect: (action: PdfAction) => void;
  readonly children: ReactNode;
  readonly open?: boolean;
  readonly onOpenChange?: (open: boolean) => void;
}

const ACTION_ORDER: ReadonlyArray<PdfAction> = ['open', 'reindex', 'cost', 'move', 'delete'];

export function ActionsMenu(props: ActionsMenuProps): ReactElement {
  const { pdf, labels, onSelect, children, open, onOpenChange } = props;
  const headerSubtitle = labels.headerSubtitle
    .replace('{size}', pdf.sizeFormatted)
    .replace('{date}', pdf.uploadedAtRelative);

  return (
    <DropdownMenu open={open} onOpenChange={onOpenChange}>
      <DropdownMenuTrigger asChild>{children}</DropdownMenuTrigger>
      <DropdownMenuContent
        data-slot="kb-hub-actions-menu"
        align="end"
        className="w-64 border-entity-kb/20 bg-card shadow-lg"
      >
        <DropdownMenuLabel className="border-b border-entity-kb/10 bg-entity-kb/5 px-4 py-2.5">
          <div className="flex items-center gap-2">
            <span aria-hidden="true" className="text-sm">
              📄
            </span>
            <div className="min-w-0">
              <div className="truncate font-display text-xs font-bold text-foreground">
                {pdf.name}
              </div>
              <div className="font-mono text-[10px] text-muted-foreground">{headerSubtitle}</div>
            </div>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator className="my-0" />
        {ACTION_ORDER.map((action, i) => {
          const item = labels.actions[action];
          const isDestructive = action === 'delete';
          return (
            <DropdownMenuItem
              key={action}
              data-slot={`kb-hub-actions-menu-${action}`}
              onSelect={event => {
                event.preventDefault();
                onSelect(action);
              }}
              className={i > 0 ? 'border-t border-border/50' : undefined}
            >
              <div className="flex w-full items-center gap-3 py-1">
                <span
                  aria-hidden="true"
                  className={
                    isDestructive
                      ? 'flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-destructive/10 text-sm text-destructive'
                      : 'flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-entity-kb/10 text-sm text-entity-kb'
                  }
                >
                  {item.icon}
                </span>
                <div className="min-w-0 flex-1">
                  <div
                    className={
                      isDestructive
                        ? 'font-display text-xs font-bold text-destructive'
                        : 'font-display text-xs font-bold text-foreground'
                    }
                  >
                    {item.label}
                  </div>
                  <div className="mt-0.5 text-[10px] text-muted-foreground">{item.description}</div>
                </div>
              </div>
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
