'use client';

/**
 * ProposalsTable Component (Issue #3182)
 *
 * Table displaying agent definitions for editors.
 * Note: The typology proposal workflow was simplified — definitions are
 * now managed directly. This component is preserved for display purposes.
 */

import { Eye } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import type { AgentDefinitionDto } from '@/lib/api/schemas/agent-definitions.schemas';

import { StatusBadge } from './StatusBadge';

interface ProposalsTableProps {
  proposals: AgentDefinitionDto[];
}

export function ProposalsTable({ proposals }: ProposalsTableProps) {
  const router = useRouter();

  const handleView = (id: string) => {
    router.push(`/admin/agent-definitions/${id}`);
  };

  if (proposals.length === 0) {
    return (
      <div className="border border-dashed rounded-lg p-8 text-center text-muted-foreground">
        No definitions match your filters
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {proposals.map(proposal => (
        <div
          key={proposal.id}
          className="border rounded-lg p-4 hover:bg-muted/50 transition-colors"
        >
          <div className="flex items-start justify-between gap-4">
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-3 mb-2">
                <h3 className="font-medium text-lg truncate">{proposal.name}</h3>
                <StatusBadge status={proposal.isActive ? 'Approved' : 'Draft'} />
              </div>
              <p className="text-sm text-muted-foreground line-clamp-2 mb-3">
                {proposal.description}
              </p>
              <div className="flex items-center gap-4 text-xs text-muted-foreground">
                <span>Strategy: {proposal.strategyName}</span>
                <span>•</span>
                <span>Created: {new Date(proposal.createdAt).toLocaleDateString()}</span>
                {proposal.updatedAt && (
                  <>
                    <span>•</span>
                    <span>Updated: {new Date(proposal.updatedAt).toLocaleDateString()}</span>
                  </>
                )}
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" onClick={() => handleView(proposal.id)}>
                <Eye className="h-4 w-4 mr-1" />
                View
              </Button>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
