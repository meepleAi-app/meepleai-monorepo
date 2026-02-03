/**
 * ChatSessionCard - Card component for displaying chat session summary
 * Issue #3484 - Frontend Chat History Integration
 *
 * Features:
 * - Display session title, game, timestamp, message count
 * - Click to resume chat
 * - Delete action with confirmation
 * - Loading and hover states
 *
 * @example
 * ```tsx
 * <ChatSessionCard
 *   session={session}
 *   onResume={(id) => router.push(`/chat/${id}`)}
 *   onDelete={(id) => deleteMutation.mutate(id)}
 * />
 * ```
 */

'use client';

import { useState } from 'react';

import { motion } from 'framer-motion';
import {
  Clock,
  Gamepad2,
  Hash,
  MessageSquare,
  MoreVertical,
  Trash2,
} from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { ConfirmationDialog } from '@/components/ui/overlays/confirmation-dialog';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ChatSessionCardProps {
  /** Session ID */
  id: string;
  /** Session title (optional) */
  title?: string | null;
  /** Associated game title */
  gameTitle?: string | null;
  /** Number of messages in session */
  messageCount: number;
  /** ISO timestamp of last message */
  lastMessageAt: string | null;
  /** ISO timestamp of session creation */
  createdAt: string;
  /** Preview of last message (optional) */
  lastMessagePreview?: string | null;
  /** Click handler to resume session */
  onResume?: (sessionId: string) => void;
  /** Delete handler */
  onDelete?: (sessionId: string) => void;
  /** Whether delete is in progress */
  isDeleting?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Animation index for staggered entrance */
  index?: number;
}

// ============================================================================
// Helper Functions
// ============================================================================

function formatRelativeTime(isoDate: string | null): string {
  if (!isoDate) return 'Mai';

  const date = new Date(isoDate);
  const now = new Date();

  // Check if same day
  const isToday =
    date.getDate() === now.getDate() &&
    date.getMonth() === now.getMonth() &&
    date.getFullYear() === now.getFullYear();

  // Check if yesterday
  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);
  const isYesterday =
    date.getDate() === yesterday.getDate() &&
    date.getMonth() === yesterday.getMonth() &&
    date.getFullYear() === yesterday.getFullYear();

  if (isToday) {
    return `Oggi ${date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}`;
  }
  if (isYesterday) {
    return `Ieri ${date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}`;
  }
  // More than yesterday, show date
  return date.toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function truncateText(text: string, maxLength: number): string {
  if (text.length <= maxLength) return text;
  return text.slice(0, maxLength - 3) + '...';
}

// ============================================================================
// ChatSessionCard Component
// ============================================================================

export function ChatSessionCard({
  id,
  title,
  gameTitle,
  messageCount,
  lastMessageAt,
  createdAt,
  lastMessagePreview,
  onResume,
  onDelete,
  isDeleting = false,
  className,
  index = 0,
}: ChatSessionCardProps) {
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);

  const displayTitle = title || `Chat del ${new Date(createdAt).toLocaleDateString('it-IT')}`;

  const handleResume = () => {
    onResume?.(id);
  };

  const handleDeleteClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    setShowDeleteDialog(true);
  };

  const handleConfirmDelete = () => {
    onDelete?.(id);
    setShowDeleteDialog(false);
  };

  return (
    <>
      <motion.div
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: index * 0.05 }}
        className={cn(
          'group relative rounded-xl border border-border/60 bg-card/80 backdrop-blur-sm p-4',
          'hover:border-primary/30 hover:bg-card/90 hover:shadow-sm',
          'transition-all duration-200 cursor-pointer',
          isDeleting && 'opacity-50 pointer-events-none',
          className
        )}
        onClick={handleResume}
        data-testid={`chat-session-card-${id}`}
      >
        {/* Header */}
        <div className="flex items-start justify-between gap-2 mb-2">
          <div className="flex items-center gap-2 min-w-0 flex-1">
            <div className="h-8 w-8 rounded-lg bg-blue-500/20 flex items-center justify-center shrink-0">
              <MessageSquare className="h-4 w-4 text-blue-600 dark:text-blue-400" />
            </div>
            <div className="min-w-0 flex-1">
              <h4
                className="font-medium text-sm truncate group-hover:text-primary transition-colors"
                data-testid={`session-title-${id}`}
              >
                {displayTitle}
              </h4>
              {gameTitle && (
                <div className="flex items-center gap-1 text-xs text-muted-foreground">
                  <Gamepad2 className="h-3 w-3" />
                  <span className="truncate">{gameTitle}</span>
                </div>
              )}
            </div>
          </div>

          {/* Actions Menu */}
          {onDelete && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild onClick={(e: React.MouseEvent) => e.stopPropagation()}>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity"
                  data-testid={`session-menu-${id}`}
                >
                  <MoreVertical className="h-4 w-4" />
                  <span className="sr-only">Menu</span>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem
                  className="text-destructive focus:text-destructive"
                  onClick={handleDeleteClick}
                  data-testid={`delete-session-${id}`}
                >
                  <Trash2 className="h-4 w-4 mr-2" />
                  Elimina
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>

        {/* Preview */}
        {lastMessagePreview && (
          <p
            className="text-xs text-muted-foreground mb-3 line-clamp-2"
            data-testid={`session-preview-${id}`}
          >
            {truncateText(lastMessagePreview, 100)}
          </p>
        )}

        {/* Footer */}
        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-1" data-testid={`session-messages-${id}`}>
              <Hash className="h-3 w-3" />
              <span>{messageCount} messaggi</span>
            </div>
          </div>
          <div className="flex items-center gap-1" data-testid={`session-time-${id}`}>
            <Clock className="h-3 w-3" />
            <span>{formatRelativeTime(lastMessageAt)}</span>
          </div>
        </div>
      </motion.div>

      {/* Delete Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={showDeleteDialog}
        onClose={() => setShowDeleteDialog(false)}
        onConfirm={handleConfirmDelete}
        title="Eliminare questa conversazione?"
        message={`Questa azione non può essere annullata. La conversazione "${displayTitle}" e tutti i suoi ${messageCount} messaggi verranno eliminati permanentemente.`}
        confirmText="Elimina"
        cancelText="Annulla"
        variant="destructive"
      />
    </>
  );
}

export default ChatSessionCard;
