/**
 * ChatThreadHeader - Header for active chat thread (Issue #4364)
 *
 * Shows:
 * - Back button to /chat/new
 * - Editable thread title
 * - Agent switcher dropdown
 * - Action buttons: History, Export, Share, Delete
 */

'use client';

import React, { useState, useCallback } from 'react';

import {
  ArrowLeft,
  Bot,
  Check,
  Download,
  History,
  Pencil,
  Share2,
  Trash2,
  X,
} from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ChatThreadHeaderProps {
  /** Thread title */
  title: string;
  /** Game name for subtitle */
  gameName?: string;
  /** Agent name for subtitle */
  agentName?: string;
  /** Whether title is editable */
  editableTitle?: boolean;
  /** Handler for title change */
  onTitleChange?: (newTitle: string) => void;
  /** Handler for history drawer toggle */
  onHistoryToggle?: () => void;
  /** Handler for export */
  onExport?: () => void;
  /** Handler for share */
  onShare?: () => void;
  /** Handler for delete */
  onDelete?: () => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function ChatThreadHeader({
  title,
  gameName,
  agentName,
  editableTitle = true,
  onTitleChange,
  onHistoryToggle,
  onExport,
  onShare,
  onDelete,
  className,
}: ChatThreadHeaderProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editValue, setEditValue] = useState(title);

  const handleStartEdit = useCallback(() => {
    if (!editableTitle || !onTitleChange) return;
    setEditValue(title);
    setIsEditing(true);
  }, [editableTitle, onTitleChange, title]);

  const handleSaveEdit = useCallback(() => {
    const trimmed = editValue.trim();
    if (trimmed && trimmed !== title) {
      onTitleChange?.(trimmed);
    }
    setIsEditing(false);
  }, [editValue, title, onTitleChange]);

  const handleCancelEdit = useCallback(() => {
    setEditValue(title);
    setIsEditing(false);
  }, [title]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter') handleSaveEdit();
      if (e.key === 'Escape') handleCancelEdit();
    },
    [handleSaveEdit, handleCancelEdit]
  );

  return (
    <header
      className={cn(
        'flex items-center gap-3 px-4 py-3',
        'border-b border-border/50 dark:border-border/30',
        'bg-background/95 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none',
        className
      )}
      data-testid="chat-thread-header"
    >
      {/* Back button */}
      <Link
        href="/chat/new"
        className="p-2 rounded-lg hover:bg-muted/50 transition-colors flex-shrink-0"
        aria-label="Torna alla selezione"
        data-testid="header-back-btn"
      >
        <ArrowLeft className="h-5 w-5 text-muted-foreground" />
      </Link>

      {/* Title + metadata */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          {isEditing ? (
            <div className="flex items-center gap-1 flex-1">
              <input
                type="text"
                value={editValue}
                onChange={e => setEditValue(e.target.value)}
                onKeyDown={handleKeyDown}
                className="flex-1 px-2 py-1 text-lg font-semibold font-quicksand bg-transparent border-b-2 border-amber-500 focus:outline-none"
                autoFocus
                data-testid="title-edit-input"
              />
              <button
                onClick={handleSaveEdit}
                className="p-1 rounded hover:bg-green-100 dark:hover:bg-green-500/10"
                aria-label="Salva titolo"
              >
                <Check className="h-4 w-4 text-green-600" />
              </button>
              <button
                onClick={handleCancelEdit}
                className="p-1 rounded hover:bg-red-100 dark:hover:bg-red-500/10"
                aria-label="Annulla"
              >
                <X className="h-4 w-4 text-red-500" />
              </button>
            </div>
          ) : (
            <button
              onClick={handleStartEdit}
              className="group flex items-center gap-1.5 min-w-0"
              disabled={!editableTitle}
              data-testid="title-display"
            >
              <h1 className="text-lg font-semibold font-quicksand truncate">{title}</h1>
              {editableTitle && (
                <Pencil className="h-3.5 w-3.5 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity flex-shrink-0" />
              )}
            </button>
          )}
        </div>

        {/* Metadata line */}
        <div className="flex items-center gap-2 text-xs text-muted-foreground font-nunito mt-0.5">
          {gameName && <span>{gameName}</span>}
          {gameName && agentName && <span>•</span>}
          {agentName && (
            <span className="flex items-center gap-1">
              <Bot className="h-3 w-3" />
              {agentName}
            </span>
          )}
        </div>
      </div>

      {/* Action buttons */}
      <div className="flex items-center gap-1 flex-shrink-0">
        {onHistoryToggle && (
          <button
            onClick={onHistoryToggle}
            className="p-2 rounded-lg hover:bg-muted/50 transition-colors text-muted-foreground hover:text-foreground"
            aria-label="Cronologia chat"
            data-testid="header-history-btn"
          >
            <History className="h-4 w-4" />
          </button>
        )}
        {onExport && (
          <button
            onClick={onExport}
            className="p-2 rounded-lg hover:bg-muted/50 transition-colors text-muted-foreground hover:text-foreground"
            aria-label="Esporta chat"
            data-testid="header-export-btn"
          >
            <Download className="h-4 w-4" />
          </button>
        )}
        {onShare && (
          <button
            onClick={onShare}
            className="p-2 rounded-lg hover:bg-muted/50 transition-colors text-muted-foreground hover:text-foreground"
            aria-label="Condividi chat"
            data-testid="header-share-btn"
          >
            <Share2 className="h-4 w-4" />
          </button>
        )}
        {onDelete && (
          <button
            onClick={onDelete}
            className="p-2 rounded-lg hover:bg-red-100 dark:hover:bg-red-500/10 transition-colors text-muted-foreground hover:text-red-600"
            aria-label="Elimina chat"
            data-testid="header-delete-btn"
          >
            <Trash2 className="h-4 w-4" />
          </button>
        )}
      </div>
    </header>
  );
}
