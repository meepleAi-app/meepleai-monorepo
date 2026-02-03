/**
 * Edit Proposal Page - Issue #3182
 *
 * Page for editors to edit their own Draft proposals.
 * Route: /editor/agent-proposals/[id]/edit
 * Only Draft status proposals can be edited.
 */

'use client';

import { useQuery } from '@tanstack/react-query';
import { useRouter, useParams } from 'next/navigation';

import { TypologyForm } from '@/components/admin/agent-typologies/TypologyForm';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';

/**
 * EditorAuthGuard placeholder
 * TODO: Extract to shared component
 */
function EditorAuthGuard({ children, loading, user }: {
  children: React.ReactNode;
  loading: boolean;
  user: { role: string; id: string } | null;
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

function EditProposalClient() {
  const router = useRouter();
  const params = useParams();
  const { user, loading: authLoading } = useAuthUser();
  const typologyId = params?.id as string;

  // Fetch typology
  const { data: typology, isLoading, error } = useQuery({
    queryKey: ['typology', typologyId],
    queryFn: () => agentTypologiesApi.getById(typologyId),
    enabled: !!typologyId,
  });

  const handleSuccess = () => {
    router.push('/editor/agent-proposals');
  };

  const handleCancel = () => {
    router.back();
  };

  // Loading state
  if (authLoading || isLoading) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">Loading...</div>
      </div>
    );
  }

  // Error state
  if (error || !typology) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">
          <h2 className="text-2xl font-bold mb-4">Proposal Not Found</h2>
          <p className="text-muted-foreground">
            {error instanceof Error ? error.message : 'The proposal you are looking for does not exist.'}
          </p>
        </div>
      </div>
    );
  }

  // Authorization check: only creator can edit
  if (user && typology.createdBy !== user.id) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">
          <h2 className="text-2xl font-bold mb-4">Not Authorized</h2>
          <p className="text-muted-foreground">
            You can only edit your own proposals.
          </p>
        </div>
      </div>
    );
  }

  // Status check: only Draft can be edited
  if (typology.status !== 'Draft') {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">
          <h2 className="text-2xl font-bold mb-4">Cannot Edit</h2>
          <p className="text-muted-foreground">
            Only Draft proposals can be edited. This proposal is currently: {typology.status}
          </p>
        </div>
      </div>
    );
  }

  return (
    <EditorAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-5xl">
        {/* Header */}
        <div className="mb-6">
          <h1 className="text-3xl font-bold">Edit Proposal</h1>
          <p className="text-muted-foreground mt-1">
            Update your Draft proposal. Changes are saved immediately.
          </p>
        </div>

        {/* Form Container */}
        <TypologyForm
          typology={typology}
          onSubmit={handleSuccess}
          onCancel={handleCancel}
          isProposal={true}
        />
      </div>
    </EditorAuthGuard>
  );
}

export default function EditProposalPage() {
  return <EditProposalClient />;
}
