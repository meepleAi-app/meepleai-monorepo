/**
 * VersionBadge Component - Issue #2391 Sprint 1
 *
 * Badge component for displaying document version with active status indicator.
 */

import { CheckCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

export interface VersionBadgeProps {
  version: string;
  isActive: boolean;
  className?: string;
}

export function VersionBadge({ version, isActive, className }: VersionBadgeProps) {
  return (
    <Badge variant={isActive ? 'default' : 'outline'} className={cn('gap-1.5', className)}>
      <span className="font-mono text-xs">v{version}</span>
      {isActive && (
        <>
          <CheckCircle className="h-3 w-3" />
          <span className="text-xs">Attiva</span>
        </>
      )}
    </Badge>
  );
}
