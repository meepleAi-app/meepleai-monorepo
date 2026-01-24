/**
 * ShareRequestCard Component (Issue #2744)
 *
 * Displays a share request card with:
 * - Game thumbnail
 * - Title + status badge
 * - Contribution type badge
 * - Metadata (created date, document count)
 * - Admin feedback (if any)
 * - Quick actions (view details, update, view game)
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { FileText, Eye, Edit, ExternalLink } from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { ContributionStatusBadge } from './ContributionStatusBadge';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import type { UserShareRequestDto } from '@/lib/api/schemas/share-requests.schemas';

interface ShareRequestCardProps {
  request: UserShareRequestDto;
}

export function ShareRequestCard({ request }: ShareRequestCardProps) {
  const canUpdate = request.status === 'ChangesRequested';
  const hasResult = request.status === 'Approved' && request.resultingSharedGameId;

  const contributionTypeBadge =
    request.contributionType === 'NewGame' ? (
      <Badge variant="default" className="text-xs">
        New Game
      </Badge>
    ) : (
      <Badge variant="secondary" className="text-xs">
        Additional Content
      </Badge>
    );

  return (
    <Card className="flex flex-col h-full hover:shadow-lg transition-shadow">
      <CardHeader className="p-4">
        <div className="flex items-start gap-4">
          {/* Game Thumbnail */}
          <div className="relative h-20 w-20 shrink-0 overflow-hidden rounded-lg bg-muted">
            {request.gameThumbnailUrl ? (
              <Image
                src={request.gameThumbnailUrl}
                alt={request.gameTitle}
                fill
                className="object-cover"
                sizes="80px"
              />
            ) : (
              <div className="flex h-full items-center justify-center text-muted-foreground">
                <FileText className="h-8 w-8" />
              </div>
            )}
          </div>

          {/* Title + Badges */}
          <div className="flex-1 space-y-2">
            <CardTitle className="text-lg line-clamp-1">{request.gameTitle}</CardTitle>
            <div className="flex flex-wrap items-center gap-2">
              <ContributionStatusBadge status={request.status} />
              {contributionTypeBadge}
            </div>
          </div>
        </div>
      </CardHeader>

      <CardContent className="flex-1 space-y-2 p-4 pt-0">
        {/* Metadata */}
        <CardDescription className="text-xs">
          Submitted {formatDistanceToNow(new Date(request.createdAt), { addSuffix: true })}
          {request.attachedDocumentCount > 0 && (
            <>
              {' '}
              • <FileText className="inline h-3 w-3" /> {request.attachedDocumentCount} document
              {request.attachedDocumentCount !== 1 ? 's' : ''}
            </>
          )}
        </CardDescription>

        {/* User Notes (if any) */}
        {request.userNotes && (
          <div className="rounded-md bg-muted/50 p-2">
            <p className="text-xs font-medium text-muted-foreground">Your Notes</p>
            <p className="mt-1 text-sm line-clamp-2">{request.userNotes}</p>
          </div>
        )}

        {/* Admin Feedback (if any) */}
        {request.adminFeedback && (
          <div className="rounded-md border border-orange-200 bg-orange-50 p-2">
            <p className="text-xs font-medium text-orange-700">Admin Feedback</p>
            <p className="mt-1 text-sm text-orange-800 line-clamp-2">{request.adminFeedback}</p>
          </div>
        )}

        {/* Resolved Date (if approved/rejected) */}
        {request.resolvedAt && (
          <CardDescription className="text-xs">
            Resolved {formatDistanceToNow(new Date(request.resolvedAt), { addSuffix: true })}
          </CardDescription>
        )}
      </CardContent>

      <CardFooter className="flex flex-wrap gap-2 p-4 pt-0">
        {/* View Game (if approved) */}
        {hasResult && (
          <Button asChild variant="default" size="sm">
            <Link href={`/games/catalog/${request.resultingSharedGameId}`}>
              <ExternalLink className="mr-1.5 h-3.5 w-3.5" />
              View Game
            </Link>
          </Button>
        )}

        {/* Update (if changes requested) */}
        {canUpdate && (
          <Button asChild variant="outline" size="sm">
            <Link href={`/contributions/requests/${request.id}/edit`}>
              <Edit className="mr-1.5 h-3.5 w-3.5" />
              Update
            </Link>
          </Button>
        )}

        {/* View Details */}
        <Button asChild variant="outline" size="sm" className="ml-auto">
          <Link href={`/contributions/requests/${request.id}`}>
            <Eye className="mr-1.5 h-3.5 w-3.5" />
            Details
          </Link>
        </Button>
      </CardFooter>
    </Card>
  );
}
