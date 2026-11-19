'use client';

/**
 * Admin review of one share request (#5076): the game it is about (for a proposal, the user's private game), the
 * catalog game it targets, the contributor, notes and documents, and the review panel.
 */

import type { JSX } from 'react';

import { ArrowLeftIcon, FileTextIcon, Gamepad2Icon } from 'lucide-react';
import Link from 'next/link';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { SafeImg } from '@/components/ui/data-display/safe-img';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useShareRequestDetails } from '@/hooks/queries/useAdminShareRequests';
import type { GameDetailsDto } from '@/lib/api/schemas/admin-share-requests.schemas';

import { ReviewPanel } from './ReviewPanel';
import { CONTRIBUTION_TYPE_LABELS, formatDateTime } from './share-request-labels';
import { ShareRequestStatusBadge } from './ShareRequestStatusBadge';

function players(game: GameDetailsDto): string | null {
  if (game.minPlayers == null && game.maxPlayers == null) return null;
  if (game.minPlayers === game.maxPlayers || game.maxPlayers == null) return `${game.minPlayers}`;
  if (game.minPlayers == null) return `fino a ${game.maxPlayers}`;
  return `${game.minPlayers}–${game.maxPlayers}`;
}

function GameFacts({ game }: { game: GameDetailsDto }): JSX.Element {
  const facts: [string, string | null][] = [
    ['Anno', game.yearPublished != null ? String(game.yearPublished) : null],
    ['Giocatori', players(game)],
    ['Durata', game.playingTime != null ? `${game.playingTime} min` : null],
    ['Complessità', game.complexity != null ? game.complexity.toFixed(1) : null],
    ['BGG', game.bggId != null ? String(game.bggId) : null],
  ];

  return (
    <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-sm sm:grid-cols-3">
      {facts.map(([label, value]) => (
        <div key={label}>
          <dt className="text-muted-foreground">{label}</dt>
          <dd>{value ?? '—'}</dd>
        </div>
      ))}
    </dl>
  );
}

function GameCard({ title, game }: { title: string; game: GameDetailsDto }): JSX.Element {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="font-quicksand text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex items-start gap-4">
          <SafeImg
            src={game.thumbnailUrl}
            alt=""
            className="h-20 w-20 shrink-0 rounded-md object-cover"
            fallback={
              <span className="flex h-20 w-20 shrink-0 items-center justify-center rounded-md bg-muted">
                <Gamepad2Icon className="h-6 w-6 text-muted-foreground" aria-hidden />
              </span>
            }
          />
          <div className="min-w-0 space-y-3">
            {/* eslint-disable-next-line local/prefer-use-game-title -- the review shows the title as sent: a proposal's
                private game has no catalog translations to resolve. */}
            <p className="font-medium">{game.title}</p>
            <GameFacts game={game} />
          </div>
        </div>
        <p className="whitespace-pre-line text-sm">
          {game.description?.trim() ? (
            game.description
          ) : (
            <span className="text-muted-foreground">Nessuna descrizione.</span>
          )}
        </p>
      </CardContent>
    </Card>
  );
}

function formatSize(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function ShareRequestDetail({ id }: { id: string }): JSX.Element {
  const { data: request, isLoading, isError } = useShareRequestDetails(id);

  const back = (
    <Link
      href="/admin/share-requests"
      className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
    >
      <ArrowLeftIcon className="h-4 w-4" aria-hidden />
      Richieste di condivisione
    </Link>
  );

  if (isLoading) {
    return (
      <div className="space-y-4" data-testid="share-request-loading">
        {back}
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-48 w-full rounded-xl" />
      </div>
    );
  }

  if (isError || !request) {
    return (
      <div className="space-y-4 text-foreground">
        {back}
        <p className="text-sm text-muted-foreground">Richiesta non trovata.</p>
      </div>
    );
  }

  const isProposal = request.contributionType === 'NewGameProposal';
  const target = request.targetSharedGame;

  return (
    <div className="space-y-6 text-foreground">
      {back}
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="font-quicksand text-2xl font-bold tracking-tight">
          {request.sourceGame.title}
        </h1>
        <ShareRequestStatusBadge status={request.status} />
      </div>
      <p className="text-sm text-muted-foreground">
        {CONTRIBUTION_TYPE_LABELS[request.contributionType]} · creata il{' '}
        {formatDateTime(request.createdAt)}
      </p>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <GameCard title={isProposal ? 'Gioco proposto' : 'Gioco'} game={request.sourceGame} />

          {target && (
            <Card>
              <CardHeader>
                <CardTitle className="font-quicksand text-base">Gioco nel catalogo</CardTitle>
              </CardHeader>
              <CardContent className="text-sm">
                <Link
                  href={`/admin/shared-games/${target.id}`}
                  className="font-medium hover:underline"
                >
                  {target.title}
                </Link>
                {target.yearPublished != null && (
                  <span className="text-muted-foreground"> ({target.yearPublished})</span>
                )}
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader>
              <CardTitle className="font-quicksand text-base">Note del contributore</CardTitle>
            </CardHeader>
            <CardContent className="whitespace-pre-line text-sm">
              {request.userNotes?.trim() || (
                <span className="text-muted-foreground">Nessuna nota.</span>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="font-quicksand text-base">Documenti</CardTitle>
            </CardHeader>
            <CardContent className="text-sm">
              {request.attachedDocuments.length === 0 ? (
                <span className="text-muted-foreground">Nessun documento allegato.</span>
              ) : (
                <ul className="space-y-1">
                  {request.attachedDocuments.map(doc => (
                    <li key={doc.documentId} className="flex items-center gap-2">
                      <FileTextIcon className="h-4 w-4 text-muted-foreground" aria-hidden />
                      <span>{doc.fileName}</span>
                      <span className="text-muted-foreground">{formatSize(doc.fileSize)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="space-y-6">
          <ReviewPanel request={request} />

          <Card>
            <CardHeader>
              <CardTitle className="font-quicksand text-base">Contributore</CardTitle>
            </CardHeader>
            <CardContent className="space-y-1 text-sm">
              <p className="font-medium">{request.contributor.userName}</p>
              {/* approvedContributions counts approved share requests, approvalRate divides it by all of them;
                  totalContributions counts something else (contributor records), so they are not shown as "x of y". */}
              <p className="text-muted-foreground">
                Richieste approvate: {request.contributor.approvedContributions} (
                {Math.round(request.contributor.approvalRate * 100)}%)
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
