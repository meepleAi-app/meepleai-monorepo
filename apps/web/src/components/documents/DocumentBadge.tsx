/**
 * Document Badge Component
 *
 * Displays a badge showing document type with color coding:
 * - base: blue (primary document)
 * - expansion: purple (expansion rules)
 * - errata: orange (corrections/clarifications)
 * - homerule: green (custom rules)
 *
 * Issue #2051: Multi-document upload frontend components
 */

import * as React from 'react';

import { Badge } from '@/components/ui/data-display/badge';
import { cn } from '@/lib/utils';

export type DocumentType = 'base' | 'expansion' | 'errata' | 'homerule';

export interface DocumentBadgeProps {
  type: DocumentType;
  className?: string;
}

const DOCUMENT_TYPE_CONFIG: Record<
  DocumentType,
  {
    label: string;
    className: string;
  }
> = {
  base: {
    label: 'Base Rules',
    className: 'bg-blue-100 text-blue-800 border-blue-300 hover:bg-blue-200',
  },
  expansion: {
    label: 'Expansion',
    className: 'bg-purple-100 text-purple-800 border-purple-300 hover:bg-purple-200',
  },
  errata: {
    label: 'Errata',
    className: 'bg-orange-100 text-orange-800 border-orange-300 hover:bg-orange-200',
  },
  homerule: {
    label: 'House Rule',
    className: 'bg-green-100 text-green-800 border-green-300 hover:bg-green-200',
  },
};

export function DocumentBadge({ type, className }: DocumentBadgeProps) {
  // eslint-disable-next-line security/detect-object-injection -- type is validated by TypeScript as DocumentType union
  const config = DOCUMENT_TYPE_CONFIG[type];

  return (
    <Badge
      variant="outline"
      className={cn(config.className, className)}
      aria-label={`Document type: ${config.label}`}
    >
      {config.label}
    </Badge>
  );
}
