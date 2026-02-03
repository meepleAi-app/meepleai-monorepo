'use client';

/**
 * Proposals Client Component (Issue #3182)
 *
 * Main client component for editor agent typology proposal management.
 * Orchestrates:
 * - ProposalsList (list, filters, status badges)
 * - Create/Edit/Test navigation
 *
 * Part of Epic #3174 (AI Agent System).
 * Implements authorization check (Editor only).
 */

import { Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { Button } from '@/components/ui/primitives/button';

import { ProposalsList } from './_components/ProposalsList';

/**
 * EditorAuthGuard placeholder
 * TODO: Create proper EditorAuthGuard component
 * For now, check role === 'Editor' or 'Admin'
 */
function EditorAuthGuard({ children, loading, user }: {
  children: React.ReactNode;
  loading: boolean;
  user: { role: string } | null;
}) {
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

export function ProposalsClient() {
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();

  const handleCreate = () => {
    router.push('/editor/agent-proposals/create');
  };

  return (
    <EditorAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        <div className="mb-6 flex items-start justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">My Typology Proposals</h1>
            <p className="text-muted-foreground mt-2">
              Create, test, and submit AI agent typologies for approval
            </p>
          </div>
          <div className="flex gap-2">
            <Button onClick={handleCreate}>
              <Plus className="h-4 w-4 mr-2" />
              Create Proposal
            </Button>
          </div>
        </div>

        <ProposalsList />
      </div>
    </EditorAuthGuard>
  );
}
