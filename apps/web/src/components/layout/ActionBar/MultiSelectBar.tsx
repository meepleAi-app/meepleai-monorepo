/**
 * MultiSelectBar Component
 * Issue #3292 - Phase 6: Breadcrumb & Polish
 *
 * Batch actions mode when multiple items are selected.
 * Replaces ActionBar when multi-select is active.
 */

'use client';

import { forwardRef, type ComponentPropsWithoutRef } from 'react';

import { X, Tag, FolderPlus, Trash2, MoreHorizontal } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { useMultiSelect } from '@/hooks/useMultiSelect';
import { cn } from '@/lib/utils';

export interface MultiSelectBarProps
  extends Omit<ComponentPropsWithoutRef<'div'>, 'children'> {
  /** Handler for tag action */
  onTag?: () => void;
  /** Handler for move action */
  onMove?: () => void;
  /** Handler for delete action */
  onDelete?: () => void;
  /** Additional menu actions */
  additionalActions?: Array<{
    id: string;
    label: string;
    icon?: React.ReactNode;
    action: () => void;
    variant?: 'default' | 'destructive';
  }>;
}

/**
 * MultiSelectBar Component
 *
 * Shows batch action buttons when multiple items are selected.
 * Automatically appears when multi-select mode is active.
 */
export const MultiSelectBar = forwardRef<HTMLDivElement, MultiSelectBarProps>(
  function MultiSelectBar(
    { onTag, onMove, onDelete, additionalActions = [], className, ...props },
    ref
  ) {
    const { isActive, selectedCount, exitMultiSelect } = useMultiSelect();

    // Don't render if not in multi-select mode
    if (!isActive) {
      return null;
    }

    return (
      <div
        ref={ref}
        className={cn(
          // Fixed positioning at bottom
          'fixed bottom-0 left-0 right-0 z-40',

          // Height with safe area
          'h-14',
          'pb-safe-area-inset-bottom',

          // Glass morphism effect
          'bg-primary/95 dark:bg-primary/90',
          'backdrop-blur-lg',
          'border-t border-primary-foreground/20',

          // Animation
          'animate-in fade-in-0 slide-in-from-bottom-4',
          'duration-200',

          className
        )}
        role="toolbar"
        aria-label={`Selezione multipla: ${selectedCount} elementi selezionati`}
        {...props}
      >
        <div className="h-full max-w-screen-lg mx-auto flex items-center justify-between px-4">
          {/* Left side: Clear button and count */}
          <div className="flex items-center gap-3">
            <Button
              variant="ghost"
              size="icon"
              onClick={exitMultiSelect}
              className="h-10 w-10 text-primary-foreground hover:bg-primary-foreground/10"
              aria-label="Annulla selezione"
            >
              <X className="h-5 w-5" aria-hidden="true" />
            </Button>

            <span className="text-sm font-medium text-primary-foreground">
              {selectedCount} selezionat{selectedCount === 1 ? 'o' : 'i'}
            </span>
          </div>

          {/* Right side: Actions */}
          <div className="flex items-center gap-2">
            {/* Tag action */}
            {onTag && (
              <Button
                variant="ghost"
                size="icon"
                onClick={onTag}
                className="h-11 w-11 text-primary-foreground hover:bg-primary-foreground/10"
                aria-label="Aggiungi etichetta"
              >
                <Tag className="h-5 w-5" aria-hidden="true" />
              </Button>
            )}

            {/* Move action */}
            {onMove && (
              <Button
                variant="ghost"
                size="icon"
                onClick={onMove}
                className="h-11 w-11 text-primary-foreground hover:bg-primary-foreground/10"
                aria-label="Sposta in cartella"
              >
                <FolderPlus className="h-5 w-5" aria-hidden="true" />
              </Button>
            )}

            {/* Delete action */}
            {onDelete && (
              <Button
                variant="ghost"
                size="icon"
                onClick={onDelete}
                className="h-11 w-11 text-primary-foreground hover:bg-primary-foreground/10"
                aria-label="Elimina selezionati"
              >
                <Trash2 className="h-5 w-5" aria-hidden="true" />
              </Button>
            )}

            {/* Additional actions overflow */}
            {additionalActions.length > 0 && (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-11 w-11 text-primary-foreground hover:bg-primary-foreground/10"
                    aria-label="Altre azioni"
                  >
                    <MoreHorizontal className="h-5 w-5" aria-hidden="true" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                  align="end"
                  side="top"
                  sideOffset={8}
                  className="min-w-[180px]"
                >
                  {additionalActions.map(action => (
                    <DropdownMenuItem
                      key={action.id}
                      onClick={action.action}
                      className={cn(
                        'flex items-center gap-3 px-3 py-2 min-h-[44px]',
                        action.variant === 'destructive' &&
                          'text-destructive focus:text-destructive'
                      )}
                    >
                      {action.icon && (
                        <span className="shrink-0" aria-hidden="true">
                          {action.icon}
                        </span>
                      )}
                      <span>{action.label}</span>
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </div>
        </div>

        {/* Screen reader announcement */}
        <span className="sr-only" role="status" aria-live="polite">
          Modalità selezione multipla attiva. {selectedCount} elementi selezionati.
        </span>
      </div>
    );
  }
);

MultiSelectBar.displayName = 'MultiSelectBar';
