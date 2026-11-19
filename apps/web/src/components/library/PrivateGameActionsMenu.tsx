'use client';

/**
 * #5081: Edit, Propose and Delete for a private game card in /library/private.
 *
 * The card passed them as `actions` without `showQuickActions`, so since #1877 (2026-06-04) the grid card drew only
 * the decorative `MenuPlaceholder` and none of them was reachable. This menu goes in the card's `actionsSlot`,
 * outside its clickable surface (no axe nested-interactive), and is always visible: by touch and by keyboard too.
 */

import type { JSX } from 'react';

import { MoreHorizontalIcon } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { useTranslation } from '@/hooks/useTranslation';

interface PrivateGameActionsMenuProps {
  title: string;
  onEdit?: () => void;
  onPropose?: () => void;
  onDelete?: () => void;
}

export function PrivateGameActionsMenu({
  title,
  onEdit,
  onPropose,
  onDelete,
}: PrivateGameActionsMenuProps): JSX.Element {
  const { t } = useTranslation();

  return (
    // Not modal: each item opens a dialog, and a modal menu closing under a modal dialog can leave the body inert.
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={t('privateGames.cardActions', { title })}
          className="absolute right-2 top-2 z-20 flex h-8 w-8 items-center justify-center rounded-md border border-border bg-card/90 text-foreground shadow-sm backdrop-blur-md transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          <MoreHorizontalIcon className="h-4 w-4" aria-hidden />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {onEdit && (
          <DropdownMenuItem onSelect={onEdit}>{t('privateGames.actionEdit')}</DropdownMenuItem>
        )}
        {onPropose && (
          <DropdownMenuItem onSelect={onPropose}>
            {t('privateGames.actionPropose')}
          </DropdownMenuItem>
        )}
        {onDelete && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onSelect={onDelete}
              className="text-destructive focus:text-destructive"
            >
              {t('privateGames.actionDelete')}
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
