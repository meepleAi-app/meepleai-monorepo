/**
 * EditorAuthGuard - Restricts access to Editor and Admin roles
 *
 * Extracted from editor proposal pages (Issue #3182 TODO).
 * Shows loading state, access denied, or renders children.
 */

'use client';

import type { ReactNode } from 'react';

interface EditorAuthGuardProps {
  children: ReactNode;
  loading: boolean;
  user: { role: string; id?: string } | null;
}

export function EditorAuthGuard({ children, loading, user }: EditorAuthGuardProps) {
  if (loading) {
    return <div className="container mx-auto p-6">Loading...</div>;
  }

  if (!user || (user.role !== 'Editor' && user.role !== 'Admin')) {
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
