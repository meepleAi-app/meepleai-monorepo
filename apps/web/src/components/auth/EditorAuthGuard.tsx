/**
 * EditorAuthGuard - Restricts access to Editor and Admin roles
 *
 * Extracted from editor proposal pages (resolved Issue #3182).
 * Uses centralized isAdminRole() for case-insensitive admin/superadmin check.
 */

'use client';

import type { ReactNode } from 'react';

import { isAdminRole } from '@/lib/utils/roles';

interface EditorAuthGuardProps {
  children: ReactNode;
  loading: boolean;
  user: { role: string; id?: string } | null;
}

function isEditorOrAdmin(role: string | null | undefined): boolean {
  if (!role) return false;
  return role.toLowerCase() === 'editor' || isAdminRole(role);
}

export function EditorAuthGuard({ children, loading, user }: EditorAuthGuardProps) {
  if (loading) {
    return <div className="container mx-auto p-6">Loading...</div>;
  }

  if (!user || !isEditorOrAdmin(user.role)) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">
          <h2 className="text-2xl font-bold mb-4">Access Denied</h2>
          <p className="text-muted-foreground">
            This page is only accessible to Editors and Administrators.
          </p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
