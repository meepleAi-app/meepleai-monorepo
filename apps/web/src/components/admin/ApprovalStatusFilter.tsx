'use client';

/**
 * Approval Status Filter Component
 *
 * Filter dropdown for admin games by approval status with counts.
 * Issue #3482: Admin approval status management UI
 */

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import type { ApprovalStatus } from '@/lib/api/schemas/admin.schemas';

interface ApprovalStatusFilterProps {
  value: ApprovalStatus | 'all';
  onChange: (value: ApprovalStatus | 'all') => void;
  counts?: Partial<Record<ApprovalStatus | 'all', number>>;
}

const FILTER_OPTIONS: { value: ApprovalStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'Tutti i Giochi' },
  { value: 'Draft', label: 'Bozza' },
  { value: 'PendingReview', label: 'In Revisione' },
  { value: 'Approved', label: 'Approvati' },
  { value: 'Rejected', label: 'Rifiutati' },
];

export function ApprovalStatusFilter({ value, onChange, counts }: ApprovalStatusFilterProps) {
  return (
    <Select value={value} onValueChange={(val: string) => onChange(val as ApprovalStatus | 'all')}>
      <SelectTrigger className="w-[220px]">
        <SelectValue placeholder="Filtra per stato..." />
      </SelectTrigger>
      <SelectContent>
        {FILTER_OPTIONS.map(option => {
          const count = counts?.[option.value];
          return (
            <SelectItem key={option.value} value={option.value}>
              <div className="flex items-center justify-between gap-4">
                <span>{option.label}</span>
                {count !== undefined && (
                  <span className="text-xs text-slate-500 dark:text-slate-400">({count})</span>
                )}
              </div>
            </SelectItem>
          );
        })}
      </SelectContent>
    </Select>
  );
}
