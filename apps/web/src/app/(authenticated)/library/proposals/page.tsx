/**
 * My Proposals Dashboard Page
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.5)
 *
 * Dashboard showing user's game proposals with status tracking.
 */

'use client';

import { useEffect, useState } from 'react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

// Placeholder interface - extend when backend DTOs available
interface GameProposal {
  id: string;
  gameTitle: string;
  status: 'Pending' | 'InReview' | 'Approved' | 'Rejected' | 'ChangesRequested';
  submittedAt: string;
  sharedGameId?: string;
}

const STATUS_COLORS = {
  Pending: 'bg-yellow-100 text-yellow-800',
  InReview: 'bg-blue-100 text-blue-800',
  Approved: 'bg-green-100 text-green-800',
  Rejected: 'bg-red-100 text-red-800',
  ChangesRequested: 'bg-orange-100 text-orange-800',
} as const;

export default function MyProposalsPage() {
  const [proposals, setProposals] = useState<GameProposal[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadProposals();
  }, []);

  const loadProposals = async () => {
    setLoading(true);
    try {
      // TODO: Replace with actual API call when endpoint available
      // const data = await api.shareRequests.getMyProposals();
      // setProposals(data);

      // Placeholder empty state
      setProposals([]);
    } catch (error) {
      console.error('Failed to load proposals:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="container mx-auto py-8 space-y-6">
      <div>
        <h1 className="text-3xl font-bold">My Proposals</h1>
        <p className="text-muted-foreground mt-2">
          Track your game proposals submitted to the shared catalog
        </p>
      </div>

      {loading ? (
        <p>Loading proposals...</p>
      ) : proposals.length === 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>No Proposals Yet</CardTitle>
            <CardDescription>
              You haven't proposed any games to the shared catalog yet.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Add a private game to your library, then use the "Propose to Catalog" button to
              submit it for review.
            </p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4">
          {proposals.map((proposal) => (
            <Card key={proposal.id}>
              <CardHeader>
                <div className="flex items-start justify-between">
                  <div>
                    <CardTitle>{proposal.gameTitle}</CardTitle>
                    <CardDescription>
                      Submitted {new Date(proposal.submittedAt).toLocaleDateString()}
                    </CardDescription>
                  </div>
                  <Badge className={STATUS_COLORS[proposal.status]}>{proposal.status}</Badge>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex gap-2">
                  {proposal.status === 'Approved' && proposal.sharedGameId && (
                    <Button size="sm" asChild>
                      <a href={`/shared-games/${proposal.sharedGameId}`}>View in Catalog</a>
                    </Button>
                  )}
                  <Button size="sm" variant="outline" asChild>
                    <a href={`/contributions/requests/${proposal.id}`}>View Details</a>
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
