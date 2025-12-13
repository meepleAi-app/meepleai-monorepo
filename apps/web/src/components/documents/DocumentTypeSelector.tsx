/**
 * Document Type Selector Component
 *
 * Dropdown select for choosing document type with color-coded badges.
 * Used in multi-document upload workflow to classify each PDF.
 *
 * Issue #2051: Multi-document upload frontend components
 */

import * as React from 'react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { DocumentBadge, type DocumentType } from './DocumentBadge';

export type { DocumentType };

export interface DocumentTypeSelectorProps {
  value: DocumentType;
  onChange: (value: DocumentType) => void;
  disabled?: boolean;
  className?: string;
}

const DOCUMENT_TYPES: Array<{ value: DocumentType; label: string; description: string }> = [
  {
    value: 'base',
    label: 'Base Rules',
    description: 'Core rulebook for the game',
  },
  {
    value: 'expansion',
    label: 'Expansion',
    description: 'Additional rules for expansions',
  },
  {
    value: 'errata',
    label: 'Errata',
    description: 'Official corrections and clarifications',
  },
  {
    value: 'homerule',
    label: 'House Rule',
    description: 'Custom variant rules',
  },
];

export function DocumentTypeSelector({
  value,
  onChange,
  disabled = false,
  className,
}: DocumentTypeSelectorProps) {
  return (
    <Select value={value} onValueChange={onChange} disabled={disabled}>
      <SelectTrigger className={className} aria-label="Select document type">
        <SelectValue>
          <DocumentBadge type={value} />
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {DOCUMENT_TYPES.map(docType => (
          <SelectItem key={docType.value} value={docType.value}>
            <div className="flex flex-col gap-1">
              <DocumentBadge type={docType.value} />
              <span className="text-xs text-muted-foreground">{docType.description}</span>
            </div>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
