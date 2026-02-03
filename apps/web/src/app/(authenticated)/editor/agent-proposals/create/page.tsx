/**
 * Create Proposal Page - Issue #3182
 *
 * Page for editors to create new agent typology proposals.
 * Route: /editor/agent-proposals/create
 * Proposals are created as Draft status and require Admin approval.
 */

'use client';

import { useRouter } from 'next/navigation';

import { TypologyForm } from '@/components/admin/agent-typologies/TypologyForm';
import { useAuthUser } from '@/components/auth/AuthProvider';

/**
 * EditorAuthGuard placeholder
 * TODO: Extract to shared component
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
            Define a new AI agent typology with prompt template and RAG strategy.
            Your proposal will be created as Draft and can be tested before submitting for approval.
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
