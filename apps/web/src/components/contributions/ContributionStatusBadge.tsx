import {
  CheckCircle2,
  Clock,
  Eye,
  AlertCircle,
  XCircle,
  Undo2,
  type LucideIcon,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import type { ShareRequestStatus } from '@/lib/api/schemas/share-requests.schemas';
import { cn } from '@/lib/utils';

interface StatusConfig {
  label: string;
  variant: 'default' | 'secondary' | 'destructive' | 'outline';
  icon: LucideIcon;
  className: string;
}

const statusConfig: Record<ShareRequestStatus, StatusConfig> = {
  Pending: {
    label: 'Pending',
    variant: 'secondary',
    icon: Clock,
    className: 'border-yellow-200 bg-yellow-50 text-yellow-700 hover:bg-yellow-100',
  },
  InReview: {
    label: 'In Review',
    variant: 'default',
    icon: Eye,
    className: 'border-blue-200 bg-blue-50 text-blue-700 hover:bg-blue-100',
  },
  ChangesRequested: {
    label: 'Changes Requested',
    variant: 'outline',
    icon: AlertCircle,
    className: 'border-orange-200 bg-orange-50 text-orange-700 hover:bg-orange-100',
  },
  Approved: {
    label: 'Approved',
    variant: 'outline',
    icon: CheckCircle2,
    className: 'border-green-200 bg-green-50 text-green-700 hover:bg-green-100',
  },
  Rejected: {
    label: 'Rejected',
    variant: 'destructive',
    icon: XCircle,
    className: 'border-red-200 bg-red-50 text-red-700 hover:bg-red-100',
  },
  Withdrawn: {
    label: 'Withdrawn',
    variant: 'outline',
    icon: Undo2,
    className: 'border-gray-200 bg-gray-50 text-gray-600 hover:bg-gray-100',
  },
};

interface ContributionStatusBadgeProps {
  status: ShareRequestStatus;
  showIcon?: boolean;
  className?: string;
}

export function ContributionStatusBadge({
  status,
  showIcon = true,
  className,
}: ContributionStatusBadgeProps) {
  // eslint-disable-next-line security/detect-object-injection -- Safe: status is a typed ShareRequestStatus enum value
  const config = statusConfig[status];
  const Icon = config.icon;

  return (
    <Badge
      variant={config.variant}
      className={cn('inline-flex items-center gap-1.5', config.className, className)}
    >
      {showIcon && <Icon className="h-3 w-3" />}
      <span>{config.label}</span>
    </Badge>
  );
}
