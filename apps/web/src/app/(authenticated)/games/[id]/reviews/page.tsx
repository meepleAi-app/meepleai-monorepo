/**
 * Game Reviews Page - /games/[id]/reviews
 *
 * Coming Soon placeholder - backend API for game reviews is not yet implemented.
 * A GitHub issue has been created to track backend implementation.
 *
 * @see Issue #4889
 */

'use client';

import { ArrowLeft, MessageSquare, Rocket, Star } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

export default function GameReviewsPage() {
  const params = useParams();
  const gameId = params?.id as string;

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

        {/* Coming Soon Card */}
        <Card className="border-l-4 border-l-blue-400 shadow-lg">
          <CardContent className="py-16 flex flex-col items-center text-center gap-6">
            <div className="rounded-full bg-blue-100 p-6">
              <Rocket className="h-12 w-12 text-blue-600" />
            </div>
            <div>
              <h2 className="text-2xl font-bold font-quicksand mb-2">Coming Soon</h2>
              <p className="text-muted-foreground font-nunito max-w-md">
                Community reviews are on our roadmap. Soon you&apos;ll be able to read and write
                detailed reviews, rate games, and see what other players think.
              </p>
            </div>
            <div className="flex flex-wrap items-center justify-center gap-4 text-sm text-muted-foreground font-nunito">
              <span className="flex items-center gap-1">
                <Star className="h-4 w-4 text-amber-400" />
                Star ratings
              </span>
              <span className="flex items-center gap-1">
                <MessageSquare className="h-4 w-4 text-blue-400" />
                Written reviews
              </span>
            </div>
            <div className="flex flex-col sm:flex-row gap-3">
              <Button asChild variant="outline" className="font-nunito">
                <Link href={`/games/${gameId}`}>Return to Game</Link>
              </Button>
              <Button asChild variant="outline" className="font-nunito">
                <Link href={`/games/${gameId}/faqs`}>Browse FAQs instead</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
