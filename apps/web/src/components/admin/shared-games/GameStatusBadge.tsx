/**
 * Game Status Badge Component - Issue #2372
 *
 * Displays the publication status of a shared game with appropriate styling.
 * Statuses: Draft, PendingApproval, Published, Archived (string enum)
 */

import { Archive, Clock, Eye, FileText } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { type GameStatus } from '@/lib/api/schemas/shared-games.schemas';

export interface GameStatusBadgeProps {
  /** String status: Draft, PendingApproval, Published, Archived */
  status: GameStatus;
  /** Optional size variant */
  size?: 'sm' | 'default';
}

/**
 * Badge displaying the publication status of a shared game
 */
export function GameStatusBadge({ status, size = 'default' }: GameStatusBadgeProps) {
  const iconClass = size === 'sm' ? 'h-2.5 w-2.5 mr-0.5' : 'h-3 w-3 mr-1';
  const badgeClass = size === 'sm' ? 'text-xs px-1.5 py-0' : '';

  switch (status) {
    case 'Draft':
      return (
        <Badge
          variant="outline"
          className={`bg-yellow-50 text-yellow-700 border-yellow-200 ${badgeClass}`}
        >
          <FileText className={iconClass} />
          Bozza
        </Badge>
      );
    case 'PendingApproval':
      return (
        <Badge
          variant="outline"
          className={`bg-blue-50 text-blue-700 border-blue-200 ${badgeClass}`}
        >
          <Clock className={iconClass} />
          In Approvazione
        </Badge>
      );
    case 'Published':
      return (
        <Badge
          variant="outline"
          className={`bg-green-50 text-green-700 border-green-200 ${badgeClass}`}
        >
          <Eye className={iconClass} />
          Pubblicato
        </Badge>
      );
    case 'Archived':
      return (
        <Badge
          variant="outline"
          className={`bg-muted text-muted-foreground border-border/50 dark:border-border/70 ${badgeClass}`}
        >
          <Archive className={iconClass} />
          Archiviato
        </Badge>
      );
    default:
      return (
        <Badge variant="outline" className={badgeClass}>
          Sconosciuto
        </Badge>
      );
  }
}

/**
 * Get status label text
 */
export function getStatusLabel(status: GameStatus): string {
  switch (status) {
    case 'Draft':
      return 'Bozza';
    case 'PendingApproval':
      return 'In Approvazione';
    case 'Published':
      return 'Pubblicato';
    case 'Archived':
      return 'Archiviato';
    default:
      return 'Sconosciuto';
  }
}

/**
 * Get status color class for custom styling
 */
export function getStatusColorClass(status: GameStatus): string {
  switch (status) {
    case 'Draft':
      return 'text-yellow-700 bg-yellow-50';
    case 'PendingApproval':
      return 'text-blue-700 bg-blue-50';
    case 'Published':
      return 'text-green-700 bg-green-50';
    case 'Archived':
      return 'text-muted-foreground bg-muted';
    default:
      return 'text-muted-foreground bg-muted';
  }
}
