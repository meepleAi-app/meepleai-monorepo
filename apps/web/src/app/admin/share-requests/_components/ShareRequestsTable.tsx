import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { ShareRequestsTableRow } from './ShareRequestsTableRow';
import type { AdminShareRequestDto } from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Share Requests Table Component
 *
 * Displays paginated list of share requests in a table format.
 * Each row shows key information and action button to review.
 *
 * Columns:
 * - Game (thumbnail + title + BGG ID)
 * - Contributor (avatar + name + total contributions)
 * - Type (NewGame / Additional Content)
 * - Status (badge with color coding)
 * - Waiting Time (with warning for >7 days)
 * - Documents (count with icon)
 * - Actions (Review button)
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface ShareRequestsTableProps {
  requests: AdminShareRequestDto[];
  onReview: (id: string) => void;
}

export function ShareRequestsTable({ requests, onReview }: ShareRequestsTableProps): JSX.Element {
  if (requests.length === 0) {
    return (
      <div className="text-center py-12 text-muted-foreground">
        <p>No share requests found.</p>
        <p className="text-sm mt-1">Try adjusting your filters.</p>
      </div>
    );
  }

  return (
    <div className="border rounded-lg">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Game</TableHead>
            <TableHead>Contributor</TableHead>
            <TableHead>Type</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Waiting</TableHead>
            <TableHead>Documents</TableHead>
            <TableHead className="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {requests.map((request) => (
            <ShareRequestsTableRow key={request.id} request={request} onReview={onReview} />
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
