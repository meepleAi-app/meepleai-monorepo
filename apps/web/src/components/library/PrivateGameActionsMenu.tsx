'use client';

/**
 * #5081: Edit, Propose and Delete for a private game card in /library/private.
 *
 * The card passed them as `actions` without `showQuickActions`, so since #1877 (2026-06-04) the grid card drew only
 * the decorative `MenuPlaceholder` and none of them was reachable. This menu goes in the card's `actionsSlot`,
 * outside its clickable surface (no axe nested-interactive), and is always visible: by touch and by keyboard too.
 */

import { useRef, type JSX } from 'react';

import { MoreHorizontalIcon } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { useTranslation } from '@/hooks/useTranslation';

/** Runs an action; `trigger` is the menu's button, where focus goes back when the action's dialog closes. */
export type PrivateGameAction = (trigger: HTMLElement | null) => void;

interface PrivateGameActionsMenuProps {
  title: string;
  onEdit?: PrivateGameAction;
  onPropose?: PrivateGameAction;
  onDelete?: PrivateGameAction;
}

export function PrivateGameActionsMenu({
  title,
  onEdit,
  onPropose,
  onDelete,
}: PrivateGameActionsMenuProps): JSX.Element {
  const { t } = useTranslation();
  const triggerRef = useRef<HTMLButtonElement>(null);
  const choose = (action: PrivateGameAction) => () => action(triggerRef.current);

  return (
    // Modal (the default): while it is open the page underneath takes no clicks, so the click that closes it does
    // not reach the card, which opens the game.
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          ref={triggerRef}
          type="button"
          aria-label={t('privateGames.cardActions', { title })}
          // 32 px drawn, 44 px to touch: the ::after extends the hit area (design-system minimum).
          className="absolute right-2 top-2 z-20 flex h-8 w-8 items-center justify-center rounded-md border border-border bg-card/90 text-foreground shadow-sm backdrop-blur-md transition-colors after:absolute after:-inset-1.5 after:content-[''] hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          <MoreHorizontalIcon className="h-4 w-4" aria-hidden />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {onEdit && (
          <DropdownMenuItem onSelect={choose(onEdit)}>
            {t('privateGames.actionEdit')}
          </DropdownMenuItem>
        )}
        {onPropose && (
          <DropdownMenuItem onSelect={choose(onPropose)}>
            {t('privateGames.actionPropose')}
          </DropdownMenuItem>
        )}
        {onDelete && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onSelect={choose(onDelete)}
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
