/**
 * Game Reviews Page - /games/[id]/reviews
 *
 * Shows paginated reviews for a game and allows authenticated users to write one.
 * Issue #4889: Replace "Coming Soon" placeholder with real implementation.
 * Issue #4904: Backend API for game reviews.
 */

'use client';

import { useEffect, useState } from 'react';

import { ArrowLeft, MessageSquare, Pencil, Star, User } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { GameReviewDto } from '@/lib/api';

const PAGE_SIZE = 10;

function StarRating({ rating }: { rating: number }) {
  return (
    <span className="flex items-center gap-0.5" aria-label={`Rating: ${rating} out of 10`}>
      {Array.from({ length: 10 }, (_, i) => (
        <Star
          key={i}
          className={`h-3.5 w-3.5 ${i < rating ? 'text-amber-400 fill-amber-400' : 'text-muted-foreground/30'}`}
        />
      ))}
      <span className="ml-1 text-xs font-nunito text-muted-foreground">{rating}/10</span>
    </span>
  );
}

function ReviewCard({ review }: { review: GameReviewDto }) {
  const date = new Date(review.createdAt).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
  return (
    <article className="border border-border/50 rounded-lg p-4">
      <div className="flex items-start justify-between gap-3 mb-2">
        <div className="flex items-center gap-2 text-sm font-nunito">
          <User className="h-4 w-4 text-muted-foreground" />
          <span className="font-medium">{review.authorName}</span>
          <span className="text-muted-foreground text-xs">{date}</span>
        </div>
        <StarRating rating={review.rating} />
      </div>
      <p className="text-sm font-nunito text-muted-foreground leading-relaxed">{review.content}</p>
    </article>
  );
}

interface WriteReviewFormProps {
  onSubmit: (authorName: string, rating: number, content: string) => Promise<void>;
  isSubmitting: boolean;
  submitError: string | null;
}

function WriteReviewForm({ onSubmit, isSubmitting, submitError }: WriteReviewFormProps) {
  const [authorName, setAuthorName] = useState('');
  const [rating, setRating] = useState(0);
  const [content, setContent] = useState('');
  const [hovered, setHovered] = useState(0);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!authorName.trim() || rating === 0 || !content.trim()) return;
    await onSubmit(authorName.trim(), rating, content.trim());
  };

  return (
    <Card className="mt-6">
      <CardHeader>
        <CardTitle className="font-quicksand text-lg flex items-center gap-2">
          <Pencil className="h-4 w-4" />
          Write a Review
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="author-name" className="text-sm font-medium font-nunito mb-1 block">
              Your Name
            </label>
            <Input
              id="author-name"
              value={authorName}
              onChange={e => setAuthorName(e.target.value)}
              placeholder="Enter your name"
              className="font-nunito"
              maxLength={100}
              required
            />
          </div>

          <div>
            <label className="text-sm font-medium font-nunito mb-1 block">Rating (1–10)</label>
            <div className="flex items-center gap-1" role="radiogroup" aria-label="Rating">
              {Array.from({ length: 10 }, (_, i) => {
                const val = i + 1;
                return (
                  <button
                    key={val}
                    type="button"
                    role="radio"
                    aria-checked={rating === val}
                    aria-label={`${val} stars`}
                    onClick={() => setRating(val)}
                    onMouseEnter={() => setHovered(val)}
                    onMouseLeave={() => setHovered(0)}
                    className="p-0.5 rounded focus:outline-none focus:ring-1 focus:ring-primary"
                  >
                    <Star
                      className={`h-5 w-5 transition-colors ${(hovered || rating) >= val ? 'text-amber-400 fill-amber-400' : 'text-muted-foreground/30'}`}
                    />
                  </button>
                );
              })}
              {rating > 0 && (
                <span className="ml-2 text-sm font-nunito text-muted-foreground">{rating}/10</span>
              )}
            </div>
          </div>

          <div>
            <label htmlFor="review-content" className="text-sm font-medium font-nunito mb-1 block">
              Review
            </label>
            <Textarea
              id="review-content"
              value={content}
              onChange={e => setContent(e.target.value)}
              placeholder="Share your thoughts about this game..."
              className="font-nunito resize-none min-h-[100px]"
              required
            />
          </div>

          {submitError && (
            <Alert variant="destructive">
              <AlertDescription className="font-nunito">{submitError}</AlertDescription>
            </Alert>
          )}

          <Button
            type="submit"
            disabled={isSubmitting || !authorName.trim() || rating === 0 || !content.trim()}
            className="font-nunito w-full sm:w-auto"
          >
            {isSubmitting ? 'Submitting...' : 'Submit Review'}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

export default function GameReviewsPage() {
  const params = useParams();
  const gameId = params?.id as string;

  const [reviews, setReviews] = useState<GameReviewDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [page, setPage] = useState(1);
  const [refreshKey, setRefreshKey] = useState(0);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  useEffect(() => {
    if (!gameId) return;
    setIsLoading(true);
    setError(null);
    api.games
      .getReviews(gameId, page, PAGE_SIZE)
      .then(result => {
        setReviews(result.items);
        setTotalCount(result.total);
      })
      .catch(err => setError(err instanceof Error ? err : new Error(String(err))))
      .finally(() => setIsLoading(false));
  }, [gameId, page, refreshKey]);

  const handleSubmitReview = async (authorName: string, rating: number, content: string) => {
    if (!gameId) return;
    setIsSubmitting(true);
    setSubmitError(null);
    try {
      await api.games.createReview(gameId, { authorName, rating, content });
      setSubmitted(true);
      // Reset to page 1 and force a refresh to show the new review
      setPage(1);
      setRefreshKey(k => k + 1);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to submit review';
      setSubmitError(message.includes('409') ? 'You have already reviewed this game.' : message);
    } finally {
      setIsSubmitting(false);
    }
  };

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
          <MessageSquare className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Reviews</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              Community ratings and written reviews
            </p>
          </div>
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <Skeleton key={i} className="h-24 w-full rounded-xl" />
            ))}
          </div>
        )}

        {/* Error */}
        {error && (
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">
              Failed to load reviews: {error.message}
            </AlertDescription>
          </Alert>
        )}

        {/* Content */}
        {!isLoading && !error && (
          <>
            {reviews.length === 0 ? (
              <Card>
                <CardContent className="py-12 text-center text-muted-foreground font-nunito">
                  No reviews yet. Be the first to review this game!
                </CardContent>
              </Card>
            ) : (
              <Card className="border-l-4 border-l-blue-400 shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl flex items-center gap-2">
                    <MessageSquare className="h-5 w-5 text-blue-500" />
                    {totalCount} Review{totalCount !== 1 ? 's' : ''}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3" role="list" aria-label="Game reviews">
                    {reviews.map(review => (
                      <ReviewCard key={review.id} review={review} />
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

        {/* Write Review Form */}
        {submitted ? (
          <Card className="mt-6 border-l-4 border-l-green-400">
            <CardContent className="py-6 text-center font-nunito text-green-600">
              Your review has been submitted successfully!
            </CardContent>
          </Card>
        ) : (
          <WriteReviewForm
            onSubmit={handleSubmitReview}
            isSubmitting={isSubmitting}
            submitError={submitError}
          />
        )}
      </div>
    </div>
  );
}
