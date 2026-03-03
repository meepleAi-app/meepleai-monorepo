/**
 * My Proposals Client Component
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.5)
 * Issue #4056: Extracted from page.tsx for RequireRole wrapping
 * Issue #4054: Connected to real API via useShareRequests hook
 *
 * Dashboard showing user's game proposals with status tracking.
 */

'use client';

import { AlertCircle, Loader2, AlertTriangle, Info, Plus } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { useShareRequests, useRateLimitStatus } from '@/hooks/queries/useShareRequests';
import type { ShareRequestStatus } from '@/lib/api/schemas/share-requests.schemas';

const STATUS_COLORS: Record<ShareRequestStatus, string> = {
  Pending: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400',
  InReview: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
  Approved: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400',
  Rejected: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400',
  ChangesRequested: 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400',
  Withdrawn: 'bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400',
};

const STATUS_LABELS: Record<ShareRequestStatus, string> = {
  Pending: 'In Attesa',
  InReview: 'In Revisione',
  Approved: 'Approvata',
  Rejected: 'Rifiutata',
  ChangesRequested: 'Modifiche Richieste',
  Withdrawn: 'Ritirata',
};

export default function MyProposalsClient() {
  const router = useRouter();
  const { data, isLoading, error } = useShareRequests();
  const { data: rateLimitData } = useRateLimitStatus();

  // Calculate quota usage percentages
  const pendingUsage = rateLimitData
    ? (rateLimitData.currentPendingCount / rateLimitData.maxPendingAllowed) * 100
    : 0;
  const monthlyUsage = rateLimitData
    ? (rateLimitData.currentMonthlyCount / rateLimitData.maxMonthlyAllowed) * 100
    : 0;

  // Error state
  if (error) {
    return (
      <div className="container mx-auto py-4 space-y-4">
        <Card className="border-destructive">
          <CardHeader>
            <div className="flex items-center gap-2">
              <AlertCircle className="h-5 w-5 text-destructive" />
              <CardTitle className="text-destructive">Errore di Caricamento</CardTitle>
            </div>
            <CardDescription>{error.message}</CardDescription>
          </CardHeader>
          <CardContent>
            <Button onClick={() => window.location.reload()} size="sm">
              Riprova
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-4 space-y-4">
      {/* Compact header — subtitle + new proposal button */}
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          Traccia le tue proposte di giochi inviate al catalogo condiviso
        </p>
        <Button size="sm" onClick={() => router.push('/library/propose')}>
          <Plus className="h-4 w-4 mr-2" />
          Nuova Proposta
        </Button>
      </div>

      {/* Rate Limit Status Banner */}
      {rateLimitData && (
        <Card
          className={
            rateLimitData.isInCooldown
              ? 'border-destructive bg-destructive/5'
              : pendingUsage >= 80 || monthlyUsage >= 80
                ? 'border-orange-500 bg-orange-50 dark:bg-orange-950/20'
                : 'border-blue-500 bg-blue-50 dark:bg-blue-950/20'
          }
        >
          <CardHeader>
            <div className="flex items-start gap-3">
              {rateLimitData.isInCooldown ? (
                <AlertTriangle className="h-5 w-5 text-destructive shrink-0" />
              ) : pendingUsage >= 80 || monthlyUsage >= 80 ? (
                <AlertTriangle className="h-5 w-5 text-orange-600 dark:text-orange-400 shrink-0" />
              ) : (
                <Info className="h-5 w-5 text-blue-600 dark:text-blue-400 shrink-0" />
              )}
              <div className="flex-1 space-y-3">
                <div>
                  <CardTitle className="text-base">
                    {rateLimitData.isInCooldown ? 'Cooldown Attivo' : 'Limiti di Proposta'}
                  </CardTitle>
                  {rateLimitData.isInCooldown && rateLimitData.cooldownEndsAt && (
                    <CardDescription className="text-destructive">
                      Cooldown termina il{' '}
                      {new Date(rateLimitData.cooldownEndsAt).toLocaleString('it-IT')}
                    </CardDescription>
                  )}
                </div>

                {/* Pending Quota */}
                <div className="space-y-1.5">
                  <div className="flex justify-between text-sm">
                    <span className="font-medium">Proposte Pendenti</span>
                    <span className="text-muted-foreground">
                      {rateLimitData.currentPendingCount} / {rateLimitData.maxPendingAllowed}
                    </span>
                  </div>
                  <Progress
                    value={pendingUsage}
                    className="h-2"
                    aria-label={`Quota proposte pendenti: ${pendingUsage.toFixed(0)}%`}
                  />
                  {pendingUsage >= 100 && (
                    <p className="text-xs text-destructive">
                      Limite raggiunto. Attendi che le proposte pendenti siano revisionate.
                    </p>
                  )}
                </div>

                {/* Monthly Quota */}
                <div className="space-y-1.5">
                  <div className="flex justify-between text-sm">
                    <span className="font-medium">Proposte Mensili</span>
                    <span className="text-muted-foreground">
                      {rateLimitData.currentMonthlyCount} / {rateLimitData.maxMonthlyAllowed}
                    </span>
                  </div>
                  <Progress
                    value={monthlyUsage}
                    className="h-2"
                    aria-label={`Quota proposte mensili: ${monthlyUsage.toFixed(0)}%`}
                  />
                  {rateLimitData.monthResetAt && (
                    <p className="text-xs text-muted-foreground">
                      Reset: {new Date(rateLimitData.monthResetAt).toLocaleDateString('it-IT')}
                    </p>
                  )}
                </div>
              </div>
            </div>
          </CardHeader>
        </Card>
      )}

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          <span className="ml-3 text-muted-foreground">Caricamento proposte...</span>
        </div>
      ) : !data || data.items.length === 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>Nessuna Proposta</CardTitle>
            <CardDescription>
              Non hai ancora proposto giochi per il catalogo condiviso.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground mb-4">
              Aggiungi un gioco privato alla tua libreria, poi usa il pulsante &quot;Proponi per il
              Catalogo&quot; per inviarlo in revisione.
            </p>
            <Button asChild variant="default">
              <Link href="/library/private">Vai ai Giochi Privati</Link>
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4">
          {data.items.map(proposal => (
            <Card key={proposal.id}>
              <CardHeader>
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <CardTitle>{proposal.gameTitle}</CardTitle>
                    <CardDescription>
                      Inviata il {new Date(proposal.createdAt).toLocaleDateString('it-IT')}
                      {proposal.resolvedAt && (
                        <>
                          {' '}
                          · Risolta il {new Date(proposal.resolvedAt).toLocaleDateString('it-IT')}
                        </>
                      )}
                    </CardDescription>
                    {proposal.userNotes && (
                      <p className="text-sm text-muted-foreground mt-2">{proposal.userNotes}</p>
                    )}
                    {proposal.adminFeedback && (
                      <div className="mt-3 p-3 bg-muted rounded-lg">
                        <p className="text-sm font-medium mb-1">Feedback Amministratore:</p>
                        <p className="text-sm text-muted-foreground">{proposal.adminFeedback}</p>
                      </div>
                    )}
                  </div>
                  <Badge className={STATUS_COLORS[proposal.status]}>
                    {STATUS_LABELS[proposal.status]}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex gap-2">
                  {proposal.status === 'Approved' && proposal.resultingSharedGameId && (
                    <Button size="sm" asChild>
                      <Link href={`/games/${proposal.resultingSharedGameId}`}>
                        Vedi nel Catalogo
                      </Link>
                    </Button>
                  )}
                  <Button size="sm" variant="outline" asChild>
                    <Link href={`/library/proposals/${proposal.id}`}>Dettagli</Link>
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}

          {/* Pagination info */}
          {data.totalCount > data.items.length && (
            <Card className="bg-muted/50">
              <CardContent className="py-4">
                <p className="text-sm text-center text-muted-foreground">
                  Mostrate {data.items.length} di {data.totalCount} proposte
                </p>
              </CardContent>
            </Card>
          )}
        </div>
      )}
    </div>
  );
}
