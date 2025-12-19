/**
 * ChatHeader Component - Issue #2232
 *
 * Header for chat pages with game selector, thread title, and actions.
 * Features:
 * - Inline game selector dropdown
 * - Inline editable thread title
 * - Actions menu (Share, Export, Delete)
 * - Mobile menu trigger slot
 * - Responsive design
 */

'use client';

import { ReactNode, useState, useCallback } from 'react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/primitives/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  ShareIcon,
  DownloadIcon,
  TrashIcon,
  MoreVerticalIcon,
  CheckIcon,
  XIcon,
  PencilIcon,
} from 'lucide-react';
import { Game } from '@/types';

export interface ChatHeaderProps {
  /** Current selected game */
  game?: Game;
  /** Available games for selector */
  games?: Game[];
  /** Game selection handler */
  onGameChange?: (gameId: string) => void;
  /** Current thread title */
  threadTitle?: string;
  /** Thread title change handler */
  onTitleChange?: (title: string) => void;
  /** Mobile menu trigger (rendered on left for mobile) */
  mobileMenuTrigger?: ReactNode;
  /** Share action handler */
  onShare?: () => void;
  /** Export action handler */
  onExport?: () => void;
  /** Delete action handler */
  onDelete?: () => void;
  /** Loading states */
  loading?: {
    games?: boolean;
    title?: boolean;
  };
  /** Additional className */
  className?: string;
}

const MAX_TITLE_LENGTH = 255;

export function ChatHeader({
  game,
  games = [],
  onGameChange,
  threadTitle = 'Untitled Thread',
  onTitleChange,
  mobileMenuTrigger,
  onShare,
  onExport,
  onDelete,
  loading = {},
  className,
}: ChatHeaderProps) {
  const [isEditingTitle, setIsEditingTitle] = useState(false);
  const [editedTitle, setEditedTitle] = useState(threadTitle);

  const handleTitleEdit = useCallback(() => {
    setEditedTitle(threadTitle);
    setIsEditingTitle(true);
  }, [threadTitle]);

  const handleTitleSave = useCallback(() => {
    const trimmed = editedTitle.trim();
    if (trimmed && trimmed !== threadTitle && onTitleChange) {
      onTitleChange(trimmed);
    }
    setIsEditingTitle(false);
  }, [editedTitle, threadTitle, onTitleChange]);

  const handleTitleCancel = useCallback(() => {
    setEditedTitle(threadTitle);
    setIsEditingTitle(false);
  }, [threadTitle]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        handleTitleSave();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        handleTitleCancel();
      }
    },
    [handleTitleSave, handleTitleCancel]
  );

  return (
    <header
      className={cn(
        'sticky top-0 z-40 border-b border-[#dadce0] bg-white dark:bg-gray-900',
        className
      )}
      aria-label="Chat header with game selection and thread actions"
    >
      <div className="flex h-14 items-center gap-3 px-4 sm:px-6">
        {/* Mobile menu trigger */}
        {mobileMenuTrigger && <div className="md:hidden">{mobileMenuTrigger}</div>}

        {/* Game Selector - Inline compact version */}
        {games.length > 0 && (
          <div className="flex items-center gap-2 min-w-0">
            <Select
              value={game?.id ?? ''}
              onValueChange={value => {
                if (value && onGameChange) {
                  onGameChange(value);
                }
              }}
              disabled={loading.games}
            >
              <SelectTrigger
                className="h-9 w-[180px] sm:w-[220px] border-[#dadce0]"
                aria-label="Select game"
              >
                <SelectValue placeholder="Select game" />
              </SelectTrigger>
              <SelectContent>
                {games.map((g: Game) => (
                  <SelectItem key={g.id} value={g.id}>
                    {g.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        {/* Thread Title - Inline editable */}
        <div className="flex-1 min-w-0 flex items-center gap-2">
          {isEditingTitle ? (
            <>
              <Input
                type="text"
                value={editedTitle}
                onChange={e => setEditedTitle(e.target.value)}
                onKeyDown={handleKeyDown}
                onBlur={handleTitleSave}
                maxLength={MAX_TITLE_LENGTH}
                className="h-9 max-w-md"
                placeholder="Thread title"
                autoFocus
                aria-label="Edit thread title"
              />
              <Button
                variant="ghost"
                size="icon"
                className="h-9 w-9 shrink-0"
                onClick={handleTitleSave}
                aria-label="Save title"
              >
                <CheckIcon className="h-4 w-4" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="h-9 w-9 shrink-0"
                onMouseDown={e => {
                  e.preventDefault(); // Prevent input blur
                  handleTitleCancel();
                }}
                aria-label="Cancel editing"
              >
                <XIcon className="h-4 w-4" />
              </Button>
            </>
          ) : (
            <button
              onClick={handleTitleEdit}
              className={cn(
                'text-left truncate text-sm font-medium hover:text-[#1a73e8] transition-colors',
                'focus:outline-none focus:ring-2 focus:ring-[#1a73e8] focus:ring-offset-2 rounded px-2 py-1',
                loading.title && 'opacity-50 cursor-wait'
              )}
              disabled={loading.title}
              aria-label="Click to edit thread title"
              title={threadTitle}
            >
              {threadTitle}
              <PencilIcon className="inline-block ml-2 h-3 w-3 opacity-0 group-hover:opacity-100 transition-opacity" />
            </button>
          )}
        </div>

        {/* Actions Menu */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="h-9 w-9"
              aria-label="Thread actions menu"
            >
              <MoreVerticalIcon className="h-5 w-5" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            {onShare && (
              <DropdownMenuItem onClick={onShare} className="cursor-pointer">
                <ShareIcon className="mr-2 h-4 w-4" />
                Share Thread
              </DropdownMenuItem>
            )}
            {onExport && (
              <DropdownMenuItem onClick={onExport} className="cursor-pointer">
                <DownloadIcon className="mr-2 h-4 w-4" />
                Export Chat
              </DropdownMenuItem>
            )}
            {(onShare || onExport) && onDelete && <DropdownMenuSeparator />}
            {onDelete && (
              <DropdownMenuItem
                onClick={onDelete}
                className="text-red-600 dark:text-red-400 cursor-pointer"
              >
                <TrashIcon className="mr-2 h-4 w-4" />
                Delete Thread
              </DropdownMenuItem>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
