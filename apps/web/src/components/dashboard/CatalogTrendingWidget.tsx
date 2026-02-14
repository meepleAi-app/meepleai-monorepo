/**
 * Catalog Trending Widget
 * Issue #4313: Display top trending games with percentage change indicators
 * Epic #3902: AI Insights & Recommendations
 */

'use client';

import { ArrowDown, ArrowRight, ArrowUp, TrendingUp } from 'lucide-react';
import Link from 'next/link';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

export interface TrendingGame {
  id: string;
  title: string;
  trendScore: number;
  percentageChange: number;
  imageUrl?: string;
}

export interface CatalogTrendingWidgetProps {
  games: TrendingGame[];
  period?: 'week' | 'month';
  lastUpdated?: Date;
  className?: string;
}

function getTrendIndicator(percentageChange: number) {
  if (percentageChange > 0) {
    return {
      icon: ArrowUp,
      color: 'text-green-600 dark:text-green-400',
      label: 'In crescita',
    };
  }
  if (percentageChange < 0) {
    return {
      icon: ArrowDown,
      color: 'text-red-600 dark:text-red-400',
      label: 'In calo',
    };
  }
  return {
    icon: ArrowRight,
    color: 'text-stone-500 dark:text-stone-400',
    label: 'Stabile',
  };
}

function formatPercentageChange(value: number): string {
  const sign = value > 0 ? '+' : '';
  return `${sign}${value}%`;
}

export function CatalogTrendingWidget({
  games,
  period = 'week',
  lastUpdated,
  className,
}: CatalogTrendingWidgetProps) {
  const displayGames = games.slice(0, 5); // Top 5 max
  const periodLabel = period === 'week' ? 'Settimana' : 'Mese';

  return (
    <Card className={cn('w-full', className)}>
      <CardHeader>
        <div className="flex items-start justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <TrendingUp className="h-5 w-5" />
              Trending
            </CardTitle>
            <CardDescription>I più popolari questa {periodLabel.toLowerCase()}</CardDescription>
          </div>
          {lastUpdated && (
            <span className="text-xs text-muted-foreground">
              Aggiornato {formatDistanceToNow(lastUpdated, { addSuffix: true, locale: it })}
            </span>
          )}
        </div>
      </CardHeader>
      <CardContent>
        {displayGames.length === 0 ? (
          <div className="text-center py-8 text-muted-foreground">
            <TrendingUp className="h-12 w-12 mx-auto mb-2 opacity-20" />
            <p>Nessun gioco in tendenza</p>
          </div>
        ) : (
          <div className="space-y-3">
            {/* Trending Games List */}
            <ol className="space-y-2">
              {displayGames.map((game, index) => {
                const trend = getTrendIndicator(game.percentageChange);
                const TrendIcon = trend.icon;

                return (
                  <li key={game.id}>
                    <Link
                      href={`/games/${game.id}`}
                      className="flex items-center gap-3 p-3 rounded-lg hover:bg-accent/50 transition-colors group"
                    >
                      {/* Rank Number */}
                      <div className="flex-shrink-0 w-6 h-6 flex items-center justify-center rounded-full bg-primary/10 text-primary font-semibold text-sm">
                        {index + 1}
                      </div>

                      {/* Game Info */}
                      <div className="flex-1 min-w-0">
                        <p className="font-medium text-sm truncate group-hover:text-primary transition-colors">
                          {game.title}
                        </p>
                      </div>

                      {/* Trend Indicator */}
                      <div className={cn('flex items-center gap-1 text-sm font-medium flex-shrink-0', trend.color)}>
                        <TrendIcon className="h-4 w-4" />
                        <span>{formatPercentageChange(game.percentageChange)}</span>
                      </div>
                    </Link>
                  </li>
                );
              })}
            </ol>

            {/* View Full Catalog CTA */}
            <div className="pt-3 border-t">
              <Button variant="ghost" className="w-full" asChild>
                <Link href="/games/catalog">
                  Vedi Catalogo Completo
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Link>
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
