/**
 * ContextChip Component - Game Context Badge with Sources
 *
 * Displays the current game context with document sources (PDF, FAQ, Wiki).
 * Follows Playful Boardroom design from wireframes.
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md (Page 4 - Chat AI)
 * @issue #1840 (PAGE-004)
 */

import React from 'react';

import { X } from 'lucide-react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface DocumentSource {
  /** Source type */
  type: 'PDF' | 'FAQ' | 'Wiki';
  /** Number of documents/entries for this source type */
  count?: number;
}

export interface ContextChipProps {
  /** Game name */
  gameName: string;
  /** Game emoji icon (default: 🎲) */
  gameEmoji?: string;
  /** Document sources available */
  sources?: DocumentSource[];
  /** Remove context callback */
  onRemove?: () => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

/**
 * ContextChip - Game context badge with sources
 *
 * Features:
 * - Game name with emoji
 * - Document sources (PDF, FAQ, Wiki)
 * - Remove button
 * - Playful Boardroom styling (accent colors)
 *
 * @example
 * ```tsx
 * <ContextChip
 *   gameName="Catan"
 *   gameEmoji="🎲"
 *   sources={[
 *     { type: 'PDF', count: 1 },
 *     { type: 'FAQ', count: 15 },
 *     { type: 'Wiki' }
 *   ]}
 *   onRemove={() => console.log('Context removed')}
 * />
 * ```
 */
export function ContextChip({
  gameName,
  gameEmoji = '🎲',
  sources = [],
  onRemove,
  className,
}: ContextChipProps) {
  return (
    <div
      className={cn(
        'flex flex-col gap-2 p-3 bg-accent/10 border border-accent rounded-lg',
        className
      )}
      role="region"
      aria-label={`Game context: ${gameName}`}
    >
      {/* Game context header */}
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2 flex-1 min-w-0">
          <span className="text-lg flex-shrink-0" role="img" aria-label="Game icon">
            {gameEmoji}
          </span>
          <span className="text-sm font-medium text-foreground truncate">
            Context: <span className="font-semibold">{gameName}</span>
          </span>
        </div>

        {/* Remove button */}
        {onRemove && (
          <button
            onClick={onRemove}
            className="flex-shrink-0 p-1 rounded-md hover:bg-accent/20 transition-colors"
            aria-label={`Remove ${gameName} context`}
            title="Remove context"
          >
            <X className="w-4 h-4 text-accent" />
          </button>
        )}
      </div>

      {/* Document sources */}
      {sources.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          {sources.map((source, index) => (
            <SourceBadge key={`${source.type}-${index}`} source={source} />
          ))}
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Sub-Components
// ============================================================================

interface SourceBadgeProps {
  source: DocumentSource;
}

/**
 * SourceBadge - Individual document source badge
 */
function SourceBadge({ source }: SourceBadgeProps) {
  const { type, count } = source;

  // Badge content
  const label = count !== undefined && count > 0 ? `${type} ${count}` : type;

  // Badge styling by type
  const getBadgeStyles = () => {
    switch (type) {
      case 'PDF':
        return 'bg-orange-100 text-orange-700 border-orange-300';
      case 'FAQ':
        return 'bg-blue-100 text-blue-700 border-blue-300';
      case 'Wiki':
        return 'bg-green-100 text-green-700 border-green-300';
      default:
        return 'bg-muted text-muted-foreground border-border/50 dark:border-border/70';
    }
  };

  return (
    <span
      className={cn(
        'inline-flex items-center px-2 py-1 rounded-md text-xs font-medium border',
        getBadgeStyles()
      )}
      role="status"
      aria-label={`${type} source${count ? `: ${count} documents` : ''}`}
    >
      {label}
    </span>
  );
}
