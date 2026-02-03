'use client';

/**
 * ProposalsTable Component (Issue #3182)
 *
 * Table displaying typology proposals with:
 * - Status badges
 * - Action buttons (Edit/Test/Submit)
 * - Rejection reason display
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Edit, TestTube, Send, Eye } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';
import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

import { RejectionReasonAlert } from './RejectionReasonAlert';
import { StatusBadge } from './StatusBadge';

interface ProposalsTableProps {
  proposals: Typology[];
}

export function ProposalsTable({ proposals }: ProposalsTableProps) {
  const router = useRouter();
  const queryClient = useQueryClient();

  // Submit for approval mutation
  const submitMutation = useMutation({
    mutationFn: async ({ id, proposal }: { id: string; proposal: Typology }) => {
      // Submit for approval (Draft → PendingReview)
      await agentTypologiesApi.submitForApproval(id, proposal);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['editor-proposals'] });
      toast.success('Submitted for approval', {
        description: 'Your proposal has been submitted for admin review.',
      });
    },
    onError: (error) => {
      toast.error('Submission failed', {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
    },
  });

  const handleEdit = (id: string) => {
    router.push(`/editor/agent-proposals/${id}/edit`);
  };

  const handleTest = (id: string) => {
    router.push(`/editor/agent-proposals/${id}/test`);
  };

  const handleSubmit = async (proposal: Typology) => {
    if (confirm(`Submit "${proposal.name}" for admin approval?`)) {
      submitMutation.mutate({ id: proposal.id, proposal });
    }
  };

  const handleView = (id: string) => {
    router.push(`/admin/agent-typologies/${id}`);
  };

  if (proposals.length === 0) {
    return (
      <div className="border border-dashed rounded-lg p-8 text-center text-muted-foreground">
        No proposals match your filters
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {proposals.map((proposal) => (
        <div
          key={proposal.id}
          className="border rounded-lg p-4 hover:bg-muted/50 transition-colors"
        >
          <div className="flex items-start justify-between gap-4">
            {/* Left: Info */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-3 mb-2">
                <h3 className="font-medium text-lg truncate">{proposal.name}</h3>
                <StatusBadge status={proposal.status} />
              </div>
              <p className="text-sm text-muted-foreground line-clamp-2 mb-3">
                {proposal.description}
              </p>
              <div className="flex items-center gap-4 text-xs text-muted-foreground">
                <span>Strategy: {proposal.defaultStrategyName}</span>
                <span>•</span>
                <span>Created: {new Date(proposal.createdAt).toLocaleDateString()}</span>
                {proposal.approvedAt && (
                  <>
                    <span>•</span>
                    <span>Approved: {new Date(proposal.approvedAt).toLocaleDateString()}</span>
                  </>
                )}
              </div>

              {/* Rejection reason */}
              {proposal.status === 'Rejected' && (
                <div className="mt-3">
                  <RejectionReasonAlert reason="Rejection reason not yet implemented in schema" />
                </div>
              )}
            </div>

            {/* Right: Actions */}
            <div className="flex items-center gap-2">
              {proposal.status === 'Draft' && (
                <>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handleEdit(proposal.id)}
                  >
                    <Edit className="h-4 w-4 mr-1" />
                    Edit
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handleTest(proposal.id)}
                  >
                    <TestTube className="h-4 w-4 mr-1" />
                    Test
                  </Button>
                  <Button
                    variant="default"
                    size="sm"
                    onClick={() => handleSubmit(proposal)}
                    disabled={submitMutation.isPending}
                  >
                    <Send className="h-4 w-4 mr-1" />
                    Submit
                  </Button>
                </>
              )}

              {proposal.status === 'PendingReview' && (
                <div className="text-sm text-muted-foreground px-3 py-1 bg-yellow-50 dark:bg-yellow-950 rounded">
                  Awaiting Admin Review
                </div>
              )}

              {(proposal.status === 'Approved' || proposal.status === 'Rejected') && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => handleView(proposal.id)}
                >
                  <Eye className="h-4 w-4 mr-1" />
                  View
                </Button>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
