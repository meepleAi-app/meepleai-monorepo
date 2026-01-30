'use client';

/**
 * Typologies Client Component (Issue #3179)
 *
 * Main client component for admin agent typology management.
 * Orchestrates:
 * - TypologyTable (list, filters, pagination)
 * - Modal dialogs for actions (future)
 *
 * Part of Epic #3174 (AI Agent System).
 * Implements authorization check (Admin/Editor only).
 */

import { Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuth } from '@/components/auth/AuthProvider';
import { Button } from '@/components/ui/primitives/button';

import { TypologyTable } from './_components/TypologyTable';

export function TypologiesClient() {
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();

  const handleCreate = () => {
    router.push('/admin/agent-typologies/create');
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        <div className="mb-6 flex items-start justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Agent Typologies</h1>
            <p className="text-muted-foreground mt-2">
              Manage AI agent typologies and review community proposals
            </p>
          </div>
          <Button onClick={handleCreate}>
            <Plus className="h-4 w-4 mr-2" />
            Create New
          </Button>
        </div>

        <TypologyTable />
      </div>
    </AdminAuthGuard>
  );
}
