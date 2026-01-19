/**
 * Game Status Badge Component - Issue #2372
 *
 * Displays the publication status of a shared game with appropriate styling.
 * Statuses: Draft (0), Published (1), Archived (2)
 */

import { Archive, Eye, FileText } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

export interface GameStatusBadgeProps {
  /** Numeric status: 0=Draft, 1=Published, 2=Archived */
  status: number;
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
    case 0: // Draft
      return (
        <Badge
          variant="outline"
          className={`bg-yellow-50 text-yellow-700 border-yellow-200 ${badgeClass}`}
        >
          <FileText className={iconClass} />
          Bozza
        </Badge>
      );
    case 1: // Published
      return (
        <Badge
          variant="outline"
          className={`bg-green-50 text-green-700 border-green-200 ${badgeClass}`}
        >
          <Eye className={iconClass} />
          Pubblicato
        </Badge>
      );
    case 2: // Archived
      return (
        <Badge
          variant="outline"
          className={`bg-gray-50 text-gray-700 border-gray-200 ${badgeClass}`}
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
export function getStatusLabel(status: number): string {
  switch (status) {
    case 0:
      return 'Bozza';
    case 1:
      return 'Pubblicato';
    case 2:
      return 'Archiviato';
    default:
      return 'Sconosciuto';
  }
}

/**
 * Get status color class for custom styling
 */
export function getStatusColorClass(status: number): string {
  switch (status) {
    case 0:
      return 'text-yellow-700 bg-yellow-50';
    case 1:
      return 'text-green-700 bg-green-50';
    case 2:
      return 'text-gray-700 bg-gray-50';
    default:
      return 'text-muted-foreground bg-muted';
  }
}
