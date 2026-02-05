/**
 * Game Gallery Page
 *
 * Showcase page for browsing games through an immersive carousel experience.
 * Features multiple carousel sections with different curation strategies.
 *
 * Issue #3586: GC-001 — Game Carousel API Integration
 * Epic: #3585 — GameCarousel Integration & Production Readiness
 */

'use client';

import { useCallback } from 'react';

import { AlertCircle, Clock, RefreshCw, Sparkles, TrendingUp, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import {
  GameCarousel,
  GameCarouselSkeleton,
  type CarouselGame,
} from '@/components/ui/data-display/game-carousel';
import { Button } from '@/components/ui/primitives/button';
import { useFeaturedGames, useTrendingGames } from '@/hooks/queries';
import { cn } from '@/lib/utils';

// ============================================================================
// Error Component
// ============================================================================

interface CarouselErrorProps {
  title: string;
  message?: string;
  onRetry?: () => void;
}

function CarouselError({ title, message, onRetry }: CarouselErrorProps) {
  return (
    <div className="py-8 md:py-12 px-4">
      <div className="text-center max-w-md mx-auto">
        <div
          className={cn(
            'w-16 h-16 mx-auto mb-4 rounded-full',
            'bg-destructive/10 flex items-center justify-center'
          )}
        >
          <AlertCircle className="w-8 h-8 text-destructive" />
        </div>
        <h3 className="font-semibold text-lg text-foreground mb-2">
          Unable to load {title}
        </h3>
        <p className="text-muted-foreground text-sm mb-4">
          {message || 'Something went wrong while loading the games. Please try again.'}
        </p>
        {onRetry && (
          <Button
            variant="outline"
            size="sm"
            onClick={onRetry}
            className="gap-2"
          >
            <RefreshCw className="w-4 h-4" />
            Try Again
          </Button>
        )}
      </div>
    </div>
  );
}

// ============================================================================
// Page Component
// ============================================================================

export default function GalleryPage() {
  const router = useRouter();

  // Fetch real data from API
  const {
    data: featuredData,
    isLoading: isLoadingFeatured,
    isError: isErrorFeatured,
    error: featuredError,
    refetch: refetchFeatured,
  } = useFeaturedGames(10);

  const {
    data: trendingData,
    isLoading: isLoadingTrending,
    isError: isErrorTrending,
    error: trendingError,
    refetch: refetchTrending,
  } = useTrendingGames(10);

  const handleGameSelect = useCallback(
    (game: CarouselGame) => {
      router.push(`/giochi/${game.id}`);
    },
    [router]
  );

  return (
    <div className="min-h-screen">
      {/* Hero Section */}
      <section
        className={cn(
          'relative overflow-hidden',
          'py-16 md:py-24',
          'bg-gradient-to-b from-primary/5 via-background to-background'
        )}
      >
        {/* Decorative elements */}
        <div className="absolute inset-0 pointer-events-none">
          <div className="absolute top-20 left-10 w-64 h-64 bg-primary/10 rounded-full blur-3xl" />
          <div className="absolute bottom-10 right-20 w-48 h-48 bg-accent/10 rounded-full blur-3xl" />
        </div>

        <div className="relative container-content text-center">
          <span
            className={cn(
              'inline-flex items-center gap-2',
              'px-4 py-1.5 rounded-full',
              'bg-primary/10 text-primary text-sm font-medium',
              'mb-6'
            )}
          >
            <Sparkles className="w-4 h-4" />
            Discover Your Next Adventure
          </span>

          <h1
            className={cn(
              'font-quicksand font-bold',
              'text-4xl md:text-5xl lg:text-6xl',
              'text-foreground mb-4',
              'leading-tight'
            )}
          >
            Game{' '}
            <span className="text-primary">Gallery</span>
          </h1>

          <p className="text-muted-foreground text-lg md:text-xl max-w-2xl mx-auto">
            Explore our curated collection of board games. Swipe through,
            discover new favorites, and start your next game night.
          </p>
        </div>
      </section>

      {/* Featured Games Carousel */}
      <section className="py-8 md:py-12">
        {isLoadingFeatured ? (
          <GameCarouselSkeleton />
        ) : isErrorFeatured ? (
          <CarouselError
            title="Featured Games"
            message={featuredError?.message}
            onRetry={() => refetchFeatured()}
          />
        ) : featuredData?.games && featuredData.games.length > 0 ? (
          <GameCarousel
            games={featuredData.games}
            title="Featured Games"
            subtitle="Top-rated games loved by our community"
            onGameSelect={handleGameSelect}
            showDots
          />
        ) : (
          <div className="py-8 text-center text-muted-foreground">
            No featured games available at this time.
          </div>
        )}
      </section>

      {/* Trending Games Carousel */}
      <section className="py-8 md:py-12 bg-muted/30">
        {isLoadingTrending ? (
          <GameCarouselSkeleton />
        ) : isErrorTrending ? (
          <CarouselError
            title="Trending Games"
            message={trendingError?.message}
            onRetry={() => refetchTrending()}
          />
        ) : trendingData?.games && trendingData.games.length > 0 ? (
          <GameCarousel
            games={trendingData.games}
            title="Trending Now"
            subtitle="What everyone is playing this week"
            onGameSelect={handleGameSelect}
            showDots
          />
        ) : (
          <div className="py-8 text-center text-muted-foreground">
            No trending games available at this time.
          </div>
        )}
      </section>

      {/* Quick Stats */}
      <section className="py-12 md:py-16 bg-card border-y border-border/50">
        <div className="container-content">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 md:gap-8">
            {[
              {
                value: featuredData?.total?.toLocaleString() ?? '---',
                label: 'Games',
                icon: Sparkles,
              },
              { value: '50K+', label: 'Players', icon: Users },
              { value: '100K+', label: 'Sessions', icon: Clock },
              { value: '4.8★', label: 'Rating', icon: TrendingUp },
            ].map((stat, index) => (
              <div
                key={stat.label}
                className={cn(
                  'text-center',
                  'animate-fade-in',
                  `[animation-delay:${index * 100}ms]`
                )}
              >
                <stat.icon className="w-6 h-6 mx-auto mb-2 text-primary" />
                <div className="font-quicksand font-bold text-2xl md:text-3xl text-foreground">
                  {stat.value}
                </div>
                <div className="text-muted-foreground text-sm">{stat.label}</div>
              </div>
            ))}
          </div>
        </div>
      </section>
    </div>
  );
}
