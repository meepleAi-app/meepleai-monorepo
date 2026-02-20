/**
 * Game Strategies Page - /games/[id]/strategies
 *
 * Coming Soon placeholder - backend API for game strategies is not yet implemented.
 * A GitHub issue has been created to track backend implementation.
 *
 * @see Issue #4889
 */

'use client';

import { ArrowLeft, Lightbulb, Rocket } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

export default function GameStrategiesPage() {
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
          <Lightbulb className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Strategies</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              Tips, tactics, and winning strategies
            </p>
          </div>
        </div>

        {/* Coming Soon Card */}
        <Card className="border-l-4 border-l-amber-400 shadow-lg">
          <CardContent className="py-16 flex flex-col items-center text-center gap-6">
            <div className="rounded-full bg-amber-100 p-6">
              <Rocket className="h-12 w-12 text-amber-600" />
            </div>
            <div>
              <h2 className="text-2xl font-bold font-quicksand mb-2">Coming Soon</h2>
              <p className="text-muted-foreground font-nunito max-w-md">
                Game strategies are on our roadmap. Soon you&apos;ll be able to browse and share
                opening moves, mid-game tactics, and winning strategies for this game.
              </p>
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
