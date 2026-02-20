/**
 * Game FAQs Page - /games/[id]/faqs
 *
 * Shows paginated FAQs for a game using api.games.getFAQs(id, limit, offset).
 * Accordion-style expandable questions with upvote counts.
 *
 * @see Issue #4889
 */

'use client';

import { useEffect, useState } from 'react';

import { ArrowLeft, ChevronDown, ChevronRight, HelpCircle, ThumbsUp } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { GameFAQ } from '@/lib/api/schemas/games.schemas';

const PAGE_SIZE = 10;

function FAQItem({ faq }: { faq: GameFAQ }) {
  const [open, setOpen] = useState(false);

  return (
    <li className="border border-border/50 rounded-lg overflow-hidden">
      <button
        onClick={() => setOpen(v => !v)}
        className="flex items-start justify-between w-full px-4 py-3 text-left hover:bg-muted/40 transition-colors gap-3"
        aria-expanded={open}
      >
        <div className="flex items-start gap-2 flex-1 min-w-0">
          <HelpCircle className="h-4 w-4 text-primary mt-0.5 shrink-0" />
          <span className="font-medium font-nunito text-sm">{faq.question}</span>
        </div>
        <div className="flex items-center gap-2 shrink-0 text-muted-foreground">
          <span className="text-xs font-nunito flex items-center gap-1">
            <ThumbsUp className="h-3 w-3" />
            {faq.upvotes}
          </span>
          {open ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        </div>
      </button>
      {open && (
        <div className="px-4 pb-4 pt-1 bg-muted/20 border-t border-border/50">
          <p className="text-sm font-nunito text-muted-foreground leading-relaxed">{faq.answer}</p>
        </div>
      )}
    </li>
  );
}

export default function GameFaqsPage() {
  const params = useParams();
  const gameId = params?.id as string;

  const [faqs, setFaqs] = useState<GameFAQ[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [page, setPage] = useState(0);

  useEffect(() => {
    if (!gameId) return;
    setIsLoading(true);
    api.games
      .getFAQs(gameId, PAGE_SIZE, page * PAGE_SIZE)
      .then(result => {
        setFaqs(result.faqs);
        setTotalCount(result.totalCount);
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
          <HelpCircle className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">FAQs</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              Frequently asked questions about this game
            </p>
          </div>
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <Skeleton key={i} className="h-14 w-full rounded-xl" />
            ))}
          </div>
        )}

        {/* Error */}
        {error && (
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">
              Failed to load FAQs: {error.message}
            </AlertDescription>
          </Alert>
        )}

        {/* Content */}
        {!isLoading && !error && (
          <>
            {faqs.length === 0 ? (
              <Card>
                <CardContent className="py-12 text-center text-muted-foreground font-nunito">
                  No FAQs available for this game yet.
                </CardContent>
              </Card>
            ) : (
              <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl flex items-center gap-2">
                    <HelpCircle className="h-5 w-5 text-primary" />
                    {totalCount} Question{totalCount !== 1 ? 's' : ''}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-2" role="list">
                    {faqs.map(faq => (
                      <FAQItem key={faq.id} faq={faq} />
                    ))}
                  </ul>

                  {/* Pagination */}
                  {totalPages > 1 && (
                    <div className="flex items-center justify-between mt-6 pt-4 border-t border-border/50">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => p - 1)}
                        disabled={page === 0}
                        className="font-nunito"
                      >
                        Previous
                      </Button>
                      <span className="text-sm text-muted-foreground font-nunito">
                        Page {page + 1} of {totalPages}
                      </span>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => p + 1)}
                        disabled={page >= totalPages - 1}
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
