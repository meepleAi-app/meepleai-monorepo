/**
 * Bulk Actions Toolbar (Issue #4118)
 * Appears when users are selected in admin user table
 */

'use client';

import React from 'react';
import { Trash2, Mail, UserCog, Download, Upload } from 'lucide-react';
import { Button } from '@/components/ui/primitives/button';

interface BulkActionsToolbarProps {
  selectedCount: number;
  onPasswordReset: () => void;
  onRoleChange: () => void;
  onDelete: () => void;
  onExport: () => void;
  onImport: () => void;
  onClearSelection: () => void;
}

export function BulkActionsToolbar({
  selectedCount,
  onPasswordReset,
  onRoleChange,
  onDelete,
  onExport,
  onImport,
  onClearSelection,
}: BulkActionsToolbarProps) {
  if (selectedCount === 0) return null;

  return (
    <div className="sticky top-0 z-50 bg-primary text-primary-foreground px-4 py-3 rounded-lg mb-4 flex items-center gap-4 shadow-lg">
      <span className="font-medium">{selectedCount} selected</span>

      <div className="flex gap-2 ml-auto">
        <Button size="sm" variant="secondary" onClick={onPasswordReset} className="gap-1">
          <Mail className="h-4 w-4" />
          Reset Passwords
        </Button>

        <Button size="sm" variant="secondary" onClick={onRoleChange} className="gap-1">
          <UserCog className="h-4 w-4" />
          Change Role
        </Button>

        <Button size="sm" variant="secondary" onClick={onExport} className="gap-1">
          <Download className="h-4 w-4" />
          Export
        </Button>

        <Button size="sm" variant="secondary" onClick={onImport} className="gap-1">
          <Upload className="h-4 w-4" />
          Import
        </Button>

        <Button size="sm" variant="destructive" onClick={onDelete} className="gap-1">
          <Trash2 className="h-4 w-4" />
          Delete
        </Button>

        <Button size="sm" variant="ghost" onClick={onClearSelection}>
          Clear
        </Button>
      </div>
    </div>
  );
}
