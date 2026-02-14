"use client";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import Link from "next/link";
import { Star } from "lucide-react";

interface WishlistGame {
  id: string;
  title: string;
  priority: number; // 1-10
  imageUrl?: string;
}

interface WishlistHighlightsProps {
  games?: WishlistGame[];
  maxItems?: number;
  onGameClick?: (gameId: string) => void;
}

function PriorityStars({ priority }: { priority: number }) {
  const stars = Math.min(Math.max(Math.round(priority / 2), 0), 5);
  return (
    <div className="flex gap-0.5">
      {Array.from({ length: 5 }).map((_, i) => (
        <Star
          key={i}
          className={`h-3 w-3 ${i < stars ? "fill-amber-400 text-amber-400" : "text-stone-300 dark:text-stone-700"}`}
        />
      ))}
    </div>
  );
}

export function WishlistHighlights({ games = [], maxItems = 5, onGameClick }: WishlistHighlightsProps) {
  if (games.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="font-quicksand">Wishlist</CardTitle>
          <CardDescription>Nessun gioco in wishlist</CardDescription>
        </CardHeader>
        <CardContent>
          <Button asChild variant="outline" className="w-full">
            <Link href="/games/catalog">Esplora Catalogo</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  const displayGames = games.slice(0, maxItems);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="font-quicksand">Wishlist</CardTitle>
        <CardDescription>I tuoi giochi più desiderati</CardDescription>
      </CardHeader>
      <CardContent className="space-y-2">
        {displayGames.map((game) => (
          <Link
            key={game.id}
            href={`/games/${game.id}`}
            onClick={() => onGameClick?.(game.id)}
            className="block group"
          >
            <div className="flex items-center justify-between p-2 rounded-md hover:bg-muted/50 transition-colors">
              <div className="flex-1 min-w-0">
                <p className="font-nunito text-sm font-medium truncate text-stone-900 dark:text-stone-100">
                  {game.title}
                </p>
                <PriorityStars priority={game.priority} />
              </div>
            </div>
          </Link>
        ))}
        {games.length > maxItems && (
          <Button asChild variant="link" className="w-full mt-2">
            <Link href="/wishlist">Gestisci Wishlist →</Link>
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
