import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/data-display/avatar';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { TableCell, TableRow } from '@/components/ui/data-display/table';
import { formatDistanceToNow } from 'date-fns';
import { FileText, Eye } from 'lucide-react';
import { ShareRequestStatusBadge } from './ShareRequestStatusBadge';
import { LockStatusBadge } from './LockStatusBadge';
import type { AdminShareRequestDto } from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Share Requests Table Row Component
 *
 * Displays a single share request in the admin queue table.
 *
 * Features:
 * - Game preview (thumbnail, title, BGG ID)
 * - Contributor info (avatar, name, contribution count)
 * - Contribution type badge
 * - Status badge with lock indicator
 * - Waiting time (highlighted if >7 days)
 * - Document count
 * - Review action button
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface ShareRequestsTableRowProps {
  request: AdminShareRequestDto;
  onReview: (id: string) => void;
}

export function ShareRequestsTableRow({ request, onReview }: ShareRequestsTableRowProps): JSX.Element {
  const waitingTime = Date.now() - new Date(request.createdAt).getTime();
  const waitingDays = Math.floor(waitingTime / (1000 * 60 * 60 * 24));
  const isWaitingLong = waitingDays > 7;

  return (
    <TableRow>
      {/* Game Column */}
      <TableCell>
        <div className="flex items-center gap-3">
          {request.gameThumbnailUrl && (
            <img
              src={request.gameThumbnailUrl}
              alt={request.gameTitle}
              className="h-12 w-12 rounded object-cover border"
            />
          )}
          <div className="min-w-0">
            <p className="font-medium truncate">{request.gameTitle}</p>
            {request.bggId && (
              <p className="text-xs text-muted-foreground">BGG #{request.bggId}</p>
            )}
          </div>
        </div>
      </TableCell>

      {/* Contributor Column */}
      <TableCell>
        <div className="flex items-center gap-2">
          <Avatar className="h-8 w-8">
            <AvatarImage src={request.userAvatarUrl ?? undefined} alt={request.userName} />
            <AvatarFallback className="text-xs">
              {request.userName.substring(0, 2).toUpperCase()}
            </AvatarFallback>
          </Avatar>
          <div>
            <p className="font-medium text-sm">{request.userName}</p>
            <p className="text-xs text-muted-foreground">
              {request.userTotalContributions} contributions
            </p>
          </div>
        </div>
      </TableCell>

      {/* Type Column */}
      <TableCell>
        <Badge variant="outline">
          {request.contributionType === 'NewGame' ? 'New Game' : 'Additional'}
        </Badge>
      </TableCell>

      {/* Status Column */}
      <TableCell>
        <div className="space-y-1">
          <ShareRequestStatusBadge status={request.status} />
          {request.isInReview && (
            <LockStatusBadge
              lockStatus={{
                isLocked: request.isInReview,
                isLockedByCurrentAdmin: false, // We don't know this from the list DTO
                lockedByAdminId: request.reviewingAdminId,
                lockedByAdminName: request.reviewingAdminName,
                lockExpiresAt: null, // Not available in list DTO
              }}
            />
          )}
        </div>
      </TableCell>

      {/* Waiting Time Column */}
      <TableCell>
        <p className={`text-sm ${isWaitingLong ? 'text-destructive font-medium' : ''}`}>
          {formatDistanceToNow(new Date(request.createdAt))}
        </p>
      </TableCell>

      {/* Documents Column */}
      <TableCell>
        {request.attachedDocumentCount > 0 && (
          <Badge variant="outline" className="gap-1">
            <FileText className="h-3 w-3" />
            {request.attachedDocumentCount}
          </Badge>
        )}
      </TableCell>

      {/* Actions Column */}
      <TableCell className="text-right">
        <Button size="sm" variant="outline" onClick={() => onReview(request.id)}>
          <Eye className="mr-2 h-4 w-4" />
          Review
        </Button>
      </TableCell>
    </TableRow>
  );
}