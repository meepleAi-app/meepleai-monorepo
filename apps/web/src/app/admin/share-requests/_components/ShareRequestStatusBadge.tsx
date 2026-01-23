import { Badge } from '@/components/ui/badge';
import { FileText, Eye, AlertCircle, CheckCircle, XCircle, MessageSquare } from 'lucide-react';
import type { ShareRequestStatus } from '@/lib/api/schemas/share-requests.schemas';

/**
 * Share Request Status Badge Component
 *
 * Displays visual status indicator for share requests with icon and color coding.
 *
 * Status mapping:
 * - Pending: Yellow (awaiting review)
 * - InReview: Blue (currently being reviewed)
 * - ChangesRequested: Orange (user action needed)
 * - Approved: Green (accepted)
 * - Rejected: Red (declined)
 * - Withdrawn: Gray (user cancelled)
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface ShareRequestStatusBadgeProps {
  status: ShareRequestStatus;
  className?: string;
}

export function ShareRequestStatusBadge({ status, className }: ShareRequestStatusBadgeProps): JSX.Element {
  const config = getStatusConfig(status);

  return (
    <Badge variant="outline" className={`${config.className} ${className ?? ''}`} data-testid="status-badge" data-status={status}>
      <config.icon className="mr-1 h-3 w-3" />
      {config.label}
    </Badge>
  );
}

function getStatusConfig(status: ShareRequestStatus) {
  switch (status) {
    case 'Pending':
      return {
        label: 'Pending Review',
        icon: FileText,
        className: 'border-yellow-300 bg-yellow-50 text-yellow-700',
      };
    case 'InReview':
      return {
        label: 'In Review',
        icon: Eye,
        className: 'border-blue-300 bg-blue-50 text-blue-700',
      };
    case 'ChangesRequested':
      return {
        label: 'Changes Requested',
        icon: MessageSquare,
        className: 'border-orange-300 bg-orange-50 text-orange-700',
      };
    case 'Approved':
      return {
        label: 'Approved',
        icon: CheckCircle,
        className: 'border-green-300 bg-green-50 text-green-700',
      };
    case 'Rejected':
      return {
        label: 'Rejected',
        icon: XCircle,
        className: 'border-red-300 bg-red-50 text-red-700',
      };
    case 'Withdrawn':
      return {
        label: 'Withdrawn',
        icon: AlertCircle,
        className: 'border-gray-300 bg-gray-50 text-gray-700',
      };
    default:
      return {
        label: 'Unknown',
        icon: AlertCircle,
        className: 'border-gray-300 bg-gray-50 text-gray-700',
      };
  }
}
