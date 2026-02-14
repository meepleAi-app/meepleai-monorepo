"use client";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import Link from "next/link";
import { TrendingUp, TrendingDown, Minus } from "lucide-react";
import { formatDistanceToNow } from "date-fns";
import { it } from "date-fns/locale";

interface TrendingGame {
  id: string;
  title: string;
  trendScore: number;
  percentageChange: number;
  imageUrl?: string;
}

interface CatalogTrendingWidgetProps {
  games?: TrendingGame[];
  period?: "week" | "month";
  lastUpdated?: Date;
}

function TrendIndicator({ change }: { change: number }) {
  if (change > 5) {
    return (
      <div className="flex items-center gap-1 text-green-600 dark:text-green-400">
        <TrendingUp className="h-4 w-4" />
        <span className="text-xs font-semibold">+{change.toFixed(0)}%</span>
      </div>
    );
  }
  if (change < -5) {
    return (
      <div className="flex items-center gap-1 text-red-600 dark:text-red-400">
        <TrendingDown className="h-4 w-4" />
        <span className="text-xs font-semibold">{change.toFixed(0)}%</span>
      </div>
    );
  }
  return (
    <div className="flex items-center gap-1 text-stone-500 dark:text-stone-400">
      <Minus className="h-4 w-4" />
      <span className="text-xs">~{change.toFixed(0)}%</span>
    </div>
  );
}

export function CatalogTrendingWidget({ games = [], period = "week", lastUpdated }: CatalogTrendingWidgetProps) {
  if (games.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="font-quicksand">Trending</CardTitle>
          <CardDescription>Nessun dato disponibile</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="font-quicksand">Trending nella Community</CardTitle>
        <CardDescription>
          I più popolari {period === "week" ? "della settimana" : "del mese"}
          {lastUpdated && ` • ${formatDistanceToNow(lastUpdated, { addSuffix: true, locale: it })}`}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-2">
        {games.slice(0, 5).map((game, index) => (
          <Link
            key={game.id}
            href={`/games/${game.id}`}
            className="block group"
          >
            <div className="flex items-center gap-3 p-2 rounded-md hover:bg-muted/50 transition-colors">
              <div className="text-lg font-bold text-stone-400 dark:text-stone-600 w-6 text-center">
                {index + 1}
              </div>
              <div className="flex-1 min-w-0">
                <p className="font-nunito text-sm font-medium truncate text-stone-900 dark:text-stone-100">
                  {game.title}
                </p>
              </div>
              <TrendIndicator change={game.percentageChange} />
            </div>
          </Link>
        ))}
        <Button asChild variant="link" className="w-full mt-2">
          <Link href="/games/catalog">Vedi Catalogo Completo →</Link>
        </Button>
      </CardContent>
    </Card>
  );
}
