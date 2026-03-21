/**
 * Create Proposal Page - Issue #3182
 *
 * Page for editors to create new agent typology proposals.
 * Route: /editor/agent-proposals/create
 * Proposals are created as Draft status and require Admin approval.
 */

'use client';

import { Loader2 } from 'lucide-react';
import dynamic from 'next/dynamic';
import { useRouter } from 'next/navigation';

import { useAuthUser } from '@/components/auth/AuthProvider';
import { EditorAuthGuard } from '@/components/auth/EditorAuthGuard';

// Lazy load TypologyForm — contains Monaco editor (~2.5MB)
const TypologyForm = dynamic(
  () =>
    import('@/components/admin/agent-typologies/TypologyForm').then(mod => ({
      default: mod.TypologyForm,
    })),
  {
    loading: () => (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    ),
    ssr: false,
  }
);

function CreateProposalClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  const handleSuccess = () => {
    // Redirect to proposals list after successful creation
    router.push('/editor/agent-proposals');
  };

  const handleCancel = () => {
    router.back();
  };

  return (
    <EditorAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-5xl">
        {/* Header */}
        <div className="mb-6">
          <h1 className="text-3xl font-bold">Create Typology Proposal</h1>
          <p className="text-muted-foreground mt-1">
            Define a new AI agent typology with prompt template and RAG strategy. Your proposal will
            be created as Draft and can be tested before submitting for approval.
          </p>
        </div>

        {/* Form Container - Reuse admin form with isProposal flag */}
        <TypologyForm
          typology={null}
          onSubmit={handleSuccess}
          onCancel={handleCancel}
          isProposal={true}
        />
      </div>
    </EditorAuthGuard>
  );
}

export default function CreateProposalPage() {
  return <CreateProposalClient />;
}
