/**
 * ShareRequestCard Component (Issue #2744)
 * Issue #4860: Migrated to MeepleCard design system
 *
 * Displays a share request card using MeepleCard entity="game" variant="list"
 * with request metadata, status badge, and contextual quick actions.
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { FileText, Eye, Edit, ExternalLink } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { UserShareRequestDto } from '@/lib/api/schemas/share-requests.schemas';

interface ShareRequestCardProps {
  request: UserShareRequestDto;
}

const statusLabels: Record<string, string> = {
  Pending: 'In attesa',
  InReview: 'In revisione',
  ChangesRequested: 'Modifiche richieste',
  Approved: 'Approvato',
  Rejected: 'Rifiutato',
  Withdrawn: 'Ritirato',
};

export function ShareRequestCard({ request }: ShareRequestCardProps) {
  const router = useRouter();

  const canUpdate = request.status === 'ChangesRequested';
  const hasResult = request.status === 'Approved' && request.resultingSharedGameId;

  const contributionLabel =
    request.contributionType === 'NewGame' ? 'New Game' : 'Additional Content';

  const submittedAgo = formatDistanceToNow(new Date(request.createdAt), { addSuffix: true });

  const subtitle = `${contributionLabel} \u00b7 ${submittedAgo}`;

  const metadata: MeepleCardMetadata[] = [
    ...(request.attachedDocumentCount > 0
      ? [
          {
            icon: FileText,
            label: `${request.attachedDocumentCount} doc${request.attachedDocumentCount !== 1 ? 's' : ''}`,
          },
        ]
      : []),
  ];

  const quickActions = [
    ...(hasResult
      ? [
          {
            icon: ExternalLink,
            label: 'View Game',
            onClick: () => router.push(`/library/games/${request.resultingSharedGameId}`),
          },
        ]
      : []),
    ...(canUpdate
      ? [
          {
            icon: Edit,
            label: 'Update',
            onClick: () => router.push(`/contributions/requests/${request.id}/edit`),
          },
        ]
      : []),
    {
      icon: Eye,
      label: 'Details',
      onClick: () => router.push(`/contributions/requests/${request.id}`),
    },
  ];

  // Build preview from notes and admin feedback
  const previewParts: string[] = [];
  if (request.userNotes) previewParts.push(request.userNotes);
  if (request.adminFeedback) previewParts.push(`Admin: ${request.adminFeedback}`);
  if (request.resolvedAt) {
    const resolvedAgo = formatDistanceToNow(new Date(request.resolvedAt), { addSuffix: true });
    previewParts.push(`Resolved ${resolvedAgo}`);
  }

  return (
    <MeepleCard
      entity="game"
      variant="list"
      title={request.gameTitle}
      subtitle={subtitle}
      imageUrl={request.gameThumbnailUrl || undefined}
      badge={statusLabels[request.status] || request.status}
      metadata={metadata.length > 0 ? metadata : undefined}
      quickActions={quickActions}
      showPreview={previewParts.length > 0}
      previewData={previewParts.length > 0 ? { description: previewParts.join('\n\n') } : undefined}
      data-testid={`share-request-card-${request.id}`}
    />
  );
}
