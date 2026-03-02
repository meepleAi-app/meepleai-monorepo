/**
 * Game Strategies Page - /games/[id]/strategies
 *
 * Shows paginated strategy guides for a game from the backend API.
 * Issue #4889: Replace "Coming Soon" placeholder with real implementation.
 * Issue #4903: Backend API for game strategies.
 */

'use client';

import { useEffect, useState } from 'react';

import { ArrowLeft, Lightbulb, Tag, ThumbsUp, User } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { GameStrategyDto } from '@/lib/api';

const PAGE_SIZE = 10;

function StrategyCard({ strategy }: { strategy: GameStrategyDto }) {
  return (
    <article className="border border-border/50 rounded-lg p-4 hover:bg-muted/20 transition-colors">
      <div className="flex items-start justify-between gap-3 mb-2">
        <h3 className="font-semibold font-quicksand text-base">{strategy.title}</h3>
        <span className="flex items-center gap-1 text-xs text-muted-foreground font-nunito shrink-0">
          <ThumbsUp className="h-3 w-3" />
          {strategy.upvotes}
        </span>
      </div>

      <p className="text-sm font-nunito text-muted-foreground leading-relaxed mb-3">
        {strategy.content}
      </p>

      <div className="flex items-center justify-between gap-2 flex-wrap">
        <div className="flex items-center gap-1 text-xs text-muted-foreground font-nunito">
          <User className="h-3 w-3" />
          {strategy.author}
        </div>
        {strategy.tags.length > 0 && (
          <div className="flex items-center gap-1 flex-wrap">
            <Tag className="h-3 w-3 text-muted-foreground" />
            {strategy.tags.map(tag => (
              <Badge key={tag} variant="secondary" className="text-xs font-nunito px-1.5 py-0">
                {tag}
              </Badge>
            ))}
          </div>
        )}
      </div>
    </article>
  );
}

export default function GameStrategiesPage() {
  const params = useParams();
  const gameId = params?.id as string;

  const [strategies, setStrategies] = useState<GameStrategyDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [page, setPage] = useState(1);

  useEffect(() => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    api.games
      .getStrategies(gameId, page, PAGE_SIZE)
      .then(result => {
        setStrategies(result.items);
        setTotalCount(result.total);
      })
      .catch(err => setError(err instanceof Error ? err : new Error(String(err))))
      .finally(() => setIsLoading(false));
  }, [gameId, page]);

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href={`/games/${gameId}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Game
          </Link>
        </Button>

        {/* Header */}
        <div className="flex items-center gap-3 mb-8">
          <Lightbulb className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Strategies</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              Tips, tactics, and winning strategies
            </p>
          </div>
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <Skeleton key={i} className="h-28 w-full rounded-xl" />
            ))}
          </div>
        )}

        {/* Error */}
        {error && (
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">
              Failed to load strategies: {error.message}
            </AlertDescription>
          </Alert>
        )}

        {/* Content */}
        {!isLoading && !error && (
          <>
            {strategies.length === 0 ? (
              <Card>
                <CardContent className="py-12 text-center text-muted-foreground font-nunito">
                  No strategies available for this game yet.
                </CardContent>
              </Card>
            ) : (
              <Card className="border-l-4 border-l-amber-400 shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl flex items-center gap-2">
                    <Lightbulb className="h-5 w-5 text-amber-500" />
                    {totalCount} Strateg{totalCount !== 1 ? 'ies' : 'y'}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3" role="list" aria-label="Game strategies">
                    {strategies.map(strategy => (
                      <StrategyCard key={strategy.id} strategy={strategy} />
                    ))}
                  </div>

                  {/* Pagination */}
                  {totalPages > 1 && (
                    <div className="flex items-center justify-between mt-6 pt-4 border-t border-border/50">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => p - 1)}
                        disabled={page === 1}
                        className="font-nunito"
                      >
                        Previous
                      </Button>
                      <span className="text-sm text-muted-foreground font-nunito">
                        Page {page} of {totalPages}
                      </span>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => p + 1)}
                        disabled={page >= totalPages}
                        className="font-nunito"
                      >
                        Next
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>
            )}
          </>
        )}
      </div>
    </div>
  );
}
